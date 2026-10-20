// 管理角色的權限、成員及生效期限表單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed class AdminRoleEditViewModel
{
    public AdminRole Role { get; set; } = new();

    public string[] Permissions { get; set; } = [];

    public string[] Members { get; set; } = [];

    public bool Creating { get; set; }

    public DateTime? StartsAtLocal { get; set; }

    public DateTime? ExpiresAtLocal { get; set; }

    public List<AdminAccountOption> Accounts { get; set; } = [];
}
