using System.Text.Json.Serialization;
using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

// 與一般帳號顯示模型分開，避免列表、身分查詢或序列化帶出密碼。
[Table("s90_auth_usr", Schema = "dbo", IsColumnAttributeRequired = true)]
internal sealed class CampusCredentialRecord
{
    [Column("usr_id", DataType = DataType.VarChar), NotNull]
    public string MemberId { get; set; } = "";
    [Column("usr_mdpwd", DataType = DataType.VarChar), Nullable, JsonIgnore]
    public string? EncodedPassword { get; set; }
    [Column("sts_id", DataType = DataType.VarChar), Nullable]
    public string? StatusCode { get; set; }
}
