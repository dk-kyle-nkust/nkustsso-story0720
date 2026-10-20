// SAML 回應自動送出表單，保留協定欄位名稱。

namespace NkustSso.Server.ViewModels;

public sealed record SamlResponseFormViewModel(string Destination, string SamlResponse, string RelayState);
