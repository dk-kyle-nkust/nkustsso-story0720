// 查詢校務身分分類及入口規則可選條件。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/identity-catalog")]
public sealed class IdentityCatalogController(IdentityCatalog catalog) : Controller
{
    // 只提供查詢端點；舊的新增、編輯與啟用／停用網址不再接受請求。
    [HttpGet("")]
    public async Task<IActionResult> Index(string dimension = "identity_type")
    {
        var snapshot = await catalog.LoadSourceAsync();
        if (!snapshot.Dimensions.Any(d => d.Key == dimension))
        {
            return BadRequest();
        }

        if (snapshot.SourceError is not null)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return View(new IdentityCatalogListViewModel(snapshot, dimension));
    }
}
