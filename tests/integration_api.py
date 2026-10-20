"""SOAP／JSON 實際 HTTP 契約與後台操作；全程隔離資料庫與合成帳密。"""
import html
import json
import os
from pathlib import Path
import socket
import subprocess
import time
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture, ROOT, BUILD_DIRECTORY, CREATE_FLAGS

SOAP11 = 'http://schemas.xmlsoap.org/soap/envelope/'
SOAP12 = 'http://www.w3.org/2003/05/soap-envelope'
NS = 'http://tempuri.org/'
ACTION = NS + 'KuasSSO_Certify2'
SECRET = 'fixture-json-secret-with-at-least-32-characters'


def envelope(version=SOAP11, user='SOAPUSER', password='correct'):
    return f'<soap:Envelope xmlns:soap="{version}"><soap:Body><KuasSSO_Certify2 xmlns="{NS}"><usr_id>{user}</usr_id><usr_pwd>{password}</usr_pwd></KuasSSO_Certify2></soap:Body></soap:Envelope>'


with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    api_url = f'http://127.0.0.1:{sock.getsockname()[1]}'
assembly = (BUILD_DIRECTORY or ROOT / 'tests/NkustSso.StorageChecks/bin/Debug/net10.0') / 'NkustSso.StorageChecks.dll'
log_path = ROOT / '.local/integration-api-test.log'
log_path.parent.mkdir(exist_ok=True)


def call(path, data=None, headers=None):
    request = Request(api_url + path, data=data.encode('utf-8') if data is not None else None, headers=headers or {})
    try:
        response = urlopen(request, timeout=10)
    except HTTPError as error:
        response = error
    with response:
        body = response.read().decode('utf-8')
        assert 'set-cookie' not in response.headers
        assert response.status == 413 or response.headers.get('Cache-Control') == 'no-store'
        return response.status, body, response.headers


with log_path.open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(assembly), '--integration-api', api_url], cwd=ROOT,
                               stdout=log, stderr=subprocess.STDOUT, creationflags=CREATE_FLAGS)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError('Isolated API host exited: ' + str(log_path))
            try:
                with urlopen(api_url + '/health', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        else:
            raise RuntimeError('API host startup timed out.')
        status, body, _ = call('/KuasSSOWebService.asmx?wsdl')
        wsdl = ET.fromstring(body)
        assert status == 200
        assert {e.get('name') for e in wsdl.iter('{http://schemas.xmlsoap.org/wsdl/}operation')} == {'KuasSSO_Certify2', 'KuasSSO_Certify1', 'KuasSSO_SchCardSearch',
            'Get_Traffic_Pub', 'Get_Traffic_Teach', 'Get_Traffic_Teach_Temp', 'Get_Traffic_Stud', 'Get_Traffic_Stud_Temp', 'NkustSSO_DocumentApplyPaymentSystemSearch'}
        for version in [SOAP11, SOAP12]:
            headers = {'Content-Type': 'text/xml; charset=utf-8', 'SOAPAction': '"' + ACTION + '"'} if version == SOAP11 else {
                'Content-Type': 'application/soap+xml; charset=utf-8; action="' + ACTION + '"'}
            for password, expected in [('correct', ['true', '02']), ('wrong', ['false', '500'])]:
                status, body, response_headers = call('/KuasSSOWebService.asmx', envelope(version, password=password), headers)
                assert status == 200, (status, body)
                root = ET.fromstring(body)
                assert root.tag == '{' + version + '}Envelope'
                values = list(root.iter('{' + NS + '}anyType'))
                assert [e.text for e in values] == expected
                assert [e.get('{http://www.w3.org/2001/XMLSchema-instance}type') for e in values] == ['xsd:boolean', 'xsd:string']
            for invalid in [envelope(version).replace('KuasSSO_Certify2', 'checkSession'),
                            '<!DOCTYPE x [<!ENTITY xxe SYSTEM "file:///never-read">]>' + envelope(version),
                            envelope(version).replace('<usr_id>SOAPUSER</usr_id>', '<usr_id>A</usr_id><usr_id>B</usr_id>')]:
                status, body, _ = call('/KuasSSOWebService.asmx', invalid, headers)
                assert status == 500 and ET.fromstring(body).find('.//{' + version + '}Fault') is not None
            status, body, _ = call('/KuasSSOWebService.asmx', envelope(version, user='OUTAGE'), headers)
            assert status == 500 and 'Fault' in body
        headers = {'Content-Type': 'application/json', 'X-Client-Id': 'fixture-json', 'X-Client-Secret': SECRET}
        for password, expected in [('correct', True), ('wrong', False)]:
            status, body, _ = call('/api/integration/v1/verify', json.dumps({'userName': 'JSONUSER', 'password': password}), headers)
            assert status == 200 and json.loads(body) == {'verified': expected}
        assert call('/api/integration/v1/verify', '{}', {'Content-Type': 'application/json'})[0] == 403
        assert call('/api/integration/v1/verify', '{', headers)[0] == 400
        assert call('/api/integration/v1/verify', 'null', headers)[0] == 400
        assert call('/api/integration/v1/verify', json.dumps({'userName': 'OUTAGE', 'password': 'correct'}), headers)[0] == 503
        assert call('/api/integration/v1/verify', '{}', {**headers, 'Content-Type': 'text/plain'})[0] == 415
        assert call('/KuasSSOWebService.asmx/KuasSSO_Certify2?usr_id=x&usr_pwd=x')[0] == 404
        assert call('/KuasSSOWebService.asmx', envelope(), {'Content-Type': 'text/xml', 'SOAPAction': 'wrong'})[0] == 500
        for _ in range(5):
            assert json.loads(call('/api/integration/v1/verify', json.dumps({'userName': 'LOCKME', 'password': 'wrong'}), headers)[1]) == {'verified': False}
        assert 'false' in call('/KuasSSOWebService.asmx', envelope(user='lockme'), {'Content-Type': 'text/xml', 'SOAPAction': ACTION})[1]
        status, _, _ = call('/api/integration/v1/verify', json.dumps({'userName': 'x', 'password': 'x' * 20_000}), headers)
        assert status == 413
        for _ in range(65):
            status, _, _ = call('/KuasSSOWebService.asmx?wsdl')
            if status == 429:
                break
        assert status == 429
        print('PASS SOAP 1.1/1.2, WSDL, typed arrays, XML rejection, JSON authorization, shared lockout, source outages, request limits and no cookies')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)

