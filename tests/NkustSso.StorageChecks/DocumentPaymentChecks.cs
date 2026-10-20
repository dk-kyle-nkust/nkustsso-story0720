using System.Data;
using LinqToDB;
using LinqToDB.Async;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services.Sources;

internal static class DocumentPaymentChecks
{
    public static async Task RunAsync(CampusSoapQueries queries, CampusCatalogChecks.ReadOnlyFixture source)
    {
        async Task<DataSet> Query(string id, string birth, string kind) => await queries.DocumentPaymentAsync(id, birth, kind, default);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static bool Success(DataSet data) => data.Tables[0].Rows[0]["message"].ToString() == "200";
        using var student = await Query(" A100000001 ", " 0900102 ", "STUD");
        Check(Success(student) && student.Tables[0].Rows[0]["std_id"].ToString() == "S0001", "學生依原 SQL 及前後空白規則查詢。");
        Check(student.Tables[0].Columns.Cast<DataColumn>().Select(c => c.ColumnName).SequenceEqual(new[] { "message", "std_id", "std_name", "std_email" }), "保留校務分支欄位順序。");
        using var employee = await Query("A100000002", "0700304", "Employee");
        Check(Success(employee) && employee.Tables[0].Rows[0]["std_id"].ToString() == "E0001", "員工亦沿用 std_ 欄位。");
        foreach (var (id, birth, kind) in new[] {
            ("A100000001", "0900103", "STUD"), ("A100000001", "20010102", "STUD"),
            ("A100000001", "", "STUD"), ("A100000001", "0900102", "stud"),
            ("A100000001", "0900102", "GRADSTUD"), ("A100000002", "0700304", "STUD"),
            ("' OR 1=1 --", "0900102", "STUD"), ("", "0900102", "STUD") })
        { using var data = await Query(id, birth, kind); Check(!Success(data) && data.Tables[0].Columns.Count == 1, "錯誤輸入／身分只回傳 message。"); }
        await using var setup = source.Setup();
        foreach (var (key, id, number) in new[] { (2, "G0001", "A100000003"), (3, "G0002", "A100000003"), (4, "OLD0001", "A100000001") })
        {
            await setup.InsertAsync(new CampusSoapAccount { Id = id, Status = "02", Campus = 1 });
            await setup.InsertAsync(new CampusSoapStudent { Key = key, Id = id, IdNumber = number, Birthday = "0900102", Name = "合成校友", Email = "g@example.invalid", Campus = 1 });
        }
        using var graduates = await Query("A100000003", "0900102", "GRADSTUD");
        Check(Success(graduates) && graduates.Tables[0].Rows.Count == 2, "同一證號多個舊學號全部回傳，不任取第一筆。");
        using var noTerm = await Query("A100000003", "0900102", "STUD");
        Check(!Success(noTerm), "無當期學籍不通過 STUD。");
        using var currentWithOld = await Query("A100000001", "0900102", "GRADSTUD");
        Check(!Success(currentWithOld), "當期學籍阻止同證號舊學號進入 GRADSTUD。");
        await setup.GetTable<CampusSoapTitle>().Set(t => t.Campus, 2).UpdateAsync();
        using var crossCampus = await Query("A100000002", "0700304", "Employee");
        Check(!Success(crossCampus), "不借用其他校區職稱完成關聯。");
        await setup.GetTable<CampusSoapTitle>().Set(t => t.Campus, 1).UpdateAsync();
        await setup.GetTable<CampusSoapEmployee>().Set(e => e.Status, "0L").UpdateAsync();
        using var formerEmployee = await Query("A100000002", "0700304", "Employee");
        Check(Success(formerEmployee), "文件申請沿用原 SQL，未加入交通方法的離職過濾。");
        await setup.GetTable<CampusSoapEmployee>().Set(e => e.Status, "00").UpdateAsync();
        await setup.InsertAsync(new CampusSoapStudent { Key = 5, Id = "G0001", IdNumber = "A100000003", Birthday = "0900102", Name = "衝突姓名", Campus = 1 });
        try { using var conflict = await Query("A100000003", "0900102", "GRADSTUD"); throw new Exception("同一帳號衝突未拒絕。"); }
        catch (InvalidOperationException) { }
        await setup.GetTable<CampusSoapStudent>().Where(s => s.Key == 5).DeleteAsync();
        await setup.InsertAsync(new AcademicTermRecord { Year = 115, Semester = 2, CurrentMark = "Y" });
        try { using var ambiguousTerm = await Query("A100000001", "0900102", "STUD"); throw new Exception("多個當期學籍未拒絕。"); }
        catch (InvalidOperationException) { }
        await setup.GetTable<AcademicTermRecord>().Where(t => t.Semester == 2).DeleteAsync();
        Check(await setup.GetTable<CampusSoapStudent>().CountAsync() == 4 && await setup.GetTable<StudentTermRecord>().CountAsync() == 1,
            "查詢未修改來源學生或學籍。");
        Console.WriteLine("PASS document payment raw campus tables, identity branches, birthdays, multiple accounts, campus boundaries and conflicting data");
    }
}
