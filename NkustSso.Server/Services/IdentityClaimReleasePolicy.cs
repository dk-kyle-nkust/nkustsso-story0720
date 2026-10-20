// 依系統授權限制對外提供的身分欄位。
using System.Security.Claims;
using NkustSso.Server.ApiModels;
using OpenIddict.Abstractions;

namespace NkustSso.Server.Services;

/// <summary>
/// 各登入協定共用的身分資料白名單；只限制對外欄位，不判斷首頁可見性或下游功能權限。
/// </summary>
public static class IdentityClaimReleasePolicy
{
    public sealed record Field(int Bit, string Type, string Label);
    public static IReadOnlyList<Field> Fields { get; } = new Field[]
    {
        new(1, "preferred_username", "校園帳號"), new(2, "name", "使用者姓名"),
        new(4, "identity_type", "校園身分類型"),
        new(8, "class_code", "班級代碼"), new(16, "class_name", "班級名稱"),
        new(32, "student_status_code", "學籍狀態代碼"),
        new(64, "student_status_name", "學籍狀態名稱"),
        new(128, "education_level_code", "學制代碼"),
        new(256, "education_level_name", "學制名稱")
    };
    public const int All = 511;
    /// <summary>
    /// 以位元遮罩判斷管理者是否允許提供指定欄位。
    /// </summary>
    public static bool Allows(int mask, int bit) => (mask & bit) != 0;

    /// <summary>
    /// 直接移除 OIDC principal 中未允許的可選聲明，保留 sub 等協定必要資訊。
    /// </summary>
    public static void Filter(ClaimsPrincipal principal, int mask)
    {
        foreach (var field in Fields.Where(f => !Allows(mask, f.Bit)))
        {
            principal.SetClaims(field.Type, System.Collections.Immutable.ImmutableArray<string>.Empty);
        }
    }

    /// <summary>
    /// 產生經過過濾的票券回應；未允許欄位設為 null，由 JSON 契約省略輸出。
    /// </summary>
    public static SsoIdentityResponse Filter(SsoIdentityResponse value, int mask) => value with
    {
        UserName = Allows(mask, 1) ? value.UserName : null,
        DisplayName = Allows(mask, 2) ? value.DisplayName : null,
        IdentityType = Allows(mask, 4) ? value.IdentityType : null,
        ClassCode = Allows(mask, 8) ? value.ClassCode : null,
        ClassName = Allows(mask, 16) ? value.ClassName : null,
        StudentStatusCode = Allows(mask, 32) ? value.StudentStatusCode : null,
        StudentStatusName = Allows(mask, 64) ? value.StudentStatusName : null,
        EducationLevelCode = Allows(mask, 128) ? value.EducationLevelCode : null,
        EducationLevelName = Allows(mask, 256) ? value.EducationLevelName : null
    };
}
