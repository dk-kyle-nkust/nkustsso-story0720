using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class IdentityChecks
{
    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "identities");
        Directory.CreateDirectory(path);
        var factory = new DatabaseFactory(path);
        // 模擬沒有 RulesJson 欄位的舊資料庫。
        await using (var old = factory.Open())
        {
            await old.ExecuteAsync("CREATE TABLE PortalAudiences (ClientId TEXT PRIMARY KEY, IdentityTypesJson TEXT NOT NULL, StudentStatusesJson TEXT NOT NULL, EducationLevelsJson TEXT NOT NULL)");
            await old.ExecuteAsync("INSERT INTO PortalAudiences VALUES ('legacy', '[\"student\"]', '[\"OLD_STATUS\"]', '[]')");
        }
        await factory.InitializeAsync();
        await factory.InitializeAsync();
        var service = new IdentityCatalog(factory, TimeProvider.System);
        var catalog = await service.LoadAsync();
        Check(catalog.Dimensions.Count == 10 && catalog.Option("student_status", "OLD_STATUS") is not null, "Idempotent migration preserves legacy option codes.");
        await using var db = factory.Open();
        // 模擬已完成舊分類遷移的資料庫，確認新種類可補入且重啟不會重設名稱。
        await db.GetTable<IdentityDimension>().Where(d => d.Key == "student_kind").DeleteAsync();
        await db.GetTable<SchemaMigration>().Where(m => m.Id == "identity-student-kind-v1").DeleteAsync();
        await factory.InitializeAsync();
        Check(await db.GetTable<IdentityDimension>().CountAsync(d => d.Key == "student_kind" && d.Family == "student") == 1,
            "Existing databases receive the student kind dimension.");
        await db.GetTable<IdentityDimension>().Where(d => d.Key == "student_kind").Set(d => d.Name, "既有名稱").UpdateAsync();
        await factory.InitializeAsync();
        Check((await db.GetTable<IdentityDimension>().SingleAsync(d => d.Key == "student_kind")).Name == "既有名稱",
            "Student kind migration does not overwrite existing configuration on restart.");
        var legacy = PortalAudienceSelection.FromRow(await db.PortalAudiences.SingleAsync());
        Check(legacy.Matches([Profile("student", ("student_status", "OLD_STATUS"))], catalog), "Legacy rules keep meaning.");
        Check(!legacy.Matches([Profile("student", ("student_status", "DEMO_ACTIVE"))], catalog), "Legacy restrictions do not broaden.");

        var day = Rules("student", ("student_status", ["DEMO_ACTIVE"]), ("division", ["DEMO_DAY"]));
        day.ValidateCatalog(catalog, new());
        var multi = new[] { Profile("student", ("student_status", "DEMO_ACTIVE"), ("division", "DEMO_NIGHT")),
            Profile("student", ("student_status", "DEMO_GRADUATED"), ("division", "DEMO_DAY")),
            Profile("staff", ("employment_status", "DEMO_CURRENT")) };
        Check(!day.Matches(multi, catalog), "Cannot combine attributes across identities.");
        Check(day.Matches([Profile("student", ("student_status", "DEMO_ACTIVE"), ("division", "DEMO_DAY"))], catalog), "AND within a single identity.");
        Check(!day.Matches([Profile("student", ("student_status", "DEMO_ACTIVE"))], catalog), "Missing values fail specified conditions.");
        var either = PortalAudienceSelection.Parse(day.ToJson());
        either.Groups.Add(Rules("employee", ("identity_type", ["staff"]), ("employment_status", ["DEMO_CURRENT"])).Groups[0]);
        Check(either.Matches(multi, catalog), "OR between groups supports employee/student identities.");
        Check(Rules("student", ("student_status", ["DEMO_ACTIVE", "DEMO_GRADUATED"])).Matches(multi, catalog), "OR between selected values.");
        Check(!Rules("employee", ("identity_type", ["teacher"])).Matches(multi, catalog), "Staff are not automatically teachers.");
        foreach (var json in new[] { "{}", "null", "{", "{\"version\":2,\"mode\":\"rules\",\"groups\":[]}",
            "{\"version\":2,\"mode\":\"all\",\"groups\":[],\"unknown\":true}",
            Rules("student", ("division", [])).ToJson() })
        {
            await Reject(() => Task.FromResult(PortalAudienceSelection.Parse(json)));
            Check(!PortalAudienceSelection.FromRow(new() { RulesJson = json }).Matches(multi, catalog), "Corrupt stored rules fail closed.");
        }
        await Reject(() => { Rules("employee", ("student_status", ["DEMO_ACTIVE"])).ValidateCatalog(catalog, new()); return Task.CompletedTask; });
        await Reject(() => { Rules("student", ("division", ["invented"])).ValidateCatalog(catalog, new()); return Task.CompletedTask; });

        var account = new Account { Id = "multi", UserName = "multi", IdentityType = "student", StudentStatusCode = "DEMO_ACTIVE" };
        await db.InsertAsync(account);
        var source = new AccountIdentityService(factory, TimeProvider.System);
        Check((await source.LoadAsync(account)).Single().Value("student_status") == "DEMO_ACTIVE", "Legacy fallback before source adoption.");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var revision = await source.ReplaceAsync(account.Id, "test_source", null,
        [new() { Id = "student", IdentityType = "student", AttributesJson = "{\"student_status\":\"DEMO_ACTIVE\",\"division\":\"DEMO_NIGHT\"}" },
         new() { Id = "employee", IdentityType = "staff", AttributesJson = "{\"employment_status\":\"DEMO_CURRENT\"}" },
         new() { Id = "expired", IdentityType = "teacher", ValidUntil = now - 1000 },
         new() { Id = "future", IdentityType = "teacher", ValidFrom = now + 60000 },
         new() { Id = "disabled", IdentityType = "teacher", Enabled = false }]);
        Check((await source.LoadAsync(account)).Count == 2, "Multiple identities and validity periods.");
        await Reject(() => source.ReplaceAsync(account.Id, "test_source", null, []));
        await Reject(() => source.ReplaceAsync(account.Id, "wrong_source", revision, []));
        await Reject(() => source.ReplaceAsync(account.Id, "test_source", revision, [new() { Id = "bad", IdentityType = "student", AttributesJson = "{\"employment_status\":\"DEMO_CURRENT\"}" }]));
        Check((await source.LoadAsync(account)).Count == 2, "Rejected snapshot preserves last good data.");
        await source.ReplaceAsync(account.Id, "test_source", revision, []);
        Check((await source.LoadAsync(account)).Count == 0, "Empty authoritative snapshot cannot revive legacy identity.");
        Check(!either.Matches(await source.LoadAsync(account), catalog), "No effective identity fails group rules.");
        Check(new PortalAudienceSelection().Matches([], catalog), "Explicit all means all signed-in users.");

        var option = catalog.Option("division", "DEMO_DAY")!;
        var stale = JsonSerializer.Deserialize<IdentityOption>(JsonSerializer.Serialize(option))!;
        option.Label = "日間部新名稱";
        option.Enabled = false;
        await service.SaveOptionAsync(option, false, "admin", "127.0.0.1");
        await Reject(() => service.SaveOptionAsync(stale, false, "admin", "127.0.0.1"));
        catalog = await service.LoadAsync();
        day.ValidateCatalog(catalog, day);
        await Reject(() => { day.ValidateCatalog(catalog, new()); return Task.CompletedTask; });
        Check(!day.Matches([Profile("student", ("student_status", "DEMO_ACTIVE"), ("division", "DEMO_DAY"))], catalog), "Disabled option retains restriction and stops matching.");
        await service.SaveDimensionAsync(new() { Key = "college", Name = "學院", Family = "student" }, true, "admin", "127.0.0.1");
        await service.SaveOptionAsync(new() { Dimension = "college", Code = "engineering", Label = "工學院" }, true, "admin", "127.0.0.1");
        await factory.InitializeAsync();
        catalog = await service.LoadAsync();
        Check(catalog.Option("division", "DEMO_DAY") is { Enabled: false, Label: "日間部新名稱" }, "Restart preserves catalog edits.");
        Check(Rules("student", ("college", ["engineering"])).Matches([Profile("student", ("college", "engineering"))], catalog), "Custom deeper dimensions work.");
        Check(await db.GetTable<IdentityCatalogAudit>().CountAsync() == 3, "Only successful catalog changes audited.");
        Console.WriteLine("PASS identity migration, grouped rules, no cross-identity mixing, source snapshots, expiry, disabled options, custom dimensions and audit");
    }
    private static IdentityProfile Profile(string type, params (string, string)[] attributes) => new(type, attributes.ToDictionary(p => p.Item1, p => p.Item2));
    private static PortalAudienceSelection Rules(string family, params (string, string[])[] conditions) => new()
    { Mode = "rules", Groups = [new() { Family = family, Conditions = conditions.Select(c => new AudienceCondition { Dimension = c.Item1, Values = c.Item2 }).ToList() }] };
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Invalid identity configuration was accepted.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
