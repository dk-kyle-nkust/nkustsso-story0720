using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

internal static class LegacySoapMigrationChecks
{
    public static async Task<CampusSoapQueries> SeedAsync(string path, DatabaseFactory local, TimeProvider clock)
    {
        Directory.CreateDirectory(path);
        var source = new CampusCatalogChecks.ReadOnlyFixture(path);
        await using var db = source.Setup();
        await db.CreateTableAsync<CampusSoapAccount>(); await db.CreateTableAsync<CampusSoapEmployee>();
        await db.CreateTableAsync<CampusSoapStudent>(); await db.CreateTableAsync<CampusSoapUnit>();
        await db.CreateTableAsync<CampusSoapTicket>(); await db.CreateTableAsync<StudentTermRecord>();
        await db.CreateTableAsync<AcademicTermRecord>(); await db.CreateTableAsync<ClassRecord>();
        await db.CreateTableAsync<CampusSoapTitle>(); await db.CreateTableAsync<CampusSoapDepartment>();
        await db.CreateTableAsync<SourceAccountStatusRecord>();
        await db.InsertAsync(new CampusSoapTitle { Id = "T1", Campus = 1 });
        await db.InsertAsync(new CampusSoapDepartment { Id = "D1", Campus = 1 });
        await db.InsertAsync(new SourceAccountStatusRecord { Code = "01", Name = "教職員" });
        await db.InsertAsync(new SourceAccountStatusRecord { Code = "02", Name = "學生" });
        await db.InsertAsync(new CampusSoapAccount { Id = "S0001", IdNumber = "A100000001", Name = "測試學生", Email = "s@example.invalid", Status = "02", Campus = 1 });
        await db.InsertAsync(new CampusSoapAccount { Id = "E0001", IdNumber = "A100000002", Name = "測試教師", Email = "e@example.invalid", Status = "01", Campus = 1 });
        await db.InsertAsync(new CampusSoapAccount { Id = "office01", IdNumber = "PUBLIC001", Name = "測試公用帳號", Email = "o@example.invalid", Status = "10", Campus = 1 });
        await db.InsertAsync(new CampusSoapStudent { Key = 1, Id = "S0001", IdNumber = "A100000001", Name = "測試學生", Email = "s@example.invalid", Campus = 1, Birthday = "0900102" });
        await db.InsertAsync(new CampusSoapEmployee { Id = "E0001", IdNumber = "A100000002", Name = "測試教師", Email = "e@example.invalid", Unit = "U1", Status = "00", Campus = 1, Birthday = "0700304", WorkUnit = "U1", Title = "T1" });
        await db.InsertAsync(new CampusSoapUnit { Id = "U1", Department = "D1", Name = "測試單位", Campus = 1 });
        await db.InsertAsync(new ClassRecord { Code = "C1", UnitCode = "U1", CampusCode = 1 });
        await db.InsertAsync(new AcademicTermRecord { Year = 115, Semester = 1, CurrentMark = "Y" });
        await db.InsertAsync(new StudentTermRecord { StudentKey = 1, ClassCode = "C1", Year = 115, Semester = 1, StatusCode = "1" });
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")).DateTime;
        foreach (var (number, issued) in new[] { (101, now.AddSeconds(-10)), (102, now.AddMinutes(-3)), (103, now.AddDays(-1)),
                     (104, now.AddSeconds(1)), (105, now.AddSeconds(-10)), (106, now.AddSeconds(-10)), (107, now.AddSeconds(-10)) })
            await db.InsertAsync(new CampusSoapTicket { Serial = number, Account = "S0001", Function = "F1", Random = number, IssuedAt = issued });
        return new CampusSoapQueries(source, local, clock);
    }

    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "soap-migration"); Directory.CreateDirectory(path);
        var local = new DatabaseFactory(path); await local.InitializeAsync();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 16, 4, 0, 0, TimeSpan.Zero));
        var sourcePath = Path.Combine(path, "source");
        var queries = await SeedAsync(sourcePath, local, clock);
        var source = new CampusCatalogChecks.ReadOnlyFixture(sourcePath);
        await using var db = local.Open();
        var registry = new IntegrationClientRegistrationService(local, clock);
        var input = new IntegrationClientInput { Id = "all-methods", Name = "隔離呼叫端", Enabled = true, AllowedIps = "127.0.0.1",
            CallerName = "original-client", CallerPassword = "fixture-original-caller-password", AllowedFunctionIds = "F1",
            ServiceIds = LegacySoapOperationCatalog.Available.Select(LegacySoapOperationCatalog.Permission).ToList(),
            ReturnFieldKeys = ["KuasSSO_Certify1/sts_id", "Get_Traffic_Stud/std_name", "Get_Traffic_Stud_Temp/std_name",
                "Get_Traffic_Teach/emp_name", "Get_Traffic_Teach_Temp/emp_name", "Get_Traffic_Pub/pubusr_name",
                LegacySoapOperationCatalog.DocumentPayment + "/std_name"] };
        await registry.SaveAsync(input, true, "admin", "test");
        var client = await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == input.Id);
        Check(client.CallerPasswordHash != input.CallerPassword && !client.CallerPasswordHash.Contains(input.CallerPassword), "原呼叫密碼只保存雜湊。");
        Check(IntegrationClientRegistrationService.VerifyCaller(client, "original-client", input.CallerPassword), "核對既有呼叫帳密。");
        Check(!IntegrationClientRegistrationService.VerifyCaller(client, "other", input.CallerPassword) && !IntegrationClientRegistrationService.VerifyCaller(client, "original-client", "wrong"), "錯誤呼叫帳密拒絕。");
        Check(!(await db.GetTable<IntegrationAudit>().ToListAsync()).Any(a => a.Detail.Contains(input.CallerPassword) || a.Detail.Contains(client.CallerPasswordHash)), "稽核無呼叫密碼或雜湊。");
        var verification = new CredentialVerificationService(local, new IntegrationVerificationChecks.FakeSource(), new(), clock, NullLogger<CredentialVerificationService>.Instance);
        foreach (var soap12 in new[] { false, true })
        foreach (var method in LegacySoapOperationCatalog.Available)
        {
            var p = Parameters(method, soap12 ? "106" : "105");
            var result = await CallAsync(method, p, soap12, registry, verification, queries);
            if (result.StatusCode is not (null or 200))
                await LegacySoapOperationDispatcher.ExecuteAsync(new(SoapHelpCatalog.Operations.Single(o => o.Name == method), p), client,
                    input.ReturnFieldKeys.ToHashSet(), verification, queries, soap12, default);
            Check(result.StatusCode is null or 200, "共用控制器接受已提供方法：" + method + "／" + result.StatusCode);
            var xml = XDocument.Parse(result.Content!);
            Check(xml.Descendants(XName.Get(method + "Result", LegacySoapContract.Namespace)).Any(), "回應使用原方法名稱。");
            if (LegacySoapOperationCatalog.ReturnsDataSet(method))
            {
                using var data = ReadData(xml, method);
                Check(data.Tables.Count == 1 && data.Tables[0].Rows[0]["message"].ToString() == "200", "ASMX DataSet 可還原 XSD 及 DiffGram。");
                var row = data.Tables[0].Rows[0];
                if (method.StartsWith("Get_Traffic_")) Check(row["pwd"].ToString() == "", "即使舊規格有 pwd 仍不輸出密碼。");
                var name = method == "Get_Traffic_Pub" ? "pubusr_name" : method.Contains("Teach") ? "emp_name" : "std_name";
                Check(row[name].ToString()!.StartsWith("測試"), "核准欄位從唯讀來源回傳。");
                Check(row[method == LegacySoapOperationCatalog.DocumentPayment ? "std_id" : method == "Get_Traffic_Pub" ? "pubusr_idno" : method.Contains("Teach") ? "emp_idno" : "std_idno"].ToString() == "", "未核准識別資料留空。");
                if (method == LegacySoapOperationCatalog.DocumentPayment) Check(row["std_email"].ToString() == "", "未核准郵件留空。");
            }
            else
            {
                var values = xml.Descendants(XName.Get("anyType", LegacySoapContract.Namespace)).Select(e => e.Value).ToArray();
                var expected = method switch { "KuasSSO_Certify1" => new[] { "true", "02" }, "KuasSSO_Certify2" => ["true", ""], _ => ["200", "登入成功!", "true"] };
                Check(values.SequenceEqual(expected), "驗證成功陣列保留順序、型別與欄位授權。");
            }
            if (LegacySoapOperationCatalog.RequiresCaller(method))
            {
                p[method.StartsWith("Get_Traffic_") ? "usr_pwd" : "SearchUsr_pwd"] = "wrong";
                var denied = await CallAsync(method, p, soap12, registry, verification, queries);
                Check(denied.Content!.Contains(LegacySoapOperationCatalog.ReturnsDataSet(method) ? "501" : "400"), "每個呼叫帳密方法都拒絕錯密碼。");
                Check(!denied.Content.Contains("測試學生") && !denied.Content.Contains("測試教師"), "未驗證不洩漏個資。");
            }
        }
        await DocumentPaymentChecks.RunAsync(queries, source);
        await db.GetTable<IntegrationClientService>().Where(g => g.ClientId == input.Id && g.ServiceId == LegacySoapOperationCatalog.Permission(LegacySoapOperationCatalog.DocumentPayment)).DeleteAsync();
        Check((await CallAsync(LegacySoapOperationCatalog.DocumentPayment, Parameters(LegacySoapOperationCatalog.DocumentPayment, "0"), false, registry, verification, queries)).StatusCode == 403, "文件申請方法須獨立授權。");
        var other = Parameters("KuasSSO_Certify1", "101"); other["fnc_id"] = "F2";
        Check((await CallAsync("KuasSSO_Certify1", other, false, registry, verification, queries)).Content!.Contains("NoPass"), "功能代碼須綁定呼叫端。");
        Check((await queries.RedeemAsync("S0001", "F1", "101", default)).Verified, "兩分鐘內票據通過。");
        Check(!(await queries.RedeemAsync("S0001", "F1", "101", default)).Verified, "票據只能使用一次。");
        foreach (var random in new[] { "102", "103", "104", "not-a-number", "2147483648" })
            Check(!(await queries.RedeemAsync("S0001", "F1", random, default)).Verified, "拒絕過期、隔天、未來或非法票據。");
        var concurrent = await Task.WhenAll(queries.RedeemAsync("S0001", "F1", "107", default), queries.RedeemAsync("S0001", "F1", "107", default));
        Check(concurrent.Count(v => v.Verified) == 1, "並行兌換只有一個成功。");
        await using (var writable = source.Setup())
        {
            Check(await writable.GetTable<CampusSoapTicket>().CountAsync() == 7, "所有來源票據均未被修改或刪除。");
            await writable.GetTable<CampusSoapEmployee>().Set(e => e.Status, "0L").UpdateAsync();
        }
        Check((await queries.TrafficAsync("Get_Traffic_Teach", "E0001", default)).Tables[0].Rows[0]["message"].ToString() == "500", "離職註記不通過交通查詢。");
        Check((await queries.TrafficAsync("Get_Traffic_Stud", "' OR 1=1 --", default)).Tables[0].Rows[0]["message"].ToString() == "500", "輸入參數不拼接 SQL。");
        await using (var writable = source.Setup())
            await writable.InsertAsync(new CampusSoapUnit { Id = "U1", Department = "D2", Name = "另一單位", Campus = 1 });
        await Reject(() => queries.TrafficAsync("Get_Traffic_Stud", "S0001", default));
        var wsdl = XDocument.Parse(LegacySoapContract.Wsdl("https://configured.example/"));
        XNamespace w = "http://schemas.xmlsoap.org/wsdl/";
        Check(wsdl.Descendants(w + "portType").Single().Elements(w + "operation").Select(e => (string)e.Attribute("name")!).ToHashSet().SetEquals(LegacySoapOperationCatalog.Available), "WSDL 與實際處理器一致。");
        await db.GetTable<IntegrationClientService>().Where(g => g.ClientId == input.Id && g.ServiceId == "soap.Get_Traffic_Stud").DeleteAsync();
        Check((await CallAsync("Get_Traffic_Stud", Parameters("Get_Traffic_Stud", "0"), false, registry, verification, queries)).StatusCode == 403, "取消單一方法授權立即生效。");
        var request = Envelope("KuasSSO_Certify1", Parameters("KuasSSO_Certify1", "0"), false);
        foreach (var invalid in new[] { request.Replace("fnc_id", "FNC_ID"), request.Replace("<usr_id>", "<unexpected>"), "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///never-read'>]>" + request })
        {
            try { await LegacySoapContract.ReadRequestAsync(new MemoryStream(Encoding.UTF8.GetBytes(invalid)), false, default); throw new Exception("非法 XML 未拒絕。"); }
            catch (XmlException) { }
        }
        Console.WriteLine($"PASS {LegacySoapOperationCatalog.Available.Count} SOAP handlers, per-method caller grants, read-only queries, DataSet roundtrip, field filtering, ticket expiry/replay and WSDL");
    }

    private static Dictionary<string, string> Parameters(string method, string ticket)
    {
        var values = new Dictionary<string, string> { ["usr_id"] = method.StartsWith("Get_Traffic_") ? "original-client" : "S0001",
            ["usr_pwd"] = method.StartsWith("Get_Traffic_") ? "fixture-original-caller-password" : "correct",
            ["fnc_id"] = "F1", ["Rnd_id"] = ticket, ["SearchUsr_id"] = "original-client", ["SearchUsr_pwd"] = "fixture-original-caller-password",
            ["auth_id"] = "S0001", ["auth_pwd"] = "correct", ["from_Clientip"] = "untrusted-value",
            ["IDNO"] = "A100000001", ["Birthday"] = "0900102", ["identity"] = "STUD",
            ["searchID"] = method == "Get_Traffic_Pub" ? "office01" : method.Contains("Teach") ? "E0001" : "S0001", ["searchid"] = "S0001" };
        return SoapHelpCatalog.Operations.Single(o => o.Name == method).Parameters.ToDictionary(p => p.Name, p => values[p.Name]);
    }
    private static string Envelope(string method, IReadOnlyDictionary<string, string> p, bool soap12)
    {
        XNamespace ns = LegacySoapContract.Namespace, env = soap12 ? LegacySoapContract.Soap12 : LegacySoapContract.Soap11;
        return new XElement(env + "Envelope", new XElement(env + "Body", new XElement(ns + method,
            SoapHelpCatalog.Operations.Single(o => o.Name == method).Parameters.Select(v => new XElement(ns + v.Name, p[v.Name]))))).ToString();
    }
    private static async Task<ContentResult> CallAsync(string method, IReadOnlyDictionary<string, string> p, bool soap12,
        IntegrationClientRegistrationService registry, CredentialVerificationService verification, ICampusSoapQueries queries)
    {
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.ContentType = soap12 ? $"application/soap+xml; action=\"{LegacySoapContract.Namespace + method}\"" : "text/xml";
        context.Request.Headers["SOAPAction"] = LegacySoapContract.Namespace + method;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Envelope(method, p, soap12)));
        var controller = new CredentialVerificationApiController(registry, verification, Options.Create(new IntegrationServiceOptions { SoapEnabled = true }),
            new ConfigurationBuilder().Build(), queries) { ControllerContext = new ControllerContext { HttpContext = context } };
        return (ContentResult)await controller.Soap(default);
    }
    private static DataSet ReadData(XDocument xml, string method)
    {
        var result = xml.Descendants(XName.Get(method + "Result", LegacySoapContract.Namespace)).Single();
        var data = new DataSet(); using var reader = result.CreateReader(); data.ReadXml(reader, XmlReadMode.Auto); return data;
    }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("不唯一資料未拒絕。"); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
