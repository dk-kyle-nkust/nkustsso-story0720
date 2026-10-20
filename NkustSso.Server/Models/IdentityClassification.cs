using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("IdentityDimensions", IsColumnAttributeRequired = false)]
public sealed class IdentityDimension
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Family { get; set; } = "any";
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("IdentityOptions", IsColumnAttributeRequired = false)]
public sealed class IdentityOption
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Dimension { get; set; } = "identity_type";
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    // 只有身分類別使用 Family；其他條件由所屬維度決定適用對象。
    public string Family { get; set; } = "other";
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}

[Table("IdentityCatalogAudits", IsColumnAttributeRequired = false)]
public sealed class IdentityCatalogAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ActorId { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    public string Target { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
}

// 已同步過的帳號即使沒有任何有效身分，也不能回退到舊的單一身分而重新開放入口。
[Table("AccountIdentitySets", IsColumnAttributeRequired = false)]
public sealed class AccountIdentitySet
{
    [PrimaryKey] public string AccountId { get; set; } = "";
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
    public long UpdatedAt { get; set; }
    public string Source { get; set; } = "";
}

[Table("AccountIdentities", IsColumnAttributeRequired = false)]
public sealed class AccountIdentity
{
    [PrimaryKey(0)] public string AccountId { get; set; } = "";
    [PrimaryKey(1)] public string Id { get; set; } = "";
    public string IdentityType { get; set; } = "";
    public string AttributesJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public long? ValidFrom { get; set; }
    public long? ValidUntil { get; set; }
}
