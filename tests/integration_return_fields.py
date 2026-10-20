"""以隔離後台驗證多方法欄位設定、清除、偽造拒絕與 RWD；不連線校務資料庫。"""
import html
import os
import sqlite3
from pathlib import Path
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
base = fixture.url + '/admin/systems/api'
status_key = 'KuasSSO_Certify2/sts_id'
name_key = 'Get_Traffic_Stud_Temp/std_name'
mail_key = 'Get_Traffic_Stud_Temp/std_email'
document_key = 'NkustSSO_DocumentApplyPaymentSystemSearch/std_email'

try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.recordCsp(e.violatedDirective));")
        page = context.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.goto(base + '/new?kind=existing')
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(base + '/new?kind=existing')
        expect(page.locator('#integration-return-fields')).to_be_visible()
        page.get_by_label('系統代碼', exact=True).fill('field-test')
        page.get_by_label('系統名稱', exact=True).fill('欄位設定測試')
        page.get_by_label('帳密驗證', exact=True).check()
        field = lambda key: page.locator('input[name="Client.ReturnFieldKeys"][value="' + key + '"]')
        expect(field(status_key)).not_to_be_checked()
        field(status_key).check()
        traffic = page.locator('details.integration-return-contract').filter(has_text='Get_Traffic_Stud_Temp')
        traffic.locator('summary').focus()
        page.keyboard.press('Enter')
        expect(traffic).to_have_attribute('open', '')
        expect(traffic).to_contain_text('已實作')
        page.get_by_label('原系統呼叫帳號', exact=True).fill('original-caller')
        page.get_by_label('原系統呼叫密碼', exact=True).fill('synthetic-caller-password-only')
        field(name_key).check()
        field(mail_key).check()
        document = page.locator('details.integration-return-contract').filter(has_text='NkustSSO_DocumentApplyPaymentSystemSearch')
        document.locator('summary').click()
        expect(document).to_contain_text('已實作')
        field(document_key).check()
        page.locator('input[name="Client.ServiceIds"][value="soap.NkustSSO_DocumentApplyPaymentSystemSearch"]').check()
        assert page.locator('input[value="Get_Traffic_Stud_Temp/pwd"]').count() == 0
        assert page.locator('input[value="Get_Traffic_Stud_Temp/message"]').count() == 0
        shots = fixture.directory / 'screenshots'
        shots.mkdir()
        for width in [1440, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), width
            page.screenshot(path=str(shots / f'return-fields-{width}.png'), full_page=True)
        # 切換介面只提交該介面的欄位，切回 SOAP 保留尚未儲存的勾選。
        page.locator('#Client_Protocol').evaluate("el => el.tomselect.setValue('json')")
        expect(page.locator('#integration-return-fields')).to_be_hidden()
        assert page.locator('form.system-form').evaluate("f => new FormData(f).getAll('Client.ReturnFieldKeys').length") == 0
        page.locator('#Client_Protocol').evaluate("el => el.tomselect.setValue('soap')")
        expect(field(name_key)).to_be_checked()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(base + '?kind=existing')
        page.goto(base + '/field-test/edit')
        expect(page.get_by_label('原系統呼叫帳號', exact=True)).to_have_value('original-caller')
        expect(page.get_by_label('原系統呼叫密碼', exact=True)).to_have_value('')
        for key in [status_key, name_key, mail_key, document_key]:
            expect(field(key)).to_be_checked()
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert {r[0] for r in db.execute('SELECT FieldKey FROM IntegrationClientReturnFields WHERE ClientId=?', ('field-test',))} == {status_key, name_key, mail_key, document_key}
        values = page.locator('form.system-form').evaluate('form => { const r = {}; for (const [k,v] of new FormData(form)) if (!(k in r)) r[k]=v; return r; }')
        for forbidden in ['Get_Traffic_Stud_Temp/pwd', 'Get_Traffic_Stud_Temp/message', 'made-up/column']:
            response = context.request.post(page.url, form={**values, 'Client.ReturnFieldKeys': forbidden})
            assert '尚未開放的回傳欄位' in html.unescape(response.text())
            assert 'synthetic-caller-password-only' not in response.text()
        for key in [status_key, name_key, mail_key, document_key]:
            field(key).uncheck()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(base + '?kind=existing')
        stale = context.request.post(base + '/field-test/edit', form=values)
        assert '其他管理者修改' in html.unescape(stale.text())
        page.goto(base + '/field-test/edit')
        expect(field(status_key)).not_to_be_checked()
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute('SELECT COUNT(*) FROM IntegrationClientReturnFields WHERE ClientId=?', ('field-test',)).fetchone()[0] == 0
            assert db.execute('SELECT COUNT(*) FROM IntegrationClientServices WHERE ClientId=?', ('field-test',)).fetchone()[0] == 2
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS multi-method field CRUD, deny forged fields, clear, stale writes, protocol switch, keyboard, mobile and CSP/JS')
        print('Screenshots:', shots)
finally:
    fixture.close()
