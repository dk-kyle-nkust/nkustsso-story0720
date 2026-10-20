// 以資料庫保存 OIDC Token 狀態及撤銷資訊。
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
/// 保存 OpenIddict Token／授權碼的識別值、有效狀態、期限及關聯授權，用於兌換與撤銷。
/// 以下 Get／Set 僅映射實體欄位；集合以 JSON 保存，實際新增與更新由共用儲存層負責。
/// </summary>
public sealed class LinqTokenStore(DatabaseFactory factory)
    : LinqOidcStore<OidcToken>(factory), IOpenIddictTokenStore<OidcToken>
{
    /// <summary>資料庫用毫秒時間戳保存時間，三個日期欄位共用同一套轉換。</summary>
    private static DateTimeOffset? ToDate(long? milliseconds)
    {
        if (milliseconds is null)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value);
    }

    public ValueTask<string?> GetApplicationIdAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ApplicationId);
    }

    public ValueTask SetApplicationIdAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.ApplicationId = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetAuthorizationIdAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.AuthorizationId);
    }

    public ValueTask SetAuthorizationIdAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.AuthorizationId = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetPayloadAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Payload);
    }

    public ValueTask SetPayloadAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.Payload = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetReferenceIdAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ReferenceId);
    }

    public ValueTask SetReferenceIdAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.ReferenceId = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetStatusAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Status);
    }

    public ValueTask SetStatusAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.Status = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetSubjectAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Subject);
    }

    public ValueTask SetSubjectAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.Subject = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetTypeAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Type);
    }

    public ValueTask SetTypeAsync(OidcToken value, string? text, CancellationToken ct)
    {
        value.Type = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(ToDate(value.CreationDate));
    }

    public ValueTask SetCreationDateAsync(OidcToken value, DateTimeOffset? date, CancellationToken ct)
    {
        value.CreationDate = date?.ToUnixTimeMilliseconds();
        return ValueTask.CompletedTask;
    }

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(ToDate(value.ExpirationDate));
    }

    public ValueTask SetExpirationDateAsync(OidcToken value, DateTimeOffset? date, CancellationToken ct)
    {
        value.ExpirationDate = date?.ToUnixTimeMilliseconds();
        return ValueTask.CompletedTask;
    }

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(OidcToken value, CancellationToken ct)
    {
        return ValueTask.FromResult(ToDate(value.RedemptionDate));
    }

    public ValueTask SetRedemptionDateAsync(OidcToken value, DateTimeOffset? date, CancellationToken ct)
    {
        value.RedemptionDate = date?.ToUnixTimeMilliseconds();
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<OidcToken> FindByApplicationIdAsync(string id, CancellationToken ct)
    {
        return FindWhere(x => x.ApplicationId == id, ct);
    }

    public IAsyncEnumerable<OidcToken> FindBySubjectAsync(string subject, CancellationToken ct)
    {
        return FindWhere(x => x.Subject == subject, ct);
    }

    public IAsyncEnumerable<OidcToken> FindByAuthorizationIdAsync(string id, CancellationToken ct)
    {
        return FindWhere(x => x.AuthorizationId == id, ct);
    }

    public ValueTask<OidcToken?> FindByReferenceIdAsync(string id, CancellationToken ct)
    {
        return FindOne(x => x.ReferenceId == id, ct);
    }

    /// <summary>
    /// 依帳號、內部用戶端 ID、狀態與類型組合查詢，null 條件代表不限。
    /// </summary>
    public IAsyncEnumerable<OidcToken> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken ct)
    {
        return FindWhere(
            x => (subject == null || x.Subject == subject)
                && (client == null || x.ApplicationId == client)
                && (status == null || x.Status == status)
                && (type == null || x.Type == type),
            ct);
    }

    /// <summary>
    /// 依非 null 條件撤銷 Token 並更新版本；client 指內部 Application ID，回傳受影響筆數。
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

        return await db.GetTable<OidcToken>()
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
    /// 撤銷指定授權下的所有 Token，並換新版本以阻止舊狀態回寫。
    /// </summary>
    public async ValueTask<long> RevokeByAuthorizationIdAsync(string id, CancellationToken ct)
    {
        await using var db = Factory.Open();
        var stamp = Guid.NewGuid().ToString();

        return await db.GetTable<OidcToken>()
            .Where(x => x.AuthorizationId == id)
            .Set(x => x.Status, "revoked")
            .Set(x => x.ConcurrencyToken, stamp)
            .UpdateAsync(ct);
    }

    /// <summary>
    /// 刪除門檻時間以前且已失效、過期或所屬授權無效的 Token。
    /// </summary>
    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken ct)
    {
        await using var db = Factory.Open();
        var cutoff = threshold.ToUnixTimeMilliseconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 三種都算不需要保留：狀態已非有效、已過期、所屬授權本身已失效。
        return await db.GetTable<OidcToken>()
            .Where(x => x.CreationDate < cutoff
                && ((x.Status != "valid" && x.Status != "inactive")
                    || x.ExpirationDate < now
                    || (x.AuthorizationId != null
                        && db.GetTable<OidcAuthorization>()
                            .Any(a => a.Id == x.AuthorizationId && a.Status != "valid"))))
            .DeleteAsync(ct);
    }
}
