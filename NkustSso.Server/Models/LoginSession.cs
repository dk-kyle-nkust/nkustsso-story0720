using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

[Table("LoginSessions", IsColumnAttributeRequired = false)]
public sealed class LoginSession
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    // 只存 Cookie 鍵的雜湊，原值不落地，查詢一律用它比對；
    // 資料庫外流也還原不出可用的登入狀態。
    public string KeyHash { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    // 建立時從 Identity 複製當下的值。帳號改密碼或被停用後 stamp 會變，
    // 比對不符就讓既有登入失效。
    public string SecurityStamp { get; set; } = "";
    // Data Protection 加密後的驗證票。撤銷時清成空陣列，所以長度 0 等於已撤銷。
    public byte[] ProtectedTicket { get; set; } = [];
    // 以下三個時間都是 UTC Unix 毫秒，不是秒也不是 DateTime；
    // 畫面上的臺灣時間由顯示層換算。
    public long CreatedAt { get; set; }
    public long LastSeenAt { get; set; }
    public long ExpiresAt { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    // 撤銷是 UPDATE 不是 DELETE：管理頁與稽核要查得到誰在何時以什麼理由終止這個登入。
    public long? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
}

[Table("SessionAudits", IsColumnAttributeRequired = false)]
public sealed class SessionAudit
{
    [PrimaryKey, Identity]
    public long Id { get; set; }
    public long OccurredAt { get; set; }
    public string ActorId { get; set; } = "";
    public string TargetAccountId { get; set; } = "";
    // 整個帳號撤銷時沒有單一 Session 可指，留空；Action 會是 revoke_account_sessions。
    public string? SessionId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    // 這次撤銷實際影響的筆數。帳號撤銷可能一次關掉多個裝置，筆數不會固定是 1。
    public int AffectedCount { get; set; }
}

