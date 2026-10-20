using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NkustSso.Server.Models;
using NkustSso.Server.Services;
using NkustSso.Server.Services.Sources;

internal static class IntegrationVerificationChecks
{
    public static async Task RunAsync(string directory)
    {
        await IntegrationMetadataChecks.RunAsync(directory);
        await IntegrationReturnFieldChecks.RunAsync(directory);
        await LegacySoapMigrationChecks.RunAsync(directory);
        var upgradePath = Path.Combine(directory, "integration-contact-upgrade");
        Directory.CreateDirectory(upgradePath);
        var upgrade = new DatabaseFactory(upgradePath);
        await using (var old = upgrade.Open())
        {
            await old.CreateTableAsync<IntegrationClient>();
            await old.InsertAsync(new IntegrationClient { Id = "existing", Name = "既有串接", Enabled = true, AllowedIps = "127.0.0.1", SecretHash = "preserved-hash" });
            foreach (var column in new[] { "ApplicantName", "ContactName", "ContactPhone", "ContactEmail", "Notes" })
                await old.ExecuteAsync($"ALTER TABLE IntegrationClients DROP COLUMN {column}");
        }
        await upgrade.InitializeAsync();
        await upgrade.InitializeAsync();
        await using (var upgraded = upgrade.Open())
        {
            var row = await upgraded.GetTable<IntegrationClient>().SingleAsync(c => c.Id == "existing");
            Check(row.Name == "既有串接" && row.Enabled && row.SecretHash == "preserved-hash", "Contact migration preserves authorization and credentials.");
            Check(row.ApplicantName == "" && row.ContactName == "" && row.ContactPhone == "" && row.ContactEmail == "" && row.Notes == "", "New contact fields default empty.");
        }
        var path = Path.Combine(directory, "integration-verification");
        Directory.CreateDirectory(path);
        var database = new DatabaseFactory(path);
        await database.InitializeAsync();
        await database.InitializeAsync();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var registry = new IntegrationClientRegistrationService(database, clock);
        var soap = new IntegrationClientInput { Id = "soap-one", Name = "SOAP", Enabled = true, AllowedIps = "::ffff:127.0.0.1", ServiceIds = [IntegrationPermissions.VerifyAccount] };
        Check(await registry.SaveAsync(soap, true, "admin", "test") is null, "Legacy SOAP adds no client secret parameter.");
        Check((await registry.AuthorizeAsync("soap", IPAddress.Loopback))?.Id == soap.Id, "IPv4-mapped IPv6 matches normalized allowlist.");
        Check(await registry.AuthorizeAsync("soap", IPAddress.Parse("192.0.2.8")) is null, "Unknown SOAP IP denied.");
        await Reject(() => registry.SaveAsync(new() { Id = "soap-two", Name = "Other", Enabled = true, AllowedIps = "127.0.0.1" }, true, "admin", "test"));
        await Reject(() => registry.SaveAsync(new() { Id = "soap-two", Name = "Other", AllowedIps = "*" }, true, "admin", "test"));
        var json = new IntegrationClientInput { Id = "json-one", Name = "JSON", Protocol = "json", Enabled = true, AllowedIps = "127.0.0.1", ServiceIds = [IntegrationPermissions.VerifyAccount] };
        json.Notes = new string('備', 20001);
        await Reject(() => registry.SaveAsync(json, true, "admin", "test"));
        json.Notes = new string('備', 20000);
        json.ContactEmail = "invalid-email";
        await Reject(() => registry.SaveAsync(json, true, "admin", "test"));
        json.ApplicantName = "申請人";
        json.ContactName = "承辦人";
        json.ContactPhone = "07-1234567 #123";
        json.ContactEmail = "contact@example.edu";
        var secret = (await registry.SaveAsync(json, true, "admin", "test"))!;
        Check(secret.Length >= 40 && (await registry.AuthorizeAsync("json", IPAddress.Loopback, json.Id, secret))?.Id == json.Id, "Random JSON secret authenticates.");
        Check(await registry.AuthorizeAsync("json", IPAddress.Parse("192.0.2.8"), json.Id, secret) is null, "JSON also requires allowed IP.");
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, json.Id, new string('x', 43)) is null, "Wrong client secret denied.");
        await using var local = database.Open();
        var client = await local.GetTable<IntegrationClient>().SingleAsync(c => c.Id == json.Id);
        Check(client.SecretHash != secret, "Only secret hash persisted.");
        Check(client.Notes == json.Notes && client.ApplicantName == json.ApplicantName && client.ContactName == json.ContactName &&
            client.ContactPhone == json.ContactPhone && client.ContactEmail == json.ContactEmail, "Full-length notes and contact details persisted.");
        var fresh = await registry.RotateAsync(json.Id, client.Revision, "admin", "test");
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, json.Id, secret) is null &&
            await registry.AuthorizeAsync("json", IPAddress.Loopback, json.Id, fresh) is not null, "Rotation invalidates old secret immediately.");
        await Reject(() => registry.RotateAsync(json.Id, client.Revision, "admin", "test"));
        client = await local.GetTable<IntegrationClient>().SingleAsync(c => c.Id == json.Id);
        json.Revision = client.Revision;
        json.Enabled = false;
        await registry.SaveAsync(json, false, "admin", "test");
        Check(await registry.AuthorizeAsync("json", IPAddress.Loopback, json.Id, fresh) is null, "Disable takes effect without restart.");
        await Reject(() => registry.SaveAsync(json, false, "admin", "test"));
        var audits = await local.GetTable<IntegrationAudit>().ToListAsync();
        Check(audits.Count == 4 && audits.All(a => !a.Detail.Contains(secret) && !a.Detail.Contains(fresh) && !a.Detail.Contains(client.SecretHash)), "Audit omits all secrets and hashes.");

        using (var change = System.Text.Json.JsonDocument.Parse(audits.First(a => a.ClientId == json.Id && a.Action == "update").Detail))
            Check(change.RootElement.GetProperty("After").GetProperty("Notes").GetString() == json.Notes &&
                change.RootElement.GetProperty("After").GetProperty("ContactName").GetString() == json.ContactName, "Audit preserves notes and contact changes.");
        json.Revision = (await local.GetTable<IntegrationClient>().SingleAsync(c => c.Id == json.Id)).Revision;
        json.Notes = ""; json.ApplicantName = ""; json.ContactName = ""; json.ContactPhone = ""; json.ContactEmail = null;
        await registry.SaveAsync(json, false, "admin", "test");
        await database.InitializeAsync();
        var cleared = await local.GetTable<IntegrationClient>().SingleAsync(c => c.Id == json.Id);
        Check(cleared.Notes == "" && cleared.ApplicantName == "" && cleared.ContactName == "" && cleared.ContactPhone == "" && cleared.ContactEmail == "", "Contacts and notes can be cleared and remain empty after restart.");
        Console.WriteLine("PASS contact migration, full-length notes, validation, audit and clearing");
        var source = new FakeSource();
        var verification = new CredentialVerificationService(database, source, new(), clock, NullLogger<CredentialVerificationService>.Instance);
        for (var i = 0; i < 5; i++) Check(!(await verification.VerifyAsync("TEST", "wrong", default)).Verified, "Invalid password denied.");
        Check(!(await verification.VerifyAsync(" test ", "correct", default)).Verified && source.Calls == 5, "Normalized account lockout blocks valid password too.");
        clock.Advance(TimeSpan.FromMinutes(5));
        Check((await verification.VerifyAsync("TEST", "correct", default)).Verified, "Lockout expires.");
        source.Unavailable = true;
        Check((await verification.VerifyAsync("TEST", "correct", default)).Unavailable, "Source outage distinguished from invalid credentials.");
        await using (var attempts = database.OpenSessions())
            Check(!await attempts.GetTable<CampusLoginAttempt>().AnyAsync(), "Successful verification clears failures; outages do not add failures.");
        Check(!await local.Users.AnyAsync(), "Service verification never creates a user or permissions.");

        var remote = new CampusCatalogChecks.ReadOnlyFixture(path);
        await using (var db = remote.Setup())
        {
            await db.CreateTableAsync<LegacyMappingCredential>();
            await db.InsertAsync(new LegacyMappingCredential { MemberId = "TEST", EncodedPassword = "cB@f579!d", StatusCode = "02", UserStatus = "00" });
            await db.InsertAsync(new LegacyMappingCredential { MemberId = "LOCKED", EncodedPassword = "cB@f579!d", StatusCode = "02", UserStatus = "0L" });
        }
        var ldap = new FakeDirectory();
        var legacy = new FakeLegacyDatabase(remote);
        var real = new IntegrationAccountSource(remote, legacy, ldap, Options.Create(new IntegrationServiceOptions { AuthenticationSource = "legacy" }));
        Check((await real.VerifyAsync("TEST", "Abc123!@", default)) is { Verified: true, Status: "01" } && legacy.Reads == 0, "LDAP success bypasses mapping.");
        ldap.Result = new(false);
        Check(!(await real.VerifyAsync("TEST", "Abc123!@", default)).Verified && legacy.Reads == 0, "Explicit LDAP denial does not fall back.");
        ldap.Fail = true;
        Check((await real.VerifyAsync("TEST", "Abc123!@", default)) is { Verified: true, Status: "02" }, "LDAP error uses actual read-only mapping query and legacy cipher.");
        Check(!(await real.VerifyAsync("LOCKED", "Abc123!@", default)).Verified, "Legacy 0L status denied.");
        Check(!(await real.VerifyAsync("TEST", "wrong", default)).Verified, "Mapping password required.");
        await using (var db = remote.Setup())
            await db.InsertAsync(new LegacyMappingCredential { MemberId = "TEST", EncodedPassword = "cB@f579!d", StatusCode = "01" });
        Check(!(await real.VerifyAsync("TEST", "Abc123!@", default)).Verified, "Duplicate source rows denied, not arbitrarily chosen.");

        foreach (var soap12 in new[] { false, true })
        {
            var ns = soap12 ? LegacySoapContract.Soap12 : LegacySoapContract.Soap11;
            var request = $"<soap:Envelope xmlns:soap=\"{ns}\"><soap:Body><KuasSSO_Certify2 xmlns=\"http://tempuri.org/\"><usr_id>TEST</usr_id><usr_pwd>A&amp;B</usr_pwd></KuasSSO_Certify2></soap:Body></soap:Envelope>";
            var parsed = await Read(request, soap12);
            Check(parsed == ("TEST", "A&B"), "Legacy XML names and escaped password preserved.");
            var xml = XDocument.Parse(LegacySoapContract.Response(new(true, "02"), soap12, includeStatus: true));
            var values = xml.Descendants(XName.Get("anyType", LegacySoapContract.Namespace)).ToArray();
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            Check(values.Length == 2 && values[0].Value == "true" && values[1].Value == "02" &&
                (string?)values[0].Attribute(xsi + "type") == "xsd:boolean" && (string?)values[1].Attribute(xsi + "type") == "xsd:string", "Legacy object array retains XML boolean/string type and order.");
            await RejectXml(request.Replace("KuasSSO_Certify2", "Unknown"), soap12);
            await RejectXml(request.Replace("<usr_id>TEST</usr_id>", "<usr_id>TEST</usr_id><usr_id>OTHER</usr_id>"), soap12);
            await RejectXml("<!DOCTYPE soap [<!ENTITY xxe SYSTEM 'file:///not-read'>]>" + request, soap12);
            await RejectXml(request.Replace("TEST", new string('x', 20_000)), soap12);
            Check(XDocument.Parse(LegacySoapContract.Fault(soap12, true)).Descendants(XName.Get("Fault", ns)).Any(), "Fault envelope matches SOAP version.");
        }
        var wsdl = XDocument.Parse(LegacySoapContract.Wsdl("https://example.edu/"));
        Check(wsdl.Descendants().Where(e => e.Name.LocalName == "address").All(e => (string?)e.Attribute("location") == "https://example.edu/KuasSSOWebService.asmx"), "WSDL uses configured issuer address.");
        Console.WriteLine("PASS integration authorization, rotation, stale updates, audits, shared lockout, read-only LDAP fallback and SOAP XML contracts");
    }

    private static async Task<(string?, string?)> Read(string xml, bool soap12)
    { using var body = new MemoryStream(Encoding.UTF8.GetBytes(xml)); return await LegacySoapContract.ReadAsync(body, soap12, default); }
    private static async Task RejectXml(string xml, bool soap12)
    { try { await Read(xml, soap12); } catch (XmlException) { return; } throw new Exception("Unsafe XML accepted."); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid mutation accepted."); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal sealed class FakeSource : IIntegrationAccountSource
    {
        public int Calls { get; private set; }
        public bool Unavailable { get; set; }
        public Task<ServiceVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Unavailable || userName == "OUTAGE" ? new ServiceVerification(false, Unavailable: true) : new(password == "correct", "02")); }
    }
    private sealed class FakeDirectory : ILegacyDirectoryAuthenticator
    {
        public bool Fail { get; set; }
        public ServiceVerification Result { get; set; } = new(true, "01");
        public Task<ServiceVerification> VerifyAsync(string userName, string password, CancellationToken cancellationToken) => Fail ? throw new LdapException(49) : Task.FromResult(Result);
    }
    private sealed class FakeLegacyDatabase(CampusCatalogChecks.ReadOnlyFixture fixture) : ILegacyMappingDatabase
    {
        public int Reads { get; private set; }
        public DataConnection Open() { Reads++; return fixture.Open(); }
    }
}
