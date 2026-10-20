// 解析與序列化舊 SOAP 訊息，保留相容格式。
using System.Data;
using System.Xml;
using System.Xml.Linq;
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.Services;

/// <summary>共用 SOAP 安全解析與序列化；只有已完成的處理器才加入 WSDL。</summary>
public static class LegacySoapContract
{
    public const string Path = "/KuasSSOWebService.asmx";
    public const string Namespace = "http://tempuri.org/";
    public const string Operation = "KuasSSO_Certify2";
    public const string Action = Namespace + Operation;
    public const string Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string Soap12 = "http://www.w3.org/2003/05/soap-envelope";

    /// <summary>XML 結構描述的命名空間，回應裡的型別標記會用到。</summary>
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";

    /// <summary>單一參數值的長度上限。</summary>
    private const int MaxParameterLength = 256;

    public static async Task<(string? UserName, string? Password)> ReadAsync(
        Stream body,
        bool soap12,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(body, soap12, cancellationToken);
        if (request.Operation.Name != Operation)
        {
            throw new XmlException("不是帳密驗證方法。");
        }

        return (
            request.Parameters.GetValueOrDefault("usr_id"),
            request.Parameters.GetValueOrDefault("usr_pwd"));
    }

    public static async Task<LegacySoapRequest> ReadRequestAsync(
        Stream body,
        bool soap12,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,

            // 禁用 DTD 與外部解析，避免 XXE 與實體展開攻擊。
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16_384,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var reader = XmlReader.Create(body, settings);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);

        var envelope = ReadEnvelope(document, soap12);
        var operation = ReadOperationElement(envelope, soap12);
        var method = ResolveMethod(operation);
        var parameters = ReadParameters(operation, method);

