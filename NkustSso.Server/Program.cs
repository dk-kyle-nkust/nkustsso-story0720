using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// This runnable prototype deliberately refuses production startup until deployment is configured.
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("此原型僅開放 Development。正式部署前請依 README 完成資料庫、憑證與帳號來源設定。");
var localDirectory = Path.GetFullPath(builder.Configuration["Sso:DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "../.local"));
Directory.CreateDirectory(localDirectory);
var initializeStorage = builder.Configuration.GetValue<bool>("initialize-storage");
var importSqlite = builder.Configuration.GetValue<bool>("import-sqlite");
if (initializeStorage && importSqlite) throw new InvalidOperationException("請分別執行資料結構初始化與 SQLite 匯入。");
if (initializeStorage)
{
    var storage = SsoStorage.FromConfiguration(builder.Configuration, localDirectory);
    await storage.InitializeAsync(seedDefaults: false);
    Console.WriteLine("SSO 自有資料結構已建立；未建立帳號、測試資料或連線校務來源。");
    return;
}
if (importSqlite)
{
    var source = builder.Configuration["Sso:ImportDirectory"] ?? throw new InvalidOperationException("請指定 Sso:ImportDirectory 停站備份目錄。");
    var counts = await SqliteStorageImport.ImportAsync(SsoStorage.FromConfiguration(builder.Configuration, localDirectory), source);
    foreach (var table in counts) Console.WriteLine($"{table.Key}: {table.Value}");
    Console.WriteLine("SQLite 資料已匯入 SQL Server；來源原檔保留，未啟動網站或校務連線。");
    return;
}
// 僅用於保護 DataProtection 金鑰；沿用原憑證才能解開 .local/keys-server 既有金鑰。
var keyProtectionCertificate = KeyProtectionCertificate.Load(localDirectory, create: true);
builder.Services.AddDataProtection().SetApplicationName("NkustSso.Server")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(localDirectory, "keys-server")))
    .ProtectKeysWithCertificate(keyProtectionCertificate);
// HTTPS 改用 dotnet dev-certs 的開發憑證：自簽憑證未進信任存放區，串接端的 OIDC
// discovery 會以 UntrustedRoot 失敗。Development 下 Kestrel 會自動採用該憑證。
builder.Services.AddSingleton(SsoStorage.FromConfiguration(builder.Configuration, localDirectory));
builder.Services.AddSingleton(_ => new SamlIdentityProvider(builder.Configuration["Sso:Issuer"]!, localDirectory));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<CampusAuthenticationOptions>().Bind(builder.Configuration.GetSection("CampusAuthentication"))
    .Validate(options => !options.DevelopmentAllowAnyPassword || builder.Environment.IsDevelopment(),
        "任意密碼測試登入僅限 Development 環境。")
    .Validate(options => options.IsValid(), "校務登入身分對應須使用 student、employee、teacher、staff 或 other，逾時須為 1 至 30 秒。")
    .Validate(options => !options.Enabled || CampusDatabaseFactory.HasValidConnection(builder.Configuration),
        "啟用校務登入前須填妥 ConnectionStrings:LoginAuth（伺服器與資料庫）。").ValidateOnStart();
builder.Services.AddSingleton<ICampusDatabaseFactory, CampusDatabaseFactory>();
builder.Services.AddSingleton<ICampusAccountSource, MssqlCampusAccountSource>();
builder.Services.AddScoped<CampusIdentityCatalog>();
builder.Services.AddSingleton<CampusLoginGates>();
builder.Services.AddSingleton<CampusSessionValidator>();
builder.Services.AddScoped<CampusLoginService>();
builder.Services.AddOptions<IntegrationServiceOptions>().Bind(builder.Configuration.GetSection("IntegrationServices"))
    .Validate(o => o.TimeoutSeconds is >= 1 and <= 30, "串接服務逾時須為 1–30 秒。")
    .Validate(o => !(o.SoapEnabled || o.JsonEnabled) ||
        builder.Configuration.GetValue<bool>("CampusAuthentication:Enabled") && CampusDatabaseFactory.HasValidConnection(builder.Configuration),
        "串接服務統一使用新校務 MSSQL；啟用前須完成 LoginAuth 並啟用 CampusAuthentication。")
    .ValidateOnStart();
