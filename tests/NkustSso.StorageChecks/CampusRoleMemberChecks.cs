using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

internal static class CampusRoleMemberChecks
{
    internal static async Task<CampusCatalogChecks.ReadOnlyFixture> SourceAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var source = new CampusCatalogChecks.ReadOnlyFixture(directory);
        await using var db = source.Setup();
        // 刻意沒有密碼欄位：查詢與預先加入角色不得讀取來源密碼。
        await db.CreateTableAsync<SourceAccountRecord>();
        await db.CreateTableAsync<SourceAccountStatusRecord>();
        await db.InsertAsync(new SourceAccountStatusRecord { Code = "01", Name = "測試教職員" });
        foreach (var id in new[] { "E901", "E902", "E903", "E904", "E905", "DUP", "DUP" })
            await db.InsertAsync(new SourceAccountRecord { MemberId = id, Name = "測試人員 " + id, StatusCode = "01" });
        await db.InsertAsync(new SourceAccountRecord { MemberId = "INCOMPLETE", Name = "缺少身分對照", StatusCode = "99" });
        return source;
    }

    public static async Task RunAsync(string directory, DatabaseFactory? supplied = null)
    {
        var path = Path.Combine(directory, "role-members"); Directory.CreateDirectory(path);
        var factory = supplied ?? new DatabaseFactory(path); await factory.InitializeAsync();
        var source = await SourceAsync(Path.Combine(path, "source"));
        var settings = new CampusAuthenticationOptions { Enabled = true };
        var gates = new CampusLoginGates();
        var service = new CampusRoleMemberService(factory, source, Options.Create(settings), new UpperInvariantLookupNormalizer(),
            gates, TimeProvider.System, NullLogger<CampusRoleMemberService>.Instance);
        var management = new AdminManagementService(factory, TimeProvider.System);
        await management.SaveRoleAsync(new AdminRole { Id = "campus-role", Name = "校務人員角色" }, ["categories"], [], true, "root", "");
        await using var db = factory.Open();
        async Task<string> Revision() => (await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "campus-role")).Revision;
        var firstRevision = await Revision();
        var preview = await service.FindAsync(" E901 ");
        Check(preview.AccountName == "E901" && preview.IdentityType == "employee" && !await db.Users.AnyAsync(), "預覽僅查來源，不建立帳號。");
        await Reject(() => service.FindAsync("MISSING"));
        await Reject(() => service.FindAsync("DUP"));
        await Reject(() => service.FindAsync("INCOMPLETE"));
        settings.Enabled = false; await Reject(() => service.FindAsync("E901")); settings.Enabled = true;

        // 建立帳號不寫成員關聯也不換角色版本，編輯頁上其他還沒儲存的變更才不會被丟掉。
        var provisioned = await service.ProvisionAsync("campus-role", "E901", "root", "127.0.0.1");
        var account = await db.Users.SingleAsync(a => a.UserName == "E901");
        Check(provisioned.AccountId == account.Id && provisioned.DisplayName == "測試人員 E901", "回傳可直接放進成員欄的識別。");
        Check(account.PasswordHash is null && account.AuthenticationSource == CampusLoginService.SourceName && account.SourceAccountId == "E901", "帳號沒有本機密碼，使用既有校務登入。");
        Check(!await db.GetTable<AdminRoleMember>().AnyAsync() && await Revision() == firstRevision, "建立帳號不寫入成員，也不改角色版本。");
        Check(!await db.GetTable<AccountRole>().AnyAsync(), "不授予最高管理者資格。");

        // 成員關聯由角色表單一併儲存。
        var saved = await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "campus-role");
        await management.SaveRoleAsync(saved, ["categories"], [account.Id], false, "root", "");
        Check((await new AdminAuthorizationService(factory, TimeProvider.System).LoadAccountAsync(account.Id)).Can("categories"), "首次登入前已有角色權限。");

        await db.InsertAsync(new Account { UserName = "E902", NormalizedUserName = "E902" });
        await Reject(() => service.ProvisionAsync("campus-role", "E902", "root", ""));
        await db.InsertAsync(new Account { UserName = "E903", NormalizedUserName = "E903", AuthenticationSource = CampusLoginService.SourceName, SourceAccountId = "E903", Enabled = false });
        await Reject(() => service.ProvisionAsync("campus-role", "E903", "root", ""));
        await Reject(() => service.ProvisionAsync("campus-role", "MISSING", "root", ""));
        await Reject(() => service.ProvisionAsync("missing-role", "E904", "root", ""));
        Check(!await db.Users.AnyAsync(a => a.UserName == "E904"), "角色不存在時不建立帳號。");
        Check(await db.GetTable<AdminRoleMember>().CountAsync() == 1 && await db.GetTable<AdminManagementAudit>().CountAsync() == 3, "拒絕的異動不增加成員或稽核。");

        // 模擬第一次校務帳密驗證成功，確認沿用預先建立的識別與角色。
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(factory);
        services.AddIdentityCore<Account>().AddRoles<IdentityRole>().AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
        await using var provider = services.BuildServiceProvider();
        var login = new CampusLoginService(factory, provider.GetRequiredService<UserManager<Account>>(), new LoginSource(),
            Options.Create(settings), gates, TimeProvider.System, NullLogger<CampusLoginService>.Instance);
        var signedIn = await login.AuthenticateAsync("E901", "synthetic-password", null, default);
        Check(signedIn.Account?.Id == account.Id && (await new AdminAuthorizationService(factory, TimeProvider.System).LoadAccountAsync(account.Id)).Can("categories"), "首次登入沿用帳號及角色。");
        var changed = await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "campus-role");
        await management.SaveRoleAsync(changed, ["categories"], [], false, "root", "");
        Check(!(await new AdminAuthorizationService(factory, TimeProvider.System).LoadAccountAsync(account.Id)).Any, "預先加入的成員可正常移除及撤權。");
        var again = await service.ProvisionAsync("campus-role", "E901", "root", "");
        Check(again.AccountId == account.Id && await db.Users.CountAsync(a => a.UserName == "E901") == 1, "重新加入沿用既有校務帳號。");
        await db.Users.Where(a => a.UserName == "E903").Set(a => a.Enabled, true).Set(a => a.LockoutEnabled, true)
            .Set(a => a.LockoutEnd, (DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(5)).UpdateAsync();
        await Reject(() => service.ProvisionAsync("campus-role", "E903", "root", ""));

        // 在交易最後的稽核步驟製造失敗，確認不留下已新增的帳號。
        await management.SaveRoleAsync(new AdminRole { Id = "rollback-role", Name = "回滾測試" }, [], [], true, "root", "");
        var rollbackRevision = (await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "rollback-role")).Revision;
        await db.ExecuteAsync(factory.IsSqlServer
            ? "CREATE TRIGGER RoleMemberAuditFailure ON dbo.AdminManagementAudits AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE TargetId = 'rollback-role') THROW 51001, 'fixture failure', 1; END"
            : "CREATE TRIGGER RoleMemberAuditFailure BEFORE INSERT ON AdminManagementAudits WHEN NEW.TargetId = 'rollback-role' BEGIN SELECT RAISE(ABORT, 'fixture failure'); END");
        try
        {
            try { await service.ProvisionAsync("rollback-role", "E905", "root", ""); throw new Exception("稽核失敗應回滾。"); }
            catch (System.Data.Common.DbException) { }
        }
        finally { await db.ExecuteAsync("DROP TRIGGER RoleMemberAuditFailure"); }
        Check(!await db.Users.AnyAsync(a => a.UserName == "E905") &&
            (await db.GetTable<AdminRole>().SingleAsync(r => r.Id == "rollback-role")).Revision == rollbackRevision, "稽核失敗完整回滾新建帳號。");
        // 來源唯讀且沒有密碼欄位，確認正常加入及登入模擬沒有改寫來源。
        await using var remote = source.Open();
        Check(await remote.GetTable<SourceAccountRecord>().CountAsync() == 8, "來源資料保持不變。");
        Console.WriteLine("PASS 校務角色成員預覽、建立、登入銜接、同名衝突、缺漏與重複來源、停用、版本及稽核檢查");
    }


    private sealed class LoginSource : ICampusAccountSource
    {
        public Task<CampusAccountProfile?> AuthenticateAsync(string memberId, string password, CancellationToken ct) =>
            Task.FromResult<CampusAccountProfile?>(memberId == "E901" && password == "synthetic-password" ? new("E901", "測試人員", "employee") : null);
        public Task<bool> IsAllowedAsync(string memberId, string identityType, CancellationToken ct) => Task.FromResult(true);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("應拒絕的角色異動被接受。"); }
}
