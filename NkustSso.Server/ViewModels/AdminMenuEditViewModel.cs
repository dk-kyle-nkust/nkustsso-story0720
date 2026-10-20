// 後台選單編輯表單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed class AdminMenuEditViewModel
{
    public AdminMenu Menu { get; set; } = new();
    public bool Creating { get; set; }
}
