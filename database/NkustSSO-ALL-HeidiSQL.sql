-- --------------------------------------------------------
-- 主機:                           D:\projects\01-main\nkustsso-story0720\.local\sso.db
-- 伺服器版本:                        3.53.4
-- 伺服器作業系統:                      
-- HeidiSQL 版本:                  12.21.0.7344
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES  */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- 傾印 sso 的資料庫結構
DROP DATABASE IF EXISTS "sso";
CREATE DATABASE IF NOT EXISTS "sso";
;

-- 傾印  資料表 sso.AccountIdentities 結構
DROP TABLE IF EXISTS "AccountIdentities";
CREATE TABLE IF NOT EXISTS [AccountIdentities]
(
	[AccountId]      NVarChar(255)     NULL,
	[Id]             NVarChar(255)     NULL,
	[IdentityType]   NVarChar(255)     NULL,
	[AttributesJson] NVarChar(255)     NULL,
	[Enabled]        Bit           NOT NULL,
	[ValidFrom]      INTEGER           NULL,
	[ValidUntil]     INTEGER           NULL,

	CONSTRAINT [PK_AccountIdentities] PRIMARY KEY ([AccountId], [Id])
);
;

-- 正在傾印表格  sso.AccountIdentities 的資料：0 rows

-- 傾印  資料表 sso.AccountIdentitySets 結構
DROP TABLE IF EXISTS "AccountIdentitySets";
CREATE TABLE IF NOT EXISTS [AccountIdentitySets]
(
	[AccountId] NVarChar(255)     NULL,
	[Revision]  NVarChar(255)     NULL,
	[UpdatedAt] INTEGER       NOT NULL,
	[Source]    NVarChar(255)     NULL,

	CONSTRAINT [PK_AccountIdentitySets] PRIMARY KEY ([AccountId])
);
;

-- 正在傾印表格  sso.AccountIdentitySets 的資料：0 rows

-- 傾印  資料表 sso.AdminManagementAudits 結構
DROP TABLE IF EXISTS "AdminManagementAudits";
CREATE TABLE IF NOT EXISTS [AdminManagementAudits]
(
	[Id]         INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt] INTEGER        NOT NULL,
	[ActorId]    NVarChar(255)      NULL,
	[RemoteIp]   NVarChar(255)      NULL,
	[Kind]       NVarChar(255)      NULL,
	[TargetId]   NVarChar(255)      NULL,
	[Action]     NVarChar(255)      NULL,
	[BeforeJson] NVarChar(255)      NULL,
	[AfterJson]  NVarChar(255)      NULL
);

-- 正在傾印表格  sso.AdminManagementAudits 的資料：0 rows

