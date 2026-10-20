using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 與 140.133.78.225 的結構中繼資料核對；只用於授權後的唯讀查詢，不讀取密碼欄位。
[Table("s90_auth_usr", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapAccount
{
    [Column("usr_id")] public string Id { get; set; } = "";
    [Column("usr_idno"), Nullable] public string? IdNumber { get; set; }
    [Column("usr_name"), Nullable] public string? Name { get; set; }
    [Column("sts_id"), Nullable] public string? Status { get; set; }
    [Column("usr_email1"), Nullable] public string? Email { get; set; }
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}

[Table("s10_employee", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapEmployee
{
    [Column("emp_id")] public string Id { get; set; } = "";
    [Column("emp_idno"), Nullable] public string? IdNumber { get; set; }
    [Column("emp_name"), Nullable] public string? Name { get; set; }
    [Column("emp_email"), Nullable] public string? Email { get; set; }
    [Column("emp_untid"), Nullable] public string? Unit { get; set; }
    [Column("emp_status"), Nullable] public string? Status { get; set; }
    [Column("emp_bdate"), Nullable] public string? Birthday { get; set; }
    [Column("emp_wuntid")] public string WorkUnit { get; set; } = "";
    [Column("emp_titid")] public string Title { get; set; } = "";
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}

[Table("s30_student", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapStudent
{
    [Column("std_key")] public decimal Key { get; set; }
    [Column("std_id")] public string Id { get; set; } = "";
    [Column("std_idno"), Nullable] public string? IdNumber { get; set; }
    [Column("std_name"), Nullable] public string? Name { get; set; }
    [Column("std_email"), Nullable] public string? Email { get; set; }
    [Column("std_birthday"), Nullable] public string? Birthday { get; set; }
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}

[Table("s90_unit", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapUnit
{
    [Column("unt_id")] public string Id { get; set; } = "";
    [Column("dpt_id"), Nullable] public string? Department { get; set; }
    [Column("unt_name_abr"), Nullable] public string? Name { get; set; }
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}

[Table("s90_auth_log", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapTicket
{
    [Column("ser_no")] public decimal Serial { get; set; }
    [Column("usr_id")] public string Account { get; set; } = "";
    [Column("fnc_id")] public string Function { get; set; } = "";
    [Column("rand_num")] public int Random { get; set; }
    [Column("get_dt"), Nullable] public DateTime? IssuedAt { get; set; }
}

[Table("s90_titlecode", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapTitle
{
    [Column("tit_id")] public string Id { get; set; } = "";
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}

[Table("s90_dept", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class CampusSoapDepartment
{
    [Column("dpt_id")] public string Id { get; set; } = "";
    [Column("cmp_area"), Nullable] public int? Campus { get; set; }
}
