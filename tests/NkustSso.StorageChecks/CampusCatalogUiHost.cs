using NkustSso.Server.ViewModels;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LinqToDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

// 僅存在測試執行檔，使用合成分類與隔離 SQLite，不載入正式連線或帳號。
internal static class CampusCatalogUiHost
{
    public static async Task RunAsync(string root, string directory, string url)
    {
        if (new Uri(url).Host != "127.0.0.1") throw new InvalidOperationException("Fixture host must bind loopback.");
        var local = new DatabaseFactory(directory);
        await local.InitializeAsync();
        var remote = new CampusCatalogChecks.ReadOnlyFixture(directory);
        await using (var db = remote.Setup())
        {
            await db.CreateTableAsync<SourceAccountStatusRecord>();
            await db.CreateTableAsync<DivisionRecord>();
            await db.CreateTableAsync<UnitRecord>();
            await db.CreateTableAsync<CampusAreaRecord>();
            await db.CreateTableAsync<StudentKindRecord>();
            await db.InsertAsync(new StudentKindRecord { Code = "1", Name = "復學生" });
            await db.InsertAsync(new StudentKindRecord { Code = "2", Name = "轉學生" });
            await db.InsertAsync(new CampusAreaRecord { Code = 1, Name = "來源校區全名", ShortName = "來源校區簡稱" });
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "02", Name = "來源學生" });
            await db.InsertAsync(new SourceAccountStatusRecord { Code = "01", Name = "來源教職員" });
            await db.InsertAsync(new DivisionRecord { Code = "D", Name = "來源日間" });
            await db.InsertAsync(new UnitRecord { Code = "IC00", Name = "電算與網路中心", SourceSortOrder = -3 });
            await db.InsertAsync(new UnitRecord { Code = "IC01", Name = "測試網路組", SourceSortOrder = -2 });
            await db.InsertAsync(new UnitRecord { Code = "IC02", Name = "測試系統組", SourceSortOrder = -1 });
            for (var i = 0; i < 12; i++) await db.InsertAsync(new UnitRecord { Code = "U" + i, Name = "來源單位 " + i, SourceSortOrder = i });
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { ContentRootPath = Path.Combine(root, "NkustSso.Server"), EnvironmentName = "Development" });
        builder.WebHost.UseUrls(url);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(local);
        builder.Services.AddIdentityCore<Account>().AddRoles<IdentityRole>()
            .AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<ICampusDatabaseFactory>(remote);
        builder.Services.Configure<CampusAuthenticationOptions>(o => o.Enabled = true);
        builder.Services.AddScoped<CampusIdentityCatalog>();
        builder.Services.AddScoped<IdentityCatalog>();
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, CatalogFixtureAuthentication>("fixture", _ => { });
        builder.Services.AddAuthorization(o => o.AddPolicy(LoginSessionAdministration.Policy, p => p.RequireRole(LoginSessionAdministration.Role)));
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(IdentityCatalogController).Assembly).AddApplicationPart(typeof(CatalogFixtureController).Assembly);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            await next();
        });
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/health", () => "ok");
        await app.RunAsync();
    }
}
internal sealed class CatalogFixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
        new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture"),
            new Claim(ClaimTypes.Role, LoginSessionAdministration.Role)], "fixture")), "fixture")));
}
public sealed class CatalogFixtureController(IdentityCatalog catalog) : Controller
{
    [HttpGet("/fixture/audience")]
    public async Task<IActionResult> Audience()
    {
        RouteData.Values["controller"] = "Systems";
        return View("~/Views/SsoSystemManagement/Edit.cshtml", new SsoSystemEditViewModel
        {
            Creating = true, System = new RegisteredSystem { Protocol = "link" }, AudienceJson = new PortalAudienceSelection().ToJson(),
            IdentityCatalog = await catalog.LoadAsync(), Categories = [new PortalCategory { Key = "other", Name = "綜合服務" }]
        });
    }
}
