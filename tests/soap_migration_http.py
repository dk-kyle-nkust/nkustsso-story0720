"""已移植方法的實際 SOAP HTTP 測試；使用獨立主機與唯讀合成校務資料。"""
import json
import socket
import subprocess
import time
import xml.etree.ElementTree as ET
from urllib.request import Request, urlopen
from urllib.error import HTTPError
from server_fixture import ROOT, BUILD_DIRECTORY, CREATE_FLAGS

catalog = json.loads((ROOT / 'NkustSso.Server/Services/SoapDefinitions/legacy-soap-operations.json').read_text(encoding='utf-8-sig'))
methods = [m for m in catalog if m['Implemented']]
ns = 'http://tempuri.org/'
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    base = f'http://127.0.0.1:{sock.getsockname()[1]}'
log_path = ROOT / '.local/soap-migration-http.log'


def call(path, data=None, headers=None):
    try:
        response = urlopen(Request(base + path, data=data, headers=headers or {}), timeout=15)
    except HTTPError as error:
        response = error
    with response:
        assert 'set-cookie' not in response.headers
        return response.status, response.read()


with log_path.open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(BUILD_DIRECTORY / 'NkustSso.StorageChecks.dll'), '--integration-api', base],
                               cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, creationflags=CREATE_FLAGS)
    try:
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError('隔離測試主機失敗：' + str(log_path))
            try:
                with urlopen(base + '/health', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        else:
            raise RuntimeError('隔離主機啟動逾時')
        status, body = call('/KuasSSOWebService.asmx?wsdl')
        assert status == 200
        wsdl = ET.fromstring(body)
        operations = {e.get('name') for e in wsdl.iter('{http://schemas.xmlsoap.org/wsdl/}operation')}
        assert operations == {m['Name'] for m in methods}
        for version in ['1.1', '1.2']:
            env = 'http://schemas.xmlsoap.org/soap/envelope/' if version == '1.1' else 'http://www.w3.org/2003/05/soap-envelope'
            for method in methods:
                name = method['Name']
                traffic = name.startswith('Get_Traffic_')
                dataset = traffic or name == 'NkustSSO_DocumentApplyPaymentSystemSearch'
                values = {'usr_id': 'original-client' if traffic else 'S0001',
                          'usr_pwd': 'fixture-original-caller-password' if traffic else 'correct',
                          'SearchUsr_id': 'original-client', 'SearchUsr_pwd': 'fixture-original-caller-password',
                          'IDNO': 'A100000001', 'Birthday': '0900102', 'identity': 'STUD',
                          'auth_id': 'S0001', 'auth_pwd': 'correct', 'from_Clientip': '192.0.2.99',
                          'fnc_id': 'F1', 'Rnd_id': '101' if version == '1.1' else '105',
                          'searchid': 'S0001', 'searchID': 'office01' if name == 'Get_Traffic_Pub' else 'E0001' if 'Teach' in name else 'S0001'}
                root = ET.Element('{' + env + '}Envelope')
                operation = ET.SubElement(ET.SubElement(root, '{' + env + '}Body'), '{' + ns + '}' + name)
                for param in method['Parameters']:
                    ET.SubElement(operation, '{' + ns + '}' + param['Name']).text = values[param['Name']]
                headers = {'Content-Type': 'text/xml', 'SOAPAction': '"' + ns + name + '"'} if version == '1.1' else {
                    'Content-Type': 'application/soap+xml; action="' + ns + name + '"'}
                request = ET.tostring(root, encoding='utf-8')
                status, body = call('/KuasSSOWebService.asmx', request, headers)
                assert status == 200, (name, version, status)
                result = ET.fromstring(body).find('.//{' + ns + '}' + name + 'Result')
                assert result is not None, name
                if dataset:
                    assert result.find('.//{http://www.w3.org/2001/XMLSchema}schema') is not None
                    assert result.find('.//{urn:schemas-microsoft-com:xml-diffgram-v1}diffgram') is not None
                    assert result.find('.//message').text == '200'
                    if traffic:
                        assert result.find('.//pwd').text is None
                    else:
                        assert result.find('.//std_id').text == 'S0001'
                        assert result.find('.//std_name').text == '測試學生'
                        assert result.find('.//std_email').text == 's@example.invalid'
                        assert result.find('.//usr_id') is None
                else:
                    actual = [e.text for e in result]
                    assert actual == (['200', '登入成功!', 'true'] if name == 'KuasSSO_SchCardSearch' else ['true', '02']), (name, actual)
                if name == 'KuasSSO_Certify1':
                    status, replay = call('/KuasSSOWebService.asmx', request, headers)
                    assert status == 200 and b'NoPass' in replay
                if dataset or name == 'KuasSSO_SchCardSearch':
                    field = 'usr_pwd' if traffic else 'SearchUsr_pwd'
                    operation.find('{' + ns + '}' + field).text = 'wrong'
                    status, denied = call('/KuasSSOWebService.asmx', ET.tostring(root, encoding='utf-8'), headers)
                    assert status == 200 and (b'501' if dataset else b'400') in denied
                    assert '測試'.encode() not in denied
        print(f'PASS all {len(methods)} methods over actual SOAP 1.1/1.2 HTTP, WSDL, DataSet, caller denial and ticket replay')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
