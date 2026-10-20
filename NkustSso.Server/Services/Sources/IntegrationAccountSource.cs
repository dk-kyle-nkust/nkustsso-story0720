// 保留舊 LDAP／備援驗證實作供相容性測試，正式入口未註冊。
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.Json.Serialization;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

public interface ILegacyDirectoryAuthenticator
{
    Task<ServiceVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken);
}

public interface ILegacyMappingDatabase
{
    DataConnection Open();
}

/// <summary>舊 SSO 的備援資料庫獨立設定，不能誤用目前校務庫的 s90_auth_usr。</summary>
public sealed class LegacyMappingDatabase(
    IConfiguration configuration,
    IOptions<IntegrationServiceOptions> options) : ILegacyMappingDatabase
{
    private const string ConnectionName = "LegacySsoReadOnly";

    public static bool IsConfigured(IConfiguration configuration)
    {
        try
        {
            var text = configuration.GetConnectionString(ConnectionName);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var value = new SqlConnectionStringBuilder(text);

            if (string.IsNullOrWhiteSpace(value.DataSource))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(value.InitialCatalog))
            {
                return false;
            }

            // AttachDBFilename 代表連到本機檔案，不是校務資料庫。
            return string.IsNullOrEmpty(value.AttachDBFilename);
        }
        catch (ArgumentException)
        {
            // 連線字串格式錯誤，視為未設定。
            return false;
        }
    }

    public DataConnection Open()
    {
        if (!IsConfigured(configuration))
        {
            throw new InvalidOperationException("舊 SSO 唯讀連線尚未設定。");
        }

        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString(ConnectionName))
        {
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            PersistSecurityInfo = false,
            ConnectTimeout = options.Value.TimeoutSeconds,
            ApplicationName = "NkustSso.LegacyReadOnly"
        };

        var dataOptions = new DataOptions()
            .UseSqlServer(
                connection.ConnectionString,
                SqlServerVersion.v2012,
                SqlServerProvider.MicrosoftDataSqlClient)
            .UseTraceLevel(TraceLevel.Off);

        return new DataConnection(dataOptions)
        {
            CommandTimeout = options.Value.TimeoutSeconds
        };
    }
}

public sealed class LegacyDirectoryAuthenticator(IOptions<IntegrationServiceOptions> options)
    : ILegacyDirectoryAuthenticator
{
    public Task<ServiceVerification> VerifyAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        // LdapConnection 是同步 API，放到執行緒集區避免卡住請求。
        return Task.Run(() => Verify(userName, password, cancellationToken), cancellationToken);
    }

    private ServiceVerification Verify(string userName, string password, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var connection = new LdapConnection(
            new LdapDirectoryIdentifier(settings.LdapHost, settings.LdapPort),
            new NetworkCredential(userName, password, settings.LdapDomain),
            AuthType.Negotiate);

        connection.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        connection.SessionOptions.ProtocolVersion = 3;

        // 不複製舊程式的明文傳輸或略過憑證驗證；使用受信任的 LDAPS 憑證。
        connection.SessionOptions.SecureSocketLayer = true;
        connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;

        connection.Bind();
        cancellationToken.ThrowIfCancellationRequested();

        var request = new SearchRequest(
            settings.LdapBaseDn,
            "(sAMAccountName=" + Escape(userName) + ")",
            SearchScope.Subtree,
            "employeeType",
            "comment")
        {
            // 取兩筆就夠判斷「結果不只一筆」。
            SizeLimit = 2,
            TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        var response = (SearchResponse)connection.SendRequest(request);
        if (response.Entries.Count != 1)
        {
            return new ServiceVerification(false);
        }

        var entry = response.Entries[0];
        var type = FirstValue(entry, "employeeType");
        var status = FirstValue(entry, "comment");

        if (string.IsNullOrWhiteSpace(type))
        {
            return new ServiceVerification(false);
        }

        // comment 為 0L 代表已離職。
        if (status?.Trim() == "0L")
        {
            return new ServiceVerification(false);
        }

        return new ServiceVerification(true, type);
    }

    private static string? FirstValue(SearchResultEntry entry, string attribute)
    {
        return entry.Attributes[attribute]
            ?.GetValues(typeof(string))
            .Cast<string>()
            .FirstOrDefault();
    }

    /// <summary>LDAP 篩選字串的特殊字元必須轉義，否則帳號可以改寫查詢條件。</summary>
    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
}

public sealed class IntegrationAccountSource(
    ICampusDatabaseFactory campus,
    ILegacyMappingDatabase legacy,
    ILegacyDirectoryAuthenticator directory,
    IOptions<IntegrationServiceOptions> options) : IIntegrationAccountSource
{
    public async Task<ServiceVerification> VerifyAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        if (options.Value.AuthenticationSource == "campus")
        {
            return await VerifyFromCampusAsync(userName, password, cancellationToken);
        }

        if (options.Value.AuthenticationSource != "legacy")
        {
            return new ServiceVerification(false, Unavailable: true);
        }

        try
        {
            return await directory.VerifyAsync(userName, password, cancellationToken);
        }
        catch (DirectoryException)
        {
            // 舊方法在 LDAP 拋出錯誤（包含帳密錯誤）時才使用 MappingUsrData 備援。
            // 不移植將明文密碼寫入同步佇列的副作用；LDAP 密碼同步由獨立流程處理。
            return await VerifyFromLegacyMappingAsync(userName, password, cancellationToken);
        }
    }

    private async Task<ServiceVerification> VerifyFromCampusAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        await using var db = campus.Open();

        var rows = await db.GetTable<CampusCredentialRecord>()
            .Where(a => a.MemberId == userName)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rows.Count != 1)
        {
            return new ServiceVerification(false);
        }

        var account = rows[0];
        var valid = TienFangPasswordVerifier.Verify(account.EncodedPassword, password);

        // 比對完立刻清掉記憶體中的密碼欄位。
        account.EncodedPassword = null;

        if (!valid || string.IsNullOrWhiteSpace(account.StatusCode))
        {
            return new ServiceVerification(false);
        }

        return new ServiceVerification(true, account.StatusCode.Trim());
    }

    private async Task<ServiceVerification> VerifyFromLegacyMappingAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        await using var db = legacy.Open();

        var rows = await db.GetTable<LegacyMappingCredential>()
            .Where(a => a.MemberId == userName)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rows.Count != 1)
        {
            return new ServiceVerification(false);
        }

        var account = rows[0];
        var valid = TienFangPasswordVerifier.Verify(account.EncodedPassword, password.Trim());
        account.EncodedPassword = null;

        if (!valid || string.IsNullOrWhiteSpace(account.StatusCode))
        {
            return new ServiceVerification(false);
        }

        // usr_status 為 0L 代表已離職。
        if (account.UserStatus?.Trim() == "0L")
        {
            return new ServiceVerification(false);
        }

        return new ServiceVerification(true, account.StatusCode.Trim());
    }
}

[Table("SSO_MappingUsrData", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class LegacyMappingCredential
{
    [Column("usr_id", DataType = DataType.VarChar)]
    [NotNull]
    public string MemberId { get; set; } = "";

    [Column("usr_mdpwd", DataType = DataType.VarChar)]
    [Nullable]
    [JsonIgnore]
    public string? EncodedPassword { get; set; }

    [Column("sts_id", DataType = DataType.VarChar)]
    [Nullable]
    public string? StatusCode { get; set; }

    [Column("usr_status", DataType = DataType.VarChar)]
    [Nullable]
    public string? UserStatus { get; set; }
}
