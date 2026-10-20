using LinqToDB.Mapping;
using System.ComponentModel.DataAnnotations;

namespace NkustSso.Server.Models;

[Table("RegisteredSystems", IsColumnAttributeRequired = false)]
public sealed class RegisteredSystem
{
    [PrimaryKey] public string ClientId { get; set; } = "";
    public string Protocol { get; set; } = "oidc";
    public string DisplayName { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string DisplayNameEn { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Description { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string DescriptionEn { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string EntryUrl { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RedirectUri { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string PostLogoutRedirectUri { get; set; } = "";
    public bool Enabled { get; set; } = true;
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string SamlEntityId { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string SamlSigningCertificate { get; set; } = "";
    public bool ShowOnPortal { get; set; } = true;
    // 公開目錄與登入後的個人可見條件分開，避免公開內部入口或誤當作授權。
    public bool PublicStudent { get; set; }
    public bool PublicEmployee { get; set; }
    public bool PublicAlumni { get; set; }
    // 與「點選後需登入」分開：此設定連名稱與詳述都只對符合身分的登入者顯示。
    public bool RequireLoginToView { get; set; }
    // 外部連結可先要求本站登入，但不代表接收網站已串接 SSO。
    public bool RequirePortalLogin { get; set; }
    public int SortOrder { get; set; }
    public int ClaimMask { get; set; }
    // 入口分類與搜尋別名：系統數量多時，單靠名稱排序找不到東西。
    // 可見身分條件另存於 PortalAudiences，見 PortalVisibility。
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Category { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Keywords { get; set; } = "";
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("SystemConfigurationAudits", IsColumnAttributeRequired = false)]
public sealed class SystemConfigurationAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ActorId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Action { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    // Configuration only. Never contains client secrets, cookies or login tickets.
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
}
