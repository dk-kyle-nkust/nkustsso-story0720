// 處理 SAML 單一登入請求及簽章回應。
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ITfoxtec.Identity.Saml2;
using LinqToDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

[RequireHttps]
public sealed class SamlSsoController(
    SamlIdentityProvider saml,
    DatabaseFactory database,
    LoginSessionStore sessions,
    TimeProvider clock,
    ILogger<SamlSsoController> logger) : Controller
{
    /// <summary>登入流程最多保留 5 分鐘，逾時就得從接收系統重新開始。</summary>
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(5);

    /// <summary>流程識別碼是 32 位元組的 Base64Url，固定 43 個字元。</summary>
    private const string FlowIdPattern = "^[A-Za-z0-9_-]{43}$";

    [HttpGet("/saml/metadata")]
    public IActionResult Metadata()
    {
        return Content(saml.Metadata(), "application/samlmetadata+xml", Encoding.UTF8);
    }

    [HttpGet("/saml/sso")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> SignOn()
    {
        RegisteredSystem? system = null;
        try
        {
            var xml = SamlIdentityProvider.ReadRequest(Request);
            var issuerName = XName.Get("Issuer", SamlIdentityProvider.AssertionNamespace);
            var issuer = xml.Element(issuerName)!.Value;

            await using var db = database.Open();
            system = await db.RegisteredSystems
                .SingleOrDefaultAsync(s => s.Protocol == "saml" && s.Enabled && s.SamlEntityId == issuer);
            if (system is null)
            {
                return BadRequest("無法辨識或已停用的 SAML 系統。");
            }

            var request = saml.ValidateRequest(Request, xml, system, clock.GetUtcNow());
            return await StartFlowAsync(system, request);
        }
        catch (Exception error) when (SsoStorage.IsUniqueViolation(error))
        {
            // RequestHash 是唯一鍵，撞鍵代表同一個請求被重送。
            await Audit(system?.ClientId ?? "", null, "replayed_request");
            return BadRequest("此 SAML 登入請求已使用，請從接收系統重新開始登入。");
        }
        catch (Exception error) when (IsRequestError(error))
        {
            // 不記錄原始 SAML、RelayState 或例外訊息，避免把輸入與身分資料寫入日誌。
            logger.LogWarning("SAML request rejected ({ExceptionType}).", error.GetType().Name);
            await Audit(system?.ClientId ?? "", null, "invalid_request");
            return BadRequest("SAML 請求無效或使用尚未支援的選項，請由系統管理者確認設定。");
        }
    }

    /// <summary>
    /// 請求格式問題與協定套件丟出的例外都算「請求無效」，不算伺服器錯誤。
    /// </summary>
    private static bool IsRequestError(Exception error)
    {
        if (error is InvalidOperationException or ArgumentException or FormatException)
        {
            return true;
        }

        if (error is XmlException or IOException or CryptographicException)
        {
            return true;
        }

        return error.GetType().Assembly == typeof(Saml2AuthnRequest).Assembly;
    }

    /// <summary>
    /// 保存待處理的請求並以 Cookie 綁定這個瀏覽器，再導向需要登入的流程頁。
    /// </summary>
    private async Task<IActionResult> StartFlowAsync(RegisteredSystem system, Saml2AuthnRequest request)
    {
        var correlation = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var pending = new SamlLoginRequest
        {
            Id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            RequestHash = Hash(system.SamlEntityId + "\n" + request.IdAsString),
            RequestId = request.IdAsString,
            ClientId = system.ClientId,
            Revision = system.Revision,
            CorrelationHash = Hash(correlation),
            RelayState = Request.Query["RelayState"].ToString(),
            IsPassive = request.IsPassive == true,
            ExpiresAt = clock.GetUtcNow().Add(FlowLifetime).ToUnixTimeMilliseconds()
        };

        await using var state = database.OpenSessions();
        await state.InsertAsync(pending);

        Response.Cookies.Append(CookieName(pending.Id), correlation, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/saml",
            MaxAge = FlowLifetime,
            IsEssential = true
        });

        return Redirect("/saml/continue?flow=" + pending.Id);
    }

    [HttpGet("/saml/continue")]
    public async Task<IActionResult> Continue(string? flow)
    {
        if (flow is null || !Regex.IsMatch(flow, FlowIdPattern))
        {
            return BadRequest();
        }

        await using var state = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        var pending = await state.GetTable<SamlLoginRequest>()
            .SingleOrDefaultAsync(r => r.Id == flow && r.ConsumedAt == null && r.ExpiresAt > now);
        if (pending is null || !MatchesCorrelationCookie(flow, pending))
        {
            return BadRequest("登入流程已失效，請從接收系統重新開始。");
        }

        await using var db = database.Open();
        var system = await db.RegisteredSystems
            .SingleOrDefaultAsync(s => s.ClientId == pending.ClientId
                && s.Enabled
                && s.Protocol == "saml"
                && s.Revision == pending.Revision);
        if (system is null)
        {
            return BadRequest("系統已停用或設定已變更，請重新開始登入。");
        }

        var (account, session) = await LoadSignedInAccountAsync(db, state);

        // IsPassive 代表接收系統不希望出現登入畫面，只要回報「目前沒有登入」。
        if (account is null && !pending.IsPassive)
        {
            var properties = new AuthenticationProperties { RedirectUri = "/saml/continue?flow=" + flow };
            return Challenge(properties, IdentityConstants.ApplicationScheme);
        }

        var authenticatedAt = session is null
            ? (DateTimeOffset?)null
            : DateTimeOffset.FromUnixTimeMilliseconds(session.CreatedAt);
        var result = saml.Response(system, pending, account, authenticatedAt);

        var consumed = await state.GetTable<SamlLoginRequest>()
            .Where(r => r.Id == flow && r.ConsumedAt == null && r.ExpiresAt > now)
            .Set(r => r.ConsumedAt, now)
            .UpdateAsync();
        if (consumed != 1)
        {
            return BadRequest("此登入流程已使用。");
        }

        Response.Cookies.Delete(CookieName(flow), new CookieOptions
        {
            Secure = true,
            Path = "/saml",
            SameSite = SameSiteMode.Lax
        });

        await Audit(system.ClientId, account?.Id, account is null ? "no_passive" : "success");

        // 僅此回應允許表單送到登記 ACS 的來源；表單 action 仍使用精確登記網址。
        var acs = new Uri(system.RedirectUri).GetLeftPart(UriPartial.Authority);
        Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; style-src 'self'; img-src 'self' data:; "
            + "frame-ancestors 'none'; base-uri 'none'; form-action " + acs;

        return View("SubmitSamlResponse", result);
    }

    /// <summary>
    /// 以固定時間比對 Cookie，確認這個流程確實是同一個瀏覽器開啟的。
    /// </summary>
    private bool MatchesCorrelationCookie(string flow, SamlLoginRequest pending)
    {
        if (!Request.Cookies.TryGetValue(CookieName(flow), out var correlation))
        {
            return false;
        }

        var stored = Encoding.ASCII.GetBytes(pending.CorrelationHash);
        var provided = Encoding.ASCII.GetBytes(Hash(correlation));
        return CryptographicOperations.FixedTimeEquals(stored, provided);
    }

    /// <summary>
    /// 取出目前已登入且仍有效的帳號；沒有登入時兩個值都是 null。
    /// </summary>
    private async Task<(Account? Account, LoginSession? Session)> LoadSignedInAccountAsync(
        SsoDatabase db,
        SsoDatabase state)
    {
        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var subject = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? sessionId = null;
        auth.Properties?.Items.TryGetValue(LoginSessionStore.SessionProperty, out sessionId);

        if (!auth.Succeeded || subject is null)
        {
            return (null, null);
        }

        if (!await sessions.IsActiveAsync(sessionId, subject))
        {
            return (null, null);
        }

        var now = clock.GetUtcNow();
        var account = await db.Users
            .SingleOrDefaultAsync(u => u.Id == subject
                && u.Enabled
                && (u.LockoutEnd == null || u.LockoutEnd <= now));
        var session = await state.LoginSessions.SingleOrDefaultAsync(s => s.Id == sessionId);

        return (account, session);
    }

    private async Task Audit(string client, string? account, string outcome)
    {
        await using var db = database.OpenSessions();
        await db.InsertAsync(new SamlAudit
        {
            ClientId = client,
            AccountId = account,
            Outcome = outcome,
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });
    }

    private static string CookieName(string flow)
    {
        return "NkustSso.Saml." + flow;
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
