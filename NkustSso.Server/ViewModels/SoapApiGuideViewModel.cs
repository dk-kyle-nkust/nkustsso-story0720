// SOAP 方法說明頁與服務位址。
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.ViewModels;

public sealed record SoapApiGuideViewModel(SoapHelpOperation? Operation, string Address, bool Enabled);
