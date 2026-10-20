using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

// 首次成功登入前也限制嘗試次數，不需預先複製來源帳號或密碼。
[Table("CampusLoginAttempts", IsColumnAttributeRequired = false)]
public sealed class CampusLoginAttempt
{
    [PrimaryKey] public string NormalizedName { get; set; } = "";
    public int FailedCount { get; set; }
    public long LockedUntil { get; set; }
    public long UpdatedAt { get; set; }
}
