using System.Security.Claims;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Data.SqlClient;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

internal static class CampusAuthenticationChecks
{
    public static async Task RunAsync(string directory, string? root = null, string? url = null)
    {
        // 不連線資料庫，確認使用者設定不再被覆蓋，且預設仍保留憑證驗證。
        foreach (var trust in new string?[] { null, "false", "true" })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LoginAuth"] = "Server=example.invalid;Database=fixture;Encrypt=False;Persist Security Info=True;"
                    + (trust is null ? "" : $"TrustServerCertificate={trust};")
            }).Build();
            using var connection = new CampusDatabaseFactory(config, Options.Create(new CampusAuthenticationOptions { Enabled = true })).Open();
            var effective = new SqlConnectionStringBuilder(connection.ConnectionString);
            Check(effective.TrustServerCertificate == (trust == "true"), "Respect configured certificate trust, including the secure default.");
            Check(effective.Encrypt == SqlConnectionEncryptOption.Mandatory && !effective.PersistSecurityInfo, "Encryption and credential redaction remain enforced.");
        }
        // 固定測試向量涵蓋奇數／偶數交換及舊版 20 碼格式，不使用任何真實來源密碼。
        Check(TienFangPasswordVerifier.Verify("cB@f579!d", "Abc123!@"), "Even-position cipher compatibility.");
        Check(TienFangPasswordVerifier.Verify("dfdB579!@", "Abc123!@"), "Odd-position cipher compatibility.");
        Check(TienFangPasswordVerifier.Verify("Bdfhjl8024680246!@#$", "Abcdef1234567890!@#$"), "Twenty-character legacy cipher compatibility.");
        foreach (var value in new[] { "", "a", "z123", "!123", "cB@f579!d " })
            Check(!TienFangPasswordVerifier.Verify(value, "Abc123!@"), "Malformed or changed cipher rejected.");
        foreach (var value in new[] { "", "abc123!@", "Abc123!@ ", "Abc123!\uFF20" })
            Check(!TienFangPasswordVerifier.Verify("cB@f579!d", value), "Password is exact and never trimmed or case-folded.");

        var remotePath = Path.Combine(directory, "campus-source");
        Directory.CreateDirectory(remotePath);
        var remote = new FixtureDatabase(remotePath);
        await using (var db = remote.Open())
        {
            await db.CreateTableAsync<CredentialFixture>();
            await db.CreateTableAsync<SourceAccountStatusRecord>();
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "02", Name = "學生" });
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "10", Name = "已離職人員" });
            await db.CreateTableAsync<StudentRecord>();
            await db.CreateTableAsync<ClassRecord>();
            await db.CreateTableAsync<UnitRecord>();
            await db.CreateTableAsync<EmployeeRecord>();
            await db.InsertAsync(new CredentialFixture { MemberId = "S001", EncodedPassword = "cB@f579!d", StatusCode = "02" });
            await db.InsertAsync(new CredentialFixture { MemberId = "S002", EncodedPassword = "cB@f579!d", StatusCode = "02" });
            await db.InsertAsync(new CredentialFixture { MemberId = "E001", EncodedPassword = "cB@f579!d", StatusCode = "01" });
            foreach (var status in new string?[] { "10", "66", "99", "", null })
                await db.InsertAsync(new CredentialFixture { MemberId = "OTHER" + (status ?? "NULL"), EncodedPassword = "cB@f579!d", StatusCode = status });
            await db.InsertAsync(new CredentialFixture { MemberId = "S003", EncodedPassword = "cB@f579!d", StatusCode = "02" });
            await db.InsertAsync(new CredentialFixture { MemberId = "E002", EncodedPassword = "cB@f579!d", StatusCode = "01" });
            await db.InsertAsync(new CredentialFixture { MemberId = "E010", EncodedPassword = "cB@f579!d", StatusCode = "10" });
            await db.InsertAsync(new EmployeeRecord { EmployeeId = "E010", Name = " 測試離職人員 " });
            await db.InsertAsync(new StudentRecord { StudentKey = 1, StudentId = "S001", Name = "Example Student", ClassCode = "C001" });
            await db.InsertAsync(new StudentRecord { StudentKey = 2, StudentId = "S002", Name = "Another Student", ClassCode = "C001" });
            await db.InsertAsync(new ClassRecord { Code = "C001", Name = "Example Class", UnitCode = "U001", DegreeCode = "01" });
            await db.InsertAsync(new UnitRecord { Code = "U001", Name = "Example Unit" });
            await db.InsertAsync(new EmployeeRecord { EmployeeId = "E001", Name = "Example Employee" });
            // 職員單位：E003 對照唯一，E004 的代碼在來源重複，兩者都不得影響登入結果。
            await db.InsertAsync(new CredentialFixture { MemberId = "E003", EncodedPassword = "cB@f579!d", StatusCode = "01" });
            await db.InsertAsync(new EmployeeRecord { EmployeeId = "E003", Name = "測試職員", UnitCode = "U001" });
            await db.InsertAsync(new CredentialFixture { MemberId = "E004", EncodedPassword = "cB@f579!d", StatusCode = "01" });
            await db.InsertAsync(new EmployeeRecord { EmployeeId = "E004", Name = "多單位職員", UnitCode = "U009" });
            await db.InsertAsync(new UnitRecord { Code = "U009", Name = "第一校區單位", CampusCode = 1 });
            await db.InsertAsync(new UnitRecord { Code = "U009", Name = "第二校區單位", CampusCode = 2 });
        }
        if (root is not null && url is not null)
        {
            await CampusLoginUiHost.RunAsync(root, directory, url, remote);
            return;
        }
        var settings = new CampusAuthenticationOptions { Enabled = true };
        var options = Options.Create(settings);
        Check(settings.IsValid() && new CampusAuthenticationOptions { Enabled = true, StatusIdentityTypes = [] }.IsValid(), "SSO does not require a status allowlist or any identity mappings.");
        Check(!new CampusAuthenticationOptions { CommandTimeoutSeconds = 0 }.IsValid()
            && !new CampusAuthenticationOptions { CommandTimeoutSeconds = 31 }.IsValid()
            && !new CampusAuthenticationOptions { StatusIdentityTypes = new() { ["01"] = "admin" } }.IsValid(), "Invalid timeout and role-like identity mappings still fail validation.");
        var environment = new TestEnvironment();
        var realSource = new MssqlCampusAccountSource(remote, options, environment);
        Check(!settings.DevelopmentAllowAnyPassword, "Development password bypass is disabled by default.");
        Check(await realSource.AuthenticateAsync("S001", "arbitrary", default) is null,
            "Development alone does not bypass passwords.");
        settings.DevelopmentAllowAnyPassword = true;
        Check(await realSource.AuthenticateAsync("S001", "arbitrary", default) is { MemberId: "S001" },
            "Explicit Development mode accepts a nonblank password for an existing account.");
        Check(await realSource.AuthenticateAsync("MISSING", "arbitrary", default) is null,
            "Development mode does not invent accounts.");
        foreach (var password in new[] { "", " ", "\t", new string('a', 257) })
            Check(await realSource.AuthenticateAsync("S001", password, default) is null,
                "Development mode still rejects blank and oversized passwords.");
        foreach (var name in new[] { Environments.Production, Environments.Staging })
        {
            environment.EnvironmentName = name;
            Check(await realSource.AuthenticateAsync("S001", "arbitrary", default) is null,
                "Other environments cannot bypass password verification even when configured.");
            Check(await realSource.AuthenticateAsync("S001", "Abc123!@", default) is not null,
                "Other environments retain normal password verification.");
        }
        environment.EnvironmentName = Environments.Development;
        Check(!(await new CampusSoapAccountSource(remote).VerifyAsync("S001", "arbitrary", default)).Verified,
            "SOAP and JSON continue to verify the actual password.");
        settings.DevelopmentAllowAnyPassword = false;
        var source = new SwitchableSource(realSource);
        var profile = await source.AuthenticateAsync("S001", "Abc123!@", default);
        Check(profile is { IdentityType: "student", Name: "Example Student", ClassName: "Example Class" }, "Real source query verifies cipher and joins student details.");
        foreach (var suffix in new[] { "10", "66", "99", "", "NULL" })
        {
            Check((await source.AuthenticateAsync("OTHER" + suffix, "Abc123!@", default))?.IdentityType == "other", "Unknown and blank statuses authenticate as other.");
            Check(await source.IsAllowedAsync("OTHER" + suffix, "other", default), "Unknown status is also accepted by session validation.");
            Check(await source.AuthenticateAsync("OTHER" + suffix, "wrong", default) is null, "Unknown status still requires a valid password.");
        }
        Check(await source.AuthenticateAsync("S003", "Abc123!@", default) is { IdentityType: "student", Name: "S003", ClassName: "" }, "Missing student profile does not deny authentication.");
        Check(await source.AuthenticateAsync("E002", "Abc123!@", default) is { IdentityType: "employee", Name: "E002" }, "Missing employee profile does not deny authentication.");
        Check((await source.AuthenticateAsync("E001", "Abc123!@", default))?.IdentityType == "employee", "Employee remains unspecialized.");
        Check(await source.AuthenticateAsync("E003", "Abc123!@", default)
            is { IdentityType: "employee", Name: "測試職員", UnitCode: "U001", UnitName: "Example Unit" },
            "職員帶出所屬單位的代碼與名稱。");
        Check(await source.AuthenticateAsync("E004", "Abc123!@", default)
            is { IdentityType: "employee", Name: "多單位職員", UnitCode: "U009", UnitName: "" },
            "單位對照不唯一時名稱留空，不猜單位也不影響姓名與登入。");
        Check(await source.AuthenticateAsync("E001", "Abc123!@", default) is { UnitCode: "", UnitName: "" },
            "來源沒有單位代碼時不填單位。");
        Check(await source.AuthenticateAsync("S001", "Abc123!@", default) is { IdentityType: "student", UnitName: "" },
            "學生的系所來自班級，不走教職員單位欄位。");

        var localPath = Path.Combine(directory, "campus-local");
        Directory.CreateDirectory(localPath);
        var database = new DatabaseFactory(localPath);
        await database.InitializeAsync();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(database);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IOptions<CampusAuthenticationOptions>>(options);
        services.AddSingleton<ICampusAccountSource>(source);
        services.AddSingleton<CampusLoginGates>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddOptions<SessionLifetimeOptions>();
        services.AddSingleton<LoginSessionLifetimeService>();
        services.AddSingleton<CampusSessionValidator>();
        services.AddSingleton<LoginSessionStore>();
        services.AddScoped<CampusLoginService>();
        services.AddIdentityCore<Account>().AddRoles<IdentityRole>().AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var login = scope.ServiceProvider.GetRequiredService<CampusLoginService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var formerEmployee = (await login.AuthenticateAsync("E010", "Abc123!@", null, default)).Account;
        Check(formerEmployee is { IdentityType: "other", DisplayName: "測試離職人員", SourceStatusName: "已離職人員" },
            "離職人員保留原始狀態，並依員編取得人事姓名。");
        var formerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, formerEmployee!.Id)], "test"));
        var formerDisplay = await CurrentUserDisplayService.LoadAsync(users, formerPrincipal);
        Check(formerDisplay is { DisplayName: "測試離職人員", IdentityLabel: "已離職人員", UserName: "E010", CanManageSystems: false },
            "登入資訊同時顯示離職狀態、姓名與帳號，不新增管理權。");
        Check(formerDisplay is { UnitName: "" }, "離職人員不是在職職員，不顯示單位。");

        var staff = (await login.AuthenticateAsync("E003", "Abc123!@", null, default)).Account;
        Check(staff is { IdentityType: "employee", UnitCode: "U001", UnitName: "Example Unit" },
            "職員單位保存在本地顯示快照。");
        var staffPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, staff!.Id)], "test"));
        Check(await CurrentUserDisplayService.LoadAsync(users, staffPrincipal) is { UnitName: "Example Unit" },
            "登入資訊顯示職員所屬單位。");
        var other = (await login.AuthenticateAsync("OTHER10", "Abc123!@", null, default)).Account;
        Check(other is { IdentityType: "other", PasswordHash: null } && (await users.GetRolesAsync(other)).Count == 0,
            "Unmapped status provisions a normal account without administrator roles.");
        var savedOther = (await users.FindByIdAsync(other!.Id))!;
        Check(savedOther.SourceStatusCode == "10" && savedOther.SourceStatusName == "已離職人員",
            "校務原始代碼與名稱保存在本地顯示快照，不取代權限分類。");
        var otherPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, other.Id)], "test"));
        Check((await CurrentUserDisplayService.LoadAsync(users, otherPrincipal))?.IdentityLabel == "已離職人員",
            "登入資訊使用校務狀態名稱。");
        await using (var sourceDb = remote.Open())
            await sourceDb.GetTable<SourceAccountStatusRecord>().Where(s => s.Code == "10")
                .Set(s => s.Name, "測試來源更新名稱").UpdateAsync();
        Check((await login.AuthenticateAsync("OTHER10", "Abc123!@", null, default)).Account?.SourceStatusName == "測試來源更新名稱",
            "重新登入會更新來源狀態名稱，沒有固定寫死代碼對應。");
        Check((await users.FindByIdAsync(other.Id))?.SourceStatusName == "測試來源更新名稱",
            "既有帳號更新仍保存來源顯示快照。");
        for (var i = 0; i < 5; i++) Check((await login.AuthenticateAsync("S001", "wrong", null, default)).Account is null, "Bad password denied.");
        Check(await users.FindByNameAsync("S001") is null, "Failures do not provision source accounts.");
        Check((await login.AuthenticateAsync("S001", "Abc123!@", null, default)).Account is null, "Correct password cannot bypass lockout.");
        // 新服務實例仍讀取本地持久化的鎖定狀態。
        using (var secondScope = provider.CreateScope())
            Check((await secondScope.ServiceProvider.GetRequiredService<CampusLoginService>().AuthenticateAsync("S001", "Abc123!@", null, default)).Account is null, "Lockout survives service recreation.");
        clock.Advance(TimeSpan.FromMinutes(5));
        var account = (await login.AuthenticateAsync("S001", "Abc123!@", null, default)).Account;
        Check(account is not null && account.PasswordHash is null && account.AuthenticationSource == CampusLoginService.SourceName && account.ClassName == "Example Class", "Successful login provisions only local identity and profile snapshot.");
        var id = account!.Id;
        Check((await login.AuthenticateAsync("S001", "Abc123!@", id, default)).Account?.Id == id, "Repeated login preserves stable subject.");
        Check((await users.GetRolesAsync(account)).Count == 0, "Source identity never grants administrator roles.");
        var before = source.AuthenticateCalls;
        Check((await login.AuthenticateAsync("S002", "Abc123!@", id, default)).Account is null && source.AuthenticateCalls == before, "Signed-in user cannot switch accounts.");
        var local = new Account { UserName = "E001", DisplayName = "Local administrator" };
        Check((await users.CreateAsync(local)).Succeeded, "Prepare collision fixture.");
        before = source.AuthenticateCalls;
        Check((await login.AuthenticateAsync("E001", "Abc123!@", null, default)).Account is null && source.AuthenticateCalls == before,
            "Source credentials cannot take over a same-name local account.");
        source.Unavailable = true;
        Check((await login.AuthenticateAsync("S001", "Abc123!@", null, default)).Unavailable, "Source outage does not fall back to local authentication.");
        source.Unavailable = false;

        var sessions = provider.GetRequiredService<LoginSessionStore>();
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
        { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, "S001") }, "test")), new AuthenticationProperties(), "test");
        var key = await sessions.StoreAsync(ticket);
        var sessionId = ticket.Properties.Items[LoginSessionStore.SessionProperty];
        Check(await sessions.RetrieveAsync(key) is not null && await sessions.IsActiveAsync(sessionId, id), "Source-backed session can be retrieved and used by SSO.");
        await using (var db = remote.Open())
            await db.GetTable<CredentialFixture>().Where(x => x.MemberId == "S001").Set(x => x.StatusCode, "10").UpdateAsync();
        Check(!await sessions.IsActiveAsync(sessionId, id), "Changed identity requires refreshed claims before protocol issuance.");
        Check(await sessions.RetrieveAsync(key) is null, "Changed identity invalidates the stale browser session.");
        Check((await login.AuthenticateAsync("S001", "Abc123!@", null, default)).Account is { IdentityType: "other" }, "Changed status can immediately log in again with its new classification.");
        await using (var db = remote.Open())
            await db.GetTable<CredentialFixture>().Where(x => x.MemberId == "S001").Set(x => x.StatusCode, "02").UpdateAsync();
        Check(await sessions.RetrieveAsync(key) is null, "Restoring a classification does not resurrect revoked session.");
        settings.Enabled = false;
        Check(!await provider.GetRequiredService<CampusSessionValidator>().IsAllowedAsync(account), "Disabling integration invalidates external sessions.");
        settings.Enabled = true;
        Check(!await provider.GetRequiredService<CampusSessionValidator>().IsAllowedAsync(local), "Enabling integration disallows pre-existing local sessions.");
        await using (var sourceDb = remote.Open())
            await sourceDb.GetTable<EmployeeRecord>().Where(e => e.EmployeeId == "E010").Set(e => e.Name, " ").UpdateAsync();
        Check((await source.AuthenticateAsync("E010", "Abc123!@", default))?.Name == "E010",
            "離職人員缺少姓名時不臆測姓名。");
        await using (var sourceDb = remote.Open())
        {
            await sourceDb.GetTable<EmployeeRecord>().Where(e => e.EmployeeId == "E010").Set(e => e.Name, "原姓名").UpdateAsync();
            await sourceDb.InsertAsync(new EmployeeRecord { EmployeeId = "E010", Name = "不同姓名" });
        }
        Check((await source.AuthenticateAsync("E010", "Abc123!@", default))?.Name == "E010",
            "離職人員員編對應不唯一時，不任意選取某筆姓名。");
        await using (var db = remote.Open())
        {
            Check(await db.GetTable<CredentialFixture>().AllAsync(x => x.EncodedPassword == "cB@f579!d"), "Authentication never modifies source credentials.");
            await db.InsertAsync(new ClassRecord { Code = "C001", Name = "Ambiguous Class", UnitCode = "U001" });
        }
        Check(await source.AuthenticateAsync("S001", "Abc123!@", default) is { IdentityType: "student", Name: "S001", ClassName: "" }, "Ambiguous display joins do not deny login or select arbitrary profile data.");
        await using (var db = remote.Open())
        {
            await db.InsertAsync(new CredentialFixture { MemberId = "S001", EncodedPassword = "cB@f579!d", StatusCode = "02" });
        }
        Check(await source.AuthenticateAsync("S001", "Abc123!@", default) is null, "Duplicate source accounts fail closed.");
        settings.DevelopmentAllowAnyPassword = true;
        Check(await source.AuthenticateAsync("S001", "arbitrary", default) is null,
            "Development mode also rejects duplicate source accounts.");
        Console.WriteLine("PASS campus cipher compatibility, read-only source queries, provisioning, lockout, collision protection and session revocation (isolated SQLite)");
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CampusAuthenticationChecks";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixtureDatabase(string directory) : ICampusDatabaseFactory
    {
        public DataConnection Open()
        {
            var db = new DataConnection(new DataOptions().UseSQLite(
                $"Data Source={Path.Combine(directory, "sso.db")};Pooling=False", SQLiteProvider.Microsoft));
            db.Execute("ATTACH DATABASE @file AS dbo", new DataParameter("file", Path.Combine(directory, "dbo.db")));
            return db;
        }
    }

    private sealed class SwitchableSource(ICampusAccountSource source) : ICampusAccountSource
    {
        public bool Unavailable { get; set; }
        public int AuthenticateCalls { get; private set; }
        public Task<CampusAccountProfile?> AuthenticateAsync(string id, string password, CancellationToken ct)
        {
            AuthenticateCalls++;
            if (Unavailable) throw new TimeoutException();
            return source.AuthenticateAsync(id, password, ct);
        }
        public Task<bool> IsAllowedAsync(string id, string type, CancellationToken ct) => Unavailable
            ? throw new TimeoutException() : source.IsAllowedAsync(id, type, ct);
    }

    [Table("s90_auth_usr", Schema = "dbo", IsColumnAttributeRequired = true)]
    private sealed class CredentialFixture
    {
        [Column("usr_id")] public string MemberId { get; set; } = "";
        [Column("usr_mdpwd")] public string? EncodedPassword { get; set; }
        [Column("sts_id")] public string? StatusCode { get; set; }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