-- 傾印  資料表 sso.AdminMenus 結構
DROP TABLE IF EXISTS "AdminMenus";
CREATE TABLE IF NOT EXISTS [AdminMenus]
(
	[Id]        NVarChar(255)     NULL,
	[Label]     NVarChar(255)     NULL,
	[Group]     NVarChar(255)     NULL,
	[Target]    NVarChar(255)     NULL,
	[Icon]      NVarChar(255)     NULL,
	[SortOrder] INTEGER       NOT NULL,
	[Enabled]   Bit           NOT NULL,
	[Revision]  NVarChar(255)     NULL,

	CONSTRAINT [PK_AdminMenus] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.AdminMenus 的資料：9 rows
REPLACE INTO "AdminMenus" ("Id", "Label", "Group", "Target", "Icon", "SortOrder", "Enabled", "Revision") VALUES
	('access', '權限管理', '系統管理', 'access', 'people', 80, '1', '673e95ab44d540b894a17ab898e7a2a8'),
	('categories', '首頁分類', '服務設定', 'categories', 'folder', 20, '1', '5a9ed63fcca34640868a4491bd49d1fa'),
	('identities', '身分分類', '服務設定', 'identities', 'people', 30, '1', 'aeb7c942d6fd4030ba926a18a4da3830'),
	('manuals', '串接手冊', '服務設定', 'manuals', 'folder', 35, '1', '6e14fd60bf0a4f0f91941c958fd57ed0'),
	('menus', '選單維護', '系統管理', 'menus', 'folder', 70, '1', 'e488a5edf9984c3397f860fb563357fc'),
	('progress', '開發進度', '紀錄與維護', 'progress', 'activity', 60, '1', 'e3a276bcb36946b9ac6bd1d88aa7a0cb'),
	('sessions', '登入管理', '紀錄與維護', 'sessions', 'shield', 40, '1', '365a2000d7c543078c19f6d078dcfa84'),
	('systems', '串接系統管理', '服務設定', 'systems', 'grid', 10, '1', 'eb7c642ac99c403da390524c4c9d406e'),
	('tickets', '票券紀錄', '紀錄與維護', 'tickets', 'ticket', 50, '1', 'd92366501f0f42b792437633f51c7edf');

-- 傾印  資料表 sso.AdminRoleMembers 結構
DROP TABLE IF EXISTS "AdminRoleMembers";
CREATE TABLE IF NOT EXISTS [AdminRoleMembers]
(
	[RoleId]    NVarChar(255)     NULL,
	[AccountId] NVarChar(255)     NULL,

	CONSTRAINT [PK_AdminRoleMembers] PRIMARY KEY ([RoleId], [AccountId])
);
;

-- 正在傾印表格  sso.AdminRoleMembers 的資料：0 rows

-- 傾印  資料表 sso.AdminRolePermissions 結構
DROP TABLE IF EXISTS "AdminRolePermissions";
CREATE TABLE IF NOT EXISTS [AdminRolePermissions]
(
	[RoleId]     NVarChar(255)     NULL,
	[Permission] NVarChar(255)     NULL,

	CONSTRAINT [PK_AdminRolePermissions] PRIMARY KEY ([RoleId], [Permission])
);
;

-- 正在傾印表格  sso.AdminRolePermissions 的資料：8 rows
REPLACE INTO "AdminRolePermissions" ("RoleId", "Permission") VALUES
	('admin', 'categories'),
	('admin', 'identities'),
	('admin', 'integrations'),
	('admin', 'manuals'),
	('admin', 'progress'),
	('admin', 'sessions'),
	('admin', 'systems'),
	('admin', 'tickets');

-- 傾印  資料表 sso.AdminRoles 結構
DROP TABLE IF EXISTS "AdminRoles";
CREATE TABLE IF NOT EXISTS [AdminRoles]
(
	[Id]          NVarChar(255)     NULL,
	[Name]        NVarChar(255)     NULL,
	[Description] NVarChar(255)     NULL,
	[Enabled]     Bit           NOT NULL,
	[StartsAt]    INTEGER           NULL,
	[ExpiresAt]   INTEGER           NULL,
	[Revision]    NVarChar(255)     NULL,

	CONSTRAINT [PK_AdminRoles] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.AdminRoles 的資料：1 rows
REPLACE INTO "AdminRoles" ("Id", "Name", "Description", "Enabled", "StartsAt", "ExpiresAt", "Revision") VALUES
	('admin', 'admin', '預設後台管理群組，可維護一般後台功能；選單與權限管理仍限最高管理者。成員須另行指派。', '1', NULL, NULL, '5765872cd96c40ba9d07d0c07fcc03cc');

-- 傾印  資料表 sso.AspNetRoles 結構
DROP TABLE IF EXISTS "AspNetRoles";
CREATE TABLE IF NOT EXISTS [AspNetRoles]
(
	[Id]               NVarChar(255)     NULL,
	[Name]             NVarChar(255)     NULL,
	[NormalizedName]   NVarChar(255)     NULL,
	[ConcurrencyStamp] NVarChar(255)     NULL,

	CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX RoleNameIndex ON AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;

-- 正在傾印表格  sso.AspNetRoles 的資料：1 rows
REPLACE INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES
	('8994cae4-ea1c-4c87-a558-4a7a1d792d5d', 'SsoAdministrator', 'SSOADMINISTRATOR', '474a16c3-7f75-4a5c-908c-575ca61cefd6');

-- 傾印  資料表 sso.AspNetUserRoles 結構
DROP TABLE IF EXISTS "AspNetUserRoles";
CREATE TABLE IF NOT EXISTS [AspNetUserRoles]
(
	[UserId] NVarChar(255)     NULL,
	[RoleId] NVarChar(255)     NULL,

	CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId])
);
;

-- 正在傾印表格  sso.AspNetUserRoles 的資料：0 rows

-- 傾印  資料表 sso.AspNetUsers 結構
DROP TABLE IF EXISTS "AspNetUsers";
CREATE TABLE IF NOT EXISTS [AspNetUsers]
(
	[Id]                   NVarChar(255)      NULL,
	[UserName]             NVarChar(255)      NULL,
	[NormalizedUserName]   NVarChar(255)      NULL,
	[Email]                NVarChar(255)      NULL,
	[NormalizedEmail]      NVarChar(255)      NULL,
	[EmailConfirmed]       Bit            NOT NULL,
	[PasswordHash]         NVarChar(255)      NULL,
	[SecurityStamp]        NVarChar(255)      NULL,
	[ConcurrencyStamp]     NVarChar(255)      NULL,
	[PhoneNumber]          NVarChar(255)      NULL,
	[PhoneNumberConfirmed] Bit            NOT NULL,
	[TwoFactorEnabled]     Bit            NOT NULL,
	[LockoutEnd]           DateTimeOffset     NULL,
	[LockoutEnabled]       Bit            NOT NULL,
	[AccessFailedCount]    INTEGER        NOT NULL,
	[AuthenticationSource] NVarChar(255)      NULL,
	[SourceAccountId]      NVarChar(255)      NULL,
	[DisplayName]          NVarChar(255)      NULL,
	[IdentityType]         NVarChar(255)      NULL,
	[SourceStatusCode]     NVarChar(255)      NULL,
	[SourceStatusName]     NVarChar(255)      NULL,
	[UnitCode]             NVarChar(255)      NULL,
	[UnitName]             NVarChar(255)      NULL,
	[ClassCode]            NVarChar(255)      NULL,
	[ClassName]            NVarChar(255)      NULL,
	[StudentStatusCode]    NVarChar(255)      NULL,
	[StudentStatusName]    NVarChar(255)      NULL,
	[EducationLevelCode]   NVarChar(255)      NULL,
	[EducationLevelName]   NVarChar(255)      NULL,
	[Enabled]              Bit            NOT NULL,

	CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX AccountSourceIndex ON AspNetUsers (AuthenticationSource, SourceAccountId) WHERE AuthenticationSource <> '';
CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

-- 正在傾印表格  sso.AspNetUsers 的資料：1 rows
REPLACE INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "AuthenticationSource", "SourceAccountId", "DisplayName", "IdentityType", "SourceStatusCode", "SourceStatusName", "UnitCode", "UnitName", "ClassCode", "ClassName", "StudentStatusCode", "StudentStatusName", "EducationLevelCode", "EducationLevelName", "Enabled") VALUES
	('af9675f4741849d4a2c0b4873ccd0c27', '42893', '42893', NULL, NULL, '0', 'AQAAAAIAAYagAAAAEM0FPtbvMLAuz1lLDafUpVZ9gYLd5pdP7q81eJXl+ixo/PH0Rw3yOdQGGcp8cDlMLw==', '66cc0bcb-4ddb-4619-b275-0b60740e0768', 'eaa77aa1-3b1a-4344-afef-ee4dd9144327', NULL, '0', '0', NULL, '1', 0, '', NULL, 'Kyle', 'teacher', NULL, NULL, 'CS', '資訊工程系', NULL, NULL, NULL, NULL, NULL, NULL, '1');

-- 傾印  資料表 sso.IdentityCatalogAudits 結構
DROP TABLE IF EXISTS "IdentityCatalogAudits";
CREATE TABLE IF NOT EXISTS [IdentityCatalogAudits]
(
	[Id]         INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt] INTEGER        NOT NULL,
	[ActorId]    NVarChar(255)      NULL,
	[RemoteIp]   NVarChar(255)      NULL,
	[Target]     NVarChar(255)      NULL,
	[BeforeJson] NVarChar(255)      NULL,
	[AfterJson]  NVarChar(255)      NULL
);

-- 正在傾印表格  sso.IdentityCatalogAudits 的資料：0 rows

-- 傾印  資料表 sso.IdentityDimensions 結構
DROP TABLE IF EXISTS "IdentityDimensions";
CREATE TABLE IF NOT EXISTS [IdentityDimensions]
(
	[Key]       NVarChar(255)     NULL,
	[Name]      NVarChar(255)     NULL,
	[Family]    NVarChar(255)     NULL,
	[SortOrder] INTEGER       NOT NULL,
	[Enabled]   Bit           NOT NULL,
	[Revision]  NVarChar(255)     NULL,

	CONSTRAINT [PK_IdentityDimensions] PRIMARY KEY ([Key])
);
;

