// 後台手冊清單資料；檔名僅對應內嵌資源，不作為使用者輸入的路徑。
namespace NkustSso.Server.ViewModels;

public sealed record IntegrationManualViewModel(
    string Id, string Title, string Description, string Category, string FileName, int Pages);
