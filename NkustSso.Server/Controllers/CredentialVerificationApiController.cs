// 提供 JSON 與舊 SOAP 帳密驗證介面及說明。
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NkustSso.Server.ApiModels;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

// 呼叫端以來源 IP／系統密鑰授權，不使用瀏覽器 Cookie 與防偽 Token。
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("service-verification")]
[RequestSizeLimit(16_384)]
public sealed class CredentialVerificationApiController(
    IntegrationClientRegistrationService registry,
    CredentialVerificationService verification,
    IOptions<IntegrationServiceOptions> options,
    IConfiguration configuration,
    ICampusSoapQueries? queries = null) : Controller
{
    /// <summary>JSON 說明頁允許帶入的系統識別碼格式。</summary>
    private const string ClientIdPattern = "^[a-z0-9][a-z0-9_-]{0,99}$";

    [HttpGet(LegacySoapContract.Path)]
    public IActionResult Contract(string? op)
    {
        // ?wsdl 沿用舊服務的慣例，直接回傳合約文件。
        if (Request.Query.Keys.Any(k => string.Equals(k, "wsdl", StringComparison.OrdinalIgnoreCase)))
        {
            return Content(LegacySoapContract.Wsdl(configuration["Sso:Issuer"]!), "text/xml; charset=utf-8");
        }

        var operation = op is null
            ? null
            : SoapHelpCatalog.Operations.SingleOrDefault(o => o.Name == op);
        if (op is not null && operation is null)
        {
            return NotFound();
        }

        var address = new Uri(new Uri(configuration["Sso:Issuer"]!), "KuasSSOWebService.asmx").AbsoluteUri;
        var model = new SoapApiGuideViewModel(operation, address, options.Value.SoapEnabled);
        return View("~/Views/CredentialVerificationApi/SoapApiGuide.cshtml", model);
    }

    [HttpGet("/api/integration")]
    public IActionResult JsonHelp(string? clientId)
    {
        if (clientId is not null && !Regex.IsMatch(clientId, ClientIdPattern))
        {
            return BadRequest();
        }

        var address = new Uri(new Uri(configuration["Sso:Issuer"]!), "api/integration/v1/verify").AbsoluteUri;
        var model = new JsonApiGuideViewModel(address, options.Value.JsonEnabled, clientId ?? "");
        return View("~/Views/CredentialVerificationApi/JsonApiGuide.cshtml", model);
    }

    [HttpPost(LegacySoapContract.Path)]
    public async Task<IActionResult> Soap(CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentType))
        {
            return StatusCode(415);
        }

        // SOAP 1.1 用 text/xml，SOAP 1.2 用 application/soap+xml，兩者的錯誤格式不同。
        var soap12 = contentType.MediaType.Equals("application/soap+xml", StringComparison.OrdinalIgnoreCase);
        if (!soap12 && !contentType.MediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(415);
        }

        if (!options.Value.SoapEnabled)
        {
            return SoapFault(soap12, true, 503);
        }

        var action = ReadSoapAction(contentType, soap12);

        LegacySoapRequest input;
        try
        {
            input = await LegacySoapContract.ReadRequestAsync(Request.Body, soap12, cancellationToken);
        }
        catch (XmlException)
        {
            return SoapFault(soap12, false);
        }

        // SOAP 1.2 可以不帶 action；有帶就必須和內容的方法一致。
        if ((!soap12 || action is not null) && action?.Trim('"') != input.Operation.Action)
        {
            return SoapFault(soap12, false);
        }

        var client = await registry.AuthorizeAsync(
            "soap",
            HttpContext.Connection.RemoteIpAddress,
            service: LegacySoapOperationCatalog.Permission(input.Operation.Name));
        if (client is null)
        {
            return SoapFault(soap12, false, 403);
        }

        return await ExecuteSoapAsync(input, client, soap12, cancellationToken);
    }

    private string? ReadSoapAction(MediaTypeHeaderValue contentType, bool soap12)
    {
        if (!soap12)
        {
            return Request.Headers["SOAPAction"].ToString();
        }

        return contentType.Parameters
            .FirstOrDefault(p => p.Name.Equals("action", StringComparison.OrdinalIgnoreCase))
            ?.Value.Value;
    }

    private async Task<IActionResult> ExecuteSoapAsync(
        LegacySoapRequest input,
        IntegrationClient client,
        bool soap12,
        CancellationToken cancellationToken)
    {
        try
        {
            var fields = await registry.ReturnFieldsAsync(client.Id, cancellationToken);
            var result = await LegacySoapOperationDispatcher.ExecuteAsync(
                input,
                client,
                fields,
                verification,
                queries,
                soap12,
                cancellationToken);

            var outcome = result.Unavailable ? "unavailable" : result.Outcome;
            await registry.RecordAsync(client.Id, Ip(), input.Operation.Name + ":" + outcome, cancellationToken);

            if (result.Unavailable)
            {
                return SoapFault(soap12, true);
            }

            return Content(result.Xml, SoapContentType(soap12));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // 不把內部錯誤內容回給呼叫端，只留下一筆 unavailable 紀錄。
            await registry.RecordAsync(client.Id, Ip(), input.Operation.Name + ":unavailable", cancellationToken);
            return SoapFault(soap12, true);
        }
    }

    [HttpPost("/api/integration/v1/verify")]
    public async Task<IActionResult> Json(CancellationToken cancellationToken)
    {
        if (!options.Value.JsonEnabled)
        {
            return StatusCode(503, new { error = "service_unavailable" });
        }

        var client = await registry.AuthorizeAsync(
            "json",
            HttpContext.Connection.RemoteIpAddress,
            Request.Headers["X-Client-Id"].ToString(),
            Request.Headers["X-Client-Secret"].ToString());
        if (client is null)
        {
            return StatusCode(403, new { error = "client_not_allowed" });
        }

        if (!Request.HasJsonContentType())
        {
            return StatusCode(415);
        }

        CredentialVerificationRequest? input;
        try
        {
            input = await Request.ReadFromJsonAsync<CredentialVerificationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        if (input is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        var result = await verification.VerifyAsync(input.UserName, input.Password, cancellationToken);

        var outcome = result.Unavailable
            ? "unavailable"
            : result.Verified ? "success" : "failed";
        await registry.RecordAsync(client.Id, Ip(), outcome, cancellationToken);

        if (result.Unavailable)
        {
            return StatusCode(503, new { error = "service_unavailable" });
        }

        return Ok(new { verified = result.Verified });
    }

    private ContentResult SoapFault(bool soap12, bool serverError, int status = 500)
    {
        return new ContentResult
        {
            StatusCode = status,
            Content = LegacySoapContract.Fault(soap12, serverError),
            ContentType = SoapContentType(soap12)
        };
    }

    private static string SoapContentType(bool soap12)
    {
        return soap12 ? "application/soap+xml; charset=utf-8" : "text/xml; charset=utf-8";
    }

    /// <summary>
    /// 取得目前連線來源 IP；不直接相信用戶端自行送入的轉送標頭。
    /// </summary>
    private string Ip()
    {
        if (HttpContext.Connection.RemoteIpAddress is not { } ip)
        {
            return "";
        }

        return IntegrationClientRegistrationService.NormalizeIp(ip);
    }
}
