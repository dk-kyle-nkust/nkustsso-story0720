// 以資料庫保存 OIDC scope 定義。
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
/// 保存 OIDC scope 定義、資源關聯及多語顯示文字。
/// 以下 Get／Set 僅映射實體欄位；集合以 JSON 保存，實際新增與更新由共用儲存層負責。
/// </summary>
public sealed class LinqScopeStore(DatabaseFactory factory)
    : LinqOidcStore<OidcScope>(factory), IOpenIddictScopeStore<OidcScope>
{
    public ValueTask<string?> GetDescriptionAsync(OidcScope value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Description);
    }

    public ValueTask SetDescriptionAsync(OidcScope value, string? text, CancellationToken ct)
    {
        value.Description = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetDisplayNameAsync(OidcScope value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.DisplayName);
    }

    public ValueTask SetDisplayNameAsync(OidcScope value, string? text, CancellationToken ct)
    {
        value.DisplayName = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetNameAsync(OidcScope value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.Name);
    }

    public ValueTask SetNameAsync(OidcScope value, string? text, CancellationToken ct)
    {
        value.Name = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(OidcScope value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.Resources));
    }

    public ValueTask SetResourcesAsync(OidcScope value, ImmutableArray<string> items, CancellationToken ct)
    {
        value.Resources = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(
        OidcScope value,
        CancellationToken ct)
    {
        return ValueTask.FromResult(Cultures(value.Descriptions));
    }

    public ValueTask SetDescriptionsAsync(
        OidcScope value,
        ImmutableDictionary<CultureInfo, string> items,
        CancellationToken ct)
    {
        // 資料庫只存文化名稱字串，讀回時再轉回 CultureInfo。
        var byCultureName = items.ToDictionary(x => x.Key.Name, x => x.Value);
        value.Descriptions = JsonSerializer.Serialize(byCultureName);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OidcScope value,
        CancellationToken ct)
    {
        return ValueTask.FromResult(Cultures(value.DisplayNames));
    }

    public ValueTask SetDisplayNamesAsync(
        OidcScope value,
        ImmutableDictionary<CultureInfo, string> items,
        CancellationToken ct)
    {
        var byCultureName = items.ToDictionary(x => x.Key.Name, x => x.Value);
        value.DisplayNames = JsonSerializer.Serialize(byCultureName);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 依 scope 名稱取得定義。
    /// </summary>
    public ValueTask<OidcScope?> FindByNameAsync(string name, CancellationToken ct)
    {
        return FindOne(x => x.Name == name, ct);
    }

    /// <summary>
    /// 一次查詢多個 scope 名稱，避免逐筆往返資料庫。
    /// </summary>
    public IAsyncEnumerable<OidcScope> FindByNamesAsync(ImmutableArray<string> names, CancellationToken ct)
    {
        var values = names.ToArray();
        return FindWhere(x => values.Contains(x.Name!), ct);
    }

    /// <summary>
    /// 搜尋使用指定資源的 scope，JSON 候選查詢後仍以解碼集合精確比對。
    /// </summary>
    public async IAsyncEnumerable<OidcScope> FindByResourceAsync(
        string resource,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var encoded = JsonSerializer.Serialize(resource);
        var candidates = FindWhere(x => x.Resources != null && x.Resources.Contains(encoded), ct);

        await foreach (var scope in candidates)
        {
            if (Array(scope.Resources).Contains(resource, StringComparer.Ordinal))
            {
                yield return scope;
            }
        }
    }
}
