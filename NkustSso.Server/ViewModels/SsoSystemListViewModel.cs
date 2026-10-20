// SSO 系統清單、篩選條件與異動紀錄。
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 管理清單的查詢結果、篩選條件與可見身分顯示資料。
/// </summary>
public sealed record SsoSystemListViewModel(
    List<RegisteredSystem> Systems,
    List<SystemConfigurationAudit> Audits,
    string Protocol = "all",
    string Status = "all",
    string Visibility = "all")
{
    public string Section { get; init; } = "sso";

    public string Category { get; init; } = "";

    public List<PortalCategory> Categories { get; init; } = [];

    public Dictionary<string, PortalAudienceSelection> Audiences { get; init; } = new();

    public IdentityCatalogSnapshot IdentityCatalog { get; init; } = new([], []);
}
