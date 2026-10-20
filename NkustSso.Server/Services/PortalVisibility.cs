// 解析並比對服務入口的身分顯示條件。
using System.Text.Json;
using System.Text.Json.Serialization;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed class AudienceCondition
{
    [JsonRequired]
    public string Dimension { get; set; } = "";

    [JsonRequired]
    public string[] Values { get; set; } = [];
}

public sealed class AudienceGroup
{
    [JsonRequired]
    public string Family { get; set; } = "student";

    [JsonRequired]
    public List<AudienceCondition> Conditions { get; set; } = [];
}

/// <summary>組間 OR、組內 AND；每組必須由同一筆身分完整滿足。只控制入口顯示。</summary>
public sealed class PortalAudienceSelection
{
    /// <summary>一筆規則最多的群組數，也是每組最多的條件數。</summary>
    private const int MaxGroups = 16;

    /// <summary>單一條件最多可選的選項數。</summary>
    private const int MaxValues = 100;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        // 多出來的欄位代表資料不是這個版本寫的，寧可拒絕也不要默默忽略。
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    [JsonRequired]
    public int Version { get; set; } = 2;

    [JsonRequired]
    public string Mode { get; set; } = "all";

    [JsonRequired]
    public List<AudienceGroup> Groups { get; set; } = [];

    [JsonIgnore]
    public bool IsUnrestricted => Mode == "all";

    public void Validate()
    {
        EnsureShapeIsValid();

        foreach (var group in Groups)
        {
            EnsureGroupIsValid(group);
        }
    }

