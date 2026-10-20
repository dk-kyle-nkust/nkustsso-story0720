# 全校系統 SSO 導入規劃

來源：校網學生／教師／職員三個入口頁（2026-10-13），共 110 條連結。
平台型態多由網址特徵推測，標「?」者待確認。

## 盤點

110 條去重與剔除後：真正需登入的應用約 40 個端點、CMS 公告頁 7（`gen`、`ofa`、`cc`、`irdnew`、`startup` 的 `p/4xx` 頁，排除）、外部 SaaS 3（走聯邦）、跨身分重複約 60。

40 個端點只分布在約 25 個程式基底，其中 **19 個擠在 `ws1`／`ws2`／`ws3`／`apply`／`apply2`**。排程用整合單位算，不用系統數算。

## 整合單位與波段

| 波段 | 單位 | 涵蓋 | 平台 | 方式 |
| --- | --- | --- | --- | --- |
| 1 | 校務核心 | `oosaf` 學務／校務／請假 | MVC? | OIDC 原生 |
| 1 | 選課 | `selcrs`、`stdsel` | MVC? | OIDC 原生 |
| 1 | 教學平台 | `elearning` | LMS? | 廠商 OIDC／LTI 外掛，不自寫 |
| 2 | `ws1` 群 | Activity、DORM、Dengue、Vote、tutor、IRead、CCElecForm、SystemMaintainApply | 同主機同框架? | 共用驗證元件，一次改多個受益 |
| 2 | `ws2` 群 | fixall、coursecounsellor | 同上 | 同上 |
| 2 | `ws3` 群 | Waiver、ScoreMOChange、BaMaApply、ProjectRAD、ProjectIAC | 同上 | 同上 |
| 2 | 申請簽核群 | `apply`/ADMApply、`apply2`/AddCrsApply、TransferUnit、srw | 同上 | 同上，另需組織職務資料 |
| 3 | 人事差勤 | `signin`、`ohr`、`empservice`、`ntapp` | WebForms，舊 | 能改碼走 OIDC，否則反向代理注入 Header |
| 3 | 主計財務 | `account02`、`account03` | ? | OIDC；高敏感 + 簽核鏈 |
| 3 | 公文知識 | `docu`、`sop`、`post`、`officemail`、`archives`、`suggest` | 混合 | 風險低，批次處理 |
| 4 | 校園服務 | `vms`、`webap0`、`ga`、`newvenue`、`hotel`、`souvenir` | 含 SPA | `newvenue` 用 Code + PKCE 公開用戶端 |
| 4 | 學籍教研 | `centapp`、`acadmis`、`recregapply`、`ceq`、`tevs`(PHP)、`care`、`higheredu` | 混合 | 批次；`care` 諮商高敏感 |
| 4 | 外部 SaaS | Primo、Google 表單、`giving` | SaaS | SAML／OIDC 聯邦 |

波段依「使用人數 × 改造成本」排，不依處室。波段 1 建立示範價值，波段 2 投資報酬率最高。

## 現有設計要改的四處

1. **身分欄位只做了學生。** 過半系統是教職員，靠單位職務判權限而非班級。補 `org_unit_code`／`org_unit_name`、`job_title`、`personnel_type`（專任教師／兼任／職員／助理／工讀）、`supervisor_level`。
2. **`identity_type` 單值會卡住。** 「兼任助理簽到」證明一人可同時是學生與受僱者，教師兼主管同理。改為可同時成立的身分集合，不在 SSO 端強迫選主身分。
3. **簽核鏈別塞進 ID Token。** `account03`、`apply2`、`docu` 需組織層級與代理人，量大又常變。ID Token 只放穩定最小集，組織樹另開人事 API 讓各系統即時查；代理簽核屬業務授權，留在各系統。
4. **全域 4 小時上限不適用全部系統。** 職員整天掛在簽到、請購、公文上，強制重登會被罵；`ohr`、`account02/03`、`centapp`、`care` 反而該更短。期限改為每系統設定，加進 `/admin/systems`。

## 波段 2 前必須補的技術債

- **即時停權**：清單含人事、主計、成績，離職或盜用須能切斷下游。目前 back-channel logout 未實作。選 back-channel logout 或各系統每請求查核中央狀態（改動小但增中央負載）。
- **密鑰輪替**：40 個用戶端密鑰，管理頁現無查閱與輪替，離開建立成功頁即拿不回。

## 不要做

- 不要為 CMS 公告頁做 SSO。
- 不要為 Primo 一個系統就引入 Keycloak 上 SAML；先把校內 OIDC 做完。
- 不要代填密碼或模擬表單登入。綁死廠商 HTML、改版即壞，且等於在中央存可還原密碼。不能改碼就走反向代理。
- 不要先談例外。做出波段 1 成功案例後再回頭處理舊系統，談判位置較好。

## 動手前要確認

1. `ws1`／`ws2`／`ws3`／`apply`／`apply2` 的 19 個應用是否同團隊同框架、可共用驗證元件？**整份規劃最大變數。**
2. 是否已有統一帳號來源（AD／LDAP／校園入口）？各系統現在各自存密碼還是已集中？
3. 哪些外包且合約含改造？哪些已無廠商維護（`.aspx` 那批特別確認）？
4. `elearning` 實際平台與版本，決定能否用現成外掛。
5. 單位、職稱、身分別的權威來源與更新頻率（對應 `ohr`）。
6. 法規或稽核是否指定特定系統的登入期限與紀錄留存。

