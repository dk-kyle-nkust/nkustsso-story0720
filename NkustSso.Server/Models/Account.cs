using Microsoft.AspNetCore.Identity;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("AspNetUsers", IsColumnAttributeRequired = false)]
public sealed class Account : IdentityUser
{
    public string AuthenticationSource { get; set; } = "";
    public string SourceAccountId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string IdentityType { get; set; } = "student";
    // 登入時保存校務原始狀態，供畫面顯示；不作為本地角色或學籍狀態。
    public string SourceStatusCode { get; set; } = "";
    public string SourceStatusName { get; set; } = "";
    // 教職員所屬單位（來源 emp_untid）；學生的系所來自班級，不填這裡。
    public string UnitCode { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string ClassCode { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string StudentStatusCode { get; set; } = "";
    public string StudentStatusName { get; set; } = "";
    public string EducationLevelCode { get; set; } = "";
    public string EducationLevelName { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

[Table("LoginEvents", IsColumnAttributeRequired = false)]
public sealed class LoginEvent
{
    [PrimaryKey, Identity]
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AccountId { get; set; }
    public string Outcome { get; set; } = "";
}

