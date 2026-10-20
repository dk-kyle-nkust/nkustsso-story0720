using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 依 ForgotPassword 的帳號模型與實際查詢建立；只映射身分查詢需要的欄位。
// 帳號表已確認為 dbo.s90_auth_usr；字串長度仍待目標 MSSQL 結構確認。
[Table("s90_auth_usr", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class SourceAccountRecord
{
    [Column("usr_id", DataType = DataType.VarChar), NotNull]
    public string MemberId { get; set; } = "";

    [Column("usr_name", DataType = DataType.VarChar), Nullable]
    public string? Name { get; set; }

    [Column("sts_id", DataType = DataType.VarChar), Nullable]
    public string? StatusCode { get; set; }
}

[Table("s90_auth_status", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class SourceAccountStatusRecord
{
    [Column("sts_id", DataType = DataType.VarChar), NotNull]
    public string Code { get; set; } = "";

    [Column("sts_name", DataType = DataType.VarChar), Nullable]
    public string? Name { get; set; }
}

public sealed class SourceAccountProfile
{
    public string MemberId { get; set; } = "";
    public string? Name { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
}
