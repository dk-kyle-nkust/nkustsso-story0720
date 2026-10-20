// 維護 JSON／SOAP 呼叫系統、密鑰及可取得的資料。
using System.Data.Common;
using System.Security.Claims;
using LinqToDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Controllers;

[Authorize(Policy = LoginSessionAdministration.Policy)]
[Route("admin/systems/api", Order = 0)]
[Route("admin/integration-services", Order = 1)]
public sealed class IntegrationClientManagementController(
    DatabaseFactory database,
    IntegrationClientRegistrationService registry,
    IOptions<IntegrationServiceOptions> options) : Controller
{
    /// <summary>呼叫紀錄只顯示最近這幾筆。</summary>
    private const int AuditCount = 50;

    [HttpGet("overview")]
    public IActionResult Overview()
    {
        return View(SoapHelpCatalog.Operations);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string kind = "existing")
    {
        if (!IsKnownKind(kind))
        {
            return BadRequest();
        }

        await using var db = database.Open();
        var clients = db.GetTable<IntegrationClient>();

        // 先依系統分類篩選，再取最近紀錄，避免另一分類的呼叫擠掉本頁紀錄。
        var audits = db.GetTable<IntegrationAudit>()
            .Where(a => clients.Any(c => c.Id == a.ClientId && c.ConnectionKind == kind));

        var list = await clients
            .Where(c => c.ConnectionKind == kind)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var recent = await audits
            .OrderByDescending(a => a.Id)
            .Take(AuditCount)
            .ToListAsync();

        return View(new IntegrationClientListViewModel(
            list,
            recent,
            options.Value,
            await db.GetTable<IntegrationClientService>().ToListAsync(),
            await db.GetTable<IntegrationCapability>().ToListAsync(),
            kind,
            await clients.CountAsync(c => c.ConnectionKind == "existing"),
            await clients.CountAsync(c => c.ConnectionKind == "new")));
    }

    /// <summary>existing 是既有校務系統，new 是新串接；網址可手改，不認的值直接回 400。</summary>
    private static bool IsKnownKind(string kind)
    {
        return kind == "existing" || kind == "new";
    }

    [HttpGet("new")]
    public Task<IActionResult> Create(string kind = "new")
    {
        if (!IsKnownKind(kind))
        {
            return Task.FromResult<IActionResult>(BadRequest());
        }

        return Editor(new IntegrationClientEditViewModel
        {
            Creating = true,
            Client = new IntegrationClientInput
            {
                ConnectionKind = kind,
                Protocol = kind == "existing" ? "soap" : "json"
            }
        });
    }

    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        await using var db = database.Open();

        var c = await db.GetTable<IntegrationClient>().SingleOrDefaultAsync(c => c.Id == id);
        if (c is null)
        {
            return NotFound();
        }

        var serviceIds = await db.GetTable<IntegrationClientService>()
            .Where(g => g.ClientId == id)
            .Select(g => g.ServiceId)
            .ToListAsync();

        var returnFieldKeys = await db.GetTable<IntegrationClientReturnField>()
            .Where(f => f.ClientId == id)
            .Select(f => f.FieldKey)
            .ToListAsync();

        return await Editor(new IntegrationClientEditViewModel
        {
            Client = new IntegrationClientInput
            {
                Id = c.Id,
                Name = c.Name,
                ConnectionKind = c.ConnectionKind,
                Notes = c.Notes,
                ApplicantName = c.ApplicantName,
                ContactName = c.ContactName,
                Purpose = c.Purpose,
                OwningUnit = c.OwningUnit,
                SourceSystem = c.SourceSystem,
                SourceId = c.SourceId,
                ServiceIds = serviceIds,
                ReturnFieldKeys = returnFieldKeys,
                CallerName = c.CallerName,
                AllowedFunctionIds = c.AllowedFunctionIds,
                ContactPhone = c.ContactPhone,
                ContactEmail = c.ContactEmail,
                Protocol = c.Protocol,
                Enabled = c.Enabled,
                AllowedIps = c.AllowedIps,
                Revision = c.Revision
            }
        });
    }

    [HttpPost("new")]
    public Task<IActionResult> Create(IntegrationClientEditViewModel model)
    {
        return Save(model, true, null);
    }

    [HttpPost("{id}/edit")]
    public Task<IActionResult> Edit(string id, IntegrationClientEditViewModel model)
    {
        return Save(model, false, id);
    }

    private async Task<IActionResult> Save(IntegrationClientEditViewModel model, bool creating, string? id)
    {
        model.Creating = creating;
        if (!creating && id != model.Client.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return await Editor(model);
        }

        try
        {
            var secret = await registry.SaveAsync(model.Client, creating, Actor(), Ip());
            if (secret is not null)
            {
                // 密鑰只在這一次回應顯示，不留在快取裡。
                Response.Headers.CacheControl = "no-store";
                var view = new IntegrationClientSecretViewModel(
                    model.Client.Id,
                    secret,
                    model.Client.ConnectionKind);
                return View("Secret", view);
            }

            TempData["Message"] = "已儲存串接設定；仍需部署設定開啟對應服務，才可呼叫驗證。";
            return RedirectToAction(nameof(Index), new { kind = model.Client.ConnectionKind });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("", exception.Message);
        }
        catch (DbException)
        {
            ModelState.AddModelError("", "儲存失敗，請重新載入並確認是否有同時修改。");
        }

        return await Editor(model);
    }

    private async Task<IActionResult> Editor(IntegrationClientEditViewModel model)
    {
        await using var db = database.Open();

        var known = LegacySoapOperationCatalog.Available
            .Select(LegacySoapOperationCatalog.Permission)
            .ToArray();
        model.Services = await db.GetTable<IntegrationCapability>()
            .Where(s => known.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync();

        // 密碼只用於當次雜湊，任何表單錯誤均不回填，也不顯示儲存值。
        model.Client.CallerPassword = null;
        ModelState.Remove("Client.CallerPassword");

        // 匯入批次與時間由移轉程序保存，不採用表單送來的值。
        var existing = model.Creating
            ? null
            : await db.GetTable<IntegrationClient>().SingleOrDefaultAsync(c => c.Id == model.Client.Id);
        model.ImportBatchId = existing?.ImportBatchId ?? "";
        model.ImportedAt = existing?.ImportedAt;

        return View("Edit", model);
    }

    [HttpPost("{id}/rotate")]
    public async Task<IActionResult> Rotate(string id, string revision)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        await using var db = database.Open();

        var kind = await db.GetTable<IntegrationClient>()
            .Where(c => c.Id == id)
            .Select(c => c.ConnectionKind)
            .SingleOrDefaultAsync() ?? "existing";

        try
        {
            // 密鑰只在這一次回應顯示，不留在快取裡。
            Response.Headers.CacheControl = "no-store";
            var secret = await registry.RotateAsync(id, revision, Actor(), Ip());
            return View("Secret", new IntegrationClientSecretViewModel(id, secret, kind));
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DbException)
        {
            TempData["Error"] = "更新失敗，請重新載入。";
        }

        return RedirectToAction(nameof(Index), new { kind });
    }

    private string Actor()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }

    private string Ip()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}
