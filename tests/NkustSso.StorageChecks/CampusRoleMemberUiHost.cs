using System.Security.Claims;
using System.Text.Encodings.Web;
using LinqToDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

// 合成來源與登入僅存在測試組件；實際控制器、授權政策與畫面沿用 Server。
internal static class CampusRoleMemberUiHost
{
    public static async Task RunAsync(string root, string directory, string url)
    {
        if (new Uri(url).Host != "127.0.0.1") throw new InvalidOperationException("測試網站只能繫結迴路位址。");
        var local = new DatabaseFactory(directory); await local.InitializeAsync();
        var source = await CampusRoleMemberChecks.SourceAsync(Path.Combine(directory, "source"));
        await using (var db = local.Open())
        {
            await db.InsertAsync(new Account { Id = "fixture-root", UserName = "fixture-root", NormalizedUserName = "FIXTURE-ROOT" });
            await db.InsertAsync(new Account { Id = "fixture-member", UserName = "fixture-member", NormalizedUserName = "FIXTURE-MEMBER" });
            await db.InsertAsync(new IdentityRole { Id = "fixture-root-role", Name = LoginSessionAdministration.Role, NormalizedName = LoginSessionAdministration.Role.ToUpperInvariant() });
            await db.InsertAsync(new AccountRole { UserId = "fixture-root", RoleId = "fixture-root-role" });
        }
        await new AdminManagementService(local, TimeProvider.System).SaveRoleAsync(new AdminRole { Id = "campus-role", Name = "校務承辦人" }, ["categories"], [], true, "fixture-root", "");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { ContentRootPath = Path.Combine(root, "NkustSso.Server"), EnvironmentName = "Development" });
        builder.WebHost.UseUrls(url);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(local);
        builder.Services.AddSingleton<ICampusDatabaseFactory>(source);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddIdentityCore<Account>().AddRoles<IdentityRole>().AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
        builder.Services.Configure<CampusAuthenticationOptions>(o => o.Enabled = true);
        builder.Services.AddSingleton<CampusLoginGates>();
        builder.Services.AddScoped<CampusRoleMemberService>();
        builder.Services.AddScoped<AdminManagementService>();
        builder.Services.AddScoped<AdminAuthorizationService>();
        builder.Services.AddAuthentication("fixture-role").AddScheme<AuthenticationSchemeOptions, RoleFixtureAuthentication>("fixture-role", _ => { });
        builder.Services.AddAuthorization(o => o.AddPolicy(LoginSessionAdministration.Policy, p => p.RequireAuthenticatedUser().RequireAssertion(async context =>
            context.Resource is HttpContext http && await http.RequestServices.GetRequiredService<AdminAuthorizationService>().AuthorizeAsync(context.User, http))));
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())).AddApplicationPart(typeof(AdminRoleManagementController).Assembly);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            await next();
        });
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        app.MapGet("/health", () => "ok");
        await app.RunAsync();
    }
}

internal sealed class RoleFixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers["X-Fixture-User"].ToString() == "member" ? "fixture-member" : "fixture-root";
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, id)], "fixture-role")), "fixture-role")));
    }
}
