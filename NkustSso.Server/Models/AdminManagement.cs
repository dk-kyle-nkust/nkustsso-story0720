using System.ComponentModel.DataAnnotations;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("AdminMenus", IsColumnAttributeRequired = false)]
public sealed class AdminMenu
{
    [PrimaryKey, Required, StringLength(50)] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Label { get; set; } = "";
    [Required, StringLength(50)] public string Group { get; set; } = "服務設定";
    // 必須對得上 AdminAuthorizationService.Features 的既有功能代碼。
    // 導覽與權限都依它判定，所以不能靠新增選單指定任意網址或繞過授權。
    [Required, StringLength(50)] public string Target { get; set; } = "systems";
    [Required, StringLength(30)] public string Icon { get; set; } = "grid";
    [Range(0, 10000)] public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
    // 更新時帶在 WHERE 條件裡，過期的編輯表單不會蓋掉別人後來的調整。
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("AdminRoles", IsColumnAttributeRequired = false)]
public sealed class AdminRole
{
    [PrimaryKey, Required, StringLength(50)] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(2000), DisplayFormat(ConvertEmptyStringToNull = false)] public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    // 授權期限，UTC Unix 毫秒；null 表示該端不限。
    // 開始時間含該時刻、結束時間不含，每次請求重新判定，不靠登入時的快照。
    public long? StartsAt { get; set; }
    public long? ExpiresAt { get; set; }
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("AdminRolePermissions", IsColumnAttributeRequired = false)]
public sealed class AdminRolePermission
{
    [PrimaryKey(0)] public string RoleId { get; set; } = "";
    // 功能代碼代表「查詢與維護」；加上 :read 後綴才是僅查詢。
    // 兩者同時存在時以維護為準，所以不能只靠字串前綴判斷權限大小。
    [PrimaryKey(1)] public string Permission { get; set; } = "";
}

[Table("AdminRoleMembers", IsColumnAttributeRequired = false)]
public sealed class AdminRoleMember
{
    [PrimaryKey(0)] public string RoleId { get; set; } = "";
    [PrimaryKey(1)] public string AccountId { get; set; } = "";
}

[Table("AdminManagementAudits", IsColumnAttributeRequired = false)]
public sealed class AdminManagementAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ActorId { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    // 選單與角色的異動混放同一張表，用 menu／role 區分，TargetId 才知道指向哪張表。
    public string Kind { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Action { get; set; } = "";
    // 變更前後的完整快照，與異動在同一交易寫入。新增時前者為空、刪除時後者為空。
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
}
