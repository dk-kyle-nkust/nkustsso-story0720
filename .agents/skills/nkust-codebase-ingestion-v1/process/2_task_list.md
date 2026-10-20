# 「讀取專案 skills」任務執行清單 (Task List)
- `${project_name}`：專案名稱。

## 任務清單中繼資訊
- **專案名稱**：`${project_name}`
- **任務清單檔案**：`2_task_list.md`
- **所屬規劃書**：[1_implementation_plan.md](1_implementation_plan.md)
- **建立日期**：2026-10-16
- **狀態摘要**：已規劃完成，正進行各模組逐步執行與驗證

---

## 零、不可妥協之安全守則檢核 (Security Boundary Guardrail)

- [x] **SEC-01**: 確認所有分析與讀取作業 100% 採取「本機離線靜態原始碼分析 (Air-Gapped Static Analysis)」。
- [x] **SEC-02**: 嚴格確認無任何寫入、修改、連線、DROP、DELETE、UPDATE 指令指向 `Sybase.nkust.edu.tw`。
- [x] **SEC-03**: 嚴格確認無任何封包傳送、端口掃描或異動請求發往 `140.133.*.*` 網段主機。
- [x] **SEC-04**: 在所有自動化腳本與工具程式中加入安全攔截常式（Safety Assertion Guard）。

---

## 一、任務分解結構 (WBS - Work Breakdown Structure)

### 模組 1：專案架構掃描與前置環境安全檢核 (Discovery & Environment)
- [x] **TASK-1.1: 方案結構盤點與檔案拓撲定位**
  - **前置條件**：工作目錄設定於 `${project_name}`。
  - **執行動作**：遍歷方案檔 `${project_name}`.`sln`，確認四大子專案 `${project_name}`.`Model`, `${project_name}`.`Service`, `${project_name}`.`ViewModel`, `${project_name}`.`Web` 之實體路徑與目錄架構。
  - **產出目標**：確立專案分層架構基線。
  - **驗收標準**：準確排除 `bin/` 與 `obj/` 干擾，辨識核心原始碼目錄。

- [x] **TASK-1.2: 資安邊界掃描腳本開發**
  - **前置條件**：PowerShell 5.1+ 與本機檔案讀取權限。
  - **執行動作**：撰寫 `Inspect-SecurityBoundaries.ps1`，主動檢測專案中所有設定檔之連線字串與遠端端點，確保無動態連線行為。
  - **產出目標**：[Inspect-SecurityBoundaries.ps1](../scripts/Inspect-SecurityBoundaries.ps1)。
  - **驗收標準**：腳本能回報所有連線字串參照並通過「禁止異動 Sybase 與 140.133.*.*」合規驗證。

---

### 模組 2：依賴關聯與全方案物料清單 (SBOM / BOM Extraction)
- [x] **TASK-2.1: 後端 NuGet 套件與目標框架自動化萃取**
  - **前置條件**：各專案 `.csproj` 檔案可被 XML / Linq 讀取。
  - **執行動作**：撰寫自動提取邏輯，抽取出 TargetFramework、TargetFrameworks、PackageReference 清冊（包括名稱、版本、私有相依）。
  - **產出目標**：後端 BOM 結構化數據。
  - **驗收標準**：完全涵蓋 `linq2db`, `Autofac`, `AutoMapper`, `NLog`, `ClosedXML`, `Microsoft.Data.SqlClient` 等核心套件。

- [x] **TASK-2.2: 前端客戶端函式庫 (LibMan / CDN) 萃取**
  - **前置條件**：`${project_name}`.`Web/libman.json` 與 `wwwroot` 存在。
  - **執行動作**：解析 `libman.json` 與前端資產，盤點 jQuery 3.7.1, Bootstrap 5.3.3, DataTables 2.2, Select2, SweetAlert2 等。
  - **產出目標**：前端 BOM 結構化清冊。
  - **驗收標準**：涵蓋套件名稱、版本、CDN Provider (cdnjs/jsdelivr) 及功能分類。

- [x] **TASK-2.3: 整合型 BOM 提取腳本建構**
  - **執行動作**：整合前端與後端 BOM 抽取邏輯，輸出為腳本 `Extract-DotnetBOM.ps1`。
  - **產出目標**：[Extract-DotnetBOM.ps1](../scripts/Extract-DotnetBOM.ps1)。
  - **驗收標準**：執行後輸出格式化之 Markdown BOM 表格或 JSON 結構。

---

### 模組 3：.NET 10 LTS 目標相容性與升級比對 (Modernization Analysis)
- [x] **TASK-3.1: .NET 10 相容性與已棄用 (Deprecated) 元件比對**
  - **前置條件**：BOM 清單與 .NET 10 官方升級指南就緒。
  - **執行動作**：比對各套件於 .NET 10 LTS 環境下的相容性狀態，精確標註 `Deprecated`（如 wkhtmltox, 舊版 System.Drawing.Common, Microsoft.AspNet.Mvc 5.2.7 等）。
  - **產出目標**：升級路徑與破壞性變更對策表。
  - **驗收標準**：為所有 Deprecated 套件提供現代化官方/開源替代解法（ImageSharp, PuppeteerSharp, 内建中介軟體）。

