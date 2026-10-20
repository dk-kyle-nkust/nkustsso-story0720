"""只讀取舊專案的檔案與專案清單，產出非資訊主管版分析網頁；不載入設定、不連線資料來源。"""
from pathlib import Path
from collections import Counter
import hashlib
import html
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('D:/Code/newnkustprojectforjinean')
OUT = ROOT / 'reports/legacy-project-analysis'
OUT.mkdir(parents=True, exist_ok=True)
DATE = '2026-10-16'
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
evidence = []

def evidence_item(key, path, anchor, label):
    source = SOURCE / path
    raw = source.read_bytes()
    text = raw.decode('utf-8-sig')
    pos = text.index(anchor)
    item = dict(id=key, path=path, line=text[:pos].count('\n') + 1,
                label=label, sha256=hashlib.sha256(raw).hexdigest())
    evidence.append(item)

for args in [
    ('S01','NkustProjectForJinean.sln','Project(', '四個子專案的組成'),
    ('S02','NewKuasSystem/Default.aspx.cs','switch (URL)', '依網域導向業務入口'),
    ('S03','NewKuasSystem/View/Space/SpaceLogin.aspx.cs','public partial class SpaceLogin', '場地登入、Session 與身分判定'),
    ('S04','NewKuasSystem/MasterPage/Space/MasterForSpaces.Master.cs','switch (Rec_PowerLoginType)', '選單授權與登出'),
    ('S05','NewKuasSystem/UserControl/SkyLoginUserControl.ascx.cs','public bool SkyKuasSystemCheckUser', '校務亂數憑據驗證'),
    ('S06','SOA/KuasSSOWebService.asmx.cs','public ArrayList KuasSSO_Certify2', '主要 SOAP 方法與對外回傳'),
    ('S07','SOA/Module/LdapAuthentication.cs','public ArrayList IsSSOAuthenticated', 'LDAP 查驗、備援與補同步紀錄'),
    ('S08','SOA/Model/ModelForSOA.cs','public DataTable getResultForLoginInMappingUsrData', '中介帳號資料與密碼驗證'),
    ('S09','AutoADAsy/Program.cs','Application.Run(new KuasLdapAsyFrm())', '桌面同步程式入口'),
    ('S10','AutoADAsy/KuasLdapAsyFrm.cs','private void timer1_Tick', '全量／差異排程與密碼同步'),
    ('S11','AutoADAsy/Control/ControlForAutoADAsy.cs','AsyLdapUserCompared(', '校務到中介表再到 LDAP'),
    ('S12','NewKuasSystem/View/Space/SpaceApply.aspx.cs','case "InsertForApply_"', '場地申請與時段檢查'),
    ('S13','NewKuasSystem/View/Space/SpaceApplyVerify.aspx.cs','case "ButtonForVerify"', '逐關審核與下一位審核者'),
    ('S14','NewKuasSystem/View/Space/ShowPaymentConfirm.aspx.cs','SaveConfirmPayment(', '費用確認與保存'),
    ('S15','NewKuasSystem/View/Enroll/enroll_View.aspx.cs','case "SavingData"', '報名預覽後送出與通知'),
    ('S16','NewKuasSystem/View/Enroll/enroll_main.aspx','網路登入報名', '招生入口與查詢服務'),
    ('S17','NewKuasSystem/View/EMS/ShowEMSApplyVerify.aspx.cs','case "ButtonForVerify"', '招生名額調整審核'),
    ('S18','NewKuasSystem/Model/KGS/ModelForKGS.cs','getWorkProcessInitialData()', '業務、流程步驟、附件與表單'),
    ('S19','NewKuasSystem/Model/EvergreenActive/EvergreenActiveModel.cs','get_OpenTimeCompare()', '活動開放時間與名額檢查'),
    ('S20','NewKuasSystem/Model/Aggregate/StudAggregateModel.cs','public DataTable KuasStudAggregate(', '學生人數彙整'),
    ('S21','NewKuasSystem/View/50Ann/ActiveRFIDOrder.aspx.cs','protected void Page_Load', '歷史頁面仍有測試導向'),
    ('S22','AutoADAsy/KuasHandCaptureNewsFrm.cs','private void Btn_Capture_Click', '校園消息擷取輔助工具'),
    ('S23','NewKuasSystem/Model/DataProcess/ModelForDataProcess.cs','Get_ModelSqlData(', '人工資料比對與同步工具'),
    ('S24','NkustProjectForJinean/test.aspx.cs','protected void Page_Load', '空白測試頁骨架'),
]:
    evidence_item(*args)

