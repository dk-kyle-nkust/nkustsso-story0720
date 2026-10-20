using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 僅映射登入者資料所需欄位；來源的身分證、聯絡地址、銀行及密碼欄位不讀取。
[Table("s30_student", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class StudentRecord
{
    // 來源宣告為 decimal(18,0)，維持 decimal；主鍵與跨來源識別規則另待確認。
    [Column("std_key", DataType = DataType.Decimal, Precision = 18, Scale = 0), NotNull]
    public decimal StudentKey { get; set; }

    [Column("std_id", DataType = DataType.VarChar, Length = 10), NotNull]
    public string StudentId { get; set; } = "";

    [Column("std_name", DataType = DataType.VarChar, Length = 45), Nullable]
    public string? Name { get; set; }

    [Column("cls_id", DataType = DataType.VarChar, Length = 8), Nullable]
    public string? ClassCode { get; set; }

    [Column("cmp_area"), Nullable]
    public int? CampusCode { get; set; }
}
