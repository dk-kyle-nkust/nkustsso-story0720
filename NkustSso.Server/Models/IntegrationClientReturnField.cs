using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

// 每個呼叫系統、每個方法分別授權；只保存欄位代碼，不保存使用者個資。
[Table("IntegrationClientReturnFields", IsColumnAttributeRequired = false)]
public sealed class IntegrationClientReturnField
{
    [PrimaryKey(0)] public string ClientId { get; set; } = "";
    [PrimaryKey(1)] public string FieldKey { get; set; } = "";
}
