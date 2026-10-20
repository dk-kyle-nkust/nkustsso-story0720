using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NkustSso.Server.Controllers;
using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class IntegrationReturnFieldChecks
{
    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "integration-return-fields");
        Directory.CreateDirectory(path);
        var database = new DatabaseFactory(path);
        // 模擬尚無欄位授權表的舊版本；不連線任何校務資料來源。
        await using (var old = database.Open())
        {
            await old.CreateTableAsync<IntegrationClient>();
            await old.InsertAsync(new IntegrationClient { Id = "old-soap", Name = "既有系統", Enabled = true, AllowedIps = "127.0.0.1" });
        }
        await database.InitializeAsync();
        await database.InitializeAsync();
        await using var db = database.Open();
        var registry = new IntegrationClientRegistrationService(database, TimeProvider.System);
        Check(await registry.CanReturnCertify2StatusAsync("old-soap", default), "升級保留原有身分代碼。");
        Check(await db.GetTable<IntegrationClientReturnField>().CountAsync() == 1, "重啟不重複授權。");
        var input = new IntegrationClientInput { Id = "new-soap", Name = "新登記", AllowedIps = "192.0.2.1", Enabled = true,
            ServiceIds = [IntegrationPermissions.VerifyAccount] };
        await registry.SaveAsync(input, true, "admin", "test");
        Check(!await registry.CanReturnCertify2StatusAsync(input.Id, default), "新登記預設不傳個資。");
        input.Revision = (await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == input.Id)).Revision;
        var revision = input.Revision;
        foreach (var invalid in new[] { "arbitrary/sql", "Get_Traffic_Stud_Temp/pwd", "Get_Traffic_Stud_Temp/message", "KuasSSO_Certify2/verified" })
        {
            input.ReturnFieldKeys = [invalid];
            await Reject(() => registry.SaveAsync(input, false, "admin", "test"));
            Check((await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == input.Id)).Revision == revision, "非法欄位不可部分儲存。");
        }
        var fields = new[] { IntegrationReturnFields.Certify2Status, "Get_Traffic_Stud_Temp/std_name", "Get_Traffic_Stud_Temp/std_email" };
        input.ReturnFieldKeys = [.. fields, fields[0]];
        await registry.SaveAsync(input, false, "admin", "test");
        Check(await db.GetTable<IntegrationClientReturnField>().CountAsync(f => f.ClientId == input.Id) == 3, "多方法欄位去重保存。");
        Check(await registry.CanReturnCertify2StatusAsync(input.Id, default), "明確授權後可傳身分代碼。");
        await AssertSoapResponseAsync(registry, database, true);
        await Reject(() => registry.SaveAsync(input, false, "admin", "test"));
        var audit = await db.GetTable<IntegrationAudit>().Where(a => a.ClientId == input.Id && a.Action == "update").SingleAsync();
        using (var detail = JsonDocument.Parse(audit.Detail))
            Check(detail.RootElement.GetProperty("BeforeReturnFields").GetArrayLength() == 0 &&
                detail.RootElement.GetProperty("AfterReturnFields").GetArrayLength() == 3, "異動保留前後欄位清單。");
        input.Revision = (await db.GetTable<IntegrationClient>().SingleAsync(c => c.Id == input.Id)).Revision;
        input.ReturnFieldKeys = [];
        await registry.SaveAsync(input, false, "admin", "test");
        await database.InitializeAsync();
        Check(!await registry.CanReturnCertify2StatusAsync(input.Id, default), "清除設定立即撤銷且不因重啟還原。");
        await AssertSoapResponseAsync(registry, database, false);
        Check(await registry.CanReturnCertify2StatusAsync("old-soap", default), "各呼叫系統獨立授權。");
        var draft = new IntegrationClientInput { Id = "draft-fields", Name = "待移植需求", Enabled = true, AllowedIps = "192.0.2.2",
            ReturnFieldKeys = ["Get_Traffic_Stud_Temp/std_name"] };
        await registry.SaveAsync(draft, true, "admin", "test");
        Check(await registry.AuthorizeAsync("soap", IPAddress.Parse("192.0.2.2")) is null, "欄位設定不開通服務。");
        await Reject(() => registry.SaveAsync(new() { Id = "json-fields", Name = "JSON", Protocol = "json",
            ReturnFieldKeys = [IntegrationReturnFields.Certify2Status] }, true, "admin", "test"));
        foreach (var soap12 in new[] { false, true })
        {
            foreach (var include in new[] { false, true })
            {
                var xml = XDocument.Parse(LegacySoapContract.Response(new(true, "02"), soap12, include));
                var values = xml.Descendants(XName.Get("anyType", LegacySoapContract.Namespace)).Select(e => e.Value).ToArray();
                Check(values.SequenceEqual(new[] { "true", include ? "02" : "" }), "只遮蔽資料、不改變位置或驗證結果。");
            }
            var failed = XDocument.Parse(LegacySoapContract.Response(new(false), soap12, false));
            Check(failed.Descendants(XName.Get("anyType", LegacySoapContract.Namespace)).Select(e => e.Value)
                .SequenceEqual(new[] { "false", "500" }), "錯誤代碼不受個資授權影響。");
        }
        Console.WriteLine("PASS per-client return fields, migration, validation, atomic audit, revocation, isolation and SOAP shape");
    }

    private static async Task AssertSoapResponseAsync(IntegrationClientRegistrationService registry, DatabaseFactory database, bool includeStatus)
    {
        foreach (var soap12 in new[] { false, true })
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
            context.Request.ContentType = soap12 ? "application/soap+xml" : "text/xml";
            context.Request.Headers["SOAPAction"] = LegacySoapContract.Action;
            var env = soap12 ? LegacySoapContract.Soap12 : LegacySoapContract.Soap11;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"<s:Envelope xmlns:s=\"{env}\"><s:Body><KuasSSO_Certify2 xmlns=\"http://tempuri.org/\"><usr_id>TEST</usr_id><usr_pwd>correct</usr_pwd></KuasSSO_Certify2></s:Body></s:Envelope>"));
            var verification = new CredentialVerificationService(database, new IntegrationVerificationChecks.FakeSource(), new(),
                TimeProvider.System, NullLogger<CredentialVerificationService>.Instance);
            var controller = new CredentialVerificationApiController(registry, verification,
                Options.Create(new IntegrationServiceOptions { SoapEnabled = true }), new ConfigurationBuilder().Build())
                { ControllerContext = new ControllerContext { HttpContext = context } };
            var response = (ContentResult)await controller.Soap(default);
            var values = XDocument.Parse(response.Content!).Descendants(XName.Get("anyType", LegacySoapContract.Namespace)).Select(e => e.Value);
            Check(values.SequenceEqual(new[] { "true", includeStatus ? "02" : "" }), "SOAP 端點實際依來源系統與最新欄位設定產生回應。");
        }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("非法欄位授權未被拒絕。"); }
}
