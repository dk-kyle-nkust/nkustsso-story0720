"""Isolated real-HTTPS checks for system administration and claim release.

Build first; pass the server assembly as argv[1] if using an alternate output.
Creates its own database, port and browser, and stops its own server in finally.
Never prints client secrets, authorization codes, tickets or cookie values.
"""
import base64
import hashlib
import html
import json
import os
from pathlib import Path
from server_fixture import seed_server
import secrets
import socket
import sqlite3
import ssl
import subprocess
import sys
import time
from urllib.parse import urlencode, urlparse, parse_qs
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'NkustSso.Server/bin/Debug/net10.0/NkustSso.Server.dll'
DATA = ROOT / '.local/identity-checks' / secrets.token_hex(8)
DATA.mkdir(parents=True)
seed_server(DATA)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    PORT = sock.getsockname()[1]
SERVER = f'https://localhost:{PORT}'
CALLBACK = 'https://localhost:7141/callback'
process = None
log = None


def start():
    global process, log
    log = (DATA / 'server.log').open('a', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(ASSEMBLY), '--contentRoot', str(ROOT / 'NkustSso.Server'),
                                '--urls', SERVER, '--Sso:Issuer', SERVER + '/', '--Sso:DataDirectory', str(DATA), '--Sso:StorageProvider', 'SQLite', '--CampusAuthentication:Enabled', 'false', '--ConnectionStrings:LoginAuth', ''],
                               cwd=ROOT, env={**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development', 'CampusAuthentication__Enabled': 'false'},
                               stdout=log, stderr=subprocess.STDOUT,
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    for _ in range(100):
        if process.poll() is not None:
            raise AssertionError('Isolated server exited; inspect its local test log.')
        try:
            with urlopen(SERVER + '/health', context=ssl._create_unverified_context(), timeout=1) as result:
                if result.status == 200:
                    return
        except OSError:
            time.sleep(.2)
    raise AssertionError('Isolated server startup timeout.')


def stop():
    global process, log
    if process and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    if log:
        log.close()


def login(page, account):
    page.goto(SERVER + '/account/login')
    page.get_by_label('校園帳號', exact=True).fill(account)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=False).click()
    page.wait_for_url(SERVER + '/')


def choose(locator, value):
    locator.evaluate('(el, value) => el.tomselect ? el.tomselect.setValue(value) : null', value)


def condition(card, dimension, values):
    part = card.locator(f'.audience-condition[data-dimension="{dimension}"]')
    if part.count() == 0:
        choose(card.locator('.audience-add-condition select'), dimension)
        card.get_by_role('button', name='新增限制').click()
    choose(part.locator('select.condition-mode'), 'specified')
    if part.locator('select[multiple]').count():
        choose(part.locator('select[multiple]'), values)
    else:
        for checkbox in part.locator('input[type="checkbox"]').all():
            checkbox.set_checked(checkbox.get_attribute('value') in values)


