using System.Security.Claims;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class AdminManagementChecks
{
    public static async Task RunAsync(string directory, DatabaseFactory? suppliedFactory = null)
    {
        var factory = suppliedFactory ?? new DatabaseFactory(Path.Combine(directory, "admin-management"));
        Directory.CreateDirectory(Path.Combine(directory, "admin-management")); await factory.InitializeAsync();
        var clock = TimeProvider.System; var service = new AdminManagementService(factory, clock); var access = new AdminAuthorizationService(factory, clock);
        await using var db = factory.Open();
        var defaultRole = await db.GetTable<AdminRole>().SingleAsync();
        Check(defaultRole.Id == "admin" && defaultRole.Name == "admin" && defaultRole.Enabled, "空白資料庫建立 admin 群組。");
        var defaultGrants = await db.GetTable<AdminRolePermission>().Where(g => g.RoleId == "admin").Select(g => g.Permission).ToListAsync();
        Check(defaultGrants.ToHashSet().SetEquals(new[] { "systems", "integrations", "categories", "identities", "sessions", "tickets", "progress", "manuals" }), "預設群組具有一般維護權，不含最高管理權。");
        Check(!await db.GetTable<AdminRoleMember>().AnyAsync() && !await db.Users.AnyAsync(), "預設群組不建立帳號或自動指派成員。");
        await db.GetTable<AdminRole>().Where(r => r.Id == "admin").Set(r => r.Name, "自訂群組").Set(r => r.Enabled, false).UpdateAsync();
        await db.GetTable<AdminRolePermission>().Where(g => g.RoleId == "admin").DeleteAsync();
        // 模擬已使用舊後台的資料庫；升級只補選單，不把手冊權限加回既有角色。
        await db.GetTable<AdminMenu>().Where(m => m.Id == "manuals").DeleteAsync();
        await db.GetTable<SchemaMigration>().Where(m => m.Id == "admin-manuals-v1").DeleteAsync();
        await factory.InitializeAsync();
        Check(await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == "manuals" && m.Target == "manuals"), "舊資料庫升級補上手冊入口。");
        await db.GetTable<AdminMenu>().Where(m => m.Id == "manuals").Set(m => m.Label, "自訂手冊").Set(m => m.Enabled, false).Set(m => m.SortOrder, 999).UpdateAsync();
        await factory.InitializeAsync();
        Check(await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == "manuals" && m.Label == "自訂手冊" && !m.Enabled && m.SortOrder == 999), "重啟保留手冊選單的自訂及停用。");
        await db.GetTable<AdminMenu>().Where(m => m.Id == "manuals").DeleteAsync();
        await factory.InitializeAsync();
        Check(!await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == "manuals"), "管理者刪除手冊選單後不會自動補回。");
        var retained = await db.GetTable<AdminRole>().SingleAsync();
        Check(retained.Name == "自訂群組" && !retained.Enabled && retained.Revision == defaultRole.Revision && !await db.GetTable<AdminRolePermission>().AnyAsync(), "重啟保留群組修改及撤權，不重複新增。");
        await db.GetTable<AdminRole>().DeleteAsync();
        await db.InsertAsync(new AdminRole { Id = "existing", Name = "既有群組" });
        await factory.InitializeAsync();
        Check((await db.GetTable<AdminRole>().SingleAsync()).Id == "existing", "有其他群組時不補建 admin。");
        await db.GetTable<AdminRole>().DeleteAsync();
        await db.InsertAsync(new AdminRoleMember { RoleId = "admin", AccountId = "orphan" });
        await db.InsertAsync(new AdminRolePermission { RoleId = "admin", Permission = "systems" });
        await factory.InitializeAsync();
        Check((await db.GetTable<AdminRole>().SingleAsync()).Id == "admin" && await db.GetTable<AdminRolePermission>().CountAsync() == AdminAuthorizationService.Features.Count(f => !f.RootOnly), "已有遷移記錄但群組清空時仍補建 admin。");
        Check(!await db.GetTable<AdminRoleMember>().AnyAsync(), "清空後重建不沿用殘留的舊成員關聯。");
        await db.InsertAsync(new Account { Id = "operator", UserName = "operator", Enabled = true });
        await db.InsertAsync(new Account { Id = "root", UserName = "root", Enabled = true });
        await db.InsertAsync(new IdentityRole { Id = "root-role", Name = LoginSessionAdministration.Role, NormalizedName = LoginSessionAdministration.Role.ToUpperInvariant() });
        await db.InsertAsync(new AccountRole { UserId = "root", RoleId = "root-role" });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "operator"), new Claim(ClaimTypes.Role, LoginSessionAdministration.Role) }, "test"));
        Check(!(await access.LoadAsync(principal)).Any, "不信任 Cookie 中宣稱的管理角色。");
        var role = new AdminRole { Id = "catalog", Name = "分類承辦" };
        await service.SaveRoleAsync(role, ["categories"], ["operator"], true, "root", "127.0.0.1");
        Check((await access.LoadAsync(principal)).Can("categories") && !(await access.LoadAsync(principal)).Can("systems"), "角色僅授予選定功能。");
        Check((await access.LoadAccountAsync("root")).Can("access"), "既有最高管理者保留完整權限。");
        await Reject(() => service.SaveRoleAsync(new AdminRole { Id = "bad", Name = "偽造" }, ["access"], ["operator"], true, "root", ""));
        await Reject(() => service.SaveRoleAsync(new AdminRole { Id = "bad", Name = "不存在成員" }, ["categories"], ["missing"], true, "root", ""));
        Check(!await db.GetTable<AdminRole>().AnyAsync(r => r.Id == "bad"), "失敗不留下角色或部分授權。");
        var original = await db.GetTable<AdminRole>().SingleAsync(r => r.Id == role.Id);
        var next = new AdminRole { Id = original.Id, Name = original.Name, Revision = original.Revision, Enabled = false };
        await service.SaveRoleAsync(next, ["categories"], ["operator"], false, "root", "");
        Check(!(await access.LoadAsync(principal)).Any, "停用立即撤除權限。");
        await Reject(() => service.SaveRoleAsync(original, ["systems"], [], false, "root", ""));
        Check(!await db.GetTable<AdminRolePermission>().AnyAsync(g => g.RoleId == role.Id && g.Permission == "systems"), "舊表單不能覆蓋新權限。");
        await service.SaveRoleAsync(new AdminRole { Id = "reader", Name = "紀錄查詢" }, ["tickets"], ["operator"], true, "root", "");
        Check((await access.LoadAsync(principal)).Can("tickets"), "多角色獨立提供功能。");
        await db.GetTable<Account>().Where(a => a.Id == "operator").Set(a => a.Enabled, false).UpdateAsync();
        Check(!(await access.LoadAsync(principal)).Any, "帳號停用優先於角色。");
        await db.GetTable<Account>().Where(a => a.Id == "operator").Set(a => a.Enabled, true).UpdateAsync();
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "SsoTicketAudit" }), "test"));
        Check(await access.AuthorizeAsync(principal, context), "使用實際控制器權限。");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "AdminRoleManagement" }), "test"));
        Check(!await access.AuthorizeAsync(principal, context), "一般角色不能管理權限。");
        var currentRole = await db.GetTable<AdminRole>().SingleAsync(r => r.Id == original.Id);
        await Reject(() => service.DeleteRoleAsync(original.Id, currentRole.Revision, "root", ""));
        var menu = await db.GetTable<AdminMenu>().SingleAsync(m => m.Id == "tickets");
        menu.Label = "新的紀錄入口"; menu.SortOrder = 1;
        await service.SaveMenuAsync(menu, false, "root", "");
        await Reject(() => service.SaveMenuAsync(menu, false, "root", ""));
        Check((await access.NavigationAsync(principal)).Single().Label == "新的紀錄入口", "選單依權限及資料庫設定顯示。");
        var menuAfter = await db.GetTable<AdminMenu>().SingleAsync(m => m.Id == "tickets");
        await service.DeleteMenuAsync(menuAfter.Id, menuAfter.Revision, "root", "");
        Check((await access.LoadAsync(principal)).Can("tickets") && await access.LandingAsync(principal) == "/admin/tickets", "隱藏導覽不等於撤權，仍有可用入口。");
        await Reject(() => service.DeleteMenuAsync("access", "any", "root", ""));
        await Reject(() => service.SaveMenuAsync(new AdminMenu { Id = "access", Label = "壞入口", Enabled = false, Target = "access" }, false, "root", ""));
        await Reject(() => service.SaveMenuAsync(new AdminMenu { Id = "bad", Label = "任意網址", Target = "https://example.invalid/" }, true, "root", ""));
        await factory.InitializeAsync();
        Check(!await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == "tickets"), "重啟不還原刪除的選單。");
        Check(!(await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "catalog")).Enabled, "重啟不還原停用的角色。");
        Check(await db.GetTable<AdminManagementAudit>().CountAsync() == 5, "成功異動與稽核同時寫入，失敗無稽核殘留。");
        Console.WriteLine("PASS admin role authorization, root preservation, stale writes, atomic audits, navigation, migration and protected controls");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action) { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("非法或過期異動未拒絕。"); }
}
