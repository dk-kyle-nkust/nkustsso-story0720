// 後台選單清單與異動紀錄。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed record AdminMenuListViewModel(List<AdminMenu> Items, List<AdminManagementAudit> Audits);
