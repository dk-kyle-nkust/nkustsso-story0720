// 驗證校務登入並建立或更新本系統的帳號資料。
using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services.Sources;

namespace NkustSso.Server.Services;

// 固定數量的鎖避免輸入任意帳號造成無上限的鎖物件；本原型採單一網站行程。
public sealed class CampusLoginGates
{
    private readonly SemaphoreSlim[] gates =
        Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public SemaphoreSlim For(string normalizedName)
    {
        // 取雜湊的第一個位元組當索引，剛好對應 256 個鎖。
        var index = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName))[0];
        return gates[index];
    }
}

public sealed record CampusLoginResult(Account? Account, bool Unavailable = false);

/// <summary>遠端驗證成功才建立本地固定識別；不複製來源密碼、不依同名帳號自動綁定既有權限。</summary>
public sealed class CampusLoginService(
    DatabaseFactory database,
    UserManager<Account> users,
    ICampusAccountSource source,
    IOptions<CampusAuthenticationOptions> options,
    CampusLoginGates gates,
    TimeProvider clock,
    ILogger<CampusLoginService> logger)
{
    public const string SourceName = "campus-mssql";

    /// <summary>連續失敗幾次後鎖定。</summary>
    private const int MaxFailedAttempts = 5;

    /// <summary>鎖定時間，也是失敗次數重新計數的間隔（毫秒）。</summary>
    private const long LockoutMilliseconds = 300_000;

    /// <summary>同一帳號同時登入時的等待上限。</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    /// <summary>來源回傳的身分型別只接受這幾種，不讓外部資料直接成為管理角色。</summary>
    private static readonly string[] AllowedIdentityTypes =
        ["student", "employee", "teacher", "staff", "other"];

    public async Task<CampusLoginResult> AuthenticateAsync(
        string userName,
        string password,
        string? currentAccountId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new CampusLoginResult(null);
        }

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 100)
        {
            return new CampusLoginResult(null);
        }

        if (string.IsNullOrEmpty(password) || password.Length > 256)
        {
            return new CampusLoginResult(null);
        }

        var input = userName.Trim();
        var normalized = users.NormalizeName(input)!;

        var gate = gates.For(normalized);
        if (!await gate.WaitAsync(GateTimeout, cancellationToken))
        {
            return new CampusLoginResult(null, true);
        }

        try
        {
            return await AuthenticateInsideGateAsync(input, password, normalized, currentAccountId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CampusLoginResult> AuthenticateInsideGateAsync(
        string input,
        string password,
        string normalized,
        string? currentAccountId,
        CancellationToken cancellationToken)
    {
        await using var attemptDb = database.OpenSessions();
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        var attempt = await attemptDb.GetTable<CampusLoginAttempt>()
            .SingleOrDefaultAsync(a => a.NormalizedName == normalized, cancellationToken);
        if (attempt?.LockedUntil > now)
        {
            return new CampusLoginResult(null);
        }

        var existing = await users.FindByNameAsync(input);
        if (!await CanUseExistingAccountAsync(existing, currentAccountId))
        {
            return new CampusLoginResult(null);
        }

        CampusAccountProfile? profile;
        try
        {
            profile = await source.AuthenticateAsync(input, password, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // 來源故障不算密碼錯誤，也不累計失敗次數。
            logger.LogWarning("校務帳密來源無法驗證。類型：{ErrorType}", e.GetType().Name);
            return new CampusLoginResult(null, true);
        }

        if (profile is null || users.NormalizeName(profile.MemberId) != normalized)
        {
            await RecordFailureAsync(attemptDb, attempt, normalized, now, cancellationToken);
            return new CampusLoginResult(null);
        }

        // 來源回傳資料也有邊界；不信任外部身分型別能直接成為管理角色。
        if (!AllowedIdentityTypes.Contains(profile.IdentityType))
        {
            return new CampusLoginResult(null);
        }

        if (profile.Name.Length > 200 || profile.MemberId.Length > 100
            || profile.SourceStatusCode.Length > 256 || profile.SourceStatusName.Length > 256
            || profile.UnitCode.Length > 256 || profile.UnitName.Length > 256)
        {
            return new CampusLoginResult(null);
        }

        var account = existing ?? new Account
        {
            UserName = profile.MemberId,
            AuthenticationSource = SourceName,
            SourceAccountId = profile.MemberId,
            PasswordHash = null,
            LockoutEnabled = true
        };

        if (account.SourceAccountId != profile.MemberId)
        {
            return new CampusLoginResult(null);
        }

        ApplyProfile(account, profile);

        var saved = existing is null
            ? await users.CreateAsync(account)
            : await users.UpdateAsync(account);
        if (!saved.Succeeded)
        {
            return new CampusLoginResult(null);
        }

        await attemptDb.GetTable<CampusLoginAttempt>()
            .Where(a => a.NormalizedName == normalized)
            .DeleteAsync(cancellationToken);

        return new CampusLoginResult(account);
    }

    /// <summary>
    /// 不接管同名本地管理者或其他來源帳號；登入中的使用者不可切換成另一人。
    /// </summary>
    private async Task<bool> CanUseExistingAccountAsync(Account? existing, string? currentAccountId)
    {
        if (existing is not null)
        {
            if (existing.AuthenticationSource != SourceName || !existing.Enabled)
            {
                return false;
            }

            if (await users.IsLockedOutAsync(existing))
            {
                return false;
            }
        }

        if (currentAccountId is not null && existing?.Id != currentAccountId)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 五分鐘未再失敗或已過鎖定期後重新計數；錯誤連線不計為密碼失敗。
    /// </summary>
    private static async Task RecordFailureAsync(
        SsoDatabase attemptDb,
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

        await attemptDb.InsertOrReplaceAsync(attempt, token: cancellationToken);
    }

    /// <summary>
    /// 學籍與學制名稱尚未確認來源欄位，先留空而不是填入可能錯誤的值。
    /// </summary>
    private static void ApplyProfile(Account account, CampusAccountProfile profile)
    {
        account.DisplayName = profile.Name;
        account.IdentityType = profile.IdentityType;
        account.SourceStatusCode = profile.SourceStatusCode;
        account.SourceStatusName = profile.SourceStatusName;
        account.UnitCode = profile.UnitCode;
        account.UnitName = profile.UnitName;
        account.ClassCode = profile.ClassCode;
        account.ClassName = profile.ClassName;
        account.EducationLevelCode = profile.EducationLevelCode;
        account.EducationLevelName = "";
        account.StudentStatusCode = "";
        account.StudentStatusName = "";

        // 校務帳號的密碼留在校務端，本系統不保存。
        account.PasswordHash = null;
    }
}
