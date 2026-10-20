"""Current-user display, cross-tab updates and logout on an isolated server."""
import os
from pathlib import Path
import sqlite3
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture
from accessibility_checks import check_accessibility


fixture = ServerFixture()


def check_legacy_header(context):
    # 模擬已開啟的舊版 HTML 載入新版 JS，仍使用舊名稱節點。
    legacy = context.new_page()
    def old_markup(route):
        response = route.fetch()
        html = response.text().replace('data-current-user-account', 'data-current-user-name')
        route.fulfill(response=response, body=html)
    legacy.route('**/portal/student', old_markup)
    legacy.goto(fixture.url + '/portal/student')
    expect(legacy.locator('[data-current-user-class-row]')).to_have_count(0)
    cases = [
        (200, {'signedIn': False}),
        (200, {'signedIn': True, 'userName': 'compat-account', 'identityLabel': '學生',
               'logoutToken': 'test-only', 'tokenField': '__RequestVerificationToken'}),
        (503, {'error': 'temporarily unavailable'}),
    ]
    for status, body in cases:
        legacy.route('**/account/current-user', lambda route, request, status=status, body=body: route.fulfill(status=status, json=body))
        with legacy.expect_response(lambda r: r.url.endswith('/account/current-user')):
            legacy.evaluate("window.dispatchEvent(new Event('focus'))")
        if body.get('signedIn'):
            expect(legacy.locator('[data-current-user-name]')).to_have_text('compat-account')
            expect(legacy.locator('[data-current-user]')).to_be_visible()
        else:
            expect(legacy.locator('[data-current-user-name]')).to_have_text('')
            expect(legacy.locator('[data-current-user]')).to_be_hidden()
        legacy.unroute('**/account/current-user')
    legacy.route('**/account/current-user', lambda route: route.abort())
    with legacy.expect_event('requestfailed', predicate=lambda r: r.url.endswith('/account/current-user')):
        legacy.evaluate("window.dispatchEvent(new Event('focus'))")
    expect(legacy.locator('[data-account-login]')).to_be_visible()
    # 網路失敗及缺少欄位不得中斷原生連結導覽。
    legacy.locator('[data-account-login]').click()
    expect(legacy.locator('form.login-form')).to_be_visible()
    legacy.close()


