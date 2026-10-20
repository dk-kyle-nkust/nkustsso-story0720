// 接收系統以 JSON 兌換票券的請求。
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NkustSso.Server.ApiModels;

/// <summary>Web API 單一登入的 JSON 票券兌換契約，欄位名稱與原有票券表單一致。</summary>
public sealed class SsoTicketRedemptionRequest
{
    [JsonPropertyName("ticket"), Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public string Ticket { get; set; } = "";
    [JsonPropertyName("redirect_uri"), Required, StringLength(2000)] public string RedirectUri { get; set; } = "";
    [JsonPropertyName("code_verifier"), Required, RegularExpression("^[A-Za-z0-9._~-]{43,128}$")] public string CodeVerifier { get; set; } = "";
}
