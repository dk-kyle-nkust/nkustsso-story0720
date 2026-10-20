# 參考手冊 06：架構圖、流程圖、時序圖與拓撲圖 Mermaid 繪製規範 (Mermaid Diagram Specs)
- `${project_name}`：專案名稱。

## 一、概述與視覺化原則

在「讀取專案 skills」中，技術文件的文字描述必須輔以高品質、標準化的視覺化圖表。本規範強制採用 **Mermaid.js** 作為統一的圖表繪製語法：
1. **純文字版本控制**：與 Markdown 檔案原生整合，能隨程式碼提交並清楚顯示 Git Diff。
2. **免外掛自動渲染**：GitHub、GitLab、VS Code、Azure DevOps 原生支援。
3. **消除歧義**：將文字難以描述的複雜非同步流程、跨資料庫交互與 SSO 憑證交換以清晰的時序與拓撲呈現。

---

## 二、Mermaid 繪圖三大禁忌與防錯守則 (Syntax Guardrails)

> [!WARNING]
> **常見 Mermaid 語法解析錯誤防範：**
> 1. **節點標籤字串跳脫**：節點名稱若包含括號 `()`、方括號 `[]`、引號 `""` 或冒號 `:`，**必須使用雙引號包覆**。例如：`NodeA["SignService.VerifyTicket(string ticket)"]`，嚴禁寫成 `NodeA[SignService.VerifyTicket(string ticket)]`。
> 2. **禁用 HTML 複雜標籤**：避免在標籤內插入複雜 HTML（如 `<font>`, `<table>`），換行統一使用 `<br/>`。
> 3. **保留字衝突**：避免使用 Mermaid 關鍵字（如 `end`, `subgraph`, `graph`）作為節點 ID。

---

## 三、標準圖表類型與樣板庫

### 3.1 方案分層架構圖樣板 (System Architecture Diagram)
適用於 `README.md` 與 `PROJECT_REPORT.md`，表達展示層、服務層、實體層與雙資料庫的職責邊界：

```mermaid
graph TD
    classDef client fill:#e0f7fa,stroke:#00838f,stroke-width:2px,color:#004d40;
    classDef web fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#bf360c;
    classDef service fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:#1b5e20;
    classDef model fill:#e1f5fe,stroke:#0277bd,stroke-width:2px,color:#01579b;
    classDef db fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px,color:#4a148c;

    subgraph ClientTier ["客戶端展示層"]
        Browser["終端瀏覽器 (Browser / SPA / Portal iframe)"]:::client
    end

    subgraph PresentationTier ["Web 展示層 (NKUST.Web - .NET 5.0)"]
        Controllers["Controllers (Account, Home, Plugin, Menu)"]:::web
        Filters["Attributes (Permission, ValidateToken)"]:::web
        DI["Autofac 依賴注入容器"]:::web
    end

    subgraph ServiceTier ["領域業務層 (NKUST.Service)"]
        SignService["SignService (SSO 憑證交換)"]:::service
        AdminService["Admin Services (Menu, Role, Embed)"]:::service
        CommonService["Common Services (Student, Unit, Yms)"]:::service
        BaseRepo["GenericRepository / UoW"]:::service
    end

    subgraph ModelTier ["領域模型層 (NKUST.Model / NKUST.ViewModel)"]
        VM["ViewModels (LoginVM, UserVM, DTOs)"]:::model
        POCO_Own["Own Models (SysMenu, SysRole)"]:::model
        POCO_Syb["Sybase Models (s10, s30, s90)"]:::model
    end

    subgraph PersistenceTier ["跨雙資料庫持久化層"]
        SQLServer[("自建庫: SQL Server 2019<br/>(選單、角色、權限)")]:::db
        SybaseASE[("校務庫: Sybase ASE 16.0+<br/>(帳號主檔、SSO 日誌)")]:::db
    end

    Browser -->|HTTPS GET/POST| Controllers
    Controllers --> Filters
    Controllers --> DI
    Controllers --> SignService
    Controllers --> AdminService
    Controllers --> CommonService
    SignService --> BaseRepo
    AdminService --> BaseRepo
    CommonService --> BaseRepo
    BaseRepo --> POCO_Own
    BaseRepo --> POCO_Syb
    POCO_Own -->|Linq2DB| SQLServer
    POCO_Syb -->|Linq2DB| SybaseASE
```

---

### 3.2 SSO 憑證交換時序圖樣板 (SSO Sequence Diagram)
精確還原校務系統受代登（SP 模式）之動態票券校驗與身分宣告流程：

```mermaid
sequenceDiagram
    autonumber
    actor User as 使用者 (瀏覽器)
    participant Skytek as 校務 Portal (IdP)
    participant Web as NKUST.Web (SP)
    participant Sign as SignService
    participant Sybase as Sybase DB (s90_auth_log)
    participant SQLServer as SQL Server (SysUser)

    User->>Skytek: 點擊校務門戶連結
    Skytek->>Skytek: 生成 60 秒一次性票券 (rand_num)
    Skytek->>Sybase: 寫入代登日誌 (s90_auth_log)
    Skytek->>Web: POST /Account/SignLogin (附帶 rand_num 票券)
    
    activate Web
    Web->>Sign: VerifySignTicketAsync(rand_num)
    activate Sign
    Sign->>Sybase: 查詢 s90_auth_log 是否存在且於 60 秒有效時間內
    Sybase-->>Sign: 回傳驗證成功
    Sign->>SQLServer: 查詢 SysUser 是否具備額外管理員權限
    SQLServer-->>Sign: 回傳角色權限
    Sign->>Sign: 簽發 ClaimsIdentity (帳號、學號、角色)
    Sign-->>Web: 回傳認證宣告主體 (ClaimsPrincipal)
    deactivate Sign

    Web->>User: 寫入加密 Cookie (.AspNetCore.Cookies)
    Web-->>User: 302 重新導向至首頁或指定嵌入功能
    deactivate Web
```

---

### 3.3 方案依賴關聯拓撲 (Dependency Topology)
適用於 `*_ENV.md` 文件，表達方案內專案的單向引用關係：

```mermaid
graph TD
    classDef host fill:#fff3e0,stroke:#e65100,stroke-width:2px;
    classDef lib fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px;

    Web["${project_name}.Web (.NET 5.0 Web 宿主)"]:::host
    Service["${project_name}.Service (.NET Standard 2.0/2.1 業務邏輯)"]:::lib
    ViewModel["${project_name}.ViewModel (.NET Standard 2.0/2.1 契約層)"]:::lib
    Model["${project_name}.Model (.NET Standard 2.0/2.1 實體層)"]:::lib

    Web -->|Project Ref| Service
    Web -->|Project Ref| ViewModel
    Web -->|Project Ref| Model
    Service -->|Project Ref| ViewModel
    Service -->|Project Ref| Model
    ViewModel -.->|型別引用| Model
```