## 一萬人同時在線的架構檢視（2026-10-19）

這次是程式碼與設定檢視，尚未進行一萬人壓力測試。ASP.NET Core 與 SQL Server 可以作為此規模的基礎，但目前專案仍限定 Development，不能將功能測試通過視為正式容量認證。先區分登入中的 Session 數、活躍使用人數、每秒請求數及每秒登入數；一萬筆有效 Session 不等於一萬條持續占用的資料庫連線。接收系統建立自己的登入後，業務操作通常由接收系統處理，並非每次操作都回到 SSO。

### 目前紀錄範圍

| 類型 | 已有紀錄 | 限制 |
| --- | --- | --- |
| 帳密登入 | `LoginEvents` 保存完成登入驗證流程的帳號 ID、時間及成功／失敗結果 | 未知帳號可能沒有 ID；表單驗證提早返回或限流拒絕不進此紀錄，沒有統一的原因碼及請求關聯 ID |
| 中央登入狀態 | `LoginSessions` 保存帳號、登入時間、最近活動、期限、IP、瀏覽器及撤銷原因 | 有效 Session 不能精準等同「人正在使用」；沒有每次點擊紀錄 |
| 管理維護 | 系統、分類、身分、選單與群組等有對應異動稽核；部分設定與稽核在同一交易提交 | 不是所有讀取、頁面瀏覽及拒絕事件都有統一紀錄 |
| 串接與票券 | `TicketAudits`、`SamlAudits`、`IntegrationAudits` 保存對應流程事件 | 不能得知接收系統登入後的選課、報帳或其他業務操作，需由各接收系統記錄 |

### 需要優先處理的負載與可靠性問題

1. `LoginSessionStore.RetrieveAsync` 每次讀取有效 Cookie 都查中央 Session 與帳號；校務帳號接著經 `CampusSessionValidator` 查中央鎖定狀態及外部校務帳號狀態。這至少涉及四次資料庫查詢，還不包含頁面及權限查詢。簡化登入資訊已移除額外班級卡片查詢，但未取消安全驗證。若要對來源狀態短暫快取，須先定義可接受的停權延遲及失效通知，不能為效能默默放寬即時撤權。
2. 登入目前按來源 IP 每分鐘限制 20 次，驗證 API 為 60 次；校內共用 NAT 或反向代理可能讓正常使用者共享額度。正式部署須依帳號、呼叫端與可信代理規劃分層防護；不能直接信任外部提供的 `X-Forwarded-For`。限流及 `CampusLoginGates` 目前是行程內狀態，多節點時也需重新設計一致性。
3. `LastSeenAt` 設計為約每分鐘更新一次，但同一 Session 並發請求可能同時通過讀取後的時間判斷。可將時間條件放入 UPDATE 來減少重複寫入，並量測實際效益。重要設定稽核應保留與異動同一交易；大量瀏覽事件如需延後寫入，應使用可持久保存、有重試與背壓的佇列，不能以未等待的背景工作或純記憶體佇列保證不遺失。
4. 清理工作目前主要處理過期票券、驗證嘗試及部分 30 天稽核；`LoginEvents`、`LoginSessions` 與管理異動紀錄尚無完整封存／保留策略。需依實際查詢與資料量確認時間、帳號、系統等索引，並採分批清理，避免大量一次刪除占住交易紀錄或鎖定。高流量清單應評估伺服器端分頁。
5. 擴充多台網站前，需共用 Data Protection 金鑰與解密設定、採正式且持久的 OIDC／SAML 簽章材料、使用共用資料庫並驗證票券原子兌換。現有 OIDC 使用行程內臨時金鑰，Data Protection 預設保存在本機；不能直接複製網站就視為可負載平衡。相關部署原則見 [Microsoft Web Farm 說明](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/web-farm?view=aspnetcore-10.0)。

### 容量驗證方式

使用獨立環境及合成帳號，停用真實校務驗證或改接模擬來源；不對正式校務資料庫進行壓測。建立一萬筆有效 Session，再分別測試閒置、每人每分鐘一次操作、每人每 30 秒一次操作、尖峰登入及票券兌換。後兩種平均操作量約為 167 與 333 次／秒，僅是情境算式，頁面附帶請求及登入導向需另外計算。

量測登入／開頁／兌換的 p95、p99 延遲，5xx、非預期 429、逾時、CPU、記憶體、GC、ThreadPool、資料庫連線池等待、SQL 查詢耗時、鎖定及交易紀錄寫入；另驗證資料庫故障、網站重啟、票券並發兌換與稽核持久性。先依正式硬體與校方服務目標訂通過標準，再決定是否需要 Redis、多節點或紀錄佇列，不以線上人數直接推定必須採微服務。減少常用路徑中的遠端往返、採非同步 I/O 與分頁等原則見 [Microsoft ASP.NET Core 效能建議](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)。
