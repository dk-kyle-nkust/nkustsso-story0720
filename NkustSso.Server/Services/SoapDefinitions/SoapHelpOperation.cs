// 舊 SOAP 方法規格，供說明頁與協定處理共用。

namespace NkustSso.Server.Services.SoapDefinitions;

public sealed record SoapHelpOperation(string Name, string Title, string Summary, string Namespace, string Action,
    List<SoapHelpParameter> Parameters, string ReturnType, bool Implemented);
