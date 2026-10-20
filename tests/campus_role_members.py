"""以唯讀模擬來源驗證校務人員在角色編輯頁加入、權限防護與桌面手機介面。"""
import os
import secrets
import socket
import subprocess
import sys
import time
from pathlib import Path
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect

sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parents[1]
BUILD = Path(os.environ.get('NKUST_TEST_BUILD_DIRECTORY', ROOT / 'tests/NkustSso.StorageChecks/bin/Debug/net10.0'))
DATA = ROOT / '.local/campus-role-ui' / secrets.token_hex(8)
DATA.mkdir(parents=True)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    URL = f'http://127.0.0.1:{sock.getsockname()[1]}'

with (DATA / 'server.log').open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(BUILD / 'NkustSso.StorageChecks.dll'), '--campus-role-ui', URL], cwd=ROOT,
        stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise AssertionError('隔離測試網站啟動失敗，請查閱測試紀錄。')
            try:
                with urlopen(URL + '/health', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        with sync_playwright() as p:
            chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
            browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
            context = browser.new_context(viewport={'width': 1440, 'height': 1000})
            errors, violations = [], []
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp({directive:e.violatedDirective,url:e.documentURI,file:e.sourceFile,line:e.lineNumber,sample:e.sample}));")
            page = context.new_page()
            page.on('pageerror', lambda e: errors.append(str(e)))
            route = URL + '/admin/access/campus-role'

            def members():
                return page.eval_on_selector_all('select[name=Members] option:checked',
                                                 'els => els.map(e => e.textContent.trim())')

            def add_local(name):
                # 點下拉選項而不是按 Enter：過濾還沒完成時 Enter 會外洩成表單送出。
                page.locator('.ts-control').first.click()
                page.keyboard.type(name)
                option = page.locator('.ts-dropdown .option', has_text=name).first
                option.wait_for(state='visible')
                option.click()

            def add_campus(account):
                page.get_by_label('加入校務人員（完整校務帳號）').fill(account)
                page.get_by_role('button', name='查詢', exact=True).click()
                expect(page.locator('[data-campus-summary]')).to_contain_text('測試人員 ' + account)
                page.get_by_role('button', name='加入成員', exact=True).click()
                expect(page.locator('[data-campus-status]')).to_contain_text('已放進成員欄')

            # 查詢與加入都在編輯頁完成，不跳頁；桌面與手機都要能操作。
            for width, account in [(1440, 'E901'), (390, 'E904')]:
                page.set_viewport_size({'width': width, 'height': 1000})
                page.goto(route + '/edit')
                add_campus(account)
                assert page.url == route + '/edit', '加入校務人員不應離開編輯頁'
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(DATA / f'inline-{width}.png'), full_page=True)
                page.get_by_role('button', name='儲存角色', exact=True).click()
                page.wait_for_url(URL + '/admin/access')
                page.goto(route + '/edit')
                expect(page.locator('.ts-control .item')).to_contain_text(
                    ['測試人員 E901'] if width == 1440 else ['測試人員 E901', '測試人員 E904'])

            # 回歸：先在成員欄選人、再加入校務人員，原本選的人不能消失。
            page.set_viewport_size({'width': 1440, 'height': 1000})
            page.goto(route + '/edit')
            add_local('fixture-member')
            chosen = members()
            assert any('fixture-member' in item for item in chosen), chosen
            add_campus('E902')
            kept = members()
            assert any('fixture-member' in item for item in kept), kept
            assert any('E902' in item for item in kept), kept
            page.get_by_role('button', name='儲存角色', exact=True).click()
            page.wait_for_url(URL + '/admin/access')
            page.goto(route + '/edit')
            saved = members()
            assert any('fixture-member' in item for item in saved), saved
            assert any('E902' in item for item in saved), saved

            # 查詢失敗、缺漏與重複來源只顯示訊息，不建立帳號也不改成員。
            before = members()
            for account, message in [('MISSING', '查無此校務帳號'), ('DUP', '不唯一'), ('INCOMPLETE', '不完整')]:
                page.get_by_label('加入校務人員（完整校務帳號）').fill(account)
                page.get_by_role('button', name='查詢', exact=True).click()
                expect(page.locator('[data-campus-status]')).to_contain_text(message)
                expect(page.locator('[data-campus-candidate]')).to_be_hidden()
            assert members() == before, '查詢失敗不應改動成員欄'

            # 缺少防偽造 token 與未授權的帳號都不得呼叫查詢或建立。
            for action in ['find', 'provision']:
                assert context.request.post(route + '/members/' + action, form={'accountName': 'E905'}).status == 400
            member = browser.new_context(extra_http_headers={'X-Fixture-User': 'member'})
            for action in ['find', 'provision']:
                assert member.request.post(route + '/members/' + action, form={'accountName': 'E905'}).status == 403
            assert member.request.get(route + '/edit').status == 403

            assert not errors and not violations, (errors, violations)
            browser.close()
            print('PASS 編輯頁內查詢與加入、未儲存成員保留、桌面手機、重複與缺漏來源、CSRF、權限與 CSP')
            print('Screenshots:', DATA)
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
