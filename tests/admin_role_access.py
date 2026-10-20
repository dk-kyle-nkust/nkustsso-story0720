"""角色僅查詢權限、授權期間、立即撤權及桌面手機檢查，僅使用隔離帳號。"""
import os
from datetime import datetime, timezone, timedelta
from pathlib import Path
import sqlite3
from urllib.parse import urlencode
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
base = fixture.url

def login(page, account):
    page.goto(base + '/account/login?' + urlencode({'returnUrl': '/portal/student'}))
    page.get_by_label('校園帳號', exact=True).fill(account)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=True).click()
    page.wait_for_url(lambda u: '/account/login' not in u)

def denied(response):
    assert response.status in [302, 403], response.status
    if response.status == 302:
        assert '/account/denied' in response.headers['location']

try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        root = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        reader = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        for context in [root, reader]:
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp(e.violatedDirective));")
        page = root.new_page(); user = reader.new_page()
        for tab in [page, user]:
            tab.on('pageerror', lambda e: errors.append(str(e)))
        login(page, '001'); login(user, '003')
        page.goto(base + '/admin/access/new')
        page.get_by_label('角色代碼', exact=True).fill('read-only')
        page.get_by_label('角色名稱', exact=True).fill('限期查詢角色')
        for permission in ['systems', 'integrations', 'categories', 'sessions']:
            page.locator(f'input[name="Permissions"][value="{permission}:read"]').check()
        page.get_by_label('授權開始時間（臺灣時間）', exact=True).fill('2000-01-01T00:00')
        page.get_by_label('授權結束時間（臺灣時間）', exact=True).fill('2099-01-01T00:00')
        page.locator('#Members-ts-control').fill('003')
        page.locator('.ts-dropdown [data-value="demo-student-leave"]').click()
        for width in [1440, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            page.screenshot(path=str(fixture.directory / f'role-period-{width}.png'), full_page=True)
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        expect(page.locator('main')).to_contain_text('僅查詢')
        for width in [1440, 390]:
            user.set_viewport_size({'width': width, 'height': 1000})
            for path in ['/admin/systems', '/admin/systems?section=links', '/admin/systems/api?kind=new', '/admin/categories', '/admin/sessions']:
                assert user.goto(base + path).status == 200
                assert user.locator('main a[href*="/new"], main a[href*="/edit"], main a[href*="/revoke"]').count() == 0
                assert user.locator('main form[method="post"]').count() == 0
                assert user.evaluate('document.documentElement.scrollWidth <= innerWidth')
                user.screenshot(path=str(fixture.directory / f'read-{width}-{path.split("?")[0].split("/")[-1]}.png'), full_page=True)
        for path in ['/admin/systems/new', '/admin/systems/academic/edit', '/admin/categories/new', '/admin/categories/academic/edit', '/admin/sessions/revoke?accountId=demo-student-leave', '/admin/integration-services/new', '/admin/access']:
            denied(reader.request.get(base + path, max_redirects=0))
        for path in ['/admin/systems/new', '/admin/categories/academic/delete', '/admin/integration-services/example/rotate', '/admin/sessions/revoke']:
            denied(reader.request.post(base + path, form={}, max_redirects=0))
        page.goto(base + '/admin/access/read-only/edit')
        expect(page.get_by_label('授權開始時間（臺灣時間）', exact=True)).to_have_value('2000-01-01T00:00')
        page.get_by_label('授權結束時間（臺灣時間）', exact=True).fill('1999-01-01T00:00')
        page.get_by_role('button', name='儲存角色', exact=True).click()
        expect(page.get_by_role('alert')).to_contain_text('結束時間須晚於開始時間')
        assert reader.request.get(base + '/admin/categories', max_redirects=0).status == 200
        page.get_by_label('授權結束時間（臺灣時間）', exact=True).fill('2001-01-01T00:00')
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        expect(page.locator('main')).to_contain_text('已到期')
        denied(reader.request.get(base + '/admin/categories', max_redirects=0))
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            row = db.execute("SELECT StartsAt, ExpiresAt FROM AdminRoles WHERE Id='read-only'").fetchone()
            expected = tuple(int(datetime(year, 1, 1, tzinfo=timezone(timedelta(hours=8))).timestamp() * 1000) for year in [2000, 2001])
            assert row == expected, (row, expected)
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS read-only role UI, direct URL/POST denial, Taiwan time conversion, expiry, desktop/mobile and CSP')
        print('Screenshots:', fixture.directory)
finally:
    fixture.close()
