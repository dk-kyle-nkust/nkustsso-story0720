using System.ComponentModel.DataAnnotations;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

/// <summary>首頁業務分類；代碼建立後固定，名稱、顏色與排序可由管理者維護。</summary>
[Table("PortalCategories", IsColumnAttributeRequired = false)]
public sealed class PortalCategory
{
    [PrimaryKey, Required, RegularExpression("^[a-z0-9][a-z0-9_-]{0,49}$")]
    public string Key { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(100), DisplayFormat(ConvertEmptyStringToNull = false)] public string NameEn { get; set; } = "";
    [Required] public string Accent { get; set; } = "slate";
    [Range(0, 10000)] public int SortOrder { get; set; }
    [Required] public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

/// <summary>版本化資料遷移標記，避免重啟時再次套用初始設定。</summary>
[Table("SchemaMigrations", IsColumnAttributeRequired = false)]
public sealed class SchemaMigration
{
    [PrimaryKey] public string Id { get; set; } = "";
}

[Table("PortalCategoryAudits", IsColumnAttributeRequired = false)]
public sealed class PortalCategoryAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string CategoryKey { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    public string Action { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
}
