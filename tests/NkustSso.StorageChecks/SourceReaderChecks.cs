using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services.Sources;

internal static class SourceReaderChecks
{
    public static async Task RunAsync(string directory)
    {
        // 僅以隔離 SQLite 驗證關聯語意，不連線校務來源，也不使用真實人員資料。
        var path = Path.Combine(directory, "source-readers");
        Directory.CreateDirectory(path);
        await using var db = new DatabaseFactory(path).Open();
        await db.ExecuteAsync("ATTACH DATABASE ':memory:' AS dbo");
        await db.CreateTableAsync<SourceAccountRecord>();
        await db.CreateTableAsync<SourceAccountStatusRecord>();
        await db.InsertAsync(new SourceAccountRecord { MemberId = "TEST_A", Name = "Example A", StatusCode = "S" });
        await db.InsertAsync(new SourceAccountRecord { MemberId = "TEST_B", Name = "Example B", StatusCode = "UNKNOWN" });
        await db.InsertAsync(new SourceAccountRecord { MemberId = "TEST_C", StatusCode = null });
        await db.InsertAsync(new SourceAccountStatusRecord { Code = "S", Name = "Student" });

        var known = await SourceAccountReader.FindAsync(db, "TEST_A");
        Check(known.Count == 1 && known[0].MemberId == "TEST_A" && known[0].Name == "Example A"
            && known[0].StatusName == "Student", "Account lookup filters and joins status.");
        var missing = await SourceAccountReader.FindAsync(db, "TEST_B");
        Check(missing.Count == 1 && missing[0].StatusCode == "UNKNOWN" && missing[0].StatusName is null,
            "Missing status lookup must not hide the account or invent a status name.");
        var empty = await SourceAccountReader.FindAsync(db, "TEST_C");
        Check(empty.Count == 1 && empty[0].StatusCode is null && empty[0].StatusName is null,
            "Null status remains unknown.");
        Check((await SourceAccountReader.FindAsync(db, "TEST_A' OR '1'='1")).Count == 0,
            "Account filter treats input as a value.");
        await db.InsertAsync(new SourceAccountStatusRecord { Code = "S", Name = "Duplicate" });
        Check((await SourceAccountReader.FindAsync(db, "TEST_A")).Count == 2,
            "Ambiguous source status is preserved instead of silently choosing one.");
        Check(await db.GetTable<SourceAccountRecord>().CountAsync() == 3,
            "Reader leaves source accounts unchanged.");
        Console.WriteLine("PASS source account filtering, left joins, unknown status and duplicate preservation (isolated SQLite)");

        await db.CreateTableAsync<StudentRecord>();
        await db.CreateTableAsync<ClassRecord>();
        await db.CreateTableAsync<UnitRecord>();
        await db.InsertAsync(new StudentRecord { StudentKey = 1, StudentId = "S1", Name = "Student A", ClassCode = "C1", CampusCode = 1 });
        await db.InsertAsync(new StudentRecord { StudentKey = 2, StudentId = "S2", ClassCode = "MISSING" });
        await db.InsertAsync(new StudentRecord { StudentKey = 3, StudentId = "S3", ClassCode = "C2" });
        await db.InsertAsync(new StudentRecord { StudentKey = 4, StudentId = "S4", ClassCode = "" });
        await db.InsertAsync(new StudentRecord { StudentKey = 5, StudentId = "S5", ClassCode = null });
        await db.InsertAsync(new ClassRecord { Code = "C1", Name = "Class A", ShortName = "A", UnitCode = "U1", DegreeCode = "D1", YearCode = "2", CampusCode = 1 });
        await db.InsertAsync(new ClassRecord { Code = "C2", Name = "Class B", UnitCode = "ABSENT" });
        await db.InsertAsync(new ClassRecord { Code = "", Name = "Invalid blank code", UnitCode = "U1" });
        await db.InsertAsync(new UnitRecord { Code = "U1", Name = "Unit A", ShortName = "UA", CampusCode = 1 });

        var profile = await StudentReader.FindWithClassAsync(db, "S1");
        Check(profile.Count == 1 && profile[0].Name == "Student A" && profile[0].ClassName == "Class A"
            && profile[0].UnitName == "Unit A" && profile[0].DegreeCode == "D1" && profile[0].YearCode == "2",
            "Student lookup joins class and unit within the database.");
        var noClass = await StudentReader.FindWithClassAsync(db, "S2");
        Check(noClass.Count == 1 && noClass[0].ClassCode == "MISSING" && noClass[0].ClassName is null && noClass[0].UnitName is null,
            "Missing class retains the student and original class code.");
        var noUnit = await StudentReader.FindWithClassAsync(db, "S3");
        Check(noUnit.Count == 1 && noUnit[0].ClassName == "Class B" && noUnit[0].UnitCode == "ABSENT" && noUnit[0].UnitName is null,
            "Missing unit retains the student and class.");
        foreach (var id in new[] { "S4", "S5" })
        {
            var noCode = await StudentReader.FindWithClassAsync(db, id);
            Check(noCode.Count == 1 && noCode[0].ClassName is null && noCode[0].UnitName is null,
                "Blank or null class code must not join a blank catalog code.");
        }
        Check((await StudentReader.FindWithClassAsync(db, "MISSING")).Count == 0, "Unknown student returns no profile.");
        await db.InsertAsync(new UnitRecord { Code = "U1", Name = "Another campus unit", CampusCode = 2 });
        var duplicate = await StudentReader.FindWithClassAsync(db, "S1");
        Check(duplicate.Count == 2 && duplicate.Select(row => row.UnitCampusCode).Distinct().Count() == 2,
            "Duplicate unit codes remain visible for association validation, without arbitrary selection.");
        Check(await db.GetTable<StudentRecord>().CountAsync() == 5 && await db.GetTable<ClassRecord>().CountAsync() == 3,
            "Student profile reader leaves source records unchanged.");
        Console.WriteLine("PASS student/class/unit joins, missing references, blank codes and ambiguous unit codes (isolated SQLite)");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
