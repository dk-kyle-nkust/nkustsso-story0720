# 舊 SOAP 相容性清單

來源：2026-10-16 下載的 https://nkustsso.nkust.edu.tw/KuasSSOWebService.asmx?WSDL，以及 D:/Code/newnkustprojectforjinean/SOA/KuasSSOWebService.asmx.cs。共 61 個方法，保留原拼字與大小寫。

目前 9 個方法的 SOAP 1.1／1.2 已實作並以隔離來源測試；差異與剩餘 model 需求見 [移植狀態](legacy-soap-migration-status.md)。實際校方來源與廠商程式互通尚待確認。原 WSDL 另有 HTTP GET／表單 POST，本批未提供。未完成的方法不列入新服務 WSDL。

| 方法 | 輸入參數 | 回傳結構 | 狀態 |
| --- | --- | --- | --- |
| `KuasSSO_Certify1` | `usr_id`, `fnc_id`, `Rnd_id` | `tns:ArrayOfAnyType` | 已實作；正式互通待驗證 |
| `KuasSSO_Certify2` | `usr_id`, `usr_pwd` | `tns:ArrayOfAnyType` | 已實作；正式互通待驗證 |
| `KuasSSO_iKuas` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_QueRespose` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `Get_Traffic_Pub` | `usr_id`, `usr_pwd`, `searchID` | `內嵌複合結構（須另核對）` | 已實作；正式互通待驗證 |
| `Get_Traffic_Teach` | `usr_id`, `usr_pwd`, `searchID` | `內嵌複合結構（須另核對）` | 已實作；正式互通待驗證 |
| `Get_Traffic_Teach_Temp` | `usr_id`, `usr_pwd`, `searchID` | `內嵌複合結構（須另核對）` | 已實作；正式互通待驗證 |
| `Get_Traffic_Stud` | `usr_id`, `usr_pwd`, `searchid` | `內嵌複合結構（須另核對）` | 已實作；正式互通待驗證 |
| `Get_Traffic_Stud_Temp` | `usr_id`, `usr_pwd`, `searchID` | `內嵌複合結構（須另核對）` | 已實作；正式互通待驗證 |
| `KuasSSO_Alumni` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Active` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Library` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Reserach` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `checkSession` | `SessionID` | `s1:ir_userinfo` | 待移植 |
| `KuasSSO_TeaAdv` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Cloud` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Moocs` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_TeachDepartment` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `chk_division` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_XiaoWuTong` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_Nelec` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_CourseMap` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_rhcloud` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_NoPaperMeeting` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_SimpleInterActive` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_AudioBook` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_LibJournal` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_LibBookSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_ZuvioSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_UnionSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_DoorAccessSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `s:string` | 待移植 |
| `KuasSSO_SchCardSearch_` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_SchCardSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 已實作；正式互通待驗證 |
| `KuasSSO_SchResearchSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_AIFUSystemResearchSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_BookingSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_InternetCampusSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_EasyTestSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_DSICSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_JESDASystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_DocumentApplyPaymentSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `IDNO`, `Birthday`, `identity`, `from_Clientip` | `DataSet（XSD＋DiffGram）` | 已實作（原校務查詢） |
| `NkustSSO_SchDevSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_AdjunctAssistantSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_SchDevQueSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_SchLeavingSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_NkustkmuRentSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_NkustConsultationSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_SchResearchInfoSysDownLoadPlatformSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_AffairPMSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_InterPassAPPSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_NkustNewEnrollSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_MonitorSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_CarDisplaySearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_StuPubVoteSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_PaySystemStudSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `IDNO`, `SchSysPwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_PaySystemEmployeeSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_TeachResourceLibrarySearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_SchStuHistorySearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_EBSystemStudSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_HyreadBookSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `KuasSSO_AIFUReportSystemResearchSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |
| `NkustSSO_TWaeaQuesSystemSearch` | `SearchUsr_id`, `SearchUsr_pwd`, `auth_id`, `auth_pwd`, `from_Clientip` | `tns:ArrayOfAnyType` | 待移植 |

## 移植順序

1. 完成 Certify2 的實際來源設定，以既有呼叫端測試，核對成功、錯誤帳密、停用、空白狀態、來源中斷與不同身分代碼。
2. 盤點真正在用的方法與呼叫系統；取得舊 SSO_Usr／SSO_Power 授權對應，逐系統登記，不以單一回傳格式套用全部方法。
3. 處理 KuasSSO_Certify1 的 s90_auth_log 亂數票據與有效期、checkSession 的舊加密與 Session 產生端。checkSession 使用的 Request／Response namespace 是 http://tempuri.org（結尾沒有斜線），需另外保留。
4. 依使用情形移植其餘 ArrayList、字串及 DataSet 方法。SearchUsr_id／SearchUsr_pwd 是呼叫系統資格；auth_id／auth_pwd 是使用者帳密，不可互換。
5. 以原用戶端、原 SOAPAction、原命名空間及型別做契約比對，再安排代理路由與網域切換、回復方案。

詳細設定與已知差異見 [串接服務說明](integration-services.md)。
