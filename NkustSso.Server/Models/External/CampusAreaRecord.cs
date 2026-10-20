using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 僅讀取校區分類所需欄位；來源未提供主鍵資訊，不自行宣告主鍵或推定校區合併關係。
[Table("s90_cmparea", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusAreaRecord
{
    [Column("cmp_area", DataType = DataType.Int32), NotNull]
    public int Code { get; set; }

    [Column("cmp_name_full", DataType = DataType.VarChar, Length = 45), Nullable]
    public string? Name { get; set; }

    [Column("cmp_name_abr", DataType = DataType.VarChar, Length = 24), Nullable]
    public string? ShortName { get; set; }
}
