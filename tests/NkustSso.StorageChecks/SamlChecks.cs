using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class SamlChecks
{
    public static async Task MigrationAsync(string directory)
    {
        var path = Path.Combine(directory, "saml-migration");
        Directory.CreateDirectory(path);
        var factory = new DatabaseFactory(path);
        await using (var old = factory.Open())
        {
            await old.CreateTableAsync<RegisteredSystem>();
            await old.InsertAsync(new RegisteredSystem { ClientId = "legacy", Protocol = "oidc", DisplayName = "Keep me", ClaimMask = 2 });
            await old.ExecuteAsync("ALTER TABLE RegisteredSystems DROP COLUMN SamlEntityId");
            await old.ExecuteAsync("ALTER TABLE RegisteredSystems DROP COLUMN SamlSigningCertificate");
        }
        await factory.InitializeAsync();
        await factory.InitializeAsync();
        await using var db = factory.Open();
        var system = await db.RegisteredSystems.SingleAsync(s => s.ClientId == "legacy");
        Check(system.DisplayName == "Keep me" && system.ClaimMask == 2 && system.SamlEntityId == "" && system.SamlSigningCertificate == "", "Upgrade preserves old registration and adds SAML columns idempotently.");
        Console.WriteLine("PASS SAML schema upgrade preserves existing registrations");
    }

    public static void Run(string directory)
    {
        using var idp = new SamlIdentityProvider("https://localhost:7100/", directory);
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=SAML test SP", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        var partner = new RegisteredSystem { ClientId = "saml-check", Protocol = "saml", SamlEntityId = "urn:test:sp",
            RedirectUri = "https://localhost:7145/acs", EntryUrl = "https://localhost:7145/login", DisplayName = "SAML test",
            Category = "general", SamlSigningCertificate = cert.ExportCertificatePem(), ClaimMask = 2 };
        Check(!SsoSystemRegistrationService.Validate(partner).Any(), "Valid SAML registration.");
        var config = new Saml2Configuration { Issuer = partner.SamlEntityId, SignAuthnRequest = true, SigningCertificate = cert,
            SingleSignOnDestination = idp.LoginUrl, SignatureAlgorithm = SamlIdentityProvider.RsaSha256 };
        var request = new Saml2AuthnRequest(config) { AssertionConsumerServiceUrl = new Uri(partner.RedirectUri), ProtocolBinding = ProtocolBindings.HttpPost };
        var binding = new Saml2RedirectBinding { RelayState = "opaque-state" };
        binding.Bind(request);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.QueryString = new QueryString(binding.RedirectLocation.Query);
        var xml = SamlIdentityProvider.ReadRequest(context.Request);
        var parsed = idp.ValidateRequest(context.Request, xml, partner, DateTimeOffset.UtcNow);
        Check(parsed.IdAsString == request.IdAsString, "Signed request accepted.");
        context.Request.QueryString = new QueryString(binding.RedirectLocation.Query.Replace("opaque-state", "tampered"));
        Reject(() => idp.ValidateRequest(context.Request, SamlIdentityProvider.ReadRequest(context.Request), partner, DateTimeOffset.UtcNow));
        context.Request.QueryString = new QueryString(binding.RedirectLocation.Query);
        partner.RedirectUri = "https://other.example/acs";
        Reject(() => idp.ValidateRequest(context.Request, xml, partner, DateTimeOffset.UtcNow));
        partner.RedirectUri = "https://localhost:7145/acs";
        Reject(() => idp.ValidateRequest(context.Request, xml, partner, DateTimeOffset.UtcNow.AddMinutes(6)));
        var account = new Account { Id = "stable-subject", UserName = "student", DisplayName = "Test Student", ClassCode = "private-class" };
        var flow = new SamlLoginRequest { RequestId = request.IdAsString, RelayState = "opaque-state" };
        var output = idp.Response(partner, flow, account, DateTimeOffset.UtcNow.AddMinutes(-10));
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        document.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(output.SamlResponse)));
        XNamespace ds = SignedXml.XmlDsigNamespaceUrl;
        XNamespace a = SamlIdentityProvider.AssertionNamespace;
        var metadata = XElement.Parse(idp.Metadata());
        using var signing = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(metadata.Descendants(ds + "X509Certificate").First().Value));
        var responseSignature = (XmlElement)document.DocumentElement!.SelectSingleNode("*[local-name()='Signature']")!;
        var signedResponse = new SignedXml(document);
        signedResponse.LoadXml(responseSignature);
        Check(signedResponse.CheckSignature(signing, true), "Response signature verified independently.");
        var assertion = (XmlElement)document.DocumentElement.SelectSingleNode("*[local-name()='Assertion']")!;
        var assertionSignature = (XmlElement)assertion.SelectSingleNode("*[local-name()='Signature']")!;
        var signedAssertion = new SignedXml(assertion);
        signedAssertion.LoadXml(assertionSignature);
        Check(signedAssertion.CheckSignature(signing, true), "Assertion signature verified independently.");
        var response = XElement.Parse(document.OuterXml);
        Check(response.Attribute("InResponseTo")?.Value == request.IdAsString, "Response correlation.");
        Check(response.Descendants(a + "NameID").Single().Value == account.Id, "Stable NameID.");
        Check(response.Descendants(a + "Audience").Single().Value == partner.SamlEntityId, "Restricted audience.");
        Check(response.Descendants(a + "Attribute").Select(e => e.Attribute("Name")!.Value).SequenceEqual(["name"]), "Only permitted attributes released.");
        Check(response.Descendants(a + "SubjectConfirmationData").Single().Attribute("Recipient")?.Value == partner.RedirectUri, "Exact recipient.");
        var conditions = response.Descendants(a + "Conditions").Single();
        Check(DateTimeOffset.Parse(conditions.Attribute("NotOnOrAfter")!.Value) <= DateTimeOffset.UtcNow.AddMinutes(3), "Short assertion lifetime.");
        assertion.SetAttribute("ID", "_tampered");
        Check(!signedResponse.CheckSignature(signing, true), "Tampering fails signature verification.");
        using var restarted = new SamlIdentityProvider("https://localhost:7100/", directory);
        Check(XElement.Parse(restarted.Metadata()).Descendants(ds + "X509Certificate").First().Value == Convert.ToBase64String(signing.RawData), "Restart preserves signing certificate.");
        Console.WriteLine("PASS SAML request signatures, ACS, expiry, claim release, independent response/assertion signatures and persistent certificate");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        var rejected = false;
        try { action(); } catch { rejected = true; }
        Check(rejected, "Invalid SAML input must be rejected.");
    }
}
