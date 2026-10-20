// 計算登入期限、滑動續期與最長登入時間。
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>
/// 中央登入期限設定，單位為分鐘；預設滑動窗口 60 分鐘、最長連續登入 240 分鐘。
/// </summary>
public sealed class SessionLifetimeOptions
{
    public int WindowMinutes { get; set; } = 60;

    public int MaximumMinutes { get; set; } = 240;
}

/// <summary>
/// 集中計算中央登入的滑動窗口與絕對期限，讓 Cookie 和資料庫使用相同規則。
/// </summary>
public sealed class LoginSessionLifetimeService(IOptions<SessionLifetimeOptions> options, TimeProvider clock)
{
    /// <summary>可續期的頁面；其餘路徑不延長登入。</summary>
    private static readonly string[] RenewablePaths =
        ["/", "/integration", "/connect/authorize", "/ticket/authorize", "/saml/continue"];

    /// <summary>可續期的路徑前綴。</summary>
    private static readonly string[] RenewableSegments =
        ["/portal", "/admin/sessions", "/admin/tickets", "/admin/systems"];

    public TimeSpan Window { get; } = TimeSpan.FromMinutes(options.Value.WindowMinutes);

    public TimeSpan Maximum { get; } = TimeSpan.FromMinutes(options.Value.MaximumMinutes);

    public long MaximumMilliseconds => (long)Maximum.TotalMilliseconds;

    /// <summary>
    /// 從最初 CreatedAt 計算最晚到期時間；續期不重設起算點，單位為 Unix 毫秒。
    /// </summary>
    public long AbsoluteExpiry(LoginSession session)
    {
        return session.CreatedAt + MaximumMilliseconds;
    }

    /// <summary>
    /// 取可續期期限與絕對上限的較早者，作為實際到期時間。
    /// </summary>
    public long EffectiveExpiry(LoginSession session)
    {
        return Math.Min(session.ExpiresAt, AbsoluteExpiry(session));
    }

    // 僅正常 SSO 瀏覽及登入導向可續期；健康檢查、後端兌換、
    // 失敗回應及可辨識的背景請求不延長登入。
    /// <summary>
    /// 判斷是否為可觸發續期的正常頁面導向；不讓健康檢查、兌換 API 或背景 fetch 延長登入。
    /// </summary>
    public static bool IsRenewalRequest(HttpContext? context)
    {
        if (context is null || !HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        if (context.Response.StatusCode >= 400)
        {
            return false;
        }

        // 有帶 Sec-Fetch-Dest 時只接受整頁導向，排除 fetch 與子資源請求。
        var destination = context.Request.Headers["Sec-Fetch-Dest"].ToString();
        if (destination.Length != 0 && destination != "document")
        {
            return false;
        }

        var path = context.Request.Path;
        if (RenewablePaths.Any(p => path == p))
        {
            return true;
        }

        return RenewableSegments.Any(s => path.StartsWithSegments(s));
    }

    /// <summary>
    /// 尚未到期且剩餘時間不超過窗口一半時，才到達續期門檻。
    /// </summary>
    public bool IsRenewalDue(long expiry, long now)
    {
        return expiry > now && expiry - now <= Window.TotalMilliseconds / 2;
    }

    /// <summary>
    /// 將相同時間來源與續期規則套用到 Cookie Authentication；資料庫端仍會再次檢查。
    /// </summary>
    public void ConfigureCookie(CookieAuthenticationOptions cookie)
    {
        cookie.TimeProvider = clock;
        cookie.ExpireTimeSpan = Window;
        cookie.SlidingExpiration = true;

        // 依剩餘期限決定續期，避免 Identity 定期更新聲明時順帶延長登入。
        cookie.Events.OnCheckSlidingExpiration = context =>
        {
            context.ShouldRenew = ShouldRenew(context.Properties.ExpiresUtc, context.HttpContext);
            return Task.CompletedTask;
        };
    }

    private bool ShouldRenew(DateTimeOffset? expiresUtc, HttpContext httpContext)
    {
        if (expiresUtc is not { } expiry)
        {
            return false;
        }

        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        if (!IsRenewalDue(expiry.ToUnixTimeMilliseconds(), now))
        {
            return false;
        }

        return IsRenewalRequest(httpContext);
    }
}
