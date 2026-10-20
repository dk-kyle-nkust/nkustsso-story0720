using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("TicketClients", IsColumnAttributeRequired = false)]
public sealed class TicketClient
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    // 兌換票券時會重新比對這個值，所以輪替密鑰會讓尚未兌換的票一併失效。
    public string SecretHash { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public bool Enabled { get; set; } = true;
    // 控制回傳是否帶學籍欄位。預設不給，避免新串接拿到超出需求的個資。
    public bool IncludeStudentProfile { get; set; }
}

[Table("LoginTickets", IsColumnAttributeRequired = false)]
public sealed class LoginTicket
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    // 與 LoginSession.KeyHash 同樣只存雜湊；票券原值只在回應中出現一次，不保留。
    public string TicketHash { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    // PKCE 的 challenge。兌換時用 verifier 重算比對，攔到票也換不到身分資料。
    public string CodeChallenge { get; set; } = "";
    // UTC Unix 毫秒。
    public long CreatedAt { get; set; }
    public long ExpiresAt { get; set; }
    // null 代表還沒兌換。兌換的 UPDATE 以此為條件，確保一張票只能成功一次。
    public long? ConsumedAt { get; set; }
    // 核發與兌換分開記錄：兩者來源不同是票券被轉手的跡象。
    public string IssuedIp { get; set; } = "";
    public string? RedeemedIp { get; set; }
}

[Table("TicketAudits", IsColumnAttributeRequired = false)]
public sealed class TicketAudit
{
    [PrimaryKey, Identity] public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ClientId { get; set; } = "";
    public string? AccountId { get; set; }
    public string? TicketId { get; set; }
    public string Action { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string RemoteIp { get; set; } = "";
}
