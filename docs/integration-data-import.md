# 串接資訊匯入

2026-10-16 已將本機程式整理的 **54 筆串接登記、61 個方法規格** 寫入本專案 `.local/sso.db`。至「串接系統管理 → 帳密驗證 API → 舊系統」查看，可搜尋系統名稱、方法名稱或原服務代碼，展開「查看備註」查閱規格。

來源是 `D:/Code/newnkustprojectforjinean/SOA/KuasSSOWebService.asmx.cs` 與先前保存的本機 WSDL。只讀取檔案，未修改來源專案，也未連線或異動 MSSQL。

## 匯入內容

- 系統名稱與原程式註記的負責單位；現行單位及承辦人仍須核對。
- 方法名稱、輸入參數順序、回傳結構型別、SOAPAction、服務路徑及來源行號。
- 原程式中的服務代碼；它不是呼叫端帳號。
- 匯入批次、時間與每筆異動紀錄。

交通車五個方法、校園卡兩個方法、校務驗證兩個方法及 LinePay 學生／職員兩個方法分別整理為同一項登記。54 是本批規格登記筆數，不宣稱等同原資料庫實際呼叫端數量。

未取得實際呼叫帳號、IP、密鑰及原服務授權關係，因此全部設為「舊系統」、SOAP、停用，IP 與聯絡欄位留空，沒有任何服務授權。原區段中的歷史窗口保留於備註，不推定為現任承辦人。匯入不會開啟服務，也不代表方法已完成移植。

來源配對使用 `SourceSystem=newnkustprojectforjinean:source`；`SourceId` 是方法或合併群組識別，與原呼叫帳號分開。發現原程式名稱／代碼不一致的項目在備註標示待核對，不自行改寫。

## 備份及驗證

本次批次：`source-inventory-20261016T060348Z-feba1cba`。

- 匯入前備份：`.local/integration-import/source-inventory-20261016T060348Z-feba1cba-before.db`。
- 規格清單：`.local/integration-import/inventory.json`。
- 匯入報告：`.local/integration-import/source-inventory-20261016T060348Z-feba1cba-report.json`。

使用 SQLite 備份 API 包含 WAL 已提交資料，並執行完整性檢查；整批插入與稽核在同一交易完成。重複執行略過已有來源對應，不覆寫管理者修改；代碼衝突整批回復。沒有變更原首頁入口、帳號、密鑰及登入方式。

## 重複操作

```powershell
# 預覽，不寫入資料庫
python -X utf8 scripts/import-integration-inventory.py `
  --source D:/Code/newnkustprojectforjinean/SOA/KuasSSOWebService.asmx.cs `
  --contract .local/legacy-soap-review/KuasSSOWebService.wsdl

# 加上 --apply 才會備份並實際匯入；目標固定限制在本專案 .local 內既有 SQLite 檔案
```

程式不讀取來源連線設定、不使用 SQL Server 驅動、不取得密碼值。未填 IP 的停用登記可以先編輯用途及備註；啟用時仍要求有效 IP 與服務授權。

隔離測試涵蓋程式／WSDL 參數核對、忽略註解與密碼值、備份、停用狀態、去重、保留人工修改、代碼衝突回復，以及後台搜尋、分頁、手機版面與 CSP。
