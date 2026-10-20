// 唯讀取得校務身分選項及個人可用的身分條件。
using System.Globalization;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

/// <summary>只讀遠端分類對照，不建立、同步或修改遠端資料。</summary>
public sealed class CampusIdentityCatalog(
    ICampusDatabaseFactory database,
    IOptions<CampusAuthenticationOptions> options,
    ILogger<CampusIdentityCatalog> logger)
{
    public bool Enabled => options.Value.Enabled;

    public static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
    {
        ["identity_type"] = "校務帳號身分",
        ["division"] = "校務部別",
        ["department"] = "校務單位",
        ["campus"] = "校務校區",
        ["student_kind"] = "校務學生狀況"
    };

    public async Task<IdentityCatalogSnapshot> LoadAsync(IdentityCatalogSnapshot local)
    {
        // 只有來源有提供的條件才啟用，其餘保留但停用。
        var dimensions = local.Dimensions.Select(d => new IdentityDimension
        {
            Key = d.Key,
            Name = d.Name,
            Family = d.Family,
            SortOrder = d.SortOrder,
            Enabled = Sources.ContainsKey(d.Key)
        }).ToList();

        var snapshot = new IdentityCatalogSnapshot(dimensions, []) { FromCampus = true };
        if (!Enabled)
        {
            return snapshot with { SourceError = "尚未啟用校務資料庫連線，無法讀取分類資料。" };
        }

        try
        {
            await LoadOptionsAsync(snapshot, local);
            AddCompatibilityOptions(snapshot, local);

            var ordered = snapshot.Options
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Code, StringComparer.Ordinal)
                .ToList();
            return snapshot with { Options = ordered };
        }
        catch (Exception e)
        {
            logger.LogWarning("校務分類讀取失敗，類型：{ErrorType}", e.GetType().Name);
            return snapshot with
            {
                Options = [],
                SourceError = "無法讀取校務分類，請確認連線、讀取權限與來源代碼是否唯一。"
            };
        }
    }

    private async Task LoadOptionsAsync(IdentityCatalogSnapshot snapshot, IdentityCatalogSnapshot local)
    {
        await using var db = database.Open();

        var statuses = await db.GetTable<SourceAccountStatusRecord>()
            .Select(s => new { s.Code, s.Name })
            .ToListAsync();
        var divisions = await db.GetTable<DivisionRecord>()
            .Select(s => new { s.Code, s.Name })
            .ToListAsync();
        var units = await db.GetTable<UnitRecord>()
            .Select(s => new { s.Code, s.Name, s.SourceSortOrder })
            .ToListAsync();
        var campuses = await db.GetTable<CampusAreaRecord>()
            .Select(s => new { s.Code, s.Name, s.ShortName })
            .ToListAsync();
        var studentKinds = await db.GetTable<StudentKindRecord>()
            .Select(s => new { s.Code, s.Name })
            .ToListAsync();

        AddOptions(snapshot, local, "identity_type", statuses.Select(s => (s.Code, s.Name, 0)));
        AddOptions(snapshot, local, "division", divisions.Select(s => (s.Code, s.Name, 0)));
        AddOptions(snapshot, local, "department", units.Select(s => (s.Code, s.Name, s.SourceSortOrder ?? 0)));
        AddOptions(snapshot, local, "campus", campuses.Select(s => (
            s.Code.ToString(CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(s.Name) ? s.ShortName : s.Name,
            s.Code)));
        AddOptions(snapshot, local, "student_kind", studentKinds.Select(s => (s.Code, s.Name, 0)));
    }

    private void AddOptions(
        IdentityCatalogSnapshot snapshot,
        IdentityCatalogSnapshot local,
        string dimension,
        IEnumerable<(string Code, string? Name, int Sort)> rows)
    {
        var groups = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Code.Trim());

        foreach (var group in groups)
        {
            var labels = group
                .Select(r => r.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToArray();

            // 相同代碼若有不同名稱，不任取第一筆造成錯誤的群體授權。
            if (labels.Length > 1)
            {
                throw new InvalidOperationException("來源代碼有多個不同名稱，需確認複合鍵。");
            }

            snapshot.Options.Add(new IdentityOption
            {
                Id = dimension + ":" + group.Key,
                Dimension = dimension,
                Code = group.Key,
                Label = labels.FirstOrDefault() ?? group.Key,
                Family = FamilyFor(dimension, group.Key),
                Enabled = true,

                // 本地若已指定排序就沿用，讓管理者的調整不被來源順序蓋掉。
                SortOrder = local.Option(dimension, group.Key)?.SortOrder ?? group.Min(r => r.Sort)
            });
        }
    }

    /// <summary>只有身分類別需要對應身分大類，其餘條件一律歸入 other。</summary>
    private string FamilyFor(string dimension, string code)
    {
        if (dimension != "identity_type")
        {
            return "other";
        }

        return options.Value.IdentityType(code) switch
        {
            "student" => "student",
            "employee" or "teacher" or "staff" => "employee",
            _ => "other"
        };
    }

    /// <summary>
    /// 舊代碼只供既有規則保留，不能再新增選取，也不列入來源分類清單。
    /// </summary>
    private static void AddCompatibilityOptions(IdentityCatalogSnapshot snapshot, IdentityCatalogSnapshot local)
    {
        var retired = local.Options
            .Where(o => snapshot.Option(o.Dimension, o.Code) is null)
            .Select(o => new IdentityOption
            {
                Id = o.Id,
                Dimension = o.Dimension,
                Code = o.Code,
                Label = o.Label,
                Family = o.Family,
                Enabled = o.Enabled && IsBuiltInIdentityType(o)
            });

        snapshot.CompatibilityOptions.AddRange(retired);
    }

    /// <summary>內建的身分類別代碼即使不在來源裡也仍然可用。</summary>
    private static bool IsBuiltInIdentityType(IdentityOption option)
    {
        if (option.Dimension != "identity_type")
        {
            return false;
        }

        return option.Code is "student" or "employee" or "teacher" or "staff" or "other";
    }

    public async Task<List<IdentityProfile>> ProfilesAsync(Account account)
    {
        try
        {
            await using var db = database.Open();

            var states = await db.GetTable<SourceAccountRecord>()
                .Where(a => a.MemberId == account.SourceAccountId)
                .Select(a => a.StatusCode)
                .Take(2)
                .ToListAsync();

            // 對應不唯一或沒有身分代碼時，視為沒有可用身分。
            if (states.Count != 1 || string.IsNullOrWhiteSpace(states[0]))
            {
                return [];
            }

            var code = states[0]!.Trim();
            var type = options.Value.IdentityType(code);
            var attributes = await LoadStudentAttributesAsync(db, account, type);

            // 部別與其他學籍屬性尚未確認關聯，不以班級代碼或名稱猜測。
            return [new IdentityProfile(code, attributes, type)];
        }
        catch (Exception e)
        {
            logger.LogWarning("校務分類身分讀取失敗，類型：{ErrorType}", e.GetType().Name);
            return [];
        }
    }

    private static async Task<Dictionary<string, string>> LoadStudentAttributesAsync(
        DataConnection db,
        Account account,
        string type)
    {
        var attributes = new Dictionary<string, string>();

        // 學號長度超過 10 的多半不是在學學生，不去查學籍。
        if (type != "student" || account.SourceAccountId.Length > 10)
        {
            return attributes;
        }

        var students = await StudentReader.QueryWithClass(db, account.SourceAccountId).Take(2).ToListAsync();
        if (students.Count == 1 && !string.IsNullOrWhiteSpace(students[0].UnitCode))
        {
            attributes["department"] = students[0].UnitCode!.Trim();
        }

        return attributes;
    }
}
