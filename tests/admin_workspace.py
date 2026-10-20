"""Shared admin navigation, real DataTables interactions, responsive layout and CSP."""
import os
import sqlite3
from pathlib import Path
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.recordCsp({directive:e.violatedDirective, url:location.pathname}));")
        page = context.new_page()
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(fixture.url + '/admin/systems')
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems')
        expect(page.get_by_role('navigation', name='串接管理類型').get_by_role('link', name='首頁連結', exact=True)).to_have_attribute('aria-current', 'page')
        expect(page.get_by_role('heading', name='首頁連結', exact=True)).to_be_visible()
        page.get_by_role('navigation', name='串接管理類型').get_by_role('link', name='單一登入 SSO', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems?section=sso')
        expect(page.locator('.system-registry th')).to_have_count(8)
        expect(page.locator('.system-registry th').filter(has_text='開放身分')).to_have_count(1)
        assert page.locator('td[data-label="入口顯示"] .audience-detail').count() == 0
        expect(page.locator('td[data-label="開放身分"]').first).to_contain_text('全部身分')
        tabs = page.get_by_role('navigation', name='串接管理類型', exact=True)
        expect(tabs.get_by_role('link', name='單一登入 SSO', exact=True)).to_have_attribute('aria-current', 'page')
        assert page.locator('.protocol-badge.link').count() == 0
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            sso_count = db.execute("SELECT count(*) FROM RegisteredSystems WHERE Protocol != 'link'").fetchone()[0]
            link_count = db.execute("SELECT count(*) FROM RegisteredSystems WHERE Protocol = 'link'").fetchone()[0]
        expect(page.locator('.systems-overview dd').first).to_have_text(str(sso_count) + '個系統')
        tabs.get_by_role('link', name='首頁連結', exact=True).click()
        expect(tabs.get_by_role('link', name='首頁連結', exact=True)).to_have_attribute('aria-current', 'page')
        expect(page.locator('.systems-overview dd').first).to_have_text(str(link_count) + '個系統')
        assert page.locator('.protocol-badge:not(.link)').count() == 0
        assert page.locator('td[data-label="開放身分"] .audience-detail').count() > 0
        page.get_by_role('button', name='查詢', exact=True).click()
        expect(tabs.get_by_role('link', name='首頁連結', exact=True)).to_have_attribute('aria-current', 'page')
        page.get_by_role('link', name='清除條件', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems?section=links')
        paging = page.locator('.dt-paging').first
        expect(paging).to_be_visible()
        page.locator('.dt-search input').first.fill('no-matching-system-unique')
        expect(paging).not_to_be_visible()
        page.locator('.dt-search input').first.fill('')
        expect(paging).to_be_visible()
        nav = page.get_by_role('navigation', name='管理功能', exact=True)
        expect(nav.get_by_role('link', name='串接系統管理', exact=True)).to_have_attribute('aria-current', 'page')
        assert nav.get_by_role('link', name='串接服務', exact=True).count() == 0
        page.get_by_role('link', name='新增首頁連結', exact=False).click()
        expect(page.locator('[name="System.Protocol"]')).to_have_value('link')
        expect(tabs.get_by_role('link', name='首頁連結', exact=True)).to_have_attribute('aria-current', 'page')
        page.locator('[name="System.ClientId"]').fill('unified-link')
        page.locator('[name="System.DisplayName"]').fill('整合入口測試連結')
        page.locator('[name="System.EntryUrl"]').fill('https://example.edu/service')
        page.get_by_role('button', name='建立系統', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems?section=links')
        page.locator('.dt-search input').first.fill('unified-link')
        page.get_by_role('link', name='編輯 整合入口測試連結', exact=True).click()
        expect(tabs.get_by_role('link', name='首頁連結', exact=True)).to_have_attribute('aria-current', 'page')
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems?section=links')
        assert context.request.get(fixture.url + '/admin/systems?section=invalid').status == 400
        assert context.request.get(fixture.url + '/admin/systems?section=links&protocol=oidc').status == 400
        tabs.get_by_role('link', name='帳密驗證 API', exact=True).click()
        page.wait_for_url(fixture.url + '/admin/systems/api?kind=new')
        expect(tabs.get_by_role('link', name='帳密驗證 API', exact=True)).to_have_attribute('aria-current', 'page')
        expect(nav.get_by_role('link', name='串接系統管理', exact=True)).to_have_attribute('aria-current', 'page')
        # 舊書籤與新增入口保留，但頁面內連結一律導向整合後的管理網址。
        page.goto(fixture.url + '/admin/integration-services?kind=new')
        expect(tabs.get_by_role('link', name='帳密驗證 API', exact=True)).to_have_attribute('aria-current', 'page')
        expect(page.locator('.admin-head a.primary')).to_have_attribute('href', '/admin/systems/api/new?kind=new')
        page.goto(fixture.url + '/admin/integration-services/new?kind=new')
        expect(page.locator('#Client_Protocol')).to_have_value('json')
        expect(page.get_by_role('link', name='返回帳密驗證 API', exact=True)).to_have_attribute('href', '/admin/systems/api?kind=new')
        nav.get_by_role('link', name='開發進度', exact=True).click()
        page.wait_for_url(fixture.url + '/integration')
        expect(page.locator('body')).to_have_class('admin admin-workspace')
        expect(page.get_by_role('heading', name='開發進度', exact=True)).to_be_visible()
        expect(nav.get_by_role('link', name='開發進度', exact=True)).to_have_attribute('aria-current', 'page')
        # Sorting and filtering use the actual DataTables instance.
        search = page.locator('.dt-search input').first
        search.fill('串接資料移轉')
        expect(page.locator('table tbody tr')).to_have_count(1)
        expect(page.locator('table tbody')).to_contain_text('部分完成')
        search.fill('')
        assert search.evaluate('(e) => getComputedStyle(e).borderRadius') == '9px'
        page.locator('table thead th').first.click()
        assert page.evaluate("jQuery('table').first().DataTable().order()[0][0]") == 0
        screenshots = fixture.directory / 'screenshots'
        screenshots.mkdir()
        routes = ['/admin/systems', '/admin/systems?category=academic', '/admin/systems/new',
                  '/admin/systems?section=links', '/admin/systems/new?section=links',
                  '/admin/systems/api?kind=new', '/admin/systems/api?kind=existing', '/admin/systems/api/new',
                  '/admin/categories', '/admin/categories/new',
                  '/admin/identity-catalog', '/admin/sessions', '/admin/tickets', '/integration']
        for width in [1440, 1024, 768, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            for route in routes:
                response = page.goto(fixture.url + route)
                # 隔離站關閉校務連線；分類頁以 503 呈現資料來源不可用的管理畫面。
                assert response.status == 200 or (route == '/admin/identity-catalog' and response.status == 503), (route, response.status)
                expect(page.locator('body')).to_have_class('admin admin-workspace')
                assert page.locator('main').evaluate('(e) => e.scrollWidth <= e.clientWidth + 1'), (width, route)
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), (width, route)
                if width in [1440, 390]:
                    page.screenshot(path=str(screenshots / (route.strip('/').replace('/', '-').replace('?', '-').replace('=', '-') + f'-{width}.png')), full_page=True)
            if width <= 1000:
                menu = page.get_by_role('button', name='選單', exact=True)
                expect(nav).not_to_be_visible()
                menu.click()
                expect(nav).to_be_visible()
                expect(menu).to_have_attribute('aria-expanded', 'true')
                page.keyboard.press('Escape')
                expect(nav).not_to_be_visible()
                expect(menu).to_be_focused()
                menu.click()
                nav.get_by_role('link', name='串接系統管理', exact=True).click()
                page.wait_for_url(fixture.url + '/admin/systems')
                expect(page.get_by_role('heading', name='首頁連結', exact=True)).to_be_visible()
                expect(nav).not_to_be_visible()
        page.set_viewport_size({'width': 1440, 'height': 1000})
        expect(nav).to_be_visible()
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS unified SSO/API/link tabs, scoped lists, link create/edit, legacy URLs, DataTables search/sort, mobile menu, keyboard, responsive routes at five widths and zero CSP/JS errors')
        print('Screenshots:', screenshots)
finally:
    fixture.close()
