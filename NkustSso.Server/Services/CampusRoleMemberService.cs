// 唯讀查詢校務人員，確認後加入本系統管理群組。
using System.Data;
using System.Text.Json;
using LinqToDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services.Sources;

namespace NkustSso.Server.Services;

public sealed record CampusRoleCandidate(
    string AccountName,
    string DisplayName,
    string StatusName,
    string IdentityType);

/// <summary>可以放進角色成員欄的本機帳號。</summary>
public sealed record CampusRoleMember(
    string AccountId,
    string AccountName,
    string DisplayName);

/// <summary>最高管理者預先指派校務人員；來源僅查詢，本機帳號的建立在同一交易完成。</summary>
public sealed class CampusRoleMemberService(
    DatabaseFactory database,
    ICampusDatabaseFactory campus,
    IOptions<CampusAuthenticationOptions> options,
    ILookupNormalizer normalizer,
    CampusLoginGates gates,
    TimeProvider clock,
    ILogger<CampusRoleMemberService> logger)
{
    /// <summary>同一帳號同時處理時的等待上限。</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    public async Task<CampusRoleCandidate> FindAsync(string? accountName, CancellationToken ct = default)
    {
        var input = accountName?.Trim() ?? "";
        if (input.Length < 1 || input.Length > 100 || input.Any(char.IsControl))
        {
            throw new InvalidOperationException("請輸入完整校務帳號，長度須為 1–100 字元。");
        }

        if (!options.Value.Enabled)
        {
            throw new InvalidOperationException("校務帳號查詢尚未啟用。");
        }

        var rows = await QuerySourceAsync(input, ct);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("查無此校務帳號，請確認完整帳號。");
        }

        if (rows.Count != 1)
        {
            throw new InvalidOperationException("校務帳號或身分對照不唯一，無法加入角色。");
        }

        return ToCandidate(rows[0], input);
    }

    private async Task<List<SourceAccountProfile>> QuerySourceAsync(string input, CancellationToken ct)
    {
        try
        {
            await using var source = campus.Open();

            // 取兩筆就夠判斷「結果不只一筆」。
            return await SourceAccountReader.Query(source, input).Take(2).ToListAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // 不把來源的錯誤內容轉給管理者，只留下類型供排查。
            logger.LogWarning("角色成員校務查詢失敗。類型：{ErrorType}", e.GetType().Name);
            throw new InvalidOperationException("校務帳號來源暫時無法查詢，請稍後重試。");
        }
    }

    /// <summary>
    /// 來源回傳的帳號必須與輸入相符，且基本欄位齊全，才能當成可加入的人選。
    /// </summary>
    private CampusRoleCandidate ToCandidate(SourceAccountProfile row, string input)
    {
        const string message = "校務帳號基本資料或身分對照不完整，無法加入角色。";

        var id = row.MemberId.Trim();
        var name = row.Name?.Trim();

        if (id.Length < 1 || id.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        // 避免模糊比對查到的是另一個帳號。
        if (normalizer.NormalizeName(id) != normalizer.NormalizeName(input))
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(row.StatusCode) || string.IsNullOrWhiteSpace(row.StatusName))
        {
            throw new InvalidOperationException(message);
        }

        return new CampusRoleCandidate(
            id,
            name,
            row.StatusName.Trim(),
            options.Value.IdentityType(row.StatusCode));
    }

    /// <summary>
    /// 讓本機有一個可用的校務帳號，回傳可直接放進角色成員欄的識別。
    /// 不寫入成員關聯也不動角色版本：成員由角色表單一併儲存，管理者在編輯頁
    /// 其他還沒儲存的變更才不會因為加入一位成員而被整批丟掉。
    /// </summary>
    public async Task<CampusRoleMember> ProvisionAsync(
        string roleId,
        string accountName,
        string actor,
        string ip,
        CancellationToken ct = default)
    {
        // 確認時重新查來源，不能信任預覽畫面傳回的姓名、來源或身分類別。
        var candidate = await FindAsync(accountName, ct);
        var normalized = normalizer.NormalizeName(candidate.AccountName)!;

        // 同一帳號同時被加入時，只讓一邊建立帳號。
        var gate = gates.For(normalized);
        if (!await gate.WaitAsync(GateTimeout, ct))
        {
            throw new InvalidOperationException("此帳號正在處理中，請稍後重試。");
        }

        try
        {
            return await ProvisionAccountAsync(roleId, candidate, normalized, actor, ip, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CampusRoleMember> ProvisionAccountAsync(
        string roleId,
        CampusRoleCandidate candidate,
        string normalized,
        string actor,
        string ip,
        CancellationToken ct)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        if (!await db.GetTable<AdminRole>().AnyAsync(r => r.Id == roleId, ct))
        {
            throw new InvalidOperationException("角色已被刪除，請返回角色清單重新確認。");
        }

        var account = await db.Users.SingleOrDefaultAsync(a => a.NormalizedUserName == normalized, ct);
        if (account is not null)
        {
            EnsureAccountCanJoin(account, candidate);
            await tx.CommitAsync(ct);

            return new CampusRoleMember(
                account.Id,
                account.UserName ?? candidate.AccountName,
                account.DisplayName);
        }

        account = BuildAccount(candidate, normalized);
        await db.InsertAsync(account, token: ct);

        // 只記帳號建立；成員關聯的前後快照由角色儲存那筆稽核負責。
        await db.InsertAsync(
            new AdminManagementAudit
            {
                Kind = "role",
                TargetId = roleId,
                Action = "provision-member",
                ActorId = actor,
                RemoteIp = ip,
                OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
                BeforeJson = "",
                AfterJson = JsonSerializer.Serialize(new
                {
                    AccountId = account.Id,
                    candidate.AccountName,
                    candidate.DisplayName,
                    candidate.IdentityType
                })
            },
            token: ct);

        await tx.CommitAsync(ct);

        return new CampusRoleMember(account.Id, candidate.AccountName, candidate.DisplayName);
    }

    private void EnsureAccountCanJoin(Account account, CampusRoleCandidate candidate)
    {
        var differentSource = account.AuthenticationSource != CampusLoginService.SourceName
            || account.SourceAccountId != candidate.AccountName;
        if (differentSource)
        {
            throw new InvalidOperationException("本系統已有同名但來源不同的帳號，無法自動合併或授權。");
        }

        if (!account.Enabled || (account.LockoutEnabled && account.LockoutEnd > clock.GetUtcNow()))
        {
            throw new InvalidOperationException("此帳號已停用或鎖定，無法加入角色。");
        }
    }

    /// <summary>
    /// 校務帳號在本系統沒有密碼，一律透過校務登入，所以 PasswordHash 留空。
    /// </summary>
    private static Account BuildAccount(CampusRoleCandidate candidate, string normalized)
    {
        return new Account
        {
            UserName = candidate.AccountName,
            NormalizedUserName = normalized,
            DisplayName = candidate.DisplayName,
            IdentityType = candidate.IdentityType,
            AuthenticationSource = CampusLoginService.SourceName,
            SourceAccountId = candidate.AccountName,
            PasswordHash = null,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }
}
