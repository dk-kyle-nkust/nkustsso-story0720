// 依 OIDC scope 建立帳號的身分聲明。
using System.Security.Claims;
using NkustSso.Server.Models;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace NkustSso.Server.Services;

/// <summary>
/// 將帳號資料轉為 OIDC 身分聲明；先依 scope 建立，再由 IdentityClaimReleasePolicy 套用每個系統的欄位白名單。
/// </summary>
public static class OidcIdentityClaims
{
    public const string Scope = "student_profile";

    /// <summary>
    /// 使用固定帳號 ID 作為 sub，依要求的 scope 建立身分欄位並指定可放入的 Token。
    /// </summary>
    public static ClaimsPrincipal Create(Account user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity("OIDC", Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id);
        identity.SetClaim(Claims.Name, user.DisplayName);
        identity.SetClaim(Claims.PreferredUsername, user.UserName);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        if (principal.HasScope(Scope))
        {
            identity.SetClaim("identity_type", user.IdentityType);
            identity.SetClaim("class_code", user.ClassCode);
            identity.SetClaim("class_name", user.ClassName);
            identity.SetClaim("student_status_code", user.StudentStatusCode);
            identity.SetClaim("student_status_name", user.StudentStatusName);
            identity.SetClaim("education_level_code", user.EducationLevelCode);
            identity.SetClaim("education_level_name", user.EducationLevelName);
        }
        // 明確指定哪些聲明可進入 ID Token；沒有指定目的地的內部資料不自動對外提供。
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Name or Claims.PreferredUsername when principal.HasScope(Scopes.Profile)
                => [Destinations.IdentityToken],
            "identity_type" or "class_code" or "class_name" or "student_status_code" or
            "student_status_name" or "education_level_code" or "education_level_name"
                when principal.HasScope(Scope) => [Destinations.IdentityToken],
            _ => []
        });
        return principal;
    }
}
