using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

// 僅以隔離資料測試真正的登入控制器，不連接校務資料庫。
internal static class CampusLoginUiHost
{
    public static async Task RunAsync(string root, string directory, string url, ICampusDatabaseFactory source)
    {
        if (new Uri(url).Host != "127.0.0.1") throw new InvalidOperationException("測試只可使用迴路位址。");
        var local = new DatabaseFactory(Path.Combine(directory, "local"));
        Directory.CreateDirectory(Path.Combine(directory, "local"));
        await local.InitializeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { ContentRootPath = Path.Combine(root, "NkustSso.Server"), EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.WebHost.UseUrls(url);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(local);
        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.Configure<CampusAuthenticationOptions>(o =>
        { o.Enabled = true; o.DevelopmentAllowAnyPassword = true; });
        builder.Services.AddSingleton<ICampusAccountSource, MssqlCampusAccountSource>();
        builder.Services.AddSingleton<CampusLoginGates>();
        builder.Services.AddScoped<CampusLoginService>();
        builder.Services.AddScoped<AdminAuthorizationService>();
        builder.Services.AddIdentity<Account, IdentityRole>()
            .AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>();
        builder.Services.AddAuthorization(o => o.AddPolicy(LoginSessionAdministration.Policy, p => p.RequireAuthenticatedUser()));
        builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("login", p =>
        { p.PermitLimit = 100; p.Window = TimeSpan.FromMinutes(1); }));
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(AccountLoginController).Assembly);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            await next();
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.RunAsync();
    }
}
