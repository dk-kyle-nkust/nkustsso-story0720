"""說明頁與 SOAP／JSON 瀏覽器測試，使用合成帳密及隔離驗證來源。"""
import os
from pathlib import Path
import socket
import subprocess
import time
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect
from server_fixture import ROOT, BUILD_DIRECTORY, CREATE_FLAGS, ServerFixture

with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    base = f'http://127.0.0.1:{sock.getsockname()[1]}'
assembly = BUILD_DIRECTORY / 'NkustSso.StorageChecks.dll'
shots = ROOT / '.local/integration-help-checks'
shots.mkdir(exist_ok=True)
secret = 'fixture-json-secret-with-at-least-32-characters'
with (shots / 'server.log').open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(assembly), '--integration-api', base], cwd=ROOT,
                               stdout=log, stderr=subprocess.STDOUT, creationflags=CREATE_FLAGS)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError('隔離測試站啟動失敗。')
            try:
                with urlopen(base + '/health', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        else:
            raise RuntimeError('隔離測試站啟動逾時。')
        with sync_playwright() as p:
            chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
            browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
            context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
            errors, violations, calls = [], [], []
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp({directive:e.violatedDirective,source:e.sourceFile}));")
            page = context.new_page()
            page.on('pageerror', lambda e: errors.append(str(e)))
            page.on('request', lambda r: calls.append((r.url, r.method)) if r.method == 'POST' else None)
            page.goto(base + '/KuasSSOWebService.asmx')
            expect(page.get_by_role('heading', name='Web Service 串接說明', exact=True)).to_be_visible()
            expect(page.locator('[data-method]')).to_have_count(61)
            page.get_by_label('搜尋方法').fill('離校')
            expect(page.locator('[data-method]:visible')).to_have_count(1)
            page.locator('[data-method]:visible').click()
            expect(page.get_by_role('heading', name='畢業離校系統登入驗證', exact=True)).to_be_visible()
            assert page.locator('#soap-test').count() == 0
            expect(page.locator('main')).to_contain_text('Email 在該分支未回傳')
            page.goto(base + '/KuasSSOWebService.asmx?op=checkSession')
            expect(page.locator('main dl dd').last).to_have_text('http://tempuri.org')
            assert context.request.get(base + '/KuasSSOWebService.asmx?op=unknown').status == 404
            page.goto(base + '/KuasSSOWebService.asmx?op=NkustSSO_DocumentApplyPaymentSystemSearch')
            expect(page.locator('main .status-row')).to_contain_text('已實作')
            expect(page.locator('main')).to_contain_text('GRADSTUD 沿用舊校務條件')
            expect(page.locator('main')).to_contain_text('std_email')
            assert page.locator('#soap-test').count() == 0
            for width in [1440, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), ('document', width)
                page.screenshot(path=str(shots / f'document-{width}.png'), full_page=True)
            page.set_viewport_size({'width': 1440, 'height': 1000})
            page.goto(base + '/KuasSSOWebService.asmx?op=KuasSSO_Certify2')
            for version in ['1.1', '1.2']:
                page.select_option('#test-version', version)
                page.locator('#test-user').fill('SOAPUSER')
                page.locator('#test-password').fill('correct')
                page.get_by_role('button', name='送出測試').click()
                expect(page.locator('#test-status')).to_contain_text('驗證通過')
                expect(page.locator('#test-status')).to_contain_text('HTTP 200')
                expect(page.locator('#test-password')).to_have_value('')
                expect(page.locator('#test-response')).to_contain_text('true')
            page.locator('#test-password').fill('wrong')
            page.get_by_role('button', name='送出測試').click()
            expect(page.locator('#test-status')).to_contain_text('驗證未通過')
            page.locator('#test-user').fill('OUTAGE')
            page.locator('#test-password').fill('correct')
            page.get_by_role('button', name='送出測試').click()
            expect(page.locator('#test-status')).to_contain_text('HTTP 500')
            for width in [1440, 768, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), ('soap', width)
                page.screenshot(path=str(shots / f'soap-{width}.png'), full_page=True)
            page.goto(base + '/api/integration?clientId=fixture-json')
            expect(page.locator('#json-client')).to_have_value('fixture-json')
            for password, expected in [('correct', '驗證通過'), ('wrong', '驗證未通過')]:
                page.locator('#json-secret').fill(secret)
                page.locator('#json-user').fill('JSONUSER')
                page.locator('#json-password').fill(password)
                page.get_by_role('button', name='送出測試').click()
                expect(page.locator('#test-status')).to_contain_text(expected)
                expect(page.locator('#test-status')).to_contain_text('HTTP 200')
                expect(page.locator('#json-secret')).to_have_value('')
                expect(page.locator('#json-password')).to_have_value('')
            page.locator('#json-secret').fill('wrong-secret-with-at-least-32-characters')
            page.locator('#json-password').fill('correct')
            page.get_by_role('button', name='送出測試').click()
            expect(page.locator('#test-status')).to_contain_text('HTTP 403')
            for width in [1440, 768, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), ('json', width)
                page.screenshot(path=str(shots / f'json-{width}.png'), full_page=True)
            assert all('password' not in url and secret not in url for url, _ in calls)
            assert page.evaluate('localStorage.length === 0 && sessionStorage.length === 0')
            assert secret not in page.content()
            # 部署開關關閉時，只提供說明，不允許從頁面繞過服務限制。
            fixture = ServerFixture()
            try:
                for route in ['/KuasSSOWebService.asmx?op=KuasSSO_Certify2', '/api/integration']:
                    page.goto(fixture.url + route)
                    expect(page.get_by_role('button', name='送出測試')).to_be_disabled()
                    expect(page.locator('main')).to_contain_text('服務尚未開啟')
            finally:
                fixture.close()
            assert not errors and not violations, (errors, violations)
            browser.close()
        print('PASS SOAP/JSON help, Chinese descriptions/search, unavailable methods, actual SOAP 1.1/1.2 and JSON tests, errors, secret clearing, disabled services, responsive layout and CSP')
        print('Screenshots:', shots)
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
