// 以新校務資料提供舊 SOAP 方法需要的查詢結果。
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinqToDB;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

public interface ICampusSoapQueries
{
    Task<ServiceVerification> RedeemAsync(
        string account,
        string function,
        string random,
        CancellationToken token);

    Task<DataSet> TrafficAsync(string operation, string search, CancellationToken token);

    Task<DataSet> DocumentPaymentAsync(
        string idNumber,
        string birthday,
        string identity,
        CancellationToken token);
}

public sealed class CampusSoapQueries(
    ICampusDatabaseFactory campus,
    DatabaseFactory local,
    TimeProvider clock) : ICampusSoapQueries
{
    /// <summary>票據有效期限，與舊版 SOAP 介面一致。</summary>
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);

    /// <summary>查詢上限多取一筆，用來判斷「結果不只一筆」。</summary>
    private const int ProbeCount = 2;

    /// <summary>文件申請最多回傳 100 筆，多取一筆用來偵測異常大量結果。</summary>
    private const int DocumentPaymentLimit = 101;

    public async Task<DataSet> DocumentPaymentAsync(
        string idNumber,
        string birthday,
        string identity,
        CancellationToken token)
    {
        idNumber = idNumber.Trim();
        birthday = birthday.Trim();

        // 沿用原 SQL 的生日字串比對，不猜測日期轉換或新畢業代碼。
        if (idNumber.Length < 1 || idNumber.Length > 100)
        {
            return Error("500");
        }

        if (birthday.Length < 1 || birthday.Length > 7)
        {
            return Error("500");
        }

        if (identity != "STUD" && identity != "Employee" && identity != "GRADSTUD")
        {
            return Error("500");
        }

        await using var db = campus.Open();

        var rows = identity == "Employee"
            ? await FindPaymentEmployeesAsync(db, idNumber, birthday, token)
            : await FindPaymentStudentsAsync(db, idNumber, birthday, identity, token);

        if (rows is null || rows.Count == 0)
        {
            return Error("500");
        }

        // 原查詢可回傳多個學號；不任選第一筆。同一帳號有衝突資料或異常大量結果則拒絕。
        if (rows.Count > 100)
        {
            throw new InvalidOperationException("文件申請資料無法唯一對應帳號。");
        }

        if (rows.GroupBy(r => r.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
        {
            throw new InvalidOperationException("文件申請資料無法唯一對應帳號。");
        }

        return DocumentPaymentTable(rows);
    }

    private static async Task<List<DocumentPaymentRow>> FindPaymentEmployeesAsync(
        IDataContext db,
        string idNumber,
        string birthday,
        CancellationToken token)
    {
        return await (from e in db.GetTable<CampusSoapEmployee>()
                      join a in db.GetTable<CampusSoapAccount>() on e.Id equals a.Id
                      join u in db.GetTable<CampusSoapUnit>() on e.WorkUnit equals u.Id
                      where e.IdNumber == idNumber
                          && e.Birthday == birthday
                          && e.Campus != null
                          && e.Campus == a.Campus
                          && e.Campus == u.Campus
                          && db.GetTable<CampusSoapTitle>()
                              .Any(t => t.Id == e.Title && t.Campus == e.Campus)
                          && db.GetTable<CampusSoapDepartment>()
                              .Any(d => d.Id == u.Department && d.Campus == e.Campus)
                          && db.GetTable<SourceAccountStatusRecord>().Any(s => s.Code == a.Status)
                      select new DocumentPaymentRow
                      {
                          Id = e.Id,
                          Name = e.Name,
                          Email = e.Email,
                          Campus = e.Campus
                      })
            .Distinct()
            .Take(DocumentPaymentLimit)
            .ToListAsync(token);
    }

    /// <summary>
    /// 回傳 null 代表已判定為無資料（GRADSTUD 但仍有當期學籍）。
    /// </summary>
    private static async Task<List<DocumentPaymentRow>?> FindPaymentStudentsAsync(
        IDataContext db,
        string idNumber,
        string birthday,
        string identity,
        CancellationToken token)
    {
        var (year, semester) = await CurrentTermAsync(db, minimumYear: 1, token);

        var candidates = from s in db.GetTable<CampusSoapStudent>()
                         join a in db.GetTable<CampusSoapAccount>() on s.Id equals a.Id
                         where s.IdNumber == idNumber
                             && s.Birthday == birthday
                             && a.Status == "02"
                             && s.Campus != null
                             && s.Campus == a.Campus
                             && db.GetTable<SourceAccountStatusRecord>().Any(t => t.Code == a.Status)
                         select new { s.Key, s.Id, s.Name, s.Email, s.Campus };

        var current = candidates.Where(s => db.GetTable<StudentTermRecord>()
            .Any(r => r.StudentKey == s.Key && r.Year == year && r.Semester == semester));

        // 舊校務分支：同一證號／生日只要有當期學籍，就拒絕 GRADSTUD（包含另有舊學號的情況）。
        if (identity == "GRADSTUD" && await current.AnyAsync(token))
        {
            return null;
        }

        var source = identity == "STUD" ? current : candidates;

        return await source
            .Select(s => new DocumentPaymentRow
            {
                Id = s.Id,
                Name = s.Name,
                Email = s.Email,
                Campus = s.Campus
            })
            .Distinct()
            .Take(DocumentPaymentLimit)
            .ToListAsync(token);
    }

    private static DataSet DocumentPaymentTable(List<DocumentPaymentRow> rows)
    {
        var result = Table(
            ["message", "std_id", "std_name", "std_email"],
            ["200", "", "", ""]);

        // Table 會先放一列樣板，這裡改放實際結果。
        result.Tables[0].Rows.Clear();
        foreach (var row in rows.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            result.Tables[0].Rows.Add("200", row.Id, row.Name ?? "", row.Email ?? "");
        }

        return result;
    }

    private sealed class DocumentPaymentRow
    {
        public string Id { get; set; } = "";

        public string? Name { get; set; }

        public string? Email { get; set; }

        public int? Campus { get; set; }
    }

    public async Task<ServiceVerification> RedeemAsync(
        string account,
        string function,
        string random,
        CancellationToken token)
    {
        if (account.Length < 1 || account.Length > 100)
        {
            return new ServiceVerification(false);
        }

        if (function.Length < 1 || function.Length > 100)
        {
            return new ServiceVerification(false);
        }

        var parsed = int.TryParse(random, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
        if (!parsed)
        {
            return new ServiceVerification(false);
        }

        await using var db = campus.Open();
        var rows = await (from t in db.GetTable<CampusSoapTicket>()
                          join a in db.GetTable<CampusSoapAccount>() on t.Account equals a.Id
                          where t.Account == account && t.Random == number && t.Function == function
                          select new { t.Serial, t.IssuedAt, a.Status })
            .Take(ProbeCount)
            .ToListAsync(token);

        if (rows.Count != 1 || rows[0].IssuedAt is not DateTime issued)
        {
            return new ServiceVerification(false);
        }

        if (string.IsNullOrWhiteSpace(rows[0].Status))
        {
            return new ServiceVerification(false);
        }

        // 校務資料庫存的是台北時間，先轉成 UTC 再比對。
        var issuedUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(issued, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"));

        var age = clock.GetUtcNow().UtcDateTime - issuedUtc;

        // 修正舊版只比較 Hours／Minutes 造成隔天票據及未來時間仍可能通過的漏洞。
        if (age < TimeSpan.Zero || age >= TicketLifetime)
        {
            return new ServiceVerification(false);
        }

        var payload = JsonSerializer.Serialize(new { account, function, number, rows[0].Serial, issuedUtc });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        if (!await MarkTicketConsumedAsync(hash, issuedUtc, token))
        {
            return new ServiceVerification(false);
        }

        return new ServiceVerification(true, rows[0].Status!.Trim());
    }

    /// <summary>
    /// 以唯一鍵記錄已兌換的票據；插入撞鍵代表這張票已經被用過了。
    /// </summary>
    private async Task<bool> MarkTicketConsumedAsync(string hash, DateTime issuedUtc, CancellationToken token)
    {
        await using var sessions = local.OpenSessions();
        try
        {
            await sessions.InsertAsync(
                new IntegrationConsumedTicket
                {
                    Hash = hash,
                    ExpiresAt = new DateTimeOffset(issuedUtc.Add(TicketLifetime)).ToUnixTimeMilliseconds()
                },
                token: token);
            return true;
        }
        catch (Exception e) when (SsoStorage.IsUniqueViolation(e))
        {
            return false;
        }
    }

    public async Task<DataSet> TrafficAsync(string operation, string search, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Length > 100)
        {
            return Error("500");
        }

        await using var db = campus.Open();

        if (operation == "Get_Traffic_Pub")
        {
            return await PublicAccountAsync(db, search, token);
        }

        if (operation == "Get_Traffic_Teach" || operation == "Get_Traffic_Teach_Temp")
        {
            return await EmployeeAsync(db, search, token);
        }

        if (operation != "Get_Traffic_Stud" && operation != "Get_Traffic_Stud_Temp")
        {
            throw new InvalidOperationException("未知的交通查詢。");
        }

        return await StudentAsync(db, operation, search, token);
    }

    private static async Task<DataSet> PublicAccountAsync(
        IDataContext db,
        string search,
        CancellationToken token)
    {
        var accounts = await db.GetTable<CampusSoapAccount>()
            .Where(a => a.Id.Contains("office") && (a.Id == search || a.IdNumber == search))
            .Take(ProbeCount)
            .ToListAsync(token);

        if (accounts.Count == 0)
        {
            return Error("500");
        }

        if (accounts.Count != 1)
        {
            throw new InvalidOperationException("公用帳號資料不唯一。");
        }

        var a = accounts[0];
        return Table(
            ["message", "pubusr_id", "pubusr_name", "pubusr_idno", "pub_name_abr", "types", "pwd", "usr_email"],
            ["200", a.Id, a.Name, a.IdNumber, a.Name, "P", "", a.Email]);
    }

    private static async Task<DataSet> EmployeeAsync(
        IDataContext db,
        string search,
        CancellationToken token)
    {
        var rows = await (from e in db.GetTable<CampusSoapEmployee>()
                          join a in db.GetTable<CampusSoapAccount>() on e.Id equals a.Id
                          join u in db.GetTable<CampusSoapUnit>() on e.Unit equals u.Id
                          where (a.Id == search || e.IdNumber == search)
                              && e.Status != null
                              && e.Status != "0L"
                              && e.Campus != null
                              && e.Campus == u.Campus
                              && e.Campus == a.Campus
                          select new
                          {
                              e.Id,
                              e.Name,
                              e.IdNumber,
                              u.Department,
                              UnitName = u.Name,
                              Account = a.Id,
                              e.Email
                          })
            .Take(ProbeCount)
            .ToListAsync(token);

        if (rows.Count == 0)
        {
            return Error("500");
        }

        if (rows.Count != 1)
        {
            throw new InvalidOperationException("教職員資料不唯一。");
        }

        var r = rows[0];
        return Table(
            [
                "message", "emp_id", "emp_name", "emp_idno", "unt_id",
                "unt_name_abr", "types", "usr_id", "pwd", "emp_email"
            ],
            [
                "200", r.Id, r.Name, IdDigest(r.IdNumber), r.Department,
                r.UnitName, "T", r.Account, "", r.Email
            ]);
    }

    private static async Task<DataSet> StudentAsync(
        IDataContext db,
        string operation,
        string search,
        CancellationToken token)
    {
        var (year, semester) = await CurrentTermAsync(db, minimumYear: 90, token);

        // Temp 版本只看在學狀態的歷年學籍。
        var temp = operation == "Get_Traffic_Stud_Temp";

        var students = await (from r in db.GetTable<StudentTermRecord>()
                              join s in db.GetTable<CampusSoapStudent>() on r.StudentKey equals s.Key
                              join c in db.GetTable<ClassRecord>() on r.ClassCode equals c.Code
                              join u in db.GetTable<CampusSoapUnit>() on c.UnitCode equals u.Id
                              join a in db.GetTable<CampusSoapAccount>() on s.Id equals a.Id
                              where (s.Id == search || s.IdNumber == search)
                                  && c.CampusCode >= 1
                                  && c.CampusCode <= 5
                                  && s.Campus != null
                                  && s.Campus == c.CampusCode
                                  && s.Campus == u.Campus
                                  && s.Campus == a.Campus
                                  && ((r.Year == year && r.Semester == semester)
                                      || (r.Year >= 90 && r.Year <= year && (!temp || r.StatusCode == "1")))
                              select new
                              {
                                  s.Id,
                                  s.Name,
                                  s.IdNumber,
                                  u.Department,
                                  UnitName = u.Name,
                                  Account = a.Id,
                                  s.Email
                              })
            .Distinct()
            .Take(ProbeCount)
            .ToListAsync(token);

        if (students.Count == 0)
        {
            return Error("500");
        }

        if (students.Count != 1)
        {
            throw new InvalidOperationException("學生資料有多筆不同對應。");
        }

        var student = students[0];
        return Table(
            [
                "message", "std_id", "std_name", "std_idno", "unt_id",
                "unt_name_abr", "types", "usr_id", "pwd", "std_email"
            ],
            [
                "200",
                student.Id,
                student.Name,
                IdDigest(student.IdNumber),
                student.Department,
                student.UnitName,
                "S",
                student.Account,
                "",
                student.Email
            ]);
    }

    /// <summary>
    /// 取出唯一一筆標記為當期的學年期；資料不唯一或數值異常時拒絕，不自行猜測。
    /// </summary>
    private static async Task<(int Year, int Semester)> CurrentTermAsync(
        IDataContext db,
        int minimumYear,
        CancellationToken token)
    {
        var terms = await db.GetTable<AcademicTermRecord>()
            .Where(t => t.CurrentMark == "Y")
            .Select(t => new { t.Year, t.Semester })
            .Distinct()
            .Take(ProbeCount)
            .ToListAsync(token);

        if (terms.Count != 1)
        {
            throw new InvalidOperationException("目前學年期無法確認。");
        }

        var year = terms[0].Year;
        var semester = terms[0].Semester;

        if (year < minimumYear || year > int.MaxValue || decimal.Truncate(year) != year)
        {
            throw new InvalidOperationException("目前學年期無法確認。");
        }

        if (semester < 1 || semester > 2)
        {
            throw new InvalidOperationException("目前學年期無法確認。");
        }

        return ((int)year, (int)semester);
    }

    // MD5 僅為舊回傳契約的身分證號摘要，不用於密碼驗證，也不讀取或輸出密碼衍生值。
    private static string IdDigest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value.Trim())));
    }

    public static DataSet Error(string code)
    {
        return Table(["message"], [code]);
    }

    private static DataSet Table(string[] columns, string?[] values)
    {
        var table = new DataTable("Table1");
        foreach (var column in columns)
        {
            table.Columns.Add(column, typeof(string));
        }

        table.Rows.Add(values.Select(v => (object)(v ?? "")).ToArray());

        var result = new DataSet("NewDataSet");
        result.Tables.Add(table);
        return result;
    }
}
