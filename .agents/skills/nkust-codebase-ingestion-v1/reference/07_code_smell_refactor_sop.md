# 參考手冊 07：程式碼壞味道偵測與重構建議指引 (Code Smell & Refactoring SOP)

## 一、概述與審查目標

在「讀取專案 skills」中，逆向分析的價值不僅在於理解「現狀是什麼」，更在於洞察「系統潛在隱患在哪裡」以及「如何重構成現代化架構」。依據專案 Prompt 之要求，每份核心模組報告均需列出**潛在風險、邊際條件（Edge Cases）、效能注意事項**，並在文末提供 **3-5 個具體、具備代碼範例的重構建議**。

---

## 二、NKUST 專案常見潛在風險與邊際條件清冊

### 2.1 資料庫與連線邊界風險
- **Sybase 編碼中文字截斷**：
  - *邊際狀況*：Sybase 連線字串若漏設 `CharSet=utf8`，當姓名包含罕見字或長字串時，Sybase 會採用預設 Codepage 導致儲存或查詢時中文字元截斷或亂碼。
  - *防禦策略*：在基礎連線工廠強制校驗連線參數並置入健全檢查。
- **資料庫連線集區（Connection Pool）枯竭**：
  - *邊際狀況*：在高併發選課或選單載入時，若 Repository 未妥善釋放 `DataConnection` 或生命週期過長，將耗盡資料庫連線數。

### 2.2 SSO 與安全憑證邊界風險
- **動態票券重送攻擊 (Ticket Replay Attack)**：
  - *邊際狀況*：代登票券（`rand_num`）雖設定 60 秒有效期，若未在首次成功校驗後立即將 `s90_auth_log` 該筆記錄標記為已核銷（Redeemed），在 60 秒內可被惡意截獲重複使用。
  - *防禦策略*：採原子化（Atomic）或單次更新標記，確保票券「一經讀取即刻失效」。
- **跨站請求偽造 (CSRF) 與 iframe 嵌入衝突**：
  - *邊際狀況*：學校 Portal 透過 `<iframe>` 內嵌本系統，若 SameSite Cookie 設為 `Strict`，外部發起的 POST 代登將遺失 Cookie；若關閉 Antiforgery 則有資安風險。
  - *防禦策略*：自訂 `CustomerValidateAntiForgeryToken`，針對白名單 IdP 來源 IP 採用簽章雜湊校驗，兼顧體驗與安全。

---

## 三、經典 Code Smells 與現代化重構 SOP

### 重構建議 1：消除 Service Locator 反模式，全面回歸建構子依賴注入 (Constructor DI)
- **問題現狀 (Code Smell)**：
  專案中部分類別透過全域靜態類別 `StaticServicesHelper.GetService<T>()` 取得相依實例，破壞了依賴關係的可見性，且在單元測試時無法 Mock。
- **重構對比**：
  ```csharp
  // 🔴 Before: 靜態服務定位器 (難以測試、隱蔽相依)
  public class MenuService : BaseService
  {
      public void LogAction(string message)
      {
          var logger = StaticServicesHelper.GetService<ILogger<MenuService>>();
          logger.LogInformation(message);
      }
  }

  // 🟢 After: 明確的建構子注入 (Clean Architecture)
  public class MenuService : BaseService
  {
      private readonly ILogger<MenuService> _logger;
      public MenuService(IDBUnitOfWork uow, IMapper mapper, ILogger<MenuService> logger) 
          : base(uow, mapper)
      {
          _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      }
  }
  ```

### 重構建議 2：消除同步阻塞非同步 (.Result / .Wait())，防止執行緒集區死鎖
- **問題現狀 (Code Smell)**：
  部分 Service 或 Controller 在呼叫非同步方法時使用 `.Result` 或 `.GetAwaiter().GetResult()`，在高負載下會耗盡 ASP.NET Core ThreadPool 造成請求逾時與死鎖。
- **重構對比**：
  ```csharp
  // 🔴 Before: 同步阻塞 (Sync-over-Async)
  public UserViewModel GetUserInfo(string account)
  {
      var user = _studentService.GetStudentAsync(account).Result; // 潛在 Deadlock
      return _mapper.Map<UserViewModel>(user);
  }

  // 🟢 After: 端到端純非同步管道 (Pure Async/Await)
  public async Task<UserViewModel> GetUserInfoAsync(string account, CancellationToken ct = default)
  {
      var user = await _studentService.GetStudentAsync(account, ct);
      return _mapper.Map<UserViewModel>(user);
  }
  ```

### 重構建議 3：ORM 查詢加入分頁與投影，避免記憶體暴增 (Out of Memory)
- **問題現狀 (Code Smell)**：
  查詢資料表時直接調用 `.ToList()` 將整張資料表實體載入記憶體後再進行篩選或排序，不僅消耗頻寬，且無法利用資料庫索引。
- **重構對比**：
  ```csharp
  // 🔴 Before: 記憶體內篩選 (In-Memory Processing)
  var allLogs = _db.GetTable<s90_auth_log>().ToList();
  var recentLogs = allLogs.Where(x => x.auth_time >= DateTime.Now.AddDays(-1)).Take(50);

  // 🟢 After: 資料庫端延遲查詢、投影與分頁 (Server-side Paging)
  var recentLogs = await _db.GetTable<s90_auth_log>()
      .Where(x => x.auth_time >= DateTime.UtcNow.AddDays(-1))
      .OrderByDescending(x => x.auth_time)
      .Select(x => new AuthLogDto { Account = x.auth_usr, AuthTime = x.auth_time })
      .Take(50)
      .ToListAsync(ct);
  ```

### 重構建議 4：以原生 Options Pattern 取代硬編碼設定字串
- **問題現狀 (Code Smell)**：
  在各處使用 `Configuration["ConnectionStrings:OwnConnection"]` 等 Magic String。
- **重構對比**：
  使用強型別 `IOptions<DatabaseSettings>` 注入，獲得編譯期型別檢查與預設值防護。
