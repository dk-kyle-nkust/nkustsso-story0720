-- ============================================================================
-- 主機 / 目標:         Microsoft SQL Server (T-SQL)
-- 資料庫:             NkustSSO
-- 結構描述 (Schema):   dbo
-- 來源檔案:           database/NkustSSO-ALL-HeidiSQL.sql (SQLite 傾印檔)
-- 輸出檔案:           database/NkustSSO-ALL-HeidiSQL-MS.sql
-- 相容性:             Microsoft SQL Server 2016+ / 2019 / 2022 / Azure SQL
-- 產出時間:           2026-10-21
-- ============================================================================

USE [master];
GO

IF DB_ID(N'NkustSSO') IS NULL
BEGIN
    -- CREATE DATABASE [NkustSSO] COLLATE Latin1_General_100_BIN2;
	CREATE DATABASE [NkustSSO];
END
GO

USE [NkustSSO];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AccountIdentities]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AccountIdentities]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AccountIdentities];
GO

CREATE TABLE [dbo].[AccountIdentities]
(
	[AccountId]                nvarchar(255)    NOT NULL,
	[Id]                       nvarchar(255)    NOT NULL,
	[IdentityType]             nvarchar(255)    NULL,
	[AttributesJson]           nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[ValidFrom]                bigint           NULL,
	[ValidUntil]               bigint           NULL,
	CONSTRAINT [PK_AccountIdentities] PRIMARY KEY ([AccountId], [Id])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AccountIdentitySets]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AccountIdentitySets]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AccountIdentitySets];
GO

CREATE TABLE [dbo].[AccountIdentitySets]
(
	[AccountId]                nvarchar(255)    NOT NULL,
	[Revision]                 nvarchar(255)    NULL,
	[UpdatedAt]                bigint           NOT NULL,
	[Source]                   nvarchar(255)    NULL,
	CONSTRAINT [PK_AccountIdentitySets] PRIMARY KEY ([AccountId])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AdminManagementAudits]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AdminManagementAudits]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AdminManagementAudits];
GO

CREATE TABLE [dbo].[AdminManagementAudits]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               bigint           NOT NULL,
	[ActorId]                  nvarchar(255)    NULL,
	[RemoteIp]                 nvarchar(255)    NULL,
	[Kind]                     nvarchar(255)    NULL,
	[TargetId]                 nvarchar(255)    NULL,
	[Action]                   nvarchar(255)    NULL,
	[BeforeJson]               nvarchar(255)    NULL,
	[AfterJson]                nvarchar(255)    NULL
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AdminMenus]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AdminMenus]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AdminMenus];
GO

CREATE TABLE [dbo].[AdminMenus]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Label]                    nvarchar(255)    NULL,
	[Group]                    nvarchar(255)    NULL,
	[Target]                   nvarchar(255)    NULL,
	[Icon]                     nvarchar(255)    NULL,
	[SortOrder]                int              NOT NULL,
	[Enabled]                  bit              NOT NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_AdminMenus] PRIMARY KEY ([Id])
);
GO

-- 傾印資料表 [dbo].[AdminMenus] 資料：9 筆
INSERT INTO [dbo].[AdminMenus] ([Id], [Label], [Group], [Target], [Icon], [SortOrder], [Enabled], [Revision]) VALUES
	(N'access', N'權限管理', N'系統管理', N'access', N'people', 80, 1, N'673e95ab44d540b894a17ab898e7a2a8'),
	(N'categories', N'首頁分類', N'服務設定', N'categories', N'folder', 20, 1, N'5a9ed63fcca34640868a4491bd49d1fa'),
	(N'identities', N'身分分類', N'服務設定', N'identities', N'people', 30, 1, N'aeb7c942d6fd4030ba926a18a4da3830'),
	(N'manuals', N'串接手冊', N'服務設定', N'manuals', N'folder', 35, 1, N'6e14fd60bf0a4f0f91941c958fd57ed0'),
	(N'menus', N'選單維護', N'系統管理', N'menus', N'folder', 70, 1, N'e488a5edf9984c3397f860fb563357fc'),
	(N'progress', N'開發進度', N'紀錄與維護', N'progress', N'activity', 60, 1, N'e3a276bcb36946b9ac6bd1d88aa7a0cb'),
	(N'sessions', N'登入管理', N'紀錄與維護', N'sessions', N'shield', 40, 1, N'365a2000d7c543078c19f6d078dcfa84'),
	(N'systems', N'串接系統管理', N'服務設定', N'systems', N'grid', 10, 1, N'eb7c642ac99c403da390524c4c9d406e'),
	(N'tickets', N'票券紀錄', N'紀錄與維護', N'tickets', N'ticket', 50, 1, N'd92366501f0f42b792437633f51c7edf');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AdminRoleMembers]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AdminRoleMembers]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AdminRoleMembers];
GO

CREATE TABLE [dbo].[AdminRoleMembers]
(
	[RoleId]                   nvarchar(255)    NOT NULL,
	[AccountId]                nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_AdminRoleMembers] PRIMARY KEY ([RoleId], [AccountId])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AdminRolePermissions]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AdminRolePermissions]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AdminRolePermissions];
GO

CREATE TABLE [dbo].[AdminRolePermissions]
(
	[RoleId]                   nvarchar(255)    NOT NULL,
	[Permission]               nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_AdminRolePermissions] PRIMARY KEY ([RoleId], [Permission])
);
GO

-- 傾印資料表 [dbo].[AdminRolePermissions] 資料：8 筆
INSERT INTO [dbo].[AdminRolePermissions] ([RoleId], [Permission]) VALUES
	(N'admin', N'categories'),
	(N'admin', N'identities'),
	(N'admin', N'integrations'),
	(N'admin', N'manuals'),
	(N'admin', N'progress'),
	(N'admin', N'sessions'),
	(N'admin', N'systems'),
	(N'admin', N'tickets');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AdminRoles]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AdminRoles]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AdminRoles];
GO

CREATE TABLE [dbo].[AdminRoles]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[StartsAt]                 bigint           NULL,
	[ExpiresAt]                bigint           NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_AdminRoles] PRIMARY KEY ([Id])
);
GO

