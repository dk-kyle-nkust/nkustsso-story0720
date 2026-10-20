# NKUST_MODEL 模組快速導覽手冊 (Submodule README)

- **所屬方案**：NKUST SSO Server
- **模組定位**：領域模型、資料契約、linq2db ORM 與資料庫工廠
- **目錄路徑**：`NkustSso.Server/Models/` 及 `NkustSso.Server/ApiModels/`

---

## 模組核心架構

1. **實體分類**：
   - 自有儲存實體：`Account`, `LoginSession`, `LoginTicket`, `RegisteredSystem`, `AdminManagement`。
   - 外部校務唯讀實體：`CampusCredentialRecord`, `EmployeeRecord`, `StudentRecord`。
2. **連線分流**：
   - `SsoStorage`: 自有業務儲存，支援 SQL Server 與 SQLite。
   - `ICampusDatabaseFactory`: 隔離校務 MSSQL，杜絕修改。

## 相關技術報告導覽

- 📄 **[NKUST_MODEL_REPORT.md](NKUST_MODEL_REPORT.md)**：包含完整 AST 類別/函式解析、實體關聯圖與重構建議。
- 📦 **[NKUST_MODEL_ENV.md](NKUST_MODEL_ENV.md)**：包含關鍵環境變數與 NuGet 相依清單。
