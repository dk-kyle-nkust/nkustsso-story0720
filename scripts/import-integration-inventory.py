"""從本機程式與 WSDL 整理串接登記；僅使用 SQLite，不讀取連線設定或連線 MSSQL。"""
import argparse
from collections import OrderedDict
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import sqlite3
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE_SYSTEM = 'newnkustprojectforjinean:source'
NS = {'w': 'http://schemas.xmlsoap.org/wsdl/', 'x': 'http://www.w3.org/2001/XMLSchema',
      'soap': 'http://schemas.xmlsoap.org/wsdl/soap/'}
# 同一業務在原程式有多個介面，保留各方法規格，不以方法數當成呼叫端數。
GROUPS = {
    'campus-verification': ('校務系統驗證入口', '', ['KuasSSO_Certify1', 'KuasSSO_Certify2']),
    'traffic': ('交通車系統', '', ['Get_Traffic_Pub', 'Get_Traffic_Teach', 'Get_Traffic_Teach_Temp', 'Get_Traffic_Stud', 'Get_Traffic_Stud_Temp']),
    'campus-card': ('校園智慧卡卡務系統平台', '出納組', ['KuasSSO_SchCardSearch_', 'KuasSSO_SchCardSearch']),
    'linepay': ('資料申請 LinePay 繳費系統', '總務處', ['NkustSSO_PaySystemStudSearch', 'NkustSSO_PaySystemEmployeeSearch']),
}
NAMES = {
    'KuasSSO_iKuas': 'iKuas 行動專案（含會議系統）',
    'KuasSSO_QueRespose': '意見反映馬上處理中心',
    'checkSession': '圖書館數位典藏',
    'KuasSSO_Moocs': '摩克斯平台',
    'KuasSSO_TeachDepartment': '各系所網站認證',
    'KuasSSO_UnionSearch': '圖書館聯合線上串接',
    'KuasSSO_ZuvioSearch': 'Zuvio 教學平台',
    'KuasSSO_SchResearchSearch': '校發中心串接（系統名稱待核對）',
    'KuasSSO_AIFUSystemResearchSearch': '艾富簽核系統',
    'KuasSSO_AIFUReportSystemResearchSearch': '艾富經費結報簽核系統',
    'NkustSSO_NkustkmuRentSystemSearch': '學生校外賃居調查平台',
    'NkustSSO_EBSystemStudSearch': '華藝電子書系統（學生）',
    'NkustSSO_TWaeaQuesSystemSearch': '台評會問卷系統（學生）',
}
UNITS = {'校友中心', '秘書室', '圖書館', '教卓計畫', '各系所', '電算中心', '國際事務處',
         '教務處', '資管系', '通識教育中心', '出納組', '校發中心', '語文中心', '文書組',
         '校務發展中心', '研究發展處', '楠梓軍訓室', '學務處-諮商輔導中心', '研發處校務研究組',
         '總務處', '教務處招生組', '總務處事務組', '學務處課外活動組', '教發中心', '教務處註冊組'}
# 保留字元位置與換行；括號配對時忽略註解和字串，避免讀到註解內的測試方法。
TOKENS = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')


def mask(text, comments_only=False):
    def replace(match):
        value = match.group()
        if comments_only and not value.startswith(('//', '/*')):
            return value
        return ''.join('\n' if char == '\n' else ' ' for char in value)
    return TOKENS.sub(replace, text)


