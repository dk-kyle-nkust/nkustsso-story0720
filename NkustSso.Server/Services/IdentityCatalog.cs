// 整合身分分類與選項，供後台查詢及入口規則驗證。
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed record IdentityCatalogSnapshot(List<IdentityDimension> Dimensions, List<IdentityOption> Options)
{
    public bool FromCampus { get; init; }

    public string? SourceError { get; init; }

    public List<IdentityOption> CompatibilityOptions { get; init; } = [];

    public IReadOnlyDictionary<string, string> DepartmentParents => DepartmentHierarchy.Parents(Options);

    public static IReadOnlyDictionary<string, string> Families { get; } = new Dictionary<string, string>
    {
        ["any"] = "全部身分類別",
        ["student"] = "學生",
        ["employee"] = "教職員",
        ["other"] = "其他身分"
    };

    /// <summary>
    /// 相容選項是舊資料留下的代碼，查詢時一併納入才不會顯示成「未登記」。
    /// </summary>
    public IdentityOption? Option(string dimension, string code)
    {
        return Options
            .Concat(CompatibilityOptions)
            .FirstOrDefault(o => o.Dimension == dimension && o.Code == code);
    }

    public string Label(string dimension, string code)
    {
        var option = Option(dimension, code);
        if (option is null)
        {
            return code + "（未登記）";
        }

        if (!option.Enabled)
        {
            return option.Label + "（已停用）";
        }

        return option.Label;
    }

    public string FamilyOf(string type)
    {
        return Option("identity_type", type)?.Family ?? "other";
    }

    public static string FamilyLabel(string family)
    {
        return Families.GetValueOrDefault(family, "未知類別");
    }
}

