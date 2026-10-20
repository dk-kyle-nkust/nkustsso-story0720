using System.Security.Cryptography;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using NkustSso.Server.Models;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

using NkustSso.Server.Services;

namespace NkustSso.Tests;

internal sealed record DemoSecrets(string Password, string AcademicSecret, string EquipmentSecret);

/// <summary>
/// 隔離測試專用資料，不編入 Server，也不供開發啟動使用。
/// </summary>
internal static class ServerTestData
{
    public const string TestPassword = "123456";

    /// <summary>
    /// 建立協定、管理介面測試所需的帳號與用戶端。
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, string localDirectory)
    {
        Directory.CreateDirectory(localDirectory);
        var file = Path.Combine(localDirectory, "demo-credentials.json");
        DemoSecrets secrets;
        if (File.Exists(file))
            secrets = JsonSerializer.Deserialize<DemoSecrets>(await File.ReadAllTextAsync(file))!;
        else
        {
            secrets = new(TestPassword,
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true }));
        }
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseFactory>().InitializeAsync();
        await SeedTicketClientAsync(scope.ServiceProvider.GetRequiredService<DatabaseFactory>());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        foreach (var user in new[]
        {
            new Account { Id = "demo-student-active", UserName = "002", DisplayName = "陳同學", ClassCode = "DEMO-CS-3A", ClassName = "資訊工程系 三年甲班", StudentStatusCode = "DEMO_ACTIVE", StudentStatusName = "在學", EducationLevelCode = "DEMO_UG", EducationLevelName = "大學部" },
            new Account { Id = "demo-student-leave", UserName = "003", DisplayName = "林同學", ClassCode = "DEMO-CS-M1", ClassName = "資訊工程系 碩士一班", StudentStatusCode = "DEMO_LEAVE", StudentStatusName = "休學", EducationLevelCode = "DEMO_MASTER", EducationLevelName = "碩士班" },
            new Account { Id = "demo-student-disabled", UserName = "004", DisplayName = "停用測試帳號", Enabled = false }
        })
        {
            if (await users.FindByIdAsync(user.Id) is not null) continue;
            user.PasswordHash = users.PasswordHasher.HashPassword(user, TestPassword);
            var result = await users.CreateAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        // 下列簡易帳密僅限本機示範；其他帳號仍使用原本至少 12 字元的密碼規則。
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(LoginSessionAdministration.Role))
        {
            var createdRole = await roles.CreateAsync(new IdentityRole(LoginSessionAdministration.Role));
            if (!createdRole.Succeeded) throw new InvalidOperationException("Cannot create the session administrator role.");
        }
        var administrator = await users.FindByIdAsync("demo-session-admin");
        if (administrator is null)
        {
            if (await users.FindByNameAsync("001") is not null)
                throw new InvalidOperationException("Account 001 already exists; refusing to overwrite an unrelated account.");
            administrator = new Account { Id = "demo-session-admin", UserName = "001", DisplayName = "系統管理員", IdentityType = "staff" };
            administrator.PasswordHash = users.PasswordHasher.HashPassword(administrator, TestPassword);
            var createdAdmin = await users.CreateAsync(administrator);
            if (!createdAdmin.Succeeded) throw new InvalidOperationException("Cannot create the development administrator.");
            var assignedRole = await users.AddToRoleAsync(administrator, LoginSessionAdministration.Role);
            if (!assignedRole.Succeeded) throw new InvalidOperationException("Cannot assign the session administrator role.");
        }
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in new[]
        {
            (Id: "academic", Name: "教務服務", Port: 7101, Secret: secrets.AcademicSecret),
            (Id: "equipment", Name: "設備借用", Port: 7102, Secret: secrets.EquipmentSecret)
        })
        {
            if (await manager.FindByClientIdAsync(client.Id) is not null) continue;
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = client.Id, ClientSecret = client.Secret, DisplayName = client.Name,
                ConsentType = ConsentTypes.Implicit,
                RedirectUris = { new Uri($"https://localhost:{client.Port}/signin-oidc") },
                PostLogoutRedirectUris = { new Uri($"https://localhost:{client.Port}/signout-callback-oidc") },
                Permissions =
                {
                    Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Profile, Permissions.Prefixes.Scope + OidcIdentityClaims.Scope
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            }); 
        }
        await SeedPortalAsync(scope.ServiceProvider.GetRequiredService<DatabaseFactory>());
    }

    private static async Task SeedTicketClientAsync(DatabaseFactory database)
    {
        await using var db = database.Open();
        if (await db.TicketClients.AnyAsync(c => c.Id == "library")) return;
        await db.InsertAsync(new TicketClient
        {
            Id = "library", DisplayName = "圖書服務", SecretHash = LoginTicketService.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            RedirectUri = "https://localhost:7103/sso/callback", IncludeStudentProfile = true
        });
    }

    /// <summary>
    /// 首次補入三個可登入示範入口；已存在的管理設定與密鑰不覆蓋。
    /// </summary>
    private static async Task SeedPortalAsync(DatabaseFactory database)
    {
        await using var db = database.Open();
        var categories = await db.PortalCategories.Select(c => c.Key).ToListAsync();
        foreach (var item in new[]
        {
            ("academic", "教務服務", "查看你的學籍資料、班級與學制資訊。", 7101, 10),
            ("equipment", "設備借用", "確認設備借用資格，查看個人可用功能。", 7102, 20),
            ("library", "圖書服務", "查看個人身分資料與圖書預約使用資格。", 7103, 30)
        })
        {
            if (await db.RegisteredSystems.AnyAsync(s => s.ClientId == item.Item1)) continue;
            var ticket = item.Item1 == "library";
            var originalTicket = ticket ? await db.TicketClients.SingleAsync(c => c.Id == item.Item1) : null;
            var originalOidc = ticket ? null : await db.GetTable<OidcApplication>().SingleAsync(c => c.ClientId == item.Item1);
            var category = item.Item1 switch { "academic" => "academic", "equipment" => "affairs", _ => "general" };
            await db.InsertAsync(new RegisteredSystem
            {
                ClientId = item.Item1, DisplayName = item.Item2, Description = item.Item3, SortOrder = item.Item5,
                Protocol = ticket ? "ticket" : "oidc", EntryUrl = $"https://localhost:{item.Item4}/",
                RedirectUri = ticket ? originalTicket!.RedirectUri : JsonSerializer.Deserialize<string[]>(originalOidc!.RedirectUris!)!.Single(),
                PostLogoutRedirectUri = ticket ? "" : JsonSerializer.Deserialize<string[]>(originalOidc!.PostLogoutRedirectUris!)!.SingleOrDefault() ?? "",
                Enabled = originalTicket?.Enabled ?? true,
                Category = categories.Contains(category) ? category : PortalCategoryService.DefaultKey,
                Keywords = "示範 demo 登入測試",
                ClaimMask = originalTicket is { IncludeStudentProfile: false } ? 7 : IdentityClaimReleasePolicy.All
            });
        }
    }
}
