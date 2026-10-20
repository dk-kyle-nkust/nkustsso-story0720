namespace NkustSso.Server.Models;

/// <summary>首頁服務支援的登入方式；獨立帳密驗證 JSON／SOAP 用戶端由串接服務管理。</summary>
public static class SystemProtocols
{
    public static bool IsKnown(string? protocol) => protocol is "oidc" or "ticket" or "saml" or "link";
    public static string Label(string protocol) => protocol switch
    {
        "oidc" => "OIDC",
        "ticket" => "Web API 單一登入",
        "saml" => "SAML 2.0",
        "link" => "僅首頁連結（尚未串接 SSO）",
        _ => "未知方式"
    };
}
