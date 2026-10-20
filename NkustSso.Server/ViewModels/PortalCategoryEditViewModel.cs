// 服務分類編輯表單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed class PortalCategoryEditViewModel
{
    public PortalCategory Category { get; set; } = new();
    public bool Creating { get; set; }
}
