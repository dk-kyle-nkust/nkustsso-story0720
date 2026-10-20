using System.Security.Claims;
using System.Security.Cryptography;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Tests;

internal static class SqlServerStorageChecks
{
    public static async Task RunAsync(string directory)
    {
        await using var fixture = await SqlServerTestDatabase.CreateAsync();
        var factory = fixture.Factory;
        await factory.InitializeAsync(seedDefaults: false);
        await using var db = factory.Open();
        Check(await db.Users.CountAsync() == 0 && await db.RegisteredSystems.CountAsync() == 0, "僅建結構不寫入帳號或預設資料。");
        await Task.WhenAll(factory.InitializeAsync(), factory.InitializeAsync());
        await db.InsertAsync(new OidcToken { Id = "null-reference-1" });
        await db.InsertAsync(new OidcToken { Id = "null-reference-2" });
        var payload = new string('測', 20000);
        await db.InsertAsync(new OidcToken { Id = "payload", Payload = payload, ReferenceId = "CaseSensitive" });
        await db.InsertAsync(new OidcToken { Id = "payload-lower", ReferenceId = "casesensitive" });
        Check((await db.GetTable<OidcToken>().SingleAsync(t => t.Id == "payload")).Payload == payload, "長 Unicode Token 完整保存。");
        await Duplicate(() => db.InsertAsync(new OidcToken { Id = "duplicate", ReferenceId = "CaseSensitive" }));
        var entityId = "https://example.invalid/" + new string('x', 970);
        await db.InsertAsync(new RegisteredSystem { ClientId = "long-saml", Protocol = "saml", SamlEntityId = entityId });
        await Duplicate(() => db.InsertAsync(new RegisteredSystem { ClientId = "duplicate-saml", Protocol = "saml", SamlEntityId = entityId }));
        await db.InsertAsync(new RegisteredSystem { ClientId = "long-saml-case", Protocol = "saml", SamlEntityId = entityId.ToUpperInvariant() });
        await db.InsertAsync(new SamlLoginRequest { Id = "saml-1", RequestHash = "same-request" });
        await Duplicate(() => db.InsertAsync(new SamlLoginRequest { Id = "saml-2", RequestHash = "same-request" }));
        await db.InsertAsync(new IntegrationConsumedTicket { Hash = "soap-replay", ExpiresAt = 1 });
        await Duplicate(() => db.InsertAsync(new IntegrationConsumedTicket { Hash = "soap-replay", ExpiresAt = 1 }));
        var identity = new LinqUserStore(factory);
        Check((await identity.CreateAsync(new Account { Id = "unique-user", UserName = "one", NormalizedUserName = "ONE" }, default)).Succeeded, "Identity 建立帳號。");
        Check(!(await identity.CreateAsync(new Account { Id = "duplicate-user", UserName = "one", NormalizedUserName = "ONE" }, default)).Succeeded, "Identity 正確轉換 SQL 唯一性錯誤。");
        await db.ExecuteAsync("ALTER TABLE dbo.PortalCategories DROP COLUMN NameEn");
        await factory.InitializeAsync();
        Check(await db.Users.AnyAsync(u => u.Id == "unique-user"), "補欄位不遺失資料。");
        await TicketsAsync(factory);
        // 管理初始化檢查要求空資料庫，不能沿用前面已建立票券帳號的測試庫。
        await using (var adminFixture = await SqlServerTestDatabase.CreateAsync())
            await AdminManagementChecks.RunAsync(directory, adminFixture.Factory);
        Console.WriteLine("PASS MSSQL 空值索引、大小寫、長字串、SAML/SOAP 重放、欄位遷移、管理授權與並行票券");
        await ImportAsync(directory);
    }

