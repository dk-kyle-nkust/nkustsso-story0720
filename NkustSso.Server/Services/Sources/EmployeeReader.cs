// 依員編唯讀查詢教職員基本資料。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

public static class EmployeeReader
{
    public static IQueryable<EmployeeRecord> Query(DataConnection source, string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        if (employeeId.Length > 6)
        {
            throw new ArgumentException("員編不得超過 6 個字元。", nameof(employeeId));
        }

        return source.GetTable<EmployeeRecord>().Where(row => row.EmployeeId == employeeId)
            .Select(row => new EmployeeRecord
            {
                EmployeeId = row.EmployeeId,
                Name = row.Name,
                UnitCode = row.UnitCode,
                WorkUnitCode = row.WorkUnitCode,
                TitleCode = row.TitleCode,
                DivisionCode = row.DivisionCode,
                EmploymentStatusCode = row.EmploymentStatusCode,
                LeaveDate = row.LeaveDate,
                CampusCode = row.CampusCode
            });
    }

    // 不以員編開頭、姓名或日期是否空白推定 SSO 可登入狀態。
    public static Task<List<EmployeeRecord>> FindAsync(
        DataConnection source, string employeeId, CancellationToken cancellationToken = default) =>
        Query(source, employeeId).ToListAsync(cancellationToken);
}
