// 查詢及撤銷使用者的中央登入狀態。
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

/// <summary>
/// 中央 Session 管理介面；需通過即時管理角色檢查，撤銷動作會保存操作原因。
/// </summary>
[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/sessions")]
public sealed class LoginSessionManagementController(
    DatabaseFactory database,
    TimeProvider clock,
    LoginSessionLifetimeService lifetime) : Controller
{
    /// <summary>清單一次最多顯示的登入筆數。</summary>
    private const int SessionCount = 100;

    /// <summary>異動紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 30;

    /// <summary>
    /// 依帳號與狀態查詢最近 100 筆登入，並標示目前登入及最近 30 筆撤銷紀錄。
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? account, string status = "active")
    {
        account = account?.Trim();
        if (account?.Length > 100)
        {
            return BadRequest();
        }

        if (status != "active" && status != "all" && status != "revoked" && status != "expired")
        {
            return BadRequest();
        }

        await using var db = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var earliestCreation = now - lifetime.MaximumMilliseconds;
        var query = db.LoginSessions.AsQueryable();
        if (!string.IsNullOrEmpty(account))
        {
            query = query.Where(s => s.UserName == account);
        }

        // 已過期包含兩種：滑動期限到了，以及已超過最長連續登入時間。
        query = status switch
        {
            "active" => query.Where(s => s.RevokedAt == null
                && s.ExpiresAt > now
                && s.CreatedAt > earliestCreation),
            "revoked" => query.Where(s => s.RevokedAt != null),
            "expired" => query.Where(s => s.RevokedAt == null
                && (s.ExpiresAt <= now || s.CreatedAt <= earliestCreation)),
            _ => query
        };
        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        string? current = null;
        auth.Properties?.Items.TryGetValue(LoginSessionStore.SessionProperty, out current);
        return View(new LoginSessionListViewModel
        {
            Account = account,
            Status = status,
            Now = now,
            CurrentSession = current,
            MaximumMilliseconds = lifetime.MaximumMilliseconds,
            Sessions = await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(SessionCount)
                .ToListAsync(),
            Audits = await db.SessionAudits
                .OrderByDescending(a => a.Id)
                .Take(AuditCount)
                .ToListAsync()
        });
    }

    /// <summary>
    /// 載入要終止的登入或帳號，供管理者確認影響範圍。
    /// </summary>
    [HttpGet("revoke")]
    public async Task<IActionResult> Revoke(string accountId, string? sessionId)
    {
        var model = await LoadTarget(accountId, sessionId);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// 驗證目標與原因後執行撤銷；sessionId 未指定時終止該帳號所有符合條件的中央登入。
    /// </summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeLoginSessionViewModel model)
    {
        var target = await LoadTarget(model.AccountId, model.SessionId);
        if (target is null)
        {
            return NotFound();
        }

        target.Reason = model.Reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(target.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "請填寫終止原因。");
        }

        if (!ModelState.IsValid)
        {
            return View(target);
        }

        await using var db = database.OpenSessions();
        var count = await LoginSessionAdministration.RevokeAsync(
            db,
            target.AccountId,
            target.SessionId,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            target.Reason,
            clock.GetUtcNow().ToUnixTimeMilliseconds(),
            lifetime.MaximumMilliseconds);
        TempData["Message"] = $"已終止 {count} 筆中央登入。使用者下次存取 SSO 時須重新登入。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 從資料庫確認帳號與 Session 的歸屬，計算目前有效筆數，
    /// 不信任表單傳來的顯示資訊。
    /// </summary>
    private async Task<RevokeLoginSessionViewModel?> LoadTarget(string accountId, string? sessionId)
    {
        await using var accounts = database.Open();
        var user = await accounts.Users.SingleOrDefaultAsync(u => u.Id == accountId);
        if (user is null)
        {
            return null;
        }

        await using var db = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var earliestCreation = now - lifetime.MaximumMilliseconds;
        var query = db.LoginSessions.Where(s => s.AccountId == accountId);
        if (sessionId is not null)
        {
            query = query.Where(s => s.Id == sessionId);
            if (!await query.AnyAsync())
            {
                return null;
            }
        }

        var activeCount = await query.CountAsync(s => s.RevokedAt == null
            && s.ExpiresAt > now
            && s.CreatedAt > earliestCreation);

        return new RevokeLoginSessionViewModel
        {
            AccountId = accountId,
            SessionId = sessionId,
            UserName = user.UserName ?? accountId,
            ActiveCount = activeCount
        };
    }
}