    private static async Task TicketsAsync(DatabaseFactory factory)
    {
        await using var db = factory.Open();
        var account = new Account { Id = "ticket-user", UserName = "ticket-user", SecurityStamp = "stamp" };
        await db.InsertAsync(account);
        var clock = TimeProvider.System;
        var lifetime = new LoginSessionLifetimeService(Options.Create(new SessionLifetimeOptions()), clock);
        var sessions = new LoginSessionStore(factory, new EphemeralDataProtectionProvider(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, clock, lifetime);
        var auth = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id), new Claim(ClaimTypes.Name, account.UserName!),
            new Claim("large-test-claim", new string('資', 12000))
        }, IdentityConstants.ApplicationScheme)), new AuthenticationProperties
        { IssuedUtc = clock.GetUtcNow(), ExpiresUtc = clock.GetUtcNow().AddHours(1) }, IdentityConstants.ApplicationScheme);
        var key = await sessions.StoreAsync(auth);
        Check((await sessions.RetrieveAsync(key))!.Principal.FindFirst("large-test-claim")!.Value.Length == 12000, "大型加密 Cookie 票證完整保存。");
        var sessionId = auth.Properties.Items[LoginSessionStore.SessionProperty]!;
        var client = new TicketClient { Id = "parallel-client", RedirectUri = "https://example.invalid/callback", SecretHash = LoginTicketService.Hash("fixture") };
        await db.InsertAsync(client);
        await db.InsertAsync(new RegisteredSystem { ClientId = client.Id, Protocol = "ticket", Enabled = true });
        var service = new LoginTicketService(factory, sessions, lifetime, clock,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sso:Issuer"] = "https://example.invalid/" }).Build());
        var verifier = new string('a', 43);
        var raw = (await service.IssueAsync(client, account.Id, sessionId, LoginTicketService.Challenge(verifier), "127.0.0.1"))!;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.RedeemAsync(client, raw, client.RedirectUri, verifier, "127.0.0.1")));
        Check(results.Count(r => r is not null) == 1, "八個並行兌換只允許一次成功。");
        var revoked = (await service.IssueAsync(client, account.Id, sessionId, LoginTicketService.Challenge(verifier), "127.0.0.1"))!;
        await LoginSessionAdministration.RevokeAsync(db, account.Id, sessionId, account.Id, "test", clock.GetUtcNow().ToUnixTimeMilliseconds());
        Check(await service.RedeemAsync(client, revoked, client.RedirectUri, verifier, "127.0.0.1") is null, "撤銷後票券不可兌換。");
    }

    private static async Task ImportAsync(string directory)
    {
        var sourcePath = Path.Combine(directory, "sql-import");
        Directory.CreateDirectory(sourcePath);
        var sqlite = new DatabaseFactory(sourcePath);
        await sqlite.InitializeAsync();
        await using (var source = sqlite.Open())
        {
            await source.InsertAsync(new Account { Id = "import-user", DisplayName = "合成使用者", PasswordHash = "fixture-hash", LockoutEnd = DateTimeOffset.UtcNow.AddHours(2) });
            await source.InsertAsync(new LoginEvent { Id = 42, Outcome = "import-fixture", OccurredAt = DateTimeOffset.UtcNow });
            await source.ExecuteAsync("UPDATE LoginEvents SET Id = 42");
            await source.InsertAsync(new AdminRole { Id = "legacy-import-role", Name = "舊版不限期角色" });
            await source.ExecuteAsync("ALTER TABLE AdminRoles DROP COLUMN StartsAt");
            await source.ExecuteAsync("ALTER TABLE AdminRoles DROP COLUMN ExpiresAt");
            await source.ExecuteAsync("ALTER TABLE AspNetUsers DROP COLUMN SourceStatusCode");
            await source.ExecuteAsync("ALTER TABLE AspNetUsers DROP COLUMN SourceStatusName");
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var before = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(sourcePath, "sso.db")));
        await using var fixture = await SqlServerTestDatabase.CreateAsync();
        await using (var source = sqlite.OpenSessions())
            await source.InsertAsync(new IntegrationConsumedTicket { Hash = new string('x', 201) });
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { await SqliteStorageImport.ImportAsync(fixture.Factory, sourcePath); throw new Exception("未拒絕不相容資料。"); }
        catch (Microsoft.Data.SqlClient.SqlException) { }
        await using (var empty = fixture.Factory.Open())
            Check(await empty.Users.CountAsync() == 0, "後段失敗回滾先前已匯入的帳號。");
        await using (var source = sqlite.OpenSessions())
            await source.GetTable<IntegrationConsumedTicket>().DeleteAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var counts = await SqliteStorageImport.ImportAsync(fixture.Factory, sourcePath);
        await using var destination = fixture.Factory.Open();
        Check(counts["AspNetUsers"] == 1 && (await destination.Users.SingleAsync()).PasswordHash == "fixture-hash", "保留帳號及密碼雜湊。");
        var importedAccount = await destination.Users.SingleAsync();
        Check(importedAccount.SourceStatusCode == "" && importedAccount.SourceStatusName == "",
            "舊備份缺少狀態欄位仍可唯讀匯入，等待重新登入取得來源狀態。");
        Check((await destination.LoginEvents.SingleAsync()).Id == 42, "保留自動編號歷史紀錄。");
        await destination.InsertAsync(new LoginEvent { Outcome = "after-import" });
        Check(await destination.LoginEvents.AnyAsync(e => e.Id > 42), "匯入後自動編號延續。");
        try { await SqliteStorageImport.ImportAsync(fixture.Factory, sourcePath); throw new Exception("未拒絕重複匯入。"); }
        catch (InvalidOperationException) { }
        var after = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(sourcePath, "sso.db")));
        Check(before.SequenceEqual(after), "來源檔案保持不變。");
        await fixture.Factory.InitializeAsync();
        Check((await destination.Users.SingleAsync()).DisplayName == "合成使用者", "初始化保留匯入帳號。");
        Console.WriteLine("PASS SQLite 至 MSSQL 匯入、來源不變、Identity 編號延續及重複匯入拒絕");
    }

    private static async Task Duplicate(Func<Task<int>> action)
    {
        try { await action(); }
        catch (Exception error) when (SsoStorage.IsUniqueViolation(error)) { return; }
        throw new Exception("未拒絕重複值。");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
