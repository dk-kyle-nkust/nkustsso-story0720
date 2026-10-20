# NkustSSO 開發指引

本檔適用於整個儲存庫。修改前先閱讀 `CLAUDE.md`；啟動與環境設定參考 `README.md`。使用繁體中文說明工作結果，程式註解與 commit message 也使用繁體中文。

## 專案定位與架構

- 校園 SSO 與服務入口，採 ASP.NET Core MVC、.NET 10、OpenIddict、ASP.NET Core Identity、linq2db。
- `NkustSso.Server/`：網站、認證與串接服務。`Controllers/` 處理 HTTP，`Services/` 放業務邏輯，`Models/` 放資料庫實體與儲存設定，`ViewModels/` 放畫面與表單模型，`ApiModels/` 放串接請求與回應；舊 SOAP 方法規格放 `Services/SoapDefinitions/`。`Views/` 與 `wwwroot/` 放畫面與本機資源。
- 自有儲存由 `Sso:StorageProvider` 選擇：SQL Server 使用 `ConnectionStrings:Sso` 的 `NkustSSO`，所有自有資料表位於 `dbo`；SQLite 使用 `sso.db` 保存帳號、系統登記及管理設定，`sessions.db` 保存登入狀態、票券及嘗試紀錄。預設憑證與 SQLite 資料目錄為 `.local/`。切換前依 README 進行離線匯入，不能自動回退或清除既有資料。
- 校務 MSSQL 是外部唯讀來源，透過 `ICampusDatabaseFactory` 存取；不得對校務庫執行建表、遷移或資料修改。
- `tests/NkustSso.StorageChecks/` 是隔離的儲存與協定檢查程式；`tests/*.py` 是瀏覽器與 HTTP 測試。
- `docs/` 放串接與架構文件，`scripts/` 放開發操作腳本。`.local/` 為忽略追蹤的建置、資料與測試輸出。

## 功能範圍

- `/admin/systems` 管理 OIDC、SAML、Web API 單一登入及首頁連結。首頁顯示條件不等於接收系統的使用權限。
- `/admin/integration-services` 管理伺服器對伺服器的 SOAP／JSON 帳密驗證，使用獨立的 `IntegrationClients` 登記。
- SOAP 保留 `/KuasSSOWebService.asmx` 與 `?wsdl`；目前 9 個方法已實作 SOAP 1.1／1.2，由同一 `CredentialVerificationApiController` 分派；清單與差異見 `docs/legacy-soap-migration-status.md`。其餘舊方法見 `docs/legacy-soap-compatibility.md`，不可宣稱已能完整取代舊服務。
- JSON 入口為 `POST /api/integration/v1/verify`，使用來源 IP 與系統密鑰，僅回傳驗證結果。
- SSO 新增頁的「Web API 單一登入」沿用 `RegisteredSystems.Protocol=ticket`，由 `/ticket/authorize` 啟動中央登入，`POST /api/sso/v1/redeem` 接收 JSON 並兌換已授權身分欄位；與 `/ticket/redeem` 共用密鑰、PKCE、一次性消耗與 Session 檢查。這不是上述帳密驗證 API，也不是 Bearer Token 資源伺服器。
- 串接服務預設關閉。執行中的 SOAP／JSON 固定使用新校務 MSSQL，舊 LDAP／Sybase 不再作為備援；不得未經核對就將舊 LDAP 身分語意與新校務代碼視為相同。
- 新驗證介面不建立中央 Cookie，也不替接收系統建立角色或授權。保留既有登入入口與流程。
- 詳細設定、已知差異與部署限制見 `docs/integration-services.md`。

## 套件與前端

- 使用者要求套件採用**最新穩定版**，不使用預覽版。新增或升級前向官方 NuGet、npm 或 PyPI 核對；不能只憑既有快取或記憶判定版本。
- NuGet 直接參考固定於 `.csproj`；Python 測試依賴固定於 `tests/requirements.txt`。升級後執行相關相容性測試，不以不明來源套件替代。
- 前端資源一律放 `wwwroot/lib/`，不要使用執行期 CDN。版本與完整性記錄在 `wwwroot/lib/versions.json`；更新腳本為 `scripts/update-frontend.py`。
- 保留 CSP，不以 `unsafe-inline` 或放寬來源解決畫面問題。JS、CSS 放外部本機檔案，不使用 inline script、style 標籤或 style 屬性。
- 修改畫面或前端套件後，以瀏覽器確認桌面與手機版面，並檢查 `securitypolicyviolation` 與 JavaScript 錯誤。
- 管理表格使用 `data-datatable`；下拉使用 `data-enhance`。載入 `_AdminStyles`／`_AdminScripts`，由 `admin-ui.js` 統一初始化。
- 表格關鍵字搜尋交給 DataTables，伺服器端只保留會改變結果集的條件。不要新增重複搜尋框。
- `/integration` 是內部開發狀態頁，維護已實作／待實作狀態即可。

