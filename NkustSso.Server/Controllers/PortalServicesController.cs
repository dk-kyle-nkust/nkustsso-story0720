// 顯示各身分專區的服務選單，檢查資格後開啟系統。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

[AllowAnonymous]
public sealed class PortalServicesController(
    PortalDirectoryService catalog,
    UserManager<Account> users,
    SignInManager<Account> signIn) : Controller
{
    [HttpGet("/faq")]
    public IActionResult Faq()
    {
        return View("FrequentlyAskedQuestions");
    }

    [HttpGet("/portal/{section}")]
    public async Task<IActionResult> Browse(string section)
    {
        if (LoginArea.Normalize(section).Length == 0)
        {
            return NotFound();
        }

        return View("ServiceDirectory", await catalog.LoadAsync(section, await CurrentAccountAsync()));
    }

    [HttpGet("/portal/{section}/systems/{id}")]
    public async Task<IActionResult> Open(string section, string id)
    {
        if (LoginArea.Normalize(section).Length == 0)
        {
            return NotFound();
        }
        // 每次開啟重新檢查發布狀態，登入期間撤下的系統也不能再由舊連結進入。
        var account = await CurrentAccountAsync();
        var system = (await catalog.VisibleAsync(section, account, id)).SingleOrDefault();
        if (system is null)
        {
            return NotFound();
        }

        // 需要登入才能開啟的入口：會建立登入的協定系統，或管理者標示的私人入口。
        var needsLogin = system.Protocol != "link"
            || system.RequirePortalLogin
            || PortalDirectoryService.IsPrivate(system);

        if (needsLogin && account is null)
        {
            var destination = Url.Action(nameof(Open), new { section, id })!;
            var query = new Dictionary<string, string?>
            {
                ["area"] = section,
                ["returnUrl"] = destination
            };
            return Redirect(QueryHelpers.AddQueryString("/account/login", query));
        }

        // 只導向管理者登記的入口；協定請求由接收系統建立，
        // 保留它自己的 state、PKCE 與回呼驗證。
        return Redirect(system.EntryUrl);
    }

    private async Task<Account?> CurrentAccountAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var account = await users.GetUserAsync(User);
        if (account is { Enabled: true } && !await users.IsLockedOutAsync(account))
        {
            return account;
        }

        await signIn.SignOutAsync();
        HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        return null;
    }
}
