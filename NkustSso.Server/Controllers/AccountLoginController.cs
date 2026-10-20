// 處理帳密登入、登出與目前登入帳號資訊。
using System.ComponentModel.DataAnnotations;
using LinqToDB;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

/// <summary>
/// 中央帳密登入與登出入口；Identity 負責密碼雜湊、鎖定及建立中央 Session。
/// </summary>
public sealed class AccountLoginController(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    DatabaseFactory database,
    CampusLoginService campusLogin,
    IOptions<CampusAuthenticationOptions> campusOptions,
    IHostEnvironment environment) : Controller
{
    // 分頁重新取得焦點才查詢，不背景輪詢以持續延長閒置登入。
    [AllowAnonymous]
    [HttpGet("/account/current-user")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CurrentUser(
        [FromServices] IAntiforgery antiforgery,
        [FromServices] AdminAuthorizationService access)
    {
        var summary = await CurrentUserDisplayService.LoadAsync(users, User, access);
        if (summary is null)
        {
            return Json(new { signedIn = false });
        }

        // 原分頁可能是在登入前開啟，登出表單必須換成目前身分的防偽 Token。
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Json(new
        {
            signedIn = true,
            summary.UserName,
            summary.DisplayName,
            summary.IdentityLabel,
            summary.AccountLabel,
            summary.CanManageSystems,
            summary.UnitName,
            logoutToken = tokens.RequestToken,
            tokenField = tokens.FormFieldName
        });
    }

    /// <summary>
    /// 顯示登入表單，僅保留本站的登入後返回網址。
    /// </summary>
    [HttpGet("/account/login")]
    public async Task<IActionResult> Login(string? returnUrl, string? area, bool reauthenticate = false)
    {
        var destination = SafeReturn(returnUrl);

        // Tag Helper 優先採用 ModelState，清除原始值才能輸出已正規化的隱藏欄位。
        ModelState.Remove(nameof(LoginViewModel.Area));
        ModelState.Remove(nameof(LoginViewModel.ReturnUrl));

        if (!reauthenticate && User.Identity?.IsAuthenticated == true)
        {
            var account = await users.GetUserAsync(User);
            if (account is { Enabled: true } && !await users.IsLockedOutAsync(account))
            {
                return LocalRedirect(destination);
            }

            // 已登入但帳號已停用或鎖定時，先清掉這個登入狀態。
            await signIn.SignOutAsync();
        }

        return View("SignIn", new LoginViewModel
        {
            ReturnUrl = destination,
            Area = LoginArea.Normalize(area),
            CampusAuthentication = campusOptions.Value.Enabled,
            DevelopmentAllowAnyPassword = IsDevelopmentLogin()
        });
    }

    /// <summary>
    /// 驗證帳密並記錄登入結果；失敗時使用相同訊息，避免直接透露帳號是否存在。
    /// </summary>
    [HttpPost("/account/login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ReturnUrl = SafeReturn(model.ReturnUrl);
        model.Area = LoginArea.Normalize(model.Area);
        ModelState.Remove(nameof(LoginViewModel.Area));
        ModelState.Remove(nameof(LoginViewModel.ReturnUrl));
        model.CampusAuthentication = campusOptions.Value.Enabled;
        model.DevelopmentAllowAnyPassword = IsDevelopmentLogin();

        if (!ModelState.IsValid)
        {
            return View("SignIn", model);
        }

        var account = await users.FindByNameAsync(model.UserName.Trim());
        if (User.Identity?.IsAuthenticated == true && account?.Id != users.GetUserId(User))
        {
            ModelState.AddModelError("", "請先登出目前帳號，再登入其他帳號。");
            return View("SignIn", model);
        }

        var (succeeded, unavailable, resolvedAccount) = model.CampusAuthentication
            ? await SignInWithCampusAsync(model, account)
            : await SignInWithPasswordAsync(model, account);
        account = resolvedAccount;

        await using var db = database.Open();
        await db.InsertAsync(new LoginEvent
        {
            AccountId = account?.Id,
            Outcome = succeeded ? "success" : "failure"
        });

        // 先完成本站的表單導向，再由一般頁面導覽接續協定或外部入口，
        // 避免瀏覽器將跨站 302 鏈當作帳密表單外送而被 form-action 'self' 阻擋。
        if (succeeded)
        {
            return RedirectToAction(nameof(Continue), new { returnUrl = model.ReturnUrl });
        }

        ModelState.AddModelError("", unavailable
            ? "登入服務暫時無法使用，請稍後再試。"
            : "無法登入，請確認帳密及帳號狀態；連續錯誤後請稍候再試。");
        return View("SignIn", model);
    }

    private async Task<(bool Succeeded, bool Unavailable, Account? Account)> SignInWithCampusAsync(
        LoginViewModel model,
        Account? account)
    {
        var currentAccountId = User.Identity?.IsAuthenticated == true ? users.GetUserId(User) : null;
        var result = await campusLogin.AuthenticateAsync(
            model.UserName,
            model.Password,
            currentAccountId,
            HttpContext.RequestAborted);

        var unavailable = result.Unavailable;
        account = result.Account ?? account;

        if (result.Account is null)
        {
            return (false, unavailable, account);
        }

        try
        {
            // 來源已完成驗證（或明確啟用開發測試）；SessionStore 仍檢查來源狀態。
            await signIn.SignInAsync(
                result.Account,
                isPersistent: false,
                authenticationMethod: CampusLoginService.SourceName);
            return (true, unavailable, account);
        }
        catch (InvalidOperationException)
        {
            // 驗證後至建立 Session 之間若來源狀態改變，保持登入失敗。
            return (false, true, account);
        }
    }

    private async Task<(bool Succeeded, bool Unavailable, Account? Account)> SignInWithPasswordAsync(
        LoginViewModel model,
        Account? account)
    {
        if (account is { Enabled: true, AuthenticationSource: "" })
        {
            var result = await signIn.PasswordSignInAsync(
                account,
                model.Password,
                isPersistent: false,
                lockoutOnFailure: true);
            return (result.Succeeded, false, account);
        }

        // 未知或停用帳號也執行雜湊運算，縮小失敗路徑的耗時差異；不代表能完全消除時間側通道。
        _ = new PasswordHasher<Account>().HashPassword(new Account(), model.Password);
        return (false, false, account);
    }

    [Authorize]
    [HttpGet("/account/continue")]
    public IActionResult Continue(string? returnUrl)
    {
        return View("ContinueSignIn", SafeReturn(returnUrl));
    }

    /// <summary>
    /// 結束目前中央登入並返回身分選擇頁；不會直接清除各系統自己的 Cookie。
    /// </summary>
    [Authorize]
    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return Redirect("/portal");
    }

    /// <summary>
    /// 只接受本站相對網址，避免登入後被導向外部網站。
    /// </summary>
    private string SafeReturn(string? value)
    {
        return Url.IsLocalUrl(value) ? value! : "/";
    }

    private bool IsDevelopmentLogin()
    {
        return environment.IsDevelopment() && campusOptions.Value.Enabled
            && campusOptions.Value.DevelopmentAllowAnyPassword;
    }

    /// <summary>
    /// 回傳無權限頁面與 HTTP 403。
    /// </summary>
    [HttpGet("/account/denied")]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("AccessDenied");
    }
}
