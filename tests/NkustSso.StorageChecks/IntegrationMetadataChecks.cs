using System.Net;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class IntegrationMetadataChecks
{
    public static async Task RunAsync(string root)
    {
        var path = Path.Combine(root, "integration-metadata");
        Directory.CreateDirectory(path);
        var factory = new DatabaseFactory(path);
        await using (var old = factory.Open())
        {
            await old.CreateTableAsync<IntegrationClient>();
            await old.InsertAsync(new IntegrationClient { Id = "existing", Name = "原有名稱", Enabled = true,
                AllowedIps = "127.0.0.1", Notes = "保留備註", Revision = "original", SecretHash = "unchanged" });
            foreach (var column in new[] { "ConnectionKind", "Purpose", "OwningUnit", "SourceSystem", "SourceId", "ImportBatchId", "ImportedAt" })
                await old.ExecuteAsync($"ALTER TABLE IntegrationClients DROP COLUMN {column}");
        }
        await factory.InitializeAsync();
        await factory.InitializeAsync();
        await using var db = factory.Open();
        var registry = new IntegrationClientRegistrationService(factory, TimeProvider.System);
        var draft = new IntegrationClientInput { Id = "draft", Name = "待確認的串接登記", Enabled = false };
        await registry.SaveAsync(draft, true, "admin", "test");
        draft.Revision = (await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == draft.Id)).Revision;
        draft.Enabled = true;
        await Reject(() => registry.SaveAsync(draft, false, "admin", "test"));
        Check(!(await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == draft.Id)).Enabled,
            "Drafts may omit IP addresses but cannot be enabled without them.");
        var existing = await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == "existing");
        Check(existing.Name == "原有名稱" && existing.Notes == "保留備註" && existing.Revision == "original" &&
            existing.SecretHash == "unchanged" && existing.Purpose == "" && existing.ImportedAt is null && existing.ConnectionKind == "existing", "Upgrade preserves old data and does not invent import records.");
        Check(await db.GetTable<IntegrationClientService>().CountAsync() == 1 &&
            await registry.AuthorizeAsync("soap", IPAddress.Loopback) is not null, "Upgrade preserves verification access exactly once.");
        Check(await db.GetTable<IntegrationAudit>().CountAsync(a => a.Action == "migrate") == 2,
            "服務與回傳欄位升級各記錄一次，重啟不重複。");
        var edit = new IntegrationClientInput { Id = existing.Id, Name = existing.Name, Enabled = true, AllowedIps = existing.AllowedIps,
            Revision = existing.Revision, Purpose = "系統用途", OwningUnit = "負責單位", SourceSystem = "source", SourceId = "Original-ID" };
        await registry.SaveAsync(edit, false, "admin", "test");
        await factory.InitializeAsync();
        Check(await registry.AuthorizeAsync("soap", IPAddress.Loopback) is null, "Revoked permission stays revoked after restart.");
        await Reject(() => registry.SaveAsync(edit, false, "admin", "test"));
        Check(!await db.GetTable<IntegrationClientService>().AnyAsync(), "Stale edits cannot restore grants.");
        var fresh = new IntegrationClientInput { Id = "fresh", Name = "新增", ConnectionKind = "new", Protocol = "json", Enabled = true, AllowedIps = "127.0.0.1" };
        var secret = (await registry.SaveAsync(fresh, true, "admin", "test"))!;
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, fresh.Id, secret) is null, "New clients need explicit service grants.");
        fresh.Revision = (await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == fresh.Id)).Revision;
        fresh.ConnectionKind = "invalid";
        await Reject(() => registry.SaveAsync(fresh, false, "admin", "test"));
        fresh.ConnectionKind = "new";
        fresh.SourceSystem = "source"; fresh.SourceId = "Original-ID";
        await Reject(() => registry.SaveAsync(fresh, false, "admin", "test"));
        fresh.SourceId = "";
        await Reject(() => registry.SaveAsync(fresh, false, "admin", "test"));
        fresh.SourceSystem = ""; fresh.ServiceIds = ["unknown"];
        await Reject(() => registry.SaveAsync(fresh, false, "admin", "test"));
        Check((await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == fresh.Id)).Revision == fresh.Revision,
            "Rejected updates are atomic.");
        fresh.ServiceIds = [IntegrationPermissions.VerifyAccount, IntegrationPermissions.VerifyAccount];
        fresh.Purpose = new string('長', 1001);
        await Reject(() => registry.SaveAsync(fresh, false, "admin", "test"));
        fresh.Purpose = "實際用途";
        await db.GetTable<IntegrationClient>().Where(c => c.Id == fresh.Id)
            .Set(c => c.ImportBatchId, "fixture-batch").Set(c => c.ImportedAt, (long?)1234567890000).UpdateAsync();
        await registry.SaveAsync(fresh, false, "admin", "test");
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, fresh.Id, secret) is not null,
            "Explicit grant enables existing verification endpoint.");
        Check(await db.GetTable<IntegrationClientService>().CountAsync(g => g.ClientId == fresh.Id) == 1, "Duplicate grant choices normalized.");
        var saved = await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == fresh.Id);
        Check(saved.ConnectionKind == "new", "Explicit classification saved independently of protocol.");
        Check(saved.ImportBatchId == "fixture-batch" && saved.ImportedAt == 1234567890000, "Normal editing preserves import metadata.");
        var audit = await db.GetTable<IntegrationAudit>().Where(a => a.ClientId == fresh.Id && a.Action == "update").OrderByDescending(a => a.Id).FirstAsync();
        using var detail = JsonDocument.Parse(audit.Detail);
        Check(detail.RootElement.GetProperty("AfterServices")[0].GetString() == IntegrationPermissions.VerifyAccount &&
            detail.RootElement.GetProperty("After").GetProperty("Purpose").GetString() == fresh.Purpose, "Metadata and grants audited together.");
        fresh.Revision = saved.Revision; fresh.ConnectionKind = "existing";
        await registry.SaveAsync(fresh, false, "admin", "test");
        await factory.InitializeAsync();
        var reclassified = await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == fresh.Id);
        Check(reclassified.ConnectionKind == "existing" && reclassified.Protocol == saved.Protocol && reclassified.SecretHash == saved.SecretHash &&
            await registry.AuthorizeAsync("json", IPAddress.Loopback, fresh.Id, secret) is not null,
            "Reclassification persists on restart without changing protocol, secret or authorization.");
        var classificationAudit = await db.GetTable<IntegrationAudit>().Where(a => a.ClientId == fresh.Id && a.Action == "update").OrderByDescending(a => a.Id).FirstAsync();
        using var classificationDetail = JsonDocument.Parse(classificationAudit.Detail);
        Check(classificationDetail.RootElement.GetProperty("Before").GetProperty("ConnectionKind").GetString() == "new" &&
            classificationDetail.RootElement.GetProperty("After").GetProperty("ConnectionKind").GetString() == "existing", "Classification change audited.");
        fresh.Revision = reclassified.Revision; fresh.ServiceIds = [];
        await registry.SaveAsync(fresh, false, "admin", "test");
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, fresh.Id, secret) is null, "Grant removal denies JSON with otherwise valid credentials.");
        Console.WriteLine("PASS integration metadata upgrade, source uniqueness, explicit service grants, revocation, atomic audit and import preservation");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid metadata change accepted."); }
}
