using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("PortalAudiences", IsColumnAttributeRequired = false)]
public sealed class PortalAudience
{
    [PrimaryKey] public string ClientId { get; set; } = "";
    public string IdentityTypesJson { get; set; } = "[]";
    public string StudentStatusesJson { get; set; } = "[]";
    public string EducationLevelsJson { get; set; } = "[]";
    // 空字串代表尚未轉換的舊規則；不可把損壞的新版 JSON 當成不限。
    public string RulesJson { get; set; } = "";
}
