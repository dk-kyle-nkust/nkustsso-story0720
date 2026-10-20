// 檢查校務帳號是否仍可沿用目前登入狀態。
using LinqToDB;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services.Sources;

namespace NkustSso.Server.Services;

public sealed class CampusSessionValidator(
    IOptions<CampusAuthenticationOptions> options,
    ICampusAccountSource source,
    IHttpContextAccessor contexts,
    DatabaseFactory database,
    TimeProvider clock,
    ILogger<CampusSessionValidator> logger)
{
    public async Task<bool> IsAllowedAsync(Account account)
    {
        // 本地帳號只在未啟用校務驗證時可用。
        if (account.AuthenticationSource.Length == 0)
        {
            return !options.Value.Enabled;
        }

        if (!options.Value.Enabled)
        {
            return false;
        }

        if (account.AuthenticationSource != CampusLoginService.SourceName)
        {
            return false;
        }

        if (account.SourceAccountId.Length == 0)
        {
            return false;
        }

        var ct = contexts.HttpContext?.RequestAborted ?? CancellationToken.None;
        try
        {
            await using var db = database.OpenSessions();
            var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

            // 鎖定中的帳號連既有登入也不該繼續生效。
            var locked = await db.GetTable<CampusLoginAttempt>()
                .AnyAsync(a => a.NormalizedName == account.NormalizedUserName && a.LockedUntil > now, ct);
            if (locked)
            {
                return false;
            }

            return await source.IsAllowedAsync(account.SourceAccountId, account.IdentityType, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // 不記錄例外文字、SQL、連線字串或帳密，只留下故障類型供追蹤。
            logger.LogWarning("校務 Session 狀態檢查失敗，拒絕本次驗證。類型：{ErrorType}", e.GetType().Name);
            return false;
        }
    }
}
