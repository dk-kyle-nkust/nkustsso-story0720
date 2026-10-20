// 依資料庫中的角色與權限判斷後台功能存取。
using System.Security.Claims;
using LinqToDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed record AdminFeature(
    string Key,
    string Label,
    string Path,
    string Controller,
    bool RootOnly = false);

public sealed record AdminNavigationItem(
    string Id,
    string Label,
    string Group,
    string Target,
    string Icon,
    string Path);

public sealed record AdminAccessSnapshot(bool IsRoot, IReadOnlySet<string> Permissions)
{
    /// <summary>有查詢或維護任一種權限就算看得到這個功能。</summary>
    public bool Can(string key)
    {
        var feature = AdminAuthorizationService.Features.FirstOrDefault(f => f.Key == key);
        if (feature is null)
        {
            return false;
        }

        if (IsRoot)
        {
            return true;
        }

        if (feature.RootOnly)
        {
            return false;
        }

        return Permissions.Contains(key) || Permissions.Contains(key + ":read");
    }

    /// <summary>維護權限不含唯讀；新增、編輯與撤銷都要走這裡。</summary>
    public bool CanManage(string key)
    {
        var feature = AdminAuthorizationService.Features.FirstOrDefault(f => f.Key == key);
        if (feature is null)
        {
            return false;
        }

        if (IsRoot)
        {
            return true;
        }

        if (feature.RootOnly)
        {
            return false;
        }

        return Permissions.Contains(key);
    }

    public bool Any => IsRoot || Permissions.Count > 0;
}

// 選單僅提供導覽；權限依實際控制器判定，不接受表單指定任意 policy 或網址。
public sealed class AdminAuthorizationService(DatabaseFactory database, TimeProvider clock)
{
    /// <summary>唯讀權限的後綴。</summary>
    private const string ReadOnlySuffix = ":read";

    public static readonly IReadOnlyList<AdminFeature> Features = new AdminFeature[]
    {
        new("systems", "SSO 系統與首頁連結維護", "/admin/systems", "SsoSystemManagement"),
        new("integrations", "JSON／SOAP 串接維護", "/admin/systems/api?kind=new", "IntegrationClientManagement"),
        new("categories", "首頁分類維護", "/admin/categories", "PortalCategoryManagement"),
        new("identities", "身分分類維護", "/admin/identity-catalog", "IdentityCatalog"),
        new("sessions", "登入狀態查詢與撤銷", "/admin/sessions", "LoginSessionManagement"),
        new("tickets", "票券紀錄查詢", "/admin/tickets", "SsoTicketAudit"),
        new("progress", "開發進度", "/integration", "PortalHome"),
        new("manuals", "串接手冊下載", "/admin/manuals", "IntegrationManual"),
        new("menus", "後台選單維護", "/admin/menus", "AdminMenuManagement", true),
        new("access", "後台權限管理", "/admin/access", "AdminRoleManagement", true)
    };

    public static readonly IReadOnlyDictionary<string, string> Icons = new Dictionary<string, string>
    {
        ["grid"] = "方格",
        ["folder"] = "資料夾",
        ["people"] = "人員",
        ["shield"] = "盾牌",
        ["ticket"] = "票券",
        ["activity"] = "紀錄",
        ["home"] = "首頁"
    };

    /// <summary>最高管理者專屬的功能不能授權給一般角色。</summary>
    public static bool IsGrant(string permission)
    {
        return Features.Any(f => !f.RootOnly
            && (permission == f.Key || permission == f.Key + ReadOnlySuffix));
    }

    public static string? GrantLabel(string permission)
    {
        var feature = Features.FirstOrDefault(f => permission == f.Key
            || permission == f.Key + ReadOnlySuffix);
        if (feature is null)
        {
            return null;
        }

        var readOnly = permission.EndsWith(ReadOnlySuffix, StringComparison.Ordinal);
        return feature.Label + (readOnly ? "（僅查詢）" : "（查詢與維護）");
    }

