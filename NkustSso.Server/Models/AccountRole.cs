using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("AspNetUserRoles", IsColumnAttributeRequired = false)]
public sealed class AccountRole
{
    [PrimaryKey(0)] public string UserId { get; set; } = "";
    [PrimaryKey(1)] public string RoleId { get; set; } = "";
}