-- 傾印資料表 [dbo].[AdminRoles] 資料：1 筆
INSERT INTO [dbo].[AdminRoles] ([Id], [Name], [Description], [Enabled], [StartsAt], [ExpiresAt], [Revision]) VALUES
	(N'admin', N'admin', N'預設後台管理群組，可維護一般後台功能；選單與權限管理仍限最高管理者。成員須另行指派。', 1, NULL, NULL, N'5765872cd96c40ba9d07d0c07fcc03cc');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AspNetRoles]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AspNetRoles];
GO

CREATE TABLE [dbo].[AspNetRoles]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	[NormalizedName]           nvarchar(255)    NULL,
	[ConcurrencyStamp]         nvarchar(255)    NULL,
	CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

-- 傾印資料表 [dbo].[AspNetRoles] 資料：1 筆
INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES
	(N'8994cae4-ea1c-4c87-a558-4a7a1d792d5d', N'SsoAdministrator', N'SSOADMINISTRATOR', N'474a16c3-7f75-4a5c-908c-575ca61cefd6');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AspNetUserRoles]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AspNetUserRoles];
GO

CREATE TABLE [dbo].[AspNetUserRoles]
(
	[UserId]                   nvarchar(255)    NOT NULL,
	[RoleId]                   nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[AspNetUsers]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
    DROP TABLE [dbo].[AspNetUsers];
GO

CREATE TABLE [dbo].[AspNetUsers]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[UserName]                 nvarchar(255)    NULL,
	[NormalizedUserName]       nvarchar(255)    NULL,
	[Email]                    nvarchar(255)    NULL,
	[NormalizedEmail]          nvarchar(255)    NULL,
	[EmailConfirmed]           bit              NOT NULL,
	[PasswordHash]             nvarchar(255)    NULL,
	[SecurityStamp]            nvarchar(255)    NULL,
	[ConcurrencyStamp]         nvarchar(255)    NULL,
	[PhoneNumber]              nvarchar(255)    NULL,
	[PhoneNumberConfirmed]     bit              NOT NULL,
	[TwoFactorEnabled]         bit              NOT NULL,
	[LockoutEnd]               datetimeoffset   NULL,
	[LockoutEnabled]           bit              NOT NULL,
	[AccessFailedCount]        int              NOT NULL,
	[AuthenticationSource]     nvarchar(255)    NULL,
	[SourceAccountId]          nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[IdentityType]             nvarchar(255)    NULL,
	[SourceStatusCode]         nvarchar(255)    NULL,
	[SourceStatusName]         nvarchar(255)    NULL,
	[UnitCode]                 nvarchar(255)    NULL,
	[UnitName]                 nvarchar(255)    NULL,
	[ClassCode]                nvarchar(255)    NULL,
	[ClassName]                nvarchar(255)    NULL,
	[StudentStatusCode]        nvarchar(255)    NULL,
	[StudentStatusName]        nvarchar(255)    NULL,
	[EducationLevelCode]       nvarchar(255)    NULL,
	[EducationLevelName]       nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [AccountSourceIndex] ON [dbo].[AspNetUsers] ([AuthenticationSource], [SourceAccountId]) WHERE [AuthenticationSource] <> '';
GO

CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

-- 傾印資料表 [dbo].[AspNetUsers] 資料：1 筆
INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [AuthenticationSource], [SourceAccountId], [DisplayName], [IdentityType], [SourceStatusCode], [SourceStatusName], [UnitCode], [UnitName], [ClassCode], [ClassName], [StudentStatusCode], [StudentStatusName], [EducationLevelCode], [EducationLevelName], [Enabled]) VALUES
	(N'af9675f4741849d4a2c0b4873ccd0c27', N'42893', N'42893', NULL, NULL, 0, N'AQAAAAIAAYagAAAAEM0FPtbvMLAuz1lLDafUpVZ9gYLd5pdP7q81eJXl+ixo/PH0Rw3yOdQGGcp8cDlMLw==', N'66cc0bcb-4ddb-4619-b275-0b60740e0768', N'eaa77aa1-3b1a-4344-afef-ee4dd9144327', NULL, 0, 0, NULL, 1, 0, N'', NULL, N'施登凱', N'約用技術人員', NULL, NULL, N'Software Deveolpment', N'電算中心軟體組', NULL, NULL, NULL, NULL, NULL, NULL, 1);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IdentityCatalogAudits]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IdentityCatalogAudits]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IdentityCatalogAudits];
GO

CREATE TABLE [dbo].[IdentityCatalogAudits]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               bigint           NOT NULL,
	[ActorId]                  nvarchar(255)    NULL,
	[RemoteIp]                 nvarchar(255)    NULL,
	[Target]                   nvarchar(255)    NULL,
	[BeforeJson]               nvarchar(255)    NULL,
	[AfterJson]                nvarchar(255)    NULL
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IdentityDimensions]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IdentityDimensions]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IdentityDimensions];
GO

CREATE TABLE [dbo].[IdentityDimensions]
(
	[Key]                      nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	[Family]                   nvarchar(255)    NULL,
	[SortOrder]                int              NOT NULL,
	[Enabled]                  bit              NOT NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_IdentityDimensions] PRIMARY KEY ([Key])
);
GO

-- 傾印資料表 [dbo].[IdentityDimensions] 資料：10 筆
INSERT INTO [dbo].[IdentityDimensions] ([Key], [Name], [Family], [SortOrder], [Enabled], [Revision]) VALUES
	(N'appointment_type', N'聘任／任用類別', N'employee', 50, 1, N'408d3fe3e21c4ba7929b2e866072e80f'),
	(N'campus', N'校區', N'any', 60, 1, N'f4385f07ee7f41469d035a8ec2a1a504'),
	(N'department', N'系所／單位', N'any', 70, 1, N'd25b16a130a24529a5bed326cabd1f6e'),
	(N'division', N'部別', N'student', 20, 1, N'd6922f846296499287ea5b160c2aaf2a'),
	(N'education_level', N'學制', N'student', 30, 1, N'f5117c2a0939454db84dfaf84e92f55e'),
	(N'employment_status', N'任職狀態', N'employee', 40, 1, N'5a027cc73bfc4e90be2c5aca03552c9c'),
	(N'grade', N'年級', N'student', 80, 1, N'0f95eb6d852e4d749b1f171dc3158152'),
	(N'identity_type', N'身分類別', N'any', 0, 1, N'3fedc1788cb142508de2b3a9281aa28e'),
	(N'student_kind', N'學生狀況', N'student', 15, 1, N'd563472c116041d481f7060fddaf717d'),
	(N'student_status', N'學籍狀態', N'student', 10, 1, N'd7112d8d7d964e169baae858d67a4c5b');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IdentityOptions]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IdentityOptions]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IdentityOptions];
