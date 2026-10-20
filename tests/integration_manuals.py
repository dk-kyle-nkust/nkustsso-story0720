"""隔離驗證手冊清單、下載授權、撤權、私有資源及桌面／手機版面。"""
import io
import json
import os
from pathlib import Path
from urllib.parse import quote
import zipfile
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture, ROOT

fixture = ServerFixture()
base = fixture.url
catalog = json.loads((ROOT / 'docs/developer-integration/catalog.json').read_text(encoding='utf-8'))
paths = ['/admin/manuals', '/admin/manuals/download/all'] + [
    '/admin/manuals/download/' + item['Id'] for item in catalog['Items']]


def login(page, account):
    page.goto(base + '/account/login')
    page.get_by_label('校園帳號', exact=True).fill(account)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=True).click()
    page.wait_for_url(base + '/')


def denied(context, path, anonymous=False):
    response = context.request.get(base + path, max_redirects=0)
    assert response.status in (302, 401, 403), (path, response.status)
    if response.status == 302:
        assert ('/account/login' if anonymous else '/account/denied') in response.headers['location']
    assert not response.body().startswith((b'%PDF-', b'PK'))


try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        anon = browser.new_context(ignore_https_errors=True)
        admin = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        reader = browser.new_context(ignore_https_errors=True)
        errors, violations = [], []
        for context in (admin, reader):
            context.expose_binding('recordCsp', lambda source, value: violations.append(value))
            context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.recordCsp(e.violatedDirective));")
            context.on('page', lambda page: page.on('pageerror', lambda error: errors.append(str(error))))
        for path in paths:
            denied(anon, path, anonymous=True)
        sample = catalog['Items'][0]['FileName']
        for path in ['/docs/developer-integration/index.html', '/docs/developer-integration/catalog.json',
                     '/docs/developer-integration/PDF/' + quote(sample), '/PDF/' + quote(sample),
                     '/IntegrationManuals.' + quote(sample)]:
            assert anon.request.get(base + path).status == 404, path

        page, user = admin.new_page(), reader.new_page()
        login(page, '001')
        login(user, '003')
        for path in paths:
            denied(reader, path)
        page.goto(base + '/admin/manuals')
        expect(page.get_by_role('heading', name='串接手冊', exact=True)).to_be_visible()
        expect(page.locator('table tbody tr')).to_have_count(16)
        expect(page.locator('.workspace-nav a[href="/admin/manuals"]')).to_have_attribute('aria-current', 'page')
        for item in catalog['Items']:
            response = admin.request.get(base + '/admin/manuals/download/' + item['Id'])
            assert response.status == 200 and response.headers['content-type'] == 'application/pdf'
            assert 'attachment;' in response.headers['content-disposition']
            assert 'no-store' in response.headers['cache-control']
            source = next((ROOT / 'docs/developer-integration/PDF').rglob(item['FileName']))
            assert response.body() == source.read_bytes()
        response = admin.request.get(base + '/admin/manuals/download/all')
        assert response.status == 200 and response.headers['content-type'] == 'application/zip'
        assert 'no-store' in response.headers['cache-control']
        with zipfile.ZipFile(io.BytesIO(response.body())) as archive:
            assert len(archive.namelist()) == 16 and archive.testzip() is None
        assert admin.request.get(base + '/admin/manuals/download/missing').status == 404
        assert admin.request.get(base + '/admin/manuals/download/..%2F..%2Fappsettings.json').status in (400, 404)
        with page.expect_download() as download:
            page.get_by_role('link', name='下載全部手冊（ZIP）', exact=True).click()
        assert download.value.suggested_filename == '開發人員串接PDF全集.zip'

        shots = fixture.directory / 'screenshots'
        shots.mkdir()
        for width in (1440, 390, 320):
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            page.screenshot(path=str(shots / f'manuals-{width}.png'), full_page=True)
        page.get_by_role('searchbox').fill('Get_Traffic_Stud_Temp')
        expect(page.locator('table tbody tr')).to_have_count(1)
        expect(page.locator('table tbody')).to_contain_text('Get_Traffic_Stud_Temp')

        # 透過後台授予僅查詢權，也應允許下載；不授予其他管理功能。
        page.set_viewport_size({'width': 1440, 'height': 1000})
        page.goto(base + '/admin/access/new')
        page.get_by_label('角色代碼', exact=True).fill('manual-reader')
        page.get_by_label('角色名稱', exact=True).fill('手冊閱讀人員')
        page.locator('input[name="Permissions"][value="manuals:read"]').check()
        page.locator('#Members-ts-control').fill('003')
        page.locator('.ts-dropdown [data-value="demo-student-leave"]').click()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        user.goto(base + '/admin')
        user.wait_for_url(base + '/admin/manuals')
        assert reader.request.get(base + '/admin/manuals/download/oidc').status == 200
        assert reader.request.get(base + '/admin/manuals/download/all').status == 200
        denied(reader, '/admin/systems')
        denied(reader, '/admin/access')
        # 撤權立即作用於既有登入與直接下載網址。
        page.goto(base + '/admin/access/manual-reader/edit')
        page.locator('input[name="Permissions"][value="manuals:read"]').uncheck()
        page.get_by_role('button', name='儲存角色', exact=True).click()
        page.wait_for_url(base + '/admin/access')
        for path in paths:
            denied(reader, path)
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS private manuals, all PDF/ZIP bytes, anonymous and unauthorized denial, read-only grant, immediate revocation, path rejection, RWD and CSP')
        print('Screenshots:', shots)
finally:
    fixture.close()
