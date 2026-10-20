using Microsoft.Data.SqlClient;
using NkustSso.Server.Models;

namespace NkustSso.Tests;

/// <summary>只建立及清理由本次測試產生的隨機資料庫，不接受既有資料庫名稱。</summary>
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly string name = "NkustSSO_Test_" + Guid.NewGuid().ToString("N");
    private readonly string server = Environment.GetEnvironmentVariable("NKUST_TEST_SQLSERVER") ?? "lpc:localhost";
    private string Connection(string database) => new SqlConnectionStringBuilder
    {
        DataSource = server, InitialCatalog = database, IntegratedSecurity = true,
        Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = true,
        Pooling = false, ConnectTimeout = 10
    }.ConnectionString;

    public DatabaseFactory Factory => DatabaseFactory.ForSqlServer(Connection(name));
    public string ConnectionString => Connection(name);

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var result = new SqlServerTestDatabase();
        await using var connection = new SqlConnection(result.Connection("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{result.name}]";
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("建立隔離 SQL Server 測試庫：" + result.name);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new SqlConnection(Connection("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("已清理隔離 SQL Server 測試庫：" + name);
    }
}
