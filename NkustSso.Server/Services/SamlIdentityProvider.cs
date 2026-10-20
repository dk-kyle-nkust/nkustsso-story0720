// 驗證 SAML 請求並產生簽章登入回應。
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.ServiceModel.Security;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2;
using Microsoft.IdentityModel.Tokens.Saml2;
using NkustSso.Server.Models;
using NkustSso.Server.ViewModels;

namespace NkustSso.Server.Services;

/// <summary>中央 SAML IdP；簽章交由協定套件處理，合作系統與資料提供規則由本站管理。</summary>
public sealed class SamlIdentityProvider : IDisposable
{
    public const string ProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    public const string AssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    public const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

    /// <summary>舊版合作系統可能送出的未指定格式，與 Persistent 一樣接受。</summary>
    private const string UnspecifiedNameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";

    /// <summary>解壓縮後的 XML 上限，避免壓縮炸彈。</summary>
    private const int MaxRequestBytes = 65536;

    public string EntityId { get; }

    public Uri LoginUrl { get; }

    private readonly X509Certificate2 certificate;

    public SamlIdentityProvider(string issuer, string directory)
    {
        var origin = new Uri(issuer);
        if (origin.Scheme != "https" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new InvalidOperationException("SAML 的 Sso:Issuer 必須為 HTTPS 基底網址。");
        }

        EntityId = new Uri(origin, "saml/metadata").AbsoluteUri;
        LoginUrl = new Uri(origin, "saml/sso");

        // 原型使用獨立且持久的簽章憑證，避免重新啟動後合作系統的信任失效。
        var path = Path.Combine(directory, "saml-signing.pfx");
        if (!File.Exists(path))
        {
            CreateSigningCertificate(directory, path);
        }

        certificate = X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey || !IsCurrent(certificate))
        {
            throw new InvalidOperationException("SAML 簽章憑證已失效，請更換憑證並更新接收系統的 metadata。");
        }
    }

    private static void CreateSigningCertificate(string directory, string path)
    {
        Directory.CreateDirectory(directory);

        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest(
            "CN=NkustSSO Development SAML",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));

        using var created = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(path, created.Export(X509ContentType.Pfx));
    }

    public Saml2Configuration Configuration()
    {
        return new Saml2Configuration
        {
            Issuer = EntityId,
            SingleSignOnDestination = LoginUrl,
            SigningCertificate = certificate,
            SignAuthnRequest = true,
            SignatureAlgorithm = RsaSha256,
            AuthnResponseSignType = Saml2AuthnResponseSignTypes.SignAssertionAndResponse,

            // 合作方公開憑證由管理者固定登記，不從請求中的 KeyInfo 或網路下載信任來源。
            CertificateValidationMode = X509CertificateValidationMode.None,
            RevocationMode = X509RevocationMode.NoCheck
        };
    }

    public string Metadata()
    {
        var singleSignOn = new SingleSignOnService
        {
            Binding = ProtocolBindings.HttpRedirect,
            Location = LoginUrl
        };

        var descriptor = new EntityDescriptor(Configuration())
        {
            ValidUntil = 1,
            IdPSsoDescriptor = new IdPSsoDescriptor
            {
                WantAuthnRequestsSigned = true,
                SigningCertificates = [certificate],
                SingleSignOnServices = [singleSignOn],
                NameIDFormats = [NameIdentifierFormats.Persistent]
            }
        };

        return new Saml2Metadata(descriptor).CreateMetadata().XmlDocument.OuterXml;
    }

    public static bool IsCurrent(X509Certificate2 value)
    {
        var now = DateTime.UtcNow;
        return value.NotBefore.ToUniversalTime() <= now && value.NotAfter.ToUniversalTime() > now;
    }

    public static bool IsValidPartnerCertificate(string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem) || pem.Length > 16000)
        {
            return false;
        }

        // 合作系統只該提供公開憑證，出現私鑰代表貼錯檔案。
        if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var value = X509Certificate2.CreateFromPem(pem);
            using var key = value.GetRSAPublicKey();

            if (key is null || key.KeySize < 2048)
            {
                return false;
            }

            return IsCurrent(value);
        }
        catch (Exception error) when (error is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>先限制解壓縮與 XML 大小，避免協定套件讀取無上限的使用者輸入。</summary>
    public static XElement ReadRequest(HttpRequest request)
    {
        EnsureQueryIsValid(request);

        var encoded = request.Query["SAMLRequest"].ToString();
        if (encoded.Length < 1 || encoded.Length > 16000)
        {
            throw new InvalidOperationException("invalid_request");
        }

        using var output = Inflate(encoded);
        var root = LoadXml(output);
        EnsureAuthnRequestShape(root);

        return root;
    }

    private static void EnsureQueryIsValid(HttpRequest request)
    {
        const string message = "invalid_request";

        if (request.QueryString.Value?.Length > 24000)
        {
            throw new InvalidOperationException(message);
        }

        // 每個參數只能出現一次，重複的參數會讓簽章比對的對象變得不明確。
        if (request.Query.Any(p => p.Value.Count != 1))
        {
            throw new InvalidOperationException(message);
        }

        var allowed = new[] { "SAMLRequest", "RelayState", "SigAlg", "Signature" };
        if (request.Query.Keys.Any(k => !allowed.Contains(k)))
        {
            throw new InvalidOperationException(message);
        }

        if (request.Query["SigAlg"] != RsaSha256 || string.IsNullOrEmpty(request.Query["Signature"]))
        {
            throw new InvalidOperationException(message);
        }

        if (Encoding.UTF8.GetByteCount(request.Query["RelayState"].ToString()) > 80)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 逐段解壓縮並在超過上限時中止，不讓一小段輸入撐出巨大的記憶體用量。
    /// </summary>
    private static MemoryStream Inflate(string encoded)
    {
        using var source = new MemoryStream(Convert.FromBase64String(encoded));
        using var deflate = new DeflateStream(source, CompressionMode.Decompress);

        var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;

        while ((count = deflate.Read(buffer)) > 0)
        {
            if (output.Length + count > MaxRequestBytes)
            {
                output.Dispose();
                throw new InvalidOperationException("request_too_large");
            }

            output.Write(buffer, 0, count);
        }

        output.Position = 0;
        return output;
    }

    private static XElement LoadXml(Stream content)
    {
        var settings = new XmlReaderSettings
        {
            // 禁用 DTD 與外部解析，避免 XXE 與實體展開攻擊。
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxRequestBytes
        };

        using var reader = XmlReader.Create(content, settings);
        return XElement.Load(reader);
    }

    private static void EnsureAuthnRequestShape(XElement root)
    {
        const string message = "unsupported_request";
        XNamespace samlp = ProtocolNamespace;
        XNamespace saml = AssertionNamespace;

        if (root.Name != samlp + "AuthnRequest" || (string?)root.Attribute("Version") != "2.0")
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrEmpty((string?)root.Attribute("ID")))
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrEmpty((string?)root.Attribute("IssueInstant")))
        {
            throw new InvalidOperationException(message);
        }

        if (root.Elements(saml + "Issuer").Count() != 1)
        {
            throw new InvalidOperationException(message);
        }

        // 只接受這三種子元素，其餘一律拒絕而不是忽略。
        var knownChildren = new[]
        {
            saml + "Issuer",
            samlp + "NameIDPolicy",
            samlp + "RequestedAuthnContext"
        };
        if (root.Elements().Any(e => !knownChildren.Contains(e.Name)))
        {
            throw new InvalidOperationException(message);
        }

        if (root.Elements(samlp + "NameIDPolicy").Count() > 1)
        {
            throw new InvalidOperationException(message);
        }

        if (root.Elements(samlp + "RequestedAuthnContext").Count() > 1)
        {
            throw new InvalidOperationException(message);
        }
    }

    public Saml2AuthnRequest ValidateRequest(
        HttpRequest http,
        XElement xml,
        RegisteredSystem system,
        DateTimeOffset now)
    {
        if (!IsValidPartnerCertificate(system.SamlSigningCertificate))
        {
            throw new InvalidOperationException("invalid_certificate");
        }

        using var partner = X509Certificate2.CreateFromPem(system.SamlSigningCertificate);
        var config = Configuration();
        config.SignatureValidationCertificates.Add(partner);

        var request = new Saml2AuthnRequest(config);
        new Saml2RedirectBinding().Unbind(http.ToGenericHttpRequest(), request);

        EnsureRequestMatchesSystem(request, system, now);
        XmlConvert.VerifyNCName(request.IdAsString);

        EnsureNameIdPolicyIsSupported(xml, system);
        EnsureAuthnContextIsSupported(xml);

        return request;
    }

    private void EnsureRequestMatchesSystem(
        Saml2AuthnRequest request,
        RegisteredSystem system,
        DateTimeOffset now)
    {
        const string message = "unsupported_or_invalid_request";

        if (request.Issuer != system.SamlEntityId)
        {
            throw new InvalidOperationException(message);
        }

        if (request.Destination?.OriginalString != LoginUrl.AbsoluteUri)
        {
            throw new InvalidOperationException(message);
        }

        if (request.IdAsString.Length < 1 || request.IdAsString.Length > 200)
        {
            throw new InvalidOperationException(message);
        }

        // 容許少量時鐘誤差，但不接受明顯過舊或來自未來的請求。
        if (request.IssueInstant < now.AddMinutes(-5) || request.IssueInstant > now.AddSeconds(30))
        {
            throw new InvalidOperationException(message);
        }

        // 有指定回呼時必須與登記的完全相同；沒指定則沿用登記值。
        if (request.AssertionConsumerServiceUrl is { } acs && acs.OriginalString != system.RedirectUri)
        {
            throw new InvalidOperationException(message);
        }

        if (request.ProtocolBinding is { } binding && binding != ProtocolBindings.HttpPost)
        {
            throw new InvalidOperationException(message);
        }

        // 以索引指定回呼或屬性集會繞過上面的比對，一律不支援。
        if (request.AssertionConsumerServiceIndex is not null)
        {
            throw new InvalidOperationException(message);
        }

        if (request.AttributeConsumingServiceIndex is not null)
        {
            throw new InvalidOperationException(message);
        }

        if (request.ForceAuthn == true)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureNameIdPolicyIsSupported(XElement xml, RegisteredSystem system)
    {
        const string message = "unsupported_name_id";
        XNamespace p = ProtocolNamespace;

        var policy = xml.Element(p + "NameIDPolicy");
        var format = (string?)policy?.Attribute("Format");

        if (format is not null
            && format != NameIdentifierFormats.Persistent.OriginalString
            && format != UnspecifiedNameIdFormat)
        {
            throw new InvalidOperationException(message);
        }

        if (policy?.Attribute("SPNameQualifier") is { } qualifier && qualifier.Value != system.SamlEntityId)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureAuthnContextIsSupported(XElement xml)
    {
        const string message = "unsupported_authn_context";
        XNamespace p = ProtocolNamespace;
        XNamespace a = AssertionNamespace;

        var context = xml.Element(p + "RequestedAuthnContext");
        if (context is null)
        {
            return;
        }

        var comparison = (string?)context.Attribute("Comparison");
        if (comparison is not null && comparison != "exact")
        {
            throw new InvalidOperationException(message);
        }

        if (context.Elements().Count() != 1)
        {
            throw new InvalidOperationException(message);
        }

        var classRef = context.Element(a + "AuthnContextClassRef")?.Value;
        if (classRef != AuthnContextClassTypes.PasswordProtectedTransport.OriginalString)
        {
            throw new InvalidOperationException(message);
        }
    }

    public SamlResponseFormViewModel Response(
        RegisteredSystem system,
        SamlLoginRequest request,
        Account? account,
        DateTimeOffset? authenticatedAt)
    {
        if (!IsCurrent(certificate))
        {
            throw new InvalidOperationException("expired_signing_certificate");
        }

        var config = Configuration();
        if (account is null)
        {
            // 沒有登入結果時只有回應本身需要簽章，沒有 assertion 可簽。
            config.AuthnResponseSignType = Saml2AuthnResponseSignTypes.SignResponse;
        }

        var response = new Saml2AuthnResponse(config)
        {
            InResponseTo = new Saml2Id(request.RequestId),
            Destination = new Uri(system.RedirectUri),
            Status = account is null ? Saml2StatusCodes.NoPassive : Saml2StatusCodes.Success
        };

        if (account is not null)
        {
            FillAssertion(response, system, account, authenticatedAt);
        }

        var binding = new Saml2PostBinding();
        binding.Bind(response);

        // 套件預設頁面含 inline script；自行呈現表單，維持本站 CSP。
        var xml = Convert.ToBase64String(Encoding.UTF8.GetBytes(binding.XmlDocument.OuterXml));
        return new SamlResponseFormViewModel(system.RedirectUri, xml, request.RelayState);
    }

    private static void FillAssertion(
        Saml2AuthnResponse response,
        RegisteredSystem system,
        Account account,
        DateTimeOffset? authenticatedAt)
    {
        var principal = OidcIdentityClaims.Create(account, ["openid", "profile", OidcIdentityClaims.Scope]);
        IdentityClaimReleasePolicy.Filter(principal, system.ClaimMask);

        response.NameId = new Saml2NameIdentifier(account.Id, NameIdentifierFormats.Persistent);
        response.SessionIndex = "_" + Guid.NewGuid().ToString("N");

        var released = principal.Claims
            .Where(c => IdentityClaimReleasePolicy.Fields.Any(f => f.Type == c.Type));
        response.ClaimsIdentity = new ClaimsIdentity(released);

        var token = response.CreateSecurityToken(
            system.SamlEntityId,
            subjectConfirmationLifetime: 2,
            issuedTokenLifetime: 2);

        // 回報實際通過驗證的時間，而不是這次產生回應的時間。
        var instant = (authenticatedAt ?? DateTimeOffset.UtcNow).UtcDateTime;
        foreach (var statement in token.Assertion.Statements.OfType<Saml2AuthenticationStatement>())
        {
            statement.AuthenticationInstant = instant;
        }
    }

    public void Dispose()
    {
        certificate.Dispose();
    }
}
