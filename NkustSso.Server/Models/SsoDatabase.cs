using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;
using Microsoft.AspNetCore.Identity;
using NkustSso.Server.Models;

namespace NkustSso.Server.Models;

public sealed class DatabaseFactory
{
    private static readonly MappingSchema Mapping = CreateMapping();
    private static readonly MappingSchema SqlMapping = SsoStorage.CreateSqlMapping(Mapping);
    private readonly string? directory;
    private readonly string? sqlConnection;
    public bool IsSqlServer => sqlConnection is not null;
    public DatabaseFactory(string directory) => this.directory = directory;
    private DatabaseFactory(string connection, bool sqlServer) => sqlConnection = SsoStorage.ValidateConnection(connection);
    public static DatabaseFactory ForSqlServer(string connection) => new(connection, true);
    public SsoDatabase Open() => IsSqlServer
        ? new(SsoStorage.SqlOptions(sqlConnection!, SqlMapping), true)
        : new(Path.Combine(directory!, "sso.db"), Mapping);
    public SsoDatabase OpenSessions() => IsSqlServer ? Open() : new(Path.Combine(directory!, "sessions.db"), Mapping);
    internal static SsoDatabase OpenSqliteReadOnly(string file) => new(new DataOptions()
        .UseSQLite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = file,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString, SQLiteProvider.Microsoft)
        .UseMappingSchema(Mapping), false);

    private static MappingSchema CreateMapping()
    {
        var schema = new MappingSchema();
        var builder = new FluentMappingBuilder(schema);
        builder.Entity<Account>().HasTableName("AspNetUsers").Property(u => u.Id).IsPrimaryKey();
        builder.Entity<IdentityRole>().HasTableName("AspNetRoles").Property(r => r.Id).IsPrimaryKey();
        builder.Build();
        return schema;
    }

    // CreateIfNotExists 只建立不存在的資料表，不會替既有資料表補欄位，
    // 因此新增欄位必須個別檢查後 ALTER。表名與欄位定義都是程式內常數，不來自輸入。
    private static void AddColumns(SsoDatabase db, string table, params (string Name, string Definition)[] columns)
    {
        if (db.IsSqlServer)
        {
            var existingSql = db.Query<string>("SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@table)",
                new DataParameter("table", "dbo." + table)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var descriptor = SsoStorage.MainTables.Select(t => db.MappingSchema.GetEntityDescriptor(t)).Single(t => t.TableName == table);
            foreach (var column in columns.Where(c => !existingSql.Contains(c.Name)))
            {
                var mapped = descriptor.Columns.Single(c => c.ColumnName == column.Name);
                var definition = column.Definition.Replace("TEXT", mapped.DbType + " COLLATE Latin1_General_100_BIN2")
                    .Replace("INTEGER", mapped.MemberType == typeof(bool) ? "bit" : "bigint");
                db.Execute($"ALTER TABLE [dbo].[{table}] ADD [{column.Name}] {definition}");
            }
            return;
        }
        var existing = db.Query<string>($"SELECT name FROM pragma_table_info('{table}')").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns.Where(c => !existing.Contains(c.Name)))
            db.Execute($"ALTER TABLE {table} ADD COLUMN {column.Name} {column.Definition}");
    }

    public async Task InitializeAsync(bool seedDefaults = true)
    {
        if (!IsSqlServer) { await InitializeCoreAsync(seedDefaults); return; }
        await using var gate = Open();
        var acquired = await gate.ExecuteAsync<int>("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = N'NkustSso.Schema', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 30000; SELECT @result;");
        if (acquired < 0) throw new InvalidOperationException("無法取得 SSO 資料結構更新鎖定。");
        try { await InitializeCoreAsync(seedDefaults); }
        finally { await gate.ExecuteAsync("EXEC sys.sp_releaseapplock @Resource = N'NkustSso.Schema', @LockOwner = 'Session'"); }
    }

    private async Task InitializeCoreAsync(bool seedDefaults)
    {
        await using var db = Open();
        await db.CreateTableAsync<Account>(tableOptions: TableOptions.CreateIfNotExists);
        AddColumns(db, "AspNetUsers", ("AuthenticationSource", "TEXT NOT NULL DEFAULT ''"), ("SourceAccountId", "TEXT NOT NULL DEFAULT ''"));
        AddColumns(db, "AspNetUsers", ("SourceStatusCode", "TEXT NOT NULL DEFAULT ''"),
            ("SourceStatusName", "TEXT NOT NULL DEFAULT ''"));
        AddColumns(db, "AspNetUsers", ("UnitCode", "TEXT NOT NULL DEFAULT ''"),
            ("UnitName", "TEXT NOT NULL DEFAULT ''"));
        await db.CreateIndexAsync("AspNetUsers", "AccountSourceIndex", "AuthenticationSource, SourceAccountId", unique: true, filter: "AuthenticationSource <> ''");
        await db.CreateTableAsync<IdentityRole>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AccountRole>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<LoginEvent>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<TicketClient>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<RegisteredSystem>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<PortalAudience>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<SystemConfigurationAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IntegrationClient>(tableOptions: TableOptions.CreateIfNotExists);
        AddColumns(db, "IntegrationClients",
            ("CallerName", "TEXT NOT NULL DEFAULT ''"), ("CallerPasswordHash", "TEXT NOT NULL DEFAULT ''"),
            ("AllowedFunctionIds", "TEXT NOT NULL DEFAULT ''"),
            ("ConnectionKind", "TEXT NOT NULL DEFAULT 'existing'"),
            ("Purpose", "TEXT NOT NULL DEFAULT ''"), ("OwningUnit", "TEXT NOT NULL DEFAULT ''"),
            ("SourceSystem", "TEXT NOT NULL DEFAULT ''"), ("SourceId", "TEXT NOT NULL DEFAULT ''"),
            ("ImportBatchId", "TEXT NOT NULL DEFAULT ''"), ("ImportedAt", "INTEGER NULL"),
            ("ApplicantName", "TEXT NOT NULL DEFAULT ''"), ("ContactName", "TEXT NOT NULL DEFAULT ''"),
            ("ContactPhone", "TEXT NOT NULL DEFAULT ''"), ("ContactEmail", "TEXT NOT NULL DEFAULT ''"),
            ("Notes", "TEXT NOT NULL DEFAULT ''"));
        await db.CreateTableAsync<IntegrationAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateIndexAsync("IntegrationAudits", "IntegrationAuditTimeIndex", "OccurredAt");
        AddColumns(db, "RegisteredSystems",
            ("DisplayNameEn", "TEXT NOT NULL DEFAULT ''"),
            ("DescriptionEn", "TEXT NOT NULL DEFAULT ''"),
            ("PublicStudent", "INTEGER NOT NULL DEFAULT 0"),
            ("PublicEmployee", "INTEGER NOT NULL DEFAULT 0"),
            ("PublicAlumni", "INTEGER NOT NULL DEFAULT 0"),
            ("RequirePortalLogin", "INTEGER NOT NULL DEFAULT 0"),
            ("RequireLoginToView", "INTEGER NOT NULL DEFAULT 0"),
            ("Category", "TEXT NOT NULL DEFAULT ''"),
            ("Keywords", "TEXT NOT NULL DEFAULT ''"),
            ("SamlEntityId", "TEXT NOT NULL DEFAULT ''"),
            ("SamlSigningCertificate", "TEXT NOT NULL DEFAULT ''"));
        await db.CreateIndexAsync("RegisteredSystems", "SamlEntityIndex", "SamlEntityId", unique: true, filter: "Protocol = 'saml'");
        await db.CreateTableAsync<PortalCategory>(tableOptions: TableOptions.CreateIfNotExists);
        AddColumns(db, "PortalCategories", ("NameEn", "TEXT NOT NULL DEFAULT ''"));
        await db.CreateTableAsync<PortalCategoryAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<SchemaMigration>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminMenu>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminRole>(tableOptions: TableOptions.CreateIfNotExists);
        AddColumns(db, "AdminRoles", ("StartsAt", "INTEGER NULL"), ("ExpiresAt", "INTEGER NULL"));
        await db.CreateTableAsync<AdminRolePermission>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminRoleMember>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminManagementAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IntegrationCapability>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IntegrationClientService>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IntegrationClientReturnField>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateIndexAsync("IntegrationClients", "IntegrationClientSourceIndex", "SourceSystem, SourceId", unique: true, filter: "SourceSystem <> '' AND SourceId <> ''");
        if (seedDefaults)
        {
            await Services.AdminManagementService.InitializeAsync(db);
            await Services.IntegrationPermissions.MigrateAsync(db);
            await Services.IntegrationReturnFields.MigrateAsync(db);
        }
        AddColumns(db, "PortalAudiences", ("RulesJson", "TEXT NOT NULL DEFAULT ''"));
        await db.CreateTableAsync<IdentityDimension>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IdentityOption>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IdentityCatalogAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AccountIdentitySet>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AccountIdentity>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateIndexAsync("IdentityOptions", "IdentityOptionCodeIndex", "Dimension, Code", unique: true);
        if (seedDefaults)
        {
            await Services.IdentityCatalog.SeedAsync(db);
            await Services.IdentityCatalog.AddStudentKindDimensionAsync(db);
            // 校務來源僅能辨識「教職員」時，不強制誤分為教師或一般職員。
            if (!await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == "campus-employee-identity-v1"))
            {
                if (!await db.GetTable<IdentityOption>().AnyAsync(o => o.Dimension == "identity_type" && o.Code == "employee"))
                    await db.InsertAsync(new IdentityOption { Dimension = "identity_type", Code = "employee", Label = "教職員（未細分）", Family = "employee", SortOrder = 25 });
                await db.InsertAsync(new SchemaMigration { Id = "campus-employee-identity-v1" });
            }
            await Services.PortalCategoryService.MigrateAsync(db);
        }
        await db.CreateTableAsync<OidcApplication>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<OidcAuthorization>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<OidcScope>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<OidcToken>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateIndexAsync("AspNetUsers", "UserNameIndex", "NormalizedUserName", unique: true, filter: "NormalizedUserName IS NOT NULL");
        await db.CreateIndexAsync("AspNetRoles", "RoleNameIndex", "NormalizedName", unique: true, filter: "NormalizedName IS NOT NULL");
        await db.CreateIndexAsync("OidcApplications", "OidcClientIndex", "ClientId", unique: true, filter: "ClientId IS NOT NULL");
        await db.CreateIndexAsync("OidcScopes", "OidcScopeNameIndex", "Name", unique: true, filter: "Name IS NOT NULL");
        await db.CreateIndexAsync("OidcTokens", "OidcReferenceIndex", "ReferenceId", unique: true, filter: "ReferenceId IS NOT NULL");
        await db.CreateIndexAsync("OidcTokens", "OidcTokenAuthIndex", "AuthorizationId");
        await using var sessions = OpenSessions();
        await sessions.CreateTableAsync<LoginSession>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<SessionAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<CampusLoginAttempt>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<LoginTicket>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<IntegrationConsumedTicket>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<TicketAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<SamlLoginRequest>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateTableAsync<SamlAudit>(tableOptions: TableOptions.CreateIfNotExists);
        await sessions.CreateIndexAsync("SamlLoginRequests", "SamlRequestReplayIndex", "RequestHash", unique: true);
        await sessions.CreateIndexAsync("LoginTickets", "LoginTicketHashIndex", "TicketHash", unique: true);
        await sessions.CreateIndexAsync("LoginTickets", "LoginTicketExpiryIndex", "ExpiresAt");
        await sessions.CreateIndexAsync("TicketAudits", "TicketAuditTimeIndex", "OccurredAt");
        await sessions.CreateIndexAsync("LoginSessions", "SessionKeyIndex", "KeyHash", unique: true);
        await sessions.CreateIndexAsync("LoginSessions", "SessionAccountIndex", "AccountId, ExpiresAt");
    }
}

