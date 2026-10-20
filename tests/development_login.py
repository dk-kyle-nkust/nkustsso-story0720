"""隔離來源驗證開發登入、真實表單與桌面／手機提示。"""
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
BUILD = Path(os.environ.get('NKUST_TEST_BUILD_DIRECTORY', ROOT / 'tests/NkustSso.StorageChecks/bin/Debug/net10.0'))
DATA = ROOT / '.local/development-login' / secrets.token_hex(8)
DATA.mkdir(parents=True)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    URL = f'http://127.0.0.1:{sock.getsockname()[1]}'

with (DATA / 'server.log').open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(BUILD / 'NkustSso.StorageChecks.dll'), '--campus-login-ui', URL],
        cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise AssertionError('Test server exited; inspect isolated log.')
            try:
                with urlopen(URL + '/account/login', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        else:
            raise AssertionError('Test server startup timeout.')
        with sync_playwright() as p:
            chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
            browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
            context = browser.new_context()
            errors, violations = [], []
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.recordCsp(e.violatedDirective));")
            page = context.new_page()
            page.on('pageerror', lambda error: errors.append(str(error)))
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                page.goto(URL + '/account/login?area=student')
                expect(page.get_by_role('note')).to_contain_text('開發測試模式')
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(DATA / f'login-{width}.png'), full_page=True)

            def post(user, password):
                page.goto(URL + '/account/login?returnUrl=%2Faccount%2Fcurrent-user')
                token = page.locator('.login-form input[name=__RequestVerificationToken]').input_value()
                return context.request.post(URL + '/account/login', form={
                    'UserName': user, 'Password': password,
                    'ReturnUrl': '/account/current-user', '__RequestVerificationToken': token,
                    'DevelopmentAllowAnyPassword': 'true'}, max_redirects=0)

            for user, password in [('MISSING', 'anything'), ('S001', ''), ('S001', '   ')]:
                assert post(user, password).status == 200
                assert not context.request.get(URL + '/account/current-user').json()['signedIn']
            response = post('S001', 'any-development-password')
            assert response.status == 302
            current = context.request.get(URL + '/account/current-user').json()
            assert current['signedIn'] and current['userName'] == 'S001'
            assert not current['canManageSystems']
            context.clear_cookies()
            assert post('E010', 'another-development-password').status == 302
            current = context.request.get(URL + '/account/current-user').json()
            assert current['identityLabel'] == '已離職人員'
            assert current['displayName'] == '測試離職人員' and current['userName'] == 'E010'
            assert current['accountLabel'] == '帳號' and not current['canManageSystems']
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                page.goto(URL + '/account/login?reauthenticate=true')
                expect(page.locator('[data-current-user-identity]')).to_have_text('已離職人員')
                expect(page.locator('[data-current-user-display-name]')).to_have_text('測試離職人員')
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(DATA / f'status-{width}.png'), full_page=True)
            # 職員顯示所屬單位；離職人員與學生不顯示。
            context.clear_cookies()
            assert post('E003', 'staff-development-password').status == 302
            current = context.request.get(URL + '/account/current-user').json()
            assert current['unitName'] == 'Example Unit', current
            assert current['displayName'] == '測試職員' and current['accountLabel'] == '員編'
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                page.goto(URL + '/account/login?reauthenticate=true')
                expect(page.locator('[data-current-user-unit]')).to_have_text('Example Unit')
                expect(page.locator('[data-current-user-unit-row]')).to_be_visible()
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(DATA / f'unit-{width}.png'), full_page=True)
            context.clear_cookies()
            assert post('S001', 'student-development-password').status == 302
            assert context.request.get(URL + '/account/current-user').json()['unitName'] == ''
            page.goto(URL + '/account/login?reauthenticate=true')
            expect(page.locator('[data-current-user-unit-row]')).to_be_hidden()
            assert not errors and not violations, (errors, violations)
            browser.close()
            print('PASS arbitrary development password, unknown/blank rejection, no extra admin rights, desktop/mobile UI and CSP')
            print('Screenshots:', DATA)
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
