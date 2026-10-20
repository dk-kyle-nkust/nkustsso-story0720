using System.Net;
using System.Security.Claims;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

// An isolated HTTP server exercises the real CookieAuthenticationHandler and
// ITicketStore using a controlled clock. No test clock/endpoints enter the app.
internal static class SlidingCookieChecks
{
    public static async Task RunAsync(DatabaseFactory factory, string accountId)
    {
        const string scheme = "test-session";
        var clock = new TestClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(factory);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddOptions<SessionLifetimeOptions>();
        builder.Services.AddSingleton<LoginSessionLifetimeService>();
        builder.Services.AddSingleton<LoginSessionStore>();
        builder.Services.AddAuthentication(scheme).AddCookie(scheme, options =>
        {
            options.Cookie.Name = "isolated-session";
            options.Events.OnValidatePrincipal = context =>
            {
                // Identity can request a claims refresh independently of sliding expiration.
                if (context.Request.Path == "/health") context.ShouldRenew = true;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddOptions<CookieAuthenticationOptions>(scheme)
            .Configure<LoginSessionStore, LoginSessionLifetimeService>((options, store, lifetime) =>
            {
                options.SessionStore = store;
                lifetime.ConfigureCookie(options);
            });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/test/login", async (HttpContext context) =>
        {
            await context.SignInAsync(scheme, new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId), new Claim(ClaimTypes.Name, "001")
            }, scheme)));
            return Results.Ok();
        });
        app.MapGet("/", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/health", () => Results.Ok());
        app.MapGet("/connect/authorize", () => Results.Redirect("/client-return")).RequireAuthorization();
        app.MapGet("/integration", async (HttpContext context) =>
        {
            var auth = await context.AuthenticateAsync(scheme);
            var id = auth.Properties!.Items[LoginSessionStore.SessionProperty];
            await using var db = factory.OpenSessions();
            await LoginSessionAdministration.RevokeAsync(db, accountId, id, accountId, "in-flight test", clock.GetUtcNow().ToUnixTimeMilliseconds());
            return Results.Ok();
        }).RequireAuthorization();
        await app.StartAsync();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
            await using var db = factory.OpenSessions();
            async Task<LoginSession> Login()
            {
                var before = await db.LoginSessions.Select(s => s.Id).ToListAsync();
                using var response = await client.PostAsync("/test/login", null);
                Require(response.StatusCode == HttpStatusCode.OK, "Test login succeeded.");
                return await db.LoginSessions.SingleAsync(s => !before.Contains(s.Id));
            }
            async Task<LoginSession> Read(string id) => await db.LoginSessions.SingleAsync(s => s.Id == id);
            async Task<HttpResponseMessage> Get(string path, string destination = "document")
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add("Sec-Fetch-Dest", destination);
                return await client.SendAsync(request);
            }
            var session = await Login();
            var id = session.Id;
            var initialExpiry = session.ExpiresAt;
            clock.Advance(TimeSpan.FromMinutes(10));
            using (var response = await Get("/")) Require(!response.Headers.Contains("Set-Cookie"), "No early cookie refresh.");
            Require((await Read(id)).ExpiresAt == initialExpiry, "No early database refresh.");
            clock.Advance(TimeSpan.FromMinutes(21));
            using (await Get("/", "empty")) { }
            using (await Get("/health")) { }
            Require((await Read(id)).ExpiresAt == initialExpiry, "Background fetch or health/claims refresh must not extend session.");
            using (var response = await Get("/connect/authorize"))
            {
                Require(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString == "/client-return", "OIDC navigation returns to client.");
                Require(response.Headers.Contains("Set-Cookie"), "Eligible renewal reissues browser cookie.");
            }
            Require((await Read(id)).ExpiresAt == clock.GetUtcNow().AddHours(1).ToUnixTimeMilliseconds(), "Cookie and database renewal agree.");
            clock.Advance(TimeSpan.FromMinutes(31)); // Past the original one-hour deadline.
            using (var response = await Get("/connect/authorize")) Require(response.Headers.Location?.OriginalString == "/client-return", "Renewed cookie survives the original deadline.");
            for (var i = 0; i < 5; i++)
            {
                clock.Advance(TimeSpan.FromMinutes(31));
                using var response = await Get("/connect/authorize");
                Require(response.Headers.Location?.OriginalString == "/client-return", "Still valid before four-hour maximum.");
            }
            var capped = await Read(id);
            var absolute = session.CreatedAt + (long)TimeSpan.FromHours(4).TotalMilliseconds;
            Require(capped.CreatedAt == session.CreatedAt && capped.ExpiresAt == absolute, "Renewals never move original login or four-hour maximum.");
            clock.Advance(TimeSpan.FromMilliseconds(absolute - clock.GetUtcNow().ToUnixTimeMilliseconds()));
            using (var response = await Get("/")) Require(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.OriginalString.Contains("/Account/Login"), "Exact maximum requires login.");
            var store = app.Services.GetRequiredService<LoginSessionStore>();
            Require(!await store.IsActiveAsync(id, accountId), "Authorization-code exchange also rejects maximum age.");
            Console.WriteLine("PASS real cookie renewal, OIDC redirect, no background extension and four-hour cap");

            session = await Login();
            clock.Advance(TimeSpan.FromMinutes(31));
            using (await Get("/integration")) { }
            var revoked = await Read(session.Id);
            Require(revoked.RevokedAt != null && revoked.ProtectedTicket.Length == 0 && revoked.ExpiresAt == session.ExpiresAt,
                "Revoke during request wins over scheduled cookie refresh.");
            using (var response = await Get("/")) Require(response.StatusCode == HttpStatusCode.Redirect, "Reissued stale cookie cannot restore revoked session.");
            session = await Login();
            clock.Advance(TimeSpan.FromHours(1));
            using (var response = await Get("/")) Require(response.StatusCode == HttpStatusCode.Redirect, "Idle window expiry requires login.");
            Require((await Read(session.Id)).ExpiresAt == session.ExpiresAt, "Expired session cannot extend.");
            session = await Login();
            Require(await store.IsActiveAsync(session.Id, accountId), "Explicit login after expiry creates a new session.");
            Console.WriteLine("PASS in-flight revocation, idle expiry and fresh login");
        }
        finally { await app.StopAsync(); }
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
