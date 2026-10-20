// 相容既有表單格式的票券兌換請求。
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace NkustSso.Server.ApiModels;

/// <summary>
/// 接收系統後端兌換票券的表單；CodeVerifier 必須對應核發時的 CodeChallenge。
/// </summary>
public sealed class SsoTicketFormRedemptionRequest
{
    [FromForm(Name = "ticket"), Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public string Ticket { get; set; } = "";
    [FromForm(Name = "redirect_uri"), Required, StringLength(2000)] public string RedirectUri { get; set; } = "";
    [FromForm(Name = "code_verifier"), Required, RegularExpression("^[A-Za-z0-9._~-]{43,128}$")] public string CodeVerifier { get; set; } = "";
}
