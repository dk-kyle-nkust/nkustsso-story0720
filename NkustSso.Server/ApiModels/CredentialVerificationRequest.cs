// 呼叫系統送入的帳密驗證 JSON，不可記錄密碼。

namespace NkustSso.Server.ApiModels;

public sealed class CredentialVerificationRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
