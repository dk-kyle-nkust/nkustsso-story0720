-- NkustSSO 空白資料庫（SQL Server 2019 或更新版本）
-- 資料庫名稱：NkustSSO
-- 在 SSMS 開啟本檔並執行全部內容。只有資料結構，沒有任何帳號或業務資料。
-- 目標資料庫已有使用者資料表時停止；不刪除、不清空、不覆蓋既有資料。
-- 第一次啟動網站後，程式會建立預設選單、分類與 admin 群組；不自動建立會員。
-- 連線設定的 Database 請填 NkustSSO；校務來源資料庫不在本檔範圍。
USE [master];
GO
IF DB_ID(N'NkustSSO') IS NULL
    EXEC(N'CREATE DATABASE [NkustSSO]');
GO
USE [NkustSSO];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
        THROW 51000, N'目標資料庫已有資料表，已停止；請使用全新或沒有資料表的 NkustSSO。', 1;
    CREATE TABLE [dbo].[AccountIdentities]
    (
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [IdentityType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AttributesJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [ValidFrom] bigint NULL,
        [ValidUntil] bigint NULL
    );

    CREATE TABLE [dbo].[AccountIdentitySets]
    (
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [UpdatedAt] bigint NOT NULL,
        [Source] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[AdminManagementAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Kind] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [TargetId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [BeforeJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AfterJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[AdminMenus]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Label] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Group] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Target] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Icon] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SortOrder] int NOT NULL,
        [Enabled] bit NOT NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[AdminRoleMembers]
    (
        [RoleId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[AdminRolePermissions]
    (
        [RoleId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Permission] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[AdminRoles]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [StartsAt] bigint NULL,
        [ExpiresAt] bigint NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[AspNetRoles]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [NormalizedName] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ConcurrencyStamp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[AspNetUserRoles]
    (
        [UserId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [RoleId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[AspNetUsers]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [UserName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [NormalizedUserName] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Email] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [NormalizedEmail] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SecurityStamp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ConcurrencyStamp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PhoneNumber] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset(7) NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        [AuthenticationSource] nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL,
        [SourceAccountId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [IdentityType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SourceStatusCode] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [SourceStatusName] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ClassCode] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClassName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [StudentStatusCode] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [StudentStatusName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [EducationLevelCode] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [EducationLevelName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL
    );

    CREATE TABLE [dbo].[CampusLoginAttempts]
    (
        [NormalizedName] nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [FailedCount] int NOT NULL,
        [LockedUntil] bigint NOT NULL,
        [UpdatedAt] bigint NOT NULL
    );

    CREATE TABLE [dbo].[IdentityCatalogAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Target] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [BeforeJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AfterJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IdentityDimensions]
    (
        [Key] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Family] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SortOrder] int NOT NULL,
        [Enabled] bit NOT NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IdentityOptions]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Dimension] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Code] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Label] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Family] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IntegrationAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Detail] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IntegrationCapabilities]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IntegrationClientReturnFields]
    (
        [ClientId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [FieldKey] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[IntegrationClients]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ConnectionKind] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Purpose] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [OwningUnit] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SourceSystem] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [SourceId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ImportBatchId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ImportedAt] bigint NULL,
        [Protocol] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [AllowedIps] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicantName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ContactName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ContactPhone] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ContactEmail] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Notes] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SecretHash] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CallerName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CallerPasswordHash] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AllowedFunctionIds] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[IntegrationClientServices]
    (
        [ClientId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ServiceId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[IntegrationConsumedTickets]
    (
        [Hash] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ExpiresAt] bigint NOT NULL
    );

    CREATE TABLE [dbo].[LoginEvents]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] datetimeoffset(7) NOT NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [Outcome] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[LoginSessions]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [KeyHash] nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [UserName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SecurityStamp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ProtectedTicket] varbinary(max) NULL,
        [CreatedAt] bigint NOT NULL,
        [LastSeenAt] bigint NOT NULL,
        [ExpiresAt] bigint NOT NULL,
        [IpAddress] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [UserAgent] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RevokedAt] bigint NULL,
        [RevokedBy] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RevocationReason] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[LoginTickets]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [TicketHash] nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [SessionId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUri] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CodeChallenge] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CreatedAt] bigint NOT NULL,
        [ExpiresAt] bigint NOT NULL,
        [ConsumedAt] bigint NULL,
        [IssuedIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedeemedIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[OidcApplications]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ConcurrencyToken] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AuthorizationId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientSecret] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ConsentType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayNames] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Descriptions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [JsonWebKeySet] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Payload] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Permissions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PostLogoutRedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Properties] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ReferenceId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Requirements] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Resources] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Scopes] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Settings] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Status] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Subject] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Type] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CreationDate] bigint NULL,
        [ExpirationDate] bigint NULL,
        [RedemptionDate] bigint NULL
    );

    CREATE TABLE [dbo].[OidcAuthorizations]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ConcurrencyToken] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AuthorizationId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientSecret] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ConsentType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayNames] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Descriptions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [JsonWebKeySet] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Payload] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Permissions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PostLogoutRedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Properties] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ReferenceId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Requirements] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Resources] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Scopes] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Settings] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Status] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Subject] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Type] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CreationDate] bigint NULL,
        [ExpirationDate] bigint NULL,
        [RedemptionDate] bigint NULL
    );

    CREATE TABLE [dbo].[OidcScopes]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ConcurrencyToken] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AuthorizationId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientSecret] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ConsentType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayNames] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Descriptions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [JsonWebKeySet] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Payload] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Permissions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PostLogoutRedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Properties] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ReferenceId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Requirements] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Resources] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Scopes] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Settings] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Status] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Subject] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Type] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CreationDate] bigint NULL,
        [ExpirationDate] bigint NULL,
        [RedemptionDate] bigint NULL
    );

    CREATE TABLE [dbo].[OidcTokens]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ConcurrencyToken] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AuthorizationId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [ApplicationType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientSecret] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ConsentType] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayNames] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Descriptions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [JsonWebKeySet] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Payload] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Permissions] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PostLogoutRedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Properties] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUris] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ReferenceId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Requirements] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Resources] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Scopes] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Settings] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Status] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Subject] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Type] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CreationDate] bigint NULL,
        [ExpirationDate] bigint NULL,
        [RedemptionDate] bigint NULL
    );

    CREATE TABLE [dbo].[PortalAudiences]
    (
        [ClientId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [IdentityTypesJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [StudentStatusesJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [EducationLevelsJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RulesJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[PortalCategories]
    (
        [Key] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Name] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [NameEn] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Accent] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SortOrder] int NOT NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[PortalCategoryAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [CategoryKey] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [BeforeJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AfterJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[RegisteredSystems]
    (
        [ClientId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Protocol] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DisplayNameEn] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Description] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [DescriptionEn] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [EntryUrl] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUri] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [PostLogoutRedirectUri] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [SamlEntityId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SamlSigningCertificate] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ShowOnPortal] bit NOT NULL,
        [PublicStudent] bit NOT NULL,
        [PublicEmployee] bit NOT NULL,
        [PublicAlumni] bit NOT NULL,
        [RequireLoginToView] bit NOT NULL,
        [RequirePortalLogin] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [ClaimMask] int NOT NULL,
        [Category] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Keywords] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SamlEntityHash] AS (CONVERT([binary](32),hashbytes('SHA2_256',[SamlEntityId]))) PERSISTED
    );

    CREATE TABLE [dbo].[SamlAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [Outcome] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[SamlLoginRequests]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [RequestHash] nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL,
        [RequestId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Revision] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [CorrelationHash] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RelayState] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [IsPassive] bit NOT NULL,
        [ExpiresAt] bigint NOT NULL,
        [ConsumedAt] bigint NULL
    );

    CREATE TABLE [dbo].[SchemaMigrations]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL
    );

    CREATE TABLE [dbo].[SessionAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [TargetAccountId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SessionId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Reason] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AffectedCount] int NOT NULL
    );

    CREATE TABLE [dbo].[SystemConfigurationAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ActorId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [BeforeJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [AfterJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[TicketAudits]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [OccurredAt] bigint NOT NULL,
        [ClientId] nvarchar(256) COLLATE Latin1_General_100_BIN2 NULL,
        [AccountId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [TicketId] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Action] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Outcome] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RemoteIp] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL
    );

    CREATE TABLE [dbo].[TicketClients]
    (
        [Id] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [DisplayName] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [SecretHash] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [RedirectUri] nvarchar(max) COLLATE Latin1_General_100_BIN2 NULL,
        [Enabled] bit NOT NULL,
        [IncludeStudentProfile] bit NOT NULL
    );

    ALTER TABLE [dbo].[AccountIdentities] ADD CONSTRAINT [PK_AccountIdentities] PRIMARY KEY CLUSTERED ([AccountId] ASC, [Id] ASC);
    ALTER TABLE [dbo].[AccountIdentitySets] ADD CONSTRAINT [PK_AccountIdentitySets] PRIMARY KEY CLUSTERED ([AccountId] ASC);
    ALTER TABLE [dbo].[AdminManagementAudits] ADD CONSTRAINT [PK_AdminManagementAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[AdminMenus] ADD CONSTRAINT [PK_AdminMenus] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[AdminRoleMembers] ADD CONSTRAINT [PK_AdminRoleMembers] PRIMARY KEY CLUSTERED ([RoleId] ASC, [AccountId] ASC);
    ALTER TABLE [dbo].[AdminRolePermissions] ADD CONSTRAINT [PK_AdminRolePermissions] PRIMARY KEY CLUSTERED ([RoleId] ASC, [Permission] ASC);
    ALTER TABLE [dbo].[AdminRoles] ADD CONSTRAINT [PK_AdminRoles] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[AspNetRoles] ADD CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName] ASC) WHERE ([NormalizedName] IS NOT NULL);
    ALTER TABLE [dbo].[AspNetUserRoles] ADD CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [AccountSourceIndex] ON [dbo].[AspNetUsers] ([AuthenticationSource] ASC, [SourceAccountId] ASC) WHERE ([AuthenticationSource]<>'');
    ALTER TABLE [dbo].[AspNetUsers] ADD CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName] ASC) WHERE ([NormalizedUserName] IS NOT NULL);
    ALTER TABLE [dbo].[CampusLoginAttempts] ADD CONSTRAINT [PK_CampusLoginAttempts] PRIMARY KEY CLUSTERED ([NormalizedName] ASC);
    ALTER TABLE [dbo].[IdentityCatalogAudits] ADD CONSTRAINT [PK_IdentityCatalogAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[IdentityDimensions] ADD CONSTRAINT [PK_IdentityDimensions] PRIMARY KEY CLUSTERED ([Key] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [IdentityOptionCodeIndex] ON [dbo].[IdentityOptions] ([Dimension] ASC, [Code] ASC);
    ALTER TABLE [dbo].[IdentityOptions] ADD CONSTRAINT [PK_IdentityOptions] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE NONCLUSTERED INDEX [IntegrationAuditTimeIndex] ON [dbo].[IntegrationAudits] ([OccurredAt] ASC);
    ALTER TABLE [dbo].[IntegrationAudits] ADD CONSTRAINT [PK_IntegrationAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[IntegrationCapabilities] ADD CONSTRAINT [PK_IntegrationCapabilities] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[IntegrationClientReturnFields] ADD CONSTRAINT [PK_IntegrationClientReturnFields] PRIMARY KEY CLUSTERED ([ClientId] ASC, [FieldKey] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [IntegrationClientSourceIndex] ON [dbo].[IntegrationClients] ([SourceSystem] ASC, [SourceId] ASC) WHERE ([SourceSystem]<>'' AND [SourceId]<>'');
    ALTER TABLE [dbo].[IntegrationClients] ADD CONSTRAINT [PK_IntegrationClients] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[IntegrationClientServices] ADD CONSTRAINT [PK_IntegrationClientServices] PRIMARY KEY CLUSTERED ([ClientId] ASC, [ServiceId] ASC);
    ALTER TABLE [dbo].[IntegrationConsumedTickets] ADD CONSTRAINT [PK_IntegrationConsumedTickets] PRIMARY KEY CLUSTERED ([Hash] ASC);
    ALTER TABLE [dbo].[LoginEvents] ADD CONSTRAINT [PK_LoginEvents] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[LoginSessions] ADD CONSTRAINT [PK_LoginSessions] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE NONCLUSTERED INDEX [SessionAccountIndex] ON [dbo].[LoginSessions] ([AccountId] ASC, [ExpiresAt] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [SessionKeyIndex] ON [dbo].[LoginSessions] ([KeyHash] ASC);
    CREATE NONCLUSTERED INDEX [LoginTicketExpiryIndex] ON [dbo].[LoginTickets] ([ExpiresAt] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [LoginTicketHashIndex] ON [dbo].[LoginTickets] ([TicketHash] ASC);
    ALTER TABLE [dbo].[LoginTickets] ADD CONSTRAINT [PK_LoginTickets] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [OidcClientIndex] ON [dbo].[OidcApplications] ([ClientId] ASC) WHERE ([ClientId] IS NOT NULL);
    ALTER TABLE [dbo].[OidcApplications] ADD CONSTRAINT [PK_OidcApplications] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[OidcAuthorizations] ADD CONSTRAINT [PK_OidcAuthorizations] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [OidcScopeNameIndex] ON [dbo].[OidcScopes] ([Name] ASC) WHERE ([Name] IS NOT NULL);
    ALTER TABLE [dbo].[OidcScopes] ADD CONSTRAINT [PK_OidcScopes] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [OidcReferenceIndex] ON [dbo].[OidcTokens] ([ReferenceId] ASC) WHERE ([ReferenceId] IS NOT NULL);
    CREATE NONCLUSTERED INDEX [OidcTokenAuthIndex] ON [dbo].[OidcTokens] ([AuthorizationId] ASC);
    ALTER TABLE [dbo].[OidcTokens] ADD CONSTRAINT [PK_OidcTokens] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[PortalAudiences] ADD CONSTRAINT [PK_PortalAudiences] PRIMARY KEY CLUSTERED ([ClientId] ASC);
    ALTER TABLE [dbo].[PortalCategories] ADD CONSTRAINT [PK_PortalCategories] PRIMARY KEY CLUSTERED ([Key] ASC);
    ALTER TABLE [dbo].[PortalCategoryAudits] ADD CONSTRAINT [PK_PortalCategoryAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[RegisteredSystems] ADD CONSTRAINT [PK_RegisteredSystems] PRIMARY KEY CLUSTERED ([ClientId] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [SamlEntityIndex] ON [dbo].[RegisteredSystems] ([SamlEntityHash] ASC) WHERE ([Protocol]='saml');
    ALTER TABLE [dbo].[SamlAudits] ADD CONSTRAINT [PK_SamlAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[SamlLoginRequests] ADD CONSTRAINT [PK_SamlLoginRequests] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [SamlRequestReplayIndex] ON [dbo].[SamlLoginRequests] ([RequestHash] ASC);
    ALTER TABLE [dbo].[SchemaMigrations] ADD CONSTRAINT [PK_SchemaMigrations] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[SessionAudits] ADD CONSTRAINT [PK_SessionAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[SystemConfigurationAudits] ADD CONSTRAINT [PK_SystemConfigurationAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    ALTER TABLE [dbo].[TicketAudits] ADD CONSTRAINT [PK_TicketAudits] PRIMARY KEY CLUSTERED ([Id] ASC);
    CREATE NONCLUSTERED INDEX [TicketAuditTimeIndex] ON [dbo].[TicketAudits] ([OccurredAt] ASC);
    ALTER TABLE [dbo].[TicketClients] ADD CONSTRAINT [PK_TicketClients] PRIMARY KEY CLUSTERED ([Id] ASC);

    COMMIT TRANSACTION;
    PRINT N'NkustSSO 空白資料庫結構已建立。';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
