// 僅供有手冊權限的後台人員瀏覽及下載；清單、PDF 與 ZIP 套用同一授權。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

[Authorize(Policy = LoginSessionAdministration.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IntegrationManualController(IntegrationManualService manuals) : Controller
{
    [HttpGet("/admin/manuals")]
    public IActionResult Index() => View(manuals.Catalog);

    [HttpGet("/admin/manuals/download/{id}")]
    public IActionResult Download(string id)
    {
        if (id == "all")
        {
            return File(manuals.OpenBundle(), "application/zip", "開發人員串接PDF全集.zip");
        }
        var manual = manuals.Find(id);
        return manual is null ? NotFound() : File(manuals.Open(manual), "application/pdf", manual.FileName);
    }
}
