# NKUST_WEB 模組快速導覽手冊 (Submodule README)

- **所屬方案**：NKUST SSO Server
- **模組定位**：HTTP 請求進入點、MVC 控制器、中介軟體管道、Razor 視圖與前端資產
- **目錄路徑**：`NkustSso.Server/Controllers/`, `Views/`, `wwwroot/`

---

## 模組核心架構

1. **端點分類**：
   - 認證端點：`AccountLoginController`, `OidcAuthorizationController`, `SamlSsoController`, `WebApiSsoController`。
   - 整合驗證端點：`CredentialVerificationApiController` (SOAP / JSON)。
   - 專區瀏覽端點：`PortalHomeController`, `PortalServicesController`。
   - 後台管理端點：`Admin*Controller`, `SsoSystemManagementController`, `LoginSessionManagementController`。
2. **資安邊界防衛**：
   - 強制 CSP 政策（`default-src 'self'`、`frame-ancestors 'none'`）。
   - 登入與查驗端點速率限制 (`RateLimiter`)。
   - 外部靜態資源 100% 本地化離線提供（`wwwroot/lib/`）。

## 相關技術報告導覽

- 📄 **[NKUST_WEB_REPORT.md](NKUST_WEB_REPORT.md)**：包含完整 AST 類別/函式解析、中介軟體流程與重構建議。
- 📦 **[NKUST_WEB_ENV.md](NKUST_WEB_ENV.md)**：包含前端 SRI 完整性雜湊清單與管線拓撲圖。
