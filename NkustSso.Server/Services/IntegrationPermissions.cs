// 定義並初始化 JSON／SOAP 可授權的服務項目。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public static class IntegrationPermissions
{
    public const string VerifyAccount = "account.verify";

    public static async Task MigrateAsync(SsoDatabase db)
    {
        await db.CreateTableAsync<IntegrationCapability>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<IntegrationClientService>(tableOptions: TableOptions.CreateIfNotExists);

        // 同一組來源系統與原系統識別碼只能有一筆登記；空值不納入唯一性。
        await db.CreateIndexAsync(
            "IntegrationClients",
            "IntegrationClientSourceIndex",
            "SourceSystem, SourceId",
            unique: true,
            filter: "SourceSystem <> '' AND SourceId <> ''");

        await using var transaction = await db.BeginTransactionAsync();
        await InsertCapabilitiesAsync(db);
        await KeepExistingGrantsAsync(db);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 每個已完成的 SOAP 方法對應一個可授權的服務項目，帳密驗證另外列為共用項目。
    /// </summary>
    private static async Task InsertCapabilitiesAsync(SsoDatabase db)
    {
        if (!await db.GetTable<IntegrationCapability>().AnyAsync(s => s.Id == VerifyAccount))
        {
            await db.InsertAsync(new IntegrationCapability { Id = VerifyAccount, Name = "帳密驗證" });
        }

        var methods = SoapHelpCatalog.Operations
            .Where(o => LegacySoapOperationCatalog.Available.Contains(o.Name)
                && o.Name != LegacySoapContract.Operation);

        foreach (var method in methods)
        {
            var id = LegacySoapOperationCatalog.Permission(method.Name);
            if (await db.GetTable<IntegrationCapability>().AnyAsync(s => s.Id == id))
            {
                continue;
            }

            await db.InsertAsync(new IntegrationCapability
            {
                Id = id,
                Name = method.Title + "（" + method.Name + "）"
            });
        }
    }

    /// <summary>
    /// 升級前只有帳密驗證：一次性保留原本的服務資格，日後取消不會被重啟還原。
    /// </summary>
    private static async Task KeepExistingGrantsAsync(SsoDatabase db)
    {
        const string migration = "integration-service-grants-v1";
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            return;
        }

        foreach (var id in await db.GetTable<IntegrationClient>().Select(c => c.Id).ToListAsync())
        {
            var granted = await db.GetTable<IntegrationClientService>()
                .AnyAsync(g => g.ClientId == id && g.ServiceId == VerifyAccount);
            if (!granted)
            {
                await db.InsertAsync(new IntegrationClientService
                {
                    ClientId = id,
                    ServiceId = VerifyAccount
                });
            }

            await db.InsertAsync(new IntegrationAudit
            {
                ClientId = id,
                ActorId = "schema-migration",
                Action = "migrate",
                OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Detail = "升級服務授權結構，保留原有帳密驗證資格；啟用狀態不變。"
            });
        }

        await db.InsertAsync(new SchemaMigration { Id = migration });
    }
}
