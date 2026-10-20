using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NkustSso.Server.Services;

/// <summary>
/// 加密 Data Protection 金鑰環用的本機憑證。
/// 不是 HTTPS 憑證：自簽憑證沒進信任存放區，串接端做 OIDC discovery 會以
/// UntrustedRoot 失敗，所以 HTTPS 另外交給 dotnet dev-certs 的開發憑證。
/// </summary>
public static class KeyProtectionCertificate
{
    private const int KeySizeBits = 2048;
    private const int ValidDays = 30;
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// 沿用 <paramref name="directory"/> 既有的 localhost.pfx，沒有時才依
    /// <paramref name="create"/> 產生。這張憑證換掉就解不開既有金鑰環，
    /// 所有現存登入狀態會一次失效，所以絕不覆寫已存在的檔案。
    /// </summary>
    public static X509Certificate2 Load(string directory, bool create = false)
    {
        var file = Path.Combine(directory, "localhost.pfx");
        if (!File.Exists(file) && create)
        {
            CreateSelfSigned(directory, file);
        }

        // Windows SChannel 不接受只存在記憶體的私鑰，所以用 DefaultKeySet 載入。
        return X509CertificateLoader.LoadPkcs12FromFile(file, null, X509KeyStorageFlags.DefaultKeySet);
    }

    /// <summary>
    /// 只寫入指定目錄，不改動憑證存放區，也不調整系統信任設定。
    /// </summary>
    private static void CreateSelfSigned(string directory, string file)
    {
        Directory.CreateDirectory(directory);
        using var key = RSA.Create(KeySizeBits);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ServerAuthenticationOid) },
            false));
        // 起始時間往前五分鐘，避免與系統時鐘有秒級落差時憑證還沒生效。
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(ValidDays));
        File.WriteAllBytes(file, certificate.Export(X509ContentType.Pfx));
        File.WriteAllBytes(Path.Combine(directory, "localhost.cer"), certificate.Export(X509ContentType.Cert));
    }
}
