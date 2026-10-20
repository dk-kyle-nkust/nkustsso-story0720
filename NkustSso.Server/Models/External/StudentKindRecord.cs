using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 學生狀況對照，保留來源字串代碼；不將復學／轉學類別當作當前學籍狀態。
[Table("s90_srckind", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class StudentKindRecord
{
    [Column("srckind_id", DataType = DataType.VarChar, Length = 1), NotNull]
    public string Code { get; set; } = "";

    [Column("srckind_name", DataType = DataType.VarChar, Length = 60), Nullable]
    public string? Name { get; set; }

    // 校區是否參與複合鍵尚待確認，重複代碼不任取第一筆。
    [Column("cmp_area", DataType = DataType.Int32), Nullable]
    public int? CampusCode { get; set; }
}
