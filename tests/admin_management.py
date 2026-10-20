"""隔離後台選單／角色管理、直接存取授權、即時撤權及桌面手機 CSP 檢查。"""
import os
import html
import sqlite3
from pathlib import Path
from urllib.parse import urlencode
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
base = fixture.url
shots = fixture.directory / 'screenshots'
shots.mkdir()


def login(page, name, target='/admin'):
    page.goto(base + '/account/login?' + urlencode({'returnUrl': target}))
    page.get_by_label('校園帳號', exact=True).fill(name)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=True).click()
    page.wait_for_url(lambda u: '/account/login' not in u)


def denied(response):
    assert response.status in [302, 403], response.status
    if response.status == 302:
        assert '/account/denied' in response.headers['location']


def fields(page):
    return page.locator('form.system-form').evaluate('f => Array.from(new FormData(f).entries())')


def post(context, path, data):
    return context.request.post(base + path, data=urlencode([tuple(pair) for pair in data]), headers={'Content-Type': 'application/x-www-form-urlencoded'}, max_redirects=0)


try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        root = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        member = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        for ctx in [root, member]:
            ctx.expose_binding('recordCsp', lambda source, value: violations.append(value))
            ctx.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp(e.violatedDirective));")
        page = root.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        user = member.new_page()
        user.on('pageerror', lambda e: errors.append(str(e)))
        anonymous = root.request.get(base + '/admin/access', max_redirects=0)
        assert anonymous.status == 302 and '/account/login' in anonymous.headers['location']
        login(page, '001', '/admin/menus')
        login(user, '002', '/portal/student')
        denied(member.request.get(base + '/admin/categories', max_redirects=0))
        assert root.request.post(base + '/admin/access/new', form={'Role.Id': 'bad'}).status == 400
        page.goto(base + '/admin/access/new')
        page.get_by_label('角色代碼', exact=True).fill('catalog-office')
        page.get_by_label('角色名稱', exact=True).fill('首頁分類承辦')
        page.get_by_label('首頁分類維護', exact=True).check()
        page.locator('#Members-ts-control').fill('002')
        page.locator('.ts-dropdown [data-value="demo-student-active"]').click()
        for width in [1440, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            page.locator('.ts-control .item[data-value="demo-student-active"] .remove').click()
            page.locator('#Members-ts-control').fill('002')
            page.locator('.ts-dropdown [data-value="demo-student-active"]').click()
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), ('role', width)
            page.screenshot(path=str(shots / f'role-{width}.png'), full_page=True)
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        page.set_viewport_size({'width': 1440, 'height': 1000})
        user.goto(base + '/admin')
        user.wait_for_url(base + '/admin/categories')
        expect(user.get_by_role('navigation', name='管理功能')).to_contain_text('首頁分類')
        assert user.get_by_role('navigation', name='管理功能').locator('a').count() == 1
        for path in ['/admin/systems', '/admin/systems/api?kind=new', '/admin/integration-services', '/admin/identity-catalog', '/admin/sessions', '/admin/tickets', '/admin/menus', '/admin/access', '/integration']:
            denied(member.request.get(base + path, max_redirects=0))
        denied(member.request.post(base + '/admin/access/new', form={'Role.Id': 'escalation'}, max_redirects=0))
        # 用實際點選驗證成員移除、儲存後保留及再次加入，不繞過搜尋輸入框。
        page.goto(base + '/admin/access/catalog-office/edit')
        expect(page.locator('.ts-control .item[data-value="demo-student-active"]')).to_be_visible()
        page.locator('.ts-control .item[data-value="demo-student-active"] .remove').click()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        denied(member.request.get(base + '/admin/categories', max_redirects=0))
        page.goto(base + '/admin/access/catalog-office/edit')
        expect(page.locator('.ts-control .item')).to_have_count(0)
        page.locator('#Members-ts-control').fill('002')
        page.locator('.ts-dropdown [data-value="demo-student-active"]').click()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        assert member.request.get(base + '/admin/categories', max_redirects=0).status == 200
        # 真實維護操作也受功能授權控制。
        user.goto(base + '/admin/categories/new')
        user.get_by_label('分類代碼', exact=True).fill('delegated')
        user.get_by_label('分類名稱', exact=True).fill('承辦人建立的分類')
        user.get_by_role('button', name='儲存分類', exact=True).click()
        user.wait_for_url(base + '/admin/categories')
        user.goto(base + '/portal/student')
        expect(user.locator('[data-manage-systems]')).to_be_visible()
        expect(user.locator('[data-manage-systems]')).to_have_attribute('href', '/admin')
        # 偽造最高權限或未知成員不會寫入，舊表單不能覆蓋新設定。
        page.goto(base + '/admin/access/catalog-office/edit')
        snapshot = fields(page)
        forged = post(root, '/admin/access/catalog-office/edit', [(k, v) for k, v in snapshot if k != 'Permissions'] + [('Permissions', 'access')])
        assert forged.status == 200 and '角色不可授予最高管理權限' in html.unescape(forged.text()), forged.status
        page.get_by_label('角色名稱', exact=True).fill('分類維護承辦')
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        stale = post(root, '/admin/access/catalog-office/edit', snapshot)
        assert stale.status == 200 and '其他管理者修改' in html.unescape(stale.text())
        # 選單設定立即顯示，停用僅隱藏導覽。
        page.goto(base + '/admin/menus/new')
        page.get_by_label('選單代碼', exact=True).fill('catalog-shortcut')
        page.get_by_label('選單名稱', exact=True).fill('分類快捷入口')
        page.get_by_label('選單分組', exact=True).fill('常用管理')
        page.get_by_label('排序', exact=True).fill('1')
        page.locator('#Menu_Target').evaluate("e => e.tomselect.setValue('categories')")
        page.get_by_role('button', name='儲存選單', exact=True).click()
        page.wait_for_url(base + '/admin/menus')
        for width in [1440, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), ('menus', width)
            page.screenshot(path=str(shots / f'menus-{width}.png'), full_page=True)
        user.goto(base + '/admin/categories')
        expect(user.get_by_role('navigation', name='管理功能').locator('a').first).to_have_text('分類快捷入口')
        page.goto(base + '/admin/menus/catalog-shortcut/edit')
        page.get_by_label('啟用選單', exact=True).uncheck()
        page.get_by_role('button', name='儲存選單', exact=True).click()
        page.wait_for_url(base + '/admin/menus')
        user.reload()
        assert user.get_by_role('navigation', name='管理功能').locator('a').count() == 1
        page.goto(base + '/admin/menus/access/edit')
        protected = fields(page)
        result = post(root, '/admin/menus/access/edit', [(k, v) for k, v in protected if k != 'Menu.Enabled'] + [('Menu.Enabled', 'false')])
        assert result.status == 200 and '不可停用' in html.unescape(result.text())
        token = dict(protected)['__RequestVerificationToken']
        result = post(root, '/admin/menus/access/delete', [('__RequestVerificationToken', token), ('revision', dict(protected)['Menu.Revision'])])
        assert result.status == 302
        # 同一登入 Cookie 在角色停用後，GET 與 POST 都立即被拒。
        user.goto(base + '/admin/categories/new')
        old_category_form = fields(user)
        page.goto(base + '/admin/access/catalog-office/edit')
        page.get_by_label('啟用角色', exact=True).uncheck()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        denied(member.request.get(base + '/admin/categories', max_redirects=0))
        denied(post(member, '/admin/categories/new', old_category_form))
        summary = member.request.get(base + '/account/current-user').json()
        assert summary['signedIn'] and not summary['canManageSystems']
        assert root.request.get(base + '/admin/access').status == 200
        # 只有 API 權限時仍使用串接管理入口，且不顯示沒有權限的 SSO／首頁連結分頁。
        page.goto(base + '/admin/access/new')
        page.get_by_label('角色代碼', exact=True).fill('api-office')
        page.get_by_label('角色名稱', exact=True).fill('API 承辦')
        page.get_by_label('JSON／SOAP 串接維護', exact=True).check()
        page.locator('#Members-ts-control').fill('002')
        page.locator('.ts-dropdown [data-value="demo-student-active"]').click()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        user.goto(base + '/admin')
        user.wait_for_url(base + '/admin/systems/api?kind=new')
        assert user.get_by_role('navigation', name='串接管理類型').locator('a').count() == 1
        expect(user.get_by_role('navigation', name='管理功能')).to_contain_text('串接系統管理')
        denied(member.request.get(base + '/admin/systems', max_redirects=0))
        page.goto(base + '/admin/access/new')
        page.get_by_label('角色代碼', exact=True).fill('empty-role')
        page.get_by_label('角色名稱', exact=True).fill('待刪除空角色')
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        page.goto(base + '/admin/access/empty-role/edit')
        page.get_by_text('刪除這個角色', exact=True).click()
        page.get_by_role('button', name='確認刪除角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        page.goto(base + '/admin/menus/catalog-shortcut/edit')
        page.get_by_text('刪除這個選單', exact=True).click()
        page.get_by_role('button', name='確認刪除選單', exact=True).click()
        page.wait_for_url(base + '/admin/menus')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute('SELECT COUNT(*) FROM AdminManagementAudits').fetchone()[0] == 11
            assert db.execute("SELECT COUNT(*) FROM AdminRoles WHERE Id='empty-role'").fetchone()[0] == 0
            assert db.execute("SELECT COUNT(*) FROM AdminMenus WHERE Id='catalog-shortcut'").fetchone()[0] == 0
            assert db.execute("SELECT Enabled FROM AdminMenus WHERE Id='access'").fetchone()[0] == 1
            assert db.execute('SELECT COUNT(*) FROM AspNetUserRoles').fetchone()[0] == 1
            assert db.execute("SELECT COUNT(*) FROM PortalCategories WHERE Key='delegated'").fetchone()[0] == 1
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS admin menu CRUD, role grants, real maintenance, direct URL/POST denial, immediate revocation, root protection, CSRF, stale writes and RWD/CSP')
        print('Screenshots:', shots)
finally:
    fixture.close()
