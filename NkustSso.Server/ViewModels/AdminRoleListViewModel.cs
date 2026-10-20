// 管理角色、成員、權限及異動紀錄清單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed record AdminRoleListViewModel(
    List<AdminRole> Roles,
    List<AdminRoleMember> Members,
    List<AdminRolePermission> Permissions,
    List<AdminManagementAudit> Audits);
