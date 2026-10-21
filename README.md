# NKUST SSO Server

本專案只包含 ASP.NET Core 10 SSO Server，使用 OpenIddict、linq2db 與 SQL Server／SQLite 儲存層，提供校園入口、登入頁、管理介面、OIDC、Web API 單一登入（自訂協定）及 SAML 2.0 IdP。接收系統自行實作登入回呼及業務權限，不包含示範用戶端。

目前仍是 Development 環境的開發原型，已接入 MSSQL 校務帳密驗證及唯讀分類查詢；完整學籍、多重身分、LDAP 與舊 ASP／DLL 介面仍待串接。

交給接收系統開發人員的文件集中在 [串接文件目錄](docs/developer-integration/index.html)：共 16 份獨立 PDF，包含串接總覽、OIDC、Web API 單一登入、SAML、JSON 帳密驗證、SOAP、一般連結，以及 9 個已實作 SOAP 方法的個別規格。每份附流程圖、必要設定、欄位與範例，可直接交付 [PDF 全集 ZIP](docs/developer-integration/開發人員串接PDF全集.zip)。維護原稿與產生方式見該目錄的 `generate.py`；網址、密鑰及人員資料均使用占位符。

後台「串接手冊」（`/admin/manuals`）提供 PDF 及 ZIP 下載，清單與每次下載均檢查登入及 `manuals`／`manuals:read` 權限。最高管理者可直接使用；既有一般角色須由權限管理另行授權。文件嵌入 Server 組件，不在 `wwwroot`，不對外提供靜態下載；回應禁止快取。手冊更新後執行 `python docs/developer-integration/generate.py`，再重新編譯網站，使目錄與 PDF 一起更新。升級會一次補上選單，之後保留管理者的排序、停用及刪除設定。

## 身分入口與登入

登入前後，`/` 與 `/portal` 統一顯示學生、教職員（教師與職員合併）及較小的校友入口。點選專區後，進入 `/portal/student`、`/portal/employee` 或 `/portal/alumni` 的公開分類選單，可直接搜尋與瀏覽，不要求登入。`/portal` 隨時可返回專區選擇。

