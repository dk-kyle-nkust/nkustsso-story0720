// 維護後台選單名稱、順序與顯示設定。
using System.Data.Common;
using System.Security.Claims;
using LinqToDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/menus")]
public sealed class AdminMenuManagementController(
    DatabaseFactory database,
    AdminManagementService management) : Controller
{
    /// <summary>異動紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 30;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await using var db = database.Open();

        var menus = await db.GetTable<AdminMenu>()
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .ToListAsync();

        var audits = await db.GetTable<AdminManagementAudit>()
            .Where(a => a.Kind == "menu")
            .OrderByDescending(a => a.Id)
            .Take(AuditCount)
            .ToListAsync();

        return View(new AdminMenuListViewModel(menus, audits));
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        return View("Edit", new AdminMenuEditViewModel { Creating = true });
    }

    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        await using var db = database.Open();

        var row = await db.GetTable<AdminMenu>().SingleOrDefaultAsync(m => m.Id == id);
        if (row is null)
        {
            return NotFound();
        }

        return View(new AdminMenuEditViewModel { Menu = row });
    }

    [HttpPost("new")]
    public Task<IActionResult> Create(AdminMenuEditViewModel model)
    {
        return Save(model, true, null);
    }

    [HttpPost("{id}/edit")]
    public Task<IActionResult> Edit(string id, AdminMenuEditViewModel model)
    {
        return Save(model, false, id);
    }

    private async Task<IActionResult> Save(AdminMenuEditViewModel model, bool creating, string? id)
    {
        model.Creating = creating;
        if (!creating && id != model.Menu.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                await management.SaveMenuAsync(model.Menu, creating, actor, Ip());

                TempData["Message"] = "已儲存選單，導覽會依最新設定顯示。";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException e)
            {
                ModelState.AddModelError("", e.Message);
            }
            catch (DbException)
            {
                ModelState.AddModelError("", "儲存失敗，請確認是否有重複代碼或同時更新。");
            }
        }

        return View("Edit", model);
    }

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
            await management.DeleteMenuAsync(id, revision, actor, Ip());
            TempData["Message"] = "已刪除選單。";
        }
        catch (InvalidOperationException e)
        {
            TempData["Error"] = e.Message;
        }
        catch (DbException)
        {
            TempData["Error"] = "刪除失敗，請重新確認目前設定。";
        }

        return RedirectToAction(nameof(Index));
    }

    private string Ip()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}
