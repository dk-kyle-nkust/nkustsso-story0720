// 保存、讀取、續期與撤銷使用者登入狀態。
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>
/// 以資料庫保存中央驗證票證的 ITicketStore；瀏覽器 Cookie 由驗證處理器保護並攜帶參照，
/// 資料庫保存其雜湊及加密票證。
/// </summary>
public sealed class LoginSessionStore(
    DatabaseFactory database,
    IDataProtectionProvider protection,
    IHttpContextAccessor contexts,
    TimeProvider clock,
    LoginSessionLifetimeService lifetime,
    CampusSessionValidator? campus = null) : ITicketStore
{
    // 票證屬性中的公開紀錄 ID 用於稽核／關聯，不能當作瀏覽器的登入參照使用。
    public const string SessionProperty = "nkust.session.id";
    public const string SessionClaim = "urn:nkust:sso:session-id";

    /// <summary>最近活動時間的寫入間隔，避免每個頁面請求都更新資料庫。</summary>
    private const int LastSeenIntervalSeconds = 60;

    /// <summary>瀏覽器資訊的保存長度上限。</summary>
    private const int MaxUserAgentLength = 300;

    private readonly IDataProtector protector =
        protection.CreateProtector("NkustSso.Server.SessionTickets.v1");

    /// <summary>
    /// 建立中央 Session，綁定帳號與 SecurityStamp，記錄 IP／瀏覽器資訊並保存加密票證。
    /// </summary>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        await using var accountDb = database.Open();
        await using var db = database.OpenSessions();

        var accountId = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing account identifier.");

        var user = await accountDb.Users.SingleAsync(u => u.Id == accountId);
        var now = clock.GetUtcNow();

        if (!user.Enabled || user.LockoutEnd > now || !await SourceAllowedAsync(user))
        {
            throw new InvalidOperationException("Cannot create a session for a disabled or locked account.");
        }

        // 只將隨機參照交回 Cookie 驗證處理器；資料庫保存其雜湊，不保存原始參照。
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var context = contexts.HttpContext;

        // 票證帶來的期限可能比設定的視窗更長，取較早的那個。
        var requestedExpiry = (ticket.Properties.ExpiresUtc ?? now.Add(lifetime.Window)).ToUnixTimeMilliseconds();
        var windowExpiry = now.Add(lifetime.Window).ToUnixTimeMilliseconds();

        var session = new LoginSession
        {
            KeyHash = Hash(key),
            AccountId = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            SecurityStamp = user.SecurityStamp ?? "",
            CreatedAt = now.ToUnixTimeMilliseconds(),
            LastSeenAt = now.ToUnixTimeMilliseconds(),
            ExpiresAt = Math.Min(requestedExpiry, windowExpiry),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent = Truncate(context?.Request.Headers.UserAgent.ToString() ?? "", MaxUserAgentLength)
        };

        session.ExpiresAt = lifetime.EffectiveExpiry(session);
        ticket.Properties.IssuedUtc = now;
        ticket.Properties.ExpiresUtc = DateTimeOffset.FromUnixTimeMilliseconds(session.ExpiresAt);
        ticket.Properties.Items[SessionProperty] = session.Id;
        session.ProtectedTicket = protector.Protect(TicketSerializer.Default.Serialize(ticket));

        await db.InsertAsync(session);
        return key;
    }

    /// <summary>
    /// 依 Cookie 處理器提供的隨機參照查詢 Session；檢查撤銷、期限與帳號狀態後才還原票證。
    /// </summary>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var db = database.OpenSessions();
        var now = clock.GetUtcNow();
        var hash = Hash(key);

        var session = await db.LoginSessions.SingleOrDefaultAsync(s => s.KeyHash == hash);
        if (session is null || session.RevokedAt is not null)
        {
            return null;
        }

        if (lifetime.EffectiveExpiry(session) <= now.ToUnixTimeMilliseconds())
        {
            return null;
        }

        await using var accountDb = database.Open();
        var user = await accountDb.Users.SingleOrDefaultAsync(u => u.Id == session.AccountId);
        if (!await IsAccountStillValidAsync(user, session, now))
        {
            await RevokeAsync(db, session.Id, session.AccountId, "account_changed", now.ToUnixTimeMilliseconds());
            return null;
        }

        var ticket = Unprotect(session.ProtectedTicket);
        if (ticket is null)
        {
            return null;
        }

        if (ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier) != session.AccountId)
        {
            return null;
        }

        ticket.Properties.Items[SessionProperty] = session.Id;

        // 同時採用資料庫期限及最初登入的絕對上限，不以 Cookie 內的舊期限為準。
        ticket.Properties.ExpiresUtc = DateTimeOffset.FromUnixTimeMilliseconds(lifetime.EffectiveExpiry(session));

        // 最近活動時間最多每分鐘寫入一次，降低一般頁面請求的資料庫更新量。
        var threshold = now.AddSeconds(-LastSeenIntervalSeconds).ToUnixTimeMilliseconds();
        if (session.LastSeenAt <= threshold)
        {
            await db.LoginSessions
                .Where(s => s.Id == session.Id && s.RevokedAt == null)
                .Set(s => s.LastSeenAt, now.ToUnixTimeMilliseconds())
                .UpdateAsync();
        }

        return ticket;
    }

    /// <summary>
    /// 更新中央票證；符合滑動條件才延長期限，且不能超過最初登入起算的絕對上限。
    /// </summary>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var db = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var hash = Hash(key);

        var session = await db.LoginSessions.SingleOrDefaultAsync(s => s.KeyHash == hash);
        if (session is null || session.RevokedAt is not null || lifetime.EffectiveExpiry(session) <= now)
        {
            return;
        }

        if (ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier) != session.AccountId)
        {
            throw new InvalidOperationException("Sign out before switching accounts.");
        }

        await using var accountDb = database.Open();
        var user = await accountDb.Users.SingleOrDefaultAsync(u => u.Id == session.AccountId);
        if (!await IsAccountStillValidAsync(user, session, DateTimeOffset.FromUnixTimeMilliseconds(now)))
        {
            await RevokeAsync(db, session.Id, session.AccountId, "account_changed", now);
            return;
        }

        var stored = Unprotect(session.ProtectedTicket);
        if (stored is null)
        {
            return;
        }

        var expiry = ExtendExpiry(session, ticket, now);

        ticket.Properties.Items[SessionProperty] = session.Id;
        ticket.Properties.IssuedUtc = expiry > session.ExpiresAt
            ? DateTimeOffset.FromUnixTimeMilliseconds(now)
            : stored.Properties.IssuedUtc;
        ticket.Properties.ExpiresUtc = DateTimeOffset.FromUnixTimeMilliseconds(expiry);

        var encrypted = protector.Protect(TicketSerializer.Default.Serialize(ticket));

        // 同時比對原票證與期限：較晚抵達的舊要求不得覆蓋新續期，
        // 也不能恢復已撤銷／過期的登入；CreatedAt 始終保留最初登入時間。
        await db.LoginSessions
            .Where(s => s.Id == session.Id
                && s.RevokedAt == null
                && s.ExpiresAt > now
                && s.CreatedAt + lifetime.MaximumMilliseconds > now
                && s.ExpiresAt == session.ExpiresAt
                && s.ProtectedTicket == session.ProtectedTicket)
            .Set(s => s.ProtectedTicket, encrypted)
            .Set(s => s.ExpiresAt, expiry)
            .UpdateAsync();
    }

    /// <summary>
    /// 只有符合滑動續期條件的請求才延長期限，而且不超過絕對上限。
    /// </summary>
    private long ExtendExpiry(LoginSession session, AuthenticationTicket ticket, long now)
    {
        var expiry = lifetime.EffectiveExpiry(session);

        if (ticket.Properties.AllowRefresh == false)
        {
            return expiry;
        }

        if (!LoginSessionLifetimeService.IsRenewalRequest(contexts.HttpContext))
        {
            return expiry;
        }

        if (!lifetime.IsRenewalDue(expiry, now))
        {
            return expiry;
        }

        var extended = Math.Min(now + (long)lifetime.Window.TotalMilliseconds, lifetime.AbsoluteExpiry(session));
        return Math.Max(expiry, extended);
    }

    /// <summary>
    /// 由 Cookie 登出流程呼叫，將對應中央登入標示撤銷並清空加密票證。
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        await using var db = database.OpenSessions();
        var hash = Hash(key);

        var session = await db.LoginSessions.SingleOrDefaultAsync(s => s.KeyHash == hash);
        if (session is not null)
        {
            var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
            await RevokeAsync(db, session.Id, session.AccountId, "user_logout", now);
        }
    }

    /// <summary>
    /// 檢查公開 Session 紀錄 ID 是否屬於指定帳號且仍有效；此 ID 不等同可用於登入的 Cookie 參照。
    /// </summary>
    public async Task<bool> IsActiveAsync(string? id, string accountId)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        await using var db = database.OpenSessions();
        await using var accountDb = database.Open();
        var now = clock.GetUtcNow();

        var session = await db.LoginSessions
            .SingleOrDefaultAsync(s => s.Id == id && s.AccountId == accountId);
        if (session is null || session.RevokedAt != null)
        {
            return false;
        }

        if (lifetime.EffectiveExpiry(session) <= now.ToUnixTimeMilliseconds())
        {
            return false;
        }

        var user = await accountDb.Users.SingleOrDefaultAsync(u => u.Id == accountId);
        return await IsAccountStillValidAsync(user, session, now);
    }

    /// <summary>
    /// 帳號被停用、鎖定、換過 SecurityStamp 或來源不再允許時，既有登入都不該繼續生效。
    /// </summary>
    private async Task<bool> IsAccountStillValidAsync(Account? user, LoginSession session, DateTimeOffset now)
    {
        if (user is null || !user.Enabled)
        {
            return false;
        }

        if (user.LockoutEnd > now)
        {
            return false;
        }

        if (user.SecurityStamp != session.SecurityStamp)
        {
            return false;
        }

        return await SourceAllowedAsync(user);
    }

    /// <summary>
    /// 票證解密失敗代表金鑰已輪替或資料損壞，當成沒有登入而不是伺服器錯誤。
    /// </summary>
    private AuthenticationTicket? Unprotect(byte[] protectedTicket)
    {
        try
        {
            return TicketSerializer.Default.Deserialize(protector.Unprotect(protectedTicket));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // 未註冊校務驗證器的隔離測試只允許本地帳號，外部帳號不可繞過來源檢查。
    private Task<bool> SourceAllowedAsync(Account account)
    {
        if (campus is not null)
        {
            return campus.IsAllowedAsync(account);
        }

        return Task.FromResult(account.AuthenticationSource.Length == 0);
    }

    /// <summary>
    /// 記錄撤銷時間、操作者與原因，同時移除可還原的驗證票證。
    /// </summary>
    private static Task<int> RevokeAsync(SsoDatabase db, string id, string actor, string reason, long now)
    {
        return db.LoginSessions
            .Where(s => s.Id == id && s.RevokedAt == null)
            .Set(s => s.RevokedAt, now)
            .Set(s => s.RevokedBy, actor)
            .Set(s => s.RevocationReason, reason)
            .Set(s => s.ProtectedTicket, Array.Empty<byte>())
            .UpdateAsync();
    }

    private static string Hash(string key)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    private static string Truncate(string text, int length)
    {
        return text.Length <= length ? text : text[..length];
    }
}
