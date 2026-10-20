// 依已支援的方法產生相容舊介面的 WSDL。
using System.Xml.Linq;
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.Services;

public static class LegacySoapWsdlGenerator
{
    private static readonly XNamespace Wsdl = "http://schemas.xmlsoap.org/wsdl/";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/wsdl/soap/";
    private static readonly XNamespace Soap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";

    private const string PortTypeName = "KuasSSOWebServiceSoap";
    private const string ServiceName = "KuasSSOWebService";
    private const string Transport = "http://schemas.xmlsoap.org/soap/http";

    public static string Build(string issuer)
    {
        // 只公開已完成的方法；未移植的方法不列入 WSDL。
        var methods = SoapHelpCatalog.Operations
            .Where(o => LegacySoapOperationCatalog.Available.Contains(o.Name))
            .ToArray();

        var root = new XElement(
            Wsdl + "definitions",
            new XAttribute(XNamespace.Xmlns + "wsdl", Wsdl),
            new XAttribute(XNamespace.Xmlns + "s", Xsd),
            new XAttribute(XNamespace.Xmlns + "soap", Soap11),
            new XAttribute(XNamespace.Xmlns + "soap12", Soap12),
            new XAttribute(XNamespace.Xmlns + "tns", LegacySoapContract.Namespace),
            new XAttribute("targetNamespace", LegacySoapContract.Namespace),
            new XElement(Wsdl + "types", BuildSchema(methods)));

        AddMessages(root, methods);
        AddPortType(root, methods);
        AddBindingsAndService(root, methods, issuer);

        return new XDocument(root).ToString();
    }

    /// <summary>
    /// 內嵌的 XSD：共用的 ArrayOfAnyType，加上每個方法的請求與回應元素。
    /// </summary>
    private static XElement BuildSchema(IReadOnlyList<SoapHelpOperation> methods)
    {
        var arrayOfAnyType = new XElement(
            Xsd + "complexType",
            new XAttribute("name", "ArrayOfAnyType"),
            new XElement(
                Xsd + "sequence",
                new XElement(
                    Xsd + "element",
                    new XAttribute("name", "anyType"),
                    new XAttribute("minOccurs", "0"),
                    new XAttribute("maxOccurs", "unbounded"),
                    new XAttribute("nillable", "true"))));

        var schema = new XElement(
            Xsd + "schema",
            new XAttribute("elementFormDefault", "qualified"),
            new XAttribute("targetNamespace", LegacySoapContract.Namespace),
            new XElement(Xsd + "import", new XAttribute("namespace", Xsd)),
            arrayOfAnyType);

        foreach (var method in methods)
        {
            schema.Add(BuildRequestElement(method));
            schema.Add(BuildResponseElement(method));
        }

        return schema;
    }

    private static XElement BuildRequestElement(SoapHelpOperation method)
    {
        var parameters = method.Parameters.Select(p => new XElement(
            Xsd + "element",
            new XAttribute("name", p.Name),
            new XAttribute("type", "s:string"),
            new XAttribute("minOccurs", "0"),
            new XAttribute("maxOccurs", "1")));

        return new XElement(
            Xsd + "element",
            new XAttribute("name", method.Name),
            new XElement(Xsd + "complexType", new XElement(Xsd + "sequence", parameters)));
    }

    private static XElement BuildResponseElement(SoapHelpOperation method)
    {
        var result = new XElement(
            Xsd + "element",
            new XAttribute("name", method.Name + "Result"),
            new XAttribute("minOccurs", "0"),
            new XAttribute("maxOccurs", "1"));

        if (LegacySoapOperationCatalog.ReturnsDataSet(method.Name))
        {
            // DataSet 回傳是內嵌 XSD 加上 DiffGram，不是固定結構。
            result.Add(new XElement(
                Xsd + "complexType",
                new XElement(
                    Xsd + "sequence",
                    new XElement(Xsd + "element", new XAttribute("ref", "s:schema")),
                    new XElement(Xsd + "any"))));
        }
        else
        {
            result.Add(new XAttribute("type", "tns:ArrayOfAnyType"));
        }

        return new XElement(
            Xsd + "element",
            new XAttribute("name", method.Name + "Response"),
            new XElement(Xsd + "complexType", new XElement(Xsd + "sequence", result)));
    }

    private static void AddMessages(XElement root, IReadOnlyList<SoapHelpOperation> methods)
    {
        foreach (var method in methods)
        {
            root.Add(new XElement(
                Wsdl + "message",
                new XAttribute("name", method.Name + "SoapIn"),
                new XElement(
                    Wsdl + "part",
                    new XAttribute("name", "parameters"),
                    new XAttribute("element", "tns:" + method.Name))));

            root.Add(new XElement(
                Wsdl + "message",
                new XAttribute("name", method.Name + "SoapOut"),
                new XElement(
                    Wsdl + "part",
                    new XAttribute("name", "parameters"),
                    new XAttribute("element", "tns:" + method.Name + "Response"))));
        }
    }

    private static void AddPortType(XElement root, IReadOnlyList<SoapHelpOperation> methods)
    {
        var operations = methods.Select(m => new XElement(
            Wsdl + "operation",
            new XAttribute("name", m.Name),
            new XElement(Wsdl + "input", new XAttribute("message", "tns:" + m.Name + "SoapIn")),
            new XElement(Wsdl + "output", new XAttribute("message", "tns:" + m.Name + "SoapOut"))));

        root.Add(new XElement(
            Wsdl + "portType",
            new XAttribute("name", PortTypeName),
            operations));
    }

    /// <summary>
    /// SOAP 1.1 與 1.2 各一組 binding 與 port，內容只差在命名空間與名稱。
    /// </summary>
    private static void AddBindingsAndService(
        XElement root,
        IReadOnlyList<SoapHelpOperation> methods,
        string issuer)
    {
        // 服務位址取自設定，不採用呼叫者提供的 Host。
        var address = new Uri(new Uri(issuer), "KuasSSOWebService.asmx").AbsoluteUri;
        var service = new XElement(Wsdl + "service", new XAttribute("name", ServiceName));

        foreach (var bindingNs in new[] { Soap11, Soap12 })
        {
            var name = bindingNs == Soap11 ? PortTypeName : PortTypeName + "12";

            root.Add(BuildBinding(name, bindingNs, methods));

            service.Add(new XElement(
                Wsdl + "port",
                new XAttribute("name", name),
                new XAttribute("binding", "tns:" + name),
                new XElement(bindingNs + "address", new XAttribute("location", address))));
        }

        root.Add(service);
    }

    private static XElement BuildBinding(
        string name,
        XNamespace bindingNs,
        IReadOnlyList<SoapHelpOperation> methods)
    {
        var operations = methods.Select(m => new XElement(
            Wsdl + "operation",
            new XAttribute("name", m.Name),
            new XElement(
                bindingNs + "operation",
                new XAttribute("soapAction", m.Action),
                new XAttribute("style", "document")),
            new XElement(Wsdl + "input", new XElement(bindingNs + "body", new XAttribute("use", "literal"))),
            new XElement(Wsdl + "output", new XElement(bindingNs + "body", new XAttribute("use", "literal")))));

        return new XElement(
            Wsdl + "binding",
            new XAttribute("name", name),
            new XAttribute("type", "tns:" + PortTypeName),
            new XElement(bindingNs + "binding", new XAttribute("transport", Transport)),
            operations);
    }
}
