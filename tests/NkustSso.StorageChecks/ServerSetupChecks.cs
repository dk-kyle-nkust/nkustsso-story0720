using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class ServerSetupChecks
{
    public static async Task RunAsync(IServiceProvider provider, DatabaseFactory factory)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        await SsoStartupInitializer.InitializeAsync(provider, configuration);
        await SsoStartupInitializer.InitializeAsync(provider, configuration);
        await using var db = factory.Open();
        Check(await db.Users.CountAsync() == 0 && await db.RegisteredSystems.AllAsync(s => s.Protocol == "link" && !s.Enabled)
            && await db.TicketClients.CountAsync() == 0 && await db.GetTable<OidcApplication>().CountAsync() == 0,
            "Fresh server must not seed accounts or clients.");
        configuration["Sso:Administrator:UserName"] = "bootstrap-admin";
        configuration["Sso:Administrator:Password"] = "short";
        try
        {
            await SsoStartupInitializer.InitializeAsync(provider, configuration);
            throw new Exception("Weak administrator password accepted.");
        }
        catch (InvalidOperationException) { }
        Check(await db.Users.CountAsync() == 0, "Rejected bootstrap must not leave an account.");
        configuration["Sso:Administrator:Password"] = "Bootstrap!Password123";
        await SsoStartupInitializer.InitializeAsync(provider, configuration);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var admin = (await users.FindByNameAsync("bootstrap-admin"))!;
        Check(await users.CheckPasswordAsync(admin, "Bootstrap!Password123")
            && await users.IsInRoleAsync(admin, LoginSessionAdministration.Role), "Explicit administrator bootstrap.");
        Check((await users.RemoveFromRoleAsync(admin, LoginSessionAdministration.Role)).Succeeded, "Remove role.");
        Check((await users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddHours(1))).Succeeded, "Lock account.");
        configuration["Sso:Administrator:Password"] = "Replacement!Password123";
        await SsoStartupInitializer.InitializeAsync(provider, configuration);
        admin = (await users.FindByNameAsync("bootstrap-admin"))!;
        Check(await db.Users.CountAsync() == 1 && await users.CheckPasswordAsync(admin, "Bootstrap!Password123")
            && await users.IsLockedOutAsync(admin) && !await users.IsInRoleAsync(admin, LoginSessionAdministration.Role),
            "Restart must preserve password, lockout and revoked role.");
        Console.WriteLine("PASS public links only initialization, explicit administrator and restart preservation");
    }

    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