## 資料與認證規則

- 儲存初始化位於 `Models/SsoDatabase.cs`，SQL Server 欄位映射與自有資料表清單位於 `Models/SsoStorage.cs`。`CreateIfNotExists` 不會更新既有資料表；新增欄位須補兩種 provider 的 `AddColumns` 遷移並驗證既有資料保留。自有 SQL Server 可建表，校務來源不可。
- 變更預設資料時以舊值為更新條件，避免覆蓋管理者設定。管理異動沿用版本檢查與稽核機制。
- `/admin/menus`、`/admin/access` 維護後台選單與角色，僅最高管理者可用；`AdminAuthorizationService.Features` 定義可授權功能。一般角色按功能授權，選單隱藏不等於撤權；新管理控制器須加入固定功能對應，權限每次請求查核資料庫。
- 管理功能沿用 `LoginSessionAdministration.Policy`；瀏覽器 POST 保留防偽驗證。只有使用獨立呼叫端認證的協定端點可明確豁免。
- 維持 OIDC／票券／SAML 的回呼白名單、重放防護、欄位授權及 Session 撤銷檢查。
- 不記錄來源密碼、系統密鑰、完整 Cookie、票券或 Token。不得移植舊程式將明文密碼寫入同步佇列的副作用。
- 不信任呼叫者自填的 IP 或任意 `X-Forwarded-For`；代理來源必須有明確信任設定。
- 外部資料缺漏或關聯不唯一時，不猜測分類或任取一筆身分。依功能回傳拒絕或明確的資料不可用狀態。

## 建置與測試

使用 PowerShell；以 `rg` 搜尋，讀取中文檔案時明確指定 UTF-8。保留工作目錄中使用者或其他工作已做的修改。

若使用者的開發站或 Visual Studio 正在執行，優先建置到獨立輸出目錄，不為了測試擅自停止服務：

```powershell
dotnet restore NkustSSO.slnx
dotnet build tests/NkustSso.StorageChecks/NkustSso.StorageChecks.csproj --no-restore -o .local/agent-check-build
dotnet .local/agent-check-build/NkustSso.StorageChecks.dll
$env:NKUST_TEST_BUILD_DIRECTORY = "$PWD/.local/agent-check-build"
python tests/integration_api.py
python tests/e2e.py
python tests/systems.py .local/agent-check-build/NkustSso.Server.dll
python tests/saml.py
python tests/catalog.py .local/agent-check-build/NkustSso.StorageChecks.dll
```

依修改範圍選擇必要測試；上面列出主要入口，不要求每次文字修改都跑全套。`catalog.py` 與部分舊測試需明確傳入組件路徑，不能假設所有測試都讀取同一環境變數。

- Python 測試需安裝 `tests/requirements.txt`，使用 Chrome 或 Playwright Chromium；可在 `.local/` 建立獨立虛擬環境，避免修改使用者的全域套件。
- 測試只能使用隔離 SQLite 或隨機 `NkustSSO_Test_...` SQL Server 測試庫、合成帳號與模擬來源，明確停用真實校務認證及不需要的串接服務。不用真實帳密試登入，不向 `NkustSSO` 寫入測試資料。SQL Server 檢查入口與清理規則見 README。
- 測試資料只放測試專案，Server 啟動不建立示範帳號或測試用戶端。
- 測試結束停止自己建立的服務；不要清除或覆寫使用者的 `.local/sso.db`、憑證與金鑰。
- 如確實需要操作開發服務，用 `dotnet run --project NkustSso.Server` 啟動並自行結束；不要直接以程序名稱終止所有 dotnet 或網站程序，使用者在 Visual Studio 偵錯中的 `NkustSso.Server.exe` 一律由使用者自己停止。

## 設定、Git 與部署

- `NkustSso.Server/appsettings.json` 是忽略追蹤的本機設定，可能包含真實連線資訊。不要完整輸出、提交或以範本覆蓋它。
- 新環境參考 `appsettings.example.json`；機密放本機設定、環境變數或部署機密儲存。
- `.local/`、資料庫、私鑰、憑證與環境機密檔不得加入 Git。提交前檢查 staged diff，不使用強制加入來繞過忽略規則。
- 不自行推送、部署或切換正式網域。保留目前非 Development 啟動防護；正式 TLS、持久金鑰、代理、備份與互通測試完成前，不以移除防護代替部署準備。
- 回報時說明實際完成範圍、測試結果與尚未驗證的來源；區分「程式已實作」「已用隔離資料測試」「已與正式來源互通」。
