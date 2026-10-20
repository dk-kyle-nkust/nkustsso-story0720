# NKUST_VIEWMODEL 模組快速導覽手冊 (Submodule README)

- **所屬方案**：NKUST SSO Server
- **模組定位**：強型別前後端資料契約、表單模型與 DataAnnotations 驗證
- **目錄路徑**：`NkustSso.Server/ViewModels/`

---

## 模組核心架構

1. **核心模型分類**：
   - 登入與會話：`LoginViewModel`, `CurrentUserViewModel`, `LoginSessionListViewModel`。
   - 身分專區：`PortalDirectoryViewModel`, `PortalServiceItemViewModel`, `PortalCategoryEditViewModel`。
   - 系統登記：`SsoSystemEditViewModel`, `IntegrationClientEditViewModel`。
   - 協定傳輸：`SamlResponseFormViewModel`。

## 相關技術報告導覽

- 📄 **[NKUST_VIEWMODEL_REPORT.md](NKUST_VIEWMODEL_REPORT.md)**：包含完整 AST 類別/屬性解析、安全注意事項與重構建議。
- 📦 **[NKUST_VIEWMODEL_ENV.md](NKUST_VIEWMODEL_ENV.md)**：包含相依組件與拓撲關係圖。
