# NKUST-OOSA Codebase AST Architecture Report

- **Generated At**: 2026-10-20 05:54:08 UTC
- **Total C# Files**: 35
- **Total Classes**: 74
- **Security Violations**: 0

## Key Classes and Interfaces

| Namespace | Class Name | Base / Interface | Methods | Summary |
| :--- | :--- | :--- | :---: | :--- |
| `NkustSso.Server.Models` | `[Account]` | `IdentityUser` | 0 | N/A |
| `NkustSso.Server.Models` | `[LoginEvent]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AccountRole]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AdminMenu]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AdminRole]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AdminRolePermission]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AdminRoleMember]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AdminManagementAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[CampusAuthenticationOptions]` | `None` | 2 | N/A |
| `NkustSso.Server.Models` | `[ICampusDatabaseFactory]` | `None` | 1 | N/A |
| `NkustSso.Server.Models` | `[CampusDatabaseFactory]` | `None` | 1 | N/A |
| `NkustSso.Server.Models` | `[CampusLoginAttempt]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IdentityDimension]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IdentityOption]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IdentityCatalogAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AccountIdentitySet]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[AccountIdentity]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationClientReturnField]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationConsumedTicket]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationClient]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationClientInput]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationCapability]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationClientService]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[IntegrationServiceOptions]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[LinqUserStore]` | `None` | 46 | N/A |
| `NkustSso.Server.Models` | `[LinqRoleStore]` | `None` | 46 | N/A |
| `NkustSso.Server.Models` | `[LoginArea]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[LoginSession]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SessionAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[TicketClient]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[LoginTicket]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[TicketAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[OidcEntity]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[OidcApplication]` | `OidcEntity` | 0 | N/A |
| `NkustSso.Server.Models` | `[OidcAuthorization]` | `OidcEntity` | 0 | N/A |
| `NkustSso.Server.Models` | `[OidcScope]` | `OidcEntity` | 0 | N/A |
| `NkustSso.Server.Models` | `[OidcToken]` | `OidcEntity` | 0 | N/A |
| `NkustSso.Server.Models` | `[PortalAudience]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[PortalCategory]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SchemaMigration]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[PortalCategoryAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[RegisteredSystem]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SystemConfigurationAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SamlLoginRequest]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SamlAudit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SqliteStorageImport]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[DatabaseFactory]` | `None` | 3 | N/A |
| `NkustSso.Server.Models` | `[SsoDatabase]` | `DataConnection` | 3 | N/A |
| `NkustSso.Server.Models` | `[SsoStorage]` | `None` | 0 | N/A |
| `NkustSso.Server.Models` | `[SystemProtocols]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusAreaRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusCredentialRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapAccount]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapEmployee]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapStudent]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapUnit]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapTicket]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapTitle]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[CampusSoapDepartment]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[ClassRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[DivisionRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[EmployeeRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[SourceAccountRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[SourceAccountStatusRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[SourceAccountProfile]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[StudentClassProfile]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[StudentKindRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[StudentRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[StudentTermRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[AcademicTermRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[DegreeRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[StudentStatusRecord]` | `None` | 0 | N/A |
| `NkustSso.Server.Models.External` | `[UnitRecord]` | `None` | 0 | N/A |
