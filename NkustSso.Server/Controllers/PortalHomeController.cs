// 提供身分選擇首頁及內部開發進度頁。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NkustSso.Server.Controllers;

/// <summary>登入前後共用身分專區，登入狀態不改變首頁版型。</summary>
[Authorize]
public sealed class PortalHomeController : Controller
{
    [HttpGet("/")]
    [HttpGet("/portal")]
    [AllowAnonymous]
    public IActionResult Index() => View("~/Views/PortalServices/SelectIdentityArea.cshtml");

    [HttpGet("/integration")]
    [Authorize(Policy = NkustSso.Server.Services.LoginSessionAdministration.Policy)]
    public IActionResult Integration() => View("DevelopmentProgress");
}