public sealed class SsoDatabase : DataConnection
{
    public bool IsSqlServer { get; }
    public SsoDatabase(DataOptions options, bool isSqlServer) : base(options) => IsSqlServer = isSqlServer;
    public SsoDatabase(string file, MappingSchema mapping) : base(new DataOptions()
        .UseSQLite($"Data Source={file}", SQLiteProvider.Microsoft).UseMappingSchema(mapping)) { }

    // 識別字與篩選式只接受程式內固定值，不傳入使用者輸入。
    internal async Task CreateIndexAsync(string table, string name, string columns, bool unique = false, string? filter = null)
    {
        if (!IsSqlServer)
        {
            await this.ExecuteAsync($"CREATE {(unique ? "UNIQUE " : "")}INDEX IF NOT EXISTS {name} ON {table} ({columns}){(filter is null ? "" : " WHERE " + filter)}");
            return;
        }
        // Entity ID 可長達 1000 字元，使用完整值的 SHA-256 避免索引鍵長度限制。
        if (name == "SamlEntityIndex")
        {
            await this.ExecuteAsync("IF COL_LENGTH('dbo.RegisteredSystems', 'SamlEntityHash') IS NULL ALTER TABLE dbo.RegisteredSystems ADD SamlEntityHash AS CONVERT(binary(32), HASHBYTES('SHA2_256', SamlEntityId)) PERSISTED");
            columns = "SamlEntityHash";
        }
        await this.ExecuteAsync($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{name}') CREATE {(unique ? "UNIQUE " : "")}INDEX [{name}] ON [dbo].[{table}] ({columns}){(filter is null ? "" : " WHERE " + filter)}");
    }
    public ITable<Account> Users => this.GetTable<Account>();
    public ITable<IdentityRole> Roles => this.GetTable<IdentityRole>();
    public ITable<AccountRole> UserRoles => this.GetTable<AccountRole>();
    public ITable<LoginEvent> LoginEvents => this.GetTable<LoginEvent>();
    public ITable<LoginSession> LoginSessions => this.GetTable<LoginSession>();
    public ITable<SessionAudit> SessionAudits => this.GetTable<SessionAudit>();
    public ITable<TicketClient> TicketClients => this.GetTable<TicketClient>();
    public ITable<RegisteredSystem> RegisteredSystems => this.GetTable<RegisteredSystem>();
    public ITable<PortalCategory> PortalCategories => this.GetTable<PortalCategory>();
    public ITable<PortalCategoryAudit> PortalCategoryAudits => this.GetTable<PortalCategoryAudit>();
    public ITable<PortalAudience> PortalAudiences => this.GetTable<PortalAudience>();
    public ITable<SystemConfigurationAudit> SystemConfigurationAudits => this.GetTable<SystemConfigurationAudit>();
    public ITable<LoginTicket> LoginTickets => this.GetTable<LoginTicket>();
    public ITable<TicketAudit> TicketAudits => this.GetTable<TicketAudit>();
}
