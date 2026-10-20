// 串接密鑰僅顯示一次的結果頁資料，不可記錄密鑰。

namespace NkustSso.Server.ViewModels;

public sealed record IntegrationClientSecretViewModel(string ClientId, string Secret, string Kind);
