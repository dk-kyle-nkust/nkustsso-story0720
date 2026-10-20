// 維護 SSO 串接系統、首頁連結及入口顯示條件。
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

/// <summary>
/// 管理首頁連結與 SSO 接收系統：查詢、欄位提供、首頁可見身分及設定異動紀錄。
/// </summary>
[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/systems")]
public sealed class SsoSystemManagementController(
    DatabaseFactory database,
    SsoSystemRegistrationService registry,
    PortalCategoryService catalog,
    IdentityCatalog identities) : Controller
{
    /// <summary>異動紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 30;

    /// <summary>
    /// 依名稱／識別碼、登入方式、啟用狀態、首頁顯示設定及分類組合查詢；異動紀錄跟隨查詢結果。
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string protocol = "all",
        string status = "all",
        string visibility = "all",
        string category = "",
        string? section = null)
    {
        // 預設先管理前台連結；指定 SSO 協定的舊篩選網址仍導向 SSO 分頁。
        section ??= protocol == "all" || protocol == "link" ? "links" : "sso";
        category = category?.Trim() ?? "";

        if (!IsValidFilter(section, protocol, status, visibility, category))
        {
            return BadRequest();
        }

        await using var db = database.Open();

        // 關鍵字搜尋改由表格前端處理，這裡只留會改變查詢結果集的條件。
        var query = db.RegisteredSystems.AsQueryable();
        if (section == "links")
        {
            query = query.Where(s => s.Protocol == "link");
        }
        else
        {
            query = query.Where(s => s.Protocol != "link");
        }

        if (protocol != "all")
        {
            query = query.Where(s => s.Protocol == protocol);
        }

        if (status != "all")
        {
            query = query.Where(s => s.Enabled == (status == "enabled"));
        }

        if (visibility != "all")
        {
            query = query.Where(s => s.ShowOnPortal == (visibility == "shown"));
        }

        if (category.Length != 0)
        {
            query = query.Where(s => s.Category == category);
        }

        var systems = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ClientId)
            .ToListAsync();

        var audits = await db.SystemConfigurationAudits
            .Where(a => query.Any(s => s.ClientId == a.ClientId))
            .OrderByDescending(a => a.Id)
            .Take(AuditCount)
            .ToListAsync();

        var audiences = (await db.PortalAudiences.ToListAsync())
            .ToDictionary(a => a.ClientId, PortalAudienceSelection.FromRow);

        return View(new SsoSystemListViewModel(systems, audits, protocol, status, visibility)
        {
            Section = section,
            Category = category,
            Categories = await catalog.ListAsync(),
            Audiences = audiences,
            IdentityCatalog = await identities.LoadAsync()
        });
    }

    /// <summary>
    /// 篩選條件逐項檢查；網址可以手改，不符合的組合直接回 400 而不是查出奇怪的結果。
    /// </summary>
    private static bool IsValidFilter(
        string section,
        string protocol,
        string status,
        string visibility,
        string category)
    {
        if (section != "sso" && section != "links")
        {
            return false;
        }

        // 首頁連結與 SSO 系統分在兩個分頁，協定與分頁必須一致。
        if (section == "sso" && protocol == "link")
        {
            return false;
        }

        if (section == "links" && protocol != "all" && protocol != "link")
        {
            return false;
        }

        if (category.Length > 50)
        {
            return false;
        }

        if (protocol != "all" && !SystemProtocols.IsKnown(protocol))
        {
            return false;
        }

        if (status != "all" && status != "enabled" && status != "disabled")
        {
            return false;
        }

        if (visibility != "all" && visibility != "shown" && visibility != "hidden")
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 顯示新增表單；新系統預設不提供額外身分欄位，首頁可見身分則不限。
    /// </summary>
    [HttpGet("new")]
    public async Task<IActionResult> Create(string section = "sso")
    {
        if (section != "sso" && section != "links")
        {
            return BadRequest();
        }

        return await Editor(new SsoSystemEditViewModel
        {
            Creating = true,
            AudienceJson = new PortalAudienceSelection().ToJson(),
            System = new RegisteredSystem
            {
                Category = PortalCategoryService.DefaultKey,
                Protocol = section == "links" ? "link" : "oidc"
            }
        });
    }

    /// <summary>
    /// 載入指定系統、允許提供欄位與首頁可見條件。
    /// </summary>
    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var system = await registry.FindAsync(id);
        if (system is null)
        {
            return NotFound();
        }

        await using var db = database.Open();

        var allowedClaims = IdentityClaimReleasePolicy.Fields
            .Where(f => IdentityClaimReleasePolicy.Allows(system.ClaimMask, f.Bit))
            .Select(f => f.Bit)
            .ToArray();

        var audienceRow = await db.PortalAudiences.SingleOrDefaultAsync(a => a.ClientId == id);

        return await Editor(new SsoSystemEditViewModel
        {
            System = system,
            AllowedClaims = allowedClaims,
            AudienceJson = PortalAudienceSelection.FromRow(audienceRow).ToJson()
        });
    }

    /// <summary>
    /// 接收新增表單，交給共用儲存流程驗證及建立設定。
    /// </summary>
    [HttpPost("new")]
    public Task<IActionResult> Create(SsoSystemEditViewModel model)
    {
        return Save(model, true, null);
    }

    /// <summary>
    /// 接收編輯表單，交給共用儲存流程檢查識別碼與版本。
    /// </summary>
    [HttpPost("{id}/edit")]
    public Task<IActionResult> Edit(string id, SsoSystemEditViewModel model)
    {
        return Save(model, false, id);
    }

    /// <summary>
    /// 驗證欄位白名單及可見條件，再儲存設定；新產生的密鑰僅在這次成功回應中顯示。
    /// </summary>
    private async Task<IActionResult> Save(SsoSystemEditViewModel model, bool creating, string? id)
    {
        model.Creating = creating;
        if (!creating && id != model.System.ClientId)
        {
            return BadRequest();
        }

        if (model.AllowedClaims.Any(bit => !IdentityClaimReleasePolicy.Fields.Any(f => f.Bit == bit)))
        {
            ModelState.AddModelError("", "包含不允許提供的欄位。");
        }

        // 勾選的欄位合併成一個位元遮罩保存。
        model.System.ClaimMask = model.AllowedClaims.Aggregate(0, (mask, bit) => mask | bit);

        foreach (var error in SsoSystemRegistrationService.Validate(model.System))
        {
            ModelState.AddModelError("", error);
        }

        PortalAudienceSelection? audience = null;
        try
        {
            audience = PortalAudienceSelection.Parse(model.AudienceJson);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
        }

        if (!ModelState.IsValid)
        {
            return await Editor(model);
        }

        try
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var secret = await registry.SaveAsync(model.System, creating, actor, ip, audience);

            if (secret is not null)
            {
                // 密鑰只在這一次回應顯示，不留在快取裡。
                Response.Headers.CacheControl = "no-store";
                return View("Created", new SsoSystemCreatedViewModel(model.System, secret));
            }

            return RedirectAfterSave(model.System.Protocol);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
        }
        catch (DbException)
        {
            ModelState.AddModelError("", "無法儲存，可能有同時更新或重複的系統識別碼。請重新開啟列表確認設定。");
        }

        return await Editor(model);
    }

    private IActionResult RedirectAfterSave(string protocol)
    {
        if (protocol == "link")
        {
            TempData["Message"] = "已儲存首頁連結。前台重新整理後會依新的名稱、分類、網址、排序及顯示設定呈現。";
            return RedirectToAction(nameof(Index), new { section = "links" });
        }

        TempData["Message"] = "已儲存系統設定。新的登入會套用更新；已登入的各系統 Cookie 不會因此自動清除。";
        return RedirectToAction(nameof(Index), new { section = "sso" });
    }

    /// <summary>
    /// 補齊選項後呈現表單，也用於保留驗證失敗時的輸入。
    /// </summary>
    private async Task<IActionResult> Editor(SsoSystemEditViewModel model)
    {
        model.Categories = await catalog.ListAsync();
        model.IdentityCatalog = await identities.LoadAsync();
        return View("Edit", model);
    }
}
