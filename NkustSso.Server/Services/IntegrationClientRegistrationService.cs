// 維護 JSON／SOAP 呼叫端登記、密鑰與授權。
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinqToDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

public sealed class IntegrationClientRegistrationService(DatabaseFactory database, TimeProvider clock)
{
    /// <summary>來源 IP 與功能代碼都用換行、逗號或空白分隔。</summary>
    private static readonly char[] ListSeparators = ['\r', '\n', ',', ' '];

    public static string NormalizeIp(IPAddress ip)
    {
        // IPv4-mapped IPv6 要先還原成 IPv4，否則同一台機器會比對不到。
        var normalized = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        return normalized.ToString();
    }

    public static string[] ParseIps(string value)
    {
        var entries = value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (entries.Length < 1 || entries.Length > 40)
        {
            throw new InvalidOperationException("請填入 1–40 個完整來源 IP，以換行分隔；不接受萬用字元或網段。");
        }

        if (entries.Any(e => !IPAddress.TryParse(e, out _)))
        {
            throw new InvalidOperationException("請填入 1–40 個完整來源 IP，以換行分隔；不接受萬用字元或網段。");
        }

        return entries
            .Select(e => NormalizeIp(IPAddress.Parse(e)))
            .Distinct()
            .Order()
            .ToArray();
    }

