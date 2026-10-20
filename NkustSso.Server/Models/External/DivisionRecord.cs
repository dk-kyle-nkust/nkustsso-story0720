using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 遠端既有資料表，只映射讀取欄位；不加入本地建表或遷移。
[Table("s90_division", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class DivisionRecord
{
    [Column("dvs_id", DataType = DataType.VarChar, Length = 1), NotNull]
    public string Code { get; set; } = "";

    [Column("dvs_name", DataType = DataType.VarChar, Length = 20), Nullable]
    public string? Name { get; set; }

    // 尚未確認代碼是否跨校／校區唯一，保留範圍資訊，不假設 Code 是主鍵。
    [Column("school_id", DataType = DataType.VarChar, Length = 10), Nullable]
    public string? SchoolId { get; set; }

    [Column("cmp_area"), Nullable]
    public int? CampusCode { get; set; }
}
