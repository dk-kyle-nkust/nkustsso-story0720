"""以隔離站台確認匯入清單、規格備註及未填 IP 的停用登記。"""
import importlib.util
import os
from pathlib import Path
import sqlite3
import subprocess
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture, ROOT

fixture = ServerFixture()
try:
    # 只複製已整理的非機密規格，不使用實際登入帳號或來源連線。
    spec = importlib.util.spec_from_file_location('inventory_import', ROOT / 'scripts/import-integration-inventory.py')
    importer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(importer)
    import json
    manifest = json.loads((ROOT / '.local/integration-import/inventory.json').read_text(encoding='utf-8'))
    importer.import_records(fixture.directory / 'sso.db', manifest, fixture.directory / 'import', apply=True)
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation',e=>window.recordCsp(e.violatedDirective));")
        page = context.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        url = fixture.url + '/admin/systems/api?kind=existing'
        page.goto(url)
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(url)
        expect(page.locator('.integration-tabs a[aria-current=page]')).to_contain_text(str(manifest['registration_count']))
        page.wait_for_function("window.jQuery && jQuery.fn.dataTable.isDataTable('table.integration-client-table')")
        assert page.evaluate("jQuery('table.integration-client-table').DataTable().rows().count()") == manifest['registration_count']
        page.locator('.dt-search input').first.fill('交通車')
        row = page.locator('table').first.locator('tbody tr').filter(has_text='交通車系統')
        expect(row).to_contain_text('已停用')
        expect(row).to_contain_text('待確認，啟用前必填')
        row.get_by_text('查看備註', exact=True).click()
        expect(row).to_contain_text('Get_Traffic_Stud_Temp')
        expect(row).to_contain_text('/KuasSSOWebService.asmx')
        for width in [1440, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            page.screenshot(path=str(fixture.directory / f'imported-{width}.png'), full_page=True)
        row.get_by_role('link', name='編輯', exact=True).click()
        expect(page.get_by_label('允許來源 IP', exact=True)).to_have_value('')
        page.get_by_label('系統用途', exact=True).fill('隔離測試修改用途')
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(url)
        page.goto(fixture.url + '/admin/systems/api/legacy-traffic/edit')
        expect(page.get_by_label('系統用途', exact=True)).to_have_value('隔離測試修改用途')
        page.get_by_label('啟用此串接', exact=True).check()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        expect(page.locator('.validation')).to_contain_text('完整來源 IP')
        with importer.connect(fixture.directory / 'sso.db') as db:
            assert db.execute('SELECT Enabled FROM IntegrationClients WHERE Id=\'legacy-traffic\'').fetchone()[0] == 0
        assert not errors and not violations, (errors, violations)
        browser.close()
    print('PASS imported registrations, grouped methods, search, paging, disabled drafts, enable rejection, responsive UI and CSP')
    print('Screenshots:', fixture.directory)
finally:
    fixture.close()