GO

CREATE TABLE [dbo].[IdentityOptions]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Dimension]                nvarchar(255)    NULL,
	[Code]                     nvarchar(255)    NULL,
	[Label]                    nvarchar(255)    NULL,
	[Family]                   nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[SortOrder]                int              NOT NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_IdentityOptions] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IdentityOptionCodeIndex] ON [dbo].[IdentityOptions] ([Dimension], [Code]);
GO

-- 傾印資料表 [dbo].[IdentityOptions] 資料：20 筆
INSERT INTO [dbo].[IdentityOptions] ([Id], [Dimension], [Code], [Label], [Family], [Enabled], [SortOrder], [Revision]) VALUES
	(N'11a4e269ddb247d39b4c7594197d7a5a', N'student_status', N'DEMO_ACTIVE', N'在校', N'other', 1, 30, N'e0677a6eb1f74467b8945d0f5bab1143'),
	(N'18cf165836014567ab8517927301a9a9', N'appointment_type', N'DEMO_CONTRACT', N'約聘僱', N'other', 1, 180, N'd3b28cdc6bdf466691a01b33c39e51b4'),
	(N'19e9c9daf9fc458991018df49c466ccf', N'identity_type', N'teacher', N'教師', N'employee', 1, 10, N'edf7cb0b59df4b909a4b1ca12d3f171c'),
	(N'265e42411a5e432b84bf8e41b793e382', N'division', N'DEMO_DAY', N'日間部', N'other', 1, 70, N'2e94f4280a7944f78731c6dcd36e3650'),
	(N'34b8eeda55c248c98cba657a6aa73196', N'identity_type', N'staff', N'一般職員', N'employee', 1, 20, N'c800b9b6dc5c4bf0b1fd5e7995bbb20e'),
	(N'3cdf4ed80e424f90bb15df5f48db6904', N'student_status', N'DEMO_WITHDRAWN', N'退學', N'other', 1, 50, N'fbd0f60118d647039af2efa32115f64e'),
	(N'50702f728fca459f98063d928d1dce7f', N'identity_type', N'student', N'學生', N'student', 1, 0, N'45ea9138e43c45058424bc5bebffb611'),
	(N'5ff8f76f34dc402395feea934fca6a8c', N'appointment_type', N'DEMO_PART_TIME', N'兼任', N'other', 1, 160, N'def8c1e963a84534b7c1390a68088a3a'),
	(N'63c4f5c84eed47019a7c33e30f0c921e', N'education_level', N'DEMO_UG', N'大學部', N'other', 1, 90, N'6ada56e819c54493915da9931ac81b6c'),
	(N'6cb1bb78353f4330bb319fa63a0faa9d', N'education_level', N'DEMO_PHD', N'博士班', N'other', 1, 110, N'6f8c544379e74d1ea1cccf5f9b691edd'),
	(N'730b8c10962a4cdea2a5ccc59bffcf0b', N'division', N'DEMO_NIGHT', N'夜間／進修部', N'other', 1, 80, N'a269f784acc0465c960fd2c2424b7b99'),
	(N'9be58fddd08b4ecaa9588b467aece19e', N'student_status', N'DEMO_GRADUATED', N'畢業', N'other', 1, 60, N'e6ba4269e8684e458720a02f5f205029'),
	(N'a7bf44c8c97b41c08479a0dbc3780a07', N'employment_status', N'DEMO_RETIRED', N'退休', N'other', 1, 140, N'03457c325aa84def8d221bdc42140f08'),
	(N'c2c1e199bb2044d3861e1f353f02ddd5', N'employment_status', N'DEMO_CURRENT', N'現職', N'other', 1, 120, N'ae859b90f4b2407e977c345307cd3025'),
	(N'c8b9f7eb36154e43bf4031556ef03240', N'employment_status', N'DEMO_LEFT', N'離職', N'other', 1, 130, N'9355d7d9fe23445c9520ef1d5c9db588'),
	(N'cee8f957ecea4aa3948af19da436c135', N'identity_type', N'employee', N'教職員（未細分）', N'employee', 1, 25, N'b9ed465a13e54373beb800c520c90215'),
	(N'dc2e0672325b46b997548bd6991526c0', N'education_level', N'DEMO_MASTER', N'碩士班', N'other', 1, 100, N'8ab0970b234b4628afdcf15efabbf9b3'),
	(N'e7ff62a767844bb79bd9c7a3d1dbe147', N'appointment_type', N'DEMO_FULL_TIME', N'專任', N'other', 1, 150, N'debd11bcd6ad4b099c26c70427e61a19'),
	(N'f996448c5e4940d495833378c30fb375', N'student_status', N'DEMO_LEAVE', N'休學', N'other', 1, 40, N'f541cd463b074ea38fb9e527e9405435'),
	(N'fa3ddd1977d9497faeda7069907cb986', N'appointment_type', N'DEMO_CIVIL', N'公務人員', N'other', 1, 170, N'0283c74bdb5243628888af4e835dab11');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IntegrationAudits]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IntegrationAudits]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IntegrationAudits];
GO

CREATE TABLE [dbo].[IntegrationAudits]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               bigint           NOT NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[ActorId]                  nvarchar(255)    NULL,
	[RemoteIp]                 nvarchar(255)    NULL,
	[Action]                   nvarchar(255)    NULL,
	[Detail]                   nvarchar(255)    NULL
);
GO

CREATE NONCLUSTERED INDEX [IntegrationAuditTimeIndex] ON [dbo].[IntegrationAudits] ([OccurredAt]);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IntegrationCapabilities]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IntegrationCapabilities]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IntegrationCapabilities];
GO

CREATE TABLE [dbo].[IntegrationCapabilities]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	CONSTRAINT [PK_IntegrationCapabilities] PRIMARY KEY ([Id])
);
GO

