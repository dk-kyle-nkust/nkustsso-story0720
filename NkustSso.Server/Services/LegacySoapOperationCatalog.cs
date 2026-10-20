// 列出可執行的舊 SOAP 方法及必要驗證規則。
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.Services;

// 同一控制器依此清單分派、產生 WSDL 與驗證服務授權；未完成資料對應者不冒充可用。
public static class LegacySoapOperationCatalog
{
    public const string DocumentPayment = "NkustSSO_DocumentApplyPaymentSystemSearch";
    public static readonly IReadOnlySet<string> Available = new HashSet<string>(StringComparer.Ordinal)
    {
        "KuasSSO_Certify2", "KuasSSO_Certify1", "KuasSSO_SchCardSearch",
        "Get_Traffic_Pub", "Get_Traffic_Teach", "Get_Traffic_Teach_Temp",
        "Get_Traffic_Stud", "Get_Traffic_Stud_Temp", DocumentPayment
    };
    /// <summary>帳密驗證沿用共用的服務代碼，其餘方法各自一個 soap. 前綴的代碼。</summary>
    public static string Permission(string operation)
    {
        if (operation == LegacySoapContract.Operation)
        {
            return IntegrationPermissions.VerifyAccount;
        }

        return "soap." + operation;
    }

    /// <summary>除了這兩個驗證方法以外，都要帶原系統的呼叫帳密。</summary>
    public static bool RequiresCaller(string operation)
    {
        return operation != "KuasSSO_Certify1" && operation != "KuasSSO_Certify2";
    }

    /// <summary>回傳 DataSet 的方法沿用 ASMX 的內嵌 XSD 與 DiffGram 格式。</summary>
    public static bool ReturnsDataSet(string operation)
    {
        if (operation == DocumentPayment)
        {
            return true;
        }

        return operation.StartsWith("Get_Traffic_", StringComparison.Ordinal);
    }
}

public sealed record LegacySoapRequest(
    SoapHelpOperation Operation,
    IReadOnlyDictionary<string, string> Parameters)
{
    public string Get(string name)
    {
        return Parameters.GetValueOrDefault(name, "");
    }
}
