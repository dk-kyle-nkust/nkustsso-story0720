// 維護前台服務分類與顯示順序。
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

/// <summary>首頁分類管理；所有操作均使用現有管理者權限，POST 套用全域防偽驗證。</summary>
[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/categories")]
public sealed class PortalCategoryManagementController(
    DatabaseFactory database,
    PortalCategoryService catalog,
    PortalCategoryManagementService administration) : Controller
{
    /// <summary>異動紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 30;

    /// <summary>依首頁順序列出分類、使用數量及最近異動。</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await using var db = database.Open();

        var counts = await db.RegisteredSystems
            .GroupBy(s => s.Category)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync();

        var audits = await db.PortalCategoryAudits
            .OrderByDescending(a => a.Id)
            .Take(AuditCount)
            .ToListAsync();

        return View(new PortalCategoryListViewModel(
            await catalog.ListAsync(),
            counts.ToDictionary(c => c.Key, c => c.Count),
            audits));
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        return View("Edit", new PortalCategoryEditViewModel { Creating = true });
    }

    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        await using var db = database.Open();

        var category = await db.PortalCategories.SingleOrDefaultAsync(c => c.Key == id);
        if (category is null)
        {
            return NotFound();
        }

        return View("Edit", new PortalCategoryEditViewModel { Category = category });
    }

    [HttpPost("new")]
    public Task<IActionResult> Create(PortalCategoryEditViewModel model)
    {
        return Save(model, true, null);
    }

    [HttpPost("{id}/edit")]
    public Task<IActionResult> Edit(string id, PortalCategoryEditViewModel model)
    {
        return Save(model, false, id);
    }

    /// <summary>以路由確認固定代碼，驗證失敗保留輸入及原版本，讓管理者決定是否重新載入。</summary>
    private async Task<IActionResult> Save(PortalCategoryEditViewModel model, bool creating, string? id)
    {
        model.Creating = creating;
        if (!creating && id != model.Category.Key)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        try
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await administration.SaveAsync(model.Category, creating, actor, Ip());

            TempData["Message"] = "已儲存分類，首頁重新整理後即可看到最新名稱與順序。";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
        }
        catch (DbException)
        {
            ModelState.AddModelError("", "儲存失敗，可能有重複代碼或同時更新。請重新開啟列表確認。");
        }

        return View("Edit", model);
    }

    /// <summary>顯式 POST 刪除空分類；伺服器重新確認引用及版本，不信任畫面上的使用數量。</summary>
    [HttpPost("{id}/delete")]
    public async Task<IActionResult> Delete(string id, string revision)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        try
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await administration.DeleteAsync(id, revision, actor, Ip());
            TempData["Message"] = "已刪除分類。";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DbException)
        {
            TempData["Error"] = "刪除失敗，可能有同時更新，請重新確認分類與系統設定。";
        }

        return RedirectToAction(nameof(Index));
    }

    private string Ip()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}
