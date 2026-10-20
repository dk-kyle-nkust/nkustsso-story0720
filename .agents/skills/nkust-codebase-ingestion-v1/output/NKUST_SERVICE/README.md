# NKUST_SERVICE 模組快速導覽手冊 (Submodule README)

- **所屬方案**：NKUST SSO Server
- **模組定位**：SSO 協定核心、校務身分驗證、入口可見度過濾與舊版 SOAP 相容分派
- **目錄路徑**：`NkustSso.Server/Services/`

---

## 模組核心架構

1. **協定引擎**：
   - OIDC: `LinqApplicationStore`, `LinqTokenStore`, `LinqScopeStore`。
   - SAML 2.0: `SamlIdentityProvider`。
   - Web API Ticket: `LoginTicketService` (60 秒動態票券、PKCE、一次性兌換)。
   - SOAP: `LegacySoapOperationDispatcher` (相容 `/KuasSSOWebService.asmx`)。
2. **安全會話與權限**：
   - `LoginSessionLifetimeService`, `LoginSessionStore`: 中央 Session 管理。
   - `AdminAuthorizationService`: 後台動態功能代碼查核。
   - `PortalVisibility`: 身分專區選單過濾。

## 相關技術報告導覽

- 📄 **[NKUST_SERVICE_REPORT.md](NKUST_SERVICE_REPORT.md)**：包含完整 AST 類別/函式解析、時序圖與重構建議。
- 📦 **[NKUST_SERVICE_ENV.md](NKUST_SERVICE_ENV.md)**：包含關鍵環境變數與安全組態清單。
