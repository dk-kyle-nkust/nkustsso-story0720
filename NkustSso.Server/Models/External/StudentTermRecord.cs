using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 關聯依學務專案的就學紀錄模型；只讀取個人卡片所需欄位。
[Table("s30_sturec", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class StudentTermRecord
{
    [Column("std_key")] public decimal StudentKey { get; set; }
    [Column("yms_year")] public int Year { get; set; }
    [Column("yms_sms")] public int Semester { get; set; }
    [Column("cls_id"), Nullable] public string? ClassCode { get; set; }
    [Column("src_kind"), Nullable] public string? KindCode { get; set; }
    [Column("src_status"), Nullable] public string? StatusCode { get; set; }
}

[Table("s90_yms", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class AcademicTermRecord
{
    [Column("yms_year")] public decimal Year { get; set; }
    [Column("yms_smester")] public decimal Semester { get; set; }
    [Column("yms_mark"), Nullable] public string? CurrentMark { get; set; }
}

[Table("s90_degree", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class DegreeRecord
{
    [Column("dgr_id"), NotNull] public string Code { get; set; } = "";
    [Column("dgr_name"), Nullable] public string? Name { get; set; }
    [Column("dvs_id"), Nullable] public string? DivisionCode { get; set; }
}

[Table("s90_srcstatus", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class StudentStatusRecord
{
    [Column("srcstatus_id"), NotNull] public string Code { get; set; } = "";
    [Column("srcstatus_name"), Nullable] public string? Name { get; set; }
}
