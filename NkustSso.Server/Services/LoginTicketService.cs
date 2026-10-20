// 核發及一次性兌換登入票券，檢查 PKCE 與登入有效性。
using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.ApiModels;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>
/// Web API 單一登入的一次性票券核發、一次性兌換及稽核；
/// 有效期最多 60 秒，且不得超過中央 Session 的剩餘期限。
/// </summary>
public sealed class LoginTicketService(
    DatabaseFactory database,
    LoginSessionStore sessions,
    LoginSessionLifetimeService lifetime,
    TimeProvider clock,
    IConfiguration configuration)
{
    /// <summary>票券最長有效 60 秒，實際還會再受中央 Session 的剩餘期限限制。</summary>
    private const long TicketLifetimeMilliseconds = 60_000;

    /// <summary>
    /// 對高熵隨機票券或系統密鑰計算 SHA-256；此函式不作為使用者密碼雜湊。
    /// </summary>
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>
    /// 依 PKCE S256 規則，將 verifier 雜湊後轉為 Base64Url。
    /// </summary>
    public static string Challenge(string verifier)
    {
        return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    }

    /// <summary>
    /// 只查詢已啟用且登記為 Web API 單一登入（ticket）的系統，排除純首頁連結。
    /// </summary>
    public async Task<TicketClient?> FindClientAsync(string id)
    {
        await using var db = database.Open();
        return await db.TicketClients.SingleOrDefaultAsync(c => c.Id == id
            && c.Enabled
            && db.RegisteredSystems.Any(s => s.ClientId == id && s.Enabled && s.Protocol == "ticket"));
    }

    /// <summary>
    /// 以固定時間方式比對密鑰雜湊；不存在的系統也進行比對，最後仍回傳驗證失敗。
    /// </summary>
    public async Task<TicketClient?> AuthenticateClientAsync(string id, string secret)
    {
        var client = await FindClientAsync(id);

        // 系統不存在時用假雜湊比對，讓回應時間不洩漏「這個代碼有沒有登記」。
        var expected = client?.SecretHash ?? new string('0', 64);
        var provided = Encoding.ASCII.GetBytes(Hash(secret));
        var valid = CryptographicOperations.FixedTimeEquals(provided, Encoding.ASCII.GetBytes(expected));

        return valid ? client : null;
    }

    /// <summary>
    /// 驗證中央 Session 後產生隨機票券，綁定接收系統、回呼網址與 PKCE，資料庫只保存票券雜湊。
    /// </summary>
    /// <param name="client">接收票券的登記系統，核發前會再次確認設定仍有效。</param>
    /// <param name="accountId">已登入使用者的內部帳號識別碼。</param>
    /// <param name="sessionId">中央登入紀錄的公開識別碼，不是瀏覽器 Cookie 內的秘密金鑰。</param>
    /// <param name="challenge">接收系統以 PKCE S256 計算的挑戰值。</param>
    /// <param name="ip">本次核發要求的來源 IP，供稽核紀錄使用。</param>
    /// <returns>可兌換一次的原始票券；系統設定或中央登入無效時回傳 null。</returns>
    public async Task<string?> IssueAsync(
        TicketClient client,
        string accountId,
        string sessionId,
        string challenge,
        string ip)
    {
        // 表單送出後設定可能已被改過，核發前再讀一次現況比對。
        var current = await FindClientAsync(client.Id);
        if (current is null)
        {
            return null;
        }

        if (current.RedirectUri != client.RedirectUri || current.SecretHash != client.SecretHash)
        {
            return null;
        }

        if (!await sessions.IsActiveAsync(sessionId, accountId))
        {
            return null;
        }

        await using var db = database.OpenSessions();
        var session = await db.LoginSessions.SingleAsync(s => s.Id == sessionId);
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var ticket = new LoginTicket
        {
            TicketHash = Hash(raw),
            AccountId = accountId,
            SessionId = sessionId,
            ClientId = client.Id,
            RedirectUri = client.RedirectUri,
            CodeChallenge = challenge,
            CreatedAt = now,
            ExpiresAt = Math.Min(now + TicketLifetimeMilliseconds, lifetime.EffectiveExpiry(session)),
            IssuedIp = ip
        };

        await using var transaction = await db.BeginTransactionAsync();
        await db.InsertAsync(ticket);
        await db.InsertAsync(Audit(client.Id, "issue", "success", ip, ticket.Id, accountId));
        await transaction.CommitAsync();

        return raw;
    }

    /// <summary>
    /// 重新確認票券、PKCE、系統及帳號，再以原子條件更新兌換狀態；成功後依白名單回傳身分資料。
    /// </summary>
    /// <param name="client">已通過系統密鑰驗證的接收系統。</param>
    /// <param name="raw">瀏覽器轉交的原始票券，不應寫入日誌。</param>
    /// <param name="redirectUri">必須與票券綁定及系統登記完全相同的回呼網址。</param>
    /// <param name="verifier">接收系統保存的 PKCE 原始驗證值，用來核對票券的挑戰值。</param>
    /// <param name="ip">本次後端兌換要求的來源 IP，通常是接收系統伺服器的位址。</param>
    /// <returns>允許提供的身分資料；驗證失敗、票券逾期或已兌換時回傳 null。</returns>
    public async Task<SsoIdentityResponse?> RedeemAsync(
        TicketClient client,
        string raw,
        string redirectUri,
        string verifier,
        string ip)
    {
        await using var db = database.OpenSessions();
        var hash = Hash(raw);
        var ticket = await db.LoginTickets.SingleOrDefaultAsync(t => t.TicketHash == hash);
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        if (ticket is null || !IsTicketUsable(ticket, client, redirectUri, verifier, now))
        {
            await RecordAsync(client.Id, "redeem", "invalid_ticket", ip);
            return null;
        }

        await using var accounts = database.Open();
        var registration = await accounts.RegisteredSystems
            .SingleOrDefaultAsync(s => s.ClientId == client.Id && s.Enabled && s.Protocol == "ticket");
        var current = await FindClientAsync(client.Id);

        if (registration is null || current is null)
        {
            return null;
        }

        if (current.RedirectUri != redirectUri || current.SecretHash != client.SecretHash)
        {
            return null;
        }

        var user = await accounts.Users.SingleOrDefaultAsync(u => u.Id == ticket.AccountId);
        if (user is null || !await IsUserActiveAsync(user, ticket))
        {
            await RecordAsync(client.Id, "redeem", "session_inactive", ip, ticket.Id, ticket.AccountId);
            return null;
        }

        var changed = await ConsumeTicketAsync(db, ticket, user, ip);
        if (changed != 1)
        {
            return null;
        }

        var session = await db.LoginSessions.SingleAsync(s => s.Id == ticket.SessionId);
        var identity = BuildIdentity(client, user, session);
        return IdentityClaimReleasePolicy.Filter(identity, registration.ClaimMask);
    }

    /// <summary>
    /// 票券本身的檢查：存在、屬於這個系統、回呼一致、尚未兌換、未過期，且 PKCE 相符。
    /// </summary>
    private static bool IsTicketUsable(
        LoginTicket ticket,
        TicketClient client,
        string redirectUri,
        string verifier,
        long now)
    {
        if (ticket.ClientId != client.Id)
        {
            return false;
        }

        if (ticket.RedirectUri != redirectUri || client.RedirectUri != redirectUri)
        {
            return false;
        }

        if (ticket.ConsumedAt != null || ticket.ExpiresAt <= now)
        {
            return false;
        }

        var stored = Encoding.ASCII.GetBytes(ticket.CodeChallenge);
        var provided = Encoding.ASCII.GetBytes(Challenge(verifier));
        return CryptographicOperations.FixedTimeEquals(stored, provided);
    }

    private async Task<bool> IsUserActiveAsync(Account user, LoginTicket ticket)
    {
        if (!user.Enabled)
        {
            return false;
        }

        if (user.LockoutEnd > clock.GetUtcNow())
        {
            return false;
        }

        return await sessions.IsActiveAsync(ticket.SessionId, user.Id);
    }

    /// <summary>
    /// 以單一條件 UPDATE 完成一次性兌換；Session 檢查放在同一個 SQL 敘述中，
    /// 確保已提交的撤銷能阻擋兌換，並讓並行要求最多只有一筆成功。
    /// </summary>
    private async Task<int> ConsumeTicketAsync(SsoDatabase db, LoginTicket ticket, Account user, string ip)
    {
        await using var transaction = await db.BeginTransactionAsync();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        var changed = await db.LoginTickets
            .Where(t => t.Id == ticket.Id
                && t.ConsumedAt == null
                && t.ExpiresAt > now
                && db.LoginSessions.Any(s => s.Id == t.SessionId
                    && s.AccountId == t.AccountId
                    && s.RevokedAt == null
                    && s.ExpiresAt > now
                    && s.CreatedAt + lifetime.MaximumMilliseconds > now
                    && s.SecurityStamp == user.SecurityStamp))
            .Set(t => t.ConsumedAt, now)
            .Set(t => t.RedeemedIp, ip)
            .UpdateAsync();

        var outcome = changed == 1 ? "success" : "invalid_ticket";
        await db.InsertAsync(Audit(ticket.ClientId, "redeem", outcome, ip, ticket.Id, user.Id));
        await transaction.CommitAsync();

        return changed;
    }

    /// <summary>
    /// 學籍欄位只在系統登記為需要時才帶出；最終還會再過一次欄位白名單。
    /// </summary>
    private SsoIdentityResponse BuildIdentity(TicketClient client, Account user, LoginSession session)
    {
        var profile = client.IncludeStudentProfile;

        return new SsoIdentityResponse(
            configuration["Sso:Issuer"]!,
            client.Id,
            user.Id,
            user.UserName ?? "",
            user.DisplayName,
            user.IdentityType,
            session.CreatedAt / 1000,
            lifetime.EffectiveExpiry(session) / 1000,
            profile ? user.ClassCode : null,
            profile ? user.ClassName : null,
            profile ? user.StudentStatusCode : null,
            profile ? user.StudentStatusName : null,
            profile ? user.EducationLevelCode : null,
            profile ? user.EducationLevelName : null);
    }

    /// <summary>
    /// 保存票券操作的結果與來源 IP；票券 ID 是稽核識別值，不是原始票券內容。
    /// </summary>
    public async Task RecordAsync(
        string clientId,
        string action,
        string outcome,
        string ip,
        string? ticketId = null,
        string? accountId = null)
    {
        await using var db = database.OpenSessions();
        await db.InsertAsync(Audit(clientId, action, outcome, ip, ticketId, accountId));
    }

    /// <summary>
    /// 組合稽核資料；呼叫者傳入的 IP 在核發時是瀏覽器來源，在兌換時是接收系統後端來源。
    /// </summary>
    private TicketAudit Audit(
        string clientId,
        string action,
        string outcome,
        string ip,
        string? ticketId = null,
        string? accountId = null)
    {
        return new TicketAudit
        {
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            ClientId = clientId,
            Action = action,
            Outcome = outcome,
            RemoteIp = ip,
            TicketId = ticketId,
            AccountId = accountId
        };
    }
}
