// 唯讀查詢校務單位代碼與名稱。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

/// <summary>讀取遠端單位資料，不更新來源排序或建立本地單位資料表。</summary>
public static class UnitReader
{
    public static IQueryable<UnitRecord> Query(DataConnection source) =>
        source.GetTable<UnitRecord>().Select(row => new UnitRecord
        {
            Code = row.Code,
            Name = row.Name,
            ShortName = row.ShortName,
            CampusCode = row.CampusCode,
            SourceSortOrder = row.SourceSortOrder
        });

    /// <summary>
    /// 按單位代碼查名稱。取兩筆就夠判斷「不只一筆」：`unt_id` 是否跨校區唯一尚未
    /// 以實際資料確認，所以由呼叫端在不唯一時留空，而不是任取第一筆。
    /// </summary>
    public static Task<List<UnitRecord>> FindAsync(
        DataConnection source,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return Query(source)
            .Where(row => row.Code == code)
            .Take(2)
            .ToListAsync(cancellationToken);
    }

    // 本地覆寫順序須在整合層套用，此處僅保留來源提供的順序。
    public static Task<List<UnitRecord>> ListAsync(
        DataConnection source,
        CancellationToken cancellationToken = default)
    {
        return Query(source)
            .OrderBy(row => row.SourceSortOrder)
            .ThenBy(row => row.Code)
            .ThenBy(row => row.CampusCode)
            .ToListAsync(cancellationToken);
    }
}
