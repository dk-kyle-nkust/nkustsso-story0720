// 以資料庫保存 OIDC 用戶端登記。
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
/// OIDC 用戶端登記的儲存介面；包含識別碼、協定密鑰欄位、回呼白名單及允許的協定操作。
/// 以下 Get／Set 僅映射實體欄位；集合以 JSON 保存，實際新增與更新由共用儲存層負責。
/// </summary>
public sealed class LinqApplicationStore(DatabaseFactory factory)
    : LinqOidcStore<OidcApplication>(factory), IOpenIddictApplicationStore<OidcApplication>
{
    public ValueTask<string?> GetApplicationTypeAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ApplicationType);
    }

    public ValueTask SetApplicationTypeAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.ApplicationType = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetClientIdAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ClientId);
    }

    public ValueTask SetClientIdAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.ClientId = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetClientSecretAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ClientSecret);
    }

    public ValueTask SetClientSecretAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.ClientSecret = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetClientTypeAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ClientType);
    }

    public ValueTask SetClientTypeAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.ClientType = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetConsentTypeAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.ConsentType);
    }

    public ValueTask SetConsentTypeAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.ConsentType = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetDisplayNameAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(value.DisplayName);
    }

    public ValueTask SetDisplayNameAsync(OidcApplication value, string? text, CancellationToken ct)
    {
        value.DisplayName = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.Permissions));
    }

    public ValueTask SetPermissionsAsync(OidcApplication value, ImmutableArray<string> items, CancellationToken ct)
    {
        value.Permissions = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.PostLogoutRedirectUris));
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(
        OidcApplication value,
        ImmutableArray<string> items,
        CancellationToken ct)
    {
        value.PostLogoutRedirectUris = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.RedirectUris));
    }

    public ValueTask SetRedirectUrisAsync(OidcApplication value, ImmutableArray<string> items, CancellationToken ct)
    {
        value.RedirectUris = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(OidcApplication value, CancellationToken ct)
    {
        return ValueTask.FromResult(Array(value.Requirements));
    }

    public ValueTask SetRequirementsAsync(OidcApplication value, ImmutableArray<string> items, CancellationToken ct)
    {
        value.Requirements = JsonSerializer.Serialize(items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OidcApplication value,
        CancellationToken ct)
    {
        return ValueTask.FromResult(Cultures(value.DisplayNames));
    }

    public ValueTask SetDisplayNamesAsync(
        OidcApplication value,
        ImmutableDictionary<CultureInfo, string> items,
        CancellationToken ct)
    {
        // 資料庫只存文化名稱字串，讀回時再轉回 CultureInfo。
        var byCultureName = items.ToDictionary(x => x.Key.Name, x => x.Value);
        value.DisplayNames = JsonSerializer.Serialize(byCultureName);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(
        OidcApplication value,
        CancellationToken ct)
    {
        return ValueTask.FromResult(Dictionary<string>(value.Settings));
    }

    public ValueTask SetSettingsAsync(
        OidcApplication value,
        ImmutableDictionary<string, string> settings,
        CancellationToken ct)
    {
        value.Settings = JsonSerializer.Serialize(settings);
        return ValueTask.CompletedTask;
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(OidcApplication value, CancellationToken ct)
    {
        if (value.JsonWebKeySet is null)
        {
            return ValueTask.FromResult<JsonWebKeySet?>(null);
        }

        return ValueTask.FromResult<JsonWebKeySet?>(new JsonWebKeySet(value.JsonWebKeySet));
    }

    public ValueTask SetJsonWebKeySetAsync(OidcApplication value, JsonWebKeySet? set, CancellationToken ct)
    {
        value.JsonWebKeySet = set is null ? null : JsonSerializer.Serialize(set);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 依公開 Client ID 取得協定用戶端；系統啟用狀態仍由登入端點另行檢查。
    /// </summary>
    public ValueTask<OidcApplication?> FindByClientIdAsync(string id, CancellationToken ct)
    {
        return FindOne(x => x.ClientId == id, ct);
    }

    /// <summary>
    /// 先以 JSON 文字縮小候選範圍，再逐項精確比對登入回呼網址，避免部分字串誤配。
    /// </summary>
    public async IAsyncEnumerable<OidcApplication> FindByRedirectUriAsync(
        string uri,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var encoded = JsonSerializer.Serialize(uri);
        var candidates = FindWhere(x => x.RedirectUris != null && x.RedirectUris.Contains(encoded), ct);

        await foreach (var app in candidates)
        {
            if (Array(app.RedirectUris).Contains(uri, StringComparer.Ordinal))
            {
                yield return app;
            }
        }
    }

    /// <summary>
    /// 搜尋登出回呼白名單，候選資料解碼後仍須完整且區分大小寫比對。
    /// </summary>
    public async IAsyncEnumerable<OidcApplication> FindByPostLogoutRedirectUriAsync(
        string uri,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var encoded = JsonSerializer.Serialize(uri);
        var candidates = FindWhere(
            x => x.PostLogoutRedirectUris != null && x.PostLogoutRedirectUris.Contains(encoded),
            ct);

        await foreach (var app in candidates)
        {
            if (Array(app.PostLogoutRedirectUris).Contains(uri, StringComparer.Ordinal))
            {
                yield return app;
            }
        }
    }
}