    public async Task<AdminAccessSnapshot> LoadAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(id))
        {
            return new AdminAccessSnapshot(false, new HashSet<string>());
        }

        return await LoadAccountAsync(id);
    }

    public async Task<AdminAccessSnapshot> LoadAccountAsync(string id)
    {
        await using var db = database.Open();
        var user = await db.GetTable<Account>().SingleOrDefaultAsync(a => a.Id == id);

        if (user is null || !user.Enabled)
        {
            return new AdminAccessSnapshot(false, new HashSet<string>());
        }

        if (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow())
        {
            return new AdminAccessSnapshot(false, new HashSet<string>());
        }

        var root = await IsRootAsync(db, id);
        var permissions = await LoadPermissionsAsync(db, id);

        return new AdminAccessSnapshot(root, permissions.Where(IsGrant).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>最高管理者由 Identity 角色判定，不經後台角色表。</summary>
    private static async Task<bool> IsRootAsync(SsoDatabase db, string id)
    {
        var rootRole = LoginSessionAdministration.Role.ToUpperInvariant();

        return await (from member in db.GetTable<AccountRole>()
                      join role in db.GetTable<IdentityRole>() on member.RoleId equals role.Id
                      where member.UserId == id && role.NormalizedName == rootRole
                      select member).AnyAsync();
    }

    /// <summary>只取啟用中且在授權期間內的角色所帶的權限。</summary>
    private async Task<List<string>> LoadPermissionsAsync(SsoDatabase db, string id)
    {
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();

        return await (from member in db.GetTable<AdminRoleMember>()
                      join role in db.GetTable<AdminRole>() on member.RoleId equals role.Id
                      join grant in db.GetTable<AdminRolePermission>() on role.Id equals grant.RoleId
                      where member.AccountId == id
                          && role.Enabled
                          && (role.StartsAt == null || role.StartsAt <= now)
                          && (role.ExpiresAt == null || now < role.ExpiresAt)
                      select grant.Permission).Distinct().ToListAsync();
    }

    public async Task<bool> AuthorizeAsync(ClaimsPrincipal principal, HttpContext http)
    {
        var action = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var feature = Features.SingleOrDefault(f => f.Controller == action?.ControllerName);
        if (feature is null)
        {
            return false;
        }

        var access = await LoadAsync(principal);

        // 查詢只開放明確列出的唯讀動作；新增、編輯與撤銷確認頁也要求維護權限。
        var isReadMethod = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method);
        var isReadAction = action?.ActionName is "Index" or "Overview" or "Integration";
        // 僅此控制器的下載屬於查詢，不擴大其他功能的唯讀權限。
        isReadAction |= action?.ControllerName == "IntegrationManual" && action.ActionName == "Download";

        if (isReadMethod && isReadAction)
        {
            return access.Can(feature.Key);
        }

        return access.CanManage(feature.Key);
    }

    public async Task<List<AdminNavigationItem>> NavigationAsync(ClaimsPrincipal principal)
    {
        var access = await LoadAsync(principal);

        await using var db = database.Open();
        var menus = await db.GetTable<AdminMenu>()
            .Where(m => m.Enabled)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .ToListAsync();

        // 保留原串接管理的單一入口；只有 API 權限時直接進入可使用的 API 分頁。
        return menus
            .Where(m => access.Can(m.Target) || (m.Target == "systems" && access.Can("integrations")))
            .Select(m => ToNavigationItem(m, access))
            .ToList();
    }

    private static AdminNavigationItem ToNavigationItem(AdminMenu menu, AdminAccessSnapshot access)
    {
        // 沒有系統維護權限但有 API 權限時，這個入口要指到 API 分頁。
        var target = menu.Target == "systems" && !access.Can("systems") ? "integrations" : menu.Target;
        var path = Features.Single(f => f.Key == target).Path;

        return new AdminNavigationItem(menu.Id, menu.Label, menu.Group, menu.Target, menu.Icon, path);
    }

    public async Task<string?> LandingAsync(ClaimsPrincipal principal)
    {
        var menu = (await NavigationAsync(principal)).FirstOrDefault();
        if (menu is not null)
        {
            return menu.Path;
        }

        // 選單全被停用時，退回第一個有權限的功能。
        var access = await LoadAsync(principal);
        return Features.FirstOrDefault(f => access.Can(f.Key))?.Path;
    }
}
