# 參考手冊 04：.NET 10 LTS 升級評估與 Deprecated 套件判準 (Modernization Guide)

## 一、升級背景與核心目標

NKUST-OOSA 專案之 Web 展示層目前建置於 **.NET 5.0**（Microsoft 官方已於 2022 年 5 月宣告 EOL 終止支援）。當進行專案讀取與現代化升級分析時，目標為全面推進至 **.NET 10 LTS (Long-Term Support)**。

升級至 .NET 10 將獲得：
1. **極致執行效能**：動態設定檔引導最佳化（Dynamic PGO）、向量化處理、零配置 Native AOT。
2. **現代 C# 語法**：支援 C# 14 / C# 13 特性（增強型 Pattern Matching、Primary Constructors、Collection Expressions、Inline Arrays）。
3. **安全防禦加固**：內建現代化的 CSRF、SameSite Cookie 機制、TLS 1.3 優先支援與即時弱點修補。

---

## 二、已棄用 (Deprecated) 元件判定四大準則

在「讀取專案 skills」中，任何現有依賴項若符合以下任一條件，必須於升級 BOM 表中明確標註為 **`Deprecated`**，並提供官方或業界推薦之替代方案：

```
準則 1: 官方 EOL (End of Life) ── 專案或上游組織已正式宣布終止維護，無後續安全修補。
準則 2: 跨平台阻斷 (Cross-Platform Incompatibility) ── 依賴非託管二進位或 Windows 專屬 API，無法於 Linux 容器運作。
準則 3: 框架內建吸收 (Framework Absorption) ── 該功能已被現代 ASP.NET Core 10 原生模組納入，外掛套件已多餘。
準則 4: 存在重大架構破壞性變更 (Architectural Breaking Changes) ── 新版 API 存在不相容重構，舊用法被廢除。
```

---

## 三、NKUST 核心元件現代化升級比對矩陣

| 現有套件 / 元件 | 現有版本 | .NET 10 目標版本 | 狀態標註 | 官方替代方案與升級處置說明 |
| :--- | :--- | :--- | :---: | :--- |
| **目標執行環境** | .NET 5.0 (net5.0) | **.NET 10.0 (net10.0)** | 核心升級 | 全方案統一升級至 .NET 10 LTS，獲得長期微軟支援。 |
| **類別庫標準** | netstandard2.0/2.1 | **net10.0 (或保留 2.0)** | 核心升級 | 移除 `netstandard2.1`，統一以 `net10.0` 編譯以解鎖最新 C# 語法。 |
| **PDF 轉檔工具** | `wkhtmltox` (native x64) | **PuppeteerSharp 20.x+** | **`Deprecated`** | `wkhtmltopdf` 依賴過時之 WebKit 核心且維護停止。替代方案為以無頭 Chromium (PuppeteerSharp) 進行 HTML 轉 PDF，支援現代 CSS Flexbox / Grid。 |
| **影像處理庫** | `System.Drawing.Common 5.0.2` | **SixLabors.ImageSharp 3.1+** | **`Deprecated`** | 微軟自 .NET 7 起正式將 System.Drawing 標記為 Windows-Only。Linux 容器下會拋出異常。替換為純託管 ImageSharp。 |
| **舊版 MVC 套件** | `Microsoft.AspNet.Mvc 5.2.7` | **ASP.NET Core 10 內建** | **`Deprecated`** | 舊版相依誤引入了 ASP.NET Framework 舊組件，直接移除並依賴 `Microsoft.AspNetCore.App` 框架內建 MVC。 |
| **Cookie 驗證套件** | `Microsoft.AspNetCore.Authentication.Cookies 2.2.0` | **ASP.NET Core 10 內建** | **`Deprecated`** | 獨立 NuGet 2.2.0 版已廢棄。現代 .NET 已將 Cookie 驗證中介軟體直接整合於共用框架（Shared Framework）中。 |
| **ORM 引擎** | `linq2db 2.9.8` | **linq2db 6.0+** | 建議升級 | 全面支援現代 LINQ 查詢管道，最佳化 SQL 轉譯效率與連線集區安全。 |
| **Sybase 驅動** | `AdoNetCore.AseClient 0.19.2` | **AdoNetCore.AseClient 0.19.5+** | 審慎升級 | 需確保在保持純靜態解析的前提下，升級驅動以相容 .NET 10 Socket 管道。 |
| **IoC 容器** | `Autofac 7.1.0` | **Autofac 11.0.2+** | 版本升級 | 升級以配合 Microsoft.Extensions.DependencyInjection 10.0。 |
| **物件映射** | `AutoMapper 10.1.1` | **AutoMapper 16.2.0+** | 版本升級 | 適配靜態型別編譯與委派最佳化。 |
| **日誌框架** | `NLog 5.2.3` | **NLog 6.0+** | 版本升級 | 提升非同步資料庫寫入效率，支援現代結構化日誌格式。 |

---

## 四、前端客戶端現代化升級指南

前端元件在升級至現代標準時，應依據現代瀏覽器與無 jQuery 依賴趨勢進行漸進式重構：

1. **DataTables 2.2 -> 3.x 生態系**：
   - 採用現代 ES 模組引入或更新至最新 LTS 版，支援原生的響應式控制與深色主題。
2. **Moment.js (已處於維護模式) -> Day.js 或原生 Temporal API**：
   - Moment.js 官方已建議停止在新專案中使用，體積過大且不支持 Tree-Shaking。應升級為 Day.js (僅 2KB) 以大幅提升頁面載入速度。
3. **AdminLTE 4 與 Bootstrap 5.3+**：
   - 汰除舊版 jQuery 插件，擁抱原生 Bootstrap 5 JavaScript API，降低對全域變數與 jQuery 的過度耦合。
