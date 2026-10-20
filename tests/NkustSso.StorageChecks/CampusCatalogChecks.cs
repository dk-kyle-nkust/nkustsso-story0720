using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

internal static class CampusCatalogChecks
{
    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "campus-catalog");
        Directory.CreateDirectory(path);
        var local = new DatabaseFactory(path);
        await local.InitializeAsync();
        var remote = new ReadOnlyFixture(path);
        await using (var db = remote.Setup())
        {
            await db.CreateTableAsync<SourceAccountStatusRecord>();
            await db.CreateTableAsync<DivisionRecord>();
            await db.CreateTableAsync<UnitRecord>();
            await db.CreateTableAsync<CampusAreaRecord>();
            await db.CreateTableAsync<StudentKindRecord>();
            await db.InsertAsync(new StudentKindRecord { Code = "1", Name = " 復學生 ", CampusCode = 1 });
            await db.InsertAsync(new StudentKindRecord { Code = "2", Name = "轉學生" });
            await db.CreateTableAsync<SourceAccountRecord>();
            await db.CreateTableAsync<StudentRecord>();
            await db.CreateTableAsync<ClassRecord>();
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "02", Name = "來源學生" });
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "01", Name = "來源教職員" });
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "99", Name = "來源其他" });
            await db.InsertAsync(new DivisionRecord { Code = "D", Name = "來源日間" });
            await db.InsertAsync(new UnitRecord { Code = "U1", Name = "來源單位", SourceSortOrder = 12 });
            await db.InsertAsync(new CampusAreaRecord { Code = 1, Name = " 來源校區全名 ", ShortName = "校區簡稱" });
            await db.InsertAsync(new CampusAreaRecord { Code = 2, Name = " ", ShortName = " 來源校區簡稱 " });
            await db.InsertAsync(new CampusAreaRecord { Code = 10 });
            await db.InsertAsync(new SourceAccountRecord { MemberId = "S1", StatusCode = "02" });
            await db.InsertAsync(new StudentRecord { StudentKey = 1, StudentId = "S1", ClassCode = "C1" });
            await db.InsertAsync(new ClassRecord { Code = "C1", UnitCode = "U1" });
        }
        var settings = new CampusAuthenticationOptions { Enabled = true };
        var source = new CampusIdentityCatalog(remote, Options.Create(settings), NullLogger<CampusIdentityCatalog>.Instance);
        var catalogService = new IdentityCatalog(local, TimeProvider.System, source);
        var catalog = await catalogService.LoadAsync();
        Check(catalog.SourceError is null && catalog.FromCampus && catalog.Options.Count == 10, "Catalog uses remote reference rows.");
        Check(catalog.Dimensions.Single(d => d.Key == "student_kind") is { Enabled: true, Family: "student", Name: "學生狀況" }
            && catalog.Option("student_kind", "1")?.Label == "復學生" && catalog.Option("student_kind", "2")?.Label == "轉學生"
            && catalog.Option("student_status", "1") is null, "Student kind labels retain raw codes without becoming academic status.");
        Check(catalog.Dimensions.Single(d => d.Key == "campus").Enabled
            && catalog.Option("campus", "1") is { Label: "來源校區全名" }
            && catalog.Option("campus", "2") is { Label: "來源校區簡稱" }
            && catalog.Option("campus", "10") is { Label: "10" }, "Campus names prefer full name, then abbreviation, then integer code.");
        Check(catalog.Options.Where(o => o.Dimension == "campus").Select(o => o.Code).SequenceEqual(["1", "2", "10"]),
            "Campus options retain numeric ordering.");
        Check(catalog.Option("identity_type", "02") is { Label: "來源學生", Family: "student" }
            && catalog.Option("identity_type", "99") is { Family: "other" }, "Raw status codes and source labels retained.");
        Check(!catalog.Options.Any(o => o.Code.StartsWith("DEMO_") || o.Code == "student"), "Demo and legacy options excluded from new choices.");
        Check(!catalog.Dimensions.Single(d => d.Key == "student_status").Enabled, "Unconfirmed sources cannot be newly selected.");
        var account = new Account { AuthenticationSource = CampusLoginService.SourceName, SourceAccountId = "S1", IdentityType = "student" };
        var profiles = await new AccountIdentityService(local, TimeProvider.System, source).LoadAsync(account);
        var exact = Rule("02");
        exact.ValidateCatalog(catalog, new());
        Check(exact.Matches(profiles, catalog) && !Rule("01").Matches(profiles, catalog), "Source code rules match current source account status.");
        var old = Rule("student");
        old.ValidateCatalog(catalog, old);
        Check(old.Matches(profiles, catalog), "Existing generic identity rules retain their meaning.");
        await Reject(() => { old.ValidateCatalog(catalog, new()); return Task.CompletedTask; });
        var unit = new PortalAudienceSelection { Mode = "rules", Groups = [new() { Family = "student", Conditions = [new() { Dimension = "department", Values = ["U1"] }] }] };
        Check(unit.Matches(profiles, catalog), "Student unit comes from the class join.");
        var campusRule = new PortalAudienceSelection { Mode = "rules", Groups = [new() { Family = "student", Conditions = [new() { Dimension = "campus", Values = ["1"] }] }] };
        campusRule.ValidateCatalog(catalog, new());
        Check(!campusRule.Matches(profiles, catalog), "Campus catalog does not invent an unconfirmed person-to-campus association.");
        var kindRule = new PortalAudienceSelection { Mode = "rules", Groups = [new() { Family = "student", Conditions = [new() { Dimension = "student_kind", Values = ["1"] }] }] };
        kindRule.ValidateCatalog(catalog, new());
        Check(!kindRule.Matches(profiles, catalog), "Source account status cannot substitute for missing student kind.");
        kindRule.Groups[0].Family = "employee";
        await Reject(() => { kindRule.ValidateCatalog(catalog, new()); return Task.CompletedTask; });
        await using (var db = remote.Setup())
            await db.GetTable<StudentKindRecord>().Where(s => s.Code == "2").Set(s => s.Name, " ").UpdateAsync();
        Check((await catalogService.LoadAsync()).Option("student_kind", "2")?.Label == "2", "Blank student kind name falls back to its code.");
        await using (var db = remote.Setup())
            await db.InsertAsync(new StudentKindRecord { Code = "1", Name = "同碼不同名稱", CampusCode = 2 });
        Check((await catalogService.LoadAsync()).SourceError is not null, "Ambiguous student kind codes across campuses require clarification.");
        await using (var db = remote.Setup())
            await db.GetTable<StudentKindRecord>().Where(s => s.CampusCode == 2).DeleteAsync();
        await Reject(() => catalogService.SaveOptionAsync(new() { Code = "invented", Label = "test" }, true, "admin", "test"));
        await Reject(() => catalogService.SaveDimensionAsync(new() { Key = "identity_type", Name = "test", Family = "any" }, false, "admin", "test"));
        await using (var db = remote.Setup())
            await db.GetTable<SourceAccountStatusRecord>().Where(s => s.Code == "02").Set(s => s.Name, "來源更新名稱").UpdateAsync();
        Check((await catalogService.LoadAsync()).Option("identity_type", "02")?.Label == "來源更新名稱", "Source edits appear without local synchronization.");
        await using (var db = remote.Setup())
        {
            await db.GetTable<CampusAreaRecord>().Where(c => c.Code == 1).Set(c => c.Name, "更新校區名稱").UpdateAsync();
            await db.InsertAsync(new CampusAreaRecord { Code = 1, Name = "更新校區名稱" });
        }
        var campusUpdate = await catalogService.LoadAsync();
        Check(campusUpdate.SourceError is null && campusUpdate.Options.Count(o => o.Dimension == "campus" && o.Code == "1") == 1
            && campusUpdate.Option("campus", "1")?.Label == "更新校區名稱", "Campus changes are live and identical duplicate codes are merged.");
        await using (var db = remote.Setup())
            await db.InsertAsync(new CampusAreaRecord { Code = 1, Name = "衝突校區名稱" });
        var campusConflict = await catalogService.LoadAsync();
        Check(campusConflict.SourceError is not null && campusConflict.Options.Count == 0, "Conflicting campus names do not select an arbitrary label.");
        await using (var db = remote.Setup())
            await db.GetTable<CampusAreaRecord>().Where(c => c.Name == "衝突校區名稱").DeleteAsync();
        await using (var db = remote.Setup())
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "02", Name = "衝突名稱" });
        var conflict = await catalogService.LoadAsync();
        Check(conflict.SourceError is not null && conflict.Options.Count == 0 && !exact.Matches(profiles, conflict), "Ambiguous classifications fail closed without falling back to demo data.");
        settings.Enabled = false;
        Check((await catalogService.LoadSourceAsync()).SourceError is not null, "Disabled connection renders a clear source error.");
        Console.WriteLine("PASS remote read-only classifications, source status rules, legacy compatibility, missing sources and mutation rejection");
    }
    private static PortalAudienceSelection Rule(string code) => new() { Mode = "rules", Groups = [new() { Family = "any", Conditions = [new() { Dimension = "identity_type", Values = [code] }] }] };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Unexpected mutation or invalid rule accepted."); }
    internal sealed class ReadOnlyFixture(string directory) : ICampusDatabaseFactory
    {
        public DataConnection Setup()
        {
            var db = new DataConnection(new DataOptions().UseSQLite($"Data Source={Path.Combine(directory, "remote.db")};Pooling=False", SQLiteProvider.Microsoft));
            db.Execute("ATTACH DATABASE @file AS dbo", new DataParameter("file", Path.Combine(directory, "source.db")));
            return db;
        }
        public DataConnection Open()
        {
            var db = Setup();
            db.Execute("PRAGMA query_only=ON");
            return db;
        }
    }
}
