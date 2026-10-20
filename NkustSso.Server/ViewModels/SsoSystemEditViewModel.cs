// SSO 系統及首頁連結的編輯表單。
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 系統編輯模型：欄位提供與首頁可見條件各自保存，不代表相同授權。
/// </summary>
public sealed class SsoSystemEditViewModel
{
    public RegisteredSystem System { get; set; } = new() { Category = PortalCategoryService.DefaultKey };

    public List<PortalCategory> Categories { get; set; } = [];

    public int[] AllowedClaims { get; set; } = [];

    public bool Creating { get; set; }

    public string AudienceJson { get; set; } = "";

    [ValidateNever]
    public IdentityCatalogSnapshot IdentityCatalog { get; set; } = new([], []);
}
