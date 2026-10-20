// 終止中央登入的確認表單。
using System.ComponentModel.DataAnnotations;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 撤銷操作表單；帳號、Session 歸屬及有效筆數仍會由後端重新查詢。
/// </summary>
public sealed class RevokeLoginSessionViewModel
{
    [Required, StringLength(128)] public string AccountId { get; set; } = "";
    [StringLength(64)] public string? SessionId { get; set; }
    [Required(ErrorMessage = "請填寫終止原因。"), StringLength(200, ErrorMessage = "原因最多 200 字。")]
    public string Reason { get; set; } = "";
    public string UserName { get; set; } = "";
    public int ActiveCount { get; set; }
}
