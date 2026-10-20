// 角色編輯頁可選取的帳號資料。

namespace NkustSso.Server.ViewModels;

public sealed record AdminAccountOption(string Id, string UserName, string DisplayName, bool Enabled);
