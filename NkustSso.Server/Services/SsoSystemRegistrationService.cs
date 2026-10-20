// 儲存 SSO 系統與首頁連結，驗證回呼及資料授權。
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqToDB;
using NkustSso.Server.Models;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace NkustSso.Server.Services;

/// <summary>
/// 管理接收系統與首頁連結的登記、驗證及交易儲存；公開設定與密鑰保存分開處理。
/// </summary>
public sealed class SsoSystemRegistrationService(
    DatabaseFactory database,
    IOpenIddictApplicationManager applications,
    TimeProvider clock,
    IdentityCatalog? identityCatalog = null)
{
    /// <summary>系統識別碼只收小寫英數、底線與連字號，長度 1–100。</summary>
    private const string ClientIdPattern = "^[a-z0-9][a-z0-9_-]{0,99}$";

    /// <summary>
    /// 依協定檢查識別碼、HTTPS 網址、分類及欄位白名單；純連結不應登記登入回呼。
    /// </summary>
    public static IEnumerable<string> Validate(RegisteredSystem value)
    {
        if (!Regex.IsMatch(value.ClientId ?? "", ClientIdPattern))
        {
            yield return "系統識別碼限 1–100 個小寫英文字母、數字、底線或連字號。";
        }

        if (!SystemProtocols.IsKnown(value.Protocol))
        {
            yield return "請選擇有效的登入方式。";
        }

        foreach (var error in ValidateSamlSettings(value))
        {
            yield return error;
        }

        foreach (var error in ValidateDisplayText(value))
        {
            yield return error;
        }

        foreach (var error in ValidateUrls(value))
        {
            yield return error;
        }

        if (string.IsNullOrEmpty(value.Category) || value.Category.Length > 50)
        {
            yield return "請選擇首頁分類。";
        }

        if (value.Keywords is null || value.Keywords.Length > 300)
        {
            yield return "搜尋別名最多 300 字。";
        }

        if (value.SortOrder < 0 || value.SortOrder > 10000)
        {
            yield return "排序限 0–10000。";
        }

        if (value.ClaimMask < 0 || (value.ClaimMask & ~IdentityClaimReleasePolicy.All) != 0)
        {
            yield return "包含不允許提供的欄位。";
        }
    }

    /// <summary>
    /// SAML 的 Entity ID 與簽章憑證只對 SAML 系統有意義，其他協定填了就是設定錯誤。
    /// </summary>
    private static IEnumerable<string> ValidateSamlSettings(RegisteredSystem value)
    {
        if (value.Protocol != "saml")
        {
            var hasSamlSettings = !string.IsNullOrEmpty(value.SamlEntityId)
                || !string.IsNullOrEmpty(value.SamlSigningCertificate);
            if (hasSamlSettings)
            {
                yield return "SAML 設定只適用 SAML 系統。";
            }

            yield break;
        }

        if (!IsValidEntityId(value.SamlEntityId))
        {
            yield return "SAML SP Entity ID 必須為完整 URI（網址或 URN），最多 1000 字。";
        }

        if (!SamlIdentityProvider.IsValidPartnerCertificate(value.SamlSigningCertificate))
        {
            yield return "請提供有效的 SP RSA 簽章公開憑證（PEM，至少 2048 位元），不可包含私鑰。";
        }
    }

    /// <summary>
    /// Entity ID 可以是網址或 URN，所以只要求是絕對 URI 且不含空白。
    /// </summary>
    private static bool IsValidEntityId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length > 1000)
        {
            return false;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    /// <summary>
    /// 中英文名稱與說明的長度上限，對應資料表欄位與管理頁版面。
    /// </summary>
    private static IEnumerable<string> ValidateDisplayText(RegisteredSystem value)
    {
        if (string.IsNullOrWhiteSpace(value.DisplayName) || value.DisplayName.Length > 100)
        {
            yield return "系統名稱必填且最多 100 字。";
        }

        if (value.Description is null || value.Description.Length > 300)
        {
            yield return "系統說明最多 300 字。";
        }

        if (value.DisplayNameEn is null || value.DisplayNameEn.Length > 100)
        {
            yield return "英文系統名稱最多 100 字。";
        }

        if (value.DescriptionEn is null || value.DescriptionEn.Length > 600)
        {
            yield return "英文功能詳述最多 600 字。";
        }
    }

    /// <summary>
    /// 入口、登入回呼與登出回呼三個網址的規則；純首頁連結不接收登入，不得有回呼。
    /// </summary>
    private static IEnumerable<string> ValidateUrls(RegisteredSystem value)
    {
        if (!SafeUrl(value.EntryUrl))
        {
            yield return "入口網址必須為完整 HTTPS 網址，不可包含帳密、萬用字元或 #片段。";
        }

        // 僅首頁連結的系統不接收登入，因此不得登記任何回呼網址。
        if (value.Protocol == "link")
        {
            if (!string.IsNullOrEmpty(value.RedirectUri))
            {
                yield return "僅首頁連結的系統不需要登入回呼網址。";
            }
        }
        else if (!SafeUrl(value.RedirectUri))
        {
            yield return "登入回呼網址必須為完整 HTTPS 網址，不可包含帳密、萬用字元或 #片段。";
        }

        if (!string.IsNullOrEmpty(value.PostLogoutRedirectUri))
        {
            if (value.Protocol != "oidc" || !SafeUrl(value.PostLogoutRedirectUri))
            {
                yield return "登出回呼只適用 OIDC，並須為完整 HTTPS 網址。";
            }
        }
    }

    /// <summary>
    /// 僅接受完整 HTTPS 網址，排除內嵌帳密、控制字元、萬用字元及片段。
    /// 逐項檢查而不是串一長串 and 條件，出問題時可以直接在某一行下中斷點。
    /// </summary>
    private static bool SafeUrl(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Length > 2000)
        {
            return false;
        }

        // 前後有空白代表來源沒整理過，不猜測使用者的原意。
        if (value != value.Trim())
        {
            return false;
        }

        if (value.Any(char.IsControl))
        {
            return false;
        }

        if (value.Contains('*') || value.Contains('\\'))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != "https")
        {
            return false;
        }

        // 擋掉把帳號密碼寫在網址裡的寫法。
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // 井字號片段不會送到伺服器，出現在回呼網址多半是設定錯誤。
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 依固定 Client ID 讀取設定，包含已停用項目，供管理頁編輯。
    /// </summary>
    public async Task<RegisteredSystem?> FindAsync(string? id)
    {
        await using var db = database.Open();
        return await db.RegisteredSystems.SingleOrDefaultAsync(s => s.ClientId == id);
    }

    /// <summary>
    /// 登入端點只接受協定符合且已啟用的登記；首頁可見條件不在此處當作登入權限。
    /// 未登記、停用或協定不符的系統不能取得新的登入結果。
    /// </summary>
    public async Task<RegisteredSystem?> ActiveAsync(string? id, string protocol)
    {
        var system = await FindAsync(id);
        if (system is null)
        {
            return null;
        }

        if (!system.Enabled)
        {
            return null;
        }

        if (system.Protocol != protocol)
        {
            return null;
        }

        return system;
    }

    /// <summary>
    /// 建立或依版本更新系統，並在交易中保存可見條件與異動紀錄。
    /// 新增 SSO 系統時產生密鑰供這次回應使用；編輯或純首頁連結回傳 null。
    /// </summary>
    public async Task<string?> SaveAsync(
        RegisteredSystem value,
        bool creating,
        string actor,
        string ip,
        PortalAudienceSelection? audience = null)
    {
        var errors = Validate(value).ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        await using var db = database.Open();
        var before = await db.RegisteredSystems.SingleOrDefaultAsync(s => s.ClientId == value.ClientId);
        var beforeAudience = await db.PortalAudiences.SingleOrDefaultAsync(a => a.ClientId == value.ClientId);

        audience ??= PortalAudienceSelection.FromRow(beforeAudience);
        audience.Validate();

        if (creating)
        {
            await EnsureClientIdIsFreeAsync(db, value, before);
        }
        else
        {
            EnsureSameRevision(before, value);
        }

        // 純首頁連結只負責導向，不產生密鑰，也不建立可兌換的協定用戶端。
        var secret = CreateSecretIfNeeded(value, creating);

        object? createdApplication = null;
        try
        {
            // 新 OIDC 密鑰由 OpenIddict 驗證及雜湊；管理登記提交之前，
            // 授權與 Token 端點都會拒絕此用戶端登入。
            if (creating && value.Protocol == "oidc")
            {
                createdApplication = await applications.CreateAsync(Descriptor(value, secret));
            }

            await SaveInTransactionAsync(db, value, creating, secret, actor, ip, audience, before, beforeAudience);
            return secret;
        }
        catch
        {
            // 新增後續失敗時清理剛建立的 OIDC 用戶端；不刪除既有系統。
            if (createdApplication is not null)
            {
                await applications.DeleteAsync(createdApplication);
            }

            throw;
        }
    }

    /// <summary>
    /// 三個地方都要查：系統登記、Web API 用戶端與 OIDC 用戶端，任一個撞名都不能建立。
    /// </summary>
    private async Task EnsureClientIdIsFreeAsync(SsoDatabase db, RegisteredSystem value, RegisteredSystem? before)
    {
        const string message = "此系統識別碼已存在，請使用其他代碼。";

        if (before is not null)
        {
            throw new InvalidOperationException(message);
        }

        if (await db.TicketClients.AnyAsync(c => c.Id == value.ClientId))
        {
            throw new InvalidOperationException(message);
        }

        if (await applications.FindByClientIdAsync(value.ClientId) is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 比對管理表單載入時的版本；協定不允許改，改了代表表單不是從這筆資料開的。
    /// </summary>
    private static void EnsureSameRevision(RegisteredSystem? before, RegisteredSystem value)
    {
        const string message = "設定已被其他操作更新，請重新開啟編輯頁面。";

        if (before is null)
        {
            throw new InvalidOperationException(message);
        }

        if (before.Revision != value.Revision)
        {
            throw new InvalidOperationException(message);
        }

        if (before.Protocol != value.Protocol)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 只有會兌換憑證的協定需要密鑰；純首頁連結與 SAML 不需要。
    /// </summary>
    private static string? CreateSecretIfNeeded(RegisteredSystem value, bool creating)
    {
        if (!creating)
        {
            return null;
        }

        if (value.Protocol != "oidc" && value.Protocol != "ticket")
        {
            return null;
        }

        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// 系統設定、協定用戶端、可見條件與異動紀錄寫在同一個交易，避免只成功一半。
    /// </summary>
    private async Task SaveInTransactionAsync(
        SsoDatabase db,
        RegisteredSystem value,
        bool creating,
        string? secret,
        string actor,
        string ip,
        PortalAudienceSelection audience,
        RegisteredSystem? before,
        PortalAudience? beforeAudience)
    {
        await using var transaction = await db.BeginTransactionAsync();

        var catalog = identityCatalog is null
            ? await IdentityCatalog.LoadAsync(db)
            : await identityCatalog.LoadAsync();
        audience.ValidateCatalog(catalog, PortalAudienceSelection.FromRow(beforeAudience));

        // 與系統儲存在同一交易檢查分類，避免使用已被刪除的分類。
        if (!await db.PortalCategories.AnyAsync(c => c.Key == value.Category))
        {
            throw new InvalidOperationException("分類不存在或已被刪除，請重新選擇首頁分類。");
        }

        // 比對管理表單載入時的版本，避免另一位管理者的更新被舊表單覆蓋。
        var previousRevision = value.Revision;
        value.Revision = Guid.NewGuid().ToString("N");

        if (creating)
        {
            await db.InsertAsync(value);
        }
        else
        {
            await UpdateSystemAsync(db, value, previousRevision);
        }

        if (value.Protocol == "ticket")
        {
            await SaveTicketClientAsync(db, value, creating, secret);
        }
        else if (!creating && value.Protocol == "oidc")
        {
            await UpdateOidcApplicationAsync(db, value);
        }

        await db.InsertOrReplaceAsync(audience.ToRow(value.ClientId));
        await db.InsertAsync(BuildAudit(value, creating, actor, ip, audience, before, beforeAudience));
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 以載入表單時的版本當更新條件，另一位管理者先存檔時這裡會更新 0 筆。
    /// </summary>
    private static async Task UpdateSystemAsync(SsoDatabase db, RegisteredSystem value, string previousRevision)
    {
        var changed = await db.RegisteredSystems
            .Where(s => s.ClientId == value.ClientId && s.Revision == previousRevision)
            .Set(s => s.DisplayName, value.DisplayName)
            .Set(s => s.Description, value.Description)
            .Set(s => s.DisplayNameEn, value.DisplayNameEn.Trim())
            .Set(s => s.DescriptionEn, value.DescriptionEn.Trim())
            .Set(s => s.EntryUrl, value.EntryUrl)
            .Set(s => s.RedirectUri, value.RedirectUri)
            .Set(s => s.PostLogoutRedirectUri, value.PostLogoutRedirectUri)
            .Set(s => s.SamlEntityId, value.SamlEntityId)
            .Set(s => s.SamlSigningCertificate, value.SamlSigningCertificate)
            .Set(s => s.Enabled, value.Enabled)
            .Set(s => s.ShowOnPortal, value.ShowOnPortal)
            .Set(s => s.PublicStudent, value.PublicStudent)
            .Set(s => s.PublicEmployee, value.PublicEmployee)
            .Set(s => s.PublicAlumni, value.PublicAlumni)
            .Set(s => s.RequirePortalLogin, value.RequirePortalLogin)
            .Set(s => s.RequireLoginToView, value.RequireLoginToView)
            .Set(s => s.SortOrder, value.SortOrder)
            .Set(s => s.ClaimMask, value.ClaimMask)
            .Set(s => s.Category, value.Category)
            .Set(s => s.Keywords, value.Keywords)
            .Set(s => s.Revision, value.Revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw new InvalidOperationException("設定已被其他操作更新，請重新開啟編輯頁面。");
        }
    }

    /// <summary>
    /// Web API 單一登入的用戶端；新增時寫入密鑰雜湊，編輯時不換密鑰。
    /// </summary>
    private static async Task SaveTicketClientAsync(
        SsoDatabase db,
        RegisteredSystem value,
        bool creating,
        string? secret)
    {
        if (creating)
        {
            await db.InsertAsync(new TicketClient
            {
                Id = value.ClientId,
                DisplayName = value.DisplayName,
                SecretHash = LoginTicketService.Hash(secret!),
                RedirectUri = value.RedirectUri,
                Enabled = value.Enabled,
                IncludeStudentProfile = true
            });
            return;
        }

        var changed = await db.TicketClients
            .Where(c => c.Id == value.ClientId)
            .Set(c => c.DisplayName, value.DisplayName)
            .Set(c => c.RedirectUri, value.RedirectUri)
            .Set(c => c.Enabled, value.Enabled)
            .Set(c => c.IncludeStudentProfile, true)
            .UpdateAsync();

        if (changed != 1)
        {
            throw new InvalidOperationException("找不到原始Web API 單一登入串接設定。");
        }
    }

    /// <summary>
    /// 公開協定設定、版本與欄位提供規則在同一交易更新，
    /// 保留原有密鑰雜湊，不因一般編輯而更換密鑰。
    /// </summary>
    private static async Task UpdateOidcApplicationAsync(SsoDatabase db, RegisteredSystem value)
    {
        var redirectUris = JsonSerializer.Serialize(new[] { value.RedirectUri });
        var postLogoutUris = string.IsNullOrEmpty(value.PostLogoutRedirectUri)
            ? Array.Empty<string>()
            : [value.PostLogoutRedirectUri];

        var changed = await db.GetTable<OidcApplication>()
            .Where(c => c.ClientId == value.ClientId)
            .Set(c => c.DisplayName, value.DisplayName)
            .Set(c => c.RedirectUris, redirectUris)
            .Set(c => c.PostLogoutRedirectUris, JsonSerializer.Serialize(postLogoutUris))
            .Set(c => c.ConcurrencyToken, Guid.NewGuid().ToString())
            .UpdateAsync();

        if (changed != 1)
        {
            throw new InvalidOperationException("找不到原始 OIDC 串接設定。");
        }
    }

    /// <summary>
    /// 異動紀錄同時保存系統設定與可見條件的前後值，之後才查得出是誰改了什麼。
    /// </summary>
    private SystemConfigurationAudit BuildAudit(
        RegisteredSystem value,
        bool creating,
        string actor,
        string ip,
        PortalAudienceSelection audience,
        RegisteredSystem? before,
        PortalAudience? beforeAudience)
    {
        var beforeJson = before is null
            ? ""
            : JsonSerializer.Serialize(new
            {
                System = before,
                Audience = PortalAudienceSelection.FromRow(beforeAudience)
            });

        var afterJson = JsonSerializer.Serialize(new
        {
            System = value,
            Audience = audience
        });

        return new SystemConfigurationAudit
        {
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            ActorId = actor,
            ClientId = value.ClientId,
            RemoteIp = ip,
            Action = creating ? "create" : "update",
            BeforeJson = beforeJson,
            AfterJson = afterJson
        };
    }

    /// <summary>
    /// 建立只允許授權碼流程且要求 PKCE 的機密 OIDC 用戶端設定。
    /// </summary>
    private static OpenIddictApplicationDescriptor Descriptor(RegisteredSystem value, string? secret)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = value.ClientId,
            ClientSecret = secret,
            DisplayName = value.DisplayName,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            RedirectUris = { new Uri(value.RedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + OidcIdentityClaims.Scope
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange }
        };

        if (!string.IsNullOrEmpty(value.PostLogoutRedirectUri))
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(value.PostLogoutRedirectUri));
        }

        return descriptor;
    }
}
