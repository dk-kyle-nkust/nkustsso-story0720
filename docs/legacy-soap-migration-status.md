# 舊 SOAP 方法移植狀態

2026-10-16：已實作 9／61 個方法；其餘 52 個仍待資料語意對應與移植，不能用「已完成所有方法」描述。目前未與正式校務個人資料、正式呼叫密鑰或廠商接收端互通測試。

## 集中維護方式

所有 SOAP 請求仍走 `CredentialVerificationApiController.Soap` 與 `/KuasSSOWebService.asmx`；不新增 61 個控制器。控制器共用 XML／SOAPAction 檢查、來源 IP／方法授權、稽核及錯誤處理。

- `LegacySoapOperationCatalog`：可執行方法及服務授權代碼。
- `LegacySoapContract`／`LegacySoapWsdlGenerator`：安全解析、陣列／DataSet 序列化與對應 WSDL。
- `LegacySoapOperationDispatcher`：集中依方法分派與回傳欄位限制。
- `CampusSoapQueries`／`CampusSoapAccountSource`：新 MSSQL 唯讀查詢與帳密驗證。
- `IntegrationReturnFields`：後台可勾選欄位，固定結果與禁止輸出的密碼欄位。

此拆分讓 HTTP 入口集中，同時避免在控制器混入資料庫連線、SQL、密碼驗證及 XML 組裝。

## 已實作方法與已知差異

| 方法 | 新來源／實作 | 接收端須確認 |
|---|---|---|
| `KuasSSO_Certify2` | `s90_auth_usr.usr_mdpwd`、`sts_id` | 來源固定新 MSSQL，不再回退 LDAP；未授權身分代碼回傳空字串 |
| `KuasSSO_Certify1` | `s90_auth_log`＋`s90_auth_usr` | 台北時間，0–119 秒有效、呼叫端 fnc_id 白名單、票據只能兌換一次；只在本機記錄摘要，不改校務票據 |
| `KuasSSO_SchCardSearch` | 新校務帳密驗證 | 保留結果陣列，呼叫帳密改由後台原呼叫帳密雜湊驗證 |
| `Get_Traffic_Pub` | `s90_auth_usr`，沿用帳號含 office 的條件 | 回傳原具名欄位；pubusr_idno 沿用原文識別欄位，須單獨授權；pwd 留空 |
| `Get_Traffic_Teach`／`Get_Traffic_Teach_Temp` | `s10_employee`＋`s90_unit`＋`s90_auth_usr` | 限 emp_status 非 0L；Temp 改用校務查詢，不再 LDAP；pwd 留空 |
| `Get_Traffic_Stud`／`Get_Traffic_Stud_Temp` | `s30_student`＋`s30_sturec`＋`s90_class`＋`s90_unit`＋`s90_auth_usr` | 沿用主要學年期條件；Temp 歷史紀錄需 src_status=1；移除原碼的單一學號特例；pwd 留空 |
| `NkustSSO_DocumentApplyPaymentSystemSearch` | 原校務 SQL 的學生／教職員資料表與身分證號、生日比對 | 使用原校務分支的 std_ 欄位；GRADSTUD 依是否具有當期學籍判定；詳見下節 |

交通查詢保留 DataSet 的內嵌 XSD＋DiffGram 與原欄位順序；學生／員工識別摘要使用舊版 UTF-8 MD5 大寫格式，但不將 MD5 用於新密碼驗證。未授權欄位保留空字串，錯誤回應保留 message。來源關聯不唯一時回傳通用 SOAP Fault，不任選一筆。

新 MSSQL 含多校區資料，交通關聯須帳號、學生／教職員、班級與單位的 cmp_area 一致；無法確認或沒有符合關聯時不返回個資。此限制與舊版只以單位代碼連接的作法不同，須以實際資料驗證。

`pwd` 一律不讀取及輸出。若接收端仍依賴舊密碼衍生值，不能視為已完全相容，需要確認接收端調整方式。