-- 傾印資料表 [dbo].[IntegrationCapabilities] 資料：9 筆
INSERT INTO [dbo].[IntegrationCapabilities] ([Id], [Name]) VALUES
	(N'account.verify', N'帳密驗證'),
	(N'soap.Get_Traffic_Pub', N'交通車資料查詢（公用帳號）（Get_Traffic_Pub）'),
	(N'soap.Get_Traffic_Stud', N'交通車資料查詢（學生）（Get_Traffic_Stud）'),
	(N'soap.Get_Traffic_Stud_Temp', N'交通車學生介面（Temp）（Get_Traffic_Stud_Temp）'),
	(N'soap.Get_Traffic_Teach', N'交通車資料查詢（教職員）（Get_Traffic_Teach）'),
	(N'soap.Get_Traffic_Teach_Temp', N'交通車教職員介面（Temp）（Get_Traffic_Teach_Temp）'),
	(N'soap.KuasSSO_Certify1', N'校務功能票據驗證（KuasSSO_Certify1）'),
	(N'soap.KuasSSO_SchCardSearch', N'校園智慧卡登入（資料庫版本）（KuasSSO_SchCardSearch）'),
	(N'soap.NkustSSO_DocumentApplyPaymentSystemSearch', N'一卡通文件申請／繳費資料（NkustSSO_DocumentApplyPaymentSystemSearch）');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IntegrationClientReturnFields]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IntegrationClientReturnFields]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IntegrationClientReturnFields];
GO

CREATE TABLE [dbo].[IntegrationClientReturnFields]
(
	[ClientId]                 nvarchar(255)    NOT NULL,
	[FieldKey]                 nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_IntegrationClientReturnFields] PRIMARY KEY ([ClientId], [FieldKey])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IntegrationClients]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IntegrationClients]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IntegrationClients];
GO

CREATE TABLE [dbo].[IntegrationClients]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	[ConnectionKind]           nvarchar(255)    NULL,
	[Purpose]                  nvarchar(255)    NULL,
	[OwningUnit]               nvarchar(255)    NULL,
	[SourceSystem]             nvarchar(255)    NULL,
	[SourceId]                 nvarchar(255)    NULL,
	[ImportBatchId]            nvarchar(255)    NULL,
	[ImportedAt]               bigint           NULL,
	[Protocol]                 nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[AllowedIps]               nvarchar(255)    NULL,
	[ApplicantName]            nvarchar(255)    NULL,
	[ContactName]              nvarchar(255)    NULL,
	[ContactPhone]             nvarchar(255)    NULL,
	[ContactEmail]             nvarchar(255)    NULL,
	[Notes]                    nvarchar(255)    NULL,
	[SecretHash]               nvarchar(255)    NULL,
	[CallerName]               nvarchar(255)    NULL,
	[CallerPasswordHash]       nvarchar(255)    NULL,
	[AllowedFunctionIds]       nvarchar(255)    NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_IntegrationClients] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IntegrationClientSourceIndex] ON [dbo].[IntegrationClients] ([SourceSystem], [SourceId]) WHERE [SourceSystem] <> '' AND [SourceId] <> '';
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[IntegrationClientServices]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[IntegrationClientServices]', N'U') IS NOT NULL
    DROP TABLE [dbo].[IntegrationClientServices];
GO

CREATE TABLE [dbo].[IntegrationClientServices]
(
	[ClientId]                 nvarchar(255)    NOT NULL,
	[ServiceId]                nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_IntegrationClientServices] PRIMARY KEY ([ClientId], [ServiceId])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[LoginEvents]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[LoginEvents]', N'U') IS NOT NULL
    DROP TABLE [dbo].[LoginEvents];
GO

CREATE TABLE [dbo].[LoginEvents]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               datetimeoffset   NOT NULL,
	[AccountId]                nvarchar(255)    NULL,
	[Outcome]                  nvarchar(255)    NULL
);
GO

-- 傾印資料表 [dbo].[LoginEvents] 資料：7 筆
SET IDENTITY_INSERT [dbo].[LoginEvents] ON;
GO

INSERT INTO [dbo].[LoginEvents] ([Id], [OccurredAt], [AccountId], [Outcome]) VALUES
	(1, '2026-10-21 01:37:47.1425958+00:00', NULL, N'failure'),
	(2, '2026-10-21 01:38:33.1394477+00:00', NULL, N'failure'),
	(3, '2026-10-21 02:02:15.2667927+00:00', NULL, N'failure'),
	(4, '2026-10-21 02:13:35.8030194+00:00', NULL, N'failure'),
	(5, '2026-10-21 02:16:32.5010734+00:00', N'af9675f4741849d4a2c0b4873ccd0c27', N'success'),
	(6, '2026-10-21 02:16:45.5938268+00:00', N'af9675f4741849d4a2c0b4873ccd0c27', N'success'),
	(7, '2026-10-21 02:19:32.9427827+00:00', N'af9675f4741849d4a2c0b4873ccd0c27', N'success');
GO

SET IDENTITY_INSERT [dbo].[LoginEvents] OFF;
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[OidcApplications]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OidcApplications]', N'U') IS NOT NULL
    DROP TABLE [dbo].[OidcApplications];
GO

CREATE TABLE [dbo].[OidcApplications]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[ConcurrencyToken]         nvarchar(255)    NULL,
	[ApplicationId]            nvarchar(255)    NULL,
	[AuthorizationId]          nvarchar(255)    NULL,
	[ApplicationType]          nvarchar(255)    NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[ClientSecret]             nvarchar(255)    NULL,
	[ClientType]               nvarchar(255)    NULL,
	[ConsentType]              nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[DisplayNames]             nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[Descriptions]             nvarchar(255)    NULL,
	[JsonWebKeySet]            nvarchar(255)    NULL,
	[Name]                     nvarchar(255)    NULL,
	[Payload]                  nvarchar(255)    NULL,
	[Permissions]              nvarchar(255)    NULL,
	[PostLogoutRedirectUris]   nvarchar(255)    NULL,
	[Properties]               nvarchar(255)    NULL,
	[RedirectUris]             nvarchar(255)    NULL,
	[ReferenceId]              nvarchar(255)    NULL,
	[Requirements]             nvarchar(255)    NULL,
	[Resources]                nvarchar(255)    NULL,
	[Scopes]                   nvarchar(255)    NULL,
	[Settings]                 nvarchar(255)    NULL,
	[Status]                   nvarchar(255)    NULL,
	[Subject]                  nvarchar(255)    NULL,
	[Type]                     nvarchar(255)    NULL,
	[CreationDate]             bigint           NULL,
	[ExpirationDate]           bigint           NULL,
	[RedemptionDate]           bigint           NULL,
	CONSTRAINT [PK_OidcApplications] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [OidcClientIndex] ON [dbo].[OidcApplications] ([ClientId]) WHERE [ClientId] IS NOT NULL;
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[OidcAuthorizations]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OidcAuthorizations]', N'U') IS NOT NULL
    DROP TABLE [dbo].[OidcAuthorizations];
