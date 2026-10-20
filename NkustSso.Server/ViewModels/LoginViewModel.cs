// 中央登入表單，保留帳密驗證及返回網址的欄位規則。
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 中央登入表單；ReturnUrl 經控制器檢查為本站網址後才使用。
/// </summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "請輸入帳號")]
    [StringLength(100)]
    public string UserName { get; set; } = "";
    [Required(ErrorMessage = "請輸入密碼")]
    [StringLength(256)]
    [DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    public string Password { get; set; } = "";
    public string ReturnUrl { get; set; } = "/";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Area { get; set; } = "";
    [BindNever] public bool CampusAuthentication { get; set; }
    [BindNever] public bool DevelopmentAllowAnyPassword { get; set; }
}
