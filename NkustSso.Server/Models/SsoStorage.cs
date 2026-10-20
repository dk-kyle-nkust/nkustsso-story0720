using System.Diagnostics;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace NkustSso.Server.Models;

/// <summary>SSO 自有資料的 SQL Server 設定，與校務唯讀連線完全分開。</summary>
public static class SsoStorage
{
    public static DatabaseFactory FromConfiguration(IConfiguration configuration, string directory)
    {
        var provider = configuration["Sso:StorageProvider"] ?? "SQLite";
        if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase)) return new DatabaseFactory(directory);
        if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sso:StorageProvider 僅接受 SqlServer 或 SQLite。");
        var connection = configuration.GetConnectionString("Sso");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("請設定 SSO 自有資料庫 ConnectionStrings:Sso；不會回退至 SQLite 或校務連線。");
        return DatabaseFactory.ForSqlServer(connection);
    }

    internal static string ValidateConnection(string connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(connection.DataSource) ||
            !(connection.InitialCatalog.Equals("NkustSSO", StringComparison.OrdinalIgnoreCase) ||
              connection.InitialCatalog.StartsWith("NkustSSO_Test_", StringComparison.Ordinal)) ||
            !string.IsNullOrEmpty(connection.AttachDBFilename))
            throw new InvalidOperationException("SSO 儲存連線須明確指定伺服器與 NkustSSO（或 NkustSSO_Test_ 開頭的隔離測試庫）。");
        connection.Encrypt = SqlConnectionEncryptOption.Mandatory;
        connection.PersistSecurityInfo = false;
        connection.ApplicationName = "NkustSso.Storage";
        return connection.ConnectionString;
    }

    internal static DataOptions SqlOptions(string connection, MappingSchema mapping) => new DataOptions()
        .UseSqlServer(connection, SqlServerVersion.v2019, SqlServerProvider.MicrosoftDataSqlClient)
        .UseMappingSchema(mapping).UseTraceLevel(TraceLevel.Off);

    public static bool IsUniqueViolation(Exception exception) => exception switch
    {
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode is 1555 or 2067,
        SqlException sql => sql.Number is 2601 or 2627,
        _ => false
    };

    // 只列自有資料模型，避免反射掃描意外將校務唯讀模型納入建表。
    public static IReadOnlyList<Type> MainTables { get; } = new Type[]
    {
        typeof(Account), typeof(Microsoft.AspNetCore.Identity.IdentityRole), typeof(AccountRole), typeof(LoginEvent),
        typeof(TicketClient), typeof(RegisteredSystem), typeof(PortalAudience), typeof(SystemConfigurationAudit),
        typeof(IntegrationClient), typeof(IntegrationAudit), typeof(IntegrationCapability), typeof(IntegrationClientService),
        typeof(IntegrationClientReturnField), typeof(PortalCategory), typeof(PortalCategoryAudit), typeof(SchemaMigration),
        typeof(AdminMenu), typeof(AdminRole), typeof(AdminRolePermission), typeof(AdminRoleMember), typeof(AdminManagementAudit),
        typeof(IdentityDimension), typeof(IdentityOption), typeof(IdentityCatalogAudit), typeof(AccountIdentitySet), typeof(AccountIdentity),
        typeof(OidcApplication), typeof(OidcAuthorization), typeof(OidcScope), typeof(OidcToken)
    };
    public static IReadOnlyList<Type> SessionTables { get; } = new Type[]
    {
        typeof(LoginSession), typeof(SessionAudit), typeof(CampusLoginAttempt), typeof(LoginTicket),
        typeof(IntegrationConsumedTicket), typeof(TicketAudit), typeof(SamlLoginRequest), typeof(SamlAudit)
    };

    internal static MappingSchema CreateSqlMapping(MappingSchema original)
    {
        var mapping = new MappingSchema(original);
        var builder = new FluentMappingBuilder(mapping);
        var mappedMembers = new HashSet<System.Reflection.MemberInfo>();
        foreach (var type in MainTables.Concat(SessionTables))
        {
            var entity = original.GetEntityDescriptor(type);
            builder.HasAttribute(type, new TableAttribute(entity.TableName) { Schema = "dbo", IsColumnAttributeRequired = true });
            foreach (var column in entity.Columns)
            {
                if (!mappedMembers.Add(column.MemberInfo)) continue;
                var attribute = new ColumnAttribute(column.ColumnName)
                {
                    IsPrimaryKey = column.IsPrimaryKey, PrimaryKeyOrder = column.PrimaryKeyOrder,
                    IsIdentity = column.IsIdentity, CanBeNull = !column.IsPrimaryKey && column.CanBeNull
                };
                if (column.MemberType == typeof(string))
                {
                    var length = StringLength(column);
                    attribute.DataType = DataType.NVarChar;
                    attribute.Length = length;
                    // 大小寫敏感欄位延續 SQLite 語意；JSON、憑證、Token 及稽核不截斷。
                    attribute.DbType = $"nvarchar({(length > 0 ? length.ToString() : "max")})";
                    attribute.CreateFormat = "{0} {1} COLLATE Latin1_General_100_BIN2 {2} {3}";
                }
                else if (column.MemberType == typeof(byte[]))
                {
                    attribute.DataType = DataType.VarBinary;
                    attribute.DbType = "varbinary(max)";
                }
                builder.HasAttribute(column.MemberInfo, attribute);
            }
        }
        builder.Build();
        return mapping;
    }

    private static int StringLength(ColumnDescriptor column)
    {
        if (column.ColumnName is "NormalizedName" or "NormalizedUserName" or "SourceAccountId") return 256;
        if (column.IsPrimaryKey) return 200; // 複合主鍵最多兩個字串，合計 800 bytes。
        return column.ColumnName switch
        {
            "AuthenticationSource" => 64,
            "SourceStatusCode" or "SourceStatusName" or "UnitCode" or "UnitName" => 256,
            "SourceSystem" or "SourceId" or "Dimension" or "Code" or "ClientId" or "Name" or "ReferenceId" => 256,
            "AccountId" or "AuthorizationId" => 200,
            "RequestHash" or "TicketHash" or "KeyHash" => 64,
            _ => -1
        };
    }
}
