// 讀取已登入帳號的顯示資料，不另外查詢校務資料庫。
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NkustSso.Server.Models;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Services;

public static class CurrentUserDisplayService
{
    public static async Task<CurrentUserViewModel?> LoadAsync(
        UserManager<Account> users,
        ClaimsPrincipal principal,
        AdminAuthorizationService? access = null)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var account = await users.GetUserAsync(principal);
        if (account is not { Enabled: true } || await users.IsLockedOutAsync(account))
        {
            return null;
        }

        var identityLabel = account.IdentityType switch
        {
            "student" => "學生",
            "employee" => "教職員",
            "teacher" => "教師",
            "staff" => "職員",
            "alumni" => "校友",
            _ => "使用者"
        };
        // 校務狀態與後台分類查詢使用相同來源；不把離職等狀態改寫成一般教職員。
        if (account.AuthenticationSource == CampusLoginService.SourceName
            && !string.IsNullOrWhiteSpace(account.SourceStatusName))
        {
            identityLabel = account.SourceStatusName.Trim();
        }
        var accountLabel = account.IdentityType switch
        {
            "student" => "學號",
            "employee" or "teacher" or "staff" => "員編",
            _ => "帳號"
        };
        // 沒有傳入權限服務時退回只看最高管理者角色。
        var canManageSystems = access is null
            ? await users.IsInRoleAsync(account, LoginSessionAdministration.Role)
            : (await access.LoadAsync(principal)).Any;

        return new CurrentUserViewModel(
            account.UserName ?? "",
            account.DisplayName.Trim(),
            identityLabel,
            accountLabel,
            canManageSystems,
            UnitName(account));
    }

    /// <summary>
    /// 只對教職員顯示所屬單位。學生的系所來自班級，不走這個欄位；
    /// 單位對照不唯一或來源沒給名稱時登入已存成空字串，這裡就不顯示。
    /// </summary>
    private static string UnitName(Account account)
    {
        if (account.AuthenticationSource != CampusLoginService.SourceName)
        {
            return "";
        }

        if (account.IdentityType is not ("employee" or "teacher" or "staff"))
        {
            return "";
        }

        return account.UnitName.Trim();
    }
}
