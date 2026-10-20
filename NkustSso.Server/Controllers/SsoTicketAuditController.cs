// 查詢一次性登入票券的核發與兌換紀錄。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Server.Controllers;

/// <summary>
/// 僅供管理者查詢票券核發／兌換紀錄及來源 IP；紀錄不包含原始票券或密鑰。
/// </summary>
[Authorize(Policy = LoginSessionAdministration.Policy)]
public sealed class SsoTicketAuditController(DatabaseFactory database) : Controller
{
    /// <summary>
    /// 讀取最近 100 筆票券事件，由新到舊顯示。
    /// </summary>
    [HttpGet("/admin/tickets")]
    public async Task<IActionResult> Index()
    {
        await using var db = database.OpenSessions();
        return View(await db.TicketAudits.OrderByDescending(a => a.Id).Take(100).ToListAsync());
    }
}
