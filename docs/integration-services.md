# SOAP 與 JSON 帳密驗證服務

管理入口：「串接系統管理 → 帳密驗證 API」（`/admin/systems/api?kind=new`）。與 OIDC、SAML、票券和首頁連結共用管理導覽，資料及授權仍各自保存；舊 `/admin/integration-services` 管理網址保留相容。原有登入方式不變；JSON 驗證帳密；SOAP 另提供已移植的票據及資料查詢。兩者都不建立中央 Cookie 或接收系統權限。

## 舊系統與新串接

管理頁以兩個分頁區分系統清單、數量與最近紀錄：

- **舊系統**（`?kind=existing`）：管理需要保留既有介面的系統。登記入口預選 SOAP；原方法、參數及回傳格式仍須逐項移植及驗證，分類不代表相容工作已完成。
- **新串接**（`?kind=new`）：管理新加入的系統。新增入口預選 JSON，共用驗證端點並以每個系統獨立的代碼及密鑰識別。目前不提供個人資料欄位選取或資料同步。

分類儲存在 `IntegrationClients.ConnectionKind`，與協定獨立。升級前已有登記一律先歸入「舊系統」；管理者可在編輯頁調整分類，修改不變更協定、密鑰、IP 或服務授權，並保留版本檢查及分類前後的異動紀錄。不會根據來源識別碼猜測分類，也不會從外部資料庫自動匯入。

最近紀錄先篩選所屬系統，再取該分類最新 50 筆，顯示系統名稱與代碼。變更分類後，該系統歷史紀錄隨目前分類顯示；保存、取消及密鑰頁返回對應分頁。分頁是一般導覽連結，可使用鍵盤、瀏覽器上一頁及直接分享網址。

## 本批範圍

| 介面 | 已實作 | 尚未完成 |
| --- | --- | --- |
| SOAP | 同一 `CredentialVerificationApiController` 處理 9 個方法、SOAP 1.1／1.2、WSDL、逐方法授權與欄位限制 | 其餘 52 個方法的資料對應與移植、HTTP GET／表單 POST 繫結、正式資料及接收端互通 |
| JSON | `POST /api/integration/v1/verify`，系統密鑰＋來源 IP，回傳驗證布林值 | 個人資料查詢、票券或 Cookie 登入 |
| 後台 | 新增／編輯、啟停、來源 IP、JSON 密鑰更新、異動與驗證紀錄、本機程式規格匯入 | 實際呼叫帳號、IP 與原權限關係核對 |

完整舊介面盤點見 [SOAP 相容性清單](legacy-soap-compatibility.md)。**目前不能只搬移正式網域就取代全部舊服務。**

## 設定與啟用

預設 `SoapEnabled=false`、`JsonEnabled=false`。後台可以先完成登記；部署開關未開啟時，驗證端點回傳 HTTP 503。WSDL 可以先下載核對。

