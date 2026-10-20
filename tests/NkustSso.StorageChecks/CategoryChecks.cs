using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

/// <summary>分類遷移與維護回歸測試；只操作獨立測試資料庫。</summary>
internal static class CategoryChecks
{
    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "categories");
        Directory.CreateDirectory(path);
        var factory = new DatabaseFactory(path);
        // 模擬升級前已存在的系統，確認既有分類及其他設定均保留。
        await using (var old = factory.Open())
        {
            await old.CreateTableAsync<RegisteredSystem>();
            await old.InsertAsync(new RegisteredSystem { ClientId = "legacy", Category = "custom_old", SortOrder = 7 });
            await old.InsertAsync(new RegisteredSystem { ClientId = "empty", Category = "" });
        }
        await factory.InitializeAsync();
        await using (var old = factory.Open())
            await old.ExecuteAsync("ALTER TABLE PortalCategories DROP COLUMN NameEn");
        await factory.InitializeAsync();
        await using var db = factory.Open();
        var catalog = new PortalCategoryService(factory);
        var service = new PortalCategoryManagementService(factory, TimeProvider.System);
        var rows = await catalog.ListAsync();
        Check(rows.Count == 10 && rows.Any(c => c.Key == "custom_old"), "Migration preserves unknown category.");
        Check((await db.RegisteredSystems.SingleAsync(s => s.ClientId == "legacy")).SortOrder == 7, "Migration preserves system order.");
        Check((await db.RegisteredSystems.SingleAsync(s => s.ClientId == "empty")).Category == "general", "Empty category migrated.");
        var category = rows.Single(c => c.Key == "academic");
        var oldRevision = category.Revision;
        category.Name = "教學服務";
        category.NameEn = "Teaching services";
        category.SortOrder = 1;
        await service.SaveAsync(category, false, "admin", "127.0.0.1");
        await Reject(() => service.SaveAsync(category, false, "admin", "127.0.0.1"));
        await Reject(() => service.DeleteAsync(category.Key, oldRevision, "admin", "127.0.0.1"));
        await Reject(() => service.DeleteAsync("general", rows.Single(c => c.Key == "general").Revision, "admin", "127.0.0.1"));
        await Reject(() => service.DeleteAsync("custom_old", rows.Single(c => c.Key == "custom_old").Revision, "admin", "127.0.0.1"));
        await Reject(() => service.SaveAsync(new PortalCategory { Key = "all", Name = "保留字" }, true, "admin", "127.0.0.1"));
        await Reject(() => service.SaveAsync(new PortalCategory { Key = "new", Name = "新分類", Accent = "arbitrary-css" }, true, "admin", "127.0.0.1"));
        await service.DeleteAsync("hr", rows.Single(c => c.Key == "hr").Revision, "admin", "127.0.0.1");
        await factory.InitializeAsync();
        rows = await catalog.ListAsync();
        Check(!rows.Any(c => c.Key == "hr") && rows[0].Name == "教學服務" && rows[0].NameEn == "Teaching services", "Restart preserves deletion/name/order.");
        // 預設分類可移至最前面，兜底仍依 general 代碼，而不是最後一項。
        var general = rows.Single(c => c.Key == "general");
        general.SortOrder = 0;
        await service.SaveAsync(general, false, "admin", "127.0.0.1");
        Check(PortalCategoryService.Of(await catalog.ListAsync(), "missing").Key == "general", "Fallback independent of category order.");
        Check(await db.PortalCategoryAudits.CountAsync() == 3, "Only committed category mutations are audited.");
        Check(await db.PortalCategoryAudits.AllAsync(a => a.ActorId == "admin" && a.RemoteIp == "127.0.0.1"), "Audit actor/IP recorded.");
        Console.WriteLine("PASS category migration, restart preservation, ordering, protected/in-use deletion, stale forms and audit");
    }

    private static async Task Reject(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Invalid category mutation was accepted.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
