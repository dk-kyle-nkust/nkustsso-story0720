// 唯讀查詢校務部別代碼與名稱。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

/// <summary>使用呼叫端提供的遠端唯讀連線；不使用本地 DatabaseFactory。</summary>
public static class DivisionReader
{
    /// <summary>保留 IQueryable，供後續身分查詢在資料庫端組合篩選與關聯。</summary>
    public static IQueryable<DivisionRecord> Query(DataConnection source) =>
        source.GetTable<DivisionRecord>().Select(row => new DivisionRecord
        {
            Code = row.Code,
            Name = row.Name,
            SchoolId = row.SchoolId,
            CampusCode = row.CampusCode
        });

    // 只提供穩定的來源排序；前端顯示順序待與本地管理設定合併。
    public static Task<List<DivisionRecord>> ListAsync(
        DataConnection source,
        CancellationToken cancellationToken = default)
    {
        return Query(source)
            .OrderBy(row => row.SchoolId)
            .ThenBy(row => row.CampusCode)
            .ThenBy(row => row.Code)
            .ToListAsync(cancellationToken);
    }
}