點選服務會另開分頁，原專區保留；`/portal/{section}/systems/{id}` 會重新確認發布狀態。已串接 SSO 的服務，或勾選「需先登入本站」（`RequirePortalLogin`）的外部連結，才要求中央登入；已有有效中央 Session 時直接前往服務。登入成功後接續原本選擇的服務，未要求本站登入的外部連結則直接開啟。此設定不會使外部網站自動支援 SSO，對方仍可能要求自己的帳密。登入後仍使用同一套身分專區，已移除舊個人首頁、常用與最近使用。完整判斷與目前設定見 [入口操作邏輯](docs/portal-design.md#目前的操作流程)。

`/account/login?area=student`、`area=employee`、`area=alumni` 僅調整頁面說明與協助連結，不會改變帳號身分、權限或驗證來源。已有有效中央 Session 時直接返回目標功能；OIDC `prompt=login` 仍要求重新驗證。登出入口後返回身分選擇頁。

校友專區可不登入瀏覽。校方校友服務系統仍使用原系統的帳號規則；本站沒有新增校友帳號來源或自動授予校友資格。選擇專區不會改變身分與權限。

登入者區塊上方顯示主要身分（學生、職員、教師、教職員、校友等）與姓名，下方顯示學生的「學號」、教職員的「員編」，其他身分則標示「帳號」。姓名缺漏時隱藏姓名欄，不顯示班級、學年期、學籍或單位資訊。身分來自已登入帳號，不依目前瀏覽的專區判定；來源僅能辨識教職員時仍顯示「教職員」，不推測為教師或職員。使用已保存的帳號資料，不額外查詢校務資料庫；桌面與手機均維持靠右。

登入者區塊直接使用 SSO 已保存的帳號資訊，不為顯示而額外查詢校務就學或人事資料。切回分頁會更新身分與帳號，登出時清除；手機維持靠右，中英切換保留。此簡化不更改登入驗證、選單授權或對外協定欄位。

管理者可在「串接系統管理 → 顯示專區與隱私」設定各入口的發布位置；未勾選的內部入口不會公開。勾選「登入後才顯示」可將指定專區的項目限制為符合身分者可見；私人入口的名稱及詳述不會送給未登入者，直接開啟也重新檢查資格。首次升級匯入 35 個校方公開指南連結（純連結，並非 SSO 串接），只對識別碼與網址吻合且仍顯示的既有純連結補上發布設定。自訂網址、隱藏入口及既有 SSO 設定不自動公開；重啟保留管理者的修改與刪除。

前台支援繁體中文／English 切換，語言偏好保存在瀏覽器；服務及分類的英文名稱、詳述由管理者維護。Header 提供回校首頁、忘記密碼及常見問題入口，`/faq` 集中顯示登入說明與四種身分的校方指南。介面與資料欄位見[入口設計](docs/portal-design.md#中英介面與英文內容)。

## 啟動

需求：Windows、.NET 10 SDK，可存取 NuGet。新設定範本使用本機 SQL Server 2019 以上的 `NkustSSO`；SQLite 隔離測試不需額外安裝資料庫。

首次從 Git 取得專案時，先複製設定範本；若本機已有設定則保留，不要覆蓋：

```powershell
if (!(Test-Path NkustSso.Server/appsettings.json)) {
    Copy-Item NkustSso.Server/appsettings.example.json NkustSso.Server/appsettings.json
}
```

實際 `appsettings.json` 與各環境設定不納入版本控制。範本預設停用校務連線，需要使用時再於本機設定 `ConnectionStrings:LoginAuth` 及 `CampusAuthentication:Enabled`；勿將資料庫密碼填入範本。

在 Visual Studio 選擇 `NkustSso.Server` 啟動，或用命令列：

```powershell
dotnet run --project NkustSso.Server
```

兩者都走 `Properties/launchSettings.json`，啟動 `https://localhost:7100`。若該埠已被占用，改用 `dotnet run --project NkustSso.Server --urls https://localhost:7200 --Sso:Issuer https://localhost:7200/`；Issuer 必須一起改，否則簽發的權杖會指向錯誤位址。

停止服務：Visual Studio 結束偵錯，命令列按 Ctrl+C。

`Properties/launchSettings.json` 的兩個啟動設定排除 `Microsoft.WebTools.BrowserLink.Net` 與 `Microsoft.AspNetCore.Watch.BrowserRefresh`，並設定 `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=true`。這些開發工具會在每個頁面注入腳本並連向其他本機埠，與本專案 CSP 衝突；不應因此放寬登入站的安全政策。調整後須在 Visual Studio 停止偵錯並重新 F5 啟動，已執行的程序不會自動套用環境變數。排除方式見 [Microsoft Hosting Startup 文件](https://learn.microsoft.com/aspnet/core/fundamentals/host/platform-specific-configuration?view=aspnetcore-10.0)。

初次啟動依 `Sso:StorageProvider` 建立自有資料表，並在 `.local/` 保存開發憑證與 Server 的 Data Protection 金鑰，再初始化必要分類及管理角色。每次啟動若後台群組表 `AdminRoles` 為空，會建立沒有成員的 `admin` 群組及八項一般後台維護權限；已有群組時不覆寫設定、不恢復撤權。最高管理者仍使用 `SsoAdministrator`，`admin` 群組不包含選單與權限管理資格。**不自動建立測試帳號、示範用戶端或假入口**。SQLite 模式使用 `.local/sso.db`、`.local/sessions.db`；SQL Server 模式將兩組資料表集中在 `NkustSSO.dbo`。既有資料庫、帳號、角色與串接設定會保留；切換 provider 不會自動搬資料。

### 本機管理者

既有帳號不會重設密碼或重新授予角色。若使用全新資料庫，且要以本機帳密測試管理介面，可在同一個 PowerShell 設定以下環境變數後啟動：

```powershell
$env:CampusAuthentication__Enabled = 'false'
$env:Sso__Administrator__UserName = 'admin'
$adminPassword = Read-Host '設定管理者密碼（至少 12 字元，含大小寫、數字、符號）' -AsSecureString
$env:Sso__Administrator__Password = [System.Net.NetworkCredential]::new('', $adminPassword).Password
dotnet run --project NkustSso.Server
Remove-Item Env:Sso__Administrator__Password
```

`dotnet run` 會占用該視窗，按 Ctrl+C 停止服務後才會執行最後一行清除密碼環境變數；期間不要關掉視窗，否則變數會留在該工作階段。

只在設定的帳號不存在時建立本機管理者，遵循 Identity 密碼規則；未設定時不建立帳號。啟用 MSSQL 校務登入時只接受來源帳密，本機管理者不適用，來源帳號的 `SsoAdministrator` 角色須另由技術端明確授予。

### 指派最高管理者

`SsoAdministrator` 是 Identity 角色（`AspNetUserRoles`），後台「權限管理」頁刻意不碰它，啟動程序也不會對既有帳號補授——否則重啟會把管理者撤除過的資格還原回去。啟用校務登入後本機管理者無法建立 Session，所以**第一位最高管理者只能由技術端指派**，否則 `/admin` 會因為沒有任何最高管理者而完全進不去（導覽列空白、`/admin/*` 一律 403、入口首頁的後台連結也會隱藏）。

```powershell
# 列出目前的最高管理者
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 -List

# 指派（帳號須已存在：校務帳號先登入過一次，或由既有最高管理者預先建立）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 42665

# 撤除；不允許撤除最後一位
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 42665 -Revoke
```

腳本預設讀 `appsettings.json` 的 `ConnectionStrings:Sso`，可用 `-ConnectionString` 覆寫，支援 `-WhatIf`。重複執行不會重複寫入。權限每次請求都重查資料庫，**指派後下一個請求即生效，不需要重新登入或重啟服務**。腳本只支援 SQL Server；SQLite 模式請直接操作 `.local/sso.db` 的 `AspNetUserRoles`。

管理者在 `/admin/systems` 預設維護前台首頁連結，可新增或修改名稱、網址、分類、排序與顯示專區；儲存後前台重新整理即可套用。分類名稱與順序在 `/admin/categories` 維護。SSO 接收系統及允許提供的欄位由「單一登入 SSO」分頁（`/admin/systems?section=sso`）進入。參閱 [系統管理](docs/system-administration.md)、[串接流程書](docs/sso-integration-guide.md)（[HTML](docs/sso-integration-guide.html)、[PDF](docs/sso-integration-guide.pdf)）及 [入口設計](docs/portal-design.md)。

## 本機 HTTPS

HTTPS 使用 ASP.NET Core 的開發憑證，由 Kestrel 在 Development 下自動採用。請在一般使用者 PowerShell 執行，讓 Windows SChannel 能存取使用者私鑰儲存空間。

該憑證須已受信任，否則串接端（例如宣傳品管理）讀取 `/.well-known/openid-configuration` 會以 `UntrustedRoot` 失敗：

```powershell
dotnet dev-certs https --check --trust
```

`.local/localhost.pfx` 的自簽憑證改為只保護 DataProtection 金鑰，不再供 HTTPS 使用；請勿刪除，否則 `.local/keys-server` 既有金鑰無法解開。

`.local` 包含資料庫、私鑰與本機設定，已由 `.gitignore` 排除，不應提交或分享。正式部署需使用正式 TLS 與持久簽章憑證。

## 專案配置

```text
NkustSso.Server/
  Controllers/       登入、協定及管理接口
  Services/          認證、Session、系統登記與來源查詢
  Models/            linq2db 資料模型
  ViewModels/        登入、專區及後台畫面的顯示資料與表單
  ApiModels/         對外 API 請求與回應格式
  Views/、wwwroot/   Server 登入、入口及管理介面
scripts/             文件匯出、前端套件更新、管理者指派與舊系統分析
tests/              Server 隔離測試與測試資料
docs/               需求、管理、串接及導入文件
```

方案只包含 Server 與其測試專案；Server 無其他專案或共用目錄相依性。

## linq2db 資料存取

### SSO 自有 SQL Server

`ConnectionStrings:Sso` 專供 SSO 帳號、管理設定、OIDC、Session 與票券寫入；`LoginAuth` 仍是獨立唯讀校務來源，不能替代前者。只允許自有庫 `NkustSSO`，隔離測試使用隨機 `NkustSSO_Test_...` 名稱。應先由管理者建立資料庫及授予自有庫權限，網站不建立 SQL Server 資料庫。

本機 Windows 驗證設定如下，只新增這兩個設定，不覆蓋既有 `appsettings.json`：

```json
"ConnectionStrings": {
  "Sso": "Server=lpc:localhost;Database=NkustSSO;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
},
"Sso": { "StorageProvider": "SqlServer" }
```

`lpc:localhost` 使用同機共用記憶體連線，執行網站的 Windows 身分必須能登入資料庫。`TrustServerCertificate=True` 僅適用明確信任的本機開發環境；遠端部署應配置可信憑證與伺服器名稱。SQL Server 模式連線失敗直接停止，不會回退到 SQLite。既有設定未指定 provider 時仍沿用 SQLite；測試則明確指定 `SQLite`。

所有字串採 Unicode，識別碼與雜湊維持大小寫敏感比較，Token／JSON／票證使用大型欄位。唯一索引排除可空的 OIDC 參照值，SAML Entity ID 以 SHA-256 計算欄位建立唯一索引，保留原本可達 1000 字元的上限。建表與補欄位可重跑，初始化有資料庫鎖，管理者設定仍依既有版本標記保留。

先建置至獨立目錄，再僅建立空資料結構（不啟動網站、不建立帳號、不連線校務來源）：

```powershell
dotnet build tests/NkustSso.StorageChecks/NkustSso.StorageChecks.csproj -o .local/mssql-build
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet .local/mssql-build/NkustSso.Server.dll --contentRoot "$PWD/NkustSso.Server" --initialize-storage true --Sso:StorageProvider SqlServer --ConnectionStrings:Sso 'Server=lpc:localhost;Database=NkustSSO;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
```

### 既有 SQLite 離線搬移

停止原網站的寫入後，備份完整的 `sso.db` 與 `sessions.db` 到 `.local/backups/` 的新目錄。Visual Studio 啟動的網站由 IDE 結束偵錯停止，命令列啟動的按 Ctrl+C。若有未 checkpoint 的 WAL，應使用 SQLite 備份 API 產生完整備份，不可只複製主檔。保留原資料庫及 `.local/` 憑證、Data Protection 金鑰。

先用現行 SQLite 版本完成資料結構升級，再從完整停站備份匯入。目的庫可已有空資料表，但不得有任何資料；匯入不會合併或清除目的庫：

```powershell
dotnet .local/mssql-build/NkustSso.Server.dll --contentRoot "$PWD/NkustSso.Server" --import-sqlite true --Sso:ImportDirectory "$PWD/.local/backups/停站備份目錄" --Sso:StorageProvider SqlServer --ConnectionStrings:Sso 'Server=lpc:localhost;Database=NkustSSO;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
```

工具唯讀開啟來源，拒絕未完成 WAL 的備份；將全部 38 張自有資料表在單一目的端交易中匯入，逐表核對筆數與每列欄位摘要，保留帳號密碼雜湊、管理設定、稽核流水號與 Session 資料。任何失敗均回滾，日誌只輸出表名與筆數。來源中的舊版閒置資料表不匯入，原檔完整保留。匯入完成才修改本機 `Sso:StorageProvider` 與連線設定，重新建置並啟動網站；切換後避免同時啟動舊 SQLite 網站。

SQL Server 隔離驗證需要本機 Windows 帳號具有建立／刪除測試庫的權限；測試只使用合成帳號與模擬校務來源，結束清理自己建立的隨機測試庫：

```powershell
dotnet .local/mssql-build/NkustSso.StorageChecks.dll --sqlserver
dotnet .local/mssql-build/NkustSso.StorageChecks.dll --sqlserver-storage
dotnet .local/mssql-build/NkustSso.StorageChecks.dll --sqlserver-browser e2e.py
dotnet .local/mssql-build/NkustSso.StorageChecks.dll --sqlserver-browser webapi_sso.py
```

`--sqlserver` 將核心 Identity／OIDC／Session 檢查改用 SQL Server，其餘來源模擬檢查仍使用隔離 SQLite；`--sqlserver-storage` 專驗證 SQL Server 索引、重放、並行票券、管理權限與匯入回滾。瀏覽器測試會先將合成 SQLite 資料匯入獨立 SQL Server 測試庫，再啟動隔離網站。

### 儲存層分工

- `Models/`：帳號、身分分類、系統登記及登入紀錄等資料模型，命名空間為 `NkustSso.Server.Models`。
- `Models/SsoDatabase.cs`：本地資料庫連線、資料表映射及初始建表。
- `Models/CampusDatabaseFactory.cs`：遠端 MSSQL 唯讀來源連線，不執行建表或遷移。
- `Models/LinqIdentityStores.cs`：Identity 帳號與角色的儲存介面。Identity 保留密碼雜湊、鎖定與登入功能，並不需要 Entity Framework。
- `Services/Linq*Store.cs`：OpenIddict Application、Authorization、Scope、Token 的自訂儲存介面；用並行版本欄位避免舊資料覆寫與授權碼重複兌換。
- `Services/LoginSessionStore.cs`：ASP.NET Core Cookie Authentication 的 `ITicketStore`，由 linq2db 儲存及驗證中央 Session。

SQLite 模式的 `sso.db` 存放帳號、角色、管理設定與 `Oidc*` 協定資料表；`sessions.db` 存放 Session、票券、重放防護與登入嘗試。SQL Server 模式使用同一 `NkustSSO` 資料庫，保留相同表名。延用 `AspNetUsers` 等表名以保留帳號與密碼雜湊，表名不代表使用 EF。原有 `OpenIddict*` 表保留在 SQLite 備份但不再讀寫，新協定流程改用 `Oidc*` 表。

本機帳號資料庫備份保存在 `.local/backups/`。建表補上不存在的表、索引與明確登記的新欄位，不會自動改變既有欄位型別；正式資料庫結構變更仍應經版本化遷移與備份驗證。

## 登入與欄位介面

### MSSQL 校務登入設定

開發測試時可於本機 `appsettings.json` 設定 `CampusAuthentication:DevelopmentAllowAnyPassword=true`，
並保持 `CampusAuthentication:Enabled=true`。僅在 `Development` 環境允許中央登入以任意非空白密碼
登入既有校務帳號，登入頁會顯示「開發測試模式」。帳號唯一性、停用／鎖定與管理權限檢查仍保留；
SOAP／JSON 帳密驗證不受此開關影響。預設值為 `false`，結束測試後改回 `false` 並重新啟動；
既有登入狀態請先登出，再檢查正常密碼驗證。此開關不會修改任何校務密碼或資料。

`NkustSso.Server/appsettings.json` 已提供空白連線範本：

```json
"ConnectionStrings": {
  "LoginAuth": "Server=;Database=;User ID=;Password=;Encrypt=True;TrustServerCertificate=False;Connect Timeout=10;"
}
```

填寫伺服器、資料庫、唯讀帳號及密碼後，將 `CampusAuthentication:Enabled` 改為 `true`。SSO 不以 `sts_id` 預先限制登入，已移除 `AllowedStatusIds` 設定。`StatusIdentityTypes` 僅將來源代碼對應為身分；未知、空白或未設定對應的代碼使用 `other`，仍可憑正確帳密登入，不會自動取得管理權。學生／人事顯示資料缺漏或關聯不唯一時，以帳號顯示且不任取資料；帳密表的帳號本身仍須唯一。各系統的授權須另行設定與檢查，不能把成功登入 SSO 當成所有系統都可使用。

登入以 `dbo.s90_auth_usr.usr_id` 查帳號，使用 `usr_mdpwd` 的天方格式驗證。學生查詢 `s30_student`、`s90_class`、`s90_unit`；教職員查詢 `s10_employee`。這些表須給來源帳號 SELECT 權限，程式不修改來源資料；`ApplicationIntent` 不作為權限保障。連線強制加密，`TrustServerCertificate` 依連線字串設定，未設定時預設驗證 SQL Server 憑證。開發環境若明確設定為 `True`，會略過伺服器憑證驗證；正式部署應使用可信任憑證並設為 `False`。逾時由 `CommandTimeoutSeconds` 控制。可使用既有 ASP.NET Core 設定覆寫機制；文件與範本不包含真實連線憑證。

分類查詢另需 `dbo.s90_auth_status`、`dbo.s90_srckind`、`dbo.s90_division`、`dbo.s90_unit` 與 `dbo.s90_cmparea` 的 SELECT 權限。學生狀況（復學生、轉學生等）使用獨立的 `student_kind` 種類，尚待確認學生紀錄關聯；不代替在學／休學的學籍狀態。校區名稱使用全名，空白時用簡稱；來源直接查詢，不另建同步工作。單位以既有 `XX00` 對應 `XX01`～`XX99` 呈現上下層，選取規則仍逐一比對代碼。

驗證成功後本地保存固定帳號識別與最小顯示資料快照，不保存來源密碼。中央 Session 再次使用時檢查來源帳號及身分對應；分類改變需重新登入更新快照，新的分類不會被禁止登入。目前沒有將任何 `sts_id` 定義為停用。啟用來源登入後，不接受同名本地密碼，也不接受原示範管理者登入；一般後台角色可由最高管理者在 `/admin/access` 指派；最高管理者資格仍須由技術端明確授予。尚未完成即時多重身分與完整學籍分類串接，詳細限制見 [身分來源文件](docs/identity-audiences.md)。

本工作區的 `appsettings.json` 已啟用校務登入；隔離本機測試可用 `CampusAuthentication__Enabled=false` 覆寫。Visual Studio 的 `SSO (7100)` 與 `NkustSso.Server` 皆使用 `https://localhost:7100`，與 `Sso:Issuer` 一致。Server 仍維持 Development 限制，沒有啟用正式部署。

### 協定欄位

採用 OIDC Authorization Code＋PKCE（S256），用戶端使用獨立機密。Redirect URI 精確註冊，不允許任意跳轉。Discovery 位址為 `https://localhost:7100/.well-known/openid-configuration`。

| Scope | 提供的資料 |
| --- | --- |
| openid | `sub` 等 OIDC 標準身分資料 |
| profile | `name`、`preferred_username` |
| student_profile | 以下自訂身分欄位 |

| 欄位 | 定義 | 示例 |
| --- | --- | --- |
| identity_type | 身分類別，與學籍狀態分開 | student |
| class_code | 班級代號 | DEMO-CS-3A |
| class_name | 班級名稱 | 資訊工程系 三年甲班 |
| student_status_code | 身分代號，代表學籍狀態 | DEMO_ACTIVE、DEMO_LEAVE |
| student_status_name | 學籍狀態名稱 | 在學、休學 |
| education_level_code | 學制代號 | DEMO_UG、DEMO_MASTER |
| education_level_name | 學制名稱 | 大學部、碩士班 |

以上代碼僅為開發樣本，正式代碼、完整清單與來源仍待確認。班級先實作代號與名稱兩欄，後續可依既有資料調整。用戶端以發行者 `iss` 與穩定的 `sub` 對應帳號，不以姓名或可變動的 Email 自動合併帳號。

自訂欄位只在申請且獲准的 `student_profile` 範圍下進入 ID Token。接收系統須使用 OIDC 函式庫驗證簽章、發行者、受眾、期限、state 與 nonce，不信任網址中自行傳入的身分。未建立下游 API，請勿把 ID Token 當作 API 存取權杖使用。

## 各系統的權限

SSO 提供已驗證的身分。接收系統以 `iss` 與 `sub` 對應自己的帳號及角色，在後端檢查功能權限；首頁可見規則不代替接收系統授權。

## 中央 Session 與管理

登入後的身分票證保存在伺服器資料庫，使用 Data Protection 加密；瀏覽器仍會收到安全 Cookie，但其中只持有受保護的 Session 參照。資料庫只存隨機 Session 金鑰的 SHA-256 雜湊，管理畫面的紀錄 ID 不能當登入憑證。

管理者登入後，點選「登入管理」，或開啟 `https://localhost:7100/admin/sessions`：

1. 依完整帳號及有效／已終止／已到期狀態查詢最近 100 筆。
2. 查看登入時間、最近活動、到期時間、來源 IP 及裝置資訊；時間以臺灣時區顯示。
3. 選擇終止單筆或該帳號全部中央登入，填寫原因後確認。
4. 查看最近 30 筆管理操作紀錄，包含操作者、對象、原因及影響數量。

管理頁每次請求都查核資料庫中的 `SsoAdministrator` 最高管理角色或已指派的一般後台角色，並依實際功能判定權限。最高管理者可在 `/admin/menus` 維護選單、`/admin/access` 維護角色與成員。未授權使用者不能呼叫管理操作，所有變更都要求 POST 與防偽權杖。詳見 [選單與權限管理](docs/system-administration.md)。日常依期限自動失效或使用者自行登出，管理者可在裝置遺失、疑似盜用等情況介入。

終止會立即標記中央 Session 並清除儲存的登入票證，下次存取 SSO 即需重新登入；已進行中的請求不會被中途取消。終止不是停用帳號，使用者仍可重新輸入帳密登入。Session 到期自動被拒絕，歷史紀錄目前保留在資料庫，尚未設定定期清理工作。

## 登出與資料更新

滑動續期、最長登入期限與各系統配合範圍，記錄於 [SSO 登入續期與系統整合需求](docs/sso-session-renewal.md)。

- 中央 Session 採 1 小時滑動窗口。剩餘期限不超過 30 分鐘時，符合條件的 SSO 頁面瀏覽或 OIDC 導向可延長至當下起算 1 小時；首次登入起算最多 4 小時，到上限必須重新登入。
- 單純停留在其他系統不會續期。現有 OIDC 流程會在需要重新驗證時自動導向 SSO，使用者不必手動回首頁。健康檢查、token exchange、已知背景 fetch、失敗回應及非 GET 請求不延長期限；沒有新增保活心跳。
- 接收系統自行設定本地登入期限，到期後重新透過 SSO 取得資料。
- 每次中央登入驗證都檢查 Session、帳號停用／鎖定狀態及 SecurityStamp；授權與換取權杖時重新讀取身分資料，且授權碼只能在對應中央 Session 有效時兌換。
- 登出入口會撤銷中央 Session 並清除瀏覽器的中央 Cookie；其他系統已建立的 Cookie 仍可能有效到原期限。
- 登出某個系統只清除該系統 Cookie，中央仍登入時再次進入可自動登入。
- 尚未實作跨系統即時登出、即時停權推播或 back-channel logout，因此中央停權不會立刻清除既有的用戶端 Cookie。

中央期限設定在 `NkustSso.Server/appsettings.json` 的 `Sso:Session`：`WindowMinutes=60`、`MaximumMinutes=240`。調整後重啟中央服務生效；必須滿足 `1 <= WindowMinutes <= MaximumMinutes <= 1440`。最長期限以資料庫原始 `CreatedAt` 計算，續期不修改這個時間。變更最長期限也會影響既有 Session 的有效性。管理頁會顯示「目前到期」與「最晚重新登入」時間。

## 建置與測試

```powershell
dotnet restore NkustSSO.slnx --packages .local/packages
dotnet build NkustSSO.slnx --no-restore
dotnet run --project tests/NkustSso.StorageChecks --no-build
```

StorageChecks 驗證空白 Server 初始化、明確設定的管理者、重啟保留密碼／鎖定／權限、資料庫並行更新、Session 續期與撤銷、校務認證、分類及 SAML 簽章。所有測試都使用 `.local` 下的獨立資料目錄，不修改開發資料庫。

瀏覽器測試自行啟動與停止隔離 Server，不需先執行開發啟動腳本，也不需示範網站。需 Python、Playwright 及 Chrome 或 Playwright Chromium：

```powershell
python -m pip install -r tests/requirements.txt
python -m playwright install chromium
python tests/gateway.py
python tests/e2e.py
python tests/sessions.py
python tests/systems.py
python tests/categories.py
python tests/identities.py
python tests/catalog.py
python tests/saml.py
```

涵蓋登入、入口搜尋、分類、私人項目、分頁登入、CSRF、OIDC／PKCE、授權碼重播、票券、欄位白名單、管理權限、Session 撤銷及 SAML 流程。測試帳號與協定用戶端只存在 `tests/NkustSso.StorageChecks/ServerTestData.cs`；測試會明確停用校務認證，截圖與日誌放在各自的 `.local/*-checks/` 子目錄。

## 接下來接入既有環境

全校系統盤點、整合單位分組與導入波段，見 [全校系統 SSO 導入規劃](docs/sso-rollout-plan.md)。

1. 確認正式資料庫類型、帳號資料表、密碼雜湊格式與既有唯一識別碼；不可直接假設舊密碼與 ASP.NET Core Identity 相容。
2. 對應班級、學籍狀態及學制資料，定義更新頻率與缺漏值。
3. 完成正式身分來源對應，調整 linq2db Mapping 與連線提供者，並持續維護版本化資料庫遷移。
4. 逐一註冊正式用戶端、機密與精確回呼網址，調整各系統的本地帳號對應。
5. SAML 使用 ITfoxtec.Identity.Saml2 元件提供 IdP；正式接入需確認對方支援下方的 Binding、NameID 與簽章要求。OpenIddict 僅負責原有 OIDC。
6. LDAP、舊 Web Service 或 DLL 必須先確認介面；目前未提供任意 DLL 載入或代填密碼功能。

目前 Server 會拒絕以非 Development 環境啟動。正式部署須建立持久簽章／加密憑證與輪替、正式 TLS、機密儲存、資料庫遷移及備份、反向代理與可信主機設定、集中日誌及保留政策，以及停權／登出的生效時間。開發用 OIDC 簽章金鑰只存在程序記憶體，登入中心重啟後未完成的登入流程需重新開始。

## SAML 2.0 接入

本專案作為 IdP，接收系統作為 SP。管理者在 `/admin/systems/new` 選擇 SAML，填入 SP Entity ID、ACS（登入回呼網址）與 SP 的 RSA 簽章公開憑證（PEM，至少 2048 位元且在有效期間內）。首頁入口填 SP 的 SAML 登入啟動網址；不直接填 ACS。對方匯入 `https://localhost:7100/saml/metadata`（依 `Sso:Issuer` 調整）並信任其中的 IdP 憑證。

流程：使用者點首頁入口 → SP 發出已簽章的 AuthnRequest → `/saml/sso` 驗證 → 沿用中央登入或導向登入表單 → 簽署 Response 與 Assertion → 瀏覽器 POST 到登記 ACS → SP 驗證並建立自己的登入狀態。

- 支援 **SP 發起、HTTP-Redirect 請求、HTTP-POST 回應**。SP 須用 RSA-SHA256 簽署 Redirect 查詢字串；不接受未簽章請求。Issuer、Destination 與 ACS 嚴格比對已登記值，不從請求或遠端 metadata 動態建立信任。
- NameID 使用固定帳號 ID，格式為 persistent。Attribute 名稱沿用 `preferred_username`、`name`、`identity_type`、班級／學籍／學制欄位；只提供管理者勾選的欄位，沒有 OIDC scope 要求。SP 應以 IdP Entity ID＋NameID 對應帳號。
- Assertion 與 SubjectConfirmation 有效 2 分鐘，指定 Audience、Recipient、InResponseTo。SP 必須驗證簽章、期限、受眾、接收網址、請求關聯及 Assertion 重放，不能只解碼 XML 就登入。
- 登入請求限過去 5 分鐘內，最多容忍未來 30 秒。續接流程有效 5 分鐘，綁定原瀏覽器並只能完成一次；相同 SP／Request ID 不可重用。RelayState 最多 80 UTF-8 位元組，只原樣傳回，不作為本站導向網址。
- 支援 `IsPassive`：沒有有效中央登入時回傳已簽章的 NoPassive 回應。尚未支援 ForceAuthn、HTTP-POST AuthnRequest、ACS 索引、IdP 主動發起、加密 Assertion、Artifact、SAML Single Logout；不支援的請求會拒絕，不會忽略後繼續核發。RequestedAuthnContext 僅接受 exact PasswordProtectedTransport。
- 本機專用簽章私鑰保存在 `.local/saml-signing.pfx`，與 HTTPS 憑證分開，重新啟動不更換。一年效期，不自動輪替；不得公開此檔案。正式環境需另完成私鑰保管、憑證更新與合作方信任更新流程；原型仍只允許 Development。
- 設定改變、停用系統、停用／鎖定帳號或撤銷中央 Session 會阻止後續登入。既有 SP Session 仍由對方管理。`sessions.db` 的 `SamlAudits` 保存結果，不保存完整 Assertion；流程與稽核分別保留至到期一天後及 30 天後清理。

驗證方式：`dotnet run --project tests/NkustSso.StorageChecks --no-restore` 包含獨立 XML 簽章驗證；安裝 `tests/requirements.txt` 後執行 `python tests/saml.py`，使用隔離資料庫與模擬 SP 檢查管理畫面、首頁自動登入、CSP、重放、錯誤簽章、ACS 與撤銷。尚未與校方實際 SP 進行互通測試。

## 參考

`/admin/systems` 統一提供「單一登入 SSO／帳密驗證 API／首頁連結」三個管理分頁。SOAP／JSON 管理位於 `/admin/systems/api?kind=new`，保留舊 `/admin/integration-services` 網址相容；資料及服務授權仍與 SSO 登記分開。SOAP 保留 `/KuasSSOWebService.asmx`，已實作 9 個方法；JSON 保留 `POST /api/integration/v1/verify`。服務預設關閉，尚未涵蓋所有舊方法。設定、操作與限制見 [串接服務說明](docs/integration-services.md)，盤點見 [舊 SOAP 相容性清單](docs/legacy-soap-compatibility.md)。

套件使用最新穩定版並固定版本。前端資源維持本機載入，版本與 npm 完整性記錄在 `NkustSso.Server/wwwroot/lib/versions.json`，更新使用 `scripts/update-frontend.py`。升級後須執行儲存、登入協定與瀏覽器相容性測試。

- [OpenIddict 伺服器設定](https://documentation.openiddict.com/guides/getting-started/creating-your-own-server-instance)
- [ASP.NET Core OIDC 用戶端](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication)
- [Keycloak 的 LDAP 與 SAML 整合能力](https://www.keycloak.org/docs/latest/server_admin/)

## 程式命名與維護導覽

資料模型依用途分類，每個型別各有同名檔案：`ViewModels/` 放畫面與表單模型，以 `ViewModel` 結尾；`ApiModels/` 放串接請求與回應，以 `Request`／`Response` 結尾；`Services/SoapDefinitions/` 放舊 SOAP 方法規格與 JSON 清單。資料庫實體及既有儲存設定保留於 `Models/`，業務服務的內部結果型別保留於所屬服務。

例如登入表單是 [LoginViewModel](NkustSso.Server/ViewModels/LoginViewModel.cs)，系統編輯頁是 [SsoSystemEditViewModel](NkustSso.Server/ViewModels/SsoSystemEditViewModel.cs)，票券兌換回應是 [SsoIdentityResponse](NkustSso.Server/ApiModels/SsoIdentityResponse.cs)。原本混在 Controller 與 Service 內的畫面模型已移至 `ViewModels/`；登入者資料查詢由 `CurrentUserDisplayService` 負責，`CurrentUserViewModel` 只保存畫面資料。搬移不改變 JSON 欄位名稱、驗證規則、SOAP 方法、資料表或網址。

檔名使用英文功能名稱；每個 Controller、Service 及 Razor View 開頭都有一行中文用途說明。Controller 處理 HTTP 與畫面導向，Service 處理驗證或資料規則，View 負責呈現。一般 CRUD 頁面保留 `Index.cshtml`、`Edit.cshtml` 慣例，由功能資料夾區分；登入、導向及協定頁面改用明確名稱。這次改名不更改資料表、對外網址、SOAP 方法或 JSON 欄位。

| 功能 | Controller | 主要 Service |
| --- | --- | --- |
| 帳密登入與登出 | [AccountLoginController](NkustSso.Server/Controllers/AccountLoginController.cs) | [CampusLoginService](NkustSso.Server/Services/CampusLoginService.cs) |
| 登入狀態查詢與撤銷 | [LoginSessionManagementController](NkustSso.Server/Controllers/LoginSessionManagementController.cs) | [LoginSessionStore](NkustSso.Server/Services/LoginSessionStore.cs) |
| SSO 系統及首頁連結維護 | [SsoSystemManagementController](NkustSso.Server/Controllers/SsoSystemManagementController.cs) | [SsoSystemRegistrationService](NkustSso.Server/Services/SsoSystemRegistrationService.cs) |
| Web API 單一登入 | [WebApiSsoController](NkustSso.Server/Controllers/WebApiSsoController.cs) | [LoginTicketService](NkustSso.Server/Services/LoginTicketService.cs) |
| OIDC 單一登入 | [OidcAuthorizationController](NkustSso.Server/Controllers/OidcAuthorizationController.cs) | [OidcIdentityClaims](NkustSso.Server/Services/OidcIdentityClaims.cs) |
| SAML 單一登入 | [SamlSsoController](NkustSso.Server/Controllers/SamlSsoController.cs) | [SamlIdentityProvider](NkustSso.Server/Services/SamlIdentityProvider.cs) |
| JSON／SOAP 帳密驗證 | [CredentialVerificationApiController](NkustSso.Server/Controllers/CredentialVerificationApiController.cs) | [CredentialVerificationService](NkustSso.Server/Services/CredentialVerificationService.cs) |
| JSON／SOAP 呼叫系統維護 | [IntegrationClientManagementController](NkustSso.Server/Controllers/IntegrationClientManagementController.cs) | [IntegrationClientRegistrationService](NkustSso.Server/Services/IntegrationClientRegistrationService.cs) |
| 後台角色與成員 | [AdminRoleManagementController](NkustSso.Server/Controllers/AdminRoleManagementController.cs) | [AdminManagementService](NkustSso.Server/Services/AdminManagementService.cs) |
| 服務選單 | [PortalServicesController](NkustSso.Server/Controllers/PortalServicesController.cs) | [PortalDirectoryService](NkustSso.Server/Services/PortalDirectoryService.cs) |

`LoginSessionStore` 保存及驗證瀏覽器的中央登入狀態；`LoginTicketService` 處理交給接收系統兌換的一次性登入票券。兩者職責不同。`LoginSessionLifetimeService` 計算期限，`ExpiredTicketCleanupService` 負責過期資料清理。

## 系統逆向架構與技術文件清冊 (Living Documentation)

由 `nkust-codebase-ingestion-v1` 逆向架構分析技能自動化產出與維護之標準技術文件：

### 全系統架構與規格報告
- 📋 [PROJECT_BOM.md](README/PROJECT_BOM.md) - 前後端完整技術物料清單 (BOM / LibMan / NuGet)
- 🚀 [PROJECT_BOM_UPGRADE.md](README/PROJECT_BOM_UPGRADE.md) - .NET 10 LTS 升級相容性比對、已棄用 (Deprecated) 元件檢測矩陣與行動指引
- 🔍 [PROJECT_REPORT.md](README/PROJECT_REPORT.md) - 全系統架構逆向工程報告、跨模組職責劃分、潛在風險邊際與 5 大現代化重構建議
- 🏛️ [PROJECT_ARCHITECTURE.md](README/PROJECT_ARCHITECTURE.md) - 系統分層架構圖、5 大 SSO 憑證交換協定全時序圖與資安橫切防護規格書
- 💾 [PROJECT_DATABASE.md](README/PROJECT_DATABASE.md) - SQL Server 與 SQLite 雙模儲存架構、30 張主表 + 8 張會話票券表 Schema、校務唯讀模型與分散式鎖機制

### 各子模組架構報告
- 📦 **NKUST_MODEL**：[架構報告](README/NKUST_MODEL/NKUST_MODEL_REPORT.md) ｜ [相依環境](README/NKUST_MODEL/NKUST_MODEL_ENV.md) ｜ [模組說明](README/NKUST_MODEL/README.md)
- ⚙️ **NKUST_SERVICE**：[架構報告](README/NKUST_SERVICE/NKUST_SERVICE_REPORT.md) ｜ [相依環境](README/NKUST_SERVICE/NKUST_SERVICE_ENV.md) ｜ [模組說明](README/NKUST_SERVICE/README.md)
- 📑 **NKUST_VIEWMODEL**：[架構報告](README/NKUST_VIEWMODEL/NKUST_VIEWMODEL_REPORT.md) ｜ [相依環境](README/NKUST_VIEWMODEL/NKUST_VIEWMODEL_ENV.md) ｜ [模組說明](README/NKUST_VIEWMODEL/README.md)
- 🌐 **NKUST_WEB**：[架構報告](README/NKUST_WEB/NKUST_WEB_REPORT.md) ｜ [相依環境](README/NKUST_WEB/NKUST_WEB_ENV.md) ｜ [模組說明](README/NKUST_WEB/README.md)