GO

CREATE TABLE [dbo].[OidcAuthorizations]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[ConcurrencyToken]         nvarchar(255)    NULL,
	[ApplicationId]            nvarchar(255)    NULL,
	[AuthorizationId]          nvarchar(255)    NULL,
	[ApplicationType]          nvarchar(255)    NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[ClientSecret]             nvarchar(255)    NULL,
	[ClientType]               nvarchar(255)    NULL,
	[ConsentType]              nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[DisplayNames]             nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[Descriptions]             nvarchar(255)    NULL,
	[JsonWebKeySet]            nvarchar(255)    NULL,
	[Name]                     nvarchar(255)    NULL,
	[Payload]                  nvarchar(255)    NULL,
	[Permissions]              nvarchar(255)    NULL,
	[PostLogoutRedirectUris]   nvarchar(255)    NULL,
	[Properties]               nvarchar(255)    NULL,
	[RedirectUris]             nvarchar(255)    NULL,
	[ReferenceId]              nvarchar(255)    NULL,
	[Requirements]             nvarchar(255)    NULL,
	[Resources]                nvarchar(255)    NULL,
	[Scopes]                   nvarchar(255)    NULL,
	[Settings]                 nvarchar(255)    NULL,
	[Status]                   nvarchar(255)    NULL,
	[Subject]                  nvarchar(255)    NULL,
	[Type]                     nvarchar(255)    NULL,
	[CreationDate]             bigint           NULL,
	[ExpirationDate]           bigint           NULL,
	[RedemptionDate]           bigint           NULL,
	CONSTRAINT [PK_OidcAuthorizations] PRIMARY KEY ([Id])
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[OidcScopes]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OidcScopes]', N'U') IS NOT NULL
    DROP TABLE [dbo].[OidcScopes];
GO

CREATE TABLE [dbo].[OidcScopes]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[ConcurrencyToken]         nvarchar(255)    NULL,
	[ApplicationId]            nvarchar(255)    NULL,
	[AuthorizationId]          nvarchar(255)    NULL,
	[ApplicationType]          nvarchar(255)    NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[ClientSecret]             nvarchar(255)    NULL,
	[ClientType]               nvarchar(255)    NULL,
	[ConsentType]              nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[DisplayNames]             nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[Descriptions]             nvarchar(255)    NULL,
	[JsonWebKeySet]            nvarchar(255)    NULL,
	[Name]                     nvarchar(255)    NULL,
	[Payload]                  nvarchar(255)    NULL,
	[Permissions]              nvarchar(255)    NULL,
	[PostLogoutRedirectUris]   nvarchar(255)    NULL,
	[Properties]               nvarchar(255)    NULL,
	[RedirectUris]             nvarchar(255)    NULL,
	[ReferenceId]              nvarchar(255)    NULL,
	[Requirements]             nvarchar(255)    NULL,
	[Resources]                nvarchar(255)    NULL,
	[Scopes]                   nvarchar(255)    NULL,
	[Settings]                 nvarchar(255)    NULL,
	[Status]                   nvarchar(255)    NULL,
	[Subject]                  nvarchar(255)    NULL,
	[Type]                     nvarchar(255)    NULL,
	[CreationDate]             bigint           NULL,
	[ExpirationDate]           bigint           NULL,
	[RedemptionDate]           bigint           NULL,
	CONSTRAINT [PK_OidcScopes] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [OidcScopeNameIndex] ON [dbo].[OidcScopes] ([Name]) WHERE [Name] IS NOT NULL;
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[OidcTokens]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OidcTokens]', N'U') IS NOT NULL
    DROP TABLE [dbo].[OidcTokens];
GO

CREATE TABLE [dbo].[OidcTokens]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[ConcurrencyToken]         nvarchar(255)    NULL,
	[ApplicationId]            nvarchar(255)    NULL,
	[AuthorizationId]          nvarchar(255)    NULL,
	[ApplicationType]          nvarchar(255)    NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[ClientSecret]             nvarchar(255)    NULL,
	[ClientType]               nvarchar(255)    NULL,
	[ConsentType]              nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[DisplayNames]             nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[Descriptions]             nvarchar(255)    NULL,
	[JsonWebKeySet]            nvarchar(255)    NULL,
	[Name]                     nvarchar(255)    NULL,
	[Payload]                  nvarchar(255)    NULL,
	[Permissions]              nvarchar(255)    NULL,
	[PostLogoutRedirectUris]   nvarchar(255)    NULL,
	[Properties]               nvarchar(255)    NULL,
	[RedirectUris]             nvarchar(255)    NULL,
	[ReferenceId]              nvarchar(255)    NULL,
	[Requirements]             nvarchar(255)    NULL,
	[Resources]                nvarchar(255)    NULL,
	[Scopes]                   nvarchar(255)    NULL,
	[Settings]                 nvarchar(255)    NULL,
	[Status]                   nvarchar(255)    NULL,
	[Subject]                  nvarchar(255)    NULL,
	[Type]                     nvarchar(255)    NULL,
	[CreationDate]             bigint           NULL,
	[ExpirationDate]           bigint           NULL,
	[RedemptionDate]           bigint           NULL,
	CONSTRAINT [PK_OidcTokens] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [OidcReferenceIndex] ON [dbo].[OidcTokens] ([ReferenceId]) WHERE [ReferenceId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [OidcTokenAuthIndex] ON [dbo].[OidcTokens] ([AuthorizationId]);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[PortalAudiences]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[PortalAudiences]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PortalAudiences];
GO

