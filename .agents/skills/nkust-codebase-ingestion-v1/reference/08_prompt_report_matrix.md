# 參考手冊 08：Prompt 需求規格與輸出說明文件雙向對齊矩陣 (Prompt-Report Matrix)

## 一、概述

本矩陣旨在建立提示詞規格檔案（`.prompt/*.md`）與對應輸出技術文件（`README/*.md` 及主 `README.md`）之間的精確映射標準。任何執行「讀取專案 skills」的 Agent 或工程師，必須嚴格依據此對照表檢驗輸出的完整性與品質。

---

## 二、Prompt 規格與目標產物全景對照表

| 提示詞來源 (Prompt) | 目標輸出文件 (Target Output) | 目標讀者與核心任務 | 關鍵約束與特別指令 |
| :--- | :--- | :--- | :--- |
| **[.prompt/NKUST-OOSA.md](../../../.prompt/NKUST-OOSA.md)** | **[README/PROJECT_REPORT.md](../../../README/PROJECT_REPORT.md)** | 新進工程師快速掌握系統全貌、SSO 憑證交換機制、跨四模組架構。 | 依序包含概述、核心架構、風險邊際、相依環境，文末 3-5 個重構建議。 |
| **[.prompt/NKUST-OOSA.NKUST_MODEL.md](../../../.prompt/NKUST-OOSA.NKUST_MODEL.md)** | **[README/NKUST_MODEL/NKUST_MODEL_REPORT.md](../../../README/NKUST_MODEL/NKUST_MODEL_REPORT.md)**<br/>與 **[NKUST_MODEL_ENV.md](../../../README/NKUST_MODEL/NKUST_MODEL_ENV.md)** | 領域實體定義層（POCO / Linq2DB）。剖析 `Own/` 與 `Sybase/` 各模組。 | 需繪製 Mermaid 拓撲；ENV 需列出完整 BOM 與子專案相依性。 |
| **[.prompt/NKUST-OOSA.NKUST_SERVICE.md](../../../.prompt/NKUST-OOSA.NKUST_SERVICE.md)** | **[README/NKUST_SERVICE/NKUST_SERVICE_REPORT.md](../../../README/NKUST_SERVICE/NKUST_SERVICE_REPORT.md)**<br/>與 **[NKUST_SERVICE_ENV.md](../../../README/NKUST_SERVICE/NKUST_SERVICE_ENV.md)** | 業務邏輯、SSO 憑證交換、UoW、快取與共用服務。 | 拓撲圖需改為 Mermaid 格式；詳細說明 Admin, Base, Common, Login, Utility。 |
| **[.prompt/NKUST-OOSA.NKUST_VIEWMODEL.md](../../../.prompt/NKUST-OOSA.NKUST_VIEWMODEL.md)** | **[README/NKUST_VIEWMODEL/NKUST_VIEWMODEL_REPORT.md](../../../README/NKUST_VIEWMODEL/NKUST_VIEWMODEL_REPORT.md)**<br/>與 **[NKUST_VIEWMODEL_ENV.md](../../../README/NKUST_VIEWMODEL/NKUST_VIEWMODEL_ENV.md)** | 視圖模型與前後端資料契約（DTOs），解耦實體與前端展示。 | 涵蓋 Admin, Common, Login 子目錄；包含 BOM 表與相依圖。 |
| **[.prompt/NKUST-OOSA.NKUST_WEB.md](../../../.prompt/NKUST-OOSA.NKUST_WEB.md)** | **[README/NKUST_WEB/NKUST_WEB_REPORT.md](../../../README/NKUST_WEB/NKUST_WEB_REPORT.md)**<br/>與 **[NKUST_WEB_ENV.md](../../../README/NKUST_WEB/NKUST_WEB_ENV.md)** | HTTP 進入點、Controllers、Filters、ViewComponents、中介軟體。 | 詳細說明 Admin, Common, Login；列出完整前後端 BOM 與安全屬性。 |
| **[.prompt/NKUST-OOSA-BOM.md](../../../.prompt/NKUST-OOSA-BOM.md)** | **[README/PROJECT_BOM.md](../../../README/PROJECT_BOM.md)**<br/>與 **[PROJECT_BOM_UPGRADE.md](../../../README/PROJECT_BOM_UPGRADE.md)** | 全方案物料清單 (BOM) 與目標 .NET 10 LTS 現代化升級清單。 | 上網比對 .NET 10 支援度，不相容或官方棄用者標註 `Deprecated`。 |
| **[.prompt/NKUST-OOSA-README.md](../../../.prompt/NKUST-OOSA-README.md)** | **[README.md](../../../README.md)** | 全系統權威總說明檔（包含架構圖、安裝、設定、啟動、測試、授權）。 | Mermaid 格式架構圖、流程圖、時序圖；清晰可點擊的檔案連結。 |

---

## 三、模組說明文件標準結構規範 (7 大固定章節)

依據各 Prompt 規定，所有子模組 REPORT 文件均需嚴格維持以下 5 大主幹結構：
1. **【專案/模組整體概述】**：核心設計目的、目錄拓撲（包含各子目錄職責說明）。
2. **【核心架構與檔案/函式解析】**：主要 Class 與 Method 之：
   - 輸入 (Parameters / Input)
   - 輸出 (Return / Output)
   - 核心處理邏輯 (What it does)
3. **【相依性與環境設定】**：獨立抽取為 `*_ENV.md`，包含：
   - 關鍵套件與環境變數清單
   - 前後端詳細技術 BOM (Bill of Materials) 表
   - 子專案運行相依性拓撲圖（Mermaid 格式）
4. **【潛在風險、邊際條件與效能注意事項】**：例外邊界、連線問題、快取併發等。
5. **【重構建議 (Refactoring Recommendations)】**：3-5 個具體的重構改進方向與代碼對比。

---

## 四、台灣繁體中文專業術語標準表

為確保文件專業度與一致性，專案一律採用台灣資訊工程標準習慣用語：

| 簡體 / 大陸用語 | 台灣繁體中文標準詞彙 (Mandatory) |
| :--- | :--- |
| 代码 | **程式碼** (Source Code) |
| 类 | **類別** (Class) |
| 函数 / 方法 | **函式 / 方法** (Function / Method) |
| 参数 | **參數** (Parameters) |
| 返回值 | **回傳值** (Return Value) |
| 接口 | **介面** (Interface) |
| 属性 | **屬性** (Property) / **特性** (Attribute) |
| 依赖注入 | **相依性注入 / 依賴注入** (Dependency Injection) |
| 异步 / 线程 | **非同步 / 執行緒** (Async / Thread) |
| 缓存 | **快取** (Cache) |
| 默认 | **預設** (Default) |
