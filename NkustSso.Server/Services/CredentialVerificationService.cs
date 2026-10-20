// 驗證來源帳密並記錄結果，不建立網站登入狀態。
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed record ServiceVerification(bool Verified, string Status = "500", bool Unavailable = false);

public interface IIntegrationAccountSource
{
    Task<ServiceVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken);
}

/// <summary>只驗證來源帳密；不建立帳號、Cookie、角色或校務系統權限。</summary>
public sealed class CredentialVerificationService(
    DatabaseFactory database,
    IIntegrationAccountSource source,
    CampusLoginGates gates,
    TimeProvider clock,
    ILogger<CredentialVerificationService> logger)
{
    /// <summary>連續失敗幾次後鎖定。</summary>
    private const int MaxFailedAttempts = 5;

    /// <summary>鎖定時間，也是失敗次數重新計數的間隔（毫秒）。</summary>
    private const long LockoutMilliseconds = 300_000;

    /// <summary>同一帳號同時驗證時的等待上限。</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    public async Task<ServiceVerification> VerifyAsync(
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 100)
        {
            return new ServiceVerification(false);
        }

        if (string.IsNullOrEmpty(password) || password.Length > 256)
        {
            return new ServiceVerification(false);
        }

        var input = userName.Trim();
        var normalized = input.ToUpperInvariant();

        // 與網站校務登入共用嘗試限制，不能透過切換 SOAP／JSON 繞過鎖定。
        var gate = gates.For(normalized);
        if (!await gate.WaitAsync(GateTimeout, cancellationToken))
        {
            return new ServiceVerification(false, Unavailable: true);
        }

        try
        {
            return await VerifyInsideGateAsync(input, password, normalized, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ServiceVerification> VerifyInsideGateAsync(
        string input,
        string password,
        string normalized,
        CancellationToken cancellationToken)
    {
        await using var db = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        var attempt = await db.GetTable<CampusLoginAttempt>()
            .SingleOrDefaultAsync(a => a.NormalizedName == normalized, cancellationToken);
        if (attempt?.LockedUntil > now)
        {
            return new ServiceVerification(false);
        }

        ServiceVerification result;
        try
        {
            result = await source.VerifyAsync(input, password, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 來源故障不算密碼錯誤，也不累計失敗次數。
            logger.LogWarning("串接帳密來源暫時無法使用。類型：{ErrorType}", exception.GetType().Name);
            return new ServiceVerification(false, Unavailable: true);
        }

        if (result.Unavailable)
        {
            return result;
        }

        if (result.Verified)
        {
            // 來源回傳的身分代碼也有邊界，不合理的值不視為驗證成功。
            if (string.IsNullOrWhiteSpace(result.Status) || result.Status.Length > 100)
            {
                return new ServiceVerification(false);
            }

            await db.GetTable<CampusLoginAttempt>()
                .Where(a => a.NormalizedName == normalized)
                .DeleteAsync(cancellationToken);
            return result;
        }

        await RecordFailureAsync(db, attempt, normalized, now, cancellationToken);
        return new ServiceVerification(false);
    }

    /// <summary>
    /// 五分鐘未再失敗或已過鎖定期後重新計數；錯誤連線不計為密碼失敗。
    /// </summary>
    private static async Task RecordFailureAsync(
        SsoDatabase db,
        CampusLoginAttempt? attempt,
        string normalized,
        long now,
        CancellationToken cancellationToken)
    {
        attempt ??= new CampusLoginAttempt { NormalizedName = normalized };

        if (attempt.UpdatedAt + LockoutMilliseconds <= now || attempt.LockedUntil != 0)
        {
            attempt.FailedCount = 0;
        }

        attempt.FailedCount++;
        attempt.LockedUntil = attempt.FailedCount >= MaxFailedAttempts ? now + LockoutMilliseconds : 0;
        attempt.UpdatedAt = now;

        await db.InsertOrReplaceAsync(attempt, token: cancellationToken);
    }
}
