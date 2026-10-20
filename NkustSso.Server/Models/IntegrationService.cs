using System.ComponentModel.DataAnnotations;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

// 與首頁入口分開管理：取得帳密驗證資格不代表取得任何校務系統權限。
[Table("IntegrationClients", IsColumnAttributeRequired = false)]
public sealed class IntegrationClient
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ConnectionKind { get; set; } = "existing";
    public string Purpose { get; set; } = "";
    public string OwningUnit { get; set; } = "";
    public string SourceSystem { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ImportBatchId { get; set; } = "";
    public long? ImportedAt { get; set; }
    public string Protocol { get; set; } = "soap";
    public bool Enabled { get; set; }
    public string AllowedIps { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SecretHash { get; set; } = "";
    public string CallerName { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string CallerPasswordHash { get; set; } = "";
    public string AllowedFunctionIds { get; set; } = "";
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

// 不記錄使用者帳號、密碼、呼叫密鑰或完整請求／回應。
[Table("IntegrationAudits", IsColumnAttributeRequired = false)]
public sealed class IntegrationAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ClientId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class IntegrationClientInput
{
    [Required, RegularExpression("^[a-z0-9][a-z0-9_-]{0,99}$")]
    public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, RegularExpression("^(existing|new)$", ErrorMessage = "請選擇有效的串接分類。")]
    public string ConnectionKind { get; set; } = "existing";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(1000)] public string Purpose { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100)] public string OwningUnit { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100)] public string SourceSystem { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100)] public string SourceId { get; set; } = "";
    public List<string> ServiceIds { get; set; } = [];
    public List<string> ReturnFieldKeys { get; set; } = [];
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100)] public string CallerName { get; set; } = "";
    [StringLength(256), System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)] public string? CallerPassword { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(2000)] public string AllowedFunctionIds { get; set; } = "";
    [Required, RegularExpression("^(soap|json)$")] public string Protocol { get; set; } = "soap";
    public bool Enabled { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(2000)] public string AllowedIps { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100, ErrorMessage = "申請人最多 100 字。")]
    public string ApplicantName { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100, ErrorMessage = "承辦人最多 100 字。")]
    public string ContactName { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(100, ErrorMessage = "聯絡電話最多 100 字。")]
    public string ContactPhone { get; set; } = "";
    [StringLength(254), EmailAddress(ErrorMessage = "請填寫有效的電子郵件地址。")]
    public string? ContactEmail { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(20000, ErrorMessage = "詳細備註最多 20,000 字。")]
    public string Notes { get; set; } = "";
    [Required, StringLength(32)] public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("IntegrationCapabilities", IsColumnAttributeRequired = false)]
public sealed class IntegrationCapability
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

// 一個呼叫端可使用多項服務；此關聯不代表接收系統內的使用者權限。
[Table("IntegrationClientServices", IsColumnAttributeRequired = false)]
public sealed class IntegrationClientService
{
    [PrimaryKey(0)] public string ClientId { get; set; } = "";
    [PrimaryKey(1)] public string ServiceId { get; set; } = "";
}

public sealed class IntegrationServiceOptions
{
    public bool SoapEnabled { get; set; }
    public bool JsonEnabled { get; set; }
    // 保留舊設定繫結相容性；執行中的 SOAP／JSON 固定使用 CampusSoapAccountSource。
    public string AuthenticationSource { get; set; } = "campus";
    public string LdapHost { get; set; } = "";
    public int LdapPort { get; set; } = 636;
    public string LdapBaseDn { get; set; } = "";
    public string LdapDomain { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}
