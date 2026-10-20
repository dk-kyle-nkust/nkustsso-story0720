// 處理 OIDC 授權、Token 交換與登出確認。
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace NkustSso.Server.Controllers;

/// <summary>
/// OIDC 授權碼登入端點；協定驗證由 OpenIddict 執行，
/// 此處補上帳號、中央 Session 與欄位提供規則。
/// </summary>
public sealed class OidcAuthorizationController(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    LoginSessionStore sessions,
    SsoSystemRegistrationService registry) : Controller
{
    private const string Scheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;

    /// <summary>
    /// 接收瀏覽器的 OIDC 授權要求；中央登入有效時核發授權碼，否則導向中央登入頁。
    /// </summary>
    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> AuthorizeRequest()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        var registration = await registry.ActiveAsync(request.ClientId, "oidc");
        if (registration is null || registration.RedirectUri != request.RedirectUri)
        {
            return BadRequest(new { error = "unauthorized_client" });
        }

        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var account = auth.Principal is null ? null : await users.GetUserAsync(auth.Principal);
        if (!auth.Succeeded || account is not { Enabled: true } || await users.IsLockedOutAsync(account))
        {
            if (auth.Succeeded)
            {
                await signIn.SignOutAsync();
            }

            if (request.HasPromptValue(PromptValues.None))
            {
                return Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired
                }), Scheme);
            }
            // 首次登入也要消耗 prompt=login，否則返回授權端點後會再要求第二次帳密。
            if (request.HasPromptValue(PromptValues.Login))
            {
                return RequireFreshLogin();
            }

            // 登入後導回同一個授權要求，連查詢字串一起帶回來。
            var properties = new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString
            };
            return Challenge(properties, IdentityConstants.ApplicationScheme);
        }
        if (request.HasPromptValue(PromptValues.Login))
        {
            return RequireFreshLogin();
        }
        string? sessionId = null;
        auth.Properties?.Items.TryGetValue(LoginSessionStore.SessionProperty, out sessionId);
        if (!await sessions.IsActiveAsync(sessionId, account.Id))
        {
            return Challenge(IdentityConstants.ApplicationScheme);
        }

        var principal = OidcIdentityClaims.Create(account, request.GetScopes());
        IdentityClaimReleasePolicy.Filter(principal, registration.ClaimMask);
        // 中央 Session ID 僅綁定於加密授權碼，不提供給 ID Token 或 Access Token。
        principal.SetClaim(LoginSessionStore.SessionClaim, sessionId);
        return SignIn(principal, Scheme);
    }

    private IActionResult RequireFreshLogin()
    {
        var parameters = Request.Query.ToDictionary(p => p.Key, p => (string?)p.Value.ToString());
        var prompts = parameters[Parameters.Prompt]!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != PromptValues.Login).ToArray();
        if (prompts.Length == 0)
        {
            parameters.Remove(Parameters.Prompt);
        }
        else
        {
            parameters[Parameters.Prompt] = string.Join(' ', prompts);
        }

        var returnUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/connect/authorize", parameters);
        return Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/account/login",
            new Dictionary<string, string?> { ["returnUrl"] = returnUrl, ["reauthenticate"] = "true" }));
    }

    /// <summary>
    /// 處理接收系統後端的授權碼兌換；再次檢查帳號、Session 與目前允許提供的欄位。
    /// </summary>
    [HttpPost("/connect/token")]
    // 後端兌換以用戶端密鑰、授權碼與 PKCE 驗證，不使用瀏覽器表單的防偽 Token。
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        if (!request.IsAuthorizationCodeGrantType())
        {
            return BadRequest();
        }

        var registration = await registry.ActiveAsync(request.ClientId, "oidc");
        if (registration is null || registration.RedirectUri != request.RedirectUri)
        {
            return BadRequest(new { error = "unauthorized_client" });
        }
        // 由 OpenIddict 解析／驗證授權碼票證；不是用下游系統的 Cookie 取得身分。
        var auth = await HttpContext.AuthenticateAsync(Scheme);
        var subject = auth.Principal?.GetClaim(Claims.Subject);
        var account = subject is null ? null : await users.FindByIdAsync(subject);
        if (account is not { Enabled: true } || await users.IsLockedOutAsync(account) ||
            !await sessions.IsActiveAsync(auth.Principal?.GetClaim(LoginSessionStore.SessionClaim), account.Id))
        {
            return Forbid(new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant
            }), Scheme);
        }
        // 兌換時重新讀取身分資料與白名單，同時保留 OpenIddict 原授權碼的內部協定屬性。
        var fresh = OidcIdentityClaims.Create(account, auth.Principal!.GetScopes());
        IdentityClaimReleasePolicy.Filter(fresh, registration.ClaimMask);
        foreach (var claim in auth.Principal!.Claims.Where(c => c.Type.StartsWith("oi_", StringComparison.Ordinal)))
        {
            if (!fresh.HasClaim(c => c.Type == claim.Type && c.Value == claim.Value))
            {
                ((System.Security.Claims.ClaimsIdentity)fresh.Identity!).AddClaim(claim);
            }
        }

        return SignIn(fresh, Scheme);
    }

    /// <summary>
    /// 顯示登出確認頁；單純 GET 不執行登出。
    /// </summary>
    [HttpGet("/connect/logout")]
    public IActionResult LogoutConfirmation() => View("ConfirmSignOut");

    /// <summary>
    /// 確認後結束中央登入，並交由 OpenIddict 完成登出回應。
    /// </summary>
    [HttpPost("/connect/logout")]
    public async Task<IActionResult> LogoutConfirmed()
    {
        await signIn.SignOutAsync();
        return SignOut(new AuthenticationProperties { RedirectUri = "/account/login" }, Scheme);
    }
}