/// <summary>共用身分選項；分類維護與系統開放規則分開，不提供個人權限介面。</summary>
public sealed class IdentityCatalog(
    DatabaseFactory database,
    TimeProvider clock,
    Sources.CampusIdentityCatalog? campus = null)
{
    /// <summary>條件代碼格式：小寫英文開頭，1–50 個小寫英數或底線。</summary>
    private const string DimensionKeyPattern = "^[a-z][a-z0-9_]{0,49}$";

    public async Task<IdentityCatalogSnapshot> LoadSourceAsync()
    {
        await using var db = database.Open();
        var local = await LoadAsync(db);

        if (campus is null)
        {
            return local with { SourceError = "尚未設定校務分類來源。" };
        }

        return await campus.LoadAsync(local);
    }

    public async Task<IdentityCatalogSnapshot> LoadAsync()
    {
        if (campus?.Enabled == true)
        {
            return await LoadSourceAsync();
        }

        await using var db = database.Open();
        return await LoadAsync(db);
    }

    public static async Task<IdentityCatalogSnapshot> LoadAsync(SsoDatabase db)
    {
        var dimensions = await db.GetTable<IdentityDimension>()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Key)
            .ToListAsync();

        var options = await db.GetTable<IdentityOption>()
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Code)
            .ToListAsync();

        return new IdentityCatalogSnapshot(dimensions, options);
    }

    public async Task SaveDimensionAsync(IdentityDimension value, bool creating, string actor, string ip)
    {
        EnsureEditable();
        EnsureDimensionIsValid(value);

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        var before = await db.GetTable<IdentityDimension>().SingleOrDefaultAsync(d => d.Key == value.Key);
        if (creating)
        {
            if (before is not null)
            {
                throw StaleDataError();
            }
        }
        else
        {
            // 代碼與所屬身分不允許在編輯時改掉，改了代表表單不是從這筆資料開的。
            if (before is null || before.Revision != value.Revision || before.Family != value.Family)
            {
                throw StaleDataError();
            }
        }

        var revision = Guid.NewGuid().ToString("N");
        if (creating)
        {
            value.Revision = revision;
            await db.InsertAsync(value);
        }
        else
        {
            await UpdateDimensionAsync(db, value, revision);
        }

        value.Revision = revision;
        await AuditAsync(db, "dimension:" + value.Key, before, value, actor, ip);
        await tx.CommitAsync();
    }

    private static void EnsureDimensionIsValid(IdentityDimension value)
    {
        const string message = "請填寫有效條件代碼、名稱、適用身分與排序。";

        if (!Regex.IsMatch(value.Key ?? "", DimensionKeyPattern))
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 80)
        {
            throw new InvalidOperationException(message);
        }

        if (!IdentityCatalogSnapshot.Families.ContainsKey(value.Family ?? ""))
        {
            throw new InvalidOperationException(message);
        }

        if (value.SortOrder < 0 || value.SortOrder > 10000)
        {
            throw new InvalidOperationException(message);
        }

        if (value.Key == "identity_type" && (!value.Enabled || value.Family != "any"))
        {
            throw new InvalidOperationException("身分類別是基礎條件，不可停用或改變適用身分。");
        }
    }

    private static async Task UpdateDimensionAsync(SsoDatabase db, IdentityDimension value, string revision)
    {
        var changed = await db.GetTable<IdentityDimension>()
            .Where(d => d.Key == value.Key && d.Revision == value.Revision)
            .Set(d => d.Name, value.Name.Trim())
            .Set(d => d.Enabled, value.Enabled)
            .Set(d => d.SortOrder, value.SortOrder)
            .Set(d => d.Revision, revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw StaleDataError();
        }
    }

    public async Task SaveOptionAsync(IdentityOption value, bool creating, string actor, string ip)
    {
        EnsureEditable();
        EnsureOptionIsValid(value);

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        var dimension = await db.GetTable<IdentityDimension>()
            .SingleOrDefaultAsync(d => d.Key == value.Dimension);
        if (dimension is null || (creating && !dimension.Enabled))
        {
            throw new InvalidOperationException("條件不存在或已停用。");
        }

        var before = await db.GetTable<IdentityOption>().SingleOrDefaultAsync(o => o.Id == value.Id);
        if (creating)
        {
            if (before is not null)
            {
                throw StaleDataError();
            }
        }
        else
        {
            // 代碼、所屬條件與身分大類都是固定的，編輯時只能改名稱、啟用與排序。
            var moved = before is null
                || before.Revision != value.Revision
                || before.Code != value.Code
                || before.Dimension != value.Dimension
                || before.Family != value.Family;
            if (moved)
            {
                throw StaleDataError();
            }
        }

        if (creating)
        {
            var duplicated = await db.GetTable<IdentityOption>()
                .AnyAsync(o => o.Dimension == value.Dimension && o.Code == value.Code);
            if (duplicated)
            {
                throw new InvalidOperationException("此條件已存在相同的選項代碼。");
            }
        }

        var revision = Guid.NewGuid().ToString("N");
        if (creating)
        {
            value.Id = Guid.NewGuid().ToString("N");
            value.Revision = revision;
            await db.InsertAsync(value);
        }
        else
        {
            await UpdateOptionAsync(db, value, revision);
        }

        value.Revision = revision;
        await AuditAsync(db, "option:" + value.Dimension + ":" + value.Code, before, value, actor, ip);
        await tx.CommitAsync();
    }

    private static void EnsureOptionIsValid(IdentityOption value)
    {
        const string message = "請填寫有效選項代碼、名稱、身分大類與排序。";

        if (!ValidCode(value.Code))
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(value.Label) || value.Label.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        if (value.SortOrder < 0 || value.SortOrder > 10000)
        {
            throw new InvalidOperationException(message);
        }

        if (value.Family != "student" && value.Family != "employee" && value.Family != "other")
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task UpdateOptionAsync(SsoDatabase db, IdentityOption value, string revision)
    {
        var changed = await db.GetTable<IdentityOption>()
            .Where(o => o.Id == value.Id && o.Revision == value.Revision)
            .Set(o => o.Label, value.Label.Trim())
            .Set(o => o.Enabled, value.Enabled)
            .Set(o => o.SortOrder, value.SortOrder)
            .Set(o => o.Revision, revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw StaleDataError();
        }
    }

    /// <summary>
    /// 設定校務來源時分類由校務端維護，本專案不開放寫入。
    /// </summary>
    private void EnsureEditable()
    {
        if (campus is not null)
        {
            throw new InvalidOperationException("分類由校務資料庫提供，本專案僅供查詢。");
        }
    }

    public static bool ValidCode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Length > 100)
        {
            return false;
        }

        if (value != value.Trim())
        {
            return false;
        }

        return !value.Any(char.IsControl);
    }

    private Task<int> AuditAsync<T>(
        SsoDatabase db,
        string target,
        T? before,
        T after,
        string actor,
        string ip)
    {
        return db.InsertAsync(new IdentityCatalogAudit
        {
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            ActorId = actor,
            RemoteIp = ip,
            Target = target,
            BeforeJson = before is null ? "" : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after)
        });
    }

    private static InvalidOperationException StaleDataError()
    {
        return new InvalidOperationException("設定已變更，或嘗試修改固定代碼／所屬類別。請重新開啟頁面。");
    }

    public static async Task AddStudentKindDimensionAsync(SsoDatabase db)
    {
        const string migration = "identity-student-kind-v1";

        await using var tx = await db.BeginTransactionAsync();
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            return;
        }

        if (!await db.GetTable<IdentityDimension>().AnyAsync(d => d.Key == "student_kind"))
        {
            await db.InsertAsync(new IdentityDimension
            {
                Key = "student_kind",
                Name = "學生狀況",
                Family = "student",
                SortOrder = 15
            });
        }

        // 只補分類種類，選項依校務來源讀取；不改寫原有學籍規則或提供欄位契約。
        await db.InsertAsync(new SchemaMigration { Id = migration });
        await tx.CommitAsync();
    }

    public static async Task SeedAsync(SsoDatabase db)
    {
        const string migration = "identity-classification-v1";

        await using var tx = await db.BeginTransactionAsync();
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            return;
        }

        await InsertSeedDimensionsAsync(db);

        var choices = BuildSeedOptions();
        await AddOptionsFromAccountsAsync(db, choices);
        await AddOptionsFromAudiencesAsync(db, choices);

        foreach (var option in choices)
        {
            await db.InsertAsync(option);
        }

        await db.InsertAsync(new SchemaMigration { Id = migration });
        await tx.CommitAsync();
    }

    private static async Task InsertSeedDimensionsAsync(SsoDatabase db)
    {
        var dimensions = new[]
        {
            ("identity_type", "身分類別", "any"),
            ("student_status", "學籍狀態", "student"),
            ("division", "部別", "student"),
            ("education_level", "學制", "student"),
            ("employment_status", "任職狀態", "employee"),
            ("appointment_type", "聘任／任用類別", "employee"),
            ("campus", "校區", "any"),
            ("department", "系所／單位", "any"),
            ("grade", "年級", "student")
        };

        for (var i = 0; i < dimensions.Length; i++)
        {
            var (key, name, family) = dimensions[i];
            await db.InsertAsync(new IdentityDimension
            {
                Key = key,
                Name = name,
                Family = family,
                SortOrder = i * 10
            });
        }
    }

    /// <summary>
    /// 範例代碼不代表校方正式分類，也不推定真實帳號的部別或任用類別。
    /// </summary>
    private static List<IdentityOption> BuildSeedOptions()
    {
        var choices = new List<IdentityOption>();

        AddOption(choices, "identity_type", "student", "學生", "student");
        AddOption(choices, "identity_type", "teacher", "教師", "employee");
        AddOption(choices, "identity_type", "staff", "一般職員", "employee");

        AddOption(choices, "student_status", "DEMO_ACTIVE", "在校");
        AddOption(choices, "student_status", "DEMO_LEAVE", "休學");
        AddOption(choices, "student_status", "DEMO_WITHDRAWN", "退學");
        AddOption(choices, "student_status", "DEMO_GRADUATED", "畢業");

        AddOption(choices, "division", "DEMO_DAY", "日間部");
        AddOption(choices, "division", "DEMO_NIGHT", "夜間／進修部");

        AddOption(choices, "education_level", "DEMO_UG", "大學部");
        AddOption(choices, "education_level", "DEMO_MASTER", "碩士班");
        AddOption(choices, "education_level", "DEMO_PHD", "博士班");

        AddOption(choices, "employment_status", "DEMO_CURRENT", "現職");
        AddOption(choices, "employment_status", "DEMO_LEFT", "離職");
        AddOption(choices, "employment_status", "DEMO_RETIRED", "退休");

        AddOption(choices, "appointment_type", "DEMO_FULL_TIME", "專任");
        AddOption(choices, "appointment_type", "DEMO_PART_TIME", "兼任");
        AddOption(choices, "appointment_type", "DEMO_CIVIL", "公務人員");
        AddOption(choices, "appointment_type", "DEMO_CONTRACT", "約聘僱");

        return choices;
    }

    /// <summary>
    /// 既有帳號上出現過的代碼也要補成選項，否則後台會顯示成未登記。
    /// </summary>
    private static async Task AddOptionsFromAccountsAsync(SsoDatabase db, List<IdentityOption> choices)
    {
        var profiles = await db.Users
            .Select(u => new
            {
                u.IdentityType,
                u.StudentStatusCode,
                u.StudentStatusName,
                u.EducationLevelCode,
                u.EducationLevelName
            })
            .Distinct()
            .ToListAsync();

        foreach (var p in profiles)
        {
            AddOption(choices, "identity_type", p.IdentityType, p.IdentityType, FamilyOfIdentityType(p.IdentityType));
            AddOption(choices, "student_status", p.StudentStatusCode, p.StudentStatusName);
            AddOption(choices, "education_level", p.EducationLevelCode, p.EducationLevelName);
        }
    }

    /// <summary>
    /// 入口可見條件裡用過的代碼同樣補成選項。
    /// </summary>
    private static async Task AddOptionsFromAudiencesAsync(SsoDatabase db, List<IdentityOption> choices)
    {
        foreach (var row in await db.PortalAudiences.ToListAsync())
        {
            var columns = new[]
            {
                ("identity_type", row.IdentityTypesJson),
                ("student_status", row.StudentStatusesJson),
                ("education_level", row.EducationLevelsJson)
            };

            foreach (var (dimension, json) in columns)
            {
                string[] codes;
                try
                {
                    codes = JsonSerializer.Deserialize<string[]>(json) ?? [];
                }
                catch (JsonException)
                {
                    // 損壞的舊限制由讀取端拒絕，不當成不限。
                    continue;
                }

                foreach (var code in codes)
                {
                    AddOption(choices, dimension, code, code);
                }
            }
        }
    }

    private static string FamilyOfIdentityType(string? identityType)
    {
        if (identityType == "student")
        {
            return "student";
        }

        if (identityType == "staff" || identityType == "teacher")
        {
            return "employee";
        }

        return "other";
    }

    private static void AddOption(
        List<IdentityOption> choices,
        string dimension,
        string? code,
        string? label,
        string family = "other")
    {
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        if (choices.Any(o => o.Dimension == dimension && o.Code == code))
        {
            return;
        }

        choices.Add(new IdentityOption
        {
            Dimension = dimension,
            Code = code,
            Label = string.IsNullOrEmpty(label) ? code : label,
            Family = family,
            SortOrder = choices.Count * 10
        });
    }
}
