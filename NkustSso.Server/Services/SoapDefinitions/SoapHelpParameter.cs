// 舊 SOAP 方法參數名稱、用途與敏感資料標記。

namespace NkustSso.Server.Services.SoapDefinitions;

public sealed record SoapHelpParameter(string Name, string Description, bool Sensitive);
