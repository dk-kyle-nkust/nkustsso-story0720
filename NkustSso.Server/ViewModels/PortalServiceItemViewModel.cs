// 專區服務卡片資料，不包含串接密鑰。

namespace NkustSso.Server.ViewModels;

public sealed record PortalServiceItemViewModel(string Id, string Name, string NameEn, string Description, string DescriptionEn, string Category, string Keywords, bool Sso, bool RequiresLogin);
