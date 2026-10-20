// 依專區與身分讀取可顯示的服務選單。
using LinqToDB;
using NkustSso.Server.Models;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Services;

/// <summary>公開目錄只讀取明確發布的入口，不載入使用者、身分規則或串接憑證。</summary>
public sealed class PortalDirectoryService(
    DatabaseFactory database,
    PortalCategoryService categories,
    AccountIdentityService identities,
    IdentityCatalog identityCatalog)
{
    /// <summary>訪客看得到的入口：已發布、不需登入才看得到，且對應到這個專區。</summary>
    public static IQueryable<RegisteredSystem> Published(SsoDatabase db, string area)
    {
        return db.RegisteredSystems.Where(s => s.ShowOnPortal
            && !s.RequireLoginToView
            && (s.Protocol == "link" || s.Enabled)
            && ((area == "student" && s.PublicStudent)
                || (area == "employee" && s.PublicEmployee)
                || (area == "alumni" && s.PublicAlumni)));
    }

    /// <summary>沒有開放給任何身分，或明確要求登入才看得到的入口，都算私人入口。</summary>
    public static bool IsPrivate(RegisteredSystem system)
    {
        if (system.RequireLoginToView)
        {
            return true;
        }

        return !(system.PublicStudent || system.PublicEmployee || system.PublicAlumni);
    }

    /// <summary>私人入口的名稱不送至訪客；開啟入口時也重用同一份身分檢查。</summary>
    public async Task<List<RegisteredSystem>> VisibleAsync(string area, Account? account, string? id = null)
    {
        await using var db = database.Open();
        if (account is null)
        {
            return await Published(db, area)
                .Where(s => id == null || s.ClientId == id)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.ClientId)
                .ToListAsync();
        }

        var systems = await db.RegisteredSystems.Where(s => s.ShowOnPortal && (s.Protocol == "link" || s.Enabled) &&
            (id == null || s.ClientId == id) &&
            ((area == "student" && s.PublicStudent) || (area == "employee" && s.PublicEmployee) ||
             (area == "alumni" && s.PublicAlumni) || (!s.PublicStudent && !s.PublicEmployee && !s.PublicAlumni)))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.ClientId).ToListAsync();
        if (!systems.Any(IsPrivate))
        {
            return systems;
        }

        var profiles = await identities.LoadAsync(account);
        var options = await identityCatalog.LoadAsync();
        var rules = (await db.PortalAudiences.ToListAsync()).ToDictionary(a => a.ClientId);
        return systems.Where(s => !IsPrivate(s) ||
            PortalAudienceSelection.FromRow(rules.GetValueOrDefault(s.ClientId)).Matches(profiles, options)).ToList();
    }

    public async Task<PortalDirectoryViewModel> LoadAsync(string area, Account? account = null)
    {
        var entries = (await VisibleAsync(area, account)).Select(s => new PortalServiceItemViewModel(
            s.ClientId, s.DisplayName, s.DisplayNameEn, s.Description, s.DescriptionEn, s.Category,
            s.Keywords, s.Protocol != "link", s.Protocol != "link" || s.RequirePortalLogin || IsPrivate(s))).ToList();
        var groups = await categories.ListAsync();
        return new(area, groups.Select(c => new PortalServiceGroupViewModel(c,
            entries.Where(e => PortalCategoryService.Of(groups, e.Category).Key == c.Key).ToList()))
            .Where(g => g.Entries.Count != 0).ToList());
    }
}