CREATE TABLE [dbo].[PortalAudiences]
(
	[ClientId]                 nvarchar(255)    NOT NULL,
	[IdentityTypesJson]        nvarchar(255)    NULL,
	[StudentStatusesJson]      nvarchar(255)    NULL,
	[EducationLevelsJson]      nvarchar(255)    NULL,
	[RulesJson]                nvarchar(255)    NULL,
	CONSTRAINT [PK_PortalAudiences] PRIMARY KEY ([ClientId])
);
GO

-- 傾印資料表 [dbo].[PortalAudiences] 資料：35 筆
INSERT INTO [dbo].[PortalAudiences] ([ClientId], [IdentityTypesJson], [StudentStatusesJson], [EducationLevelsJson], [RulesJson]) VALUES
	(N'link-acadmis', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-account02', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-account03', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-activity', N'[]', N'[]', N'[]', N''),
	(N'link-addcrs', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-alumni-benefits', N'[]', N'[]', N'[]', N''),
	(N'link-alumni-card', N'[]', N'[]', N'[]', N''),
	(N'link-alumni-center', N'[]', N'[]', N'[]', N''),
	(N'link-alumni-jobs', N'[]', N'[]', N'[]', N''),
	(N'link-alumni-system', N'[]', N'[]', N'[]', N''),
	(N'link-care', N'["student"]', N'[]', N'[]', N''),
	(N'link-centapp', N'[]', N'[]', N'[]', N''),
	(N'link-docu', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-elearning', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-empservice', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-fixall', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-giving', N'[]', N'[]', N'[]', N''),
	(N'link-higheredu', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-leave', N'["student"]', N'[]', N'[]', N''),
	(N'link-newvenue', N'[]', N'[]', N'[]', N''),
	(N'link-officemail', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-ohr', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-oosaf', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-post', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-primo', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-projectiac', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-projectrad', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-ptsign', N'["student"]', N'[]', N'[]', N''),
	(N'link-selcrs', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-sop', N'["teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-stdsel', N'["student"]', N'[]', N'[]', N''),
	(N'link-suggest', N'[]', N'[]', N'[]', N''),
	(N'link-vms', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-waiver', N'["student","teacher","staff","employee"]', N'[]', N'[]', N''),
	(N'link-webap0', N'["student","teacher","staff","employee"]', N'[]', N'[]', N'');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[PortalCategories]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[PortalCategories]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PortalCategories];
GO

CREATE TABLE [dbo].[PortalCategories]
(
	[Key]                      nvarchar(255)    NOT NULL,
	[Name]                     nvarchar(255)    NULL,
	[NameEn]                   nvarchar(255)    NULL,
	[Accent]                   nvarchar(255)    NULL,
	[SortOrder]                int              NOT NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_PortalCategories] PRIMARY KEY ([Key])
);
GO

-- 傾印資料表 [dbo].[PortalCategories] 資料：9 筆
INSERT INTO [dbo].[PortalCategories] ([Key], [Name], [NameEn], [Accent], [SortOrder], [Revision]) VALUES
	(N'academic', N'教務類', N'', N'indigo', 10, N'3ab0f689342e41d490d2e58cdb5e4fe1'),
	(N'affairs', N'總務類', N'', N'slate', 50, N'7c52f0d6346f483fb40f339ea46adfa8'),
	(N'campus', N'學務類', N'', N'teal', 20, N'9b9fac86ff0e4ca39fc4bdca80bb0d4e'),
	(N'document', N'文書類', N'', N'cyan', 70, N'd5ff0ae61881446c8548548ce71569d1'),
	(N'finance', N'財務類', N'', N'rose', 40, N'46240c0d28704bcbbecc2dc5af97425d'),
	(N'general', N'綜合服務', N'', N'green', 90, N'f20f68288ee74a78860254fe4c4f2dca'),
	(N'hr', N'人事類', N'', N'amber', 30, N'8457c283c3044edfaf75580db22574fb'),
	(N'it', N'資訊類', N'', N'blue', 80, N'cd385fba4add48c1a940bbc9d865ff56'),
	(N'research', N'研究類', N'', N'violet', 60, N'3bbbc24267b8422894a75b213face97f');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[PortalCategoryAudits]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[PortalCategoryAudits]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PortalCategoryAudits];
GO

CREATE TABLE [dbo].[PortalCategoryAudits]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               bigint           NOT NULL,
	[CategoryKey]              nvarchar(255)    NULL,
	[ActorId]                  nvarchar(255)    NULL,
	[RemoteIp]                 nvarchar(255)    NULL,
	[Action]                   nvarchar(255)    NULL,
	[BeforeJson]               nvarchar(255)    NULL,
	[AfterJson]                nvarchar(255)    NULL
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[RegisteredSystems]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[RegisteredSystems]', N'U') IS NOT NULL
    DROP TABLE [dbo].[RegisteredSystems];
GO

CREATE TABLE [dbo].[RegisteredSystems]
(
	[ClientId]                 nvarchar(255)    NOT NULL,
	[Protocol]                 nvarchar(255)    NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[DisplayNameEn]            nvarchar(255)    NULL,
	[Description]              nvarchar(255)    NULL,
	[DescriptionEn]            nvarchar(255)    NULL,
	[EntryUrl]                 nvarchar(255)    NULL,
	[RedirectUri]              nvarchar(255)    NULL,
	[PostLogoutRedirectUri]    nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[SamlEntityId]             nvarchar(255)    NULL,
	[SamlSigningCertificate]   nvarchar(255)    NULL,
	[ShowOnPortal]             bit              NOT NULL,
	[PublicStudent]            bit              NOT NULL,
	[PublicEmployee]           bit              NOT NULL,
	[PublicAlumni]             bit              NOT NULL,
	[RequireLoginToView]       bit              NOT NULL,
	[RequirePortalLogin]       bit              NOT NULL,
	[SortOrder]                int              NOT NULL,
	[ClaimMask]                int              NOT NULL,
	[Category]                 nvarchar(255)    NULL,
	[Keywords]                 nvarchar(255)    NULL,
	[Revision]                 nvarchar(255)    NULL,
	CONSTRAINT [PK_RegisteredSystems] PRIMARY KEY ([ClientId])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [SamlEntityIndex] ON [dbo].[RegisteredSystems] ([SamlEntityId]) WHERE [Protocol] = 'saml';
GO

-- 傾印資料表 [dbo].[RegisteredSystems] 資料：35 筆
INSERT INTO [dbo].[RegisteredSystems] ([ClientId], [Protocol], [DisplayName], [DisplayNameEn], [Description], [DescriptionEn], [EntryUrl], [RedirectUri], [PostLogoutRedirectUri], [Enabled], [SamlEntityId], [SamlSigningCertificate], [ShowOnPortal], [PublicStudent], [PublicEmployee], [PublicAlumni], [RequireLoginToView], [RequirePortalLogin], [SortOrder], [ClaimMask], [Category], [Keywords], [Revision]) VALUES
	(N'link-acadmis', N'link', N'入學獎學金申請', N'', N'', N'', N'https://acadmis.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 109, 0, N'campus', N'', N'f2f966115ce741eab040b12e1adc008f'),
	(N'link-account02', N'link', N'主計請購系統', N'', N'', N'', N'https://account02.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 114, 0, N'finance', N'', N'33c8b4801f4d4acda7d006295c3d212c'),
	(N'link-account03', N'link', N'經費結報簽核', N'', N'', N'', N'https://account03.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 115, 0, N'finance', N'', N'42c2e8c324b44fcb8f4586f6b10f5c9d'),
	(N'link-activity', N'link', N'活動訊息平台', N'', N'', N'', N'https://ws1.nkust.edu.tw/Activity/', N'', N'', 0, N'', N'', 1, 1, 1, 1, 0, 0, 110, 0, N'campus', N'', N'8c6ec5d2b6764cdfb00a43c5b0e568bb'),
	(N'link-addcrs', N'link', N'選課表單申請暨簽核', N'', N'', N'', N'https://apply2.nkust.edu.tw/AddCrsApply', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 104, 0, N'academic', N'', N'0bd11a79b31c47af9a67c357bb0cb052'),
	(N'link-alumni-benefits', N'link', N'校友優惠資訊', N'', N'', N'', N'https://ascdc.nkust.edu.tw/p/412-1022-6565.php', N'', N'', 0, N'', N'', 1, 0, 0, 1, 0, 0, 134, 0, N'general', N'', N'a53f068ac0364104811ed6b179eb5e69'),
	(N'link-alumni-card', N'link', N'校友證申請', N'', N'', N'', N'https://ascdc.nkust.edu.tw/p/412-1022-1425.php?Lang=zh-tw', N'', N'', 0, N'', N'', 1, 0, 0, 1, 0, 0, 132, 0, N'general', N'', N'e5b57a1cc78543c2ad3d6a53472b2fee'),
	(N'link-alumni-center', N'link', N'校友服務中心', N'', N'', N'', N'https://ascdc.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 0, 1, 0, 0, 130, 0, N'general', N'', N'021dbf6c67aa43b3b3098d4b62b91ce2'),
	(N'link-alumni-jobs', N'link', N'就業職缺資訊', N'', N'', N'', N'https://ascdc.nkust.edu.tw/p/412-1022-8177.php', N'', N'', 0, N'', N'', 1, 0, 0, 1, 0, 0, 133, 0, N'general', N'', N'3c4b19c006714614bca211e5d885bde2'),
	(N'link-alumni-system', N'link', N'校友服務系統', N'', N'', N'', N'https://as.nkust.edu.tw/Login', N'', N'', 0, N'', N'', 1, 0, 0, 1, 0, 0, 131, 0, N'general', N'', N'0c96b2353f1e4b0d8c1560c0fd2f0955'),
	(N'link-care', N'link', N'預約諮商輔導', N'', N'', N'', N'https://care.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 0, 0, 0, 0, 108, 0, N'campus', N'', N'9fee97ea0c3346379a240560070297ab'),
	(N'link-centapp', N'link', N'學籍成績證明申請', N'', N'', N'', N'https://centapp.nkust.edu.tw/Home', N'', N'', 0, N'', N'', 1, 1, 0, 1, 0, 0, 105, 0, N'academic', N'', N'13790b121aa24329ac93408a0b3de32f'),
	(N'link-docu', N'link', N'文件流程查詢', N'', N'', N'', N'https://docu.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 124, 0, N'document', N'', N'22eff87482df4cadbc2f07df426430a4'),
	(N'link-elearning', N'link', N'教學平台', N'', N'', N'', N'https://elearning.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 102, 0, N'academic', N'', N'54dff3fe268043be8b84dd043b434d63'),
	(N'link-empservice', N'link', N'在職服務證明申請', N'', N'', N'', N'https://empservice.nkust.edu.tw/EmployService', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 112, 0, N'hr', N'', N'a502d4a2022348b0b03024f4b6005c37'),
	(N'link-fixall', N'link', N'各式維修申請', N'', N'', N'', N'https://ws2.nkust.edu.tw/fixall', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 119, 0, N'affairs', N'', N'e8a5a6d4ff5d4873929d55e0fbe61cff'),
	(N'link-giving', N'link', N'捐贈興學', N'', N'', N'', N'https://giving.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 1, 0, 0, 129, 0, N'general', N'', N'52ce4e85d5294a359931921af949fe5d'),
	(N'link-higheredu', N'link', N'教學補助申請', N'', N'', N'', N'https://higheredu.nkust.edu.tw/service/index?type=teacher', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 123, 0, N'research', N'', N'e1b1504954c241e7b873d2698e986632'),
	(N'link-leave', N'link', N'線上請假', N'', N'', N'', N'https://oosaf.nkust.edu.tw/Student/Leave/Create', N'', N'', 0, N'', N'', 1, 1, 0, 0, 0, 0, 107, 0, N'campus', N'', N'023c245bafc6466aaebd662df316a224'),
	(N'link-newvenue', N'link', N'場地租借暨門禁', N'', N'', N'', N'https://newvenue.nkust.edu.tw/rental-system/', N'', N'', 0, N'', N'', 1, 1, 1, 1, 0, 0, 118, 0, N'affairs', N'', N'75fa2af9511a437081be407ed9eed0aa'),
	(N'link-officemail', N'link', N'公務電子郵件平台', N'', N'', N'', N'https://officemail.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 126, 0, N'it', N'', N'2cb5c011458849c38f00b3db5ba6196b'),
	(N'link-ohr', N'link', N'進用暨保費管理', N'', N'', N'', N'https://ohr.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 113, 0, N'hr', N'', N'2ae92e6f73ed4e6eae0d36e9063d7e63'),
	(N'link-oosaf', N'link', N'學務資訊系統', N'', N'', N'', N'https://oosaf.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 106, 0, N'campus', N'', N'46c88651bb0745879fa6c3a06ab3b87e'),
	(N'link-post', N'link', N'郵件查詢', N'', N'', N'', N'https://post.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 120, 0, N'affairs', N'', N'b2ea1215d8994ff49907a02cf5230e18'),
	(N'link-primo', N'link', N'圖書資源查詢', N'', N'', N'', N'https://nkust.primo.exlibrisgroup.com/discovery/search', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 128, 0, N'general', N'', N'e0da8eb6350d483fadfefcf7e1e77fb4'),
	(N'link-projectiac', N'link', N'非政府產學計畫', N'', N'', N'', N'https://ws3.nkust.edu.tw/ProjectIAC', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 122, 0, N'research', N'', N'a4b8f2fd407d453ea7b2e9e2026c4646'),
	(N'link-projectrad', N'link', N'政府產學計畫', N'', N'', N'', N'https://ws3.nkust.edu.tw/ProjectRAD', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 121, 0, N'research', N'', N'c3dd629cd24a4c8280de4a8d0ad431ec'),
	(N'link-ptsign', N'link', N'兼任助理簽到', N'', N'', N'', N'https://ntapp.nkust.edu.tw/ptSign/', N'', N'', 0, N'', N'', 1, 1, 0, 0, 0, 0, 111, 0, N'campus', N'', N'90223ff2b11e4efcb292a7a6bdfd3362'),
	(N'link-selcrs', N'link', N'選課管理', N'', N'', N'', N'https://selcrs.nkust.edu.tw/selcrs/Account/Login', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 101, 0, N'academic', N'', N'da7a8678c81e498c87661846d126b4d3'),
	(N'link-sop', N'link', N'業務流程 SOP', N'', N'', N'', N'https://sop.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 0, 1, 0, 0, 0, 125, 0, N'document', N'', N'c12fc9f631d442249cfcd1fc10bb3b6b'),
	(N'link-stdsel', N'link', N'選課系統', N'', N'', N'', N'https://stdsel.nkust.edu.tw/dispatch', N'', N'', 0, N'', N'', 1, 1, 0, 0, 0, 0, 100, 0, N'academic', N'', N'a9a7d8765fb54e5f8cd0aedd0550ecd1'),
	(N'link-suggest', N'link', N'校務建言', N'', N'', N'', N'https://suggest.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 1, 0, 0, 127, 0, N'general', N'', N'e6e23b9682fe416ebce3297160e69ec4'),
	(N'link-vms', N'link', N'校車預約', N'', N'', N'', N'https://vms.nkust.edu.tw/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 116, 0, N'affairs', N'', N'3010f03e2641419b8867579fe880e281'),
	(N'link-waiver', N'link', N'學分抵免申請', N'', N'', N'', N'https://ws3.nkust.edu.tw/Waiver', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 103, 0, N'academic', N'', N'e95c1a2153a24cebae5ce40657136dde'),
	(N'link-webap0', N'link', N'停車證申請', N'', N'', N'', N'https://webap0.nkust.edu.tw/nkust/', N'', N'', 0, N'', N'', 1, 1, 1, 0, 0, 0, 117, 0, N'affairs', N'', N'58f43494b73c442dbf9f42591acc5be5');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[SchemaMigrations]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[SchemaMigrations]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SchemaMigrations];
GO

CREATE TABLE [dbo].[SchemaMigrations]
(
	[Id]                       nvarchar(255)    NOT NULL,
	CONSTRAINT [PK_SchemaMigrations] PRIMARY KEY ([Id])
);
GO

-- 傾印資料表 [dbo].[SchemaMigrations] 資料：9 筆
INSERT INTO [dbo].[SchemaMigrations] ([Id]) VALUES
	(N'admin-management-v1'),
	(N'admin-manuals-v1'),
	(N'campus-employee-identity-v1'),
	(N'identity-classification-v1'),
	(N'identity-student-kind-v1'),
	(N'integration-return-fields-v1'),
	(N'integration-service-grants-v1'),
	(N'portal-categories-v1'),
	(N'public-campus-directory-v1');
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[SystemConfigurationAudits]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[SystemConfigurationAudits]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SystemConfigurationAudits];
GO

CREATE TABLE [dbo].[SystemConfigurationAudits]
(
	[Id]                       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[OccurredAt]               bigint           NOT NULL,
	[ActorId]                  nvarchar(255)    NULL,
	[ClientId]                 nvarchar(255)    NULL,
	[Action]                   nvarchar(255)    NULL,
	[RemoteIp]                 nvarchar(255)    NULL,
	[BeforeJson]               nvarchar(255)    NULL,
	[AfterJson]                nvarchar(255)    NULL
);
GO

-- --------------------------------------------------------
-- 資料表結構: [dbo].[TicketClients]
-- --------------------------------------------------------
IF OBJECT_ID(N'[dbo].[TicketClients]', N'U') IS NOT NULL
    DROP TABLE [dbo].[TicketClients];
GO

CREATE TABLE [dbo].[TicketClients]
(
	[Id]                       nvarchar(255)    NOT NULL,
	[DisplayName]              nvarchar(255)    NULL,
	[SecretHash]               nvarchar(255)    NULL,
	[RedirectUri]              nvarchar(255)    NULL,
	[Enabled]                  bit              NOT NULL,
	[IncludeStudentProfile]    bit              NOT NULL,
	CONSTRAINT [PK_TicketClients] PRIMARY KEY ([Id])
);
GO

/****** Object:  Login [NkustSSOUser]    Script Date: 11/15/2021 09:56:32 ******/
CREATE LOGIN [NkustSSOUser] WITH PASSWORD=N'7wn4tp93', DEFAULT_DATABASE=[NkustSSO], DEFAULT_LANGUAGE=[繁體中文], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF
GO
ALTER LOGIN [NkustSSOUser] ENABLE
GO
CREATE USER [NkustSSOUser] FOR LOGIN [NkustSSOUser] WITH DEFAULT_SCHEMA=[dbo]
GO
EXEC sp_addrolemember N'db_owner', N'NkustSSOUser'
GO
