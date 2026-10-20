using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class PublicPortalChecks
{
    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "public-portal");
        Directory.CreateDirectory(path);
        var factory = new DatabaseFactory(path);
        await using (var old = factory.Open())
        {
            await old.CreateTableAsync<RegisteredSystem>();
            await old.InsertAsync(new RegisteredSystem { ClientId = "private", DisplayName = "Internal", Category = "academic" });
            await old.InsertAsync(new RegisteredSystem { ClientId = "link-elearning", Protocol = "link", EntryUrl = "https://elearning.nkust.edu.tw/", DisplayName = "Custom name", Category = "academic" });
            await old.InsertAsync(new RegisteredSystem { ClientId = "link-stdsel", Protocol = "link", EntryUrl = "https://stdsel.nkust.edu.tw/dispatch", ShowOnPortal = false });
            await old.InsertAsync(new RegisteredSystem { ClientId = "link-selcrs", Protocol = "link", EntryUrl = "https://private.example/" });
            foreach (var column in new[] { "PublicStudent", "PublicEmployee", "PublicAlumni", "RequirePortalLogin", "RequireLoginToView", "DisplayNameEn", "DescriptionEn" })
                await old.ExecuteAsync($"ALTER TABLE RegisteredSystems DROP COLUMN {column}");
        }
        await factory.InitializeAsync();
        await PortalDefaultDataInitializer.InitializeAsync(factory);
        await using var db = factory.Open();
        Check(!await db.RegisteredSystems.AnyAsync(s => s.ClientId == "private" && (s.PublicStudent || s.PublicEmployee || s.PublicAlumni)), "Private registration stays private during schema migration.");
        Check(!await db.RegisteredSystems.AnyAsync(s => (s.ClientId == "link-stdsel" || s.ClientId == "link-selcrs") && (s.PublicStudent || s.PublicEmployee)), "Hidden and customized URLs are not published.");
        var original = await db.RegisteredSystems.SingleAsync(s => s.ClientId == "link-elearning");
        Check(original.DisplayName == "Custom name" && original.PublicStudent && original.PublicEmployee, "Known public link published with existing metadata intact.");
        Check(original.DisplayNameEn == "" && original.DescriptionEn == "", "Legacy services receive empty translation fields without changing Chinese content.");
        Check(!original.RequirePortalLogin, "Legacy external links remain public by default.");
        await db.RegisteredSystems.Where(s => s.ClientId == "link-elearning").Set(s => s.RequirePortalLogin, true).UpdateAsync();
        var catalog = new PortalDirectoryService(factory, new PortalCategoryService(factory), new AccountIdentityService(factory, TimeProvider.System), new IdentityCatalog(factory, TimeProvider.System));
        var student = (await catalog.LoadAsync("student")).Groups.SelectMany(g => g.Entries).ToList();
        Check(student.Any(s => s.Id == "link-elearning") && !student.Any(s => s.Id == "link-empservice" || s.Id == "private"), "Public area separation.");
        Check(student.Any(s => s.Id == "link-elearning" && s.RequiresLogin && !s.Sso), "Portal login gate does not imply federated SSO.");
        await db.RegisteredSystems.Where(s => s.ClientId == "link-elearning").Set(s => s.PublicStudent, false).UpdateAsync();
        await db.RegisteredSystems.Where(s => s.ClientId == "link-alumni-card").DeleteAsync();
        await factory.InitializeAsync();
        await PortalDefaultDataInitializer.InitializeAsync(factory);
        Check(!await db.RegisteredSystems.AnyAsync(s => s.ClientId == "link-elearning" && s.PublicStudent)
            && !await db.RegisteredSystems.AnyAsync(s => s.ClientId == "link-alumni-card"), "Restart preserves unpublishing and deletion.");
        Check((await db.RegisteredSystems.SingleAsync(s => s.ClientId == "link-elearning")).RequirePortalLogin, "Restart preserves explicit login gate.");
        await db.RegisteredSystems.Where(s => s.ClientId == "link-elearning")
            .Set(s => s.PublicStudent, true).Set(s => s.RequireLoginToView, true)
            .Set(s => s.Description, "Private metadata").UpdateAsync();
        if (!await db.PortalAudiences.AnyAsync(a => a.ClientId == "link-elearning"))
            await db.InsertAsync(new PortalAudience { ClientId = "link-elearning" });
        await db.PortalAudiences.Where(a => a.ClientId == "link-elearning")
            .Set(a => a.IdentityTypesJson, "[\"student\"]").Set(a => a.RulesJson, "").UpdateAsync();
        var pupil = new Account { Id = "private-student", IdentityType = "student" };
        var staff = new Account { Id = "private-staff", IdentityType = "staff" };
        Check(!(await catalog.VisibleAsync("student", null)).Any(s => s.ClientId == "link-elearning"), "Anonymous response excludes private metadata.");
        Check((await catalog.VisibleAsync("student", pupil, "link-elearning")).Count == 1, "Matching identity can see private services in the same directory.");
        Check((await catalog.VisibleAsync("student", staff, "link-elearning")).Count == 0, "Wrong identity cannot open a private entry directly.");
        Check((await catalog.VisibleAsync("student", pupil, "private")).Count == 1, "Legacy private entries remain private and visible to eligible signed-in accounts.");
        await db.PortalAudiences.Where(a => a.ClientId == "link-elearning").Set(a => a.IdentityTypesJson, "[\"staff\"]").UpdateAsync();
        Check((await catalog.VisibleAsync("student", pupil, "link-elearning")).Count == 0, "Opening an entry rechecks changed identity rules.");
        Console.WriteLine("PASS public directory migration, private metadata isolation and restart preservation");
    }

    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