projects = []
for name in ['NewKuasSystem', 'SOA', 'AutoADAsy', 'NkustProjectForJinean']:
    path = SOURCE / name / (name + '.csproj')
    tree = ET.parse(path)
    contents = [x.attrib['Include'].replace('\\', '/') for x in tree.findall('.//m:Content', NS)]
    pages = [x for x in contents if x.endswith('.aspx')]
    groups = Counter(x.split('/')[1] if x.startswith('View/') else '其他位置' for x in pages)
    projects.append(dict(name=name, framework=tree.find('.//m:TargetFrameworkVersion', NS).text,
                         aspx_count=len(pages), asmx_count=len([x for x in contents if x.endswith('.asmx')]),
                         view_groups=dict(sorted(groups.items())), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

def esc(value): return html.escape(str(value))
def cards(items, cls=''):
    return '<div class="cards '+cls+'">'+''.join('<article><span class="tag">'+esc(k)+'</span><h3>'+esc(t)+'</h3><p>'+v+'</p></article>' for k,t,v in items)+'</div>'
def flow(items, cls=''):
    return '<ol class="flow '+cls+'">'+''.join('<li><span>'+str(i+1).zfill(2)+'</span><h3>'+esc(t)+'</h3><p>'+v+'</p></li>' for i,(t,v) in enumerate(items))+'</ol>'
def note(title, content, cls=''):
    return '<aside class="note '+cls+'"><strong>'+title+'</strong><p>'+content+'</p></aside>'
def table(headers, rows, cls=''):
    return '<table class="'+cls+'"><thead><tr>'+''.join('<th>'+h+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+v+'</td>' for v in row)+'</tr>' for row in rows)+'</tbody></table>'
pages=[]
def page(kicker,title,lead,body,refs):
    n=len(pages)+1
    pages.append(f'<section class="sheet" id="p{n}"><div class="page-content"><header><span class="kicker">{kicker}</span><span class="edition">舊專案分析 · {DATE}</span></header><h1>{title}</h1><p class="lead">{lead}</p>{body}</div><footer><span>依本機原始碼分析 · 非正式環境驗收報告<br />依據：{refs}</span><b>{n:02d} / 13</b></footer></section>')

page('EXECUTIVE REPORT / 專案全貌','舊專案<br />功能與完整流程分析',
     '從使用者登入、業務申請，到帳號同步與跨系統驗證，<br />看懂這套校園應用程式如何運作。',
     '<div class="cover-banner"><span>國立高雄科技大學</span><h2>一套方案，三個主要運作面向。</h2><p>提供業務畫面、處理帳號資料、支援其他系統確認身分。</p></div>'+cards([
         ('業務網站','NewKuasSystem','場地借用、招生、名額管理、業務流程文件，以及活動與統計工具。'),
         ('串接服務','SOA','對外提供帳密驗證、身分資料、登入憑據及特定業務查詢。'),
         ('同步工具','AutoADAsy','把校務帳號資料經中介表更新到 LDAP，並處理密碼變更。'),
         ('測試骨架','NkustProjectForJinean','保留基本網站結構；目前檢視的 test.aspx 沒有業務處理內容。')], 'two')+
     note('本報告的分析範圍','涵蓋整個舊方案的功能版圖與主要端到端流程，包括業務網站、對外服務與帳號同步。流程依程式分支整理，並不代表所有功能已在正式環境啟用。')+
     '<div class="cover-index"><span>02–03　整體架構與功能</span><span>04–06　登入、串接與同步</span><span>07–10　各業務流程</span><span>11–13　判讀重點與依據</span></div>', 'S01、S09、S24；四份 .csproj')

page('01 / 整體運作架構','先備妥帳號，再支援登入與業務。',
     '同步、驗證、業務操作是三條互相配合的流程，並非每次登入都執行一次全校資料同步。',
     '<div class="lane"><h2><span>A</span> 帳號準備與維護</h2>'+flow([
         ('校務來源','人員、學生、組織與帳號變更'),('同步工具','依設定排程或人工操作'),('中介表 → LDAP','比對後更新帳號、屬性與密碼')])+'</div>'+
     '<div class="lane violet"><h2><span>B</span> 使用者登入</h2>'+flow([
         ('使用者／業務網站','輸入帳密，或帶入既有登入憑據'),('驗證服務','依方法檢查資格與帳號資料'),('接收系統','取得結果，建立自己的登入狀態')])+'</div>'+
     '<div class="lane sand"><h2><span>C</span> 業務執行</h2>'+flow([
         ('功能與權限','判斷誰能申請、維護或審核'),('資料處理','保存申請、審核結果或業務資料'),('作業結果','顯示狀態、查詢、報表或通知')])+'</div>'+
     note('入口也有分流','NewKuasSystem 的首頁依請求網域導向場地、KGS／SOP 或 EMS 頁面。同一個程式方案可以承載不同業務網站；目前正式站台的部署位置仍需另行核對。'),
     'S02、S03、S06、S10、S11、S13')

page('02 / 功能版圖','這個方案包含哪些功能？',
     '以程式中的模組、畫面與資料處理邏輯分類；以下是功能存在的證據，不是目前使用率排名。',
     table(['功能群','程式中可見的內容','主要使用者'],[
         ['場地與空間','Space：查詢、申請、逐關審核、費用確認、統計。<br />CeedSpace：另一組場地流程。LibSpace：圖書館空間與權限維護。','借用者、場地管理者、審核者'],
         ['招生作業','Enroll：報名、收件／繳費狀態、審查、成績、錄取與報到查詢。','考生、招生承辦人'],
         ['招生名額管理','EMS：學制／系所名額、調整申請、審核與註冊率資料。','系所、院級與招生管理人員'],
         ['業務流程文件','KGS：業務項目、SOP 步驟、表單、附件、備註及單位對應。','承辦人、校內查詢者'],
         ['活動與資料查詢','EvergreenActive 活動報名；Aggregate 學生統計；50Ann 歷史活動頁。','活動參與者、管理者'],
         ['身分與外部串接','SOA 驗證及查詢；KuasLib 數位典藏登入銜接；共用登入控制項。','各校內系統與串接廠商'],
         ['帳號與維運工具','AutoADAsy 排程／人工同步、密碼處理、消息擷取；DataProcess 資料比對。','資訊維運人員']
     ])+note('同一資料夾，不等於一個完整且在用的系統','Practice 是練習／示範範圍；部分頁面是備份或歷史版本。50Ann 的 RFID 命名頁仍含測試導向，不能直接列為完整 RFID 業務服務。', 'amber'),
     'S12–S23；NewKuasSystem/View 與 .csproj')

page('03 / 完整流程一：登入與權限','驗證你是誰，再決定你能做什麼。',
     '以場地系統為代表：中央驗證與業務權限分開處理，成功驗證後仍需建立本系統的使用者狀態。',
     flow([('進入業務入口','開啟場地等網站；部分入口由網域導向。'),('選擇／輸入身分','校內使用者以帳密查驗；部分場地流程另處理校外廠商、學生組織等身分。'),('執行驗證','登入程式呼叫 SOAP，或依該入口使用本地資料／LDAP。'),('建立登入狀態','保存帳號、姓名、身分、單位等 Session 資料。'),('套用業務權限','依身分、個人或單位授權形成選單與功能範圍。'),('操作與離開','進行申請／維護；已檢視的部分主版登出分支會清除 Session。')], 'vertical')+
     cards([('另一種入口','校務憑據查驗','已有登入來源時，帶入帳號、功能代碼及亂數，與校務紀錄核對；不需要在此方法重新輸入密碼。'),('權限邊界','登入不等於取得審核權','校園身分可用來辨識使用者，但誰能修改資料、審核或管理場地，仍由各模組規則決定。')], 'two compact')+
     note('不能直接宣稱跨系統全面免重登','程式同時存在帳密、校務憑據與 Session 等不同登入方式。某一入口可免重登，不表示所有網站都共用同一個登入狀態。'),
     'S02–S05；UserControl/Space/LoginFormForSpace.ascx.cs')

page('04 / 完整流程二：對外串接','別的系統如何取得驗證結果？',
     '主要入口提供 61 個方法。多數專用方法會先查呼叫系統的服務資格，再處理使用者資料；各方法不是同一套回傳格式。',
     flow([('對方提出需求','依方法傳入系統資格、使用者帳密或其他查詢條件。'),('檢查服務資格','專用方法可查呼叫帳號、服務代碼與授權對應。'),('查驗身分／資料','依該方法連接 LDAP、帳號中介表或其他校務資料。'),('整理並回覆','回傳結果、指定身分欄位或查詢資料；由對方決定後續登入。')], 'four')+
     table(['代表方法／用途','送進來的內容','原程式回傳重點'],[
         ['Certify2：帳密驗證','使用者帳號、密碼','是否通過、來源身分代碼；此介面沒有呼叫系統帳密參數。'],
         ['離校／諮商輔導','呼叫系統資格＋使用者帳密','離校成功分支：狀態、帳號、姓名；諮商輔導另含成功標記及信箱。'],
         ['Certify1／checkSession','校務亂數憑據／加密 SessionID','核對既有登入憑據；不是一般帳密驗證。'],
         ['交通車／文件申請','方法各自定義的查詢資料','回傳資料集合，不能只當成「成功／失敗」。']
     ])+
     '<div class="branch"><h3>帳密驗證的備援路徑（以 Certify2 為例）</h3><p class="branch-main">LDAP 查驗 → 有符合資料且狀態可用 → 回覆驗證結果</p><p class="branch-alt">LDAP 處理發生例外 → 查帳號中介表 → 核對帳密與狀態 → 符合時回覆，並登記待補同步資料</p><p class="small">備援位於例外處理分支，不表示所有失敗情況都會切換，也不能直接等同於高可用保證。</p></div>'+
     note('WSDL 是規格，不會自行完成驗證','對方真正呼叫的是服務方法。參數、資料來源、回傳欄位與錯誤處理都寫在各方法中；有些方法還會寫入紀錄或補同步資料。'),
     'S06–S08；已保存的主要入口 WSDL')

page('05 / 完整流程三：帳號與密碼同步','同步工具在背景維持帳號資料一致。',
     'AutoADAsy 是桌面程式，透過計時器和人工按鈕執行。實際排程是否運作，取決於程式有沒有持續執行及其設定。',
     '<div class="lane"><h2><span>A</span> 帳號差異同步</h2>'+flow([('讀取校務資料','依教職員、在學生、畢業生、公用帳號等分類。'),('比對中介資料','判斷新增、異動或移除，更新帳號映射。'),('更新 LDAP','比對目錄資料，執行相應帳號與屬性更新。')])+'</div>'+
     '<div class="lane violet"><h2><span>B</span> 密碼變更同步</h2>'+flow([('取得變更紀錄','讀取校務密碼異動，或驗證服務登記的待同步資料。'),('更新目錄密碼','依紀錄執行 LDAP 密碼變更。'),('保存處理狀態','更新相關紀錄，供後續處理與查核。')])+'</div>'+
     cards([('全量／重建','也有較大範圍的同步操作','程式包含先清理再建立組織／帳號的流程，並有分類開關；這類操作與日常差異同步的影響範圍不同。'),('人工操作','網站也有資料處理工具','DataProcess 提供資料查詢、中介資料比對及 LDAP 更新工具。它與桌面排程共存，並不是單一自動流程。')], 'two compact')+
     note('主管需要理解的影響','帳號資料更新與登入驗證有時間差。若同步程式停止或來源無法使用，可能出現資料／密碼不同步；僅從程式碼無法確認目前延遲多久、是否有失敗告警。', 'amber'),
     'S09–S11、S23；AutoADAsy/Model/ModelForAutoADAsy.cs')

page('06 / 完整流程四：場地借用','從選場地，到審核與費用確認。',
     '以下依 Space 的申請、審核及費用確認程式整理。CeedSpace 與 LibSpace 為相關模組，不能假設所有規則完全相同。',
     flow([('登入與選擇場地','確認申請身分，查詢場地、可用時間與限制。'),('填寫申請內容','活動、使用人數、聯絡方式、借用時段與附件。'),('檢查後建立申請','檢查欄位、時間與適用條件，保存申請主檔及明細。'),('依規則逐關審核','記錄同意／不同意與意見，判斷下一位審核者；通知依程式分支執行。'),('費用與繳費確認','依場地流程處理場地費、水電費、保證金及確認紀錄。'),('查詢與留存','顯示申請／審核狀態、列印確認單，提供歷史查詢與統計。')], 'vertical')+
     cards([('申請者','知道申請到了哪一步','可查目前進度、明細及歷史狀態；校外申請另有廠商註冊與資格審核畫面。'),('管理者','管理資源與審核規則','設定場地、管理人、身分可用功能、審核關卡與適用時間；並處理費用確認。')], 'two compact')+
     note('費用確認不等於已證明線上收款完成','程式有費用計算、繳費與確認畫面；本次未連線金流或對帳來源，不據此宣稱每種支付方式均可使用。'),
     'S03、S12–S14；View/Space、View/CeedSpace、View/LibSpace')

page('07 / 完整流程五：招生作業','涵蓋報名、審查、成績與錄取查詢。',
     'Enroll 服務對象包括考生與承辦人。不同招生種類具有不同欄位與判斷，以下為程式功能串接出的主要作業順序。',
     '<div class="lane"><h2><span>A</span> 考生的操作路徑</h2>'+flow([('選招生項目','查看開放期間與報名入口。'),('填寫與確認','檢查身分、填報資料、志願與預覽。'),('送出與追蹤','保存報名，取得查詢資料，確認收件／繳費狀態。')])+'</div>'+
     '<div class="lane violet"><h2><span>B</span> 承辦人的處理路徑</h2>'+flow([('審查資料','設定招生項目，處理收件、初審與複審。'),('處理考試成績','准考證、成績匯入、計算與排名，依招生種類處理。'),('辦理錄取作業','志願排序、最低分數、錄取名單與報到管理。')])+'</div>'+
     '<div class="lane sand"><h2><span>C</span> 考生回來查詢</h2><div class="chips"><span>報名狀態</span><span>成績與複查</span><span>錄取結果</span><span>錄取通知單</span><span>網路報到</span></div><p>查詢頁面各有登入／識別流程，不應視為所有考生都經過同一個校園 LDAP 帳號登入。</p></div>'+
     note('招生流程中的例外','程式有重複報名檢查、不同招生種類分支與失敗訊息。實際招生年度、開放期間、名額與計分規則由資料與設定影響，須由承辦單位確認。'),
     'S15、S16；View/Enroll 的初複審、成績、錄取及報到頁面')

page('08 / 完整流程六：名額管理與 SOP','兩類行政管理，處理不同的業務問題。',
     'EMS 管理招生名額調整；KGS 保存業務與作業步驟。名稱相近的「流程」不代表兩者都在執行同一套電子簽核。',
     '<div class="lane"><h2><span>A</span> EMS：招生名額調整</h2>'+flow([('建立基礎資料','設定招生單位、學制、系所與名額。'),('提出調整申請','填入轉出／轉入名額及申請內容。'),('審核與記錄','依身分與關卡審核，保存結果與後續通知。')])+'<p class="subline">另有註冊率資料匯入／維護、承辦人與身分程式授權畫面。</p></div>'+
     '<div class="lane violet"><h2><span>B</span> KGS：業務流程知識庫</h2>'+flow([('建置業務項目','設定名稱、業管單位與服務對象。'),('維護作業內容','編排流程步驟、表單、附件與備註。'),('查詢與使用','依業務或單位尋找 SOP 與所需文件。')])+'<p class="subline">另有最新消息、個人／單位授權與業務掛接單位等維護功能。</p></div>'+
     cards([('EMS 的價值','讓名額調整留下可追蹤紀錄','誰提出、調整哪些項目、由誰審核，透過資料與流程保存。'),('KGS 的價值','讓承辦流程與文件可查找','使用者可以依作業需求找到步驟和表單；本次未據此認定所有 SOP 都可線上送件簽核。')], 'two compact')+
     note('需要業務單位補充的資訊','目前有效的關卡、承辦角色、名額規則與文件版本，不能僅從舊程式檔案判斷。'),
     'S17、S18；View/EMS、View/KGS 與相關 Model')

page('09 / 其他功能與歷史範圍','還有活動、統計、圖書與維運工具。',
     '這些功能同樣位於舊方案內，但應依現行需求分別判斷是否保留，不能全部視為 SSO 核心功能。',
     cards([
         ('活動報名','EvergreenActive','檢查活動開放期間與人數限制 → 使用者報名／修改／取消 → 管理者查詢與匯出。部分畫面包含接送或相關活動資料。'),
         ('學生統計','Aggregate','選擇學年學期 → 查詢彙整資料 → 依部別、學院、學制、科系分組與計算人數。這是統計查詢，不是即時學籍異動服務。'),
         ('圖書登入銜接','KuasLib','共用登入控制項執行驗證 → 依設定銜接數位典藏登入入口。實際遠端接收端與登入有效性仍需互通驗證。'),
         ('資料維護','DataProcess','查詢校務／中介資料 → 比對差異 → 更新中介表或 LDAP。屬於維運操作畫面，不能當作一般使用者唯讀查詢。'),
         ('消息擷取','AutoADAsy 輔助工具','手動選擇消息類別 → 取得網頁並解析 → 保存標題、連結與日期。網站結構改變時，這段解析是否仍可用需另測。'),
         ('歷史與練習','50Ann、Practice、骨架專案','包含活動展示、登入示例與練習頁；部分仍有測試導向。骨架專案的 test.aspx 未見實質業務邏輯。')
     ], 'two')+
     note('檔案存在 ≠ 功能完整 ≠ 正式仍在使用','本頁特別保留歷史／測試分類。未實際登入、未查正式流量，也沒有把未執行的程式當作已完成驗收的服務。', 'amber'),
     'S19–S24；View/KuasLib 與相關共用登入控制項')

page('10 / 資料、紀錄與維運判讀','整體運作依賴多個外部環節。',
     '網站畫面正常，不代表同步、驗證來源、郵件與業務資料處理都正常；每一段都有不同的責任與檢查點。',
     table(['環節','原程式中扮演的角色','主管應確認的事項'],[
         ['校務資料來源','提供人員、學生、組織與帳號異動；多處資料存取區分 SQL Server／Sybase。','來源的負責單位、更新時點與變更通知。'],
         ['中介帳號與 LDAP','中介表支援帳號映射及部分備援驗證；LDAP 用於目錄身分查驗。','誰維護同步程式？失敗多久會被發現？'],
         ['業務資料庫與檔案','保存申請、審核、名額、招生資料、附件及表單。','備份是否涵蓋資料庫與附件？由誰驗證復原？'],
         ['SOAP 呼叫端','各系統依不同方法、參數與欄位格式使用服務。','哪些仍在用？哪些方法不可中斷？'],
         ['郵件與紀錄','部分流程有通知、呼叫／同步紀錄或錯誤處理。','郵件送達不等於業務完成；需查申請本身的狀態。']
     ])+
     cards([('已觀察到的設計','登入驗證也可能有寫入副作用','部分驗證分支會保存待同步資料；某些紀錄路徑含密碼相關欄位。重新設計時應逐項確認資料必要性與存取範圍。'),('尚不能由原始碼確認','正式環境的穩定度與使用量','未取得有效部署清單、呼叫統計、同步失敗率或備份演練結果，因此不估算目前負載與可用率。')], 'two compact')+
     note('本次分析的結論','舊方案是一組業務應用、身分服務與資料同步工具的集合。若要接手維運或規劃更新，應按這三個面向盤點，不宜只以單一驗證網址代表整個專案。'),
     'S06–S11、S13–S15、S17、S23；Shared 資料與郵件工具')

page('11 / 主管決策摘要','先確認「仍在用什麼」，再決定後續範圍。',
     '程式已提供功能線索與主要流程；下一步需要業務單位與維運人員補上實際使用證據。',
     cards([('已確認','四個子專案、三個主要運作面向','業務網站、SOAP 服務與桌面同步工具均有實際程式；另有一個測試骨架專案。'),('已確認','登入方式與資料回傳並不一致','不同入口可能使用帳密、校務憑據或 Session；各服務提供的欄位與授權檢查也不同。'),('已確認','業務網站有完整作業鏈的程式','場地從申請到審核與費用確認；招生從報名到錄取查詢；EMS 與 KGS 各有管理功能。'),('待確認','正式可用性與當前營運設定','功能存在不代表目前開放；各年度業務規則、使用量、現行窗口及實際部署需另查。')], 'two')+
     '<h2 class="section-title">建議先完成的四件事</h2>'+flow([('確認在用功能','各單位標示在用、停用、歷史保留。'),('確認關鍵流程','哪些登入、招生或申請流程不可中斷。'),('確認維運責任','資料來源、同步排程、備份與異常處理各由誰負責。'),('核對實際結果','以授權測試環境逐條驗證正常、拒絕與來源中斷情境。')], 'four')+
     note('閱讀本報告時的界線','這是一份靜態程式分析與流程說明，不是上線驗收、完整資安稽核或所有分支的測試報告。沒有啟動舊程式，沒有連線查詢或修改任何舊資料庫。'),
     '本報告 S01–S24 的交叉整理')

source_rows = ''.join('<div class="source-item"><b>'+s['id']+' · '+esc(s['label'])+'</b><code>'+esc(s['path'])+':'+str(s['line'])+'</code></div>' for s in evidence)
web = projects[0]
counts = '；'.join(f'{esc(k)} {v}' for k,v in web['view_groups'].items())
page('APPENDIX / 分析依據','來源與盤點口徑',
     '以本機舊方案為準；行號便於資訊人員回查。附件 source-inventory.json 保存檔案雜湊與專案清單統計。',
     '<div class="source-grid">'+source_rows+'</div>'+
     '<div class="inventory"><h3>數量如何解讀</h3><p>四個專案均宣告 .NET Framework 4.8。NewKuasSystem 專案清單列入 <b>'+str(web['aspx_count'])+' 個 .aspx 項目</b>，包含歷史、測試與練習頁，不能當作 '+str(web['aspx_count'])+' 項正式功能。</p><p class="small">View／其他位置的項目數：'+counts+'。</p><p class="small">SOA 專案清單列入 5 個 .asmx 入口；主要 KuasSSOWebService 有 61 個 WebMethod。另有版本、測試入口與已註解的方法，不能直接相加當作不重複服務數。</p></div>',
     '本機來源：newnkustprojectforjinean；盤點日期 '+DATE)

assert len(pages)==13
document='''<!DOCTYPE html><html lang="zh-Hant"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'"><title>舊專案功能與完整流程分析｜國立高雄科技大學</title><link rel="stylesheet" href="report.css"><script src="report.js" defer></script></head><body><nav class="toolbar" aria-label="報告操作"><strong>舊專案功能與完整流程分析</strong><div><a href="legacy-project-analysis.pdf" download>下載 PDF</a><button type="button" id="print-report">列印報告</button></div></nav><main>'''+''.join(pages)+'''</main></body></html>'''
(OUT/'index.html').write_text(document,encoding='utf-8')
(OUT/'source-inventory.json').write_text(json.dumps(dict(date=DATE,scope='靜態原始碼分析；未連線任何舊服務或資料庫',projects=projects,evidence=evidence),ensure_ascii=False,indent=2),encoding='utf-8')
print('Created report HTML and source inventory:',OUT)
