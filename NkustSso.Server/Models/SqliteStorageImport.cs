using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace NkustSso.Server.Models;

/// <summary>離線匯入：來源唯讀，目的庫須無資料；全部資料在單一交易內保存。</summary>
public static class SqliteStorageImport
{
    public static async Task<IReadOnlyDictionary<string, long>> ImportAsync(DatabaseFactory target, string directory)
    {
        if (!target.IsSqlServer) throw new InvalidOperationException("匯入目的地必須是 SQL Server。");
        var mainFile = Path.Combine(Path.GetFullPath(directory), "sso.db");
        var sessionsFile = Path.Combine(Path.GetFullPath(directory), "sessions.db");
        if (!File.Exists(mainFile) || !File.Exists(sessionsFile))
            throw new InvalidOperationException("請提供停站備份中的 sso.db 與 sessions.db；來源檔案不會被建立或修改。");
        foreach (var file in new[] { mainFile, sessionsFile })
            if (File.Exists(file + "-wal") && new FileInfo(file + "-wal").Length > 0)
                throw new InvalidOperationException("來源仍有 WAL 資料，請先停站並產生完整 SQLite 備份，不能只複製主檔。");
        using var mainGuard = File.Open(mainFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sessionsGuard = File.Open(sessionsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var main = DatabaseFactory.OpenSqliteReadOnly(mainFile);
        await using var sessions = DatabaseFactory.OpenSqliteReadOnly(sessionsFile);
        await using var destination = target.Open();
        await EnsureEmptyAsync(destination);
        await target.InitializeAsync(seedDefaults: false);
        await using var transaction = await destination.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await EnsureEmptyAsync(destination, lockTables: true);
        var counts = new Dictionary<string, long>();
        foreach (var type in SsoStorage.MainTables.Concat(SsoStorage.SessionTables))
        {
            var source = SsoStorage.SessionTables.Contains(type) ? sessions : main;
            var method = typeof(SqliteStorageImport).GetMethod(nameof(CopyAsync), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);
            var count = await (Task<long>)method.Invoke(null, [source, destination])!;
            counts[destination.MappingSchema.GetEntityDescriptor(type).TableName] = count;
        }
        await transaction.CommitAsync();
        return counts;
    }

    private static async Task EnsureEmptyAsync(SsoDatabase database, bool lockTables = false)
    {
        var tables = await database.QueryToListAsync<string>("SELECT QUOTENAME(s.name) + '.' + QUOTENAME(t.name) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0");
        foreach (var table in tables)
        {
            if (await database.ExecuteAsync<long>($"SELECT COUNT_BIG(*) FROM {table}{(lockTables ? " WITH (TABLOCKX, HOLDLOCK)" : "")}") != 0)
                throw new InvalidOperationException("目的資料庫已有資料，拒絕合併或覆寫；請使用空的 SSO 資料庫。");
        }
    }

    private static async Task<long> CopyAsync<T>(SsoDatabase source, SsoDatabase destination) where T : class
    {
        List<T> rows;
        if (typeof(T) == typeof(AdminRole))
        {
            // 舊 SQLite 尚無授權期限欄位時以不限期匯入，不修改唯讀來源備份。
            var existing = (await source.QueryToListAsync<string>("SELECT name FROM pragma_table_info('AdminRoles')")).ToHashSet(StringComparer.Ordinal);
            var extra = new[] { "StartsAt", "ExpiresAt" }.Where(name => !existing.Contains(name)).Select(name => ", NULL AS " + name);
            rows = await source.QueryToListAsync<T>("SELECT *" + string.Concat(extra) + " FROM AdminRoles");
        }
        else if (typeof(T) == typeof(Account))
        {
            // 舊備份尚無來源狀態與單位快照；以空值匯入，待下次登入更新，不修改來源檔案。
            var existing = (await source.QueryToListAsync<string>("SELECT name FROM pragma_table_info('AspNetUsers')"))
                .ToHashSet(StringComparer.Ordinal);
            var extra = new[] { "SourceStatusCode", "SourceStatusName", "UnitCode", "UnitName" }
                .Where(name => !existing.Contains(name)).Select(name => ", '' AS " + name);
            rows = await source.QueryToListAsync<T>("SELECT *" + string.Concat(extra) + " FROM AspNetUsers");
        }
        else rows = await source.GetTable<T>().ToListAsync();
        var columns = source.MappingSchema.GetEntityDescriptor(typeof(T)).Columns;
        if (rows.Count != 0)
            await destination.BulkCopyAsync(new BulkCopyOptions
            { KeepIdentity = columns.Any(c => c.IsIdentity), BulkCopyType = BulkCopyType.MultipleRows }, rows);
        var copied = await destination.GetTable<T>().ToListAsync();
        string Fingerprint(T row) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            columns.Select(c => c.MemberAccessor.GetValue(row)).ToArray())));
        if (copied.Count != rows.Count || !rows.Select(Fingerprint).Order().SequenceEqual(copied.Select(Fingerprint).Order()))
            throw new InvalidOperationException("匯入筆數或資料內容不一致，交易已取消。");
        return copied.Count;
    }
}
