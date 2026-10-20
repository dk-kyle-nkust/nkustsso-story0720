namespace NkustSso.Server.ViewModels;

/// <summary>
/// 登入資訊的顯示資料；學號或員編標籤依實際帳號身分決定。
/// UnitName 只有教職員且來源單位對照唯一時才有值，其餘情況留空不顯示。
/// </summary>
public sealed record CurrentUserViewModel(
    string UserName,
    string DisplayName,
    string IdentityLabel,
    string AccountLabel,
    bool CanManageSystems,
    string UnitName = "");