try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        violations, errors = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.recordCsp(e.violatedDirective));")
        context.on('page', lambda page: page.on('pageerror', lambda error: errors.append(str(error))))
        check_legacy_header(context)
        assert not errors, errors
        original = context.new_page()
        original.goto(fixture.url + '/portal/student')
        expect(original.locator('[data-current-user]')).to_be_hidden()
        expect(original.locator('.site-header-navigation').get_by_role('link', name='登入', exact=True)).to_be_visible()
        expect(original.locator('header [data-current-user]')).to_have_count(0)
        response = context.request.get(fixture.url + '/account/current-user')
        assert response.json() == {'signedIn': False}
        assert 'no-store' in response.headers['cache-control']
        original.locator('#directory-search').fill('學務')

        login = context.new_page()
        login.goto(fixture.url + '/account/login?area=employee')
        expect(login.get_by_role('link', name='← 返回服務選單', exact=True)).to_have_count(0)
        login.get_by_label('校園帳號', exact=True).fill('002')
        login.get_by_label('密碼', exact=True).fill('123456')
        login.get_by_role('button', name='登入', exact=True).click()
        login.wait_for_url(fixture.url + '/')
        expect(login.locator('[data-current-user-account]')).to_have_text('002')
        expect(login.locator('[data-current-user-display-name]')).to_have_text('陳同學')
        expect(login.locator('[data-current-user-account-label]')).to_have_text('學號')
        expect(login.locator('[data-current-user-identity]')).to_have_text('學生')
        info = context.request.get(fixture.url + '/account/current-user?id=demo-session-admin').json()
        assert info['userName'] == '002' and info['identityLabel'] == '學生'
        assert info['canManageSystems'] is False
        assert info['displayName'] == '陳同學' and info['accountLabel'] == '學號'
        # 學生沒有教職員單位，unitName 必須是空字串而不是其他個資。
        assert info['unitName'] == ''
        assert set(info) == {'signedIn', 'userName', 'displayName', 'identityLabel', 'accountLabel',
                             'canManageSystems', 'unitName', 'logoutToken', 'tokenField'}
        assert context.request.post(fixture.url + '/account/logout').status == 400

        def refresh(page):
            page.bring_to_front()
            with page.expect_response(lambda r: r.url.endswith('/account/current-user')):
                page.evaluate("window.dispatchEvent(new Event('focus'))")

        refresh(original)
        expect(original.locator('[data-current-user-account]')).to_have_text('002')
        expect(original.locator('[data-current-user-class-row], [data-current-user-fields], [data-current-user-notice]')).to_have_count(0)
        expect(original.locator('[data-account-login]')).to_be_hidden()
        expect(original.locator('[data-manage-systems]')).to_be_hidden()
        expect(original.locator('.site-header-navigation').get_by_role('button', name='登出', exact=True)).to_be_visible()
        expect(original.locator('#directory-search')).to_have_value('學務')
        assert original.url == fixture.url + '/portal/student'
        original.get_by_role('button', name='Switch to English').click()
        expect(original.locator('[data-current-user-identity]')).to_have_text('Student')
        expect(original.locator('[data-current-user-account-label]')).to_have_text('Student ID')
        expect(original.locator('[data-current-user-display-name]')).to_have_text('陳同學')
        expect(original.locator('[data-current-user-account]')).to_have_text('002')
        shots = fixture.directory / 'screenshots'
        shots.mkdir()
        for width in [1440, 1024, 768, 390, 320]:
            original.set_viewport_size({'width': width, 'height': 1000})
            assert original.evaluate('document.documentElement.scrollWidth <= innerWidth')
            assert original.locator('.directory-account .site-current-user').evaluate("el => getComputedStyle(el).textAlign") == 'right'
            assert original.locator('[data-current-user]').evaluate("el => Math.abs(el.getBoundingClientRect().right - el.parentElement.getBoundingClientRect().right) < 2")
            original.screenshot(path=str(shots / f'user-header-en-{width}.png'), full_page=True)
        original.get_by_role('button', name='切換為繁體中文').click()
        expect(original.locator('[data-current-user-identity]')).to_have_text('學生')
        check_accessibility(original)
        # 姓名與學號使用純文字輸出；班級及學籍查詢資訊仍不顯示。
        rich_info = {**info, 'userName': '<img src=x onerror=alert(1)>', 'displayName': '<svg onload=alert(1)>',
                     'fields': [{'label': '學年期', 'value': '不應顯示'}],
                     'notice': '查無當學期就學資料。', 'className': '不應顯示班級'}
        original.route('**/account/current-user', lambda route: route.fulfill(json=rich_info))
        refresh(original)
        expect(original.locator('[data-current-user-account]')).to_have_text('<img src=x onerror=alert(1)>')
        expect(original.locator('[data-current-user-display-name]')).to_have_text('<svg onload=alert(1)>')
        expect(original.locator('[data-current-user] img, [data-current-user] svg')).to_have_count(0)
        expect(original.locator('[data-current-user]')).not_to_contain_text('不應顯示')
        expect(original.locator('[data-current-user-notice]')).to_have_count(0)
        original.unroute('**/account/current-user')
        refresh(original)
        expect(original.locator('[data-current-user-account]')).to_have_text('002')

        nojs = browser.new_context(ignore_https_errors=True, java_script_enabled=False, storage_state=context.storage_state())
        plain = nojs.new_page()
        plain.goto(fixture.url + '/portal/student')
        expect(plain.locator('[data-current-user-account]')).to_have_text('002')
        expect(plain.locator('[data-current-user-display-name]')).to_have_text('陳同學')
        expect(plain.locator('[data-current-user-account-label]')).to_have_text('學號')
        expect(plain.locator('[data-current-user-class]')).to_have_count(0)
        expect(plain.get_by_role('button', name='登出', exact=True)).to_be_visible()
        nojs.close()

        for identity_type, label, account_label in [('staff', '職員', '員編'), ('teacher', '教師', '員編'), ('employee', '教職員', '員編'), ('alumni', '校友', '帳號'), ('student', '學生', '學號')]:
            with sqlite3.connect(fixture.directory / 'sso.db') as db:
                db.execute("UPDATE AspNetUsers SET IdentityType=? WHERE UserName='002'", (identity_type,))
            refresh(original)
            expect(original.locator('[data-current-user-identity]')).to_have_text(label)
            expect(original.locator('[data-current-user-account]')).to_have_text('002')
            expect(original.locator('[data-current-user-account-label]')).to_have_text(account_label)
        # 缺少姓名時不編造姓名或重複顯示學號；長姓名也能在窄螢幕換行。
        for name in ['', '王小明 Long Family Name ' * 5]:
            with sqlite3.connect(fixture.directory / 'sso.db') as db:
                db.execute("UPDATE AspNetUsers SET DisplayName=? WHERE UserName='002'", (name,))
            refresh(original)
            if name:
                expect(original.locator('[data-current-user-display-name]')).to_have_text(name.strip())
            else:
                expect(original.locator('[data-current-user-display-name]')).to_be_hidden()
            assert original.evaluate('document.documentElement.scrollWidth <= innerWidth')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            db.execute("UPDATE AspNetUsers SET DisplayName='陳同學' WHERE UserName='002'")

        # 新分頁登入前產生的 Token 不能用於登入後的登出，頁首須更新成新 Token。
        original.get_by_role('button', name='登出', exact=True).click()
        original.wait_for_url(fixture.url + '/portal')
        expect(original.locator('[data-current-user]')).to_be_hidden()
        refresh(login)
        expect(login.locator('[data-current-user]')).to_be_hidden()
        expect(login.locator('[data-current-user-account]')).to_have_text('')
        expect(login.locator('[data-current-user-display-name]')).to_have_text('')
        expect(login.locator('[data-current-user-account-label]')).to_have_text('')
        expect(login.locator('[data-current-user-fields] dd')).to_have_count(0)
        login.goto(fixture.url + '/portal/student')
        expect(login.locator('[data-account-login]')).to_have_text('登入')
        expect(login.locator('[data-account-login]')).to_be_visible()

        # 管理入口按最新角色顯示，跨分頁登入及撤銷角色都不依賴原頁 Cookie claims。
        admin = browser.new_context(ignore_https_errors=True, viewport={'width': 390, 'height': 1000})
        admin_original = admin.new_page()
        admin_original.goto(fixture.url + '/portal/student')
        admin_login = admin.new_page()
        admin_login.goto(fixture.url + '/account/login')
        admin_login.get_by_label('校園帳號', exact=True).fill('001')
        admin_login.get_by_label('密碼', exact=True).fill('123456')
        admin_login.get_by_role('button', name='登入', exact=True).click()
        admin_login.wait_for_url(fixture.url + '/')
        refresh(admin_original)
        # 管理入口位於手機版收合的專區導覽，先由使用者操作展開。
        nav_toggle = admin_original.locator('.site-nav-toggle')
        if nav_toggle.is_visible() and nav_toggle.get_attribute('aria-expanded') == 'false':
            nav_toggle.click()
        manage = admin_original.locator('[data-manage-systems]')
        expect(manage).to_be_visible()
        admin_original.screenshot(path=str(shots / 'admin-mobile-390.png'), full_page=True)
        manage.click()
        admin_original.wait_for_url(fixture.url + '/admin/systems')
        assert admin.request.get(fixture.url + '/admin/systems/new').status == 200
        admin_original.go_back()
        admin_original.wait_for_url(fixture.url + '/portal/student')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            db.execute("DELETE FROM AspNetUserRoles WHERE UserId='demo-session-admin'")
        refresh(admin_original)
        expect(admin_original.locator('[data-manage-systems]')).to_be_hidden()
        assert admin.request.get(fixture.url + '/admin/systems/new').status == 403
        admin.close()

        login.goto(fixture.url + '/account/login')
        login.get_by_label('校園帳號', exact=True).fill('002')
        login.get_by_label('密碼', exact=True).fill('123456')
        login.get_by_role('button', name='登入', exact=True).click()
        login.wait_for_url(fixture.url + '/')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            db.execute("UPDATE AspNetUsers SET Enabled=0 WHERE UserName='002'")
        refresh(login)
        expect(login.locator('[data-current-user]')).to_be_hidden()
        assert context.request.get(fixture.url + '/account/current-user').json() == {'signedIn': False}
        assert not violations and not errors, (violations, errors)
        browser.close()
        print('PASS anonymous privacy, actual account identity, cross-tab login/logout, fresh CSRF token, disabled account, bilingual and responsive header, no-JS fallback and zero CSP/JS errors')
        print('Screenshots:', shots)
finally:
    fixture.close()
