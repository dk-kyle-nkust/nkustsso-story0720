// 處理 Web API 單一登入導向與一次性票券兌換。
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.ApiModels;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

/// <summary>
/// Web API 單一登入（自訂協定）的 HTTP 介面；瀏覽器取得票券，接收系統後端再兌換身分資料。
/// </summary>
[RequireHttps]
public sealed class WebApiSsoController(LoginTicketService tickets) : Controller
{
    /// <summary>
    /// 檢查接收系統、精確回呼網址與 PKCE，再以中央登入核發票券並導回接收端。
    /// </summary>
    [HttpGet("/ticket/authorize")]
    public async Task<IActionResult> AuthorizeTicket([FromQuery] SsoAuthorizeRequest request)
    {
        if (!ModelState.IsValid || request.CodeChallengeMethod != "S256")
        {
            return BadRequest(new { error = "invalid_request" });
        }

        var client = await tickets.FindClientAsync(request.ClientId);
        if (client is null)
        {
            return BadRequest(new { error = "invalid_client_or_redirect" });
        }

        // 回呼網址必須與登記值完全相同，不接受前綴或大小寫不同的變形。
        if (!string.Equals(client.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "invalid_client_or_redirect" });
        }

        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        string? sessionId = null;
        auth.Properties?.Items.TryGetValue(LoginSessionStore.SessionProperty, out sessionId);
        var subject = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!auth.Succeeded || subject is null || sessionId is null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Request.Path + Request.QueryString
            };
            return Challenge(properties, IdentityConstants.ApplicationScheme);
        }

        var ticket = await tickets.IssueAsync(
            client,
            subject,
            sessionId,
            request.CodeChallenge,
            RemoteIp());
        if (ticket is null)
        {
            return BadRequest(new { error = "session_inactive" });
        }

        var query = new Dictionary<string, string?>
        {
            ["ticket"] = ticket,
            ["state"] = request.State
        };
        return Redirect(QueryHelpers.AddQueryString(client.RedirectUri, query));
    }

    /// <summary>
    /// 接收後端 POST：驗證系統密鑰、票券與 PKCE，成功才回傳允許提供的身分資料。
    /// </summary>
    [HttpPost("/ticket/redeem")]
    [IgnoreAntiforgeryToken] // 此端點供後端兌換，以系統密鑰、PKCE 及一次性票券驗證，不使用瀏覽器 Cookie 授權。
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Redeem([FromForm] SsoTicketFormRedemptionRequest request)
    {
        return await RedeemCore(request.Ticket, request.RedirectUri, request.CodeVerifier);
    }

    /// <summary>供接收系統後端以 JSON 兌換中央登入票券；與既有表單端點共用一次性消耗與欄位授權。</summary>
    [HttpPost("/api/sso/v1/redeem")]
    [Consumes("application/json")]
    [IgnoreAntiforgeryToken] // 以系統密鑰及 PKCE 驗證，不以瀏覽器 Cookie 授權。
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> RedeemJson([FromBody] SsoTicketRedemptionRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        return await RedeemCore(request.Ticket, request.RedirectUri, request.CodeVerifier);
    }

    private async Task<IActionResult> RedeemCore(string ticket, string redirectUri, string codeVerifier)
    {
        if (!TryClientCredentials(out var id, out var secret))
        {
            return Unauthorized(new { error = "invalid_client" });
        }

        var client = await tickets.AuthenticateClientAsync(id, secret);
        if (client is null)
        {
            await tickets.RecordAsync(id, "redeem", "invalid_client", RemoteIp());
            return Unauthorized(new { error = "invalid_client" });
        }

        // 先驗系統密鑰再看表單欄位，避免未授權的呼叫端從錯誤訊息分辨欄位是否正確。
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        var result = await tickets.RedeemAsync(client, ticket, redirectUri, codeVerifier, RemoteIp());
        if (result is null)
        {
            return BadRequest(new { error = "invalid_ticket" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 取得目前連線來源 IP；不直接相信用戶端自行送入的轉送標頭。
    /// </summary>
    private string RemoteIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    /// <summary>
    /// 解析 HTTP Basic 的 Client ID 與密鑰並限制長度；Base64 是編碼，傳輸仍須使用 HTTPS。
    /// </summary>
    private bool TryClientCredentials(out string id, out string secret)
    {
        id = secret = "";

        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header))
        {
            return false;
        }

        if (!string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (header.Parameter is null || header.Parameter.Length < 1 || header.Parameter.Length > 1024)
        {
            return false;
        }

        try
        {
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter)).Split(':', 2);
            if (pair.Length != 2)
            {
                return false;
            }

            if (pair[0].Length < 1 || pair[0].Length > 100)
            {
                return false;
            }

            if (pair[1].Length < 1 || pair[1].Length > 256)
            {
                return false;
            }

            id = pair[0];
            secret = pair[1];
            return true;
        }
        catch (FormatException)
        {
            // 不是合法的 Base64，視為未提供憑證。
            return false;
        }
    }
}