## 剩餘方法需要補充的 model／規則

### 逐一處理：NkustSSO_DocumentApplyPaymentSystemSearch（已實作）

依使用者最新決定，直接使用原校務 SQL 分支，不再依賴 `SSO_MappingUsrData` 或 LDAP。後續方法也先核對原校務 SQL；只有原始碼沒有校務來源或缺少必要欄位時才逐項確認。

- 原參數完整保留：`SearchUsr_id`、`SearchUsr_pwd`、`IDNO`、`Birthday`、`identity`、`from_Clientip`。呼叫端須獨立授權此方法並通過來源 IP／呼叫帳密驗證；自填 IP 不作為授權依據。
- 學生查 `s90_auth_usr`、`s30_student`、`s30_sturec`、`s90_yms`、`s90_auth_status`，要求帳號 `sts_id=02`。`STUD` 需有當期學籍；`GRADSTUD` 依原校務條件，該證號與生日不得查到當期學籍，再回傳符合條件的學生資料。這不是改用新畢業註記判定。
- 教職員查 `s10_employee`、`s90_auth_usr`、`s90_titlecode`、`s90_unit`、`s90_dept`、`s90_auth_status`。單位沿用 `emp_wuntid`；原 SQL 未排除離職，不套用交通方法的 `emp_status` 過濾。
- 生日使用 `s30_student.std_birthday`／`s10_employee.emp_bdate`（新來源皆為 `varchar(7)`），沿用去除輸入前後空白後的字串比對，不猜測或自動轉換日期格式。
- DataSet 欄位固定為原校務投影的 `message/std_id/std_name/std_email`，教職員也沿用 `std_`；不回傳 LDAP 分支的 `usr_` 欄位。接收端須核對此差異。結果代碼：200 成功、500 無符合資料／輸入不符、501 呼叫帳密錯誤。
- 不同學號依原查詢回傳多列，輸出按編號排序；相同帳號有不同資料、超過 100 列或當期學年期不唯一時回傳通用 SOAP Fault。未授權欄位保留空字串。
- 新增校區一致限制，避免借用另一校區的帳號、工作單位、職稱或部門；正式來源尚需驗證關聯完整性。所有查詢參數化且唯讀，不記錄身分證號、生日或密碼。

### 逐一處理：NkustSSO_PaySystemStudSearch（原始碼已核對，尚未實作）

- SOAP 入口在舊 `SOA/KuasSSOWebService.asmx.cs:9402`；先呼叫 `SOA/Model/ModelForSOA.cs:120` 的 `getMappingUsrDataByType1`，再呼叫 `SOA/Module/LdapAuthentication.cs:7199` 的 `IsSSOForEasyTestAuthenticated`。
- 入口依證號、校務密碼及 `STUD/GRADSTUD` 篩選對照表，再取第一個密碼符合的帳號。成功陣列依序為結果碼、登入說明、boolean、帳號、姓名、Email、身分類別字串、單位字串；失敗為 500，呼叫帳密錯誤為 400。
- 本次進一步找到 `AutoADAsy/Model/ModelForAutoADAsy.cs` 的 `Get_ModelSqlData`，其中保存對照表的原始校務 SQL；`NewKuasSystem/Model/DataProcess/ModelForDataProcess.cs` 也有同類來源。這些來源僅供讀取與核對，未執行舊同步或寫入程式。
- 在學生來源可追至 `s90_auth_usr → s30_student → s30_sturec → s90_class → s90_unit → s90_dept`，以當期學年學期查詢；`ou` 字串由班級、單位、部門的代碼與名稱組成。原 EasyTest helper 取 `ou` 的第二段作為學生單位，並以 `02,學生`／`02,畢業生` 表示身分。
- 校友來源還有 `usr.usr_id NOT IN (SELECT usr_id FROM kuasdev.dbo.v_SSO_CmpGrdStud)`（`AutoADAsy/Model/ModelForAutoADAsy.cs:427`）。目前專案只有引用，搜尋未找到 View 定義或 SQL 專案檔，不能直接假設該 View 等同當期學籍。已逐項詢問此 View 的 SELECT 或新來源對應。
- 尚未加入可執行清單、WSDL 或已完成數量；目前仍是 9／61。拿到 View 定義後才能完成校友範圍及多學號驗證規則，不能把第 9 個方法不同的校友 SQL 直接套用。

