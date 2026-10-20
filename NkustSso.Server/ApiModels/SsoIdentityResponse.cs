// 一次性登入票券兌換回應的公開欄位契約。
using System.Text.Json.Serialization;

namespace NkustSso.Server.ApiModels;

public sealed record SsoIdentityResponse(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("sub")] string Subject,
    [property: JsonPropertyName("username"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UserName,
    [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName,
    [property: JsonPropertyName("identity_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IdentityType,
    [property: JsonPropertyName("authenticated_at")] long AuthenticatedAt,
    [property: JsonPropertyName("session_expires_at")] long SessionExpiresAt,
    [property: JsonPropertyName("class_code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClassCode,
    [property: JsonPropertyName("class_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClassName,
    [property: JsonPropertyName("student_status_code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StudentStatusCode,
    [property: JsonPropertyName("student_status_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StudentStatusName,
    [property: JsonPropertyName("education_level_code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EducationLevelCode,
    [property: JsonPropertyName("education_level_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EducationLevelName);
