// 讀取與初始化前台服務分類。
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>從資料庫讀取首頁分類；分類僅組織入口，不代表登入或功能權限。</summary>
public sealed class PortalCategoryService(DatabaseFactory database)
{
    public const string DefaultKey = "general";
    // 顏色使用已定義的樣式，不允許表單注入任意 CSS。
    public static IReadOnlyDictionary<string, string> Accents { get; } = new Dictionary<string, string>
    {
        ["indigo"] = "靛色",
        ["teal"] = "青綠",
        ["amber"] = "琥珀",
        ["rose"] = "玫瑰",
        ["slate"] = "灰藍",
        ["violet"] = "紫色",
        ["cyan"] = "青色",
        ["blue"] = "藍色",
        ["green"] = "綠色"
    };

    /// <summary>每次要求讀取最新設定；同排序時依固定代碼決定順序。</summary>
    public async Task<List<PortalCategory>> ListAsync()
    {
        await using var db = database.Open();
        return await db.PortalCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Key).ToListAsync();
    }

    /// <summary>未知分類以固定 general 代碼兜底，與管理者調整後的分類順序無關。</summary>
    public static PortalCategory Of(IReadOnlyList<PortalCategory> categories, string? key) =>
        categories.FirstOrDefault(c => c.Key == key) ?? categories.Single(c => c.Key == DefaultKey);

    /// <summary>只執行一次的分類遷移；保留既有分類，重啟不還原管理者的修改或刪除。</summary>
    public static async Task MigrateAsync(SsoDatabase db)
    {
        const string migration = "portal-categories-v1";
        await using var transaction = await db.BeginTransactionAsync();
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            return;
        }

        var defaults = new[]
        {
            ("academic", "教務類", "indigo"), ("campus", "學務類", "teal"), ("hr", "人事類", "amber"),
            ("finance", "財務類", "rose"), ("affairs", "總務類", "slate"), ("research", "研究類", "violet"),
            ("document", "文書類", "cyan"), ("it", "資訊類", "blue"), (DefaultKey, "綜合服務", "green")
        };
        var order = 0;
        foreach (var (key, name, accent) in defaults)
        {
            order += 10;
            if (!await db.PortalCategories.AnyAsync(c => c.Key == key))
            {
                await db.InsertAsync(new PortalCategory { Key = key, Name = name, Accent = accent, SortOrder = order });
            }
        }
        // 空分類歸入綜合服務；非空自訂代碼保留並建立分類，避免入口消失。
        await db.RegisteredSystems.Where(s => s.Category == "").Set(s => s.Category, DefaultKey).UpdateAsync();
        foreach (var key in await db.RegisteredSystems.Select(s => s.Category).Distinct().ToListAsync())
        {
            if (!await db.PortalCategories.AnyAsync(c => c.Key == key))
            {
                await db.InsertAsync(new PortalCategory { Key = key, Name = key, SortOrder = 100 });
            }
        }

        await db.InsertAsync(new SchemaMigration { Id = migration });
        await transaction.CommitAsync();
    }
}