builder.Services.AddScoped<IntegrationClientRegistrationService>();
builder.Services.AddScoped<CredentialVerificationService>();
builder.Services.AddScoped<IIntegrationAccountSource, CampusSoapAccountSource>();
builder.Services.AddScoped<ICampusSoapQueries, CampusSoapQueries>();
builder.Services.AddOptions<SessionLifetimeOptions>().Bind(builder.Configuration.GetSection("Sso:Session"))
    .Validate(options => options.WindowMinutes >= 1 && options.WindowMinutes <= options.MaximumMinutes && options.MaximumMinutes <= 1440,
        "SSO Session requires 1 <= WindowMinutes <= MaximumMinutes <= 1440.").ValidateOnStart();
builder.Services.AddSingleton<LoginSessionLifetimeService>();
builder.Services.AddSingleton<LoginSessionStore>();
builder.Services.AddSingleton<LoginTicketService>();
builder.Services.AddScoped<SsoSystemRegistrationService>();
builder.Services.AddScoped<PortalCategoryService>();
builder.Services.AddScoped<PortalDirectoryService>();
builder.Services.AddScoped<PortalCategoryManagementService>();
builder.Services.AddScoped<IdentityCatalog>();
builder.Services.AddScoped<AdminAuthorizationService>();
builder.Services.AddScoped<AdminManagementService>();
builder.Services.AddSingleton<IntegrationManualService>();
builder.Services.AddScoped<CampusRoleMemberService>();
builder.Services.AddScoped<AccountIdentityService>();
builder.Services.AddHostedService<ExpiredTicketCleanupService>();
builder.Services.AddIdentity<Account, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddUserStore<LinqUserStore>().AddRoleStore<LinqRoleStore>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "NkustSso.Session.v1";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/denied";
});
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<LoginSessionStore, LoginSessionLifetimeService>((options, store, lifetime) =>
    {
        options.SessionStore = store;
        lifetime.ConfigureCookie(options);
    });
builder.Services.AddAuthorization(options => options.AddPolicy(LoginSessionAdministration.Policy, policy =>
    policy.RequireAuthenticatedUser().RequireAssertion(async context =>
    {
        if (context.Resource is not HttpContext http) return false;
        return await http.RequestServices.GetRequiredService<AdminAuthorizationService>().AuthorizeAsync(context.User, http);
    })));
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.SetDefaultApplicationEntity<OidcApplication>().SetDefaultAuthorizationEntity<OidcAuthorization>()
            .SetDefaultScopeEntity<OidcScope>().SetDefaultTokenEntity<OidcToken>();
        options.ReplaceApplicationStore<OidcApplication, LinqApplicationStore>()
            .ReplaceAuthorizationStore<OidcAuthorization, LinqAuthorizationStore>()
            .ReplaceScopeStore<OidcScope, LinqScopeStore>().ReplaceTokenStore<OidcToken, LinqTokenStore>();
        // Token state must be re-read to enforce redemption/revocation across concurrent requests.
        options.DisableEntityCaching();
    })
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["Sso:Issuer"]!));
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetEndSessionEndpointUris("connect/logout");
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.RegisterScopes(OpenIddictConstants.Scopes.Profile, OidcIdentityClaims.Scope);
        options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1));
        options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(5));
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
        // Keys only live for this development process: restart intentionally requires a fresh login.
        options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough().EnableEndSessionEndpointPassthrough();
    });
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("service-verification", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
await SsoStartupInitializer.InitializeAsync(app.Services, builder.Configuration);
app.Run();
