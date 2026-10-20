// 提供 OIDC 儲存實作共用的查詢與版本檢查。
using System.Collections.Immutable;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LinqToDB;
using NkustSso.Server.Models;
using OpenIddict.Abstractions;

namespace NkustSso.Server.Services;

/// <summary>
/// OpenIddict 四類協定實體共用的 linq2db 儲存層；以版本欄位避免過期更新覆寫較新的狀態。
/// </summary>
public abstract class LinqOidcStore<T>(DatabaseFactory factory) where T : OidcEntity, new()
{
    /// <summary>版本衝突時統一的錯誤訊息，OpenIddict 會據此重試或放棄。</summary>
    private const string ConcurrencyMessage = "The protocol record was changed by another request.";

    protected DatabaseFactory Factory { get; } = factory;

    public async ValueTask<long> CountAsync(CancellationToken ct)
    {
        await using var db = Factory.Open();
        return await db.GetTable<T>().LongCountAsync(ct);
    }

    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<T>, IQueryable<TResult>> query,
        CancellationToken ct)
    {
        await using var db = Factory.Open();
        return await query(db.GetTable<T>()).LongCountAsync(ct);
    }

    public async ValueTask CreateAsync(T value, CancellationToken ct)
    {
        await using var db = Factory.Open();
        await db.InsertAsync(value, token: ct);
    }

    public async ValueTask<T?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = Factory.Open();
        return await db.GetTable<T>().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public ValueTask<string?> GetIdAsync(T value, CancellationToken ct)
    {
        return ValueTask.FromResult<string?>(value.Id);
    }

    public ValueTask<T> InstantiateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new T());
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(T value, CancellationToken ct)
    {
        return ValueTask.FromResult(Dictionary<JsonElement>(value.Properties));
    }

    public ValueTask SetPropertiesAsync(
        T value,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken ct)
    {
        value.Properties = JsonSerializer.Serialize(properties);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 套用 OpenIddict 提供的查詢及投影，取得第一筆結果；ct 用於取消資料庫操作。
    /// </summary>
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<T>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken ct)
    {
        await using var db = Factory.Open();
        return await query(db.GetTable<T>(), state).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// 依 ID 穩定排序後套用 offset／count 分頁，並逐筆非同步傳回。
    /// </summary>
    public async IAsyncEnumerable<T> ListAsync(
        int? count,
        int? offset,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = Factory.Open();
        var query = db.GetTable<T>().OrderBy(x => x.Id).AsQueryable();

        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue)
        {
            query = query.Take(count.Value);
        }

        await foreach (var value in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return value;
        }
    }

    /// <summary>
    /// 執行呼叫端提供的投影查詢，以非同步列舉避免一次載入全部資料。
    /// </summary>
    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<T>, TState, IQueryable<TResult>> query,
        TState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = Factory.Open();
        var results = query(db.GetTable<T>(), state).AsAsyncEnumerable();

        await foreach (var value in results.WithCancellation(ct))
        {
            yield return value;
        }
    }

    /// <summary>
    /// 更新前比對原始 ConcurrencyToken；成功後換新版本，衝突時拒絕覆寫，
    /// 避免撤銷或兌換狀態被還原。
    /// </summary>
    public async ValueTask UpdateAsync(T value, CancellationToken ct)
    {
        await using var db = Factory.Open();
        var stamp = Guid.NewGuid().ToString();

        var count = await db.GetTable<T>()
            .Where(x => x.Id == value.Id && x.ConcurrencyToken == value.ConcurrencyToken)
            .Set(x => x.ConcurrencyToken, stamp)
            .Set(x => x.ApplicationId, value.ApplicationId)
            .Set(x => x.AuthorizationId, value.AuthorizationId)
            .Set(x => x.ApplicationType, value.ApplicationType)
            .Set(x => x.ClientId, value.ClientId)
            .Set(x => x.ClientSecret, value.ClientSecret)
            .Set(x => x.ClientType, value.ClientType)
            .Set(x => x.ConsentType, value.ConsentType)
            .Set(x => x.DisplayName, value.DisplayName)
            .Set(x => x.DisplayNames, value.DisplayNames)
            .Set(x => x.Description, value.Description)
            .Set(x => x.Descriptions, value.Descriptions)
            .Set(x => x.JsonWebKeySet, value.JsonWebKeySet)
            .Set(x => x.Name, value.Name)
            .Set(x => x.Payload, value.Payload)
            .Set(x => x.Permissions, value.Permissions)
            .Set(x => x.PostLogoutRedirectUris, value.PostLogoutRedirectUris)
            .Set(x => x.Properties, value.Properties)
            .Set(x => x.RedirectUris, value.RedirectUris)
            .Set(x => x.ReferenceId, value.ReferenceId)
            .Set(x => x.Requirements, value.Requirements)
            .Set(x => x.Resources, value.Resources)
            .Set(x => x.Scopes, value.Scopes)
            .Set(x => x.Settings, value.Settings)
            .Set(x => x.Status, value.Status)
            .Set(x => x.Subject, value.Subject)
            .Set(x => x.Type, value.Type)
            .Set(x => x.CreationDate, value.CreationDate)
            .Set(x => x.ExpirationDate, value.ExpirationDate)
            .Set(x => x.RedemptionDate, value.RedemptionDate)
            .UpdateAsync(ct);

        if (count != 1)
        {
            throw new OpenIddictExceptions.ConcurrencyException(ConcurrencyMessage);
        }

        value.ConcurrencyToken = stamp;
    }

    /// <summary>
    /// 確認版本後刪除實體，並在同一交易清除相關授權／Token，避免留下孤立紀錄。
    /// </summary>
    public async ValueTask DeleteAsync(T value, CancellationToken ct)
    {
        await using var db = Factory.Open();
        await using var tx = await db.BeginTransactionAsync(ct);

        var deleted = await db.GetTable<T>()
            .Where(x => x.Id == value.Id && x.ConcurrencyToken == value.ConcurrencyToken)
            .DeleteAsync(ct);

        if (deleted != 1)
        {
            throw new OpenIddictExceptions.ConcurrencyException(ConcurrencyMessage);
        }

        if (typeof(T) == typeof(OidcApplication))
        {
            await db.GetTable<OidcToken>().Where(x => x.ApplicationId == value.Id).DeleteAsync(ct);
            await db.GetTable<OidcAuthorization>().Where(x => x.ApplicationId == value.Id).DeleteAsync(ct);
        }

        if (typeof(T) == typeof(OidcAuthorization))
        {
            await db.GetTable<OidcToken>().Where(x => x.AuthorizationId == value.Id).DeleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// 以型別化條件查詢並逐筆回傳，供各協定儲存介面共用。
    /// </summary>
    protected async IAsyncEnumerable<T> FindWhere(
        Expression<Func<T, bool>> filter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = Factory.Open();
        var results = db.GetTable<T>().Where(filter).AsAsyncEnumerable();

        await foreach (var value in results.WithCancellation(ct))
        {
            yield return value;
        }
    }

    /// <summary>
    /// 以型別化條件取得唯一紀錄，查無資料時回傳 null。
    /// </summary>
    protected async ValueTask<T?> FindOne(Expression<Func<T, bool>> filter, CancellationToken ct)
    {
        await using var db = Factory.Open();
        return await db.GetTable<T>().SingleOrDefaultAsync(filter, ct);
    }

    /// <summary>
    /// 把集合 JSON 還原為不可變陣列；資料未設定時回傳空集合。
    /// </summary>
    protected static ImmutableArray<string> Array(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<ImmutableArray<string>>(value);
    }

    /// <summary>
    /// 把屬性 JSON 還原為型別化不可變字典。
    /// </summary>
    protected static ImmutableDictionary<string, TValue> Dictionary<TValue>(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ImmutableDictionary<string, TValue>.Empty;
        }

        return JsonSerializer.Deserialize<ImmutableDictionary<string, TValue>>(value)!;
    }

    /// <summary>
    /// 將 JSON 的文化代碼鍵轉為 CultureInfo，供多語名稱與描述使用。
    /// </summary>
    protected static ImmutableDictionary<CultureInfo, string> Cultures(string? value)
    {
        return Dictionary<string>(value)
            .ToImmutableDictionary(x => CultureInfo.GetCultureInfo(x.Key), x => x.Value);
    }
}
