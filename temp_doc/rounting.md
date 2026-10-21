# NkustSSO 網站 Routing 路由清單與安全鎖定機制

本文件彙整 NkustSSO 網站之完整 Routing 路徑、HTTP 方法、對應 Controller、功能說明，以及如何在 `Program.cs` 中透過中介軟體管線、動態授權 Policy、頻率限制與安全防護機制達成路徑映射與安全鎖定。

---

## 一、前台門戶與服務入口 (Portal & Public)

| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/` | `PortalHomeController.Index` | 匿名 | 網站首頁，自動導向 `/portal` |
| `GET` | `/portal` | `PortalHomeController.Index` | 匿名 / 登入 | SSO 服務入口大廳（依已登入身分顯示開放之系統卡片） |
| `GET` | `/portal/{section}` | `PortalServicesController.Section` | 匿名 / 登入 | 專區服務列表（如 `/portal/student`、`/portal/employee`、`/portal/alumni`） |
| `GET` | `/portal/{section}/systems/{id}` | `PortalServicesController.Launch` | 匿名 / 登入 | 點擊系統入口卡片，啟動對應 SSO 協定或轉導至外部系統 |
| `GET` | `/faq` | `PortalServicesController.FrequentlyAskedQuestions` | 匿名 | 常見問題頁面 |
| `GET` | `/integration` | `PortalHomeController.Integration` | `LoginSessionAdministration.Policy` | 內部串接相容性與開發進度頁面 |

---

## 二、帳號登入與工作階段 (Account & Session)

| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/account/login` | `AccountLoginController.SignIn` | 匿名 | 顯示中央帳密登入頁面 |
| `POST` | `/account/login` | `AccountLoginController.SignIn` | 匿名（限速防暴破） | 提交帳號密碼登入（校務登入 / 本機管理員登入） |
| `GET` | `/account/continue` | `AccountLoginController.Continue` | 已登入 (`[Authorize]`) | 登入成功後的中轉防偽跳轉頁 |
| `POST` | `/account/logout` | `AccountLoginController.Logout` | 已登入 (`[Authorize]`) | 登出目前的中央登入工作階段 |
| `GET` | `/account/current-user` | `AccountLoginController.CurrentUser` | 匿名 / 已登入 | 取得當前已登入者的身分摘要與防偽 Token（供前端狀態列刷新） |
| `GET` | `/account/denied` | `AccountLoginController.Denied` | 匿名 | 權限不足提示頁面（HTTP 403 Forbidden） |

---

## 三、單一登入（SSO）協定端點

### 1. OpenID Connect (OIDC) - OpenIddict
| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/connect/authorize` | `OidcAuthorizationController.Authorize` | 匿名 / 導向登入 | OIDC 授權端點（Authorization Code Flow + PKCE） |
| `POST` | `/connect/token` | `OidcAuthorizationController.Exchange` | 用戶端認證 | OIDC Token 端點（以 Code 兌換 Access Token 與 ID Token） |
| `GET` / `POST` | `/connect/logout` | `OidcAuthorizationController.Logout` | 匿名 / 已登入 | OIDC 登出端點 |
| `GET` | `/.well-known/openid-configuration` | OpenIddict 內建處理 | 匿名 | OpenID Provider 設定探索中繼資料 |
| `GET` | `/.well-known/jwks.json` | OpenIddict 內建處理 | 匿名 | 公鑰憑證 JWKS 端點 |

### 2. SAML 2.0 IdP
| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/saml/metadata` | `SamlSsoController.Metadata` | 匿名 | SAML 2.0 IdP 元資料（Metadata XML） |
| `GET` | `/saml/sso` | `SamlSsoController.SingleSignOn` | 匿名 / 導向登入 | SAML 2.0 單一登入起始端點 |
| `GET` | `/saml/continue` | `SamlSsoController.Continue` | 已登入 (`[Authorize]`) | SAML 驗證後防偽轉導頁 |

