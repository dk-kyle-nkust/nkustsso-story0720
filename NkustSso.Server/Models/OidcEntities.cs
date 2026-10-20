using LinqToDB.Mapping;

namespace NkustSso.Server.Models;

// Shared scalar storage shape; each protocol entity has its own table.
public abstract class OidcEntity
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column] public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    [Column, Nullable] public string? ApplicationId { get; set; }
    [Column, Nullable] public string? AuthorizationId { get; set; }
    [Column, Nullable] public string? ApplicationType { get; set; }
    [Column, Nullable] public string? ClientId { get; set; }
    [Column, Nullable] public string? ClientSecret { get; set; }
    [Column, Nullable] public string? ClientType { get; set; }
    [Column, Nullable] public string? ConsentType { get; set; }
    [Column, Nullable] public string? DisplayName { get; set; }
    [Column, Nullable] public string? DisplayNames { get; set; }
    [Column, Nullable] public string? Description { get; set; }
    [Column, Nullable] public string? Descriptions { get; set; }
    [Column, Nullable] public string? JsonWebKeySet { get; set; }
    [Column, Nullable] public string? Name { get; set; }
    [Column, Nullable] public string? Payload { get; set; }
    [Column, Nullable] public string? Permissions { get; set; }
    [Column, Nullable] public string? PostLogoutRedirectUris { get; set; }
    [Column, Nullable] public string? Properties { get; set; }
    [Column, Nullable] public string? RedirectUris { get; set; }
    [Column, Nullable] public string? ReferenceId { get; set; }
    [Column, Nullable] public string? Requirements { get; set; }
    [Column, Nullable] public string? Resources { get; set; }
    [Column, Nullable] public string? Scopes { get; set; }
    [Column, Nullable] public string? Settings { get; set; }
    [Column, Nullable] public string? Status { get; set; }
    [Column, Nullable] public string? Subject { get; set; }
    [Column, Nullable] public string? Type { get; set; }
    [Column, Nullable] public long? CreationDate { get; set; }
    [Column, Nullable] public long? ExpirationDate { get; set; }
    [Column, Nullable] public long? RedemptionDate { get; set; }
}

[Table("OidcApplications")]
public sealed class OidcApplication : OidcEntity { }

[Table("OidcAuthorizations")]
public sealed class OidcAuthorization : OidcEntity { }

[Table("OidcScopes")]
public sealed class OidcScope : OidcEntity { }

[Table("OidcTokens")]
public sealed class OidcToken : OidcEntity { }