1. 以 SSO 管理者登入，進入「串接系統管理 → 帳密驗證 API → 新串接」，點「新增串接系統」。
2. 填入代碼、名稱、SOAP 或 JSON、呼叫端主機 IP，並在「可使用的服務」勾選「帳密驗證」。新登記預設沒有服務資格，未勾選會拒絕呼叫。來源 IP 每行一個，最多 40 個，接受 IPv4／IPv6，不接受 `*` 或網段；停用登記可先留空，啟用時必填。SOAP 同一 IP 只能對應一筆啟用登記。用途、負責單位與來源欄位見 [後台管理說明](system-administration.md#系統資料來源對應與服務授權)。
3. JSON 建立後顯示一次 `X-Client-Secret`；遺失時使用「更新密鑰」。更新後舊密鑰立即失效。
4. 參考 `appsettings.example.json`，以本機未追蹤設定、環境變數或部署機密配置帳密來源。不要覆蓋已存在的 `appsettings.json`，也不要把實際連線資訊放入 Git。
5. 完成測試後，將對應 `IntegrationServices:SoapEnabled`／`JsonEnabled` 設為 `true` 並重啟；同時啟用後台登記。停用某個登記立即阻止後續請求，已開始的驗證與接收端既有登入不會被撤回。

### 驗證來源

依本次移植要求，執行中的 SOAP／JSON 統一使用新校務 MSSQL `140.133.78.225`（實際連線放在 `ConnectionStrings:LoginAuth`），需啟用 `CampusAuthentication:Enabled`。所有外部查詢唯讀；不建立、修改或刪除任何舊資料庫／校務資料。

`IntegrationServices:AuthenticationSource`、LDAP 與 LegacySsoReadOnly 舊設定暫留供設定檔相容，但執行端不會使用舊 LDAP／Sybase 備援。帳密比對使用 `s90_auth_usr.usr_mdpwd` 的既有校務驗證器；不得把新 `sts_id` 的語意直接套到其他舊 LDAP 身分規則。

SOAP 的 `KuasSSO_Certify1` 另需後台設定允許的 `fnc_id`。含 `SearchUsr_id/SearchUsr_pwd` 或交通查詢 `usr_id/usr_pwd` 的方法，需填入對方既有呼叫帳密，只在本機保存密碼雜湊。新增加的方法資格不會自動授予既有登記。

完整方法、差異及缺少的 model 見 [移植狀態](legacy-soap-migration-status.md)。

## SOAP 呼叫

固定路徑：`https://主機/KuasSSOWebService.asmx`。不是實體 `.asmx` 檔案，而是 ASP.NET Core 路由。

SOAP 1.1：

```http
POST /KuasSSOWebService.asmx HTTP/1.1
Content-Type: text/xml; charset=utf-8
SOAPAction: "http://tempuri.org/KuasSSO_Certify2"
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <KuasSSO_Certify2 xmlns="http://tempuri.org/">
      <usr_id>測試帳號</usr_id>
      <usr_pwd>測試密碼</usr_pwd>
    </KuasSSO_Certify2>
  </soap:Body>
</soap:Envelope>
```

SOAP 1.2 使用 Envelope 命名空間 `http://www.w3.org/2003/05/soap-envelope`，Content-Type 為 `application/soap+xml; charset=utf-8; action="http://tempuri.org/KuasSSO_Certify2"`。

成功回傳的 `KuasSSO_Certify2Result` 保留舊 `ArrayOfAnyType`：

1. `anyType xsi:type="xsd:boolean"`：`true`。
2. `anyType xsi:type="xsd:string"`：已授權時為來源身分代碼，例如 `02`，不換成 `student`；未授權時為空字串，仍保留第二個元素。

帳密不通過回傳同樣兩個元素：`false`、`500`，HTTP 200。來源暫時無法使用回傳 SOAP Fault／HTTP 500；部署開關關閉為 503；來源 IP 未登記為 403。不把來源例外或連線資訊傳給對方。

原 `Certify2` 沒有系統帳密參數，因此不強迫舊呼叫端增加密鑰標頭。管理者必須先登記對方實際來源 IP。多個應用共用同一 NAT／代理 IP 時，只能按該共同來源登記，無法用舊參數區分個別應用。

## 回傳欄位管理

後台「串接服務 → 舊系統 → 新增／編輯」的 SOAP 登記提供「允許回傳的欄位」。每個呼叫系統、每個方法獨立勾選，畫面顯示中文用途、原欄位名稱／陣列位置、固定回傳及待移植狀態。

| 方法 | 可設定內容 | 實際狀態 |
|---|---|---|
| `KuasSSO_Certify2` | 成功時的來源身分代碼 `sts_id`（位置 1） | SOAP 1.1／1.2 回應已套用設定 |
| `KuasSSO_Certify1` | 成功時的來源身分代碼（位置 1） | 已實作校務票據查驗、有效期及本機防重放 |
| `Get_Traffic_Stud`／`Get_Traffic_Stud_Temp` | 學號、姓名、身分證號 MD5、單位代碼／簡稱、身分類型、帳號、電子郵件 | 已實作唯讀查詢及 DataSet 回傳，pwd 固定空字串 |
| `Get_Traffic_Teach`／`Get_Traffic_Teach_Temp` | 員工編號、姓名、識別摘要、單位、帳號、電子郵件 | 已實作唯讀查詢及 DataSet 回傳，pwd 固定空字串 |
| `Get_Traffic_Pub` | 公用帳號、名稱、識別資料、類型及電子郵件 | 已實作唯讀查詢及 DataSet 回傳，pwd 固定空字串 |
| `KuasSSO_SchCardSearch` | 固定結果代碼、說明及 boolean | 已實作，不提供個資欄位 |
| `NkustSSO_DocumentApplyPaymentSystemSearch` | `std_id`、`std_name`、`std_email` | 已實作原校務資料表查詢；教職員沿用相同欄位名稱，多學號可回傳多列 |

其餘 52 個方法尚未完成逐欄核對，不提供推測的欄位選項。欄位目錄集中於 `Services/IntegrationReturnFields.cs`，依舊版 `KuasSSOWebService.asmx.cs` 的成功分支核對；陣列方法不得刪除元素後讓後續位置前移。尚待移植的方法完成時，也必須在實際回應組裝處接入欄位授權與測試；目前保存需求不等於已提供資料。

- 未勾選的個資不傳出，已實作的 `Certify2` 以空字串保留原位置。驗證結果及失敗錯誤碼固定保留，不允許取消。舊接收端是否接受空值，仍須實際互通確認。
- `Get_Traffic_Stud_Temp.pwd` 為舊版密碼衍生值，畫面列出用途但不開放勾選；直接偽造表單也會拒絕。未經確認不移植密碼輸出或舊密碼紀錄副作用。
- 授權只寫入本專案 SQLite 的 `IntegrationClientReturnFields(ClientId, FieldKey)`；不存使用者實際資料，不異動舊資料庫或校務 MSSQL。新校務資料來源維持唯讀。
- 儲存欄位、服務資格及主資料使用同一交易、版本檢查與稽核；紀錄 `BeforeReturnFields`／`AfterReturnFields`，不記錄實際回應個資。取消後新的請求立即套用。
- 新登記預設沒有可選回傳欄位。一次性升級僅保留原有已授權 SOAP 帳密驗證系統的 `Certify2` 身分代碼，避免無預警改變既有回應；重新啟動不會還原管理者取消的設定。
- 欄位勾選不會開通服務或改變來源 IP 限制。待移植欄位不會加入可執行 WSDL。JSON 帳密驗證固定只有 `verified`，不接受 SOAP 欄位授權。

隔離驗證：`IntegrationReturnFieldChecks` 檢查遷移、欄位白名單、取消及 XML 形狀；`tests/integration_return_fields.py` 檢查實際後台操作、鍵盤、手機、CSP 與表單偽造。

## JSON 呼叫

```http
POST /api/integration/v1/verify HTTP/1.1
Content-Type: application/json
X-Client-Id: 對方系統代碼
X-Client-Secret: 建立時顯示的密鑰

{"userName":"測試帳號","password":"測試密碼"}
```

正常處理為 HTTP 200：`{"verified":true}` 或 `{"verified":false}`。接收端必須先確認 HTTP 狀態與 JSON 格式，再判斷 `verified`；非 200 都不能當成登入成功。

| HTTP 狀態 | 意義 |
| --- | --- |
| 400 | JSON 格式錯誤 |
| 403 | 系統停用、來源 IP 不符或系統密鑰錯誤 |
| 413 | 請求超過 16 KiB |
| 415 | Content-Type 不支援 |
| 429 | 同一來源 IP 超過每分鐘 60 次請求 |
| 503 | 服務尚未啟用，或帳密來源暫時無法使用 |

密鑰只用於伺服器對伺服器呼叫，不可放在前端。每個帳號在五分鐘內連續失敗五次會鎖定五分鐘，SOAP、JSON 與原 MSSQL 登入共用帳號嘗試紀錄；目前鎖定序列化以單行程為前提。

## 與舊行為的已知差異

- 舊 LDAP `employeeType` 或備援 `sts_id` 空白可能回傳空陣列；新介面固定使用新校務來源，要求非空白身分代碼，否則回傳 `false, "500"`，與原網站將空白分類視為 other 的行為不同。
- 不移植舊程式把明文密碼寫入 `SSO_RecordForUpdPwd` 的副作用。若仍依賴該佇列更新 AD 密碼，必須先完成替代同步流程。
- 重複帳號資料不任取第一筆，會拒絕驗證。密碼格式限現有天方驗證器支援的 ASCII。
- 不提供以 URL 傳送帳密的 HTTP GET 或舊表單 POST 繫結；不支援額外 SOAP Header。使用這些功能的呼叫端需要個別處理。
- 新增來源 IP 管制、請求大小、速率與帳號鎖定；既有系統需安排登記與負載確認。
- 驗證紀錄只保存系統代碼、時間、來源 IP、結果，保留 30 天。設定異動另保留管理者與不含密鑰的設定快照。

## 正式網域切換前

保留 `/KuasSSOWebService.asmx` 路徑、SOAPAction 與命名空間，且將 `Sso:Issuer` 換成部署網址，WSDL 才會顯示正確位址。網址相同並不代表所有舊方法已相容。

需先確認實際呼叫中的方法、繫結與參數；移植所需方法並以對方現有程式驗證成功／失敗／停用情境。`KuasSSO_Certify1` 已加上功能白名單及本機防重放；`checkSession` 的舊加密／憑據與其他未移植方法仍待對應，不能因此宣稱全部相容。

部署時需安排 TLS、IIS／反向代理 `.asmx` 路由轉送及可信主機。來源 IP 目前採 `HttpContext.Connection.RemoteIpAddress`，不相信呼叫者自填欄位或任意 `X-Forwarded-For`；若經代理，需另完成限制已知代理的 Forwarded Headers 設定，不可直接把共用代理 IP 當成所有來源都可信。

本專案仍有非 Development 啟動防護；正式用憑證、持久金鑰、備份、代理設定與部署切換尚未完成。本批沒有變更 DNS 或正式服務。

## 驗證與套件

```powershell
dotnet build tests/NkustSso.StorageChecks/NkustSso.StorageChecks.csproj -o .local/integration-verified-build
dotnet .local/integration-verified-build/NkustSso.StorageChecks.dll
$env:NKUST_TEST_BUILD_DIRECTORY = "$PWD/.local/integration-verified-build"
python tests/integration_api.py
python tests/soap_migration_http.py
```

測試使用合成帳密、隔離且唯讀的校務 SQLite、模擬來源及 HTTP 呼叫；不連線正式來源。涵蓋 9 個方法的 SOAP 1.1／1.2、DataSet 往返、逐方法授權、欄位限制、票據有效期與防重放，以及 XML 型別、命名空間、錯誤格式、拒絕 DTD、WSDL、IP／密鑰、停用、輪替、共用鎖定、來源錯誤、CSRF、管理權限與 CSP。舊 LDAP 模擬測試僅保留歷史類別的回歸檢查，執行端不使用該來源。

套件版本依官方 NuGet／npm 最新穩定版核對並固定；前端檔案由 `scripts/update-frontend.py` 下載且驗證 npm 完整性，本機版本與來源見 `wwwroot/lib/versions.json`。未使用預覽版或執行期 CDN。
