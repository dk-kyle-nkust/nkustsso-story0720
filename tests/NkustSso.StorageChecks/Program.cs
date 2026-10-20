using System.Security.Claims;
using LinqToDB;
using LinqToDB.Async;
using NkustSso.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using OpenIddict.Abstractions;

var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "NkustSSO.slnx"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Run storage checks from within the repository.");
var seedOnly = args.Length == 2 && args[0] == "--seed-server";
var directory = seedOnly ? Path.GetFullPath(args[1]) : Path.Combine(root.FullName, ".local/storage-checks", Guid.NewGuid().ToString("N"));
if (seedOnly && Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.db").Any())
    throw new InvalidOperationException("Test fixtures require a fresh directory; refusing to alter existing databases.");
Directory.CreateDirectory(directory);
if (args.Contains("--account-status"))
{
    await using var statusDatabase = args.Contains("--sqlserver") ? await SqlServerTestDatabase.CreateAsync() : null;
    await AccountStatusChecks.RunAsync(directory, statusDatabase?.Factory);
    return;
}
if (args.Length == 2 && args[0] == "--campus-login-ui")
{
    await CampusAuthenticationChecks.RunAsync(directory, root.FullName, args[1]);
    return;
}
if (args.Length == 1 && args[0] == "--campus-authentication")
{
    await CampusAuthenticationChecks.RunAsync(directory);
    return;
}
if (args.Contains("--role-access"))
{
    await using var roleAccessDatabase = args.Contains("--sqlserver") ? await SqlServerTestDatabase.CreateAsync() : null;
    await AdminRoleAccessChecks.RunAsync(directory, roleAccessDatabase?.Factory);
    return;
}
if (args.Contains("--campus-role-members"))
{
    await using var roleDatabase = args.Contains("--sqlserver") ? await SqlServerTestDatabase.CreateAsync() : null;
    await CampusRoleMemberChecks.RunAsync(directory, roleDatabase?.Factory);
    return;
}
if (args.Length == 2 && args[0] == "--campus-role-ui")
{
    await CampusRoleMemberUiHost.RunAsync(root.FullName, directory, args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--sqlserver-browser")
{
    await SqlServerBrowserChecks.RunAsync(root.FullName, args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--import-sqlserver-fixture")
{
    var connection = Environment.GetEnvironmentVariable("NKUST_TEST_SQLSERVER_CONNECTION") ?? "";
    if (!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("NkustSSO_Test_", StringComparison.Ordinal))
        throw new InvalidOperationException("測試資料只能匯入隔離測試庫。");
    await SqliteStorageImport.ImportAsync(DatabaseFactory.ForSqlServer(connection), Path.GetFullPath(args[1]));
    return;
}
if (args.Length == 1 && args[0] == "--sqlserver-storage")
{
    await SqlServerStorageChecks.RunAsync(directory);
    return;
}
if (args.Contains("--admin-management"))
{
    await using var adminDatabase = args.Contains("--sqlserver") ? await SqlServerTestDatabase.CreateAsync() : null;
    await AdminManagementChecks.RunAsync(directory, adminDatabase?.Factory);
    return;
}
if (args.Length == 1 && args[0] == "--soap-migration")
{
    await LegacySoapMigrationChecks.RunAsync(directory);
    return;
}
if (args.Length == 2 && args[0] == "--integration-api")
{
    await IntegrationApiHost.RunAsync(directory, args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--catalog-ui")
{
    await CampusCatalogUiHost.RunAsync(root.FullName, directory, args[1]);
    return;
}
await using var sqlTest = args.Contains("--sqlserver") ? await SqlServerTestDatabase.CreateAsync() : null;
var factory = sqlTest?.Factory ?? new DatabaseFactory(directory);
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton(factory);
services.AddIdentityCore<Account>(options => options.Password.RequiredLength = 12)
    .AddRoles<IdentityRole>().AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
services.AddOpenIddict().AddCore(options =>
{
    options.SetDefaultApplicationEntity<OidcApplication>().SetDefaultAuthorizationEntity<OidcAuthorization>()
        .SetDefaultScopeEntity<OidcScope>().SetDefaultTokenEntity<OidcToken>();
    options.ReplaceApplicationStore<OidcApplication, LinqApplicationStore>()
        .ReplaceAuthorizationStore<OidcAuthorization, LinqAuthorizationStore>()
        .ReplaceScopeStore<OidcScope, LinqScopeStore>().ReplaceTokenStore<OidcToken, LinqTokenStore>();
    options.DisableEntityCaching();
});
await using var provider = services.BuildServiceProvider();
if (seedOnly)
{
    await ServerTestData.SeedAsync(provider, directory);
    return;
}
await ServerSetupChecks.RunAsync(provider, factory);
await PublicPortalChecks.RunAsync(directory);
await ServerTestData.SeedAsync(provider, directory);
await ServerTestData.SeedAsync(provider, directory);
await using var db = factory.Open();
Check(await db.Users.CountAsync() == 5, "Test fixtures and bootstrap administrator are idempotent.");
Check(await db.GetTable<OidcApplication>().CountAsync() == 2, "Exactly two fixture OIDC clients.");
using var scope = provider.CreateScope();
var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
var admin = (await users.FindByNameAsync("001"))!;
Check(await users.CheckPasswordAsync(admin, "123456"), "Administrator password.");
Check(await users.IsInRoleAsync(admin, LoginSessionAdministration.Role), "Administrator role.");
for (var i = 0; i < 5; i++) Check((await users.AccessFailedAsync(admin)).Succeeded, "Record failure.");
Check(await users.IsLockedOutAsync(admin), "Lockout must persist through linq2db.");
Check((await users.SetLockoutEndDateAsync(admin, null)).Succeeded, "Clear test lockout.");
Console.WriteLine("PASS fresh schema, idempotent seed, password hash, role and lockout");

var userStore = new LinqUserStore(factory);
var left = (await userStore.FindByIdAsync(admin.Id, default))!;
var right = (await userStore.FindByIdAsync(admin.Id, default))!;
Check((await userStore.UpdateAsync(left, default)).Succeeded, "First identity update.");
Check(!(await userStore.UpdateAsync(right, default)).Succeeded, "Stale identity update must fail.");
Console.WriteLine("PASS Identity optimistic concurrency");

var tokenStore = new LinqTokenStore(factory);
var token = new OidcToken { Status = "valid", Subject = admin.Id };
await tokenStore.CreateAsync(token, default);
var stale = (await tokenStore.FindByIdAsync(token.Id, default))!;
token.Status = "redeemed";
await tokenStore.UpdateAsync(token, default);
try { await tokenStore.UpdateAsync(stale, default); throw new Exception("Stale token update succeeded."); }
catch (OpenIddictExceptions.ConcurrencyException) { }
stale = (await tokenStore.FindByIdAsync(token.Id, default))!;
await tokenStore.RevokeBySubjectAsync(admin.Id, default);
try { await tokenStore.UpdateAsync(stale, default); throw new Exception("Revoked token was resurrected."); }
catch (OpenIddictExceptions.ConcurrencyException) { }
Console.WriteLine("PASS OIDC token concurrency and revoke cannot be undone");

var clock = new TestClock(DateTimeOffset.UtcNow);
var lifetime = new LoginSessionLifetimeService(Options.Create(new SessionLifetimeOptions()), clock);
var request = new DefaultHttpContext();
request.Request.Method = "GET";
request.Request.Path = "/";
var sessions = new LoginSessionStore(factory, new EphemeralDataProtectionProvider(), new HttpContextAccessor { HttpContext = request }, clock, lifetime);
AuthenticationTicket Ticket() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new Claim(ClaimTypes.NameIdentifier, admin.Id), new Claim(ClaimTypes.Name, "001")
}, IdentityConstants.ApplicationScheme)), new AuthenticationProperties
{
    IssuedUtc = clock.GetUtcNow(), ExpiresUtc = clock.GetUtcNow().AddHours(1)
}, IdentityConstants.ApplicationScheme);
var ticket = Ticket();
var key = await sessions.StoreAsync(ticket);
var original = ticket.Properties.ExpiresUtc;
clock.Advance(TimeSpan.FromMinutes(10));
ticket.Properties.ExpiresUtc = clock.GetUtcNow().AddHours(1);
await sessions.RenewAsync(key, ticket);
Check((await sessions.RetrieveAsync(key))?.Properties.ExpiresUtc?.ToUnixTimeSeconds() == original?.ToUnixTimeSeconds(), "Early claims refresh cannot extend expiry.");
clock.Advance(TimeSpan.FromMinutes(21));
await sessions.RenewAsync(key, ticket);
Check((await sessions.RetrieveAsync(key))?.Properties.ExpiresUtc?.ToUnixTimeSeconds() == clock.GetUtcNow().AddHours(1).ToUnixTimeSeconds(), "Eligible renewal extends window.");
await using var sessionDb = factory.OpenSessions();
var sessionId = ticket.Properties.Items[LoginSessionStore.SessionProperty];
Check(await LoginSessionAdministration.RevokeAsync(sessionDb, admin.Id, sessionId, admin.Id, "test", clock.GetUtcNow().ToUnixTimeMilliseconds()) == 1, "Revoke count.");
await sessions.RenewAsync(key, ticket);
Check(await sessions.RetrieveAsync(key) is null, "Renew cannot resurrect a revoked session.");
var expired = await sessions.StoreAsync(Ticket());
clock.Advance(TimeSpan.FromHours(1));
Check(await sessions.RetrieveAsync(expired) is null, "Session expires at exact boundary.");
Console.WriteLine("PASS renewal threshold, sliding expiry and revoke remain authoritative");
await SlidingCookieChecks.RunAsync(factory, admin.Id);
await CategoryChecks.RunAsync(directory);
await AdminManagementChecks.RunAsync(directory);
await AdminRoleAccessChecks.RunAsync(directory);
await CampusRoleMemberChecks.RunAsync(directory);
await IdentityChecks.RunAsync(directory);
await SourceReaderChecks.RunAsync(directory);
await CampusAuthenticationChecks.RunAsync(directory);
await CampusCatalogChecks.RunAsync(directory);
await IntegrationVerificationChecks.RunAsync(directory);
DepartmentHierarchyChecks.Run();
SamlChecks.Run(directory);
await SamlChecks.MigrationAsync(directory);
Console.WriteLine("All isolated storage checks passed.");

static void Check(bool result, string message) { if (!result) throw new Exception(message); }
sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}
