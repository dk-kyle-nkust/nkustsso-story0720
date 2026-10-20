# NkustSSO 專案慣例

校園 SSO 原型：ASP.NET Core 10、OpenIddict、linq2db、SQL Server／SQLite。自有 SQL Server 使用 `ConnectionStrings:Sso` 與 `NkustSSO`，校務 `LoginAuth` 維持唯讀。背景、離線匯入與測試見 [README](README.md)。

## 前端資源一律放本機，不要用 CDN

`Program.cs` 的 CSP 是 `default-src 'self'; style-src 'self'`，外部來源的 JS 與 CSS 會被瀏覽器直接擋下。**不要為了掛 CDN 而放寬 CSP**——這是認證伺服器，CSP 是防線不是障礙。

- 第三方前端套件下載到 `NkustSso.Server/wwwroot/lib/`，以相對路徑 `/lib/<檔名>` 引用。
- 自己寫的樣式與腳本放 `wwwroot/` 根層（`site.css`、`portal.css`、`admin-ui.css`、`portal.js`、`admin-ui.js`）。
- 不要用 inline `<script>`、`<style>` 或 `style="..."` 屬性，一樣會被 CSP 擋。用 class 搭配外部樣式表。
- 加新套件前先確認它不會注入 `<style>` 元素或設定 style 屬性；用 CSSOM（`el.style.x = ...`）設樣式才過得了 CSP。
- 加完務必在瀏覽器實測：攔 `securitypolicyviolation` 事件確認違規數為 0，不能只看畫面正常。

目前已安裝：jQuery 4.0.0、DataTables 3.1.4、Tom Select 2.6.2。套件採最新穩定版；更新時使用官方套件庫核對版本並執行相容性測試。本機前端資源版本與完整性見 `wwwroot/lib/versions.json`，更新腳本為 `scripts/update-frontend.py`。

## 表格與下拉

管理頁的表格用 DataTables、下拉用 Tom Select，由 `wwwroot/admin-ui.js` 統一初始化：

- 表格加 `data-datatable` 即自動套用。`data-page-length` 設每頁筆數（預設 25），`data-no-sort="2,6"` 指定不排序的欄位序號（0 起算），例如「操作」「內容」這類欄位，`data-paging="false"` 給一頁放得下的短參考表用，省掉分頁雜訊。
- 下拉加 `data-enhance`。選項少於 9 個時自動不顯示搜尋框。
- 資源只在管理頁載入，透過 `@section head` / `@section scripts` 引用 `_AdminStyles` 與 `_AdminScripts` 兩個 partial。入口首頁不載入，不必背這 265 KB。

**搜尋只留一套。** 表格內的關鍵字搜尋交給 DataTables，伺服器端只保留會改變查詢結果集的條件（登入方式、狀態、分類等下拉）。不要同時放兩個搜尋框。

## 入口首頁的分類

分類只放業務領域。「是否已串接 SSO」是系統狀態、「常用」是個人清單，兩者都不是分類，不要混進分類按鈕列。無法歸類的放「綜合服務」。設計理由見 [入口首頁設計](docs/portal-design.md)。

使用者的個人偏好（常用釘選、最近使用）只存瀏覽器 `localStorage`，**不寫資料庫**。

## 其他頁面的定位

`/integration`（整合資訊）是**開發紀錄**，給自己追蹤實作進度用，不是給外部串接窗口或廠商看的文件。保持「已實作／待實作」的狀態清單即可，不需要為了外部讀者重新編排或分拆。

## 測試

Server 不建立示範資料。測試資料只放在 `tests/NkustSso.StorageChecks/ServerTestData.cs`；瀏覽器測試自行啟動隔離 Server，並停用校務連線，不需開發服務或用戶端。

```powershell
dotnet build NkustSSO.slnx
dotnet run --project tests/NkustSso.StorageChecks --no-build
python tests/e2e.py
python tests/sessions.py
```

程式改完要先停服務再建置，執行中的程序會鎖住 `bin\Debug\net10.0\*.exe`，Visual Studio 偵錯中也一樣。**服務一律由 Visual Studio 啟停**，偵錯中的程序名稱是 `NkustSso.Server.exe`（不是 `dotnet.exe`）；命令列用 `dotnet run --project NkustSso.Server` 啟的按 Ctrl+C 停。建置被鎖住時先查出是哪個程序佔用再處理，不要直接 `Stop-Process` 掃掉所有 dotnet 程序。

## 程式碼排版與寫法

排版規則集中在根目錄 `.editorconfig`，換行固定 LF（由 `.gitattributes` 鎖住，不要拿掉，否則 `core.autocrlf` 會和 editorconfig 互相覆寫造成整包假異動）。

```powershell
dotnet format whitespace NkustSSO.slnx --include NkustSso.Server/Services/ NkustSso.Server/Controllers/
dotnet format style NkustSSO.slnx --include NkustSso.Server/Services/ NkustSso.Server/Controllers/ --diagnostics IDE0011 IDE0022 --severity info
```

`TreatWarningsAsErrors` 是開的，所以 `.editorconfig` 的風格規則一律壓在 `suggestion`，不要調成 `warning`。

**Services 與 Controllers 以「一般人讀得懂」為準，不要為了精簡而壓縮**：

- 單行 `if` 也要加大括號，一行一個敘述，不要把判斷式和動作擠在同一行。
- 一行上限 120 字元。`.Set(...)` 鏈一行一個，方法參數超過就一行一個。
- 長條件拆成逐項 `if` 或具名的 `bool` 區域變數，不要串一長串 `&&`／`||`。
- 方法做太多事就拆小方法，不要靠註解分段。
- 魔術數字改成具名常數（逾時、長度上限、筆數上限）。
- 多行的 `=>` 箭頭成員改回一般方法主體；一行寫得完的才保留 `=>`。

## 其他

- 註解與 commit message 用繁體中文，說明「為什麼」而不是「做什麼」。
- 資料表新增欄位要在 `SsoDatabase.AddColumns` 補 `ALTER TABLE`，`CreateIfNotExists` 不會改既有資料表。
- 改既有資料的預設值時以舊值為條件更新，避免蓋掉管理者後來的調整。
