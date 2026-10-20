// 依學號唯讀查詢學生及班級資料。
using LinqToDB;
using LinqToDB.Data;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

/// <summary>以學號讀取必要學生欄位；SSO 帳號與學號的對應由後續整合層確認。</summary>
public static class StudentReader
{
    public static IQueryable<StudentRecord> Query(DataConnection source) =>
        source.GetTable<StudentRecord>().Select(row => new StudentRecord
        {
            StudentKey = row.StudentKey,
            StudentId = row.StudentId,
            Name = row.Name,
            ClassCode = row.ClassCode,
            CampusCode = row.CampusCode
        });

    public static Task<List<StudentRecord>> FindByStudentIdAsync(
        DataConnection source, string studentId, CancellationToken cancellationToken = default)
    {
        ValidateStudentId(studentId);

        // 在來源端篩選，不先載入全校學生；保留所有符合紀錄，避免任意選取第一筆身分。
        return Query(source).Where(row => row.StudentId == studentId)
            .OrderBy(row => row.StudentKey).ToListAsync(cancellationToken);
    }

    /// <summary>按學號在來源端 LEFT JOIN 班級與單位，保持單一 SQL 查詢。</summary>
    public static IQueryable<StudentClassProfile> QueryWithClass(DataConnection source, string studentId)
    {
        ValidateStudentId(studentId);
        return from student in source.GetTable<StudentRecord>()
               where student.StudentId == studentId
               from classroom in source.GetTable<ClassRecord>().LeftJoin(classroom =>
                   student.ClassCode != null && student.ClassCode.Trim() != "" && classroom.Code == student.ClassCode)
               from unit in source.GetTable<UnitRecord>().LeftJoin(unit =>
                   classroom.UnitCode != null && classroom.UnitCode.Trim() != "" && unit.Code == classroom.UnitCode)
               select new StudentClassProfile
               {
                   StudentKey = student.StudentKey,
                   StudentId = student.StudentId,
                   Name = student.Name,
                   ClassCode = student.ClassCode,
                   ClassName = classroom.Name,
                   ClassShortName = classroom.ShortName,
                   UnitCode = classroom.UnitCode,
                   UnitName = unit.Name,
                   UnitShortName = unit.ShortName,
                   DegreeCode = classroom.DegreeCode,
                   YearCode = classroom.YearCode,
                   StudentCampusCode = student.CampusCode,
                   ClassCampusCode = classroom.CampusCode,
                   UnitCampusCode = unit.CampusCode
               };
    }

    // 尚未確認代碼是否跨校區唯一，回傳所有候選列，不任意取第一筆或套用到權限判斷。
    public static Task<List<StudentClassProfile>> FindWithClassAsync(
        DataConnection source, string studentId, CancellationToken cancellationToken = default) =>
        QueryWithClass(source, studentId).OrderBy(row => row.StudentKey).ToListAsync(cancellationToken);

    private static void ValidateStudentId(string studentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);
        if (studentId.Length > 10)
        {
            throw new ArgumentException("學號不得超過 10 個字元。", nameof(studentId));
        }
    }
}
