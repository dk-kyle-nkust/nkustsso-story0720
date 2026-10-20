# NKUST-OOSA Codebase AST Architecture Report

- **Generated At**: 2026-10-20 05:54:16 UTC
- **Total C# Files**: 54
- **Total Classes**: 87
- **Security Violations**: 0

## Key Classes and Interfaces

| Namespace | Class Name | Base / Interface | Methods | Summary |
| :--- | :--- | :--- | :---: | :--- |
| `NkustSso.Server.Services` | `[IdentityProfile]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[AccountIdentityService]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[in]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[in]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[in]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[AdminFeature]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[AdminNavigationItem]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[AdminAccessSnapshot]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[AdminAuthorizationService]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[AdminManagementService]` | `None` | 4 | N/A |
| `NkustSso.Server.Services` | `[CampusLoginGates]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusLoginResult]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusLoginService]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusRoleCandidate]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusRoleMember]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusRoleMemberService]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[CampusSessionValidator]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[ServiceVerification]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[IIntegrationAccountSource]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[CredentialVerificationService]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[CurrentUserDisplayService]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[DepartmentHierarchy]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[ExpiredTicketCleanupService]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[IdentityCatalogSnapshot]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[IdentityCatalog]` | `None` | 7 | N/A |
| `NkustSso.Server.Services` | `[IdentityClaimReleasePolicy]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[Field]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[IntegrationClientRegistrationService]` | `None` | 6 | N/A |
| `NkustSso.Server.Services` | `[IntegrationManualService]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[ManualCatalog]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[IntegrationPermissions]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[IntegrationReturnFields]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[IntegrationReturnField]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[IntegrationReturnContract]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[KeyProtectionCertificate]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapContract]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapOperationCatalog]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapRequest]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapOperationDispatcher]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapResult]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[LegacySoapWsdlGenerator]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[LinqApplicationStore]` | `None` | 27 | N/A |
| `NkustSso.Server.Services` | `[LinqAuthorizationStore]` | `None` | 19 | N/A |
| `NkustSso.Server.Services` | `[LinqOidcStore]` | `None` | 11 | N/A |
| `NkustSso.Server.Services` | `[was]` | `None` | 11 | N/A |
| `NkustSso.Server.Services` | `[LinqScopeStore]` | `None` | 13 | N/A |
| `NkustSso.Server.Services` | `[LinqTokenStore]` | `None` | 30 | N/A |
| `NkustSso.Server.Services` | `[LoginSessionAdministration]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[SessionLifetimeOptions]` | `None` | 4 | N/A |
| `NkustSso.Server.Services` | `[LoginSessionLifetimeService]` | `None` | 4 | N/A |
| `NkustSso.Server.Services` | `[LoginSessionStore]` | `None` | 5 | N/A |
| `NkustSso.Server.Services` | `[LoginTicketService]` | `None` | 5 | N/A |
| `NkustSso.Server.Services` | `[OidcIdentityClaims]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[PortalCategoryManagementService]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[PortalCategoryService]` | `None` | 1 | N/A |
| `NkustSso.Server.Services` | `[PortalDefaultDataInitializer]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[Entry]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[PortalDirectoryService]` | `None` | 2 | N/A |
| `NkustSso.Server.Services` | `[AudienceCondition]` | `None` | 6 | N/A |
| `NkustSso.Server.Services` | `[AudienceGroup]` | `None` | 6 | N/A |
| `NkustSso.Server.Services` | `[PortalAudienceSelection]` | `None` | 6 | N/A |
| `NkustSso.Server.Services` | `[SamlIdentityProvider]` | `IDisposable` | 5 | N/A |
| `NkustSso.Server.Services` | `[SoapHelpCatalog]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[SsoStartupInitializer]` | `None` | 0 | N/A |
| `NkustSso.Server.Services` | `[SsoSystemRegistrationService]` | `None` | 3 | N/A |
| `NkustSso.Server.Services` | `[TienFangPasswordVerifier]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.SoapDefinitions` | `[SoapHelpOperation]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.SoapDefinitions` | `[SoapHelpParameter]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[CampusIdentityCatalog]` | `None` | 2 | N/A |
| `NkustSso.Server.Services.Sources` | `[CampusSoapAccountSource]` | `None` | 1 | N/A |
| `NkustSso.Server.Services.Sources` | `[ICampusSoapQueries]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[CampusSoapQueries]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[DocumentPaymentRow]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[DivisionReader]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[EmployeeReader]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[CampusAccountProfile]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[ICampusAccountSource]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[ILegacyDirectoryAuthenticator]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[ILegacyMappingDatabase]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[LegacyMappingDatabase]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[LegacyDirectoryAuthenticator]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[IntegrationAccountSource]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[LegacyMappingCredential]` | `None` | 3 | N/A |
| `NkustSso.Server.Services.Sources` | `[MssqlCampusAccountSource]` | `None` | 2 | N/A |
| `NkustSso.Server.Services.Sources` | `[SourceAccountReader]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[StudentReader]` | `None` | 0 | N/A |
| `NkustSso.Server.Services.Sources` | `[UnitReader]` | `None` | 0 | N/A |
