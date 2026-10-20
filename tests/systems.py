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
DATA = ROOT / '.local/system-checks' / secrets.token_hex(8)
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
                               cwd=ROOT, env={**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development'},
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


def create(page, client, protocol):
    page.goto(SERVER + '/admin/systems/new')
    assert page.locator('input[name="AllowedClaims"]:checked').count() == 0
    page.locator('[name="System.DisplayName"]').fill('測試系統 ' + client)
    page.locator('[name="System.ClientId"]').fill(client)
    page.locator('select[name="System.Protocol"]').select_option(protocol, force=True)
    page.locator('[name="System.EntryUrl"]').fill('https://course.example.edu/')
    page.locator('[name="System.RedirectUri"]').fill(CALLBACK)
    page.get_by_role('button', name='建立系統', exact=True).click()
    expect(page.get_by_role('heading', name='系統已建立')).to_be_visible()
    return page.locator('#client-secret').inner_text()


def edit(page, client, mask=None, enabled=None, visible=None, audience=None):
    page.goto(SERVER + f'/admin/systems/{client}/edit')
    if mask is not None:
        for checkbox in page.locator('input[name="AllowedClaims"]').all():
            checkbox.set_checked(bool(int(checkbox.get_attribute('value')) & mask))
    if enabled is not None:
        page.locator('[name="System.Enabled"][type="checkbox"]').set_checked(enabled)
    if visible is not None:
        page.locator('[name="System.ShowOnPortal"][type="checkbox"]').set_checked(visible)
    if audience is not None:
        dimensions = {'IdentityTypes': 'identity_type', 'StudentStatuses': 'student_status', 'EducationLevels': 'education_level'}
        conditions = [dict(dimension=dimension, values=audience[name]) for name, dimension in dimensions.items() if audience.get(name)]
        rules = dict(version=2, mode='rules' if conditions else 'all', groups=[dict(family='any', conditions=conditions)] if conditions else [])
        # 此檔驗證後端整合；identities.py 另以實際控制項驗證規則編輯器。
        page.locator('#AudienceJson').evaluate('(el, value) => { el.value = value; el.form.submit(); }', json.dumps(rules))
    else:
        page.get_by_role('button', name='儲存設定').click()
    page.wait_for_url(SERVER + '/admin/systems?section=sso')


def issue(context, client, protocol, callback=CALLBACK):
    verifier = secrets.token_urlsafe(32)
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('=')
    query = dict(client_id=client, redirect_uri=callback, state=secrets.token_urlsafe(32),
                 code_challenge=challenge, code_challenge_method='S256')
    if protocol == 'oidc':
        query.update(response_type='code', scope='openid profile student_profile', nonce=secrets.token_urlsafe(32))
    path = '/connect/authorize' if protocol == 'oidc' else '/ticket/authorize'
    response = context.request.get(SERVER + path + '?' + urlencode(query), max_redirects=0)
    return response, verifier


def redeem(context, client, protocol, secret, response, verifier, callback=CALLBACK):
    assert response.status == 302, 'Expected authorization redirect.'
    parameter = 'code' if protocol == 'oidc' else 'ticket'
    code = parse_qs(urlparse(response.headers['location']).query)[parameter][0]
    form = dict(redirect_uri=callback, code_verifier=verifier)
    if protocol == 'oidc':
        form.update(grant_type='authorization_code', client_id=client, client_secret=secret, code=code)
        return context.request.post(SERVER + '/connect/token', form=form)
    form['ticket'] = code
    auth = base64.b64encode((client + ':' + secret).encode()).decode()
    return context.request.post(SERVER + '/ticket/redeem', form=form, headers={'Authorization': 'Basic ' + auth})


def payload(response, protocol):
    assert response.status == 200, f'Exchange failed: HTTP {response.status}'
    if protocol == 'ticket':
        return response.json()
    encoded = response.json()['id_token'].split('.')[1]
    # Decode only to assert release policy; application middleware verifies signatures.
    return json.loads(base64.urlsafe_b64decode(encoded + '=' * (-len(encoded) % 4)))


PROFILE = {'name', 'preferred_username', 'username', 'identity_type', 'class_code', 'class_name',
           'student_status_code', 'student_status_name', 'education_level_code', 'education_level_name'}

try:
    start()
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True)
        csp_errors, script_errors = [], []
        admin.expose_binding('__recordSystemsCsp', lambda source, directive: csp_errors.append(directive))
        admin.add_init_script("document.addEventListener('securitypolicyviolation', e => window.__recordSystemsCsp(e.violatedDirective));")
        student = browser.new_context(ignore_https_errors=True)
        page, student_page = admin.new_page(), student.new_page()
        page.on('pageerror', lambda error: script_errors.append(str(error)))
        login(page, '001')
        login(student_page, '002')
        leave = browser.new_context(ignore_https_errors=True)
        leave_page = leave.new_page()
        login(leave_page, '003')
        assert student.request.get(SERVER + '/admin/systems').status == 403
        assert admin.request.post(SERVER + '/admin/systems/new', form={}).status == 400
        print('PASS administrator authorization and CSRF protection')
        page.goto(SERVER + '/admin/systems?section=sso')
        expect(page.get_by_role('heading', name='單一登入 SSO', exact=True)).to_be_visible()
        # 首頁連結在獨立分頁；此處只計算 SSO 系統，畫面依每頁 10 筆驗證。
        with sqlite3.connect(DATA / 'sso.db') as db:
            initial_count = db.execute("SELECT count(*) FROM RegisteredSystems WHERE Protocol != 'link'").fetchone()[0]
        initial_page_count = min(10, initial_count)
        expect(page.locator('.system-registry tbody tr')).to_have_count(initial_page_count)
        assert page.locator('.system-registry').count() == 1
        assert page.locator('.system-registry .protocol-badge.oidc').count() == 2
        assert page.locator('.system-registry .protocol-badge.ticket').count() == 1
        # 摘要預設收合，鍵盤仍可查看完整欄位，概覽跟隨伺服器端篩選。
        claim_detail = page.locator('.claims-detail').first
        assert not claim_detail.evaluate('(el) => el.open')
        claim_detail.locator('summary').focus()
        page.keyboard.press('Enter')
        expect(claim_detail.locator('li')).to_have_count(9)
        assert claim_detail.evaluate('(el) => el.open')
        page.keyboard.press('Enter')
        expect(page.locator('.systems-overview dd').first).to_have_text(str(initial_count) + '個系統')
        page.locator('select[name="Protocol"]').select_option('ticket', force=True)
        page.get_by_role('button', name='查詢', exact=True).click()
        expect(page.locator('.system-registry .protocol-badge.oidc')).to_have_count(0)
        expect(page.locator('.system-registry tbody tr')).to_have_count(1)
        expect(page.locator('select[name="Protocol"]')).to_have_value('ticket')
        expect(page.locator('.systems-overview dd').first).to_contain_text('1')
        # 關鍵字搜尋改由 DataTables 在前端處理，疊加在伺服器端的下拉條件之上。
        search_box = lambda: page.locator('.dt-container').filter(
            has=page.locator('.system-registry')).locator('input[type="search"]')
        search_box().fill('教務')
        expect(page.locator('.system-registry tbody .dt-empty')).to_be_visible()
        page.get_by_role('link', name='清除條件', exact=True).click()
        expect(page.locator('.system-registry tbody tr')).to_have_count(initial_page_count)
        search_box().fill('  ACADEMIC  ')
        expect(page.locator('.system-registry tbody tr code')).to_have_text('academic')
        search_box().fill('')
        expect(page.locator('.system-registry tbody tr')).to_have_count(initial_page_count)
        page.goto(SERVER + '/admin/systems?protocol=saml')
        expect(page.locator('.system-empty').first).to_contain_text('目前沒有符合條件的系統')
        expect(page.locator('.systems-overview dd').first).to_have_text('0個系統')
        page.get_by_role('link', name='查看本分頁全部系統').click()
        expect(page.locator('.system-registry tbody tr')).to_have_count(initial_page_count)
        assert admin.request.get(SERVER + '/admin/systems?protocol=invalid').status == 400
        assert admin.request.get(SERVER + '/admin/systems?status=invalid').status == 400
        assert admin.request.get(SERVER + '/admin/systems?visibility=invalid').status == 400
        print('PASS single list with protocol labels, search, combined empty results, clear and input validation')
        # Existing clients retain their secrets, all released fields and OIDC behavior.
        initial = json.loads((DATA / 'demo-credentials.json').read_text(encoding='utf-8-sig'))
        auth, verifier = issue(student, 'academic', 'oidc', 'https://localhost:7101/signin-oidc')
        old = payload(redeem(student, 'academic', 'oidc', initial['AcademicSecret'], auth, verifier,
                             'https://localhost:7101/signin-oidc'), 'oidc')
        assert old['class_code'] == 'DEMO-CS-3A' and old['student_status_code'] == 'DEMO_ACTIVE'
        print('PASS existing OIDC configuration and claims preserved')
        generated = []
        for protocol in ('oidc', 'ticket'):
            client = 'test_' + protocol
            secret = create(page, client, protocol)
            generated.append(secret)
            assert 'no-store' in admin.request.get(SERVER + '/admin/systems').headers['cache-control']
            student_page.goto(SERVER + '/portal/student')
            expect(student_page.locator(f'[data-system-id="{client}"]')).to_be_visible()
            auth, verifier = issue(student, client, protocol)
            data = payload(redeem(student, client, protocol, secret, auth, verifier), protocol)
            assert data['sub'] == 'demo-student-active' and not PROFILE.intersection(data)
            assert redeem(student, client, protocol, secret, auth, verifier).status == 400
            edit(page, client, mask=32)
            auth, verifier = issue(student, client, protocol)
            data = payload(redeem(student, client, protocol, secret, auth, verifier), protocol)
            assert data['student_status_code'] == 'DEMO_ACTIVE'
            assert PROFILE.intersection(data) == {'student_status_code'}
            assert not {'password', 'PasswordHash', 'security_stamp', 'role', 'local_role'}.intersection(data)
            # A pending login must obey a more restrictive policy at redemption time.
            auth, verifier = issue(student, client, protocol)
            edit(page, client, mask=0, visible=False)
            data = payload(redeem(student, client, protocol, secret, auth, verifier), protocol)
            assert not PROFILE.intersection(data)
            student_page.goto(SERVER + '/portal/student')
            expect(student_page.locator(f'[data-system-id="{client}"]')).to_have_count(0)
            auth, verifier = issue(student, client, protocol)
            assert auth.status == 302  # Hidden does not mean disabled.
            edit(page, client, enabled=False)
            page.goto(SERVER + '/admin/systems?' + urlencode(dict(keyword=client, protocol=protocol, status='disabled', visibility='hidden')))
            expect(page.locator('.system-registry tbody tr')).to_have_count(1)
            expect(page.locator('.system-registry tbody tr code')).to_have_text(client)
            expect(page.locator('.system-state')).to_have_text('停用')
            for row in page.locator('.system-audits tbody tr').all():
                assert row.locator('td').nth(1).inner_text() == client
            assert redeem(student, client, protocol, secret, auth, verifier).status in (400, 401)
            assert issue(student, client, protocol)[0].status == 400
            edit(page, client, enabled=True, mask=32)
            malicious, _ = issue(student, client, protocol, 'https://unregistered.example/callback')
            assert malicious.status == 400
            print('PASS ' + protocol + ': minimal claims, selected-only disclosure, replay, live policy, disable and callback allowlist')
        # Portal audience rules are independent of claim release and login permissions.
        def visible_to(target_page, client, expected):
            target_page.goto(SERVER + '/portal/student')
            expect(target_page.locator(f'[data-system-id="{client}"]')).to_have_count(1 if expected else 0)

        active_undergrad = dict(IdentityTypes=['student'], StudentStatuses=['DEMO_ACTIVE'], EducationLevels=['DEMO_UG'])
        both_students = dict(IdentityTypes=['student'], StudentStatuses=['DEMO_ACTIVE', 'DEMO_LEAVE'],
                             EducationLevels=['DEMO_UG', 'DEMO_MASTER'])
        for protocol in ('oidc', 'ticket'):
            client = 'test_' + protocol
            edit(page, client, visible=True, audience=active_undergrad)
            visible_to(student_page, client, True)
            visible_to(leave_page, client, False)
            visible_to(page, client, False)  # No implicit administrator visibility bypass.
            assert issue(leave, client, protocol)[0].status == 302
            edit(page, client, audience=both_students)
            visible_to(student_page, client, True)
            visible_to(leave_page, client, True)
            visible_to(page, client, False)
            edit(page, client, audience={**active_undergrad, 'EducationLevels': ['DEMO_MASTER']})
            visible_to(student_page, client, False)
            visible_to(leave_page, client, False)  # Dimensions are AND, not OR.
            edit(page, client, audience=both_students)
            with sqlite3.connect(DATA / 'sso.db') as db:
                db.execute("UPDATE AspNetUsers SET StudentStatusCode='' WHERE UserName='003'")
            visible_to(leave_page, client, False)  # Missing data fails selected conditions.
            with sqlite3.connect(DATA / 'sso.db') as db:
                db.execute("UPDATE AspNetUsers SET StudentStatusCode='DEMO_LEAVE' WHERE UserName='003'")
            visible_to(leave_page, client, True)  # Profile changes apply without another login.
            edit(page, client, audience={})
            for target in (student_page, leave_page, page):
                visible_to(target, client, True)
            edit(page, client, enabled=False)
            visible_to(student_page, client, False)
            edit(page, client, enabled=True, visible=False, audience=active_undergrad)
            visible_to(student_page, client, False)
        print('PASS portal audiences: identity/status/education, OR/AND, current/missing profile, all identities, hidden/disabled precedence')
        # Stale browser forms cannot undo newer administrator edits.
        page.goto(SERVER + '/admin/systems/test_ticket/edit')
        fields = page.locator('form.system-form').evaluate('''form => Object.fromEntries(new FormData(form))''')
        stale_revision = fields['System.Revision']
        edit(page, 'test_ticket', mask=32)
        fields['System.Revision'] = stale_revision
        stale = admin.request.post(SERVER + '/admin/systems/test_ticket/edit', form=fields)
        assert stale.status == 200 and '設定已被其他操作更新' in html.unescape(stale.text())
        page.goto(SERVER + '/admin/systems/test_ticket/edit')
        fields = page.locator('form.system-form').evaluate('''form => Object.fromEntries(new FormData(form))''')
        fields['AllowedClaims'] = '1024'
        assert '包含不允許提供的欄位' in html.unescape(admin.request.post(SERVER + '/admin/systems/test_ticket/edit', form=fields).text())
        fields.pop('AllowedClaims')
        fields['System.EntryUrl'] = 'javascript:alert(1)'
        assert '完整 HTTPS 網址' in html.unescape(admin.request.post(SERVER + '/admin/systems/test_ticket/edit', form=fields).text())
        print('PASS stale edit, unknown claim and unsafe URL rejected')
        # Screenshots exclude the once-only secret page.
        artifacts = ROOT / 'test-results'
        artifacts.mkdir(exist_ok=True)
        page.goto(SERVER + '/admin/systems?section=sso')
        page.set_viewport_size({'width': 1440, 'height': 1000})
        page.screenshot(path=str(artifacts / 'systems-desktop-viewport.png'))
        page.screenshot(path=str(artifacts / 'systems-desktop.png'), full_page=True)
        page.set_viewport_size({'width': 390, 'height': 844})
        page.screenshot(path=str(artifacts / 'systems-mobile-viewport.png'))
        assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth')
        page.screenshot(path=str(artifacts / 'systems-mobile.png'), full_page=True)
        page.locator('.system-registry tbody tr').first.screenshot(path=str(artifacts / 'systems-mobile-card.png'))
        page.goto(SERVER + '/admin/systems/test_ticket/edit')
        page.set_viewport_size({'width': 1440, 'height': 1000})
        page.screenshot(path=str(artifacts / 'system-editor-desktop.png'), full_page=True)
        page.set_viewport_size({'width': 390, 'height': 844})
        page.get_by_role('link', name='02 開放身分').click()
        expect(page.locator('#audience-editor')).to_be_in_viewport()
        assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth')
        page.screenshot(path=str(artifacts / 'system-editor-mobile.png'), full_page=True)
        assert not csp_errors and not script_errors, (csp_errors, script_errors)
        print('PASS compact summaries, keyboard expansion, filtered overview, editor navigation, responsive layout and zero CSP/JS errors')
        with sqlite3.connect(DATA / 'sso.db') as db:
            rows = db.execute('SELECT * FROM SystemConfigurationAudits').fetchall()
            serialized = str(rows)
            assert rows and all(secret not in serialized for secret in generated)
            assert db.execute('SELECT count(*) FROM SystemConfigurationAudits WHERE RemoteIp <> ?', ('',)).fetchone()[0] == len(rows)
            assert db.execute("SELECT SecretHash FROM TicketClients WHERE Id='test_ticket'").fetchone()[0] != generated[1]
            assert db.execute("SELECT ClientSecret FROM OidcApplications WHERE ClientId='test_oidc'").fetchone()[0] != generated[0]
            # Authorization uses the current role store, not just stale cookie claims.
            db.execute("DELETE FROM AspNetUserRoles WHERE UserId='demo-session-admin'")
        assert admin.request.get(SERVER + '/admin/systems').status == 403
        print('PASS audit IP, no raw secrets in storage/audit, live administrator-role check')
        stop()
        start()
        with sqlite3.connect(DATA / 'sso.db') as db:
            assert db.execute("SELECT count(*) FROM RegisteredSystems WHERE Protocol != 'link'").fetchone()[0] == initial_count + 2
            assert db.execute("SELECT ClaimMask,ShowOnPortal FROM RegisteredSystems WHERE ClientId='test_ticket'").fetchone() == (32, 0)
            audience = json.loads(db.execute("SELECT RulesJson FROM PortalAudiences WHERE ClientId='test_ticket'").fetchone()[0])
            assert audience['groups'][0]['conditions'] == [dict(dimension='identity_type', values=['student']), dict(dimension='student_status', values=['DEMO_ACTIVE']), dict(dimension='education_level', values=['DEMO_UG'])]
        print('PASS restart preserves managed configuration')
        browser.close()
finally:
    stop()
    print('Isolated verification service stopped.')
