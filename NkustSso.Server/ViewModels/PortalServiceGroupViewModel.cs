// 專區服務的分類與卡片清單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed record PortalServiceGroupViewModel(PortalCategory Category, List<PortalServiceItemViewModel> Entries);