    private void EnsureShapeIsValid()
    {
        const string message = "請選擇全部身分，或建立 1–16 組指定身分條件。";

        if (Version != 2)
        {
            throw new InvalidOperationException(message);
        }

        if (Mode != "all" && Mode != "rules")
        {
            throw new InvalidOperationException(message);
        }

        if (Groups is null || Groups.Count > MaxGroups)
        {
            throw new InvalidOperationException(message);
        }

        // 「全部身分」不該帶任何群組；「指定條件」則至少要有一組。
        if (Mode == "all" && Groups.Count != 0)
        {
            throw new InvalidOperationException(message);
        }

        if (Mode == "rules" && Groups.Count == 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureGroupIsValid(AudienceGroup group)
    {
        if (group is null || !IdentityCatalogSnapshot.Families.ContainsKey(group.Family ?? ""))
        {
            throw new InvalidOperationException("身分群組格式不正確，每組最多 16 個條件。");
        }

        if (group.Conditions is null || group.Conditions.Count > MaxGroups)
        {
            throw new InvalidOperationException("身分群組格式不正確，每組最多 16 個條件。");
        }

        const string conditionMessage = "指定條件必須至少選一項；同一組不能重複同一種條件。";
        var used = new HashSet<string>();

        foreach (var condition in group.Conditions)
        {
            if (condition is null || !IdentityCatalog.ValidCode(condition.Dimension))
            {
                throw new InvalidOperationException(conditionMessage);
            }

            // 同一組裡重複同一種條件會讓 AND 的語意變得不明確。
            if (!used.Add(condition.Dimension))
            {
                throw new InvalidOperationException(conditionMessage);
            }

            if (condition.Values is null || condition.Values.Length < 1 || condition.Values.Length > MaxValues)
            {
                throw new InvalidOperationException(conditionMessage);
            }

            if (condition.Values.Any(v => !IdentityCatalog.ValidCode(v)))
            {
                throw new InvalidOperationException(conditionMessage);
            }
        }
    }

    public void ValidateCatalog(IdentityCatalogSnapshot catalog, PortalAudienceSelection previous)
    {
        if (catalog.SourceError is not null)
        {
            throw new InvalidOperationException(catalog.SourceError);
        }

        Validate();

        foreach (var group in Groups)
        {
            foreach (var condition in group.Conditions)
            {
                var dimension = ResolveDimension(catalog, group, condition);

                foreach (var code in condition.Values)
                {
                    var option = ResolveOption(catalog, group, condition, code);
                    EnsureStillSelectable(catalog, previous, group, condition, code, dimension, option);
                }
            }
        }
    }

    private static IdentityDimension ResolveDimension(
        IdentityCatalogSnapshot catalog,
        AudienceGroup group,
        AudienceCondition condition)
    {
        var dimension = catalog.Dimensions.SingleOrDefault(d => d.Key == condition.Dimension);
        if (dimension is null)
        {
            throw new InvalidOperationException("條件不存在或不適用這一組身分，請重新選擇。");
        }

        // family 為 any 的條件任何群組都能用，其餘必須與群組的身分大類相符。
        if (group.Family != "any" && dimension.Family != "any" && dimension.Family != group.Family)
        {
            throw new InvalidOperationException("條件不存在或不適用這一組身分，請重新選擇。");
        }

        return dimension;
    }

    private static IdentityOption ResolveOption(
        IdentityCatalogSnapshot catalog,
        AudienceGroup group,
        AudienceCondition condition,
        string code)
    {
        var option = catalog.Option(condition.Dimension, code);
        if (option is null)
        {
            throw new InvalidOperationException("選項不存在或不屬於這一組身分。");
        }

        if (condition.Dimension == "identity_type" && group.Family != "any" && option.Family != group.Family)
        {
            throw new InvalidOperationException("選項不存在或不屬於這一組身分。");
        }

        return option;
    }

    /// <summary>
    /// 已停用或只為相容保留的選項不能新加到規則，但原本就在規則裡的允許留著。
    /// </summary>
    private static void EnsureStillSelectable(
        IdentityCatalogSnapshot catalog,
        PortalAudienceSelection previous,
        AudienceGroup group,
        AudienceCondition condition,
        string code,
        IdentityDimension dimension,
        IdentityOption option)
    {
        var retired = !dimension.Enabled
            || !option.Enabled
            || catalog.CompatibilityOptions.Contains(option);
        if (!retired)
        {
            return;
        }

        var keptFromBefore = previous.Groups.Any(g => g.Family == group.Family
            && g.Conditions.Any(c => c.Dimension == condition.Dimension && c.Values.Contains(code)));
        if (!keptFromBefore)
        {
            throw new InvalidOperationException("已停用的條件或選項不可新增到規則中。");
        }
    }

    public bool Matches(IEnumerable<IdentityProfile> identities, IdentityCatalogSnapshot catalog)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        if (Mode != "rules")
        {
            return false;
        }

        // 群組之間是 OR，而且必須由同一筆身分完整滿足某一組。
        return identities.Any(identity => MatchesAnyGroup(identity, catalog));
    }

    private bool MatchesAnyGroup(IdentityProfile identity, IdentityCatalogSnapshot catalog)
    {
        if (catalog.Option("identity_type", identity.IdentityType) is not { Enabled: true })
        {
            return false;
        }

        return Groups.Any(group => MatchesGroup(group, identity, catalog));
    }

    private static bool MatchesGroup(
        AudienceGroup group,
        IdentityProfile identity,
        IdentityCatalogSnapshot catalog)
    {
        if (group.Family != "any" && group.Family != catalog.FamilyOf(identity.IdentityType))
        {
            return false;
        }

        // 組內是 AND，每個條件都要被這筆身分滿足。
        return group.Conditions.All(condition => MatchesCondition(condition, identity, catalog));
    }

    private static bool MatchesCondition(
        AudienceCondition condition,
        IdentityProfile identity,
        IdentityCatalogSnapshot catalog)
    {
        if (!catalog.Dimensions.Any(d => d.Key == condition.Dimension && d.Enabled))
        {
            return false;
        }

        return condition.Values.Any(code => MatchesValue(condition, code, identity, catalog));
    }

    private static bool MatchesValue(
        AudienceCondition condition,
        string code,
        IdentityProfile identity,
        IdentityCatalogSnapshot catalog)
    {
        if (catalog.Option(condition.Dimension, code) is not { Enabled: true })
        {
            return false;
        }

        if (code == identity.Value(condition.Dimension))
        {
            return true;
        }

        // 身分類別另外接受舊代碼，避免改制後既有規則立刻失效。
        return condition.Dimension == "identity_type" && code == identity.LegacyIdentityType;
    }

    public string[] Describe(IdentityCatalogSnapshot catalog)
    {
        if (IsUnrestricted)
        {
            return ["全部身分"];
        }

        if (Mode != "rules")
        {
            return ["規則無效，入口暫不顯示"];
        }

        return Groups.Select(g => DescribeGroup(g, catalog)).ToArray();
    }

    private static string DescribeGroup(AudienceGroup group, IdentityCatalogSnapshot catalog)
    {
        var familyLabel = IdentityCatalogSnapshot.FamilyLabel(group.Family);
        if (group.Conditions.Count == 0)
        {
            return familyLabel + "（不限其他條件）";
        }

        var parts = group.Conditions.Select(c => DescribeCondition(c, catalog));
        return familyLabel + " · " + string.Join("；", parts);
    }

    private static string DescribeCondition(AudienceCondition condition, IdentityCatalogSnapshot catalog)
    {
        var dimensionName = catalog.Dimensions.FirstOrDefault(d => d.Key == condition.Dimension)?.Name
            ?? condition.Dimension;
        var values = condition.Values.Select(v => catalog.Label(condition.Dimension, v));
        return dimensionName + "：" + string.Join("、", values);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public PortalAudience ToRow(string clientId)
    {
        return new PortalAudience { ClientId = clientId, RulesJson = ToJson() };
    }

    public static PortalAudienceSelection Parse(string? json)
    {
        if (json is null || json.Length < 1 || json.Length > 60000)
        {
            throw new InvalidOperationException("身分規則資料缺少或過長，請重新開啟編輯頁。");
        }

        try
        {
            var result = JsonSerializer.Deserialize<PortalAudienceSelection>(json, JsonOptions)
                ?? throw new JsonException();
            result.Validate();
            return result;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("身分規則格式不正確，請重新設定。");
        }
    }

    public static PortalAudienceSelection FromRow(PortalAudience? row)
    {
        if (row is null)
        {
            return new PortalAudienceSelection();
        }

        try
        {
            if (!string.IsNullOrEmpty(row.RulesJson))
            {
                return Parse(row.RulesJson);
            }

            return ConvertLegacyColumns(row);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // 壞掉的規則標成 invalid，讀取端會當成「不顯示」而不是「不限」。
            return new PortalAudienceSelection { Mode = "invalid" };
        }
    }

    /// <summary>
    /// 舊資料把三種條件分欄保存；沒有 RulesJson 時轉成單一群組的規則。
    /// </summary>
    private static PortalAudienceSelection ConvertLegacyColumns(PortalAudience row)
    {
        var columns = new[]
        {
            ("identity_type", row.IdentityTypesJson),
            ("student_status", row.StudentStatusesJson),
            ("education_level", row.EducationLevelsJson)
        };

        var conditions = new List<AudienceCondition>();
        foreach (var (dimension, json) in columns)
        {
            var values = JsonSerializer.Deserialize<string[]>(json) ?? throw new JsonException();
            if (values.Length > 0)
            {
                conditions.Add(new AudienceCondition { Dimension = dimension, Values = values });
            }
        }

        var converted = conditions.Count == 0
            ? new PortalAudienceSelection()
            : new PortalAudienceSelection
            {
                Mode = "rules",
                Groups = [new AudienceGroup { Family = "any", Conditions = conditions }]
            };

        converted.Validate();
        return converted;
    }
}