### 3. Web API Ticket SSO（單一登入票券模式）
| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/ticket/authorize` | `WebApiSsoController.Authorize` | 匿名 / 導向登入 | Web API 票券發起端點（登入後簽發一次性票券 Code） |
| `POST` | `/ticket/redeem` | `WebApiSsoController.RedeemForm` | 用戶端金鑰 + PKCE | 表單格式票券兌換端點 |
| `POST` | `/api/sso/v1/redeem` | `WebApiSsoController.RedeemJson` | 用戶端金鑰 + PKCE | JSON 格式 Web API 票券兌換端點（換取身分欄位） |

---

## 四、伺服器對伺服器帳密驗證介接 (Integration Services)

| HTTP 方法 | 路徑 | 對應 Controller / Action | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/KuasSSOWebService.asmx` | `CredentialVerificationApiController.Contract` | 匿名 | SOAP 服務說明頁；若帶 `?wsdl` 則輸出 WSDL 文件 |
| `POST` | `/KuasSSOWebService.asmx` | `CredentialVerificationApiController.Soap` | 來源 IP + 呼叫端帳密 | 舊版 SOAP 帳密驗證介面（相容 9 個舊校務 SOAP 方法） |
| `GET` | `/api/integration` | `CredentialVerificationApiController.JsonHelp` | 匿名 | JSON 帳密驗證介接 API 說明文件 |
| `POST` | `/api/integration/v1/verify` | `CredentialVerificationApiController.Verify` | 來源 IP + 系統密鑰 | JSON 帳密驗證端點（直接驗證帳號密碼） |

---

## 五、後台管理功能 (Admin Portal - `/admin/*`)

> **存取權限說明**：全數要求登入，並依 `AdminAuthorizationService` 動態查核管理者功能權限。

