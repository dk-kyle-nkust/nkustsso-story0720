// 手冊專區顯示文件版本、分類與可下載清單。
namespace NkustSso.Server.ViewModels;

public sealed record IntegrationManualListViewModel(
    string Version, IReadOnlyList<IntegrationManualViewModel> Items);
