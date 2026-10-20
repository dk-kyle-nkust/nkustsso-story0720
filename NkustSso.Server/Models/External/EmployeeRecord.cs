using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 表名與員編查詢來自 ForgotPassword，型態依提供的人事欄位清單。
// 不映射原表的身分證主鍵或密碼；本模型僅供唯讀查詢，不參與本地建表。
[Table("s10_employee", IsColumnAttributeRequired = true)]
public sealed class EmployeeRecord
{
    [Column("emp_id", DataType = DataType.VarChar, Length = 6), NotNull]
    public string EmployeeId { get; set; } = "";

    [Column("emp_name", DataType = DataType.VarChar, Length = 15), Nullable]
    public string? Name { get; set; }

    [Column("emp_untid", DataType = DataType.VarChar, Length = 4), NotNull]
    public string UnitCode { get; set; } = "";

    [Column("emp_wuntid", DataType = DataType.VarChar, Length = 4), NotNull]
    public string WorkUnitCode { get; set; } = "";

    [Column("emp_titid", DataType = DataType.VarChar, Length = 6), NotNull]
    public string TitleCode { get; set; } = "";

    [Column("emp_dvsid", DataType = DataType.VarChar, Length = 1), Nullable]
    public string? DivisionCode { get; set; }

    [Column("emp_status", DataType = DataType.VarChar, Length = 2), Nullable]
    public string? EmploymentStatusCode { get; set; }

    [Column("emp_leavedate", DataType = DataType.VarChar, Length = 7), Nullable]
    public string? LeaveDate { get; set; }

    [Column("cmp_area"), Nullable]
    public int? CampusCode { get; set; }
}