    public async Task<string?> SaveAsync(IntegrationClientInput input, bool creating, string actor, string ip)
    {
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true))
        {
            throw new InvalidOperationException("請確認必填欄位與格式。");
        }

        // 匯入的停用登記可先保存資料；啟用時仍必須提供有效來源 IP。
        var allowEmptyIps = !input.Enabled && string.IsNullOrWhiteSpace(input.AllowedIps);
        var ips = allowEmptyIps ? [] : ParseIps(input.AllowedIps);

        var sourceSystem = input.SourceSystem?.Trim() ?? "";
        var sourceId = input.SourceId?.Trim() ?? "";
        if ((sourceSystem.Length == 0) != (sourceId.Length == 0))
        {
            throw new InvalidOperationException("來源系統與原系統識別碼須一起填寫，或一起留空。");
        }

        var services = (input.ServiceIds ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Order()
            .ToArray();
        var returnFields = IntegrationReturnFields.Validate(input.ReturnFieldKeys, input.Protocol);
        EnsureServicesAreAvailable(services, input.Protocol);

        await using var db = database.Open();
        await using var transaction = await db.BeginTransactionAsync();

        var previous = await db.GetTable<IntegrationClient>().SingleOrDefaultAsync(c => c.Id == input.Id);
        EnsureNotStale(previous, input, creating);

        var (callerName, callerHash) = ResolveCallerCredential(input, previous);
        EnsureSoapOnlyFields(input, callerName, callerHash);

        var functions = ParseFunctionIds(input.AllowedFunctionIds);
        EnsureCallerCredentialIsReady(input, services, callerName, callerHash);
        EnsureTicketFunctionIds(input, services, functions);

        await EnsureSourceIsUniqueAsync(db, input.Id, sourceSystem, sourceId);
        await EnsureServicesExistAsync(db, services);

        var previousServices = await db.GetTable<IntegrationClientService>()
            .Where(g => g.ClientId == input.Id)
            .OrderBy(g => g.ServiceId)
            .Select(g => g.ServiceId)
            .ToArrayAsync();

        var previousFields = await db.GetTable<IntegrationClientReturnField>()
            .Where(f => f.ClientId == input.Id)
            .OrderBy(f => f.FieldKey)
            .Select(f => f.FieldKey)
            .ToArrayAsync();

        await EnsureNoSoapIpConflictAsync(db, input, ips);

        var secret = creating && input.Protocol == "json"
            ? WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))
            : null;

        var value = BuildClient(input, previous, callerName, callerHash, functions, ips, secret);

        if (creating)
        {
            await db.InsertAsync(value);
        }
        else
        {
            await UpdateClientAsync(db, value, input.Revision);
        }

        await ReplaceServicesAsync(db, input.Id, services);
        await ReplaceReturnFieldsAsync(db, input.Id, returnFields);

        await db.InsertAsync(new IntegrationAudit
        {
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds(),
            ClientId = input.Id,
            ActorId = actor,
            RemoteIp = ip,
            Action = creating ? "create" : "update",
            Detail = JsonSerializer.Serialize(new
            {
                Before = Snapshot(previous),
                After = Snapshot(value),
                BeforeServices = previousServices,
                AfterServices = services,
                CallerCredentialChanged = previous?.CallerPasswordHash != value.CallerPasswordHash,
                BeforeReturnFields = previousFields,
                AfterReturnFields = returnFields
            })
        });

        await transaction.CommitAsync();
        return secret;
    }

    /// <summary>
    /// JSON 串接只能用帳號驗證；其餘服務必須是舊方法目錄裡真的有的項目。
    /// </summary>
    private static void EnsureServicesAreAvailable(string[] services, string protocol)
    {
        var knownServices = LegacySoapOperationCatalog.Available
            .Select(LegacySoapOperationCatalog.Permission)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var service in services)
        {
            if (!knownServices.Contains(service))
            {
                throw new InvalidOperationException("包含尚未提供或不存在的服務，請重新選擇。");
            }

            if (protocol == "json" && service != IntegrationPermissions.VerifyAccount)
            {
                throw new InvalidOperationException("包含尚未提供或不存在的服務，請重新選擇。");
            }
        }
    }

    private static void EnsureNotStale(IntegrationClient? previous, IntegrationClientInput input, bool creating)
    {
        if (creating)
        {
            if (previous is not null)
            {
                throw new InvalidOperationException("代碼已存在，或資料已由其他管理者修改，請重新載入。");
            }
        }
        else if (previous is null || previous.Revision != input.Revision)
        {
            throw new InvalidOperationException("代碼已存在，或資料已由其他管理者修改，請重新載入。");
        }

        if (previous is not null && previous.Protocol != input.Protocol)
        {
            throw new InvalidOperationException("既有串接不可變更協定，請另建一筆。");
        }
    }

    /// <summary>
    /// 沒填新密碼就沿用原雜湊；但呼叫帳號換了而密碼沒跟著換，舊雜湊必須作廢。
    /// </summary>
    private static (string CallerName, string CallerHash) ResolveCallerCredential(
        IntegrationClientInput input,
        IntegrationClient? previous)
    {
        var callerName = input.CallerName?.Trim() ?? "";
        var callerHash = previous?.CallerPasswordHash ?? "";

        if (callerName != previous?.CallerName && string.IsNullOrEmpty(input.CallerPassword))
        {
            callerHash = "";
        }

        if (!string.IsNullOrEmpty(input.CallerPassword))
        {
            if (callerName.Length == 0)
            {
                throw new InvalidOperationException("設定呼叫密碼時須提供原系統呼叫帳號。");
            }

            callerHash = new PasswordHasher<IntegrationClient>()
                .HashPassword(new IntegrationClient(), input.CallerPassword);
        }

        return (callerName, callerHash);
    }

    private static void EnsureSoapOnlyFields(IntegrationClientInput input, string callerName, string callerHash)
    {
        if (input.Protocol == "soap")
        {
            return;
        }

        var hasSoapFields = callerName.Length > 0
            || callerHash.Length > 0
            || !string.IsNullOrWhiteSpace(input.AllowedFunctionIds);

        if (hasSoapFields)
        {
            throw new InvalidOperationException("原系統呼叫帳密及功能代碼只適用 SOAP。");
        }
    }

    private static string[] ParseFunctionIds(string? value)
    {
        var functions = (value ?? "")
            .Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToArray();

        if (functions.Length > 40 || functions.Any(f => f.Length > 100))
        {
            throw new InvalidOperationException("功能代碼最多 40 個，每個最多 100 字。");
        }

        return functions;
    }

    /// <summary>
    /// 部分舊方法會帶原系統的呼叫帳密，沒設定就不能啟用。
    /// </summary>
    private static void EnsureCallerCredentialIsReady(
        IntegrationClientInput input,
        string[] services,
        string callerName,
        string callerHash)
    {
        if (!input.Enabled)
        {
            return;
        }

        var needsCaller = services.Any(s => LegacySoapOperationCatalog.Available
            .Any(o => LegacySoapOperationCatalog.Permission(o) == s
                && LegacySoapOperationCatalog.RequiresCaller(o)));

        if (needsCaller && (callerName.Length == 0 || callerHash.Length == 0))
        {
            throw new InvalidOperationException("啟用這些舊方法前，請設定原系統呼叫帳號與密碼。");
        }
    }

    private static void EnsureTicketFunctionIds(
        IntegrationClientInput input,
        string[] services,
        string[] functions)
    {
        if (!input.Enabled)
        {
            return;
        }

        var certify1 = LegacySoapOperationCatalog.Permission("KuasSSO_Certify1");
        if (services.Contains(certify1) && functions.Length == 0)
        {
            throw new InvalidOperationException("票據驗證須設定允許的 fnc_id，避免其他系統的票據被使用。");
        }
    }

    private static async Task EnsureSourceIsUniqueAsync(
        SsoDatabase db,
        string id,
        string sourceSystem,
        string sourceId)
    {
        if (sourceSystem.Length == 0)
        {
            return;
        }

        var duplicated = await db.GetTable<IntegrationClient>()
            .AnyAsync(c => c.Id != id && c.SourceSystem == sourceSystem && c.SourceId == sourceId);

        if (duplicated)
        {
            throw new InvalidOperationException("此來源系統與原系統識別碼已有對應登記，請勿重複新增。");
        }
    }

    private static async Task EnsureServicesExistAsync(SsoDatabase db, string[] services)
    {
        var found = await db.GetTable<IntegrationCapability>().CountAsync(s => services.Contains(s.Id));
        if (services.Length != found)
        {
            throw new InvalidOperationException("服務設定不存在，請重新載入。");
        }
    }

    /// <summary>
    /// 舊方法沒有系統識別參數；同一來源 IP 只能對應一個已啟用的 SOAP 登記。
    /// </summary>
    private static async Task EnsureNoSoapIpConflictAsync(
        SsoDatabase db,
        IntegrationClientInput input,
        string[] ips)
    {
        if (input.Protocol != "soap" || !input.Enabled)
        {
            return;
        }

        var others = await db.GetTable<IntegrationClient>()
            .Where(c => c.Id != input.Id && c.Protocol == "soap" && c.Enabled)
            .ToListAsync();

        if (others.Any(c => ParseIps(c.AllowedIps).Intersect(ips).Any()))
        {
            throw new InvalidOperationException("此來源 IP 已由另一個啟用中的 SOAP 串接使用。");
        }
    }

    private static IntegrationClient BuildClient(
        IntegrationClientInput input,
        IntegrationClient? previous,
        string callerName,
        string callerHash,
        string[] functions,
        string[] ips,
        string? secret)
    {
        return new IntegrationClient
        {
            Id = input.Id,
            Name = input.Name.Trim(),
            Notes = input.Notes ?? "",
            Protocol = input.Protocol,
            Enabled = input.Enabled,
            ConnectionKind = input.ConnectionKind,
            CallerName = callerName,
            CallerPasswordHash = callerHash,
            AllowedFunctionIds = string.Join("\n", functions),
            Purpose = input.Purpose?.Trim() ?? "",
            OwningUnit = input.OwningUnit?.Trim() ?? "",
            SourceSystem = input.SourceSystem?.Trim() ?? "",
            SourceId = input.SourceId?.Trim() ?? "",
            ImportBatchId = previous?.ImportBatchId ?? "",
            ImportedAt = previous?.ImportedAt,
            ApplicantName = input.ApplicantName?.Trim() ?? "",
            ContactName = input.ContactName?.Trim() ?? "",
            ContactPhone = input.ContactPhone?.Trim() ?? "",
            ContactEmail = input.ContactEmail?.Trim() ?? "",
            AllowedIps = string.Join("\n", ips),

            // 沒有產生新密鑰時沿用原雜湊，一般編輯不會換掉呼叫端的密鑰。
            SecretHash = secret is null ? previous?.SecretHash ?? "" : Hash(secret)
        };
    }

    private static async Task UpdateClientAsync(SsoDatabase db, IntegrationClient value, string revision)
    {
        var changed = await db.GetTable<IntegrationClient>()
            .Where(c => c.Id == value.Id && c.Revision == revision)
            .Set(c => c.Name, value.Name)
            .Set(c => c.Notes, value.Notes)
            .Set(c => c.Enabled, value.Enabled)
            .Set(c => c.AllowedIps, value.AllowedIps)
            .Set(c => c.ApplicantName, value.ApplicantName)
            .Set(c => c.ContactName, value.ContactName)
            .Set(c => c.ContactPhone, value.ContactPhone)
            .Set(c => c.ContactEmail, value.ContactEmail)
            .Set(c => c.Purpose, value.Purpose)
            .Set(c => c.OwningUnit, value.OwningUnit)
            .Set(c => c.ConnectionKind, value.ConnectionKind)
            .Set(c => c.CallerName, value.CallerName)
            .Set(c => c.CallerPasswordHash, value.CallerPasswordHash)
            .Set(c => c.AllowedFunctionIds, value.AllowedFunctionIds)
            .Set(c => c.SourceSystem, value.SourceSystem)
            .Set(c => c.SourceId, value.SourceId)
            .Set(c => c.Revision, value.Revision)
            .UpdateAsync();

        if (changed != 1)
        {
            throw new InvalidOperationException("資料已修改，請重新載入。");
        }
    }

    private static async Task ReplaceServicesAsync(SsoDatabase db, string clientId, string[] services)
    {
        await db.GetTable<IntegrationClientService>().Where(g => g.ClientId == clientId).DeleteAsync();

        foreach (var service in services)
        {
            await db.InsertAsync(new IntegrationClientService { ClientId = clientId, ServiceId = service });
        }
    }

    private static async Task ReplaceReturnFieldsAsync(SsoDatabase db, string clientId, string[] fields)
    {
        await db.GetTable<IntegrationClientReturnField>().Where(f => f.ClientId == clientId).DeleteAsync();

        foreach (var field in fields)
        {
            await db.InsertAsync(new IntegrationClientReturnField { ClientId = clientId, FieldKey = field });
        }
    }

    public async Task<string> RotateAsync(string id, string revision, string actor, string ip)
    {
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        await using var db = database.Open();
        await using var transaction = await db.BeginTransactionAsync();

        var changed = await db.GetTable<IntegrationClient>()
            .Where(c => c.Id == id && c.Revision == revision && c.Protocol == "json")
            .Set(c => c.SecretHash, Hash(secret))
            .Set(c => c.Revision, Guid.NewGuid().ToString("N"))
            .UpdateAsync();

        if (changed != 1)
        {
            throw new InvalidOperationException("資料已修改或不是 JSON 串接，請重新載入。");
        }

        await db.InsertAsync(new IntegrationAudit
        {
            ClientId = id,
            ActorId = actor,
            RemoteIp = ip,
            Action = "rotate",
            OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds()
        });

        await transaction.CommitAsync();
        return secret;
    }

    public async Task<IntegrationClient?> AuthorizeAsync(
        string protocol,
        IPAddress? remoteIp,
        string? id = null,
        string? secret = null,
        string service = IntegrationPermissions.VerifyAccount)
    {
        if (remoteIp is null)
        {
            return null;
        }

        var ip = NormalizeIp(remoteIp);
        await using var db = database.Open();

        // SOAP 沒有系統識別參數，只能靠來源 IP 判斷是哪一個登記。
        var candidates = await db.GetTable<IntegrationClient>()
            .Where(c => c.Enabled && c.Protocol == protocol && (protocol == "soap" || c.Id == id))
            .ToListAsync();

        // 取兩筆就夠判斷「不只一筆」，對應到多筆登記時一律拒絕。
        var matches = candidates.Where(c => ParseIps(c.AllowedIps).Contains(ip)).Take(2).ToArray();
        if (matches.Length != 1)
        {
            return null;
        }

        var client = matches[0];
        var allowed = await db.GetTable<IntegrationClientService>()
            .AnyAsync(g => g.ClientId == client.Id && g.ServiceId == service);
        if (!allowed)
        {
            return null;
        }

        if (protocol == "soap")
        {
            return client;
        }

        return MatchesSecret(client, secret) ? client : null;
    }

    /// <summary>
    /// 比對密鑰雜湊；用固定時間比較，避免從回應時間推敲出正確的密鑰。
    /// </summary>
    private static bool MatchesSecret(IntegrationClient client, string? secret)
    {
        if (secret is null || secret.Length < 32 || secret.Length > 100)
        {
            return false;
        }

        if (client.SecretHash.Length != 64)
        {
            return false;
        }

        var provided = Encoding.ASCII.GetBytes(Hash(secret));
        var stored = Encoding.ASCII.GetBytes(client.SecretHash);
        return CryptographicOperations.FixedTimeEquals(provided, stored);
    }

    public async Task<bool> CanReturnCertify2StatusAsync(string clientId, CancellationToken cancellationToken)
    {
        await using var db = database.Open();
        return await db.GetTable<IntegrationClientReturnField>()
            .AnyAsync(
                f => f.ClientId == clientId && f.FieldKey == IntegrationReturnFields.Certify2Status,
                cancellationToken);
    }

    public static bool VerifyCaller(IntegrationClient client, string name, string password)
    {
        if (name.Length < 1 || name.Length > 100)
        {
            return false;
        }

        if (password.Length < 1 || password.Length > 256)
        {
            return false;
        }

        if (name != client.CallerName || client.CallerPasswordHash.Length == 0)
        {
            return false;
        }

        try
        {
            var result = new PasswordHasher<IntegrationClient>()
                .VerifyHashedPassword(client, client.CallerPasswordHash, password);
            return result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // 舊資料可能留下非標準格式的雜湊，視為驗證失敗而不是伺服器錯誤。
            return false;
        }
    }

    public async Task<HashSet<string>> ReturnFieldsAsync(string clientId, CancellationToken token)
    {
        await using var db = database.Open();
        var fields = await db.GetTable<IntegrationClientReturnField>()
            .Where(f => f.ClientId == clientId)
            .Select(f => f.FieldKey)
            .ToListAsync(token);

        return fields.ToHashSet(StringComparer.Ordinal);
    }

    public async Task RecordAsync(string clientId, string ip, string result, CancellationToken cancellationToken)
    {
        await using var db = database.Open();
        await db.InsertAsync(
            new IntegrationAudit
            {
                ClientId = clientId,
                RemoteIp = ip,
                Action = "verify",
                Detail = result,
                OccurredAt = clock.GetUtcNow().ToUnixTimeMilliseconds()
            },
            token: cancellationToken);
    }

    /// <summary>
    /// 異動紀錄只保留可公開的欄位，密鑰與密碼雜湊不進紀錄。
    /// </summary>
    private static object? Snapshot(IntegrationClient? c)
    {
        if (c is null)
        {
            return null;
        }

        return new
        {
            c.Id,
            c.Name,
            c.Protocol,
            c.Enabled,
            c.AllowedIps,
            c.ConnectionKind,
            c.CallerName,
            c.AllowedFunctionIds,
            c.Purpose,
            c.OwningUnit,
            c.SourceSystem,
            c.SourceId,
            c.ImportBatchId,
            c.ImportedAt,
            c.ApplicantName,
            c.ContactName,
            c.ContactPhone,
            c.ContactEmail,
            c.Notes
        };
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
