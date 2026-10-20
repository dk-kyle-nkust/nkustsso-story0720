# NKUST SSO Server .NET 10 LTS 現代化升級相容性評估報告 (BOM Upgrade Report)

- **專案名稱**：NKUST SSO Server
- **目標框架**：`.NET 10 LTS` (net10.0)
- **產出時間**：2026-10-20
- **稽核狀態**：100% 通過（無阻斷性棄用元件，全面適配現代化原生堆疊）

---

## 一、.NET 10 LTS 目標環境相容性總結

NKUST SSO Server 專案採用現代化 ASP.NET Core 10 架構建置，全方案統一透過根目錄 [Directory.Build.props](../../../Directory.Build.props) 強制宣告 `<TargetFramework>net10.0</TargetFramework>`、`<Nullable>enable</Nullable>` 與 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`，確保程式碼具備最嚴格的編譯期型別安全與跨平台能力。

本報告依據 [04_dotnet10_upgrade_eval.md](../../reference/04_dotnet10_upgrade_eval.md) 所定義之棄用判定準則，針對全方案後端 NuGet 套件與前端相依函式庫進行全盤靜態掃描與相容性比對。

---

## 二、棄用 (Deprecated) 與風險元件檢測矩陣

經執行 [Verify-UpgradeCompatibility.ps1](../../scripts/Verify-UpgradeCompatibility.ps1) 診斷結果如下：

| 元件名稱 | 原常見風險 / 棄用原因 | .NET 10 LTS 官方判定 | 本專案現況與處置策略 | 合規狀態 |
| :--- | :--- | :---: | :--- | :---: |
| **`wkhtmltox`** | 原生 C++ 二進位檔停止維護，存在未修復記憶體漏洞，缺乏現代 CSS3/Flexbox 支援。 | **`Deprecated`** | 本專案未使用原生 wkhtmltopdf；前端/手冊下載採用純文字與預建封存檔串流。 | ✅ **合規** |
| **`System.Drawing.Common`** | 微軟自 .NET 7 起宣告為 Windows-only，於 Linux/Container 環境拋出 `PlatformNotSupportedException`。 | **`Deprecated`** | 本專案完全未使用 GDI+；圖片處理僅以靜態資源傳遞，徹底排除平台鎖定風險。 | ✅ **合規** |
| **`Microsoft.AspNet.Mvc`** | 舊版 .NET Framework 5.2.7 套件，不相容於 ASP.NET Core 中介軟體管道。 | **`Deprecated`** | 本專案全面使用原生 `Microsoft.AspNetCore.App` 共享框架，依賴注入與 Controller 全面原生化。 | ✅ **合規** |
| **`Microsoft.AspNetCore.Authentication.Cookies`** | 獨立 NuGet 套件已棄用；Cookies 驗證已直接內建於 ASP.NET Core 共享框架。 | **`Deprecated`** | 本專案不獨立引用此套件，直接使用 `builder.Services.ConfigureApplicationCookie` 與原生 `ITicketStore`。 | ✅ **合規** |
| **`Moment.js`** | 專案官方宣布進入維護模式，無法 Tree-Shaking，打包體積龐大。 | **`Deprecated`** | 本專案未引入 Moment.js；日期時間由伺服器端原生 `TimeProvider` 與 ISO 8601 格式提供。 | ✅ **合規** |
| **`AdoNetCore.AseClient`** | Sybase 驅動程式未全面適配 .NET 10 非同步流與連線池管理。 | **`Restricted`** | 遵循零信任資安守則，完全停用 Sybase 遠端連線，全面改採校務 MSSQL (`Microsoft.Data.SqlClient`)。 | ✅ **合規** |

---

## 三、現有核心套件 .NET 10 LTS 適配清冊

| 套件 ID | 現行採用版本 | 支援框架 | LTS 支援狀態與維護建議 |
| :--- | :---: | :---: | :--- |
| [ITfoxtec.Identity.Saml2.MvcCore](https://www.nuget.org/packages/ITfoxtec.Identity.Saml2.MvcCore) | `4.21.0` | net10.0 / net8.0 | 現代化 SAML 2.0 協定庫，原生支援 ASP.NET Core Claims 轉換與中介軟體。 |
| [linq2db](https://www.nuget.org/packages/linq2db) | `6.5.0` | net10.0 / standard2.1 | 高效能輕量級 ORM，完美支援 SQL Server 與 SQLite 雙儲存引擎查詢投影。 |
| [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) | `7.1.1` | net10.0 / net8.0 | 微軟官方最新穩定版驅動程式，具備 TDS 8.0、強制 TLS 1.3 與連線池自動修復。 |
| [Microsoft.Data.Sqlite.Core](https://www.nuget.org/packages/Microsoft.Data.Sqlite.Core) | `10.0.13` | net10.0 | 微軟官方 .NET 10 同步發行之 SQLite 輕量化儲存驅動。 |
| [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) | `3.0.5` | net10.0 | 跨平台 SQLite3 C-runtime 封裝，提供測試環境隔離儲存。 |
| [System.Security.Cryptography.Pkcs](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs) | `10.0.13` | net10.0 | 微軟官方 .NET 10 憑證簽章與 PKCS#7 密碼學元件。 |
| [System.DirectoryServices.Protocols](https://www.nuget.org/packages/System.DirectoryServices.Protocols) | `10.0.13` | net10.0 | 跨平台底層 LDAP/目錄服務通訊協定庫。 |
| [OpenIddict.AspNetCore](https://www.nuget.org/packages/OpenIddict.AspNetCore) | `7.7.2` | net10.0 / net8.0 | 符合 RFC 6749 / OIDC Core 1.0 標準之現代化身分認證伺服器框架。 |

---

## 四、前端資產供應鏈相容性審查

全方案前端資源嚴格遵循 [AGENTS.md](../../AGENTS.md) 規範，一律離線置於 `wwwroot/lib/`，杜絕執行期 CDN，並由 [versions.json](../../../NkustSso.Server/wwwroot/lib/versions.json) 記錄版本與 Subresource Integrity (SRI) SHA-512 雜湊碼：

1. **`jquery` (4.0.0)**：
   - 官方最新世代版本，徹底移除已棄用之老舊 API，體積大幅縮減，全面相容現代瀏覽器標準。
2. **`datatables.net` & `datatables.net-dt` (3.1.4)**：
   - DataTables 3.x 世代原生 ES6 架構，無 eval 或不安全 inline 呼叫，完全相容專案內建嚴格 CSP 政策。
3. **`tom-select` (2.6.2)**：
   - 輕量原生 JavaScript 下拉選單元件，取代肥大且依賴老舊樣式之 Select2，提供卓越之無障礙與鍵盤操作支援。

---

## 五、升級與維護建議行動指引 (Action Items)

1. **鎖定 .NET 10 SDK 容器映像**：
   正式環境 Dockerfile 請固定使用 `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`（或 alpine），維持最小執行期受攻擊面。
2. **持續執行前端相依性驗證**：
   前端套件異動必須執行 `python scripts/update-frontend.py`，嚴禁手動置換檔案或引入未經驗證之外部來源。
3. **金鑰持久化與平滑輪替**：
   目前開發階段採用 `AddEphemeralEncryptionKey()`，正式生產環境部署時，應接入本機金鑰檔案或 Azure Key Vault / 硬體安全模組 (HSM)。