        return new LegacySoapRequest(method, parameters);
    }

    /// <summary>
    /// Envelope 只允許一個 Body 與最多一個空的 Header，不接受任何附加區塊。
    /// </summary>
    private static XElement ReadEnvelope(XDocument document, bool soap12)
    {
        XNamespace env = soap12 ? Soap12 : Soap11;
        var envelope = document.Root;

        if (envelope?.Name != env + "Envelope")
        {
            throw new XmlException("不支援的 SOAP Envelope。");
        }

        if (envelope.Elements().Any(e => e.Name != env + "Body" && e.Name != env + "Header"))
        {
            throw new XmlException("不支援的 SOAP Envelope。");
        }

        if (envelope.Elements(env + "Body").Count() != 1)
        {
            throw new XmlException("不支援的 SOAP Envelope。");
        }

        if (envelope.Elements(env + "Header").Count() > 1)
        {
            throw new XmlException("不支援的 SOAP Envelope。");
        }

        if (envelope.Element(env + "Header")?.Elements().Any() == true)
        {
            throw new XmlException("不支援的 SOAP Envelope。");
        }

        return envelope;
    }

    private static XElement ReadOperationElement(XElement envelope, bool soap12)
    {
        XNamespace env = soap12 ? Soap12 : Soap11;
        var operations = envelope.Element(env + "Body")!.Elements().ToArray();

        if (operations.Length != 1)
        {
            throw new XmlException("尚未提供此 SOAP 方法。");
        }

        return operations[0];
    }

    private static SoapHelpOperation ResolveMethod(XElement operation)
    {
        var method = SoapHelpCatalog.Operations.SingleOrDefault(
            o => o.Name == operation.Name.LocalName && o.Namespace == operation.Name.NamespaceName);

        if (method is null || !LegacySoapOperationCatalog.Available.Contains(method.Name))
        {
            throw new XmlException("尚未提供此 SOAP 方法。");
        }

        return method;
    }

    /// <summary>
    /// 參數必須是已知名稱、無重複、無巢狀，而且順序與合約一致，與舊服務的行為相同。
    /// </summary>
    private static Dictionary<string, string> ReadParameters(XElement operation, SoapHelpOperation method)
    {
        var parameters = operation.Elements().ToArray();
        var names = method.Parameters.Select(p => p.Name).ToArray();
        var indices = parameters.Select(p => Array.IndexOf(names, p.Name.LocalName)).ToArray();

        var malformed = parameters.Any(e => e.Name.NamespaceName != method.Namespace
            || e.HasElements
            || e.Value.Length > MaxParameterLength);
        if (malformed)
        {
            throw new XmlException("SOAP 參數格式不正確。");
        }

        if (indices.Any(i => i < 0))
        {
            throw new XmlException("SOAP 參數格式不正確。");
        }

        if (indices.Distinct().Count() != indices.Length)
        {
            throw new XmlException("SOAP 參數格式不正確。");
        }

        if (!indices.SequenceEqual(indices.Order()))
        {
            throw new XmlException("SOAP 參數格式不正確。");
        }

        return parameters.ToDictionary(p => p.Name.LocalName, p => p.Value, StringComparer.Ordinal);
    }

    public static string Response(ServiceVerification result, bool soap12, bool includeStatus)
    {
        // 驗證失敗沿用舊版的 500；成功但未授權身分代碼時回傳空字串。
        var status = result.Verified
            ? (includeStatus ? result.Status : "")
            : "500";

        return ArrayResponse(Operation, [result.Verified, status], soap12);
    }

    public static string ArrayResponse(string operation, IReadOnlyList<object> values, bool soap12)
    {
        XNamespace ns = Namespace;
        XNamespace xsi = XsiNamespace;

        var items = values.Select(v => new XElement(
            ns + "anyType",
            new XAttribute(xsi + "type", v is bool ? "xsd:boolean" : "xsd:string"),
            v is bool b ? (b ? "true" : "false") : (string)v));

        var response = new XElement(
            ns + (operation + "Response"),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi),
            new XAttribute(XNamespace.Xmlns + "xsd", XsdNamespace),
            new XElement(ns + (operation + "Result"), items));

        return Envelope(soap12, response);
    }

    public static string DataSetResponse(string operation, DataSet data, bool soap12)
    {
        XNamespace ns = Namespace;

        // ASMX 的 DataSet 使用內嵌 XSD + DiffGram，不能以一般 JSON/XML 字典取代。
        var result = new XElement(ns + (operation + "Result"));
        var schema = new XDocument();
        var diffgram = new XDocument();

        using (var schemaWriter = schema.CreateWriter())
        {
            data.WriteXmlSchema(schemaWriter);
        }

        using (var dataWriter = diffgram.CreateWriter())
        {
            data.WriteXml(dataWriter, XmlWriteMode.DiffGram);
        }

        result.Add(schema.Root, diffgram.Root);
        return Envelope(soap12, new XElement(ns + (operation + "Response"), result));
    }

    public static string Fault(bool soap12, bool serverError)
    {
        XNamespace env = soap12 ? Soap12 : Soap11;
        const string message = "驗證服務未完成請求，請確認服務設定與呼叫格式。";

        // SOAP 1.1 與 1.2 的 Fault 結構不同，不能共用同一份 XML。
        if (soap12)
        {
            var code = new XElement(
                env + "Code",
                new XElement(env + "Value", serverError ? "soap:Receiver" : "soap:Sender"));
            var reason = new XElement(
                env + "Reason",
                new XElement(
                    env + "Text",
                    new XAttribute(XNamespace.Xml + "lang", "zh-TW"),
                    message));

            return Envelope(soap12, new XElement(env + "Fault", code, reason));
        }

        var fault = new XElement(
            env + "Fault",
            new XElement("faultcode", serverError ? "soap:Server" : "soap:Client"),
            new XElement("faultstring", message));

        return Envelope(soap12, fault);
    }

    private static string Envelope(bool soap12, XElement body)
    {
        XNamespace env = soap12 ? Soap12 : Soap11;

        var envelope = new XElement(
            env + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", env),
            new XElement(env + "Body", body));

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + envelope.ToString(SaveOptions.DisableFormatting);
    }

    // 服務位址取自設定，不採用呼叫者提供的 Host。
    public static string Wsdl(string issuer)
    {
        return LegacySoapWsdlGenerator.Build(issuer);
    }
}
