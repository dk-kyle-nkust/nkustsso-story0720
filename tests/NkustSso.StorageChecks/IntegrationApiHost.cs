using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using LinqToDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;
using Microsoft.AspNetCore.Identity;

// 只存在測試組件；網路測試不可能觸及真實 LDAP／MSSQL。
internal static class IntegrationApiHost
{
    public static async Task RunAsync(string directory, string url)
    {
        if (new Uri(url).Host != "127.0.0.1") throw new InvalidOperationException("Test host must bind loopback.");
        var database = new DatabaseFactory(directory);
        await database.InitializeAsync();
        await using (var db = database.Open())
        {
            await db.InsertAsync(new IntegrationClient { Id = "fixture-soap", Name = "SOAP", Protocol = "soap", Enabled = true, AllowedIps = "127.0.0.1",
                CallerName = "original-client", CallerPasswordHash = new PasswordHasher<IntegrationClient>().HashPassword(new(), "fixture-original-caller-password"), AllowedFunctionIds = "F1" });
            await db.InsertAsync(new IntegrationClient { Id = "fixture-json", Name = "JSON", Protocol = "json", Enabled = true, AllowedIps = "127.0.0.1",
                SecretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture-json-secret-with-at-least-32-characters"))) });
            foreach (var id in new[] { "fixture-soap", "fixture-json" })
                await db.InsertAsync(new IntegrationClientService { ClientId = id, ServiceId = IntegrationPermissions.VerifyAccount });
            await db.InsertAsync(new IntegrationClientReturnField { ClientId = "fixture-soap", FieldKey = IntegrationReturnFields.Certify2Status });
            foreach (var method in LegacySoapOperationCatalog.Available.Where(m => m != LegacySoapContract.Operation))
            {
                await db.InsertAsync(new IntegrationClientService { ClientId = "fixture-soap", ServiceId = LegacySoapOperationCatalog.Permission(method) });
                foreach (var field in IntegrationReturnFields.Contracts.Single(c => c.Operation == method).Fields.Where(f => !f.Required && !f.Blocked))
                    await db.InsertAsync(new IntegrationClientReturnField { ClientId = "fixture-soap", FieldKey = method + "/" + field.Name });
            }
        }
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration["Sso:Issuer"] = url + "/";
        builder.Environment.WebRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.GetFullPath("NkustSso.Server/wwwroot"));
        builder.WebHost.UseUrls(url);
        builder.Services.AddSingleton(database);
        builder.Services.AddSingleton<ICampusSoapQueries>(await LegacySoapMigrationChecks.SeedAsync(Path.Combine(directory, "campus-fixture"), database, TimeProvider.System));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<CampusLoginGates>();
        builder.Services.AddScoped<IntegrationClientRegistrationService>();
        builder.Services.AddScoped<CredentialVerificationService>();
        builder.Services.AddSingleton<IIntegrationAccountSource, IntegrationVerificationChecks.FakeSource>();
        builder.Services.Configure<IntegrationServiceOptions>(o => { o.SoapEnabled = true; o.JsonEnabled = true; });
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(CredentialVerificationApiController).Assembly);
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("service-verification", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
            await next();
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapGet("/health", () => "ok");
        await app.RunAsync();
    }
}
