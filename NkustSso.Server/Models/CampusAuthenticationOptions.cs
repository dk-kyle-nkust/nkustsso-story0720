namespace NkustSso.Server.Models;

public sealed class CampusAuthenticationOptions
{
    public bool Enabled { get; set; }
    // 僅供 Development 中央登入測試，預設仍驗證來源密碼。
    public bool DevelopmentAllowAnyPassword { get; set; }
    // sts_id 僅用於身分分類，不作為 SSO 登入允許清單。
    public Dictionary<string, string> StatusIdentityTypes { get; set; } = new()
    {
        ["02"] = "student", ["01"] = "employee", ["05"] = "employee", ["06"] = "employee", ["08"] = "employee"
    };
    public int CommandTimeoutSeconds { get; set; } = 10;

    public bool IsValid() => CommandTimeoutSeconds is >= 1 and <= 30 && StatusIdentityTypes is not null &&
        StatusIdentityTypes.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Key == pair.Key.Trim()
            && pair.Value is "student" or "employee" or "teacher" or "staff" or "other");

    public string IdentityType(string? status) => status is not null
        && StatusIdentityTypes.TryGetValue(status.Trim(), out var type) ? type : "other";
}