-- 正在傾印表格  sso.IdentityDimensions 的資料：10 rows
REPLACE INTO "IdentityDimensions" ("Key", "Name", "Family", "SortOrder", "Enabled", "Revision") VALUES
	('appointment_type', '聘任／任用類別', 'employee', 50, '1', '408d3fe3e21c4ba7929b2e866072e80f'),
	('campus', '校區', 'any', 60, '1', 'f4385f07ee7f41469d035a8ec2a1a504'),
	('department', '系所／單位', 'any', 70, '1', 'd25b16a130a24529a5bed326cabd1f6e'),
	('division', '部別', 'student', 20, '1', 'd6922f846296499287ea5b160c2aaf2a'),
	('education_level', '學制', 'student', 30, '1', 'f5117c2a0939454db84dfaf84e92f55e'),
	('employment_status', '任職狀態', 'employee', 40, '1', '5a027cc73bfc4e90be2c5aca03552c9c'),
	('grade', '年級', 'student', 80, '1', '0f95eb6d852e4d749b1f171dc3158152'),
	('identity_type', '身分類別', 'any', 0, '1', '3fedc1788cb142508de2b3a9281aa28e'),
	('student_kind', '學生狀況', 'student', 15, '1', 'd563472c116041d481f7060fddaf717d'),
	('student_status', '學籍狀態', 'student', 10, '1', 'd7112d8d7d964e169baae858d67a4c5b');

-- 傾印  資料表 sso.IdentityOptions 結構
DROP TABLE IF EXISTS "IdentityOptions";
CREATE TABLE IF NOT EXISTS [IdentityOptions]
(
	[Id]        NVarChar(255)     NULL,
	[Dimension] NVarChar(255)     NULL,
	[Code]      NVarChar(255)     NULL,
	[Label]     NVarChar(255)     NULL,
	[Family]    NVarChar(255)     NULL,
	[Enabled]   Bit           NOT NULL,
	[SortOrder] INTEGER       NOT NULL,
	[Revision]  NVarChar(255)     NULL,

	CONSTRAINT [PK_IdentityOptions] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX IdentityOptionCodeIndex ON IdentityOptions (Dimension, Code);

-- 正在傾印表格  sso.IdentityOptions 的資料：20 rows
REPLACE INTO "IdentityOptions" ("Id", "Dimension", "Code", "Label", "Family", "Enabled", "SortOrder", "Revision") VALUES
	('11a4e269ddb247d39b4c7594197d7a5a', 'student_status', 'DEMO_ACTIVE', '在校', 'other', '1', 30, 'e0677a6eb1f74467b8945d0f5bab1143'),
	('18cf165836014567ab8517927301a9a9', 'appointment_type', 'DEMO_CONTRACT', '約聘僱', 'other', '1', 180, 'd3b28cdc6bdf466691a01b33c39e51b4'),
	('19e9c9daf9fc458991018df49c466ccf', 'identity_type', 'teacher', '教師', 'employee', '1', 10, 'edf7cb0b59df4b909a4b1ca12d3f171c'),
	('265e42411a5e432b84bf8e41b793e382', 'division', 'DEMO_DAY', '日間部', 'other', '1', 70, '2e94f4280a7944f78731c6dcd36e3650'),
	('34b8eeda55c248c98cba657a6aa73196', 'identity_type', 'staff', '一般職員', 'employee', '1', 20, 'c800b9b6dc5c4bf0b1fd5e7995bbb20e'),
	('3cdf4ed80e424f90bb15df5f48db6904', 'student_status', 'DEMO_WITHDRAWN', '退學', 'other', '1', 50, 'fbd0f60118d647039af2efa32115f64e'),
	('50702f728fca459f98063d928d1dce7f', 'identity_type', 'student', '學生', 'student', '1', 0, '45ea9138e43c45058424bc5bebffb611'),
	('5ff8f76f34dc402395feea934fca6a8c', 'appointment_type', 'DEMO_PART_TIME', '兼任', 'other', '1', 160, 'def8c1e963a84534b7c1390a68088a3a'),
	('63c4f5c84eed47019a7c33e30f0c921e', 'education_level', 'DEMO_UG', '大學部', 'other', '1', 90, '6ada56e819c54493915da9931ac81b6c'),
	('6cb1bb78353f4330bb319fa63a0faa9d', 'education_level', 'DEMO_PHD', '博士班', 'other', '1', 110, '6f8c544379e74d1ea1cccf5f9b691edd'),
	('730b8c10962a4cdea2a5ccc59bffcf0b', 'division', 'DEMO_NIGHT', '夜間／進修部', 'other', '1', 80, 'a269f784acc0465c960fd2c2424b7b99'),
	('9be58fddd08b4ecaa9588b467aece19e', 'student_status', 'DEMO_GRADUATED', '畢業', 'other', '1', 60, 'e6ba4269e8684e458720a02f5f205029'),
	('a7bf44c8c97b41c08479a0dbc3780a07', 'employment_status', 'DEMO_RETIRED', '退休', 'other', '1', 140, '03457c325aa84def8d221bdc42140f08'),
	('c2c1e199bb2044d3861e1f353f02ddd5', 'employment_status', 'DEMO_CURRENT', '現職', 'other', '1', 120, 'ae859b90f4b2407e977c345307cd3025'),
	('c8b9f7eb36154e43bf4031556ef03240', 'employment_status', 'DEMO_LEFT', '離職', 'other', '1', 130, '9355d7d9fe23445c9520ef1d5c9db588'),
	('cee8f957ecea4aa3948af19da436c135', 'identity_type', 'employee', '教職員（未細分）', 'employee', '1', 25, 'b9ed465a13e54373beb800c520c90215'),
	('dc2e0672325b46b997548bd6991526c0', 'education_level', 'DEMO_MASTER', '碩士班', 'other', '1', 100, '8ab0970b234b4628afdcf15efabbf9b3'),
	('e7ff62a767844bb79bd9c7a3d1dbe147', 'appointment_type', 'DEMO_FULL_TIME', '專任', 'other', '1', 150, 'debd11bcd6ad4b099c26c70427e61a19'),
	('f996448c5e4940d495833378c30fb375', 'student_status', 'DEMO_LEAVE', '休學', 'other', '1', 40, 'f541cd463b074ea38fb9e527e9405435'),
	('fa3ddd1977d9497faeda7069907cb986', 'appointment_type', 'DEMO_CIVIL', '公務人員', 'other', '1', 170, '0283c74bdb5243628888af4e835dab11');

-- 傾印  資料表 sso.IntegrationAudits 結構
DROP TABLE IF EXISTS "IntegrationAudits";
CREATE TABLE IF NOT EXISTS [IntegrationAudits]
(
	[Id]         INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt] INTEGER        NOT NULL,
	[ClientId]   NVarChar(255)      NULL,
	[ActorId]    NVarChar(255)      NULL,
	[RemoteIp]   NVarChar(255)      NULL,
	[Action]     NVarChar(255)      NULL,
	[Detail]     NVarChar(255)      NULL
);
CREATE INDEX IntegrationAuditTimeIndex ON IntegrationAudits (OccurredAt);

