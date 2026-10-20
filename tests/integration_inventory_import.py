"""僅以合成程式、WSDL 及隔離 SQLite 驗證匯入；不連線外部來源。"""
import importlib.util
import json
from pathlib import Path
import sqlite3
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('inventory_import', ROOT / 'scripts/import-integration-inventory.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def schema(path, records):
    with module.connect(path) as db:
        columns = {**records[0], 'ImportBatchId': '', 'ImportedAt': 0, 'Revision': ''}
        definitions = [f'"{key}" {"INTEGER" if isinstance(value, int) else "TEXT"} NOT NULL' for key, value in columns.items()]
        db.execute('CREATE TABLE IntegrationClients (' + ','.join(definitions) + ', PRIMARY KEY(Id))')
        db.execute('CREATE UNIQUE INDEX SourceIndex ON IntegrationClients(SourceSystem,SourceId)')
        db.execute('CREATE TABLE IntegrationAudits (Id INTEGER PRIMARY KEY, OccurredAt INTEGER, ClientId TEXT, ActorId TEXT, RemoteIp TEXT, Action TEXT, Detail TEXT)')
        db.execute('CREATE TABLE IntegrationClientServices (ClientId TEXT, ServiceId TEXT)')


with tempfile.TemporaryDirectory(prefix='inventory-test-', dir=ROOT / '.local') as temporary:
    directory = Path(temporary)
    source = directory / 'Service.cs'
    source.write_text('''
    #region [測試單位]_測試系統
    public string Example([XmlElement(ElementName="SessionID", IsNullable=false)] string SessionID) {
        // public string Decoy() { }
        var privateValue = "NEVER_COPY_THIS_SECRET";
        // lookup.Add("WebService_id", "WS9999");
        lookup.Add("WebService_id", "WS0001"); return "}";
    }
    #endregion
    #region [測試單位]_另一個系統
    public string Another(string account) { return account; }
    #endregion
    ''', encoding='utf-8')
    wsdl = directory / 'contract.wsdl'
    wsdl.write_text('''<definitions xmlns="http://schemas.xmlsoap.org/wsdl/" xmlns:x="http://www.w3.org/2001/XMLSchema">
    <types><x:schema><x:element name="Example"><x:complexType><x:sequence><x:element name="SessionID" type="x:string"/></x:sequence></x:complexType></x:element>
    <x:element name="ExampleResponse"><x:complexType><x:sequence><x:element name="ExampleResult" type="x:string"/></x:sequence></x:complexType></x:element>
    <x:element name="Another"><x:complexType><x:sequence><x:element name="account" type="x:string"/></x:sequence></x:complexType></x:element>
    <x:element name="AnotherResponse"><x:complexType><x:sequence><x:element name="AnotherResult" type="x:string"/></x:sequence></x:complexType></x:element>
    </x:schema></types><portType name="ExampleSoap"><operation name="Example"/><operation name="Another"/></portType></definitions>''', encoding='utf-8')
    manifest = module.inventory(source, wsdl)
    assert manifest['method_count'] == 2 and manifest['registration_count'] == 2
    assert manifest['methods'][0]['service_ids'] == ['WS0001']
    assert 'NEVER_COPY_THIS_SECRET' not in json.dumps(manifest)
    database = directory / 'test.db'
    schema(database, manifest['records'])
    report = module.import_records(database, manifest, directory / 'output')
    assert report['pending'] == 2 and not report['applied']
    with module.connect(database) as db:
        assert db.execute('SELECT COUNT(*) FROM IntegrationClients').fetchone()[0] == 0
    report = module.import_records(database, manifest, directory / 'output', apply=True)
    assert report['inserted'] == 2
    with module.connect(report['backup']) as backup:
        assert backup.execute('PRAGMA quick_check').fetchone()[0] == 'ok'
        assert backup.execute('SELECT COUNT(*) FROM IntegrationClients').fetchone()[0] == 0
    with module.connect(database) as db:
        assert db.execute('SELECT COUNT(*) FROM IntegrationClients WHERE Enabled=0 AND AllowedIps=\'\' AND SecretHash=\'\' AND ConnectionKind=\'existing\'').fetchone()[0] == 2
        assert db.execute('SELECT COUNT(*) FROM IntegrationClientServices').fetchone()[0] == 0
        assert db.execute('SELECT COUNT(*) FROM IntegrationAudits WHERE Action=\'import\'').fetchone()[0] == 2
        db.execute('UPDATE IntegrationClients SET Name=\'管理者已修改\' WHERE Id=?', (manifest['records'][0]['Id'],))
    repeated = module.import_records(database, manifest, directory / 'output', apply=True)
    assert repeated['inserted'] == 0 and repeated['skipped'] == 2
    with module.connect(database) as db:
        assert db.execute('SELECT Name FROM IntegrationClients WHERE Id=?', (manifest['records'][0]['Id'],)).fetchone()[0] == '管理者已修改'
    collision = directory / 'collision.db'
    schema(collision, manifest['records'])
    occupied = {**manifest, 'records': [{**manifest['records'][1], 'SourceSystem': 'another-source'}]}
    module.import_records(collision, occupied, directory / 'collision-output', apply=True)
    try:
        module.import_records(collision, manifest, directory / 'collision-output', apply=True)
        raise AssertionError('代碼衝突應取消整批。')
    except ValueError:
        pass
    with module.connect(collision) as db:
        assert db.execute('SELECT COUNT(*) FROM IntegrationClients').fetchone()[0] == 1
        assert db.execute('SELECT COUNT(*) FROM IntegrationAudits').fetchone()[0] == 1
    wsdl.write_text(wsdl.read_text(encoding='utf-8').replace('name="SessionID"', 'name="Mismatch"'), encoding='utf-8')
    try:
        module.inventory(source, wsdl)
        raise AssertionError('參數不一致應拒絕。')
    except ValueError:
        pass
print('PASS inventory parsing, no secrets, dry-run, backup, disabled import, audit, idempotence, collision rollback and contract mismatch')
