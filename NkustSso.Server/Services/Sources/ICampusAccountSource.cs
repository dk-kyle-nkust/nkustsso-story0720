// 定義校務帳密驗證與帳號狀態查詢介面。
namespace NkustSso.Server.Services.Sources;

public sealed record CampusAccountProfile(
    string MemberId,
    string Name,
    string IdentityType,
    string ClassCode = "",
    string ClassName = "",
    string EducationLevelCode = "",
    string SourceStatusCode = "",
    string SourceStatusName = "",
    string UnitCode = "",
    string UnitName = "");

public interface ICampusAccountSource
{
    Task<CampusAccountProfile?> AuthenticateAsync(
        string memberId,
        string password,
        CancellationToken cancellationToken);

    Task<bool> IsAllowedAsync(string memberId, string identityType, CancellationToken cancellationToken);
}
