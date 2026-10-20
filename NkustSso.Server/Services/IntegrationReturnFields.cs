// 定義舊介面回傳欄位與呼叫端可取得的資料。
using LinqToDB;
using NkustSso.Server.Models;
using NkustSso.Server.Services.SoapDefinitions;

namespace NkustSso.Server.Services;

// 欄位識別碼不是 SQL 欄位名稱；只接受已核對舊方法的固定契約，不接受任意資料庫投影。
public static class IntegrationReturnFields
{
    public const string Certify2Status = "KuasSSO_Certify2/sts_id";

    public static IReadOnlyList<IntegrationReturnContract> Contracts { get; } = Build();

    private static IReadOnlyList<IntegrationReturnContract> Build()
    {
        var contracts = new List<IntegrationReturnContract>
        {
            Certify2Contract(),
            Certify1Contract(),
            StudentContract()
        };

        // 兩個學生方法的欄位完全相同，只有方法名稱不同。
        var student = contracts.Single(c => c.Operation == "Get_Traffic_Stud_Temp");
        contracts.Add(student with { Operation = "Get_Traffic_Stud" });

        var employee = EmployeeContract();
        contracts.Add(employee);
        contracts.Add(employee with { Operation = "Get_Traffic_Teach_Temp" });

        contracts.Add(PublicAccountContract());
        contracts.Add(SmartCardContract());
        contracts.Add(DocumentPaymentContract());

        return contracts.AsReadOnly();
    }

    private static IntegrationReturnContract Certify2Contract()
    {
        return new IntegrationReturnContract(
            "KuasSSO_Certify2",
            "陣列位置從 0 起算；失敗時仍保留原錯誤代碼。",
            [
                new IntegrationReturnField("verified", "驗證結果", "位置 0：是否通過驗證", Required: true),
                new IntegrationReturnField(
                    "sts_id",
                    "來源身分代碼",
                    "位置 1：驗證成功時的來源身分代碼；未勾選則回傳空字串")
            ]);
    }

    private static IntegrationReturnContract Certify1Contract()
    {
        return new IntegrationReturnContract(
            "KuasSSO_Certify1",
            "校務票據驗證；限已設定的功能代碼、兩分鐘內且未兌換的票據。",
            [
                new IntegrationReturnField("verified", "驗證結果", "位置 0：票據是否通過驗證", Required: true),
                new IntegrationReturnField("sts_id", "來源身分代碼", "位置 1：驗證成功時的來源身分代碼")
            ]);
    }

    private static IntegrationReturnContract StudentContract()
    {
        return new IntegrationReturnContract(
            "Get_Traffic_Stud_Temp",
            "沿用舊版 DataSet 欄位，從新校務 MSSQL 唯讀取得；密碼欄位固定留空。",
            [
                new IntegrationReturnField("message", "處理結果", "原方法的結果代碼", Required: true),
                new IntegrationReturnField("std_id", "學號", "學生學號"),
                new IntegrationReturnField("std_name", "姓名", "學生姓名"),
                new IntegrationReturnField(
                    "std_idno",
                    "身分證號摘要",
                    "舊版為身分證號的 MD5；仍屬可識別資料，並非原文身分證號"),
                new IntegrationReturnField("unt_id", "單位代碼", "沿用舊版 unt_id 的語意；新校務關聯待核對"),
                new IntegrationReturnField("unt_name_abr", "單位簡稱", "學生所屬單位簡稱"),
                new IntegrationReturnField("types", "身分類型", "舊版學生為 S"),
                new IntegrationReturnField("usr_id", "登入帳號", "校務帳號"),
                new IntegrationReturnField(
                    "pwd",
                    "密碼衍生資料",
                    "舊版為密碼的 MD5；相容需求尚未確認，不開放授權",
                    Blocked: true),
                new IntegrationReturnField("std_email", "電子郵件", "學生電子郵件")
            ]);
    }

    private static IntegrationReturnContract EmployeeContract()
    {
        return new IntegrationReturnContract(
            "Get_Traffic_Teach",
            "依舊版欄位從新校務 MSSQL 查詢教職員；密碼固定留空。",
            [
                new IntegrationReturnField("message", "處理結果", "原方法的結果代碼", Required: true),
                new IntegrationReturnField("emp_id", "員工編號", "校務員工編號"),
                new IntegrationReturnField("emp_name", "姓名", "教職員姓名"),
                new IntegrationReturnField("emp_idno", "身分證號摘要", "身分證號的 MD5，仍屬可識別資料"),
                new IntegrationReturnField("unt_id", "單位代碼", "沿用舊查詢的 dpt_id"),
                new IntegrationReturnField("unt_name_abr", "單位簡稱", "所屬單位簡稱"),
                new IntegrationReturnField("types", "身分類型", "舊版教職員為 T"),
                new IntegrationReturnField("usr_id", "登入帳號", "校務帳號"),
                new IntegrationReturnField("pwd", "密碼衍生資料", "不開放傳送，保留空欄位", Blocked: true),
                new IntegrationReturnField("emp_email", "電子郵件", "教職員電子郵件")
            ]);
    }

