"""產生可交付的串接 PDF 與 HTML；內容依目前實作整理，不讀取本機機密設定。"""
from pathlib import Path
from html import escape
import argparse
import json
import os
import re
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
SERVER = ROOT / 'NkustSso.Server'
VERSION = '2026-10-19 · v1.0'
BASE = 'https://sso.example.edu'
APP = 'https://app.example.edu'
DOCS = []


def p(text): return '<p>' + text + '</p>'
def h(text): return '<h2>' + escape(text) + '</h2>'
def note(text): return '<aside>' + text + '</aside>'
def code(text): return '<pre><code>' + escape(text.strip()) + '</code></pre>'
def items(*texts): return '<ul>' + ''.join('<li>'+t+'</li>' for t in texts) + '</ul>'
def table(headers, rows):
    return '<table><thead><tr>'+''.join('<th>'+escape(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+str(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table>'
def flow(steps, compact=False):
    if compact:
        out=['<svg class="flow" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 680 48" role="img" aria-label="串接流程圖">']
        for i,(title,desc) in enumerate(steps):
            x=i*230
            out.append(f'<rect x="{x+1}" y="1" width="218" height="46" rx="8" fill="#eff5fb" stroke="#bdcfe0"/><text x="{x+110}" y="28" text-anchor="middle" fill="#19374d" font-size="12">{i+1}. {escape(title)}</text>')
            if i<2: out.append(f'<text x="{x+219}" y="28" font-size="14" fill="#41658b">→</text>')
        return ''.join(out)+'</svg>'
    height = len(steps)*58-8
    out = [f'<svg class="flow" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 680 {height}" role="img" aria-label="串接流程圖"><defs><marker id="arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0 0 L10 5 L0 10" fill="#41658b"/></marker></defs>']
    for i,(title,desc) in enumerate(steps):
        y=i*58
        out.append(f'<rect x="2" y="{y+1}" width="676" height="46" rx="9" fill="#eff5fb" stroke="#bdcfe0"/><circle cx="24" cy="{y+24}" r="13" fill="#244968"/><text x="24" y="{y+29}" text-anchor="middle" fill="white" font-size="14">{i+1}</text><text x="48" y="{y+19}" fill="#19374d" font-size="14" font-weight="700">{escape(title)}</text><text x="48" y="{y+37}" fill="#456079" font-size="12">{escape(desc)}</text>')
        if i < len(steps)-1:
            out.append(f'<path d="M340 {y+48} V{y+57}" stroke="#41658b" stroke-width="2" marker-end="url(#arrow)"/>')
    return ''.join(out)+'</svg>'


def doc(stem, title, subtitle, pages, sources):
    DOCS.append(dict(stem=stem,title=title,subtitle=subtitle,pages=pages,sources=sources))


COMMON = note('本文件為目前開發版介面規格；正式網址、系統代碼、密鑰／憑證、授權欄位與測試帳號請向 SSO 管理者取得。example.edu 為示意位址，不可直接呼叫。')
CHECKS = items('正常登入、中央已登入與登入逾時三種情境都要測試。', '錯誤回呼、錯誤密鑰／簽章、票券重放、停用系統與停用帳號必須拒絕。', '只有允許的欄位才可使用；欄位缺少或空白要能處理。', '本地帳號未開通或沒有業務權限時顯示拒絕頁，不自動授予管理權。', '登入後導向不含 code／ticket 的站內頁面；日誌不留原始密鑰、票券或 Token。')
SESSION = note('中央與接收系統各自建立 Cookie／Session。中央登入有效可省略再次輸入帳密；接收系統仍須完成驗證。中央登出不等於所有下游同時登出，本版未實作完整跨系統單一登出。')
FIELDS = [
 ('preferred_username','username','校園登入帳號；學生為學號、教職員為員編'),
 ('name','name','姓名'), ('identity_type','identity_type','身分類型，例如 student／employee；依實際來源，不自行推測'),
 ('class_code','class_code','班級代碼'), ('class_name','class_name','班級名稱'),
 ('student_status_code','student_status_code','學籍狀態代碼'), ('student_status_name','student_status_name','學籍狀態名稱'),
 ('education_level_code','education_level_code','學制代碼'), ('education_level_name','education_level_name','學制名稱')]


doc('00_串接總覽', '串接方式與交付清單', '先選接法，再交換設定', [
 h('要達成哪一種需求？')+table(['需求／方式','本地端需做什麼','取得資料'],[
 ('標準 OIDC 登入','支援 Code＋PKCE 的後端用戶端','ID Token 與授權身分欄位'),
 ('Web API 單一登入','登入啟動頁、回呼、後端兌換','JSON 身分資料；不是 Access Token'),
 ('SAML 2.0 登入','SP 模組、簽署請求、ACS','簽章 Assertion 與授權屬性'),
 ('JSON 帳密驗證','後端送使用者帳密驗證','只有 verified 布林值'),
 ('舊 SOAP 相容介面','按方法及 WSDL 實作呼叫','陣列或 DataSet；目前 9 個方法'),
 ('一般首頁連結','提供 HTTPS 網址','不傳送身分資料、不建立 SSO')])+
 flow([('使用者瀏覽身分專區','學生、教職員、校友專區可先瀏覽；私人服務依登入及授權顯示。'),('點選系統入口','OIDC／Web API／SAML 應進接收系統的登入啟動頁。'),('接收系統發起中央驗證','中央有有效登入就沿用；沒有時顯示中央登入頁。'),('接收系統驗證結果並建立自己的登入','帳號對應與業務權限由接收系統負責。')])+
 note('「Web API 單一登入」與「JSON 帳密驗證」是兩套不同介面。前者不用把使用者密碼交給對方；後者由對方收集帳密送來驗證，不會因此取得中央登入。'),
 h('雙方需要交換哪些資訊？')+table(['提供方','交付項目'],[
 ('接收系統','系統名稱、用途、承辦人、開發／維運窗口與聯絡方式；測試及正式網域。'),
 ('接收系統','登入啟動網址、精確回呼網址；SAML 另交 SP Entity ID、ACS 與公開簽章憑證。'),
 ('API／SOAP 呼叫端','實際對外來源 IP、需要的方法與欄位；SOAP 指定方法另需呼叫帳密或 fnc_id。'),
 ('SSO 管理者','實際 SSO 基底網址、系統代碼、所選協定、啟用狀態及允許提供欄位。'),
 ('SSO 管理者','適用模式的 Client Secret、Discovery／metadata／WSDL、合成測試帳號。密鑰另行安全交付。'),
 ('雙方確認','帳號對應方式、未開通帳號處理、權限、Session 期限、登出範圍與驗收窗口。')])+
 h('網址填法')+table(['用途','示意'],[('SSO 基底網址',BASE+'/'),('接收系統登入啟動',APP+'/login'),('OIDC 回呼',APP+'/signin-oidc'),('Web API 回呼',APP+'/sso/callback'),('SAML ACS',APP+'/saml/acs')])+
 p('入口不填中央 /account/login，也不把回呼當作啟動頁。回呼需精確登記，包含 scheme、網域、埠號及路徑；測試與正式各自登記。若要另開分頁，入口可使用 target="_blank" 與 rel="noopener"，整個登入流程在新分頁完成。')+SESSION+
 h('交接與驗收')+items('使用核准測試帳號完成正常、失敗、停用、重放及欄位授權測試；詳細案例見各模式文件。','核對本地帳號與權限、登入期限及登出行為後，再安排測試與正式環境的交接。')+COMMON
], ['Controllers/PortalServicesController.cs','Services/SsoSystemRegistrationService.cs','Services/IntegrationClientRegistrationService.cs'])


doc('01_OIDC單一登入','OIDC 單一登入','Authorization Code ＋ PKCE S256',[
 p('適合已具備 OIDC 模組或能安裝 OIDC 用戶端套件的接收系統。本版是後端保存密鑰的機密用戶端；純前端 SPA 需由後端處理登入。')+
 flow([('瀏覽器 → 接收系統 /login','套件產生並保存 state、nonce、PKCE verifier，綁定此次瀏覽器請求。'),('瀏覽器 → 中央 /connect/authorize','帶 client_id、redirect_uri、scope、state、nonce、PKCE challenge。'),('中央檢查登入狀態','有有效登入就沿用；否則在中央輸入帳密。'),('中央 → 接收系統回呼','瀏覽器帶回 code 與 state；接收端先驗證關聯。'),('接收系統後端 → 中央 /connect/token','使用授權碼、verifier 與用戶端驗證兌換。'),('接收系統建立本地登入','套件驗證 ID Token；以 iss＋sub 對應帳號，再檢查業務權限。')])+
 table(['設定','值'],[('Discovery',BASE+'/.well-known/openid-configuration'),('Authorization／Token','GET /connect/authorize；POST /connect/token'),('response_type／PKCE','code／S256'),('Scope','openid 必須；需要時加 profile、student_profile'),('回呼／用戶端驗證','精確 HTTPS 回呼；Client ID 與 Client Secret 留在後端')])+COMMON,
 h('瀏覽器請求及後端兌換欄位')+table(['階段','欄位與規則'],[
 ('授權請求','client_id、redirect_uri、response_type=code、scope=openid…、state、nonce、code_challenge、code_challenge_method=S256。由套件建立與編碼。'),
 ('回呼','成功 code＋state；錯誤可能為 error。先驗 state／關聯，不因收到參數就視為成功。'),
 ('Token POST','application/x-www-form-urlencoded：grant_type=authorization_code、code、redirect_uri、code_verifier；用戶端認證方式依可信 Discovery／套件設定。'),
 ('ID Token 驗證','由套件驗證簽章、iss、aud、exp、nonce 與協定關聯；解析 JWT 內容不等於驗證。'),
 ('本地帳號鍵','使用可信 iss＋sub；不要只按姓名或 email 自動合併帳號。')])+
 h('可取得的身分欄位')+table(['欄位','Scope','意義'],[(a,'profile' if a in ('name','preferred_username') else 'student_profile',c) for a,b,c in FIELDS])+
 p('所有可選欄位同時受該系統的欄位白名單限制；要求 scope 不代表一定取得欄位。固定識別碼 sub 與 iss／aud／exp 等協定欄位由 OIDC 回應提供。空白來源資料不代表未授權，也不可自行猜值。')+
 note('授權碼本版有效 1 分鐘、ID Token 5 分鐘；實際以回應及驗證結果為準。未提供 UserInfo、Refresh Token、PAR 或通用業務 API 授權；不要要求 offline_access，也不要把 ID Token 當作業務 API 通行證。'),
 h('設定範例：交給所使用的 OIDC 套件')+code('''Authority = "https://sso.example.edu/"
ClientId = "YOUR_CLIENT_ID"
ClientSecret = secretStore.Read("SSO_CLIENT_SECRET")
ResponseType = "code"
UsePkce = true
RedirectUri = "https://app.example.edu/signin-oidc"
Scopes = ["openid", "profile", "student_profile"]
GetClaimsFromUserInfoEndpoint = false
MapInboundClaims = false  // 若套件具備此選項，保留原 claim 名稱''')+
 p('以上是套件設定示意，不是可直接執行的程式。由框架處理 challenge、回呼、state／nonce、PKCE 與 Token 驗證；使用框架認證成功事件執行本地帳號查找。')+
 code('''onValidatedIdentity(identity):
    account = findLocalAccount(identity.issuer, identity.subject)
    if account is missing or account.disabled:
        reject("帳號尚未開通或已停用")
    checkLocalPermissions(account)
    createLocalSession(account, configuredLocalExpiry)
    redirectToAllowedLocalPage()''')+
 h('測試及登出')+CHECKS+SESSION+
 p('中央登出端點 /connect/logout：GET 顯示確認，實際登出由 POST 與防偽驗證執行。請先訂定接收系統自己的登出行為與登入期限。')+
 p('規範參考：<a href="https://openid.net/specs/openid-connect-core-1_0.html">OpenID Connect Core</a>、<a href="https://www.rfc-editor.org/rfc/rfc7636">RFC 7636（PKCE）</a>。本專案支援範圍以本文件及部署端 Discovery 為準。')
],['Program.cs','Controllers/OidcAuthorizationController.cs','Services/OidcIdentityClaims.cs','Services/IdentityClaimReleasePolicy.cs'])


doc('02_WebAPI單一登入','Web API 單一登入','JSON／一次性票券 ＋ PKCE；本專案自訂協定',[
 p('對方需準備兩個瀏覽器入口：登入啟動頁及回呼頁；再由對方後端呼叫中央兌換 API。中央不會拿使用者帳密 POST 到對方網站。')+
 flow([('瀏覽器 → 接收系統 /login','產生 state、verifier，保存於後端且綁定當次瀏覽器登入請求。'),('瀏覽器 → 中央 /ticket/authorize','帶 client_id、redirect_uri、state、challenge 與 S256。'),('中央沿用登入或顯示登入頁','通過後核發最長 60 秒、只能使用一次的票券。'),('中央 → 接收系統 /sso/callback','瀏覽器帶 ticket＋state；接收端驗證 state、期限與瀏覽器關聯。'),('接收系統後端 → POST /api/sso/v1/redeem','HTTP Basic＋JSON ticket、redirect_uri、code_verifier。'),('JSON 身分 → 接收系統本地登入','檢查 issuer、client_id、期限；以 issuer＋sub 對應帳號並查權限。')])+
 h('申請設定')+items('提供精確登入啟動網址與回呼網址；取得 Client ID、一次顯示的 Client Secret 與 SSO 基底網址。','請管理者在 SSO 系統設定選「Web API 單一登入」，啟用系統並勾選允許提供的身分欄位。','Client Secret 與 verifier 只保存在接收系統後端；回呼完成清除登入請求並轉到不含 ticket 的網址。')+COMMON,
 h('請求欄位')+table(['端點／位置','欄位','規則'],[
 ('GET /ticket/authorize','client_id／redirect_uri','登記系統代碼／精確 HTTPS 回呼'),
 ('Query','state','32～128 字元隨機 Base64Url；綁定瀏覽器及本次請求'),
 ('Query','code_challenge','SHA-256(verifier) 的 Base64Url，43 字元、不含 ='),
 ('Query','code_challenge_method','固定 S256'),
 ('GET 接收端回呼','ticket／state','驗 state 後再兌換；ticket 是 43 字元 Base64Url'),
 ('POST /api/sso/v1/redeem','HTTP Basic','帳號 Client ID；密碼 Client Secret'),
 ('JSON body','ticket／redirect_uri／code_verifier','verifier 為本次原值，43～128 字元；Content-Type: application/json')])+
 h('成功回應：JSON 物件，不是 Bearer Token')+table(['欄位','型別／說明'],[
 ('issuer／client_id／sub','string；固定發行者、接收系統代碼、穩定使用者識別碼'),
 ('authenticated_at／session_expires_at','number；Unix 秒（UTC），不是毫秒；建立中央登入時間與當下有效期限'),
 *[(b,'string；'+c) for a,b,c in FIELDS]])+
 p('未授權的可選欄位會省略；學籍欄位還受 IncludeStudentProfile 設定限制。不要把缺少的欄位視為一定有值；缺少業務必需資料時拒絕或導向申請流程。'),
 h('登入啟動與回呼：語言中立示意')+code('''startLogin(browserSession):
    state = base64url(randomBytes(32))
    verifier = base64url(randomBytes(32))
    challenge = base64url(SHA256(ASCII(verifier)))
    savePending(browserSession, state, verifier, shortExpiry)
    redirect(SSO + "/ticket/authorize?" + urlEncode({
        client_id: CLIENT_ID, redirect_uri: CALLBACK,
        state: state, code_challenge: challenge,
        code_challenge_method: "S256" }))

callback(browserSession, ticket, state):
    pending = atomicallyTakePending(browserSession, state)
    require(pending exists and not expired)
    data = backendPostJson(SSO + "/api/sso/v1/redeem",
        basicAuth(CLIENT_ID, SECRET), {
        ticket: ticket, redirect_uri: CALLBACK,
        code_verifier: pending.verifier })
    require(HTTP == 200 and validJsonSchema(data))
    require(data.issuer == EXPECTED_ISSUER)
    require(data.client_id == CLIENT_ID and data.sub != "")
    require(data.session_expires_at > nowUnixSeconds())
    account = requireEnabledLocalAccount(data.issuer, data.sub)
    createLocalSession(account, min(localExpiry, data.session_expires_at))
    redirectToAllowedLocalPage()''')+
 p('base64url 使用 -、_ 並移除 =。每次登入都產生新的隨機值；多分頁以 state 分別保存，不覆蓋同一筆 verifier。原子取出失敗、兌換逾時或重複回呼時，重新發起登入，不能把舊票券當成功。')+
 h('HTTP 兌換範例')+code('''POST /api/sso/v1/redeem HTTP/1.1
Host: sso.example.edu
Authorization: Basic BASE64_OF_CLIENT_ID_COLON_SECRET
Content-Type: application/json

{"ticket":"CALLBACK_TICKET", "redirect_uri":"https://app.example.edu/sso/callback",
 "code_verifier":"SAVED_VERIFIER"}''')+
 note('範例字串均為占位符，不符合正式票券長度；需代入當次實際值。JSON 兌換端點與舊表單 /ticket/redeem 共用一次性消耗狀態，不能各兌換一次。'),
 h('回應範例及失敗處理')+code('''{
  "issuer": "https://sso.example.edu/",
  "client_id": "YOUR_CLIENT_ID",
  "sub": "stable-user-id",
  "authenticated_at": 1792368000,
  "session_expires_at": 1792369800,
  "username": "S000001",
  "name": "測試學生",
  "identity_type": "student"
}''')+p('時間與人員資料均為示意；username、name、identity_type 只有被授權才提供。驗證依賴與受信任中央的 HTTPS 後端通訊，這個 JSON 本身不是簽章 JWT。')+
 table(['情境','處理'],[('400 invalid_request','檢查欄位、字元及長度；不要無限重送。'),('400 invalid_client_or_redirect','授權入口的系統未啟用或回呼不符；核對登記。'),('400 session_inactive','中央登入不可用；重新發起登入。'),('401 invalid_client','後端兌換的系統驗證失敗；核對 ID／密鑰。'),('400 invalid_ticket','逾時、重放、verifier／回呼不符或登入已失效；重新登入。'),('413／415／網路或服務錯誤','請求上限 8 KiB；使用 application/json，非 200 一律不建立登入。')])+
 h('驗收重點')+CHECKS+SESSION
],['Controllers/WebApiSsoController.cs','ApiModels/SsoAuthorizeRequest.cs','ApiModels/SsoTicketRedemptionRequest.cs','ApiModels/SsoIdentityResponse.cs','Services/LoginTicketService.cs'])


doc('03_SAML單一登入','SAML 2.0 單一登入','接收系統是 SP；中央 SSO 是 IdP',[
 flow([('瀏覽器 → SP 登入啟動頁','SP 保存 Request ID 與本次瀏覽器關聯。'),('SP → 瀏覽器 → IdP /saml/sso','HTTP-Redirect；以 SP 私鑰簽署 SAMLRequest 查詢字串。'),('IdP 驗請求及中央登入','核對 SP、簽章、ACS、Destination、時間與重放。'),('IdP → 瀏覽器 → SP 的 ACS','HTTP-POST：SAMLResponse 與 RelayState。'),('SP 套件驗證回應','信任 IdP 憑證；驗簽章、Issuer、Audience、Recipient、期限與 InResponseTo。'),('SP 建立本地登入','使用 IdP Entity ID＋NameID 對應帳號，依本地權限進入服務。')])+
 table(['交換資料','內容'],[('IdP metadata／Entity ID',BASE+'/saml/metadata'),('IdP SSO URL',BASE+'/saml/sso'),('SP 提供','SP Entity ID、精確 HTTPS ACS、登入啟動網址'),('SP 公開憑證','有效的 PEM RSA 簽章憑證，至少 2048 位元；私鑰留在 SP'),('IdP 公開憑證','由可信 metadata 匯入；中央管理者另核對來源及憑證輪替')])+
 p('中央目前不自動下載 SP metadata 建立信任；由管理者登記。請確認 SP 模組能採用本版的 Binding 與簽章要求。')+COMMON,
 h('請求與回應格式')+table(['方向','欄位與規則'],[
 ('Redirect 查詢','SAMLRequest、RelayState（選用）、SigAlg、Signature；RSA-SHA256 簽署查詢字串，交由 SAML 套件處理壓縮、編碼與簽章。'),
 ('AuthnRequest','唯一 ID、IssueInstant、Version=2.0、Issuer=SP Entity ID、Destination=IdP SSO URL；ACS 指定時須完全相符。'),
 ('RelayState','UTF-8 最多 80 bytes；使用短隨機關聯值，不塞個資或任意外部返回網址。'),
 ('ACS POST','SAMLResponse（Base64 XML）、RelayState；驗證成功才能建立本地登入。'),
 ('回應驗證','成功 Response 與 Assertion 都有簽章；檢查 InResponseTo 對應原始請求且僅接受一次。'),
 ('固定識別','NameID 使用 persistent 格式，值為穩定帳號 ID。')])+
 h('允許提供的 Attribute')+table(['Attribute Name','說明'],[(a,c) for a,b,c in FIELDS])+
 p('SAML 的帳號欄位仍為 preferred_username；Web API JSON 才使用 username。屬性受系統欄位白名單限制；不使用 OIDC scope。')+
 note('本版支援 SP 發起的基本登入；不提供完整 SLO、IdP 主動登入或加密 Assertion。ForceAuthn=true、索引型 ACS／AttributeConsumingServiceIndex 等不支援。未登入時 IsPassive 不能跳出互動登入。'),
 h('SP 套件設定示意')+code('''IdpMetadataUrl = "https://sso.example.edu/saml/metadata"
SpEntityId = "https://app.example.edu/saml"
AcsUrl = "https://app.example.edu/saml/acs"
RequestBinding = "HTTP-Redirect"
ResponseBinding = "HTTP-POST"
SignAuthnRequest = true
SignatureAlgorithm = "RSA-SHA256"
NameIdPolicy = "persistent"
SpPrivateKey = secretStore.Read("SP_SIGNING_PRIVATE_KEY")''')+
 p('設定名稱依 SP 套件而異，上例是概念設定。ACS 不是一般首頁，須由套件處理 SAMLResponse；禁止自行只讀 NameID 就視為通過驗證。')+
 h('ACS 處理順序')+items('核對瀏覽器關聯及 RelayState；驗證可信 IdP 的 Response／Assertion 簽章。','驗證 Issuer、Audience、Destination／Recipient、NotBefore／NotOnOrAfter 及 InResponseTo。','確認成功狀態並拒絕重放；對應本地帳號、檢查停用及業務權限。','建立自己的 Cookie，導向原先允許的站內頁面；不保存完整 Assertion 到一般日誌。')+
 h('時間及測試')+p('本版 AuthnRequest 允許過去 5 分鐘至未來 30 秒的 IssueInstant；Assertion 與 SubjectConfirmation 有效期設定 2 分鐘，接收端依回應驗證。兩端須同步時間。')+CHECKS+SESSION+
 p('規範參考：<a href="https://docs.oasis-open.org/security/saml/v2.0/saml-profiles-2.0-os.pdf">OASIS SAML 2.0 Profiles</a>。實際相容能力需以 SP 套件及雙方互通測試確認。')
],['Controllers/SamlSsoController.cs','Services/SamlIdentityProvider.cs','Services/IdentityClaimReleasePolicy.cs'])


doc('04_JSON帳密驗證','JSON 帳密驗證 API','僅驗證帳密；不建立中央 SSO',[
 p('適合接收系統已有自己的登入頁，確實需要由後端驗證使用者帳密。若希望使用者在中央登入一次就能進入多系統，應改用前述三種單一登入方式。')+
 flow([('使用者 → 接收系統登入頁','使用者輸入校園帳號與密碼。'),('接收系統後端 → 中央驗證 API','HTTPS＋X-Client-Id／Secret＋JSON 使用者帳密。'),('中央驗證呼叫資格','檢查服務開關、系統啟用、來源 IP、服務授權與系統密鑰。'),('中央驗證使用者帳密','套用帳號狀態及錯誤鎖定；回傳 verified。'),('接收系統判斷並建立自己的登入','僅 HTTP 200 且 verified === true 可繼續；權限由接收端檢查。')])+
 h('先向 SSO 管理者申請')+items('建立「帳密驗證 API／JSON」登記，授予帳密驗證服務；取得 Client ID 與一次顯示的 Client Secret。','提供後端實際連出 IP。接受逐筆 IPv4／IPv6，不接受 * 或網段；代理／NAT 要核對中央實際看到的 IP。','管理者啟用 JSON 服務與系統登記。端點預設關閉，關閉時 HTTP 503。')+COMMON,
 h('請求與回應')+table(['位置','欄位','內容'],[('Endpoint','POST',BASE+'/api/integration/v1/verify'),('Header','Content-Type','application/json'),('Header','X-Client-Id／X-Client-Secret','呼叫系統代碼／系統密鑰；不同於使用者帳密'),('JSON','userName／password','string；帳號不可空白且最多 100 字元，密碼不可空且最多 256 字元'),('HTTP 200 JSON','verified','boolean；true 通過、false 不通過；沒有姓名或其他個資')])+
 code('''POST /api/integration/v1/verify HTTP/1.1
Host: sso.example.edu
Content-Type: application/json
X-Client-Id: YOUR_CLIENT_ID
X-Client-Secret: YOUR_SECRET

{"userName":"TEST_ACCOUNT","password":"TEST_PASSWORD"}

// 成功處理時的 JSON body（二選一；不含本行註解）
{"verified":true}
{"verified":false}''')+
 table(['HTTP','意義／接收端處理'],[('400','JSON 錯誤或空請求；修正格式。'),('403','系統未授權、停用、來源 IP 或密鑰不符。'),('413／415','超過 16 KiB／Content-Type 不支援。'),('429','同來源 IP 每分鐘 60 次限制；延後並避免重試風暴。'),('503','服務未啟用或來源暫時不可用；顯示稍後再試，不能當登入成功。')])+
 note('沒有提供 sub、姓名、身分欄位或 Token，也不會建立中央 Cookie。若需要穩定識別碼及個人資料，請另選適用的 SSO 方式。'),
 h('後端處理示意')+code('''response = backendPostJson(API, clientHeaders, userCredentials)
if response.status != 200: showServiceError(); stop
if response.json.verified is not true: rejectLogin(); stop
account = requireEnabledLocalAccount(submittedUserName)
checkLocalPermissions(account)
createLocalSession(account)
discardPassword()''')+
 h('接收端驗收清單')+items('正常、錯誤、空白帳密及停用帳號。','錯誤系統密鑰、未授權 IP、停用系統、未勾服務資格。','HTTP 200＋false 與 HTTP 503 的處理要不同，但都不可登入。','密鑰更新後，舊密鑰立即失效。密鑰及使用者密碼不可放在前端或網址。','不得把使用者密碼持久化、寫入日誌或錯誤追蹤；驗證完成即釋放。','來源中斷或驗證逾時不自動降級成成功。')+
 h('操作限制')+p('同一使用者五分鐘內連續失敗五次會鎖定五分鐘，JSON／SOAP 與目前校務登入共用嘗試紀錄。不要用真實帳號進行壓力或密碼猜測測試；聯調使用核准的合成測試帳號。')+
 SESSION+COMMON
],['Controllers/CredentialVerificationApiController.cs','ApiModels/CredentialVerificationRequest.cs','Services/CredentialVerificationService.cs','Services/IntegrationClientRegistrationService.cs'])


catalog=json.loads((SERVER/'Services/SoapDefinitions/legacy-soap-operations.json').read_text(encoding='utf-8-sig'))
available=['KuasSSO_Certify2','KuasSSO_Certify1','KuasSSO_SchCardSearch','Get_Traffic_Stud','Get_Traffic_Stud_Temp','Get_Traffic_Teach','Get_Traffic_Teach_Temp','Get_Traffic_Pub','NkustSSO_DocumentApplyPaymentSystemSearch']
names={o['Name']:o for o in catalog}
implemented_source=(SERVER/'Services/LegacySoapOperationCatalog.cs').read_text(encoding='utf-8-sig')
assert all(name in implemented_source for name in available)
assert {o['Name'] for o in catalog if o.get('Implemented')} == set(available), '請重新核對 SOAP 支援清單'

doc('05_SOAP串接總覽','舊 SOAP 相容介面','SOAP 1.1／1.2；目前 9 個可執行方法',[
 p('提供已核對的舊方法形狀，讓既有系統保留 SOAP 呼叫方式。不是所有舊 .asmx 方法都已移植；可執行清單以部署端 WSDL 與本文件為準。')+
 flow([('接收系統後端 → SOAP POST','取得 WSDL，使用正確命名空間、方法及參數大小寫。'),('中央檢查來源與方法資格','實際來源 IP、系統啟用、服務開關與逐方法授權。'),('必要時驗證呼叫系統帳密','交通方法使用 usr_id／usr_pwd；卡片與文件方法使用 SearchUsr_ 欄位。'),('執行驗證或資料查詢','新校務資料唯讀；套用每個方法的欄位授權。'),('SOAP 回傳 → 接收端','解析 ArrayOfAnyType 或 DataSet；HTTP 200 不保證業務成功。')])+
 table(['位置','用途'],[('POST '+BASE+'/KuasSSOWebService.asmx','唯一 SOAP 呼叫端點'),(BASE+'/KuasSSOWebService.asmx?wsdl','下載可執行 WSDL'),(BASE+'/KuasSSOWebService.asmx?op=方法名','瀏覽方法說明；不能用 GET 帶帳密呼叫'),('Namespace','http://tempuri.org/（這是 XML 命名空間，不是傳輸網址）')])+COMMON,
 h('傳輸格式')+table(['項目','SOAP 1.1','SOAP 1.2'],[('Content-Type','text/xml; charset=utf-8','application/soap+xml; charset=utf-8; action="http://tempuri.org/方法名"'),('Envelope namespace','http://schemas.xmlsoap.org/soap/envelope/','http://www.w3.org/2003/05/soap-envelope'),('Action','SOAPAction: "http://tempuri.org/方法名"','放在 Content-Type action；若提供須與 Body 一致')])+
 h('必備授權與回傳規則')+items('管理者登記 SOAP、實際來源 IP、允許方法及允許回傳欄位。SOAP 同一來源 IP 只能對應一筆啟用登記；共用 NAT 無法僅靠舊參數分辨應用。','Certify2 驗使用者帳密；Certify1 驗舊校務票據；其餘方法另需要登記的呼叫系統帳密。三者不可混用。','陣列以固定位置解析。DataSet 使用 XSD＋DiffGram，建議用 WSDL 產生代理或 DataSet 支援工具，不能當普通 JSON。','個資欄位須逐方法授權；未授權通常保留欄位但清空值。pwd 固定空字串，不提供密碼或密碼摘要。','失敗 DataSet 可能只有 message 欄，不可先直接讀取 std_id 等成功欄。')+
 table(['失敗層級','處理'],[('HTTP 403','來源或方法未授權。'),('HTTP 503','SOAP 服務未啟用。'),('HTTP 500／SOAP Fault','請求格式、尚未支援的方法或來源不可用；不能視為成功。'),('HTTP 413／415／429','大小、Content-Type 或頻率不符；上限 16 KiB、每 IP 每分鐘 60 次。'),('HTTP 200 業務失敗','依各方法的 boolean、NoPass、400／500／501 判定，不以 HTTP 200 判成功。')])+
 note('不支援帶帳密的 GET、舊表單 POST 繫結或額外 SOAP Header。from_Clientip 不是授權依據，來源以中央實際連線 IP 判定。'),
 h('已實作方法與個別文件')+table(['方法','用途／回傳'],[
 ('KuasSSO_Certify2','使用者帳密驗證；boolean＋來源身分代碼'),('KuasSSO_Certify1','舊校務票據查驗；boolean＋狀態'),('KuasSSO_SchCardSearch','呼叫系統驗證後驗使用者帳密；三元素陣列'),('Get_Traffic_Stud／Get_Traffic_Stud_Temp','學生查詢；DataSet，兩者保留個別方法名稱與授權'),('Get_Traffic_Teach／Get_Traffic_Teach_Temp','教職員查詢；DataSet'),('Get_Traffic_Pub','公用帳號查詢；DataSet'),('NkustSSO_DocumentApplyPaymentSystemSearch','身分證號＋生日＋身分別查詢；DataSet，可能多列')])+
 p('本資料夾的 PDF/soap-methods 另有 9 份方法 PDF，每份提供輸入欄位、SOAP 範例、輸出欄位及判定規則。其餘 '+str(len(catalog)-len(available))+' 個舊方法尚未開放，不可因說明頁列有名稱就直接呼叫。')+
 h('聯調時必測')+items('核對舊程式使用 SOAP 1.1 或 1.2、參數大小寫、ArrayOfAnyType 型別及 DataSet 解析方式。','逐方法測試正常結果、錯誤呼叫帳密、未授權 IP、取消欄位授權、來源中斷。','交通類測試查無、重複或缺漏資料；文件方法測試多列結果；Certify1 測試逾時與重放。','未授權欄位空白及 pwd 空白時，接收端不得誤判為完整資料或繼續使用舊快取權限。')+
 note('舊 SOAP 的校務票據與 Web API 的 ticket 是不同來源、格式與驗證端點。不得把 /ticket/authorize 取得的 ticket 送入 KuasSSO_Certify1。')+COMMON
],['Controllers/CredentialVerificationApiController.cs','Services/LegacySoapContract.cs','Services/LegacySoapOperationCatalog.cs','Services/LegacySoapOperationDispatcher.cs','Services/IntegrationReturnFields.cs'])


doc('06_一般首頁連結','一般首頁連結','只開啟網站；不傳送登入身分',[
 flow([('使用者瀏覽身分專區','公開入口不要求登入；私人入口依中央登入與可見條件顯示。'),('使用者點選一般連結','可依設定先要求中央登入，但這只保護入口。'),('瀏覽器開啟目標網址','不自動 POST 帳密、票券或身分欄位。'),('目標網站自行處理','公開頁直接顯示；需要登入時由目標網站自己的機制決定。')])+
 table(['提供資訊','用途'],[('名稱與詳述／中英文內容','服務卡片及滑鼠／鍵盤說明'),('HTTPS 網址','實際目標頁'),('分類、順序、顯示身分','管理者安排入口位置及可見條件'),('承辦人／技術窗口','網址或服務異動時聯絡')])+
 h('要免重複輸入帳密時')+p('請改用 OIDC、Web API 單一登入或 SAML，並把入口設成接收系統的登入啟動頁。只勾「需中央登入」或把網站放進選單，不能使目標網站自動取得登入狀態。')+
 h('簡單連結範例')+code('<a href="https://app.example.edu/" target="_blank" rel="noopener noreferrer">開啟服務</a>')+
 p('中央入口可在新分頁開啟目標網站；瀏覽器 Cookie 能否共用仍受網域及瀏覽器規則影響，不能依「瀏覽器未關閉」判定所有系統已登入。')+COMMON
],['Controllers/PortalServicesController.cs','Models/RegisteredSystem.cs'])


# 每一個已移植 SOAP 方法都提供可獨立交付的兩頁說明。
traffic_student=[('message','固定','200 成功；500 查無資料；501 呼叫帳密錯誤'),('std_id','授權','學號'),('std_name','授權','姓名'),('std_idno','授權','身分證號 MD5；仍屬可識別資料'),('unt_id','授權','單位代碼（沿用部門代碼語意）'),('unt_name_abr','授權','單位簡稱'),('types','授權','學生 S'),('usr_id','授權','校務登入帳號'),('pwd','禁止','固定空字串'),('std_email','授權','電子郵件')]
traffic_teacher=[(n.replace('std_','emp_'),a,d.replace('學號','員工編號').replace('學生 S','教職員 T')) for n,a,d in traffic_student]
traffic_pub=[('message','固定','200 成功；500 查無資料；501 呼叫帳密錯誤'),('pubusr_id','授權','公用帳號'),('pubusr_name','授權','帳號名稱'),('pubusr_idno','授權','原始識別資料；不是 MD5'),('pub_name_abr','授權','名稱簡稱'),('types','授權','公用帳號 P'),('pwd','禁止','固定空字串'),('usr_email','授權','電子郵件')]
titles={'KuasSSO_Certify2':'使用者帳密驗證','KuasSSO_Certify1':'舊校務票據驗證','KuasSSO_SchCardSearch':'智慧卡帳密驗證','Get_Traffic_Stud':'學生資料查詢','Get_Traffic_Stud_Temp':'學生資料查詢（Temp）','Get_Traffic_Teach':'教職員資料查詢','Get_Traffic_Teach_Temp':'教職員資料查詢（Temp）','Get_Traffic_Pub':'公用帳號資料查詢','NkustSSO_DocumentApplyPaymentSystemSearch':'文件申請／繳費資料查詢'}
for index,method in enumerate(available,1):
    operation=names[method]
    traffic=method.startswith('Get_Traffic_')
    params=[]
    sample={}
    for field in operation['Parameters']:
        name=field['Name']
        desc=field['Description']
        if traffic and name in ('usr_id','usr_pwd'):
            desc='呼叫系統帳號（不是被查詢人的帳號）' if name=='usr_id' else '呼叫系統密碼（不是被查詢人的密碼）'
        if name in ('searchID','searchid'):
            desc='被查詢人的學號／員編／公用帳號，或原始識別號；不是 MD5。保留參數大小寫。'
        params.append((name,'string',escape(desc)))
        sample[name]=('CALLER_PASSWORD' if name in ('SearchUsr_pwd',) or traffic and name=='usr_pwd' else
                      'CALLER_ACCOUNT' if name=='SearchUsr_id' or traffic and name=='usr_id' else
                      'TEST_PASSWORD' if name in ('usr_pwd','auth_pwd') else
                      'TEST_ACCOUNT' if name in ('usr_id','auth_id') else
                      'REGISTERED_FUNCTION_ID' if name=='fnc_id' else
                      'LEGACY_TICKET' if name=='Rnd_id' else
                      'STUD' if name=='identity' else
                      'SOURCE_BIRTHDAY' if name=='Birthday' else
                      'TEST_ID_NUMBER' if name=='IDNO' else
                      '192.0.2.10' if name=='from_Clientip' else 'TEST_LOOKUP_VALUE')
    if method=='KuasSSO_Certify2':
        rows=[('位置 0','固定','boolean；true／false'),('位置 1','授權／固定','成功：授權的來源身分代碼，未授權為空字串；失敗固定 "500"')]
        rule='以位置 0 的 boolean 判定。來源身分代碼例如 02，不自動轉成 student。此方法不建立中央登入或提供其他個資。'
        response='KuasSSO_Certify2Result → [true, "02"]\n失敗 → [false, "500"]\n成功但不授權身分 → [true, ""]'
    elif method=='KuasSSO_Certify1':
        rows=[('位置 0','固定','boolean；票據是否通過'),('位置 1','授權／固定','成功：來源身分代碼或空字串；失敗 "NoPass"')]
        rule='必須來自既有校務票據發行流程，fnc_id 在此呼叫端白名單內；兩分鐘內且尚未使用。此接口不核發票據，不接受 Web API 的 ticket。'
        response='KuasSSO_Certify1Result → [true, "02"]\n過期／重放／功能不符 → [false, "NoPass"]'
    elif method=='KuasSSO_SchCardSearch':
        rows=[('位置 0','固定','string：200 成功；400 呼叫帳密錯誤；500 使用者驗證失敗'),('位置 1','固定','結果說明文字'),('位置 2','固定','boolean；驗證結果')]
        rule='先驗 SearchUsr_id／SearchUsr_pwd，再驗 auth_id／auth_pwd。from_Clientip 不取代來源 IP 管制。不提供額外個資欄位。'
        response='成功 → ["200", "登入成功!", true]\n呼叫失敗 → ["400", "管理者登入失敗!", false]\n驗證失敗 → ["500", "查詢使用者登入失敗!", false]'
    elif traffic:
        rows=traffic_student if 'Stud' in method else traffic_teacher if 'Teach' in method else traffic_pub
        rule='先驗呼叫系統帳密，再依查詢值取得唯一對應。查無回 message=500；多筆衝突或來源不可用可能回 Fault，不任取第一筆。'
        if method=='Get_Traffic_Stud': rule+=' 本方法參數名稱是小寫 searchid。範圍含當學期與符合原條件的歷年資料，不代表只有目前在學生。'
        if method=='Get_Traffic_Stud_Temp': rule+=' Temp 的歷年分支僅取在學狀態；與一般學生方法需分別授權，不可假設結果完全相同。'
        if 'Teach' in method: rule+=' 一般及 Temp 目前共用教職員查詢，仍分別授權。'
        if method=='Get_Traffic_Pub': rule+=' 公用帳號限定帳號包含 office，pubusr_idno 為原識別資料，不是摘要。'
        response='DataSet / Table1（邏輯結果示意，實際為 XSD＋DiffGram）\n成功：message="200"，以及下表中被授權的資料值\n查無：只有 message="500"\n呼叫帳密錯誤：只有 message="501"'
    else:
        rows=[('message','固定','200 成功；500 無符合資料；501 呼叫帳密錯誤'),('std_id','授權','學號／員工編號，可能回傳多列'),('std_name','授權','姓名'),('std_email','授權','電子郵件')]
        rule='identity 僅接受 STUD、Employee、GRADSTUD（區分大小寫）。Birthday 依來源生日字串比對，1～7 字元，不自動轉換日期。GRADSTUD 是略過當期學籍的舊查詢分支，不保證每列都已畢業。'
        response='DataSet / Table1（邏輯結果示意）\n成功：message="200", std_id, std_name, std_email\n可有多列；教職員仍使用 std_ 前綴\n查無：message="500"；呼叫帳密錯誤：message="501"'
    xml='POST /KuasSSOWebService.asmx HTTP/1.1\nContent-Type: text/xml; charset=utf-8\nSOAPAction: "http://tempuri.org/'+method+'"\n\n<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">\n  <soap:Body>\n    <'+method+' xmlns="http://tempuri.org/">\n'+''.join('      <'+k+'>'+v+'</'+k+'>\n' for k,v in sample.items())+'    </'+method+'>\n  </soap:Body>\n</soap:Envelope>'
    doc('soap-methods/'+f'{index:02d}_'+method,titles[method],method,[
        p('呼叫端點：'+BASE+'/KuasSSOWebService.asmx<br>WSDL：同網址加 ?wsdl。所有輸入為 XML 字串欄位，必須保留拼字與大小寫。')+
        flow([('接收系統準備請求',''),('中央執行 '+('帳密／票據驗證' if not traffic and 'Document' not in method else '資料查詢'),''),('接收端解析結果','')], compact=True)+
        p('先登記來源 IP、此方法及允許欄位。接收端先檢查 HTTP／Fault，再判斷業務結果。')+
        h('輸入參數')+table(['名稱','型別','意義'],params)+h('判定規則')+p(rule)+
        h('回傳欄位')+table(['欄位／位置','提供條件','說明'],rows)+
        note('個資未授權時保留位置／欄位但值為空；失敗 DataSet 可只有 message。未取得資料不得自行補值。'),
        h('SOAP 1.1 請求範例')+code(xml)+p('以上均為占位符；Birthday、票據及帳密須代入核准測試資料，不能原樣送出。SOAP 1.2 改用其 Envelope namespace 與 Content-Type action，方法與參數不變。')+
        h('回應判讀示意')+code(response)+
        h('聯調檢查')+items('未授權 IP／方法、錯誤呼叫帳密及來源中斷都不可視為成功。','用 WSDL 產生代理解析陣列型別或 DataSet；不假設 HTTP 200 就表示驗證通過。','驗證未勾欄位為空、密碼欄位不傳送；資料不可送到一般日誌。')+
        note('HTTP 403 為呼叫資格拒絕；503 為服務未啟用；500／SOAP Fault 須另行處理。服務限制：16 KiB、同來源 IP 每分鐘 60 次。此方法不建立中央 Cookie。')
    ],['Services/LegacySoapOperationDispatcher.cs','Services/IntegrationReturnFields.cs','Services/Sources/CampusSoapQueries.cs','Services/SoapDefinitions/legacy-soap-operations.json'])


CSS='''
@page { size: A4; margin: 14mm; }
* { box-sizing:border-box; }
html { background:#e8eef4; }
body { margin:0 auto; width:182mm; color:#233b50; background:white;
 font-family:"Microsoft JhengHei","Noto Sans CJK TC",sans-serif; font-size:12px; line-height:1.65; }
.page { break-after:page; padding:0; }
.page:last-child { break-after:auto; }
header { border-top:5px solid #244968; padding:12px 0 10px; border-bottom:1px solid #c8d6e3; margin-bottom:14px; }
.brand { font-size:10px; letter-spacing:.12em; color:#597287; }
h1 { font-size:26px; line-height:1.35; margin:7px 0 3px; color:#173b56; }
.subtitle { color:#516c81; font-size:12px; overflow-wrap:anywhere; }
h2 { font-size:16px; margin:15px 0 7px; color:#183f5d; break-after:avoid; }
p { margin:8px 0; }
ul { padding-left:19px; margin:8px 0; } li { margin:4px 0; }
aside { padding:9px 12px; background:#f1f6fa; border-left:3px solid #39809b; margin:12px 0; }
table { width:100%; border-collapse:collapse; table-layout:auto; font-size:11px; margin:9px 0; }
th { background:#eaf1f7; text-align:left; color:#1d435f; }
th,td { border:1px solid #cbd8e2; padding:6px 8px; vertical-align:top; overflow-wrap:anywhere; }
tr { break-inside:avoid; } td:first-child { font-weight:600; }
pre { font-family:Consolas,"Microsoft JhengHei",monospace; font-size:10.5px; line-height:1.6;
 white-space:pre-wrap; overflow-wrap:anywhere; padding:12px; margin:9px 0; color:#17374d; background:#f3f6f8; border:1px solid #cfdae4; }
a { color:#195e88; text-decoration:underline; overflow-wrap:anywhere; }
.flow { width:100%; height:auto; display:block; margin:12px 0; font-family:"Microsoft JhengHei",sans-serif; }
footer { font-size:9px; color:#536b80; border-top:1px solid #cbd8e2; padding-top:7px; margin-top:14px; overflow-wrap:anywhere; }
.source { font-size:9px; color:#536b80; }
@media screen { body { padding:20px 0; } .page { margin-bottom:36px; } }
@media print { html { background:white; } body { print-color-adjust:exact; -webkit-print-color-adjust:exact; } }
'''


def write_sources():
    # 後台只從固定目錄載入文件清單，下載時使用代碼而不接受檔案路徑。
    mode_ids = ['overview', 'oidc', 'webapi', 'saml', 'json', 'soap', 'links']
    catalog_items = []
    for i, d in enumerate(DOCS):
        catalog_items.append(dict(
            Id=mode_ids[i] if i < len(mode_ids) else 'soap-' + Path(d['stem']).name.split('_', 1)[1],
            Title=d['title'], Description=d['subtitle'],
            Category='SOAP 方法' if d['stem'].startswith('soap-methods/') else '串接指南',
            FileName=Path(d['stem']).name+'.pdf', Pages=len(d['pages'])))
    (HERE/'catalog.json').write_text(json.dumps(dict(Version=VERSION, Items=catalog_items),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (HERE/'source').mkdir(parents=True,exist_ok=True)
    (HERE/'source/guide.css').write_text(CSS,encoding='utf-8')
    for d in DOCS:
        filename=Path(d['stem']).name
        blocks=[]
        for i,body in enumerate(d['pages'],1):
            sources='；'.join(d['sources']) if i==len(d['pages']) else ''
            blocks.append('<section class="page"><header><div class="brand">NKUST · DEVELOPER INTEGRATION</div><h1>'+escape(d['title'])+'</h1><div class="subtitle">'+escape(d['subtitle'])+'</div></header>'+body+'<footer>'+escape(VERSION)+' · '+escape(d['title'])+f' · {i} / {len(d["pages"])}'+('<div class="source">實作核對：NkustSso.Server/'+escape(sources)+'</div>' if sources else '')+'</footer></section>')
        html='<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8"><title>'+escape(d['title'])+'</title><link rel="stylesheet" href="guide.css"></head><body>'+''.join(blocks)+'</body></html>'
        (HERE/'source'/f'{filename}.html').write_text(html,encoding='utf-8')
    rows=[(f'<a href="PDF/{d["stem"]}.pdf">{escape(d["title"])}</a>',escape(d['subtitle']),len(d['pages'])) for d in DOCS]
    index='<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8"><title>串接文件下載</title><link rel="stylesheet" href="source/guide.css"></head><body><h1>開發人員串接文件</h1>'+p(VERSION)+p('交給廠商時可只提供對應模式的 PDF；需要 SOAP 時再附指定方法。PDF 可獨立開啟，不依賴網路或 HTML 圖檔。')+table(['PDF','內容','頁數'],rows)+p('<a href="開發人員串接PDF全集.zip">下載全部 PDF（ZIP）</a>')+h('如何維護與重新產生')+p('原稿集中在 generate.py；source/ 為產生的 HTML 與樣式。修改規格後執行下列命令，產生 PDF 與 ZIP；不要手改 PDF。')+code('python docs/developer-integration/generate.py\n# 僅重新產生 HTML\npython docs/developer-integration/generate.py --html-only')+p('使用環境既有的 Playwright、Chrome／Chromium；PDF 檢查使用 PyMuPDF。無需連線校務資料庫、開啟網站或讀取本機 appsettings.json。')+COMMON+'</body></html>'
    (HERE/'index.html').write_text(index,encoding='utf-8')


def render():
    from playwright.sync_api import sync_playwright
    import fitz
    with sync_playwright() as pw:
        chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
        browser=pw.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        page=browser.new_page()
        page.emulate_media(media='print')
        for d in DOCS:
            path=HERE/'source'/(Path(d['stem']).name+'.html')
            page.goto(path.as_uri())
            page.evaluate('document.fonts.ready')
            heights=page.locator('.page').evaluate_all('(pages)=>pages.map(p=>p.getBoundingClientRect().height)')
            # A4 扣上下各 14mm：可用 269mm；留 3mm 印刷誤差。
            if max(heights)>266*96/25.4:
                raise RuntimeError(f'頁面內容過長 {d["stem"]}: {heights}')
            dest=HERE/'PDF'/(d['stem']+'.pdf')
            dest.parent.mkdir(parents=True,exist_ok=True)
            page.pdf(path=str(dest),prefer_css_page_size=True,print_background=True,tagged=True,outline=True)
            with fitz.open(dest) as pdf:
                assert len(pdf)==len(d['pages']), (dest,len(pdf),len(d['pages']))
                for n,pdfpage in enumerate(pdf):
                    text=pdfpage.get_text()
                    assert d['title'] in text and len(text)>100, (dest,n,'文字擷取異常')
                    assert '\ufffd' not in text, (dest,n,'缺字')
            print(f'PASS {dest.relative_to(HERE)} ({len(d["pages"])} pages)',flush=True)
        browser.close()
    with zipfile.ZipFile(HERE/'開發人員串接PDF全集.zip','w',zipfile.ZIP_DEFLATED) as z:
        for path in sorted((HERE/'PDF').rglob('*.pdf')):
            z.write(path,path.relative_to(HERE/'PDF'))
    print('PDF files:',len(DOCS),'Total pages:',sum(len(d['pages']) for d in DOCS),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--html-only',action='store_true')
    args=parser.parse_args()
    write_sources()
    if not args.html_only: render()
