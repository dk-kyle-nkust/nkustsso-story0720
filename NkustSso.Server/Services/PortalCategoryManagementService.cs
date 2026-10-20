// 儲存服務分類，檢查版本並記錄異動。
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>分類維護的驗證、版本檢查及稽核；設定與紀錄在同一交易提交。</summary>
public sealed class PortalCategoryManagementService(DatabaseFactory database, TimeProvider clock)
{
    /// <summary>分類代碼格式：1–50 個小寫英數、底線或連字號。</summary>
    private const string KeyPattern = "^[a-z0-9][a-z0-9_-]{0,49}$";

    /// <summary>建立分類或更新可編輯欄位；代碼固定，舊表單不可覆蓋新版設定。</summary>
    public async Task SaveAsync(PortalCategory value, bool creating, string actor, string ip)
    {
        EnsureKeyIsValid(value);
        EnsureFieldsAreValid(value);

        await using var db = database.Open();
        await using var transaction = await db.BeginTransactionAsync();

        var before = await db.PortalCategories.SingleOrDefaultAsync(c => c.Key == value.Key);
        if (creating && before is not null)
        {
            throw new InvalidOperationException("分類代碼已存在。");
        }

        if (!creating && (before is null || before.Revision != value.Revision))
        {
            throw StaleDataError();
        }

        var saved = new PortalCategory
        {
            Key = value.Key!,
            Name = value.Name.Trim(),
            NameEn = value.NameEn.Trim(),
            Accent = value.Accent,
            SortOrder = value.SortOrder
        };

        if (creating)
        {
            await db.InsertAsync(saved);
        }
        else
        {
            await UpdateAsync(db, value, saved);
        }

        await AuditAsync(db, before, saved, creating ? "create" : "update", actor, ip);
        await transaction.CommitAsync();
    }

    private static void EnsureKeyIsValid(PortalCategory value)
    {
        // all 是首頁「全部」按鈕用的保留代碼，不能被當成真的分類。
        if (!Regex.IsMatch(value.Key ?? "", KeyPattern) || value.Key == "all")
        {
            throw new InvalidOperationException("分類代碼限 1–50 個小寫英數字、底線或連字號，all 為保留代碼。");
        }
    }

    private static void EnsureFieldsAreValid(PortalCategory value)
    {
        const string message = "請填寫分類名稱（最多 100 字）、有效顏色及 0–10000 的排序。";

        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        if (value.SortOrder < 0 || value.SortOrder > 10000)
        {
            throw new InvalidOperationException(message);
        }

        if (value.NameEn is null || value.NameEn.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        if (value.Accent is null || !PortalCategoryService.Accents.ContainsKey(value.Accent))
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>以載入頁面時的版本為更新條件，更新 0 筆就代表別人先改過了。</summary>
    private static async Task UpdateAsync(SsoDatabase db, PortalCategory value, PortalCategory saved)
    {
        var changed = await db.PortalCategories
            .Where(c => c.Key == value.Key && c.Revision == value.Revision)
            .Set(c => c.Name, saved.Name)
            .Set(c => c.Accent, saved.Accent)
            .Set(c => c.SortOrder, saved.SortOrder)
            .Set(c => c.NameEn, saved.NameEn)
            .Set(c => c.Revision, saved.Revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw StaleDataError();
        }
    }

    /// <summary>只刪除未被任何系統使用的分類，包含隱藏或停用系統；general 是固定兜底分類。</summary>
    public async Task DeleteAsync(string key, string revision, string actor, string ip)
    {
        if (key == PortalCategoryService.DefaultKey)
        {
            throw new InvalidOperationException("綜合服務是預設分類，可更名及排序，但不可刪除。");
        }

        await using var db = database.Open();
        await using var transaction = await db.BeginTransactionAsync();

        var before = await db.PortalCategories.SingleOrDefaultAsync(c => c.Key == key);
        if (before is null || before.Revision != revision)
        {
            throw StaleDataError();
        }

        if (await db.RegisteredSystems.AnyAsync(s => s.Category == key))
        {
            throw new InvalidOperationException("此分類仍有系統使用，請先到串接系統管理將所屬系統移至其他分類。");
        }

        var deleted = await db.PortalCategories
            .Where(c => c.Key == key && c.Revision == revision)
            .DeleteAsync();
        if (deleted != 1)
        {
            throw StaleDataError();
        }

        await AuditAsync(db, before, null, "delete", actor, ip);
        await transaction.CommitAsync();
    }

    private Task<int> AuditAsync(
        SsoDatabase db,
        PortalCategory? before,
        PortalCategory? after,
        string action,
        string actor,
        string ip)
    {
        return db.InsertAsync(new PortalCategoryAudit
        {
            CategoryKey = (after ?? before)!.Key,
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            Action = action,
            ActorId = actor,
            RemoteIp = ip,
            BeforeJson = before is null ? "" : JsonSerializer.Serialize(before),
            AfterJson = after is null ? "" : JsonSerializer.Serialize(after)
        });
    }

    private static InvalidOperationException StaleDataError()
    {
        return new InvalidOperationException("分類已被其他操作更新或刪除，請重新開啟頁面。");
    }
}
