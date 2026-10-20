using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

// 僅保留短效流程參照與請求資訊；不儲存已簽章的身分回應或密碼。
[Table("SamlLoginRequests", IsColumnAttributeRequired = false)]
public sealed class SamlLoginRequest
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Revision { get; set; } = "";
    public string CorrelationHash { get; set; } = "";
    public string RelayState { get; set; } = "";
    public bool IsPassive { get; set; }
    public long ExpiresAt { get; set; }
    public long? ConsumedAt { get; set; }
}

[Table("SamlAudits", IsColumnAttributeRequired = false)]
public sealed class SamlAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ClientId { get; set; } = "";
    public string? AccountId { get; set; }
    public string Outcome { get; set; } = "";
    public string RemoteIp { get; set; } = "";
}
