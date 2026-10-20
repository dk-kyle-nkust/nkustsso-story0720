"""主管圖解頁：隔離來源、管理員存取、互動、響應式版面及列印產物。"""
import os
from pathlib import Path
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture, ROOT

fixture = ServerFixture()
artifacts = ROOT / '.local/integration-overview'
artifacts.mkdir(parents=True, exist_ok=True)
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations, requests = [], [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp(e.violatedDirective));")
        page = context.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.on('request', lambda r: requests.append((r.method, r.url)))
        url = fixture.url + '/admin/systems/api/overview'
        page.goto(url)
        # 說明頁沿用管理權限，匿名不能直接看到內部服務清單。
        expect(page.get_by_label('校園帳號', exact=True)).to_be_visible()
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(url)
        requests.clear()
        expect(page.locator('h1')).to_contain_text('一個驗證窗口')
        page.get_by_role('button', name='畢業離校', exact=True).click()
        expect(page.locator('#scenario-fields')).to_have_text('狀態帳號姓名')
        expect(page.locator('#scenario-note')).to_contain_text('未回傳信箱')
        page.get_by_role('button', name='諮商輔導', exact=True).click()
        expect(page.locator('#scenario-fields')).to_contain_text('信箱')
        expect(page.locator('#scenario-note')).to_contain_text('不是諮商紀錄')
        page.get_by_role('button', name='只確認帳密', exact=True).click()
        expect(page.locator('#scenario-fields')).to_have_text('驗證結果身分代碼')
        assert not requests, '情境切換不應發出任何網路請求'
        page.locator('.method-directory summary').click()
        expect(page.locator('.method-list article')).to_have_count(61)
        expect(page.get_by_role('link', name='帳密驗證', exact=True)).to_have_attribute('href', '/KuasSSOWebService.asmx?op=KuasSSO_Certify2')
        page.locator('.method-directory summary').click()
        for width in [1440, 768, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), width
            assert page.locator('.brand img').evaluate('(img) => img.complete && img.naturalWidth > 0')
            page.screenshot(path=str(artifacts / f'overview-{width}.png'), full_page=True)
        page.set_viewport_size({'width': 1440, 'height': 1000})
        page.locator('.flow-figure').screenshot(path=str(artifacts / 'service-flow.png'))
        page.evaluate('window.print = () => { window.printInvoked = true; }')
        page.get_by_role('button', name='列印／另存 PDF').click()
        assert page.evaluate('window.printInvoked === true')
        page.pdf(path=str(artifacts / 'legacy-services-brief.pdf'), print_background=True, prefer_css_page_size=True)
        page.goto(fixture.url + '/admin/systems/api?kind=existing')
        expect(page.get_by_role('link', name='舊服務圖解總覽 · 主管簡報版')).to_have_attribute('href', '/admin/systems/api/overview')
        assert not errors and not violations, (errors, violations)
        browser.close()
    print('PASS admin access, scenarios without API calls, 61 methods, responsive layouts, print, images and CSP')
    print('Artifacts:', artifacts)
finally:
    fixture.close()
