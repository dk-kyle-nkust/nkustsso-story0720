// 中央登入清單與期限的顯示資料。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 中央登入管理列表資料；區分可續期到期時間與最長登入期限。
/// </summary>
public sealed class LoginSessionListViewModel
{
    public string? Account { get; init; }
    public string Status { get; init; } = "active";
    public long Now { get; init; }
    public long MaximumMilliseconds { get; init; }
    public long AbsoluteExpiry(LoginSession session) => session.CreatedAt + MaximumMilliseconds;
    public long EffectiveExpiry(LoginSession session) => Math.Min(session.ExpiresAt, AbsoluteExpiry(session));
    public string? CurrentSession { get; init; }
    public List<LoginSession> Sessions { get; init; } = [];
    public List<SessionAudit> Audits { get; init; } = [];
    public static string Time(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value)
        .ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss");
}