try:
    start()
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        admin.add_init_script("window.cspErrors=[]; document.addEventListener('securitypolicyviolation', e=>window.cspErrors.push(e.violatedDirective));")
        student = browser.new_context(ignore_https_errors=True)
        page, home = admin.new_page(), student.new_page()
        errors, csp = [], []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.on('console', lambda msg: csp.append(dict(url=page.url, text=msg.text, location=msg.location)) if 'Content Security Policy' in msg.text else None)
        login(page, '001')
        login(home, '002')
        assert student.request.get(SERVER + '/admin/identity-catalog').status == 403
        assert admin.request.post(SERVER + '/admin/identity-catalog/options/new', form={}).status == 404
        page.goto(SERVER + '/admin/identity-catalog?dimension=grade')
        expect(page.locator('a[href="/admin/identity-catalog/dimensions/new"]')).to_have_count(0)
        assert admin.request.get(SERVER + '/admin/identity-catalog/dimensions/new').status == 404
        expect(page.locator('main [role=alert]')).to_contain_text('尚未啟用校務資料庫連線')
        token = page.locator('[name="__RequestVerificationToken"]').first.input_value()
        assert admin.request.post(SERVER + '/admin/identity-catalog/dimensions/new', form={
            '__RequestVerificationToken': token, 'Dimension.Key': 'college', 'Dimension.Name': '學院', 'Dimension.Family': 'student'
        }).status == 404
        assert page.locator('main a[href*="/edit"], main a[href*="/new"]').count() == 0
        with sqlite3.connect(DATA / 'sso.db') as db:
            # 舊分類僅作回歸測試資料，不經已移除的維護端點寫入。
            db.execute('INSERT INTO IdentityOptions (Id,Dimension,Code,Label,Family,Enabled,SortOrder,Revision) VALUES (?,?,?,?,?,1,?,?)',
                       ('year3', 'grade', 'year3', '三年級', 'other', 85, 'test'))
            assert db.execute('SELECT count(*) FROM IdentityDimensions').fetchone()[0] == 10
            for i in range(10):
                db.execute('INSERT INTO IdentityOptions (Id,Dimension,Code,Label,Family,Enabled,SortOrder,Revision) VALUES (?,?,?,?,?,1,?,?)',
                           (f'campus{i}', 'campus', f'C{i}', f'校區 {i}', 'other', i, 'test'))
        page.goto(SERVER + '/admin/systems/new')
        page.locator('[name="System.DisplayName"]').fill('分組身分測試')
        page.locator('[name="System.ClientId"]').fill('identity_rules')
        choose(page.locator('[name="System.Protocol"]'), 'link')
        page.locator('[name="System.EntryUrl"]').fill('https://identity.example.edu/')
        page.locator('[name="audience-mode"][value="rules"]').check()
        page.locator('[data-add-family="student"]').click()
        card = page.locator('.audience-card').first
        condition(card, 'student_status', ['DEMO_ACTIVE'])
        condition(card, 'division', ['DEMO_DAY'])
        condition(card, 'campus', ['C0', 'C1'])
        condition(card, 'grade', ['year3'])
        expect(page.locator('#audience-summary')).to_contain_text('三年級')
        # 多選條件使用 Tom Select，並可回到明確的全部。
        assert card.locator('[data-dimension="campus"] .ts-wrapper.multi').count() == 1
        choose(card.locator('[data-dimension="campus"] select.condition-mode'), 'all')
        choose(card.locator('[data-dimension="grade"] select.condition-mode'), 'all')
        page.locator('[data-add-family="employee"]').click()
        employee = page.locator('.audience-card').nth(1)
        condition(employee, 'identity_type', ['teacher'])
        condition(employee, 'employment_status', ['DEMO_CURRENT'])
        condition(employee, 'appointment_type', ['DEMO_FULL_TIME'])
        employee.get_by_role('button', name='複製這組').click()
        assert page.locator('.audience-card').count() == 3
        page.locator('.audience-card').nth(2).get_by_role('button', name='移除這組').click()
        assert page.locator('.audience-card').count() == 2
        page.get_by_role('button', name='建立系統', exact=True).click()
        page.wait_for_load_state('networkidle')
        assert not page.locator('.validation:visible').count(), page.locator('.validation:visible').all_text_contents()
        assert not errors, errors
        page.wait_for_url(SERVER + '/admin/systems')
        print('PASS read-only catalog, removed mutation routes, legacy fixtures, progressive rules, copy/remove and saving')

        # 真實首頁：同一帳號有在校夜間、畢業日間兩筆學生身分，仍不符合在校日間。
        with sqlite3.connect(DATA / 'sso.db') as db:
            account = db.execute("SELECT Id FROM AspNetUsers WHERE UserName='002'").fetchone()[0]
            db.execute('INSERT INTO AccountIdentitySets (AccountId,Revision,UpdatedAt,Source) VALUES (?,?,?,?)', (account, 'test', 0, 'test'))
            for ident, status, division in [('night', 'DEMO_ACTIVE', 'DEMO_NIGHT'), ('graduate', 'DEMO_GRADUATED', 'DEMO_DAY')]:
                db.execute('INSERT INTO AccountIdentities (AccountId,Id,IdentityType,AttributesJson,Enabled) VALUES (?,?,?,?,1)',
                           (account, ident, 'student', json.dumps(dict(student_status=status, division=division))))
        home.goto(SERVER + '/portal/student')
        expect(home.get_by_text('分組身分測試', exact=True)).to_have_count(0)
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute('INSERT INTO AccountIdentities (AccountId,Id,IdentityType,AttributesJson,Enabled) VALUES (?,?,?,?,1)',
                       (account, 'teacher', 'teacher', json.dumps(dict(employment_status='DEMO_CURRENT', appointment_type='DEMO_FULL_TIME'))))
        home.reload()
        expect(home.get_by_text('分組身分測試', exact=True)).to_have_count(1)
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE AccountIdentities SET Enabled=0 WHERE Id='teacher'")
        home.reload()
        expect(home.get_by_text('分組身分測試', exact=True)).to_have_count(0)
        print('PASS homepage uses separate active identities, OR groups and no cross-identity matching')

        page.goto(SERVER + '/admin/systems/identity_rules/edit')
        for card in page.locator('.audience-card').all():
            card.locator('summary').click()
        artifacts = DATA / 'screenshots'
        artifacts.mkdir()
        page.locator('#audience-editor').screenshot(path=str(artifacts / 'audience-desktop.png'))
        page.set_viewport_size({'width': 390, 'height': 844})
        assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth')
        page.locator('#audience-editor').screenshot(path=str(artifacts / 'audience-mobile.png'))
        card = page.locator('.audience-card').first
        condition(card, 'student_status', [])
        page.get_by_role('button', name='儲存設定').click()
        expect(page.locator('#audience-error')).to_be_visible()
        assert page.url.endswith('/edit')
        condition(card, 'student_status', ['DEMO_ACTIVE'])
        # 略過 JS 直接送壞資料也必須拒絕，不可變成全部。
        page.locator('#AudienceJson').evaluate('(el) => {el.value="{}"; el.form.submit();}')
        page.wait_for_load_state('networkidle')
        expect(page.locator('.validation').first).to_be_visible()
        with sqlite3.connect(DATA / 'sso.db') as db:
            rules = json.loads(db.execute("SELECT RulesJson FROM PortalAudiences WHERE ClientId='identity_rules'").fetchone()[0])
            assert rules['mode'] == 'rules' and len(rules['groups']) == 2
            dayid = db.execute("SELECT Id FROM IdentityOptions WHERE Dimension='division' AND Code='DEMO_DAY'").fetchone()[0]
        assert admin.request.get(SERVER + f'/admin/identity-catalog/options/{dayid}/edit').status == 404
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute('UPDATE IdentityOptions SET Label=?,Enabled=0 WHERE Id=?', ('日間部（新版名稱）', dayid))
        page.goto(SERVER + '/admin/identity-catalog?dimension=division')
        assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth')
        page.screenshot(path=str(artifacts / 'catalog-mobile.png'), full_page=True)
        page.goto(SERVER + '/admin/systems/identity_rules/edit')
        expect(page.locator('#audience-summary')).to_contain_text('日間部（新版名稱）（已停用）')
        page.get_by_role('button', name='儲存設定').click()
        page.wait_for_url(SERVER + '/admin/systems')
        page.goto(SERVER + '/admin/systems/identity_rules/edit')
        card = page.locator('.audience-card').first
        card.locator('summary').click()
        condition(card, 'campus', ['C0'])
        page.get_by_role('button', name='儲存設定').click()
        page.wait_for_url(SERVER + '/admin/systems')
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE IdentityOptions SET Enabled=0 WHERE Id='campus0'")
        page.goto(SERVER + '/admin/systems/identity_rules/edit')
        expect(page.locator('#audience-summary')).to_contain_text('校區 0（已停用）')
        page.get_by_role('button', name='儲存設定').click()
        page.wait_for_url(SERVER + '/admin/systems')
        assert not page.evaluate('window.cspErrors') and not errors and not csp, (errors, csp)
        print('PASS empty/forged rules rejected, disabled reference retained, desktop/mobile layout and zero CSP/JS errors')
        browser.close()
finally:
    stop()
    print('Isolated identity verification service stopped.')

