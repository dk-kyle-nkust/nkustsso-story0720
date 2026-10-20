// 唯讀查詢來源帳號與身分類別。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

/// <summary>帳號與身分名稱的唯讀關聯，不讀取密碼，也不以名稱或重設密碼規則決定 SSO 權限。</summary>
public static class SourceAccountReader
{
    public static IQueryable<SourceAccountProfile> Query(DataConnection source, string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        return from account in source.GetTable<SourceAccountRecord>()
               where account.MemberId == memberId
               from status in source.GetTable<SourceAccountStatusRecord>()
                   .LeftJoin(status => status.Code == account.StatusCode)
               select new SourceAccountProfile
               {
                   MemberId = account.MemberId,
                   Name = account.Name,
                   StatusCode = account.StatusCode,
                   StatusName = status.Name
               };
    }

    // 缺少對照仍保留帳號；重複對照也保留，不任意取第一筆作身分判斷。
    public static Task<List<SourceAccountProfile>> FindAsync(
        DataConnection source, string memberId, CancellationToken cancellationToken = default) =>
        Query(source, memberId).ToListAsync(cancellationToken);
}
