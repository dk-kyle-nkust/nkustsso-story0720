// 以資料庫保存 OIDC 授權紀錄。
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LinqToDB;
using Microsoft.IdentityModel.Tokens;
using NkustSso.Server.Models;
using OpenIddict.Abstractions;

namespace NkustSso.Server.Services;

/// <summary>
/// 保存 OIDC 授權紀錄與 scope；這裡的授權紀錄不等同各業務系統的功能角色。
/// 以下 Get／Set 僅映射實體欄位；集合以 JSON 保存，實際新增與更新由共用儲存層負責。
/// </summary>
public sealed class LinqAuthorizationStore(DatabaseFactory factory)
    : LinqOidcStore<OidcAuthorization>(factory), IOpenIddictAuthorizationStore<OidcAuthorization>
{
    public ValueTask<string?> GetApplicationIdAsync(OidcAuthorization value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ApplicationId);
    }

    public ValueTask SetApplicationIdAsync(OidcAuthorization value, string? text, CancellationToken ct)
    {
        value.ApplicationId = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetStatusAsync(OidcAuthorization value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Status);
    }

    public ValueTask SetStatusAsync(OidcAuthorization value, string? text, CancellationToken ct)
    {
        value.Status = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetSubjectAsync(OidcAuthorization value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Subject);
    }

    public ValueTask SetSubjectAsync(OidcAuthorization value, string? text, CancellationToken ct)
    {
        value.Subject = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetTypeAsync(OidcAuthorization value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Type);
    }

    public ValueTask SetTypeAsync(OidcAuthorization value, string? text, CancellationToken ct)
    {
        value.Type = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetScopesAsync(OidcAuthorization value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.Scopes));
    }

    public ValueTask SetScopesAsync(OidcAuthorization value, ImmutableArray<string> items, CancellationToken ct)
    {
        value.Scopes = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OidcAuthorization value, CancellationToken ct)
    {
        // 資料庫以毫秒時間戳保存時間。
        if (value.CreationDate is null)
        {
            return ValueTask.FromResult<DateTimeOffset?>(null);
        }

        var date = DateTimeOffset.FromUnixTimeMilliseconds(value.CreationDate.Value);
        return ValueTask.FromResult<DateTimeOffset?>(date);
    }

    public ValueTask SetCreationDateAsync(OidcAuthorization value, DateTimeOffset? date, CancellationToken ct)
    {
        value.CreationDate = date?.ToUnixTimeMilliseconds();
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<OidcAuthorization> FindByApplicationIdAsync(string id, CancellationToken ct)
    {
        return FindWhere(x => x.ApplicationId == id, ct);
    }

    public IAsyncEnumerable<OidcAuthorization> FindBySubjectAsync(string subject, CancellationToken ct)
    {
        return FindWhere(x => x.Subject == subject, ct);
    }

    /// <summary>
    /// 以非 null 條件篩選授權並撤銷，同時更新版本防止舊物件覆寫；client 為內部 Application ID。
    /// </summary>
    public async ValueTask<long> RevokeAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken ct)
    {
        await using var db = Factory.Open();
        var stamp = Guid.NewGuid().ToString();

        return await db.GetTable<OidcAuthorization>()
            .Where(x => (subject == null || x.Subject == subject)
                && (client == null || x.ApplicationId == client)
                && (status == null || x.Status == status)
                && (type == null || x.Type == type))
            .Set(x => x.Status, "revoked")
            .Set(x => x.ConcurrencyToken, stamp)
            .UpdateAsync(ct);
    }

    public ValueTask<long> RevokeByApplicationIdAsync(string id, CancellationToken ct)
    {
        return RevokeAsync(null, id, null, null, ct);
    }

    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken ct)
    {
        return RevokeAsync(subject, null, null, null, ct);
    }

    /// <summary>
    /// 組合帳號、內部用戶端 ID、狀態與類型查詢；若指定 scopes，紀錄須包含全部要求。
    /// </summary>
    public async IAsyncEnumerable<OidcAuthorization> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        ImmutableArray<string>? scopes,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var matches = FindWhere(
            x => (subject == null || x.Subject == subject)
                && (client == null || x.ApplicationId == client)
                && (status == null || x.Status == status)
                && (type == null || x.Type == type),
            ct);

        await foreach (var value in matches)
        {
            if (scopes is null)
            {
                yield return value;
                continue;
            }

            // 要求的 scope 必須全部都在這筆授權裡，少一個就不算符合。
            var granted = Array(value.Scopes);
            if (scopes.Value.All(s => granted.Contains(s, StringComparer.Ordinal)))
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// 清除門檻時間之前無效或不再有有效 Token 的臨時授權，並在同一交易刪除相關 Token。
    /// </summary>
    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken ct)
    {
        await using var db = Factory.Open();
        var cutoff = threshold.ToUnixTimeMilliseconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await using var tx = await db.BeginTransactionAsync(ct);

        // 臨時授權只要底下已經沒有還能用的 Token 就可以清掉。
        var ids = await db.GetTable<OidcAuthorization>()
            .Where(x => x.CreationDate < cutoff
                && (x.Status != "valid"
                    || (x.Type == "ad-hoc"
                        && !db.GetTable<OidcToken>()
                            .Any(t => t.AuthorizationId == x.Id
                                && (t.Status == "valid" || t.Status == "inactive")
                                && (t.ExpirationDate == null || t.ExpirationDate > now)))))
            .Select(x => x.Id)
            .ToArrayAsync(ct);

        await db.GetTable<OidcToken>()
            .Where(x => ids.Contains(x.AuthorizationId!))
            .DeleteAsync(ct);

        var count = await db.GetTable<OidcAuthorization>()
            .Where(x => ids.Contains(x.Id))
            .DeleteAsync(ct);

        await tx.CommitAsync(ct);
        return count;
    }
}
