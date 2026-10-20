// 身分專區服務目錄的畫面資料。

namespace NkustSso.Server.ViewModels;

public sealed record PortalDirectoryViewModel(string Area, List<PortalServiceGroupViewModel> Groups);