fixture = ServerFixture()
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        violations, errors = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e=>window.recordCsp({directive:e.violatedDirective,source:e.sourceFile,line:e.lineNumber,sample:e.sample}));")
        assert context.request.post(fixture.url + '/KuasSSOWebService.asmx', data=envelope(), headers={'Content-Type': 'text/xml', 'SOAPAction': ACTION}).status == 503
        assert context.request.post(fixture.url + '/api/integration/v1/verify', data={}).status == 503
        assert context.request.get(fixture.url + '/admin/integration-services', max_redirects=0).status == 302
        page = context.new_page()
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(fixture.url + '/admin/systems/api?kind=new')
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems/api?kind=new')
        for protocol in ['soap', 'json']:
            page.get_by_role('link', name='新增串接系統').click()
            page.get_by_label('系統代碼').fill('test-' + protocol)
            page.get_by_label('系統名稱').fill('測試 ' + protocol)
            page.get_by_label('帳密驗證', exact=True).check()
            page.locator('#Client_Protocol').evaluate('(el, value) => el.tomselect.setValue(value)', protocol)
            page.get_by_label('允許來源 IP').fill('127.0.0.1\n::1')
            page.get_by_label('啟用此串接').check()
            page.get_by_role('button', name='儲存設定').click()
            if protocol == 'json':
                expect(page.locator('#integration-secret')).to_be_visible()
                first_secret = page.locator('#integration-secret').inner_text()
                page.get_by_role('link', name='返回帳密驗證 API').click()
            expect(page.get_by_role('heading', name='帳密驗證 API', exact=True)).to_be_visible()
            expect(page.locator('table').first).to_contain_text('test-' + protocol)
            page.goto(fixture.url + '/admin/integration-services/test-' + protocol + '/edit')
            fields = page.locator('section form').evaluate('form => Object.fromEntries(new FormData(form))')
            assert context.request.post(page.url, form={}).status == 400
            page.get_by_label('系統名稱').fill('更新 ' + protocol)
            page.get_by_role('button', name='儲存設定').click()
            page.wait_for_url(fixture.url + '/admin/systems/api?kind=new')
            stale = context.request.post(fixture.url + '/admin/integration-services/test-' + protocol + '/edit', form=fields)
            assert '其他管理者修改' in html.unescape(stale.text())
            page.goto(fixture.url + '/admin/systems/api?kind=new')
        row = page.locator('table').first.locator('tr', has_text='test-json')
        row.get_by_text('更新密鑰', exact=True).click()
        row.get_by_role('button', name='產生新密鑰').click()
        expect(page.locator('#integration-secret')).to_be_visible()
        assert page.locator('#integration-secret').inner_text() != first_secret
        page.get_by_role('link', name='返回帳密驗證 API').click()
        shots = fixture.directory / 'screenshots'
        shots.mkdir()
        for width in [1440, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), f'Overflow at {width}'
            page.screenshot(path=str(shots / f'integration-{width}.png'), full_page=True)
        assert page.evaluate('jQuery.fn.jquery') == '4.0.0'
        assert page.evaluate('DataTable.version') == '3.1.4'
        assert page.locator('.dt-search').count() == 2
        student_context = browser.new_context(ignore_https_errors=True)
        student = student_context.new_page()
        student.goto(fixture.url + '/account/login')
        student.get_by_label('校園帳號', exact=True).fill('002')
        student.get_by_label('密碼', exact=True).fill('123456')
        student.get_by_role('button', name='登入', exact=True).click()
        student.wait_for_url(fixture.url + '/')
        assert student_context.request.get(fixture.url + '/admin/integration-services').status == 403
        assert not violations and not errors, (violations, errors)
        browser.close()
        print('PASS integration admin CRUD, rotation, CSRF, concurrency, role enforcement, disabled-by-default, newest frontend libraries and responsive CSP checks')
        print('Screenshots:', shots)
finally:
    fixture.close()
