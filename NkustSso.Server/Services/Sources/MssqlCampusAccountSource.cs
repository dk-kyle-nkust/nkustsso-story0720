// 由新校務 MSSQL 驗證帳密及讀取帳號狀態。
using LinqToDB;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

public sealed class MssqlCampusAccountSource(
    ICampusDatabaseFactory database,
    IOptions<CampusAuthenticationOptions> options,
    IHostEnvironment environment) : ICampusAccountSource
{
    public async Task<CampusAccountProfile?> AuthenticateAsync(
        string memberId,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 256)
        {
            return null;
        }

        await using var db = database.Open();

        // 取兩筆就夠判斷「結果不只一筆」。
        var rows = await db.GetTable<CampusCredentialRecord>()
            .Where(a => a.MemberId == memberId)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rows.Count != 1)
        {
            return null;
        }

        var account = rows[0];
        var type = options.Value.IdentityType(account.StatusCode?.Trim());
        // 測試開關只影響中央登入；仍需唯一來源帳號，且不能在其他環境略過密碼。
        var developmentLogin = environment.IsDevelopment() && options.Value.DevelopmentAllowAnyPassword;
        var verified = developmentLogin || TienFangPasswordVerifier.Verify(account.EncodedPassword, password);
        account.EncodedPassword = null;
        if (!verified)
        {
            return null;
        }

        var canonicalId = account.MemberId.Trim();
        if (canonicalId.Length < 1 || canonicalId.Length > 100)
        {
            return null;
        }

        var statusCode = account.StatusCode?.Trim() ?? "";
        var statusNames = await db.GetTable<SourceAccountStatusRecord>()
            .Where(s => s.Code == statusCode)
            .Select(s => s.Name)
            .Take(2)
            .ToListAsync(cancellationToken);
        // 顯示名稱沿用校務對照表；缺漏或代碼不唯一時不猜測人員狀態。
        var statusName = statusNames.Count == 1 ? statusNames[0]?.Trim() ?? "" : "";
        var fallback = new CampusAccountProfile(canonicalId, canonicalId, type,
            SourceStatusCode: statusCode, SourceStatusName: statusName);
        if (type == "student")
        {
            if (canonicalId.Length > 10)
            {
                return fallback;
            }

            var students = await StudentReader.QueryWithClass(db, canonicalId)
                .Take(2)
                .ToListAsync(cancellationToken);

            // 基本資料僅補充顯示；缺漏或關聯不唯一時，不任取一筆也不阻擋已驗證的帳號。
            if (students.Count != 1)
            {
                return fallback;
            }

            var student = students[0];
            return new CampusAccountProfile(
                canonicalId,
                string.IsNullOrWhiteSpace(student.Name) ? canonicalId : student.Name.Trim(),
                type,
                student.ClassCode?.Trim() ?? "",
                student.ClassName?.Trim() ?? "",
                student.DegreeCode?.Trim() ?? "",
                statusCode,
                statusName);
        }

        var isEmployee = type == "employee" || type == "teacher" || type == "staff";
        // 校務代碼 10 為已離職人員，仍依員編取得人事姓名；不將其改成在職身分。
        var hasEmployeeNameSource = isEmployee || statusCode == "10";
        if (!hasEmployeeNameSource || canonicalId.Length > 6)
        {
            return fallback;
        }

        var employees = await EmployeeReader.Query(db, canonicalId)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (employees.Count != 1)
        {
            return fallback;
        }

        var employee = employees[0];
        var unitCode = employee.UnitCode.Trim();
        return new CampusAccountProfile(
            canonicalId,
            string.IsNullOrWhiteSpace(employee.Name) ? canonicalId : employee.Name!.Trim(),
            type,
            SourceStatusCode: statusCode,
            SourceStatusName: statusName,
            UnitCode: unitCode,
            UnitName: await UnitNameAsync(db, unitCode, cancellationToken));
    }

    /// <summary>
    /// 所屬單位（emp_untid）的名稱，只用於顯示。單位關聯尚未以實際資料核對，
    /// 所以代碼空白或對照不唯一時回空字串，不猜單位也不擋已驗證的帳號。
    /// </summary>
    private static async Task<string> UnitNameAsync(
        LinqToDB.Data.DataConnection db,
        string unitCode,
        CancellationToken cancellationToken)
    {
        if (unitCode.Length == 0)
        {
            return "";
        }

        var units = await UnitReader.FindAsync(db, unitCode, cancellationToken);
        if (units.Count != 1)
        {
            return "";
        }

        return units[0].Name?.Trim() ?? "";
    }

    public async Task<bool> IsAllowedAsync(
        string memberId,
        string identityType,
        CancellationToken cancellationToken)
    {
        await using var db = database.Open();
        // Session 檢查只讀狀態，避免每次使用 Cookie 都載入來源密碼。
        var states = await db.GetTable<SourceAccountRecord>().Where(a => a.MemberId == memberId)
            .Select(a => a.StatusCode).Take(2).ToListAsync(cancellationToken);
        // 分類改變時要求重新登入以更新身分快照；任何 sts_id 都可重新驗證帳密。
        return states.Count == 1 && options.Value.IdentityType(states[0]) == identityType;
    }
}
