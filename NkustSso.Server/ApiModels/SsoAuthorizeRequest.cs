// 接收系統啟動單一登入的查詢參數。
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace NkustSso.Server.ApiModels;

/// <summary>
/// 瀏覽器發起票券登入的查詢參數；state 供接收端關聯原始要求，CodeChallenge 為 PKCE 驗證值的雜湊。
/// </summary>
public sealed class SsoAuthorizeRequest
{
    [FromQuery(Name = "client_id"), Required, StringLength(100)] public string ClientId { get; set; } = "";
    [FromQuery(Name = "redirect_uri"), Required, StringLength(2000)] public string RedirectUri { get; set; } = "";
    [FromQuery(Name = "state"), Required, RegularExpression("^[A-Za-z0-9_-]{32,128}$")] public string State { get; set; } = "";
    [FromQuery(Name = "code_challenge"), Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public string CodeChallenge { get; set; } = "";
    [FromQuery(Name = "code_challenge_method"), Required] public string CodeChallengeMethod { get; set; } = "";
}
