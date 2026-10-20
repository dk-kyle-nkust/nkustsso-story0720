// 讀取及同步帳號身分，供服務入口資格比對。
using System.Text.Json;
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed record IdentityProfile(
    string IdentityType,
    Dictionary<string, string> Attributes,
    string? LegacyIdentityType = null)
{
    public string? Value(string dimension)
    {
        // 身分類別直接放在紀錄上，其餘條件才從屬性字典取。
        if (dimension == "identity_type")
        {
            return IdentityType;
        }

        return Attributes.GetValueOrDefault(dimension);
    }
}

/// <summary>供身分來源同步及入口比對使用；不公開個人權限設定或任意寫入 API。</summary>
public sealed class AccountIdentityService(
    DatabaseFactory database,
    TimeProvider clock,
    Sources.CampusIdentityCatalog? campus = null)
{
    /// <summary>一個帳號最多保存的身分紀錄數。</summary>
    private const int MaxRecords = 100;

    /// <summary>單筆身分紀錄最多保存的條件數。</summary>
    private const int MaxAttributes = 16;

    /// <summary>屬性 JSON 的長度上限。</summary>
    private const int MaxAttributesJsonLength = 16000;

    public async Task<List<IdentityProfile>> LoadAsync(Account account)
    {
        if (campus?.Enabled == true && account.AuthenticationSource == CampusLoginService.SourceName)
        {
            return await campus.ProfilesAsync(account);
        }

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        // 沒有同步過身分的帳號，退回使用帳號本身的欄位。
        if (!await db.GetTable<AccountIdentitySet>().AnyAsync(s => s.AccountId == account.Id))
        {
            var attributes = new Dictionary<string, string>
            {
                ["student_status"] = account.StudentStatusCode,
                ["education_level"] = account.EducationLevelCode
            };
            return [new IdentityProfile(account.IdentityType, attributes)];
        }

        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var records = await db.GetTable<AccountIdentity>()
            .Where(i => i.AccountId == account.Id
                && i.Enabled
                && (i.ValidFrom == null || i.ValidFrom <= now)
                && (i.ValidUntil == null || i.ValidUntil > now))
            .ToListAsync();

        var result = new List<IdentityProfile>();
        foreach (var record in records)
        {
            Dictionary<string, string>? attributes;
            try
            {
                attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(record.AttributesJson);
            }
            catch (JsonException)
            {
                // 壞掉的來源紀錄不構成有效身分，也不回退成舊身分。
                continue;
            }

            if (attributes is not null)
            {
                result.Add(new IdentityProfile(record.IdentityType, attributes));
            }
        }

        return result;
    }

    /// <summary>以完整快照及版本控制取代；空集合表示來源確認已無身分，來源失敗時不得呼叫。</summary>
    public async Task<string> ReplaceAsync(
        string accountId,
        string source,
        string? expectedRevision,
        IReadOnlyList<AccountIdentity> records)
    {
        EnsureRequestIsValid(source, records);

        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync();

        if (!await db.Users.AnyAsync(u => u.Id == accountId))
        {
            throw new InvalidOperationException("帳號不存在。");
        }

        var existing = await db.GetTable<AccountIdentitySet>()
            .SingleOrDefaultAsync(s => s.AccountId == accountId);

        if (existing?.Revision != expectedRevision)
        {
            throw new InvalidOperationException("身分來源或版本不符，請重新讀取後同步。");
        }

        if (existing is not null && existing.Source != source)
        {
            throw new InvalidOperationException("身分來源或版本不符，請重新讀取後同步。");
        }

        var catalog = await IdentityCatalog.LoadAsync(db);
        foreach (var record in records)
        {
            EnsureRecordIsValid(record, catalog);
        }

        var revision = Guid.NewGuid().ToString("N");
        var updatedAt = clock.GetUtcNow().ToUnixTimeMilliseconds();

        if (existing is null)
        {
            await db.InsertAsync(new AccountIdentitySet
            {
                AccountId = accountId,
                Source = source,
                Revision = revision,
                UpdatedAt = updatedAt
            });
        }
        else
        {
            var changed = await db.GetTable<AccountIdentitySet>()
                .Where(s => s.AccountId == accountId && s.Revision == expectedRevision)
                .Set(s => s.Revision, revision)
                .Set(s => s.UpdatedAt, updatedAt)
                .UpdateAsync();

            if (changed != 1)
            {
                throw new InvalidOperationException("身分已被其他同步更新。");
            }
        }

        // 整批取代：先清掉舊紀錄，再寫入這次快照的內容。
        await db.GetTable<AccountIdentity>().Where(i => i.AccountId == accountId).DeleteAsync();
        foreach (var record in records)
        {
            record.AccountId = accountId;
            await db.InsertAsync(record);
        }

        await tx.CommitAsync();
        return revision;
    }

    private static void EnsureRequestIsValid(string source, IReadOnlyList<AccountIdentity> records)
    {
        const string message = "身分來源或紀錄格式不正確。";

        if (!IdentityCatalog.ValidCode(source))
        {
            throw new InvalidOperationException(message);
        }

        if (records.Count > MaxRecords)
        {
            throw new InvalidOperationException(message);
        }

        if (records.Select(r => r.Id).Distinct().Count() != records.Count)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureRecordIsValid(AccountIdentity record, IdentityCatalogSnapshot catalog)
    {
        EnsureRecordShapeIsValid(record, catalog);

        Dictionary<string, string> attributes;
        try
        {
            attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(record.AttributesJson)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("身分屬性必須為條件代碼與選項代碼的 JSON 對應。");
        }

        EnsureAttributesAreValid(attributes, record, catalog);
    }

    private static void EnsureRecordShapeIsValid(AccountIdentity record, IdentityCatalogSnapshot catalog)
    {
        const string message = "身分紀錄代碼、期限或類別不正確。";

        if (!IdentityCatalog.ValidCode(record.Id) || !IdentityCatalog.ValidCode(record.IdentityType))
        {
            throw new InvalidOperationException(message);
        }

        if (catalog.Option("identity_type", record.IdentityType) is null)
        {
            throw new InvalidOperationException(message);
        }

        // 兩端都有填時，結束時間必須晚於開始時間。
        if (record.ValidFrom is { } start && record.ValidUntil is { } end && start >= end)
        {
            throw new InvalidOperationException(message);
        }

        if (record.AttributesJson.Length > MaxAttributesJsonLength)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureAttributesAreValid(
        Dictionary<string, string> attributes,
        AccountIdentity record,
        IdentityCatalogSnapshot catalog)
    {
        const string message = "身分屬性包含未登記或不適用的條件／選項。";

        if (attributes.Count > MaxAttributes)
        {
            throw new InvalidOperationException(message);
        }

        var family = catalog.FamilyOf(record.IdentityType);
        foreach (var pair in attributes)
        {
            // identity_type 放在紀錄本身，不能再從屬性覆寫。
            if (pair.Key == "identity_type")
            {
                throw new InvalidOperationException(message);
            }

            if (catalog.Option(pair.Key, pair.Value) is null)
            {
                throw new InvalidOperationException(message);
            }

            // 條件必須適用這個身分大類，例如學籍狀態不能掛在教職員身上。
            var applies = catalog.Dimensions
                .Any(d => d.Key == pair.Key && (d.Family == "any" || d.Family == family));
            if (!applies)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