| 模組 | HTTP 方法 | 路徑 | 對應 Controller / Action | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| **後台首頁** | `GET` | `/admin` | `AdminHomeController.Index` | 自動導向至首個具權限的功能頁 |
| **SSO 系統與入口** | `GET` | `/admin/systems` | `SsoSystemManagementController.Index` | 登記系統與首頁入口列表 |
| | `GET` | `/admin/systems/new` | `SsoSystemManagementController.Create` | 新增登記系統表單 |
| | `POST` | `/admin/systems/new` | `SsoSystemManagementController.Create` | 儲存新增登記系統 |
| | `GET` | `/admin/systems/{id}/edit` | `SsoSystemManagementController.Edit` | 編輯登記系統與開放對象表單 |
| | `POST` | `/admin/systems/{id}/edit` | `SsoSystemManagementController.Edit` | 儲存編輯登記系統 |
| **串接服務維護** | `GET` | `/admin/integration-services`<br>（或 `/admin/systems/api`） | `IntegrationClientManagementController.Index` | 伺服器對伺服器用戶端清單 |
| | `GET` | `/admin/integration-services/overview` | `IntegrationClientManagementController.Overview` | 串接服務狀態總覽 |
| | `GET` | `/admin/integration-services/new` | `IntegrationClientManagementController.Create` | 新增串接用戶端表單 |
| | `POST` | `/admin/integration-services/new` | `IntegrationClientManagementController.Create` | 儲存新增串接用戶端 |
| | `GET` | `/admin/integration-services/{id}/edit` | `IntegrationClientManagementController.Edit` | 編輯串接用戶端表單 |
| | `POST` | `/admin/integration-services/{id}/edit` | `IntegrationClientManagementController.Edit` | 儲存編輯串接用戶端 |
| | `POST` | `/admin/integration-services/{id}/rotate` | `IntegrationClientManagementController.RotateSecret` | 重新產生串接密鑰 |
| **首頁分類維護** | `GET` | `/admin/categories` | `PortalCategoryManagementController.Index` | 入口分類列表 |
| | `GET` | `/admin/categories/new` | `PortalCategoryManagementController.Create` | 新增分類表單 |
| | `POST` | `/admin/categories/new` | `PortalCategoryManagementController.Create` | 儲存新增分類 |
| | `GET` | `/admin/categories/{id}/edit` | `PortalCategoryManagementController.Edit` | 編輯分類表單 |
| | `POST` | `/admin/categories/{id}/edit` | `PortalCategoryManagementController.Edit` | 儲存編輯分類 |
| | `POST` | `/admin/categories/{id}/delete` | `PortalCategoryManagementController.Delete` | 刪除分類 |
| **身分分類手冊** | `GET` | `/admin/identity-catalog` | `IdentityCatalogController.Index` | 校務身分代碼與屬性查閱手冊 |
| **登入狀態管理** | `GET` | `/admin/sessions` | `LoginSessionManagementController.Index` | 在線 Session 列表與篩選 |
| | `GET` | `/admin/sessions/revoke` | `LoginSessionManagementController.Revoke` | 撤銷指定工作階段確認頁 |
| | `POST` | `/admin/sessions/revoke` | `LoginSessionManagementController.Revoke` | 執行撤銷指定工作階段 |
| **票券稽核紀錄** | `GET` | `/admin/tickets` | `SsoTicketAuditController.Index` | 票券簽發與兌換紀錄查詢 |
| **串接手冊下載** | `GET` | `/admin/manuals` | `IntegrationManualController.Index` | 介接技術手冊清單 |
| | `GET` | `/admin/manuals/download/{id}` | `IntegrationManualController.Download` | 下載指定串接手冊 |
| **後台選單管理**<br>*(僅最高管理者)* | `GET` | `/admin/menus` | `AdminMenuManagementController.Index` | 後台選單項目列表 |
| | `GET` | `/admin/menus/new` | `AdminMenuManagementController.Create` | 新增選單項目表單 |
| | `POST` | `/admin/menus/new` | `AdminMenuManagementController.Create` | 儲存新增選單項目 |
| | `GET` | `/admin/menus/{id}/edit` | `AdminMenuManagementController.Edit` | 編輯選單項目表單 |
| | `POST` | `/admin/menus/{id}/edit` | `AdminMenuManagementController.Edit` | 儲存編輯選單項目 |
| | `POST` | `/admin/menus/{id}/delete` | `AdminMenuManagementController.Delete` | 刪除選單項目 |
| **後台權限管理**<br>*(僅最高管理者)* | `GET` | `/admin/access` | `AdminRoleManagementController.Index` | 後台角色與功能權限清單 |
| | `GET` | `/admin/access/new` | `AdminRoleManagementController.Create` | 新增管理角色表單 |
| | `POST` | `/admin/access/new` | `AdminRoleManagementController.Create` | 儲存新增管理角色 |
| | `GET` | `/admin/access/{id}/edit` | `AdminRoleManagementController.Edit` | 編輯角色與功能授權表單 |
| | `POST` | `/admin/access/{id}/edit` | `AdminRoleManagementController.Edit` | 儲存編輯角色 |
| | `POST` | `/admin/access/{id}/members/find` | `AdminRoleManagementController.FindMember` | 查詢校務人員資料以指派角色 |
| | `POST` | `/admin/access/{id}/members/provision` | `AdminRoleManagementController.ProvisionMember` | 將校務人員加入該角色 |
| | `POST` | `/admin/access/{id}/delete` | `AdminRoleManagementController.Delete` | 刪除管理角色 |

---

## 六、系統基礎端點

| HTTP 方法 | 路徑 | 來源定義 | 授權要求 | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/health` | `Program.cs` Minimal API | 匿名 | 系統健康檢查端點（回傳 `{"status": "ok"}`） |

---

## 七、透過 Program.cs 實現路由映射與鎖定防護機制之詳細說明

`Program.cs` 是整個系統的啟動與安全管線核心，系統中的所有路徑並非單純開放存取，而是由中介軟體管線、身分驗證、動態授權原則、速率限制及防偽機制嚴密層層「鎖定」。以下詳細解析各項關鍵實作機制：

### 1. HTTP 請求管線（Middleware Pipeline）順序與端點生效

在 `Program.cs` 的第 151 至 167 行中，嚴格定義了中介軟體的執行順序：

```csharp
var app = builder.Build();

