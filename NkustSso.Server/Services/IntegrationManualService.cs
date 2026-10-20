// 從組件讀取固定手冊，避免直接公開實體資料夾或接受任意下載路徑。
using System.Text.Json;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Services;

public sealed class IntegrationManualService
{
    private const string Prefix = "IntegrationManuals.";
    public IntegrationManualListViewModel Catalog { get; }

    public IntegrationManualService()
    {
        using var stream = OpenResource("catalog.json");
        var data = JsonSerializer.Deserialize<ManualCatalog>(stream)
            ?? throw new InvalidOperationException("手冊目錄未正確打包。");
        if (data.Items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != data.Items.Length)
        {
            throw new InvalidOperationException("手冊代碼重複。");
        }
        Catalog = new(data.Version, Array.AsReadOnly(data.Items));
    }

    public IntegrationManualViewModel? Find(string id) => Catalog.Items.FirstOrDefault(i => i.Id == id);

    public Stream Open(IntegrationManualViewModel manual) => OpenResource(manual.FileName);

    public Stream OpenBundle() => OpenResource("bundle.zip");

    private static Stream OpenResource(string name) => typeof(IntegrationManualService).Assembly
        .GetManifestResourceStream(Prefix + name)
        ?? throw new InvalidOperationException("手冊檔案未正確打包，請重新產生文件並編譯。");

    private sealed record ManualCatalog(string Version, IntegrationManualViewModel[] Items);
}