def inventory(source, contract):
    raw = source.read_bytes()
    text = raw.decode('utf-8-sig')
    masked = mask(text)
    tree = ET.parse(contract).getroot()
    port = tree.find('w:portType', NS)
    if port is None:
        raise ValueError('WSDL 缺少 portType。')
    operation_names = [op.attrib['name'] for op in port.findall('w:operation', NS)]
    elements = {e.attrib['name']: e for e in tree.findall('w:types/x:schema/x:element', NS)}
    actions = {}
    for op in tree.findall('w:binding/w:operation', NS):
        binding = op.find('soap:operation', NS)
        if binding is not None:
            actions[op.attrib['name']] = binding.attrib.get('soapAction', '')
    methods = []
    for name in operation_names:
        matches = list(re.finditer(r'\bpublic\s+(\w+)\s+' + re.escape(name) + r'\s*\(', masked))
        if len(matches) != 1:
            raise ValueError(f'方法無法唯一對應：{name}')
        match = matches[0]
        depth, parameter_end = 1, match.end()
        while depth and parameter_end < len(masked):
            depth += (masked[parameter_end] == '(') - (masked[parameter_end] == ')')
            parameter_end += 1
        declaration = re.sub(r'\[[^\]]*\]', '', text[match.end():parameter_end - 1])
        start = masked.index('{', parameter_end)
        depth, end = 1, start + 1
        while depth and end < len(masked):
            depth += (masked[end] == '{') - (masked[end] == '}')
            end += 1
        if depth:
            raise ValueError(f'方法括號不完整：{name}')
        body = mask(text[start:end], comments_only=True)
        parameters = [part.strip().split()[-1] for part in declaration.split(',') if part.strip()]
        schema_parameters = elements[name].findall('x:complexType/x:sequence/x:element', NS)
        if parameters != [e.attrib['name'] for e in schema_parameters]:
            raise ValueError(f'程式與 WSDL 參數不一致：{name}')
        stack = []
        for line in text[:match.start()].splitlines():
            if re.match(r'\s*#region\b', line):
                stack.append(re.sub(r'^\s*#region\s*', '', line).strip())
            elif re.match(r'\s*#endregion\b', line) and stack:
                stack.pop()
        region = stack[-1] if stack else ''
        result = elements[name + 'Response'].find('x:complexType/x:sequence/x:element', NS)
        methods.append({'method': name, 'parameters': parameters, 'return_type': match.group(1),
                        'wire_type': result.attrib.get('type', '內嵌複合結構，需逐項核對') if result is not None else '待核對',
                        'soap_action': actions.get(name, ''), 'source_line': text[:match.start()].count('\n') + 1,
                        'source_region': region,
                        'service_ids': sorted(set(re.findall(r'\.Add\("WebService_id",\s*"(WS\d+)"', body)))})
    if len(methods) != len(set(operation_names)):
        raise ValueError('方法名稱重複。')
    groups = OrderedDict()
    group_for_method = {method: key for key, (_, _, names) in GROUPS.items() for method in names}
    for method in methods:
        name = method['method']
        groups.setdefault(group_for_method.get(name, name), []).append(method)
    records = []
    for key, members in groups.items():
        if key in GROUPS:
            name, unit, expected = GROUPS[key]
            if set(expected) != {m['method'] for m in members}:
                raise ValueError('同系統方法不完整：' + key)
        else:
            region = members[0]['source_region']
            brackets = re.findall(r'\[([^\]]+)\]', region)
            unit = next((part for part in brackets if part in UNITS), '')
            label = re.sub(r'\[[^\]]*\]', '', region)
            label = re.sub(r'[_\s]*\d{4}-\d{2}-\d{2}.*$', '', label)
            label = re.sub(r'^[\s_.\d-]+', '', label).strip()
            name = NAMES.get(key, label or key)
            if key == 'KuasSSO_Moocs':
                unit = '教卓計畫'
        ids = sorted({sid for m in members for sid in m['service_ids']})
        notes = ['資料狀態：依本機程式整理的串接規格登記，尚未核對實際呼叫端。',
                 '系統名稱及單位採原程式註記；現行承辦人、廠商、IP、呼叫帳號及授權關係待確認。',
                 '匯入不代表服務已開放或方法已移植；預設停用且不授予任何服務。',
                 '來源：SOA/KuasSSOWebService.asmx.cs；參數順序已與本機 WSDL 核對。',
                 '介面路徑：/KuasSSOWebService.asmx',
                 '原服務代碼（不是呼叫端帳號）：' + ('、'.join(ids) or '此段程式未提供'), '']
        if key == 'KuasSSO_SchResearchSearch':
            notes.append('待核對：原區段名稱與方法用途不一致，未推定正式系統名稱。')
        if key == 'traffic':
            notes.append('待核對：Get_Traffic_Stud_Temp 程式使用 WS0002，與一般學生方法 WS0001 不同；照實保留，不推定授權。')
        for method in members:
            notes += [f"方法：{method['method']}({', '.join(method['parameters'])})",
                      f"回傳結構：{method['return_type']} / {method['wire_type']}；欄位順序與語意仍須逐項核對。",
                      f"SOAPAction：{method['soap_action']}",
                      f"原服務代碼：{', '.join(method['service_ids']) or '未提供'}",
                      f"來源行號：{method['source_line']}；原註記：{method['source_region']}",
                      '實作狀態：' + ('介面已實作，實際來源及對方程式互通待驗證。' if method['method'] == 'KuasSSO_Certify2' else '待移植與互通驗證。'), '']
        record = {'Id': 'legacy-' + key.lower().replace('_', '-'), 'Name': name, 'ConnectionKind': 'existing',
                  'Purpose': f'{name}的串接規格登記；共 {len(members)} 個方法。實際呼叫端與啟用狀態待核對。',
                  'OwningUnit': unit, 'SourceSystem': SOURCE_SYSTEM, 'SourceId': key, 'Protocol': 'soap',
                  'Enabled': 0, 'AllowedIps': '', 'SecretHash': '', 'ApplicantName': '', 'ContactName': '',
                  'ContactPhone': '', 'ContactEmail': '', 'Notes': '\n'.join(notes).strip()}
        if len(record['Name']) > 100 or len(record['Id']) > 100 or len(record['Notes']) > 20000:
            raise ValueError('登記欄位超過後台限制：' + key)
        records.append(record)
    return {'source_sha256': hashlib.sha256(raw).hexdigest(), 'contract_sha256': hashlib.sha256(contract.read_bytes()).hexdigest(),
            'method_count': len(methods), 'registration_count': len(records), 'methods': methods, 'records': records}



