// 維護後台群組、功能權限與群組成員。
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Security.Claims;
using LinqToDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/access")]
public sealed class AdminRoleManagementController(
    DatabaseFactory database,
    AdminManagementService management,
    CampusRoleMemberService campusMembers) : Controller
{
    /// <summary>異動紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 30;

    /// <summary>角色授權時間以台北時間呈現與輸入。</summary>
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await using var db = database.Open();

        var roles = await db.GetTable<AdminRole>().OrderBy(r => r.Name).ToListAsync();
        var members = await db.GetTable<AdminRoleMember>().ToListAsync();
        var permissions = await db.GetTable<AdminRolePermission>().ToListAsync();
        var audits = await db.GetTable<AdminManagementAudit>()
            .Where(a => a.Kind == "role")
            .OrderByDescending(a => a.Id)
            .Take(AuditCount)
            .ToListAsync();

        return View(new AdminRoleListViewModel(roles, members, permissions, audits));
    }

    [HttpGet("new")]
    public Task<IActionResult> Create()
    {
        return Editor(new AdminRoleEditViewModel { Creating = true });
    }

    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        await using var db = database.Open();

        var row = await db.GetTable<AdminRole>().SingleOrDefaultAsync(r => r.Id == id);
        if (row is null)
        {
            return NotFound();
        }

        var permissions = await db.GetTable<AdminRolePermission>()
            .Where(g => g.RoleId == id)
            .Select(g => g.Permission)
            .ToArrayAsync();

        var members = await db.GetTable<AdminRoleMember>()
            .Where(m => m.RoleId == id)
            .Select(m => m.AccountId)
            .ToArrayAsync();

        return await Editor(new AdminRoleEditViewModel
        {
            Role = row,
            StartsAtLocal = RoleLocalTime(row.StartsAt),
            ExpiresAtLocal = RoleLocalTime(row.ExpiresAt),
            Permissions = permissions,
            Members = members
        });
    }

    [HttpPost("new")]
    public Task<IActionResult> Create(AdminRoleEditViewModel model)
    {
        return Save(model, true, null);
    }

    [HttpPost("{id}/edit")]
    public Task<IActionResult> Edit(string id, AdminRoleEditViewModel model)
    {
        return Save(model, false, id);
    }

    private async Task<IActionResult> Save(AdminRoleEditViewModel model, bool creating, string? id)
    {
        model.Creating = creating;
        if (!creating && id != model.Role.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            try
            {
                model.Role.StartsAt = RoleTimestamp(model.StartsAtLocal);
                model.Role.ExpiresAt = RoleTimestamp(model.ExpiresAtLocal);

                await management.SaveRoleAsync(
                    model.Role,
                    model.Permissions,
                    model.Members,
                    creating,
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    Ip());

                TempData["Message"] = "已儲存角色。成員的下一次請求會套用最新權限，不必重新登入。";
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

        return await Editor(model);
    }

    private async Task<IActionResult> Editor(AdminRoleEditViewModel model)
    {
        await using var db = database.Open();

        model.Accounts = await db.GetTable<Account>()
            .OrderBy(a => a.UserName)
            .Select(a => new AdminAccountOption(a.Id, a.UserName ?? "", a.DisplayName, a.Enabled))
            .ToListAsync();

        return View("Edit", model);
    }

    /// <summary>
    /// 編輯頁內查詢校務帳號，只讀來源、不寫入任何資料，供管理者核對姓名與身分。
    /// </summary>
    [HttpPost("{id}/members/find")]
    public async Task<IActionResult> FindMember(string id, string? accountName)
    {
        if (!await RoleExistsAsync(id))
        {
            return NotFound();
        }

        try
        {
            var candidate = await campusMembers.FindAsync(accountName, HttpContext.RequestAborted);

            return Json(new
            {
                ok = true,
                candidate.AccountName,
                candidate.DisplayName,
                candidate.StatusName
            });
        }
        catch (InvalidOperationException e)
        {
            return Json(new { ok = false, message = e.Message });
        }
    }

    /// <summary>
    /// 只在本機建立（或確認）該校務帳號，回傳可直接放進成員欄的識別。
    /// 成員關聯仍由「儲存角色」一併寫入，所以不會動到畫面上其他未儲存的變更。
    /// </summary>
    [HttpPost("{id}/members/provision")]
    public async Task<IActionResult> ProvisionMember(string id, string? accountName)
    {
        if (!await RoleExistsAsync(id))
        {
            return NotFound();
        }

        try
        {
            var member = await campusMembers.ProvisionAsync(
                id,
                accountName ?? "",
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Ip(),
                HttpContext.RequestAborted);

            return Json(new
            {
                ok = true,
                accountId = member.AccountId,
                label = $"{member.AccountName} · {member.DisplayName}"
            });
        }
        catch (InvalidOperationException e)
        {
            return Json(new { ok = false, message = e.Message });
        }
        catch (DbException)
        {
            return Json(new { ok = false, message = "建立校務帳號失敗，請稍後重試。" });
        }
    }

    private async Task<bool> RoleExistsAsync(string id)
    {
        await using var db = database.Open();

        return await db.GetTable<AdminRole>().AnyAsync(r => r.Id == id);
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
            await management.DeleteRoleAsync(id, revision, User.FindFirstValue(ClaimTypes.NameIdentifier)!, Ip());
            TempData["Message"] = "已刪除角色。";
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

    /// <summary>資料庫存毫秒時間戳，畫面上顯示台北時間。</summary>
    private static DateTime? RoleLocalTime(long? value)
    {
        if (value is not long time)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(time).ToOffset(TaipeiOffset).DateTime;
    }

    /// <summary>表單填的是台北時間，存進資料庫前換成毫秒時間戳。</summary>
    private static long? RoleTimestamp(DateTime? value)
    {
        if (value is not DateTime time)
        {
            return null;
        }

        if (time < new DateTime(1970, 1, 2))
        {
            throw new InvalidOperationException("授權時間須晚於 1970 年 1 月 1 日。");
        }

        var unspecified = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TaipeiOffset).ToUnixTimeMilliseconds();
    }
}