已以新資料庫結構中繼資料核對，未找到可直接替代 `SSO_MappingUsrData` 的完整 LDAP 相容資料。現在已找到其部分校務資料產生 SQL，將逐項核對以下差異，不能再把所有欄位概括視為沒有原始來源。原校務分支也缺少必要來源時，才詢問 140.133.78.225 的表名、model 與代碼語意。

| 所需資料 | 舊版欄位／規則 | 為何不能直接推定 |
|---|---|---|
| 帳號停用／離職狀態 | LDAP comment／usr_status 的 0L | emp_status 已供交通查詢使用，但一般帳號、學生及公用帳號的停用來源仍未確認 |
| 精確身分類型 | employeeType：老師、職員、公用帳號等 | 新系統 sts_id=01 現為教職員大類，不能直接等同舊 LDAP 的老師；影響教師專用服務 |
| 在學生／畢業生 | company=GRADSTUD 等 | s30_student.grad_status、s30_sturec.src_graduate 等候選欄位存在，但畢業、休退學與多重身分判定規則未確認 |
| 主管層級、系所主管 | employeeNumber、initials=1、division | iKuas／TeachDepartment 依這些值授權，不能用名稱或任一單位欄位代替 |
| 組織與群組 | memberOf、ou、ADSPATH、主管及學制／系／單位／班級的 code,name 組合 | 部分介面直接回傳 LDAP 格式字串；新組織關聯與舊輸出順序須逐項對應 |
| 學籍、性別、聯絡資料 | title、homePostalAddress、telephoneNumber、info、uidNumber 等 | 新表有部分候選欄位，但不同 helper 的陣列位置及欄位語意不相同 |
| 身分證號／校務密碼驗證 | PaySystemStud 的 IDNO、SchSysPwd | 原始碼取首筆密碼符合帳號，需處理多帳號歧義；校友來源仍缺 v_SSO_CmpGrdStud 定義 |
| checkSession 憑據 | 舊 Encryption 解碼後查 LDAP 的 LibraryIR | 需要新發行端的憑據格式、有效期、對應帳號與使用規則；不照搬僅解密識別碼就信任的流程 |

其餘 52 個方法仍在說明頁保留原規格並標示待移植，將逐個檢查原校務 SQL、身分限制與成功／失敗回傳。上述 LDAP 差異不再一律視為阻擋條件；不能將通用個人資料陣列套用全部方法。

## 驗證

`tests/NkustSso.StorageChecks/LegacySoapMigrationChecks.cs` 使用強制 query_only 的合成校務 SQLite，檢查 9 方法的共用控制器、SOAP 1.1／1.2、DataSet 往返、逐方法授權、原呼叫密碼、欄位限制、票據有效期及並行防重放。未連線測試舊資料庫或正式個人資料。

`DocumentPaymentChecks.cs` 另驗證學生、教職員及 GRADSTUD 分支、生日不符、當期與舊學號並存、多學號回傳、跨校區關聯、相同帳號資料衝突及當期學年期不唯一。舊校務 SQL 來源為 `KuasSSOWebService.asmx.cs` 第 6995 行的 `NkustSSO_DocumentApplyPaymentSystemSearch`。

2026-10-16 已通過上述隔離檢查，以及 `tests/soap_migration_http.py` 的 9 方法實際 HTTP 呼叫。後台欄位維護、串接管理及 SOAP／JSON 說明頁的瀏覽器測試亦通過，包含桌面與手機版、CSP 及 JavaScript 錯誤檢查。這些結果不代表已與正式 MSSQL 或廠商程式完成互通。
