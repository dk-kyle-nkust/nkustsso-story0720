// 依使用者權限導向可使用的後台首頁。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

[Authorize]
public sealed class AdminHomeController(AdminAuthorizationService access) : Controller
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        var target = await access.LandingAsync(User);
        return target is null ? Forbid() : LocalRedirect(target);
    }
}
