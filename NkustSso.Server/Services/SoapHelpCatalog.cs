// 提供舊 SOAP 方法說明與範例，標示已支援的方法。
using System.Text.Json;
using System.Xml.Linq;
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.Services;

// 說明目錄與可執行契約分開；未移植方法可供核對，但不加入服務 WSDL。
public static class SoapHelpCatalog
{
    public static IReadOnlyList<SoapHelpOperation> Operations { get; } = Load();
    private static IReadOnlyList<SoapHelpOperation> Load()
    {
        using var stream = typeof(SoapHelpCatalog).Assembly.GetManifestResourceStream(
            "NkustSso.Server.Services.SoapDefinitions.legacy-soap-operations.json")!;
        return JsonSerializer.Deserialize<List<SoapHelpOperation>>(stream)!
            .Select(o => o with
            {
                // 說明目錄含未移植的方法，依可執行清單標示是否已支援。
                Implemented = LegacySoapOperationCatalog.Available.Contains(o.Name)
            })
            .ToList()
            .AsReadOnly();
    }

    public static string RequestExample(SoapHelpOperation operation, bool soap12)
    {
        XNamespace env = soap12 ? LegacySoapContract.Soap12 : LegacySoapContract.Soap11;
        XNamespace ns = operation.Namespace;
        // 範例不填入真實帳密，敏感參數改以提示文字呈現。
        var parameters = operation.Parameters.Select(p => new XElement(
            ns + p.Name,
            p.Sensitive ? "請由呼叫端安全提供" : p.Description));

        var envelope = new XElement(
            env + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", env),
            new XElement(env + "Body", new XElement(ns + operation.Name, parameters)));

        return envelope.ToString();
    }
}
