// 儲存後台選單、群組與成員，並記錄異動。
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed class AdminManagementService(DatabaseFactory database, TimeProvider clock)
{
    /// <summary>選單與角色代碼共用的格式：1–50 個小寫英數、底線或連字號。</summary>
    private const string KeyPattern = "^[a-z0-9][a-z0-9_-]{0,49}$";

    /// <summary>毫秒時間戳的上限，對應西元 9999 年底。</summary>
    private const long MaxTimestamp = 253402271999999;

    /// <summary>唯讀權限的後綴；同時有完整權限時不必另外保留唯讀。</summary>
    private const string ReadOnlySuffix = ":read";

    public static async Task InitializeAsync(SsoDatabase db)
    {
        await db.CreateTableAsync<AdminMenu>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminRole>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminRolePermission>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminRoleMember>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<AdminManagementAudit>(tableOptions: TableOptions.CreateIfNotExists);

        await using var transaction = await db.BeginTransactionAsync();
        await InsertDefaultMenusAsync(db);
        await InsertManualMenuAsync(db);
        await InsertDefaultRoleAsync(db);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 第一次啟動時建立後台選單；以 SchemaMigration 標記，之後管理者的調整不會被蓋掉。
    /// </summary>
    private static async Task InsertDefaultMenusAsync(SsoDatabase db)
    {
        const string version = "admin-management-v1";
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == version))
        {
            return;
        }

        var items = new[]
        {
            ("systems", "串接系統管理", "grid", "服務設定"),
            ("categories", "首頁分類", "folder", "服務設定"),
            ("identities", "身分分類", "people", "服務設定"),
            ("sessions", "登入管理", "shield", "紀錄與維護"),
            ("tickets", "票券紀錄", "ticket", "紀錄與維護"),
            ("progress", "開發進度", "activity", "紀錄與維護"),
            ("menus", "選單維護", "folder", "系統管理"),
            ("access", "權限管理", "people", "系統管理")
        };

        var order = 0;
        foreach (var (key, label, icon, group) in items)
        {
            order += 10;
            if (await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == key))
            {
                continue;
            }

            await db.InsertAsync(new AdminMenu
            {
                Id = key,
                Label = label,
                Target = key,
                Icon = icon,
                Group = group,
                SortOrder = order
            });
        }

        await db.InsertAsync(new SchemaMigration { Id = version });
    }

    /// <summary>升級時補上手冊入口；只執行一次，保留日後的排序、隱藏與刪除。</summary>
    private static async Task InsertManualMenuAsync(SsoDatabase db)
    {
        const string version = "admin-manuals-v1";
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == version)) return;
        if (!await db.GetTable<AdminMenu>().AnyAsync(m => m.Id == "manuals"))
        {
            await db.InsertAsync(new AdminMenu
            {
                Id = "manuals", Label = "串接手冊", Target = "manuals", Icon = "folder",
                Group = "服務設定", SortOrder = 35
            });
        }
        // 不自動擴張既有角色權限；由最高管理者自行指派下載資格。
        await db.InsertAsync(new SchemaMigration { Id = version });
    }

    /// <summary>
    /// 空白群組可重建預設值；既有群組的停用狀態、撤權與成員不得因重啟被覆寫。
    /// </summary>
    private static async Task InsertDefaultRoleAsync(SsoDatabase db)
    {
        if (await db.GetTable<AdminRole>().AnyAsync())
        {
            return;
        }

        // 手動清空群組表時可能留下關聯，不能讓舊成員因同代碼重建而自動取得權限。
        await db.GetTable<AdminRoleMember>().Where(m => m.RoleId == "admin").DeleteAsync();
        await db.GetTable<AdminRolePermission>().Where(p => p.RoleId == "admin").DeleteAsync();

        await db.InsertAsync(new AdminRole
        {
            Id = "admin",
            Name = "admin",
            Description = "預設後台管理群組，可維護一般後台功能；選單與權限管理仍限最高管理者。成員須另行指派。"
        });

        foreach (var feature in AdminAuthorizationService.Features.Where(f => !f.RootOnly))
        {
            await db.InsertAsync(new AdminRolePermission { RoleId = "admin", Permission = feature.Key });
        }
    }

    /// <summary>
    /// 代碼會進網址與權限字串，格式不符就直接擋在這裡。
    /// </summary>
    private static void EnsureValidKey(string? key)
    {
        if (key is null || !Regex.IsMatch(key, KeyPattern))
        {
            throw new InvalidOperationException("代碼須為 1–50 個小寫英數字、底線或連字號。");
        }
    }

    /// <summary>
    /// 以載入頁面時的版本為更新條件，更新 0 筆就代表別人先改過了。
    /// </summary>
    private static InvalidOperationException StaleDataError()
    {
        return new InvalidOperationException("資料已被其他管理者修改或刪除，請重新開啟頁面。");
    }

    /// <summary>
    /// 這兩個入口一旦被停用或刪除，管理者就再也進不去權限設定，所以鎖住。
    /// </summary>
    private static bool IsProtectedMenu(string id)
    {
        return id == "menus" || id == "access";
    }

    public async Task SaveMenuAsync(AdminMenu value, bool creating, string actor, string ip)
    {
        EnsureValidKey(value.Id);
        EnsureMenuIsValid(value);

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        var before = await db.GetTable<AdminMenu>().SingleOrDefaultAsync(m => m.Id == value.Id);
        if (creating && before is not null)
        {
            throw new InvalidOperationException("選單代碼已存在。");
        }

        if (!creating && (before is null || before.Revision != value.Revision))
        {
            throw StaleDataError();
        }

        var after = new AdminMenu
        {
            Id = value.Id,
            Label = value.Label.Trim(),
            Group = value.Group.Trim(),
            Target = value.Target,
            Icon = value.Icon,
            SortOrder = value.SortOrder,
            Enabled = value.Enabled
        };

        if (creating)
        {
            await db.InsertAsync(after);
        }
        else
        {
            await UpdateMenuAsync(db, value.Id, value.Revision, after);
        }

        await AuditAsync(db, "menu", value.Id, creating ? "create" : "update", before, after, actor, ip);
        await tx.CommitAsync();
    }

    /// <summary>
    /// 選單欄位的檢查；逐項分開，錯在哪一個條件一看就知道。
    /// </summary>
    private static void EnsureMenuIsValid(AdminMenu value)
    {
        const string message = "請填妥名稱、分組、有效圖示、功能與 0–10000 的排序。";

        if (string.IsNullOrWhiteSpace(value.Label) || value.Label.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(value.Group) || value.Group.Length > 50)
        {
            throw new InvalidOperationException(message);
        }

        if (value.SortOrder < 0 || value.SortOrder > 10000)
        {
            throw new InvalidOperationException(message);
        }

        if (value.Icon is null || !AdminAuthorizationService.Icons.ContainsKey(value.Icon))
        {
            throw new InvalidOperationException(message);
        }

        if (!AdminAuthorizationService.Features.Any(f => f.Key == value.Target))
        {
            throw new InvalidOperationException(message);
        }

        if (IsProtectedMenu(value.Id) && (!value.Enabled || value.Target != value.Id))
        {
            throw new InvalidOperationException("選單維護與權限管理入口不可停用或改為其他功能。");
        }
    }

    private static async Task UpdateMenuAsync(SsoDatabase db, string id, string revision, AdminMenu after)
    {
        var changed = await db.GetTable<AdminMenu>()
            .Where(m => m.Id == id && m.Revision == revision)
            .Set(m => m.Label, after.Label)
            .Set(m => m.Group, after.Group)
            .Set(m => m.Target, after.Target)
            .Set(m => m.Icon, after.Icon)
            .Set(m => m.SortOrder, after.SortOrder)
            .Set(m => m.Enabled, after.Enabled)
            .Set(m => m.Revision, after.Revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw StaleDataError();
        }
    }

    public async Task DeleteMenuAsync(string id, string revision, string actor, string ip)
    {
        if (IsProtectedMenu(id))
        {
            throw new InvalidOperationException("選單維護與權限管理入口不可刪除。");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        var before = await db.GetTable<AdminMenu>().SingleOrDefaultAsync(m => m.Id == id);
        if (before is null || before.Revision != revision)
        {
            throw StaleDataError();
        }

        var deleted = await db.GetTable<AdminMenu>()
            .Where(m => m.Id == id && m.Revision == revision)
            .DeleteAsync();

        if (deleted != 1)
        {
            throw StaleDataError();
        }

        await AuditAsync(db, "menu", id, "delete", before, null, actor, ip);
        await tx.CommitAsync();
    }

    public async Task SaveRoleAsync(
        AdminRole value,
        IEnumerable<string>? permissions,
        IEnumerable<string>? members,
        bool creating,
        string actor,
        string ip)
    {
        EnsureValidKey(value.Id);

        var grants = (permissions ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var accounts = (members ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        EnsureRoleIsValid(value, grants, accounts);
        EnsureRolePeriodIsValid(value);

        grants = RemoveRedundantReadGrants(grants);

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        if (await db.GetTable<Account>().CountAsync(a => accounts.Contains(a.Id)) != accounts.Length)
        {
            throw new InvalidOperationException("選取的帳號不存在，請重新選擇。");
        }

        var before = await db.GetTable<AdminRole>().SingleOrDefaultAsync(r => r.Id == value.Id);
        if (creating && before is not null)
        {
            throw new InvalidOperationException("角色代碼已存在。");
        }

        if (!creating && (before is null || before.Revision != value.Revision))
        {
            throw StaleDataError();
        }

        var oldGrants = await db.GetTable<AdminRolePermission>()
            .Where(g => g.RoleId == value.Id)
            .Select(g => g.Permission)
            .OrderBy(g => g)
            .ToListAsync();

        var oldMembers = await db.GetTable<AdminRoleMember>()
            .Where(m => m.RoleId == value.Id)
            .Select(m => m.AccountId)
            .OrderBy(m => m)
            .ToListAsync();

        var after = new AdminRole
        {
            Id = value.Id,
            Name = value.Name.Trim(),
            Description = value.Description.Trim(),
            Enabled = value.Enabled,
            StartsAt = value.StartsAt,
            ExpiresAt = value.ExpiresAt
        };

        if (creating)
        {
            await db.InsertAsync(after);
        }
        else
        {
            await UpdateRoleAsync(db, value.Id, value.Revision, after);
        }

        // 權限與成員整批重寫，先清空再依表單內容建立。
        await db.GetTable<AdminRolePermission>().Where(g => g.RoleId == value.Id).DeleteAsync();
        await db.GetTable<AdminRoleMember>().Where(m => m.RoleId == value.Id).DeleteAsync();

        foreach (var grant in grants)
        {
            await db.InsertAsync(new AdminRolePermission { RoleId = value.Id, Permission = grant });
        }

        foreach (var member in accounts)
        {
            await db.InsertAsync(new AdminRoleMember { RoleId = value.Id, AccountId = member });
        }

        object? beforeState = before is null
            ? null
            : new { Role = before, Permissions = oldGrants, Members = oldMembers };
        var afterState = new { Role = after, Permissions = grants, Members = accounts };

        await AuditAsync(db, "role", value.Id, creating ? "create" : "update", beforeState, afterState, actor, ip);
        await tx.CommitAsync();
    }

    private static void EnsureRoleIsValid(AdminRole value, string[] grants, string[] accounts)
    {
        const string message = "角色名稱、說明或授權項目不正確；角色不可授予最高管理權限。";

        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 100)
        {
            throw new InvalidOperationException(message);
        }

        if (value.Description is null || value.Description.Length > 2000)
        {
            throw new InvalidOperationException(message);
        }

        if (grants.Any(g => !AdminAuthorizationService.IsGrant(g)))
        {
            throw new InvalidOperationException(message);
        }

        if (accounts.Length > 1000)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureRolePeriodIsValid(AdminRole value)
    {
        const string message = "角色授權時間不正確，結束時間須晚於開始時間。";

        if (value.StartsAt is not null && (value.StartsAt < 0 || value.StartsAt > MaxTimestamp))
        {
            throw new InvalidOperationException(message);
        }

        if (value.ExpiresAt is not null && (value.ExpiresAt < 0 || value.ExpiresAt > MaxTimestamp))
        {
            throw new InvalidOperationException(message);
        }

        if (value.StartsAt is not null && value.ExpiresAt is not null && value.StartsAt >= value.ExpiresAt)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 已經有完整權限時，同名的唯讀權限沒有意義，存檔前先濾掉。
    /// </summary>
    private static string[] RemoveRedundantReadGrants(string[] grants)
    {
        return grants
            .Where(g =>
            {
                if (!g.EndsWith(ReadOnlySuffix, StringComparison.Ordinal))
                {
                    return true;
                }

                var fullGrant = g[..^ReadOnlySuffix.Length];
                return !grants.Contains(fullGrant, StringComparer.Ordinal);
            })
            .ToArray();
    }

    private static async Task UpdateRoleAsync(SsoDatabase db, string id, string revision, AdminRole after)
    {
        var changed = await db.GetTable<AdminRole>()
            .Where(r => r.Id == id && r.Revision == revision)
            .Set(r => r.Name, after.Name)
            .Set(r => r.Description, after.Description)
            .Set(r => r.Enabled, after.Enabled)
            .Set(r => r.StartsAt, after.StartsAt)
            .Set(r => r.ExpiresAt, after.ExpiresAt)
            .Set(r => r.Revision, after.Revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw StaleDataError();
        }
    }

    public async Task DeleteRoleAsync(string id, string revision, string actor, string ip)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        var before = await db.GetTable<AdminRole>().SingleOrDefaultAsync(r => r.Id == id);
        if (before is null || before.Revision != revision)
        {
            throw StaleDataError();
        }

        if (await db.GetTable<AdminRoleMember>().AnyAsync(m => m.RoleId == id))
        {
            throw new InvalidOperationException("角色仍有成員，請先移除成員或停用角色。");
        }

        var grants = await db.GetTable<AdminRolePermission>()
            .Where(g => g.RoleId == id)
            .Select(g => g.Permission)
            .ToListAsync();

        var deleted = await db.GetTable<AdminRole>()
            .Where(r => r.Id == id && r.Revision == revision)
            .DeleteAsync();

        if (deleted != 1)
        {
            throw StaleDataError();
        }

        await db.GetTable<AdminRolePermission>().Where(g => g.RoleId == id).DeleteAsync();

        var beforeState = new { Role = before, Permissions = grants };
        await AuditAsync(db, "role", id, "delete", beforeState, null, actor, ip);
        await tx.CommitAsync();
    }

    private Task<int> AuditAsync(
        SsoDatabase db,
        string kind,
        string target,
        string action,
        object? before,
        object? after,
        string actor,
        string ip)
    {
        return db.InsertAsync(new AdminManagementAudit
        {
            Kind = kind,
            TargetId = target,
            Action = action,
            ActorId = actor,
            RemoteIp = ip,
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            BeforeJson = before is null ? "" : JsonSerializer.Serialize(before),
            AfterJson = after is null ? "" : JsonSerializer.Serialize(after)
        });
    }
}
