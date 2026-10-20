using System.Diagnostics;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;

namespace NkustSso.Server.Models;

/// <summary>獨立遠端連線，不呼叫本地建表／遷移；資料庫帳號須另限制為 SELECT 權限。</summary>
public interface ICampusDatabaseFactory
{
    DataConnection Open();
}

public sealed class CampusDatabaseFactory(IConfiguration configuration, IOptions<CampusAuthenticationOptions> options) : ICampusDatabaseFactory
{
    public static bool HasValidConnection(IConfiguration configuration)
    {
        try
        {
            var text = configuration.GetConnectionString("LoginAuth");
            if (string.IsNullOrWhiteSpace(text)) return false;
            var connection = new SqlConnectionStringBuilder(text);
            return !string.IsNullOrWhiteSpace(connection.DataSource) && !string.IsNullOrWhiteSpace(connection.InitialCatalog)
                && string.IsNullOrEmpty(connection.AttachDBFilename);
        }
        catch (ArgumentException) { return false; }
    }

    public DataConnection Open()
    {
        if (!options.Value.Enabled || !HasValidConnection(configuration))
            throw new InvalidOperationException("校務唯讀連線尚未啟用或設定不完整。");
        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("LoginAuth"))
        {
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            // 保留連線字串明確指定的憑證信任設定；未設定時 SqlClient 預設仍會驗證憑證。
            PersistSecurityInfo = false,
            ConnectTimeout = options.Value.CommandTimeoutSeconds,
            ApplicationName = "NkustSso.ReadOnly"
        };
        return new DataConnection(new DataOptions()
            .UseSqlServer(connection.ConnectionString, SqlServerVersion.v2012, SqlServerProvider.MicrosoftDataSqlClient)
            .UseTraceLevel(TraceLevel.Off)) { CommandTimeout = options.Value.CommandTimeoutSeconds };
    }
}
