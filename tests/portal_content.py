"""從後台新增、修改及下架前台分類與連結，使用隔離資料驗證公開頁面同步更新。"""
import os
from pathlib import Path
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
base = fixture.url
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        visitor = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        for context in [admin, visitor]:
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp(e.violatedDirective));")
        page = admin.new_page(); front = visitor.new_page()
        for tab in [page, front]:
            tab.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(base + '/admin/systems')
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(base + '/admin/systems')
        expect(page.get_by_role('heading', name='首頁連結', exact=True)).to_be_visible()
        expect(page.locator('.protocol-badge:not(.link)')).to_have_count(0)
        for width in [1440, 390]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            page.screenshot(path=str(fixture.directory / f'content-admin-{width}.png'), full_page=True)
        page.goto(base + '/admin/categories/new')
        page.get_by_label('分類代碼', exact=True).fill('portal-maintenance')
        page.get_by_label('分類名稱', exact=True).fill('前台維護驗證分類')
        page.get_by_label('分類排序', exact=True).fill('0')
        page.get_by_role('button', name='儲存分類', exact=True).click()
        page.wait_for_url(base + '/admin/categories')
        page.goto(base + '/admin/systems')
        page.get_by_role('link', name='新增首頁連結', exact=False).click()
        expect(page.get_by_label('啟用 SSO 登入', exact=True)).not_to_be_visible()
        page.get_by_label('系統名稱', exact=True).fill('後台新增的前台項目')
        page.get_by_label('系統識別碼（Client ID）', exact=True).fill('portal-managed-link')
        page.get_by_label('首頁入口網址', exact=True).fill('https://example.invalid/original')
        page.locator('#System_Category').evaluate("e=>e.tomselect.setValue('portal-maintenance')")
        page.get_by_label('首頁排序（分類內）', exact=True).fill('0')
        page.get_by_label('學生專區', exact=True).check()
        page.get_by_label('在首頁顯示入口', exact=True).check()
        page.get_by_role('button', name='建立系統', exact=True).click()
        page.wait_for_url(base + '/admin/systems?section=links')
        front.goto(base + '/portal/student')
        group = front.locator('[data-directory-category="portal-maintenance"]')
        expect(group).to_contain_text('後台新增的前台項目')
        expect(group.locator('h2')).to_have_text('前台維護驗證分類')
        expect(front.locator('.directory-group').first).to_have_attribute('data-directory-category', 'portal-maintenance')
        response = visitor.request.get(base + '/portal/student/systems/portal-managed-link', max_redirects=0)
        assert response.status == 302 and response.headers['location'] == 'https://example.invalid/original'
        page.goto(base + '/admin/systems/portal-managed-link/edit')
        page.get_by_label('系統名稱', exact=True).fill('後台已修改的前台項目')
        page.get_by_label('首頁入口網址', exact=True).fill('https://example.invalid/updated')
        page.get_by_label('功能詳述', exact=True).fill('此說明由後台維護。')
        page.get_by_label('首頁排序（分類內）', exact=True).fill('25')
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(base + '/admin/systems?section=links')
        page.goto(base + '/admin/categories/portal-maintenance/edit')
        page.get_by_label('分類名稱', exact=True).fill('後台修改的分類')
        page.get_by_role('button', name='儲存分類', exact=True).click()
        page.wait_for_url(base + '/admin/categories')
        for width in [1440, 390]:
            front.set_viewport_size({'width': width, 'height': 1000})
            front.reload()
            expect(group.locator('h2')).to_have_text('後台修改的分類')
            expect(group).to_contain_text('後台已修改的前台項目')
            expect(group).not_to_contain_text('後台新增的前台項目')
            assert front.evaluate('document.documentElement.scrollWidth <= innerWidth')
            front.screenshot(path=str(fixture.directory / f'content-front-{width}.png'), full_page=True)
        response = visitor.request.get(base + '/portal/student/systems/portal-managed-link', max_redirects=0)
        assert response.status == 302 and response.headers['location'] == 'https://example.invalid/updated'
        page.goto(base + '/admin/systems/portal-managed-link/edit')
        page.get_by_label('在首頁顯示入口', exact=True).uncheck()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(base + '/admin/systems?section=links')
        front.reload()
        expect(front.locator('[data-system-id="portal-managed-link"]')).to_have_count(0)
        assert visitor.request.get(base + '/portal/student/systems/portal-managed-link', max_redirects=0).status == 404
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS backend category/link creation, rename, URL changes, publication and withdrawal update frontend; desktop/mobile/CSP')
        print('Screenshots:', fixture.directory)
finally:
    fixture.close()