// 1. 全域安全標頭與快取防護（第一道防護鎖）
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

// 2. 傳輸層加密
app.UseHttpsRedirection();
// 3. 靜態資源處理
app.UseStaticFiles();
// 4. 端點路由比對（將 Request URL 映射至 Controller/Action）
app.UseRouting();
// 5. 頻率限制（Rate Limiting 鎖定）
app.UseRateLimiter();
// 6. 身分認證（還原 ClaimsPrincipal）
app.UseAuthentication();
// 7. 權限授權（執行 [Authorize] 與 Policy 檢驗）
app.UseAuthorization();
// 8. 映射 Controller 屬性路由
app.MapControllers();
// 9. 映射 Minimal API 端點
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
```

- **`app.UseRouting()` 與 `app.MapControllers()` 的協同**：
  系統使用 ASP.NET Core MVC，各 Controller 類別與方法上標註 `[Route("admin/systems")]`、`[HttpGet("/account/login")]` 等屬性路由。`app.UseRouting()` 先解析出請求欲存取的端點中繼資料（Endpoint Metadata），使得後續的 RateLimiter、Authentication 與 Authorization 能夠精準辨識目標端點並進行安全攔截。

---

### 2. 後台管理路徑的動態授權鎖 (`LoginSessionAdministration.Policy`)

所有 `/admin/*` 路徑控制器均掛載了 `[Authorize(Policy = LoginSessionAdministration.Policy)]` 屬性。在 `Program.cs` 第 106–111 行註冊了這道嚴格的授權策略鎖：

```csharp
builder.Services.AddAuthorization(options => options.AddPolicy(LoginSessionAdministration.Policy, policy =>
    policy.RequireAuthenticatedUser().RequireAssertion(async context =>
    {
        if (context.Resource is not HttpContext http) return false;
        return await http.RequestServices.GetRequiredService<AdminAuthorizationService>().AuthorizeAsync(context.User, http);
    })));
```

此鎖定機制具有以下核心特點：
1. **雙層強迫查驗**：
   - **第一層：`RequireAuthenticatedUser()`**：未登入者立即由 Cookie 驗證機制攔截，並強制 302 重導向至 `/account/login`（[Program.cs:L97](file:///d:/projects/01-main/nkustsso-story0720/NkustSso.Server/Program.cs#L97)）。
   - **第二層：`RequireAssertion` 動態斷言**：已登入者並非自動擁有後台權限，每次請求均由 `AdminAuthorizationService.AuthorizeAsync` 從 `HttpContext` 擷取目前的 `ControllerActionDescriptor`，直接查核資料庫中的最新角色權限表，避免權限異動後的授權滯後。
2. **細緻的動作分離（唯讀 vs. 維護）**：
   - 僅 `GET`/`HEAD` 且為 `Index`、`Overview`、`Integration` 或 `Download` 等動作判定為唯讀請求（比對 `feature:read` 權限）。
   - 其餘如 `Create`、`Edit`、`Delete`、`Rotate`、`Revoke` 均要求完整的維護權限（`feature` 權限）。
3. **最高權限隔離 (`RootOnly`)**：
   - `/admin/menus`（選單維護）與 `/admin/access`（角色授權維護）在服務中標記為 `RootOnly = true`，非最高管理者即使被指派其他權限亦不可存取。
4. **越權處理**：
   - 查驗未通過者直接回傳 HTTP 403 Forbidden，並自動轉導向 `/account/denied`（[Program.cs:L98](file:///d:/projects/01-main/nkustsso-story0720/NkustSso.Server/Program.cs#L98)）。

---

### 3. 伺服端 Session 儲存庫與即時撤銷鎖 (Session Store & Lifetime)

在 `Program.cs` 第 92–105 行，設定了 Cookie 身分驗證與 SessionStore：

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "NkustSso.Session.v1";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/denied";
});
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<LoginSessionStore, LoginSessionLifetimeService>((options, store, lifetime) =>
    {
        options.SessionStore = store;
        lifetime.ConfigureCookie(options);
    });
```

- **伺服器端 SessionStore 鎖定**：
  用戶端 Cookie 僅存放隨機 Session Key，實際的登入憑證與狀態存放在伺服端 `sessions.db`（或 SQL Server）。
- **即時失效機制**：
  當管理員在 `/admin/sessions/revoke` 撤銷工作階段，或外部校務來源帳號狀態變更（例如離校/離職）時，伺服端 Session 紀錄立即被註銷。用戶端下一次發出請求經過 `UseAuthentication()` 時，`options.SessionStore.GetAsync` 查無有效記錄，即刻將登入狀態失效並踢回登入頁，消除傳統 JWT 或本機 Cookie 無法即時撤銷的安全漏洞。

---

### 4. 高風險路徑頻率限制鎖（Rate Limiter）

在 `Program.cs` 第 141–150 行，配置了固定窗口（Fixed Window）速率限制器：

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("service-verification", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
```

- **`login` Policy 鎖定保護**：
  套用於 `AccountLoginController`（每分鐘單一來源 IP 上限 20 次請求），超過閾值時直接中斷並回傳 HTTP 429 Too Many Requests，阻斷暴力撞庫密碼攻擊。
- **`service-verification` Policy 鎖定保護**：
  套用於 `CredentialVerificationApiController`（每分鐘上限 60 次請求），嚴格保護伺服器對伺服器的 SOAP (`/KuasSSOWebService.asmx`) 與 JSON (`/api/integration/v1/verify`) 端點。

---

### 5. 全域 CSRF 跨站請求防偽鎖 (AutoValidateAntiforgeryToken)

在 `Program.cs` 第 139–140 行：

```csharp
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
```

- **預設全域鎖定**：
  系統將所有 `POST`、`PUT`、`DELETE` 等狀態變更動作全數自動套用 CSRF 防偽驗證。任何表單若未附帶合法的 Antiforgery Token，請求將在進入 Action 前被直接拒絕（HTTP 400）。
- **顯式豁免清單**：
  僅有使用獨立安全金鑰與來源 IP 驗證的非瀏覽器機器介接端點（如 `CredentialVerificationApiController`、`WebApiSsoController.Redeem*`），標註了 `[IgnoreAntiforgeryToken]`，其餘一般用戶與後台管理端點無一例外強制驗證防偽標籤。

---

### 6. OpenIddict 協定安全規範鎖 (OIDC Protocol Enforcement)

在 `Program.cs` 第 112–138 行，針對 OpenID Connect 端點實施標準安全限制：

```csharp
options.SetAuthorizationEndpointUris("connect/authorize")
    .SetTokenEndpointUris("connect/token")
    .SetEndSessionEndpointUris("connect/logout");
options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1));
options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(5));
options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
```

- **強制 PKCE 防護**：
  `RequireProofKeyForCodeExchange()` 強制要求 OIDC 用戶端在發起授權碼流程時必須提供 `code_challenge`，並在換取 Token 時驗證 `code_verifier`，防止授權碼被惡意攔截利用。
- **極短時效鎖定**：
  授權碼僅限 1 分鐘內兌換；簽發之 Access Token 與 ID Token 壽命僅 5 分鐘，大幅降低 Token 外洩後的濫用風險。

---

### 7. 內容安全政策（CSP）與快取防護鎖

在 `Program.cs` 第 152–159 行中介軟體中鎖定 HTTP 標頭：
- **`Content-Security-Policy`**：
  `default-src 'self'; form-action 'self'; frame-ancestors 'none';`
  - 禁止將本系統頁面嵌入至任何 `<iframe>` 中（防範 Clickjacking 點擊劫持）。
  - `form-action 'self'` 確保使用者帳密表單只允許送往本站，杜絕表單釣魚跳轉外送。
- **`Cache-Control: no-store`**：
  全站預設禁止瀏覽器與代理快取登入狀態與管理畫面，防止登出後透過上一頁瀏覽敏感個人資料。