-- 正在傾印表格  sso.IntegrationAudits 的資料：0 rows

-- 傾印  資料表 sso.IntegrationCapabilities 結構
DROP TABLE IF EXISTS "IntegrationCapabilities";
CREATE TABLE IF NOT EXISTS [IntegrationCapabilities]
(
	[Id]   NVarChar(255)     NULL,
	[Name] NVarChar(255)     NULL,

	CONSTRAINT [PK_IntegrationCapabilities] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.IntegrationCapabilities 的資料：9 rows
REPLACE INTO "IntegrationCapabilities" ("Id", "Name") VALUES
	('account.verify', '帳密驗證'),
	('soap.Get_Traffic_Pub', '交通車資料查詢（公用帳號）（Get_Traffic_Pub）'),
	('soap.Get_Traffic_Stud', '交通車資料查詢（學生）（Get_Traffic_Stud）'),
	('soap.Get_Traffic_Stud_Temp', '交通車學生介面（Temp）（Get_Traffic_Stud_Temp）'),
	('soap.Get_Traffic_Teach', '交通車資料查詢（教職員）（Get_Traffic_Teach）'),
	('soap.Get_Traffic_Teach_Temp', '交通車教職員介面（Temp）（Get_Traffic_Teach_Temp）'),
	('soap.KuasSSO_Certify1', '校務功能票據驗證（KuasSSO_Certify1）'),
	('soap.KuasSSO_SchCardSearch', '校園智慧卡登入（資料庫版本）（KuasSSO_SchCardSearch）'),
	('soap.NkustSSO_DocumentApplyPaymentSystemSearch', '一卡通文件申請／繳費資料（NkustSSO_DocumentApplyPaymentSystemSearch）');

-- 傾印  資料表 sso.IntegrationClientReturnFields 結構
DROP TABLE IF EXISTS "IntegrationClientReturnFields";
CREATE TABLE IF NOT EXISTS [IntegrationClientReturnFields]
(
	[ClientId] NVarChar(255)     NULL,
	[FieldKey] NVarChar(255)     NULL,

	CONSTRAINT [PK_IntegrationClientReturnFields] PRIMARY KEY ([ClientId], [FieldKey])
);
;

-- 正在傾印表格  sso.IntegrationClientReturnFields 的資料：0 rows

-- 傾印  資料表 sso.IntegrationClients 結構
DROP TABLE IF EXISTS "IntegrationClients";
CREATE TABLE IF NOT EXISTS [IntegrationClients]
(
	[Id]                 NVarChar(255)     NULL,
	[Name]               NVarChar(255)     NULL,
	[ConnectionKind]     NVarChar(255)     NULL,
	[Purpose]            NVarChar(255)     NULL,
	[OwningUnit]         NVarChar(255)     NULL,
	[SourceSystem]       NVarChar(255)     NULL,
	[SourceId]           NVarChar(255)     NULL,
	[ImportBatchId]      NVarChar(255)     NULL,
	[ImportedAt]         INTEGER           NULL,
	[Protocol]           NVarChar(255)     NULL,
	[Enabled]            Bit           NOT NULL,
	[AllowedIps]         NVarChar(255)     NULL,
	[ApplicantName]      NVarChar(255)     NULL,
	[ContactName]        NVarChar(255)     NULL,
	[ContactPhone]       NVarChar(255)     NULL,
	[ContactEmail]       NVarChar(255)     NULL,
	[Notes]              NVarChar(255)     NULL,
	[SecretHash]         NVarChar(255)     NULL,
	[CallerName]         NVarChar(255)     NULL,
	[CallerPasswordHash] NVarChar(255)     NULL,
	[AllowedFunctionIds] NVarChar(255)     NULL,
	[Revision]           NVarChar(255)     NULL,

	CONSTRAINT [PK_IntegrationClients] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX IntegrationClientSourceIndex ON IntegrationClients (SourceSystem, SourceId) WHERE SourceSystem <> '' AND SourceId <> '';

-- 正在傾印表格  sso.IntegrationClients 的資料：0 rows

-- 傾印  資料表 sso.IntegrationClientServices 結構
DROP TABLE IF EXISTS "IntegrationClientServices";
CREATE TABLE IF NOT EXISTS [IntegrationClientServices]
(
	[ClientId]  NVarChar(255)     NULL,
	[ServiceId] NVarChar(255)     NULL,

	CONSTRAINT [PK_IntegrationClientServices] PRIMARY KEY ([ClientId], [ServiceId])
);
;

-- 正在傾印表格  sso.IntegrationClientServices 的資料：0 rows

-- 傾印  資料表 sso.LoginEvents 結構
DROP TABLE IF EXISTS "LoginEvents";
CREATE TABLE IF NOT EXISTS [LoginEvents]
(
	[Id]         INTEGER         NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt] DateTimeOffset  NOT NULL,
	[AccountId]  NVarChar(255)       NULL,
	[Outcome]    NVarChar(255)       NULL
);

-- 正在傾印表格  sso.LoginEvents 的資料：7 rows
REPLACE INTO "LoginEvents" ("Id", "OccurredAt", "AccountId", "Outcome") VALUES
	(1, '2026-10-21 01:37:47.1425958+00:00', NULL, 'failure'),
	(2, '2026-10-21 01:38:33.1394477+00:00', NULL, 'failure'),
	(3, '2026-10-21 02:02:15.2667927+00:00', NULL, 'failure'),
	(4, '2026-10-21 02:13:35.8030194+00:00', NULL, 'failure'),
	(5, '2026-10-21 02:16:32.5010734+00:00', 'af9675f4741849d4a2c0b4873ccd0c27', 'success'),
	(6, '2026-10-21 02:16:45.5938268+00:00', 'af9675f4741849d4a2c0b4873ccd0c27', 'success'),
	(7, '2026-10-21 02:19:32.9427827+00:00', 'af9675f4741849d4a2c0b4873ccd0c27', 'success');

-- 傾印  資料表 sso.OidcApplications 結構
DROP TABLE IF EXISTS "OidcApplications";
CREATE TABLE IF NOT EXISTS [OidcApplications]
(
	[Id]                     NVarChar(255)     NULL,
	[ConcurrencyToken]       NVarChar(255)     NULL,
	[ApplicationId]          NVarChar(255)     NULL,
	[AuthorizationId]        NVarChar(255)     NULL,
	[ApplicationType]        NVarChar(255)     NULL,
	[ClientId]               NVarChar(255)     NULL,
	[ClientSecret]           NVarChar(255)     NULL,
	[ClientType]             NVarChar(255)     NULL,
	[ConsentType]            NVarChar(255)     NULL,
	[DisplayName]            NVarChar(255)     NULL,
	[DisplayNames]           NVarChar(255)     NULL,
	[Description]            NVarChar(255)     NULL,
	[Descriptions]           NVarChar(255)     NULL,
	[JsonWebKeySet]          NVarChar(255)     NULL,
	[Name]                   NVarChar(255)     NULL,
	[Payload]                NVarChar(255)     NULL,
	[Permissions]            NVarChar(255)     NULL,
	[PostLogoutRedirectUris] NVarChar(255)     NULL,
	[Properties]             NVarChar(255)     NULL,
	[RedirectUris]           NVarChar(255)     NULL,
	[ReferenceId]            NVarChar(255)     NULL,
	[Requirements]           NVarChar(255)     NULL,
	[Resources]              NVarChar(255)     NULL,
	[Scopes]                 NVarChar(255)     NULL,
	[Settings]               NVarChar(255)     NULL,
	[Status]                 NVarChar(255)     NULL,
	[Subject]                NVarChar(255)     NULL,
	[Type]                   NVarChar(255)     NULL,
	[CreationDate]           INTEGER           NULL,
	[ExpirationDate]         INTEGER           NULL,
	[RedemptionDate]         INTEGER           NULL,

	CONSTRAINT [PK_OidcApplications] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX OidcClientIndex ON OidcApplications (ClientId) WHERE ClientId IS NOT NULL;

-- 正在傾印表格  sso.OidcApplications 的資料：0 rows

-- 傾印  資料表 sso.OidcAuthorizations 結構
DROP TABLE IF EXISTS "OidcAuthorizations";
CREATE TABLE IF NOT EXISTS [OidcAuthorizations]
(
	[Id]                     NVarChar(255)     NULL,
	[ConcurrencyToken]       NVarChar(255)     NULL,
	[ApplicationId]          NVarChar(255)     NULL,
	[AuthorizationId]        NVarChar(255)     NULL,
	[ApplicationType]        NVarChar(255)     NULL,
	[ClientId]               NVarChar(255)     NULL,
	[ClientSecret]           NVarChar(255)     NULL,
	[ClientType]             NVarChar(255)     NULL,
	[ConsentType]            NVarChar(255)     NULL,
	[DisplayName]            NVarChar(255)     NULL,
	[DisplayNames]           NVarChar(255)     NULL,
	[Description]            NVarChar(255)     NULL,
	[Descriptions]           NVarChar(255)     NULL,
	[JsonWebKeySet]          NVarChar(255)     NULL,
	[Name]                   NVarChar(255)     NULL,
	[Payload]                NVarChar(255)     NULL,
	[Permissions]            NVarChar(255)     NULL,
	[PostLogoutRedirectUris] NVarChar(255)     NULL,
	[Properties]             NVarChar(255)     NULL,
	[RedirectUris]           NVarChar(255)     NULL,
	[ReferenceId]            NVarChar(255)     NULL,
	[Requirements]           NVarChar(255)     NULL,
	[Resources]              NVarChar(255)     NULL,
	[Scopes]                 NVarChar(255)     NULL,
	[Settings]               NVarChar(255)     NULL,
	[Status]                 NVarChar(255)     NULL,
	[Subject]                NVarChar(255)     NULL,
	[Type]                   NVarChar(255)     NULL,
	[CreationDate]           INTEGER           NULL,
	[ExpirationDate]         INTEGER           NULL,
	[RedemptionDate]         INTEGER           NULL,

	CONSTRAINT [PK_OidcAuthorizations] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.OidcAuthorizations 的資料：0 rows

-- 傾印  資料表 sso.OidcScopes 結構
DROP TABLE IF EXISTS "OidcScopes";
CREATE TABLE IF NOT EXISTS [OidcScopes]
(
	[Id]                     NVarChar(255)     NULL,
	[ConcurrencyToken]       NVarChar(255)     NULL,
	[ApplicationId]          NVarChar(255)     NULL,
	[AuthorizationId]        NVarChar(255)     NULL,
	[ApplicationType]        NVarChar(255)     NULL,
	[ClientId]               NVarChar(255)     NULL,
	[ClientSecret]           NVarChar(255)     NULL,
	[ClientType]             NVarChar(255)     NULL,
	[ConsentType]            NVarChar(255)     NULL,
	[DisplayName]            NVarChar(255)     NULL,
	[DisplayNames]           NVarChar(255)     NULL,
	[Description]            NVarChar(255)     NULL,
	[Descriptions]           NVarChar(255)     NULL,
	[JsonWebKeySet]          NVarChar(255)     NULL,
	[Name]                   NVarChar(255)     NULL,
	[Payload]                NVarChar(255)     NULL,
	[Permissions]            NVarChar(255)     NULL,
	[PostLogoutRedirectUris] NVarChar(255)     NULL,
	[Properties]             NVarChar(255)     NULL,
	[RedirectUris]           NVarChar(255)     NULL,
	[ReferenceId]            NVarChar(255)     NULL,
	[Requirements]           NVarChar(255)     NULL,
	[Resources]              NVarChar(255)     NULL,
	[Scopes]                 NVarChar(255)     NULL,
	[Settings]               NVarChar(255)     NULL,
	[Status]                 NVarChar(255)     NULL,
	[Subject]                NVarChar(255)     NULL,
	[Type]                   NVarChar(255)     NULL,
	[CreationDate]           INTEGER           NULL,
	[ExpirationDate]         INTEGER           NULL,
	[RedemptionDate]         INTEGER           NULL,

	CONSTRAINT [PK_OidcScopes] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX OidcScopeNameIndex ON OidcScopes (Name) WHERE Name IS NOT NULL;

-- 正在傾印表格  sso.OidcScopes 的資料：0 rows

-- 傾印  資料表 sso.OidcTokens 結構
DROP TABLE IF EXISTS "OidcTokens";
CREATE TABLE IF NOT EXISTS [OidcTokens]
(
	[Id]                     NVarChar(255)     NULL,
	[ConcurrencyToken]       NVarChar(255)     NULL,
	[ApplicationId]          NVarChar(255)     NULL,
	[AuthorizationId]        NVarChar(255)     NULL,
	[ApplicationType]        NVarChar(255)     NULL,
	[ClientId]               NVarChar(255)     NULL,
	[ClientSecret]           NVarChar(255)     NULL,
	[ClientType]             NVarChar(255)     NULL,
	[ConsentType]            NVarChar(255)     NULL,
	[DisplayName]            NVarChar(255)     NULL,
	[DisplayNames]           NVarChar(255)     NULL,
	[Description]            NVarChar(255)     NULL,
	[Descriptions]           NVarChar(255)     NULL,
	[JsonWebKeySet]          NVarChar(255)     NULL,
	[Name]                   NVarChar(255)     NULL,
	[Payload]                NVarChar(255)     NULL,
	[Permissions]            NVarChar(255)     NULL,
	[PostLogoutRedirectUris] NVarChar(255)     NULL,
	[Properties]             NVarChar(255)     NULL,
	[RedirectUris]           NVarChar(255)     NULL,
	[ReferenceId]            NVarChar(255)     NULL,
	[Requirements]           NVarChar(255)     NULL,
	[Resources]              NVarChar(255)     NULL,
	[Scopes]                 NVarChar(255)     NULL,
	[Settings]               NVarChar(255)     NULL,
	[Status]                 NVarChar(255)     NULL,
	[Subject]                NVarChar(255)     NULL,
	[Type]                   NVarChar(255)     NULL,
	[CreationDate]           INTEGER           NULL,
	[ExpirationDate]         INTEGER           NULL,
	[RedemptionDate]         INTEGER           NULL,

	CONSTRAINT [PK_OidcTokens] PRIMARY KEY ([Id])
);
;
CREATE UNIQUE INDEX OidcReferenceIndex ON OidcTokens (ReferenceId) WHERE ReferenceId IS NOT NULL;
CREATE INDEX OidcTokenAuthIndex ON OidcTokens (AuthorizationId);

-- 正在傾印表格  sso.OidcTokens 的資料：0 rows

-- 傾印  資料表 sso.PortalAudiences 結構
DROP TABLE IF EXISTS "PortalAudiences";
CREATE TABLE IF NOT EXISTS [PortalAudiences]
(
	[ClientId]            NVarChar(255)     NULL,
	[IdentityTypesJson]   NVarChar(255)     NULL,
	[StudentStatusesJson] NVarChar(255)     NULL,
	[EducationLevelsJson] NVarChar(255)     NULL,
	[RulesJson]           NVarChar(255)     NULL,

	CONSTRAINT [PK_PortalAudiences] PRIMARY KEY ([ClientId])
);
;

-- 正在傾印表格  sso.PortalAudiences 的資料：35 rows
REPLACE INTO "PortalAudiences" ("ClientId", "IdentityTypesJson", "StudentStatusesJson", "EducationLevelsJson", "RulesJson") VALUES
	('link-acadmis', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-account02', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-account03', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-activity', '[]', '[]', '[]', ''),
	('link-addcrs', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-alumni-benefits', '[]', '[]', '[]', ''),
	('link-alumni-card', '[]', '[]', '[]', ''),
	('link-alumni-center', '[]', '[]', '[]', ''),
	('link-alumni-jobs', '[]', '[]', '[]', ''),
	('link-alumni-system', '[]', '[]', '[]', ''),
	('link-care', '["student"]', '[]', '[]', ''),
	('link-centapp', '[]', '[]', '[]', ''),
	('link-docu', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-elearning', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-empservice', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-fixall', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-giving', '[]', '[]', '[]', ''),
	('link-higheredu', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-leave', '["student"]', '[]', '[]', ''),
	('link-newvenue', '[]', '[]', '[]', ''),
	('link-officemail', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-ohr', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-oosaf', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-post', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-primo', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-projectiac', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-projectrad', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-ptsign', '["student"]', '[]', '[]', ''),
	('link-selcrs', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-sop', '["teacher","staff","employee"]', '[]', '[]', ''),
	('link-stdsel', '["student"]', '[]', '[]', ''),
	('link-suggest', '[]', '[]', '[]', ''),
	('link-vms', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-waiver', '["student","teacher","staff","employee"]', '[]', '[]', ''),
	('link-webap0', '["student","teacher","staff","employee"]', '[]', '[]', '');

-- 傾印  資料表 sso.PortalCategories 結構
DROP TABLE IF EXISTS "PortalCategories";
CREATE TABLE IF NOT EXISTS [PortalCategories]
(
	[Key]       NVarChar(255)     NULL,
	[Name]      NVarChar(255)     NULL,
	[NameEn]    NVarChar(255)     NULL,
	[Accent]    NVarChar(255)     NULL,
	[SortOrder] INTEGER       NOT NULL,
	[Revision]  NVarChar(255)     NULL,

	CONSTRAINT [PK_PortalCategories] PRIMARY KEY ([Key])
);
;

-- 正在傾印表格  sso.PortalCategories 的資料：9 rows
REPLACE INTO "PortalCategories" ("Key", "Name", "NameEn", "Accent", "SortOrder", "Revision") VALUES
	('academic', '教務類', '', 'indigo', 10, '3ab0f689342e41d490d2e58cdb5e4fe1'),
	('affairs', '總務類', '', 'slate', 50, '7c52f0d6346f483fb40f339ea46adfa8'),
	('campus', '學務類', '', 'teal', 20, '9b9fac86ff0e4ca39fc4bdca80bb0d4e'),
	('document', '文書類', '', 'cyan', 70, 'd5ff0ae61881446c8548548ce71569d1'),
	('finance', '財務類', '', 'rose', 40, '46240c0d28704bcbbecc2dc5af97425d'),
	('general', '綜合服務', '', 'green', 90, 'f20f68288ee74a78860254fe4c4f2dca'),
	('hr', '人事類', '', 'amber', 30, '8457c283c3044edfaf75580db22574fb'),
	('it', '資訊類', '', 'blue', 80, 'cd385fba4add48c1a940bbc9d865ff56'),
	('research', '研究類', '', 'violet', 60, '3bbbc24267b8422894a75b213face97f');

-- 傾印  資料表 sso.PortalCategoryAudits 結構
DROP TABLE IF EXISTS "PortalCategoryAudits";
CREATE TABLE IF NOT EXISTS [PortalCategoryAudits]
(
	[Id]          INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt]  INTEGER        NOT NULL,
	[CategoryKey] NVarChar(255)      NULL,
	[ActorId]     NVarChar(255)      NULL,
	[RemoteIp]    NVarChar(255)      NULL,
	[Action]      NVarChar(255)      NULL,
	[BeforeJson]  NVarChar(255)      NULL,
	[AfterJson]   NVarChar(255)      NULL
);

-- 正在傾印表格  sso.PortalCategoryAudits 的資料：0 rows

-- 傾印  資料表 sso.RegisteredSystems 結構
DROP TABLE IF EXISTS "RegisteredSystems";
CREATE TABLE IF NOT EXISTS [RegisteredSystems]
(
	[ClientId]               NVarChar(255)     NULL,
	[Protocol]               NVarChar(255)     NULL,
	[DisplayName]            NVarChar(255)     NULL,
	[DisplayNameEn]          NVarChar(255)     NULL,
	[Description]            NVarChar(255)     NULL,
	[DescriptionEn]          NVarChar(255)     NULL,
	[EntryUrl]               NVarChar(255)     NULL,
	[RedirectUri]            NVarChar(255)     NULL,
	[PostLogoutRedirectUri]  NVarChar(255)     NULL,
	[Enabled]                Bit           NOT NULL,
	[SamlEntityId]           NVarChar(255)     NULL,
	[SamlSigningCertificate] NVarChar(255)     NULL,
	[ShowOnPortal]           Bit           NOT NULL,
	[PublicStudent]          Bit           NOT NULL,
	[PublicEmployee]         Bit           NOT NULL,
	[PublicAlumni]           Bit           NOT NULL,
	[RequireLoginToView]     Bit           NOT NULL,
	[RequirePortalLogin]     Bit           NOT NULL,
	[SortOrder]              INTEGER       NOT NULL,
	[ClaimMask]              INTEGER       NOT NULL,
	[Category]               NVarChar(255)     NULL,
	[Keywords]               NVarChar(255)     NULL,
	[Revision]               NVarChar(255)     NULL,

	CONSTRAINT [PK_RegisteredSystems] PRIMARY KEY ([ClientId])
);
;
CREATE UNIQUE INDEX SamlEntityIndex ON RegisteredSystems (SamlEntityId) WHERE Protocol = 'saml';

-- 正在傾印表格  sso.RegisteredSystems 的資料：35 rows
REPLACE INTO "RegisteredSystems" ("ClientId", "Protocol", "DisplayName", "DisplayNameEn", "Description", "DescriptionEn", "EntryUrl", "RedirectUri", "PostLogoutRedirectUri", "Enabled", "SamlEntityId", "SamlSigningCertificate", "ShowOnPortal", "PublicStudent", "PublicEmployee", "PublicAlumni", "RequireLoginToView", "RequirePortalLogin", "SortOrder", "ClaimMask", "Category", "Keywords", "Revision") VALUES
	('link-acadmis', 'link', '入學獎學金申請', '', '', '', 'https://acadmis.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 109, 0, 'campus', '', 'f2f966115ce741eab040b12e1adc008f'),
	('link-account02', 'link', '主計請購系統', '', '', '', 'https://account02.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 114, 0, 'finance', '', '33c8b4801f4d4acda7d006295c3d212c'),
	('link-account03', 'link', '經費結報簽核', '', '', '', 'https://account03.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 115, 0, 'finance', '', '42c2e8c324b44fcb8f4586f6b10f5c9d'),
	('link-activity', 'link', '活動訊息平台', '', '', '', 'https://ws1.nkust.edu.tw/Activity/', '', '', '0', '', '', '1', '1', '1', '1', '0', '0', 110, 0, 'campus', '', '8c6ec5d2b6764cdfb00a43c5b0e568bb'),
	('link-addcrs', 'link', '選課表單申請暨簽核', '', '', '', 'https://apply2.nkust.edu.tw/AddCrsApply', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 104, 0, 'academic', '', '0bd11a79b31c47af9a67c357bb0cb052'),
	('link-alumni-benefits', 'link', '校友優惠資訊', '', '', '', 'https://ascdc.nkust.edu.tw/p/412-1022-6565.php', '', '', '0', '', '', '1', '0', '0', '1', '0', '0', 134, 0, 'general', '', 'a53f068ac0364104811ed6b179eb5e69'),
	('link-alumni-card', 'link', '校友證申請', '', '', '', 'https://ascdc.nkust.edu.tw/p/412-1022-1425.php?Lang=zh-tw', '', '', '0', '', '', '1', '0', '0', '1', '0', '0', 132, 0, 'general', '', 'e5b57a1cc78543c2ad3d6a53472b2fee'),
	('link-alumni-center', 'link', '校友服務中心', '', '', '', 'https://ascdc.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '0', '1', '0', '0', 130, 0, 'general', '', '021dbf6c67aa43b3b3098d4b62b91ce2'),
	('link-alumni-jobs', 'link', '就業職缺資訊', '', '', '', 'https://ascdc.nkust.edu.tw/p/412-1022-8177.php', '', '', '0', '', '', '1', '0', '0', '1', '0', '0', 133, 0, 'general', '', '3c4b19c006714614bca211e5d885bde2'),
	('link-alumni-system', 'link', '校友服務系統', '', '', '', 'https://as.nkust.edu.tw/Login', '', '', '0', '', '', '1', '0', '0', '1', '0', '0', 131, 0, 'general', '', '0c96b2353f1e4b0d8c1560c0fd2f0955'),
	('link-care', 'link', '預約諮商輔導', '', '', '', 'https://care.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '0', '0', '0', '0', 108, 0, 'campus', '', '9fee97ea0c3346379a240560070297ab'),
	('link-centapp', 'link', '學籍成績證明申請', '', '', '', 'https://centapp.nkust.edu.tw/Home', '', '', '0', '', '', '1', '1', '0', '1', '0', '0', 105, 0, 'academic', '', '13790b121aa24329ac93408a0b3de32f'),
	('link-docu', 'link', '文件流程查詢', '', '', '', 'https://docu.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 124, 0, 'document', '', '22eff87482df4cadbc2f07df426430a4'),
	('link-elearning', 'link', '教學平台', '', '', '', 'https://elearning.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 102, 0, 'academic', '', '54dff3fe268043be8b84dd043b434d63'),
	('link-empservice', 'link', '在職服務證明申請', '', '', '', 'https://empservice.nkust.edu.tw/EmployService', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 112, 0, 'hr', '', 'a502d4a2022348b0b03024f4b6005c37'),
	('link-fixall', 'link', '各式維修申請', '', '', '', 'https://ws2.nkust.edu.tw/fixall', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 119, 0, 'affairs', '', 'e8a5a6d4ff5d4873929d55e0fbe61cff'),
	('link-giving', 'link', '捐贈興學', '', '', '', 'https://giving.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '1', '0', '0', 129, 0, 'general', '', '52ce4e85d5294a359931921af949fe5d'),
	('link-higheredu', 'link', '教學補助申請', '', '', '', 'https://higheredu.nkust.edu.tw/service/index?type=teacher', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 123, 0, 'research', '', 'e1b1504954c241e7b873d2698e986632'),
	('link-leave', 'link', '線上請假', '', '', '', 'https://oosaf.nkust.edu.tw/Student/Leave/Create', '', '', '0', '', '', '1', '1', '0', '0', '0', '0', 107, 0, 'campus', '', '023c245bafc6466aaebd662df316a224'),
	('link-newvenue', 'link', '場地租借暨門禁', '', '', '', 'https://newvenue.nkust.edu.tw/rental-system/', '', '', '0', '', '', '1', '1', '1', '1', '0', '0', 118, 0, 'affairs', '', '75fa2af9511a437081be407ed9eed0aa'),
	('link-officemail', 'link', '公務電子郵件平台', '', '', '', 'https://officemail.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 126, 0, 'it', '', '2cb5c011458849c38f00b3db5ba6196b'),
	('link-ohr', 'link', '進用暨保費管理', '', '', '', 'https://ohr.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 113, 0, 'hr', '', '2ae92e6f73ed4e6eae0d36e9063d7e63'),
	('link-oosaf', 'link', '學務資訊系統', '', '', '', 'https://oosaf.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 106, 0, 'campus', '', '46c88651bb0745879fa6c3a06ab3b87e'),
	('link-post', 'link', '郵件查詢', '', '', '', 'https://post.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 120, 0, 'affairs', '', 'b2ea1215d8994ff49907a02cf5230e18'),
	('link-primo', 'link', '圖書資源查詢', '', '', '', 'https://nkust.primo.exlibrisgroup.com/discovery/search', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 128, 0, 'general', '', 'e0da8eb6350d483fadfefcf7e1e77fb4'),
	('link-projectiac', 'link', '非政府產學計畫', '', '', '', 'https://ws3.nkust.edu.tw/ProjectIAC', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 122, 0, 'research', '', 'a4b8f2fd407d453ea7b2e9e2026c4646'),
	('link-projectrad', 'link', '政府產學計畫', '', '', '', 'https://ws3.nkust.edu.tw/ProjectRAD', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 121, 0, 'research', '', 'c3dd629cd24a4c8280de4a8d0ad431ec'),
	('link-ptsign', 'link', '兼任助理簽到', '', '', '', 'https://ntapp.nkust.edu.tw/ptSign/', '', '', '0', '', '', '1', '1', '0', '0', '0', '0', 111, 0, 'campus', '', '90223ff2b11e4efcb292a7a6bdfd3362'),
	('link-selcrs', 'link', '選課管理', '', '', '', 'https://selcrs.nkust.edu.tw/selcrs/Account/Login', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 101, 0, 'academic', '', 'da7a8678c81e498c87661846d126b4d3'),
	('link-sop', 'link', '業務流程 SOP', '', '', '', 'https://sop.nkust.edu.tw/', '', '', '0', '', '', '1', '0', '1', '0', '0', '0', 125, 0, 'document', '', 'c12fc9f631d442249cfcd1fc10bb3b6b'),
	('link-stdsel', 'link', '選課系統', '', '', '', 'https://stdsel.nkust.edu.tw/dispatch', '', '', '0', '', '', '1', '1', '0', '0', '0', '0', 100, 0, 'academic', '', 'a9a7d8765fb54e5f8cd0aedd0550ecd1'),
	('link-suggest', 'link', '校務建言', '', '', '', 'https://suggest.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '1', '0', '0', 127, 0, 'general', '', 'e6e23b9682fe416ebce3297160e69ec4'),
	('link-vms', 'link', '校車預約', '', '', '', 'https://vms.nkust.edu.tw/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 116, 0, 'affairs', '', '3010f03e2641419b8867579fe880e281'),
	('link-waiver', 'link', '學分抵免申請', '', '', '', 'https://ws3.nkust.edu.tw/Waiver', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 103, 0, 'academic', '', 'e95c1a2153a24cebae5ce40657136dde'),
	('link-webap0', 'link', '停車證申請', '', '', '', 'https://webap0.nkust.edu.tw/nkust/', '', '', '0', '', '', '1', '1', '1', '0', '0', '0', 117, 0, 'affairs', '', '58f43494b73c442dbf9f42591acc5be5');

-- 傾印  資料表 sso.SchemaMigrations 結構
DROP TABLE IF EXISTS "SchemaMigrations";
CREATE TABLE IF NOT EXISTS [SchemaMigrations]
(
	[Id] NVarChar(255)     NULL,

	CONSTRAINT [PK_SchemaMigrations] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.SchemaMigrations 的資料：9 rows
REPLACE INTO "SchemaMigrations" ("Id") VALUES
	('admin-management-v1'),
	('admin-manuals-v1'),
	('campus-employee-identity-v1'),
	('identity-classification-v1'),
	('identity-student-kind-v1'),
	('integration-return-fields-v1'),
	('integration-service-grants-v1'),
	('portal-categories-v1'),
	('public-campus-directory-v1');

-- 傾印  資料表 sso.SystemConfigurationAudits 結構
DROP TABLE IF EXISTS "SystemConfigurationAudits";
CREATE TABLE IF NOT EXISTS [SystemConfigurationAudits]
(
	[Id]         INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[OccurredAt] INTEGER        NOT NULL,
	[ActorId]    NVarChar(255)      NULL,
	[ClientId]   NVarChar(255)      NULL,
	[Action]     NVarChar(255)      NULL,
	[RemoteIp]   NVarChar(255)      NULL,
	[BeforeJson] NVarChar(255)      NULL,
	[AfterJson]  NVarChar(255)      NULL
);

-- 正在傾印表格  sso.SystemConfigurationAudits 的資料：0 rows

-- 傾印  資料表 sso.TicketClients 結構
DROP TABLE IF EXISTS "TicketClients";
CREATE TABLE IF NOT EXISTS [TicketClients]
(
	[Id]                    NVarChar(255)     NULL,
	[DisplayName]           NVarChar(255)     NULL,
	[SecretHash]            NVarChar(255)     NULL,
	[RedirectUri]           NVarChar(255)     NULL,
	[Enabled]               Bit           NOT NULL,
	[IncludeStudentProfile] Bit           NOT NULL,

	CONSTRAINT [PK_TicketClients] PRIMARY KEY ([Id])
);
;

-- 正在傾印表格  sso.TicketClients 的資料：0 rows

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