@contextmanager
def connect(*args, **kwargs):
    # sqlite3 的交易 context 不會關閉連線；明確釋放，讓 Windows 可安全備份與回收檔案。
    db = sqlite3.connect(*args, **kwargs)
    try:
        with db:
            yield db
    finally:
        db.close()


def import_records(database, manifest, output, apply=False):
    database = database.resolve()
    if not database.is_relative_to(ROOT / '.local') or database.suffix != '.db' or not database.is_file():
        raise ValueError('匯入只接受專案 .local 內已存在的 SQLite .db 檔。')
    output.mkdir(parents=True, exist_ok=True)
    manifest_path = output / 'inventory.json'
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    with connect(database.as_uri() + '?mode=ro', uri=True) as read:
        columns = {r[1] for r in read.execute('PRAGMA table_info(IntegrationClients)')}
        required = set(manifest['records'][0]) | {'ImportBatchId', 'ImportedAt', 'Revision'}
        if not required <= columns:
            raise ValueError('請先完成新專案本機資料表升級；匯入程式不自動建表。')
        if read.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
            raise ValueError('目標資料庫完整性檢查失敗。')
        mapped = set(read.execute('SELECT SourceSystem,SourceId FROM IntegrationClients'))
    pending = [r for r in manifest['records'] if (r['SourceSystem'], r['SourceId']) not in mapped]
    report = {'target': str(database), 'methods': manifest['method_count'], 'registrations': manifest['registration_count'],
              'pending': len(pending), 'inserted': 0, 'skipped': len(manifest['records']) - len(pending), 'applied': False}
    if not apply or not pending:
        return report
    now = datetime.now(timezone.utc)
    batch = 'source-inventory-' + now.strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:8]
    backup = output / (batch + '-before.db')
    with connect(database.as_uri() + '?mode=ro', uri=True) as read, connect(backup) as copy:
        read.backup(copy)
        if copy.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
            raise ValueError('備份完整性檢查失敗，尚未匯入。')
    stamp = int(now.timestamp() * 1000)
    with connect(database.as_uri() + '?mode=rw', uri=True, timeout=15) as db:
        db.execute('BEGIN IMMEDIATE')
        for row in manifest['records']:
            if db.execute('SELECT 1 FROM IntegrationClients WHERE SourceSystem=? AND SourceId=?', (row['SourceSystem'], row['SourceId'])).fetchone():
                continue
            if db.execute('SELECT 1 FROM IntegrationClients WHERE Id=?', (row['Id'],)).fetchone():
                raise ValueError('系統代碼已被其他資料使用；整批取消：' + row['Id'])
            values = {**row, 'ImportBatchId': batch, 'ImportedAt': stamp, 'Revision': uuid.uuid4().hex}
            fields = ','.join('"' + key + '"' for key in values)
            db.execute(f'INSERT INTO IntegrationClients ({fields}) VALUES ({",".join("?" for _ in values)})', tuple(values.values()))
            detail = {'SourceKind': 'source-code-inventory', 'ImportBatchId': batch,
                      'SourceSha256': manifest['source_sha256'], 'ContractSha256': manifest['contract_sha256'],
                      'After': {key: value for key, value in values.items() if key != 'SecretHash'}, 'AfterServices': []}
            db.execute('INSERT INTO IntegrationAudits (OccurredAt,ClientId,ActorId,RemoteIp,Action,Detail) VALUES (?,?,?,?,?,?)',
                       (stamp, row['Id'], 'local-import', '', 'import', json.dumps(detail, ensure_ascii=False)))
            report['inserted'] += 1
        if db.execute('SELECT COUNT(*) FROM IntegrationClients WHERE ImportBatchId=? AND (Enabled<>0 OR SecretHash<>\'\' OR AllowedIps<>\'\')', (batch,)).fetchone()[0]:
            raise ValueError('匯入資料狀態不符合停用規則，整批取消。')
        if db.execute('SELECT COUNT(*) FROM IntegrationClientServices g JOIN IntegrationClients c ON c.Id=g.ClientId WHERE c.ImportBatchId=?', (batch,)).fetchone()[0]:
            raise ValueError('匯入資料不應具有服務授權，整批取消。')
        db.commit()
    report.update(applied=True, batch=batch, backup=str(backup), skipped=len(manifest['records'])-report['inserted'])
    (output / (batch + '-report.json')).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--contract', type=Path, required=True)
    parser.add_argument('--database', type=Path, default=ROOT / '.local/sso.db')
    parser.add_argument('--output', type=Path, default=ROOT / '.local/integration-import')
    parser.add_argument('--apply', action='store_true', help='備份後實際匯入；省略時僅整理及預覽。')
    args = parser.parse_args()
    manifest = inventory(args.source, args.contract)
    if not manifest['records']:
        raise ValueError('沒有可匯入的串接資訊。')
    print(json.dumps(import_records(args.database, manifest, args.output, args.apply), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
