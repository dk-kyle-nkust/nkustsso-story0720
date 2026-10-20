# 參考手冊 02：專案程式碼結構化讀取與逆向架構方法論 (Codebase Ingestion Guide)
- `${project_name}`：專案名稱。

## 一、概述與核心哲學

在大型軟體專案（尤其是結合舊系統與現代架構之企業級專案）中，原始碼往往是唯一未隨時間失真的「絕對真理來源 (Single Source of Truth)」。然而，直接通讀數萬行原始碼會引發認知超載 (Cognitive Overload) 與上下文視窗耗盡。「讀取專案 skills」的核心方法論即為**分層次、結構化、漸進式逆向工程 (Hierarchical & Progressive Architecture Recovery)**。

---

## 二、五層漸進式逆向解析模型 (The 5-Level Ingestion Pyramid)

```
              ▲
             / \
            / L5 \   Presentation & Web API (Controllers, Views, Areas)
           /------\
          /   L4   \  Domain & Business Logic (Services, DTOs, Business Rules)
         /----------\
        /     L3     \ Cross-Cutting & Infrastructure (Base, DI, DBContext, Filters)
       /--------------\
      /       L2       \ Configuration & Bootstrap (Startup, Program, Settings)
     /------------------\
    /         L1         \ Solution Topology & BOM (Projects, Targets, Packages)
   /----------------------\
```

### Level 1: 方案拓撲與物料清單 (Solution Topology & BOM)
- **目標**：掌握整體架構骨架與邊界。
- **讀取對象**：[`${project_name}`.`sln`](../../../${project_name}.sln)、各子專案之 `.csproj`、`libman.json`。
- **關鍵提取項**：
  - 專案名稱、目標框架（如 `netstandard2.0;netstandard2.1` 與 `net5.0`）。
  - 專案間的相依性方向（例如：Web -> Service -> Model & ViewModel）。
  - 第三方套件（NuGet）與前端資源（LibMan/CDN）的精確版本與職責。

### Level 2: 組態與啟動管線 (Configuration & Bootstrap)
- **目標**：理解應用程式生命週期、中介軟體順序與相依注入規則。
- **讀取對象**：`Startup.cs`、`Program.cs`、`appsettings.json`、`nlog.config`。
- **關鍵提取項**：
  - HTTP 請求中介軟體管線順序（Authentication -> Authorization -> Routing -> StaticFiles -> Custom Middleware）。
  - 控制反轉（IoC）容器註冊：Autofac 的 `RegisterAssemblyTypes` 與各服務之生命週期（Transient / Scoped / Singleton）。
  - 環境變數與連線字串組態策略。

### Level 3: 橫切基礎設施與資料存取 (Infrastructure & Data Access)
- **目標**：理解資料如何存取、認證如何攔截、快取如何運作。
- **讀取對象**：`${project_name}`.`Service/Base/*`、`${project_name}`.`Web/Attributes/*`、`${project_name}`.`Service/Utility/*`。
- **關鍵提取項**：
  - ORM 存取機制：`SqlDbContext` 與 `SybaseDbContext` 之實例化與連線池管理。
  - 工作單元（Unit of Work）與泛型倉儲（`GenericRepository<TEntity>`）實作。
  - 安全過濾器：`PermissionAttribute`（RBAC 授權檢核）與 `CustomerValidateAntiForgeryToken`（針對 Portal iframe 代登處理 CSRF）。
  - 快取策略：`BetterCacheManager` 運作機制與快取過期控制。

### Level 4: 領域實體與業務邏輯 (Domain Models & Business Logic)
- **目標**：剖析業務規則與資料庫實體映射。
- **讀取對象**：`${project_name}`.`Model/*`、`${project_name}`.`Service/*`（Admin, Login, Common）、`${project_name}`.`ViewModel/*`。
- **關鍵提取項**：
  - `${project_name}`.`Model/Own/`：自建系統實體（選單 `SysMenu`、角色 `SysRole`、外掛代登 `SysPlugin`、嵌入對照 `SysEmbed`）。
  - `${project_name}`.`Model/Sybase/`：校務系統實體（s10 人事、s30 學籍、s31 成績、s41~s49 學務、s90 權限公用）。
  - SSO 核心業務：`SignService` 處理 60 秒票券驗證、Cookie 宣告組裝與代登跳轉邏輯。

### Level 5: 展示層與控制器 (Presentation & Web API)
- **目標**：理解前端請求進入點、視圖渲染與非同步通訊。
- **讀取對象**：`${project_name}`.`Web/Controllers/*`、`${project_name}`.`Web/Areas/Admin/*`、`${project_name}`.`Web/Components/*`、`Views/*`。
- **關鍵提取項**：
  - Action 方法的輸入參數、回傳型別（`IActionResult`, `JsonResult`）。
  - 動態導覽列元件：`MenuViewComponent` 如何依據目前使用者權限動態呈現側邊欄選單。
  - AJAX 交互與前端 DataTables 渲染契約。

---

## 三、避開干擾雜訊之規則 (Noise Reduction Guidelines)

在讀取專案時，必須透過腳本或篩選規則自動排除下列不具備架構知識的暫存與二進位檔案：
1. **編譯輸出**：`bin/` 與 `obj/` 資料夾（包含 `.dll`, `.pdb`, `.cache`, `.props` 等）。
2. **IDE 專屬快取**：`.vs/`、`.vscode/`（除非包含自訂 launch.json 執行參數）。
3. **前端建置產物與大型靜態套件庫**：`node_modules/`、`wwwroot/lib/`（這些資產由 `libman.json` 或 `package.json` 的定義檔逆向即可，無需逐行讀取其二進位或打包後的 min.js 程式碼）。
4. **臨時檔案與備份**：`*.tmp`, `*.bak`, `~*`。

---

## 四、架構逆向提煉輸出樣板 (Architecture Extraction Schema)

針對任何模組進行逆向提取時，一律依循統一之標準格式記錄：

```markdown
### 模組/類別名稱：[ClassName](file:///path/to/Class.cs)
- **設計目的**：[一句話精準概括其職責]
- **所屬層級**：[Presentation / Business Service / Domain Model / Infrastructure]
- **核心公開方法**：
  1. `MethodName(ParamType param1, ParamType param2)`
     - **輸入 (Input)**：[參數用途與型別說明]
     - **輸出 (Output)**：[回傳型別與內容]
     - **處理邏輯 (What it does)**：[核心演算法、異動或查詢步驟]
     - **相依與副作用**：[呼叫了哪個外部服務或資料庫表]
- **潛在風險與注意事項**：[邊際條件、防禦性程式碼缺漏]
```
