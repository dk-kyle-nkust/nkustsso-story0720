// 撤銷指定帳號或裝置的登入，並記錄稽核。
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>
/// 管理者撤銷中央登入的共用服務；不直接操作其他網站的登入 Cookie。
/// </summary>
public static class LoginSessionAdministration
{
    public const string Role = "SsoAdministrator";
    public const string Policy = "ManageSessions";

    /// <summary>
    /// 在同一交易撤銷符合條件的登入並新增稽核紀錄；sessionId 為 null 時處理整個帳號。
    /// </summary>
    public static async Task<int> RevokeAsync(SsoDatabase db, string accountId, string? sessionId,
        string actorId, string reason, long now, long? maximumMilliseconds = null)
    {
        await using var transaction = await db.BeginTransactionAsync();
        var query = db.LoginSessions.Where(s => s.AccountId == accountId && s.RevokedAt == null && s.ExpiresAt > now);
        if (maximumMilliseconds is long maximum)
        {
            query = query.Where(s => s.CreatedAt > now - maximum);
        }

        if (sessionId is not null)
        {
            query = query.Where(s => s.Id == sessionId);
        }

        var count = await query.Set(s => s.RevokedAt, now).Set(s => s.RevokedBy, actorId)
            .Set(s => s.RevocationReason, reason).Set(s => s.ProtectedTicket, Array.Empty<byte>()).UpdateAsync();
        await db.InsertAsync(new SessionAudit
        {
            OccurredAt = now,
            ActorId = actorId,
            TargetAccountId = accountId,
            SessionId = sessionId,
            Action = sessionId is null ? "revoke_account_sessions" : "revoke_session",
            Reason = reason,
            AffectedCount = count
        });
        await transaction.CommitAsync();
        return count;
    }
}
