using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 遠端單位名稱對照；上下層關係尚待確認，不自行推定父單位欄位。
[Table("s90_unit", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class UnitRecord
{
    [Column("unt_id", DataType = DataType.VarChar, Length = 4), NotNull]
    public string Code { get; set; } = "";

    [Column("unt_name_full", DataType = DataType.VarChar, Length = 45), Nullable]
    public string? Name { get; set; }

    [Column("unt_name_abr", DataType = DataType.VarChar, Length = 24), Nullable]
    public string? ShortName { get; set; }

    [Column("cmp_area"), Nullable]
    public int? CampusCode { get; set; }

    [Column("unt_sort_seq"), Nullable]
    public int? SourceSortOrder { get; set; }
}