- [x] **TASK-3.2: 現代化相容性驗證腳本建置**
  - **執行動作**：撰寫 `Verify-UpgradeCompatibility.ps1`，提供升級相容性快速自動診斷。
  - **產出目標**：[Verify-UpgradeCompatibility.ps1](../scripts/Verify-UpgradeCompatibility.ps1)。
  - **驗收標準**：能正確標記現有套件與目標 .NET 10 規格的差異矩陣。

---

### 模組 4：Roslyn AST 與多層架構深度逆向 (Codebase Ingestion & AST Parsing)
- [x] **TASK-4.1: 靜態語法樹 (AST) 簽章提取工具開發**
  - **前置條件**：.NET 10 SDK 支援，建立 `AstCodeExtractor` C# 控制台工具。
  - **執行動作**：利用 C# Roslyn 語法分析器，遍歷 C# 檔案中的 Class, Method, Property，精確提取：
    - Class Name & Namespace
    - Method Name & Signature
    - Input Parameters
    - Return Types
    - XML Summary Docstring
  - **產出目標**：`../program/AstCodeExtractor/` 專案與原始碼。
  - **驗收標準**：可無警告編譯執行並輸出 JSON / Markdown 結構。

- [x] **TASK-4.2: 各子模組逆向分析與 Prompt 規範對齊**
  - **執行動作**：對照 7 份 Prompt 規格（`${project_name}`.Model, `${project_name}`.Service, `${project_name}`.ViewModel, `${project_name}`.Web, BOM, README），提取各層核心邏輯、資料庫實體映射與身分認證流程。
  - **驗收標準**：確保逆向深度滿足新進工程師快速上手之要求。

---

### 模組 5：Mermaid 視覺化架構建模與拓撲產生 (Visual Modeling)
- [x] **TASK-5.1: 方案依賴與拓撲 Mermaid 生成腳本**
  - **執行動作**：撰寫 `Generate-MermaidTopology.ps1`，分析專案引用關聯與層級，產出標準 Mermaid 程式碼。
  - **產出目標**：[Generate-MermaidTopology.ps1](../scripts/Generate-MermaidTopology.ps1)。
  - **驗收標準**：產出語法乾淨、無解析錯誤、具備層次色彩之 Mermaid 語法。

- [x] **TASK-5.2: 系統流程圖與 SSO 循序圖標準化建模**
  - **執行動作**：將 IdP / SP 代登流程、60 秒動態票券、Claims Cookie 交換繪製為標準 Mermaid 時序圖。
  - **驗收標準**：精確對應 `SignService.cs` 與 `AccountController.cs` 程式碼邏輯。

---

### 模組 6：參考手冊與標準指引建置 (Reference Documentation)
- [x] **TASK-6.1: 撰寫 8 份核心參考指南於 `../reference/`**
  - `01_security_boundary_rules.md`：零信任資安防衛與 Sybase / 140.133.*.* 禁動規範
  - `02_codebase_ingestion_guide.md`：專案程式碼結構化讀取與逆向架構方法論
  - `03_bom_and_sbom_standard.md`：軟體物料清單 (BOM) 與供應鏈安全標準
  - `04_dotnet10_upgrade_eval.md`：.NET 10 LTS 升級評估與 Deprecated 套件判準
  - `05_roslyn_ast_parsing_spec.md`：C# 靜態 AST 語法樹與程式符號提取規格
  - `06_mermaid_diagram_specs.md`：架構圖、流程圖、時序圖與拓撲圖 Mermaid 繪製規範
  - `07_code_smell_refactor_sop.md`：潛在風險、邊際條件、Code Smells 偵測與重構 SOP
  - `08_prompt_report_matrix.md`：Prompt 需求規格與輸出說明文件雙向對齊矩陣
  - **產出目標**：`../reference/*.md`。
  - **驗收標準**：涵蓋所有分析技術細節與規範。

---

### 模組 7：核心技能手冊與輔助工具程式建構 (Skills Manual & Program)
- [x] **TASK-7.1: 產出主要「讀取專案 skills」說明手冊 `SKILL.md`**
  - **產出目標**：[SKILL.md](../SKILL.md)。
  - **驗收標準**：包含標準 YAML frontmatter、觸發條件、工作流程、安全檢查、產出範例。

- [x] **TASK-7.2: 產出分析工具程式於 `program/`**
  - **產出目標**：`../program/` 下完整 C# 專案與 BOM 驗證器。
  - **驗收標準**：可直接使用 `dotnet run` 執行並通過驗證。

---

### 模組 8：端到端整合測試與 Walkthrough 紀錄 (Execution & Walkthrough)
- [x] **TASK-8.1: 執行腳本與程式實測**
  - **執行動作**：於本機環境執行各腳本與程式，驗證執行結果無錯誤。
  - **驗收標準**：產出結果正確，未發起任何非法網路或資料庫異動請求。

- [x] **TASK-8.2: 撰寫執行過程紀錄 `3_walkthrough.md`**
  - **產出目標**：[3_walkthrough.md](../process/3_walkthrough.md)。
  - **驗收標準**：詳實記錄執行步驟、指令、輸出截圖、檢核結果與最終驗收確認。
