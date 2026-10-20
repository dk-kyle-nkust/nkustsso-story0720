using System.Security.Claims;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class AdminRoleAccessChecks
{
    public static async Task RunAsync(string directory, DatabaseFactory? supplied = null)
    {
        var path = Path.Combine(directory, "role-access"); Directory.CreateDirectory(path);
        var factory = supplied ?? new DatabaseFactory(path); await factory.InitializeAsync();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var management = new AdminManagementService(factory, clock); var access = new AdminAuthorizationService(factory, clock);
        await using var db = factory.Open();
        await db.InsertAsync(new Account { Id = "role-reader", UserName = "role-reader" });
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "role-reader")], "fixture"));
        await management.SaveRoleAsync(new AdminRole { Id = "reader", Name = "查詢角色" }, ["categories:read", "systems:read", "integrations:read", "sessions:read"], ["role-reader"], true, "root", "");
        async Task<bool> Allowed(string controller, string action, string method)
        {
            var http = new DefaultHttpContext(); http.Request.Method = method;
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor
            { ControllerName = controller, ActionName = action }), "fixture"));
            return await access.AuthorizeAsync(principal, http);
        }
        foreach (var controller in new[] { "PortalCategoryManagement", "SsoSystemManagement", "IntegrationClientManagement", "LoginSessionManagement" })
        {
            Check(await Allowed(controller, "Index", "GET") && await Allowed(controller, "Index", "HEAD"), "查詢角色可讀清單。");
            foreach (var action in new[] { "Create", "Edit", "Delete", "Rotate", "Revoke", "Unknown" })
            {
                Check(!await Allowed(controller, action, "GET") && !await Allowed(controller, action, "POST"), "查詢角色不能進入維護表單或偽造異動。");
            }
            Check(!await Allowed(controller, "Index", "POST"), "不能偽造查詢動作繞過 POST 防護。");
        }
        Check(!await Allowed("AdminRoleManagement", "Index", "GET"), "查詢授權不包含最高管理功能。");
        var start = clock.GetUtcNow().AddMinutes(1).ToUnixTimeMilliseconds();
        await management.SaveRoleAsync(new AdminRole { Id = "temporary", Name = "臨時維護", StartsAt = start, ExpiresAt = start + 60000 },
            ["categories", "categories:read"], ["role-reader"], true, "root", "");
        Check(!(await access.LoadAsync(principal)).CanManage("categories"), "角色尚未開始不提供維護權限。");
        clock.Advance(TimeSpan.FromMinutes(1));
        Check(await Allowed("PortalCategoryManagement", "Create", "POST"), "開始時間邊界立即生效。");
        Check(await db.GetTable<AdminRolePermission>().CountAsync(g => g.RoleId == "temporary") == 1, "維護授權已含查詢，不重複儲存。");
        clock.Advance(TimeSpan.FromMinutes(1));
        Check(!(await access.LoadAsync(principal)).CanManage("categories") && (await access.LoadAsync(principal)).Can("categories"), "到期即撤銷維護，其他角色查詢權限保留。");
        var invalid = new AdminRole { Id = "bad", Name = "錯誤期限", StartsAt = start, ExpiresAt = start };
        try { await management.SaveRoleAsync(invalid, ["categories"], [], true, "root", ""); throw new Exception("無效期限被接受。"); }
        catch (InvalidOperationException) { }
        Check(!await db.GetTable<AdminRole>().AnyAsync(r => r.Id == "bad"), "無效期限不留下角色。");

        // 模擬舊版結構；升級必須保留成員、既有權限與角色狀態，期限預設不限。
        await db.ExecuteAsync("ALTER TABLE AdminRoles DROP COLUMN StartsAt");
        await db.ExecuteAsync("ALTER TABLE AdminRoles DROP COLUMN ExpiresAt");
        await factory.InitializeAsync();
        var legacy = await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "temporary");
        Check(legacy.StartsAt is null && legacy.ExpiresAt is null && legacy.Name == "臨時維護" && legacy.Enabled, "舊資料升級保留角色，期限預設不限。");
        Check((await access.LoadAsync(principal)).CanManage("categories") && await db.GetTable<AdminRoleMember>().CountAsync() == 2, "升級保留既有維護授權與成員。");
        Console.WriteLine("PASS 查詢與維護分權、直接網址及 POST 防護、期限邊界、多角色合併與舊資料升級");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