    private static IntegrationReturnContract PublicAccountContract()
    {
        return new IntegrationReturnContract(
            "Get_Traffic_Pub",
            "公用帳號查詢；依原契約保留欄位名稱與順序。",
            [
                new IntegrationReturnField("message", "處理結果", "原方法的結果代碼", Required: true),
                new IntegrationReturnField("pubusr_id", "公用帳號", "包含 office 的校務帳號"),
                new IntegrationReturnField("pubusr_name", "帳號名稱", "公用帳號名稱"),
                new IntegrationReturnField("pubusr_idno", "帳號識別資料", "原版直接傳回 usr_idno，並非 MD5"),
                new IntegrationReturnField("pub_name_abr", "名稱簡稱", "沿用帳號名稱"),
                new IntegrationReturnField("types", "身分類型", "公用帳號為 P"),
                new IntegrationReturnField("pwd", "密碼衍生資料", "不開放傳送，保留空欄位", Blocked: true),
                new IntegrationReturnField("usr_email", "電子郵件", "帳號電子郵件")
            ]);
    }

    private static IntegrationReturnContract SmartCardContract()
    {
        return new IntegrationReturnContract(
            "KuasSSO_SchCardSearch",
            "智慧卡使用新校務帳密驗證，不另外傳送個資。",
            [
                new IntegrationReturnField("message", "處理結果", "位置 0：200／400／500", Required: true),
                new IntegrationReturnField("description", "結果說明", "位置 1：登入成功或失敗說明", Required: true),
                new IntegrationReturnField("verified", "驗證結果", "位置 2：是否驗證通過", Required: true)
            ]);
    }

    private static IntegrationReturnContract DocumentPaymentContract()
    {
        return new IntegrationReturnContract(
            LegacySoapOperationCatalog.DocumentPayment,
            "直接採用原校務 SQL 分支的 DataSet 欄位；教職員也沿用 std_ 前綴，不經 LDAP 或對照表。",
            [
                new IntegrationReturnField(
                    "message",
                    "處理結果",
                    "200 成功、500 無符合資料、501 呼叫帳密錯誤",
                    Required: true),
                new IntegrationReturnField("std_id", "學號／員工編號", "沿用原校務分支的欄位名稱，可回傳多個學號"),
                new IntegrationReturnField("std_name", "姓名", "學生或教職員姓名"),
                new IntegrationReturnField("std_email", "電子郵件", "學生或教職員電子郵件")
            ]);
    }

    public static string Key(string operation, string field)
    {
        return operation + "/" + field;
    }

    public static string[] Validate(IEnumerable<string>? values, string protocol)
    {
        var keys = (values ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        // 固定回傳（Required）與未開放（Blocked）的欄位不列入可勾選範圍。
        var known = Contracts
            .SelectMany(c => c.Fields
                .Where(f => !f.Required && !f.Blocked)
                .Select(f => Key(c.Operation, f.Name)))
            .ToHashSet(StringComparer.Ordinal);

        if (keys.Any(k => !known.Contains(k)))
        {
            throw new InvalidOperationException("包含不存在、固定回傳或尚未開放的回傳欄位，請重新選擇。");
        }

        if (protocol != "soap" && keys.Length > 0)
        {
            throw new InvalidOperationException("SOAP 回傳欄位只能設定於 SOAP 串接；JSON 帳密驗證僅回傳驗證結果。");
        }

        return keys;
    }

    // 保留既有 SOAP 的成功身分代碼；只執行一次，日後取消勾選不會被啟動程序還原。
    public static async Task MigrateAsync(SsoDatabase db)
    {
        await db.CreateTableAsync<IntegrationClientReturnField>(tableOptions: TableOptions.CreateIfNotExists);

        await using var transaction = await db.BeginTransactionAsync();
        const string migration = "integration-return-fields-v1";

        if (!await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            var ids = await db.GetTable<IntegrationClient>()
                .Where(c => c.Protocol == "soap"
                    && db.GetTable<IntegrationClientService>()
                        .Any(g => g.ClientId == c.Id && g.ServiceId == IntegrationPermissions.VerifyAccount))
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var id in ids)
            {
                await KeepCertify2StatusAsync(db, id);
            }

            await db.InsertAsync(new SchemaMigration { Id = migration });
        }

        await transaction.CommitAsync();
    }

    private static async Task KeepCertify2StatusAsync(SsoDatabase db, string id)
    {
        var alreadySet = await db.GetTable<IntegrationClientReturnField>()
            .AnyAsync(f => f.ClientId == id && f.FieldKey == Certify2Status);

        if (!alreadySet)
        {
            await db.InsertAsync(new IntegrationClientReturnField
            {
                ClientId = id,
                FieldKey = Certify2Status
            });
        }

        await db.InsertAsync(new IntegrationAudit
        {
            ClientId = id,
            ActorId = "schema-migration",
            Action = "migrate",
            OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Detail = "保留既有 KuasSSO_Certify2 的來源身分代碼回傳；未新增其他欄位或服務授權。"
        });
    }
}

public sealed record IntegrationReturnField(
    string Name,
    string Label,
    string Description,
    bool Required = false,
    bool Blocked = false);

public sealed record IntegrationReturnContract(
    string Operation,
    string Description,
    IReadOnlyList<IntegrationReturnField> Fields)
{
    public SoapHelpOperation Method => SoapHelpCatalog.Operations.Single(o => o.Name == Operation);
}
