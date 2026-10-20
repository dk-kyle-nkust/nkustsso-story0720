"""Actual JSON/SOAP management lifecycle; isolated DB and disabled account sources."""
import hashlib
import html
import os
from pathlib import Path
import sqlite3
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

fixture = ServerFixture()
base = fixture.url + '/admin/systems/api'
notes = '用途：測試串接\n維護說明：<script>throw new Error(1)</script>\n文件：https://example.edu/' + 'a' * 250


def choose_protocol(page, value):
    control = page.locator('#Client_Protocol').locator('..').locator('.ts-control')
    control.click()
    page.locator('.ts-dropdown [data-value="' + value + '"]:visible').click()
    expect(page.locator('#Client_Protocol')).to_have_value(value)


def form_values(page):
    # ASP.NET checkbox 同時送 true 與隱藏的 false，模型繫結使用第一個值。
    return page.locator('section.panel form').evaluate('form => { const values = {}; for (const [key, value] of new FormData(form)) if (!(key in values)) values[key] = value; return values; }')


try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1000})
        errors, violations = [], []
        context.expose_binding('recordCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e=>window.recordCsp({directive:e.violatedDirective,source:e.sourceFile,line:e.lineNumber,sample:e.sample,url:location.pathname}));")
        page = context.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        assert context.request.get(base, max_redirects=0).status == 302
        page.goto(base + '/new')
        page.get_by_label('校園帳號', exact=True).fill('001')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        page.wait_for_url(base + '/new')
        expect(page.locator('.workspace-brand img')).to_have_attribute('alt', '國立高雄科技大學')
        assert page.locator('button.primary').evaluate('el => getComputedStyle(el).backgroundColor') == 'rgb(81, 70, 207)'
        portal = context.new_page()
        portal.goto(fixture.url + '/portal')
        # 後台改用獨立工作空間，前台不載入管理主題。
        assert portal.locator('body.admin-workspace').count() == 0
        assert portal.locator('link[href*="admin-modern.css"]').count() == 0
        portal.close()
        shots = fixture.directory / 'screenshots'
        shots.mkdir()
        for protocol in ['json', 'soap']:
            client_id = 'managed-' + protocol
            kind = 'existing' if protocol == 'soap' else 'new'
            list_url = base + '?kind=' + kind
            page.goto(base + '/new?kind=' + kind)
            expect(page.locator('#Client_ConnectionKind')).to_have_value(kind)
            expect(page.locator('#Client_Protocol')).to_have_value(protocol)
            choose_protocol(page, protocol)
            page.get_by_label('系統代碼', exact=True).fill(client_id)
            page.get_by_label('系統名稱', exact=True).fill('測試 ' + protocol)
            expect(page.get_by_label('帳密驗證', exact=True)).not_to_be_checked()
            page.get_by_label('帳密驗證', exact=True).check()
            page.get_by_label('系統用途', exact=True).fill('校園服務驗證 <script>')
            page.get_by_label('負責單位', exact=True).fill('測試負責單位')
            page.get_by_label('來源系統', exact=True).fill('migration-fixture')
            page.get_by_label('原系統識別碼', exact=True).fill('Original-' + protocol)
            page.get_by_label('申請人', exact=True).fill('申請窗口')
            page.get_by_label('承辦人', exact=True).fill('承辦窗口')
            page.get_by_label('聯絡電話', exact=True).fill('07-1234567 #123')
            page.get_by_label('電子郵件', exact=True).fill('contact@example.edu')
            page.get_by_label('詳細備註', exact=True).fill(notes)
            page.get_by_label('允許來源 IP', exact=True).fill('127.0.0.1')
            page.get_by_label('啟用此串接', exact=True).check()
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                assert page.get_by_label('帳密驗證', exact=True).bounding_box()['height'] <= 24
                page.screenshot(path=str(shots / f'{protocol}-edit-{width}.png'), full_page=True)
            page.get_by_role('button', name='儲存設定', exact=True).click()
            if protocol == 'json':
                expect(page.locator('#integration-secret')).to_be_visible()
                secret = page.locator('#integration-secret').inner_text()
                assert len(secret) >= 40
                page.goto(list_url)
            else:
                page.wait_for_url(list_url)
            expect(page.locator('.integration-tabs a[aria-current=page]')).to_contain_text('舊系統' if kind == 'existing' else '新串接')
            row = page.locator('table').first.locator('tbody tr').filter(has_text=client_id)
            expect(row).to_contain_text('等待服務開啟')
            expect(row).to_contain_text('申請窗口')
            expect(row).to_contain_text('承辦窗口')
            expect(row).to_contain_text('07-1234567 #123')
            expect(row).to_contain_text('contact@example.edu')
            row.get_by_text('查看備註', exact=True).click()
            expect(row.locator('details .integration-notes')).to_have_text(notes)
            assert row.locator('.integration-notes script').count() == 0
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            page.goto(base + '/' + client_id + '/edit')
            expect(page.get_by_label('啟用此串接', exact=True)).to_be_checked()
            expect(page.get_by_label('詳細備註', exact=True)).to_have_value(notes)
            expect(page.get_by_label('承辦人', exact=True)).to_have_value('承辦窗口')
            expect(page.get_by_label('系統用途', exact=True)).to_have_value('校園服務驗證 <script>')
            expect(page.get_by_label('負責單位', exact=True)).to_have_value('測試負責單位')
            expect(page.get_by_label('原系統識別碼', exact=True)).to_have_value('Original-' + protocol)
            expect(page.get_by_label('帳密驗證', exact=True)).to_be_checked()
            fields = form_values(page)
            invalid_kind = context.request.post(base + '/' + client_id + '/edit', form={**fields, 'Client.ConnectionKind': 'invalid'})
            assert '請選擇有效的串接分類' in html.unescape(invalid_kind.text())
            forged = context.request.post(base + '/' + client_id + '/edit', form={**fields, 'Client.ServiceIds': 'unknown.service'})
            assert '尚未提供或不存在的服務' in html.unescape(forged.text())
            partial = context.request.post(base + '/' + client_id + '/edit', form={**fields, 'Client.SourceId': ''})
            assert '須一起填寫' in html.unescape(partial.text())
            if protocol == 'soap':
                duplicate = context.request.post(base + '/' + client_id + '/edit', form={**fields, 'Client.SourceId': 'Original-json'})
                assert '已有對應登記' in html.unescape(duplicate.text())
            assert context.request.post(base + '/' + client_id + '/edit', form={}).status == 400
            invalid = context.request.post(base + '/' + client_id + '/edit',
                form={**fields, 'Client.AllowedIps': '*'})
            assert '完整來源 IP' in html.unescape(invalid.text())
            overlong = context.request.post(base + '/' + client_id + '/edit', form={**fields, 'Client.Notes': '備' * 20001})
            assert '20,000' in html.unescape(overlong.text())
            page.get_by_label('詳細備註', exact=True).fill(notes + '\n更新後的交接事項')
            page.get_by_label('承辦人', exact=True).fill('新承辦窗口')
            page.get_by_label('系統名稱', exact=True).fill('更新 ' + protocol + ' <script>')
            page.get_by_label('啟用此串接', exact=True).uncheck()
            page.get_by_role('button', name='儲存設定', exact=True).click()
            page.wait_for_url(list_url)
            expect(page.locator('table').first.locator('tbody tr').filter(has_text=client_id)).to_contain_text('已停用')
            stale = context.request.post(base + '/' + client_id + '/edit', form=fields)
            assert '其他管理者修改' in html.unescape(stale.text())
            page.goto(base + '/' + client_id + '/edit')
            expect(page.get_by_label('系統名稱', exact=True)).to_have_value('更新 ' + protocol + ' <script>')
            expect(page.get_by_label('詳細備註', exact=True)).to_have_value(notes + '\n更新後的交接事項')
            expect(page.get_by_label('承辦人', exact=True)).to_have_value('新承辦窗口')
            page.get_by_label('啟用此串接', exact=True).check()
            page.get_by_role('button', name='儲存設定', exact=True).click()
            page.wait_for_url(list_url)
            with sqlite3.connect(fixture.directory / 'sso.db') as db:
                value = db.execute('SELECT Enabled,AllowedIps,SecretHash FROM IntegrationClients WHERE Id=?', (client_id,)).fetchone()
                assert value[:2] == (1, '127.0.0.1')
                if protocol == 'json':
                    assert value[2] == hashlib.sha256(secret.encode()).hexdigest().upper()
                else:
                    assert value[2] == ''
                assert db.execute('SELECT COUNT(*) FROM RegisteredSystems WHERE ClientId=?', (client_id,)).fetchone()[0] == 0
            if protocol == 'json':
                row = page.locator('table').first.locator('tbody tr').filter(has_text=client_id)
                row.get_by_text('更新密鑰', exact=True).click()
                rotation = row.locator('form').evaluate('form => Object.fromEntries(new FormData(form))')
                response = context.request.post(base + '/' + client_id + '/rotate', form={})
                assert response.status == 400
                row.get_by_role('button', name='產生新密鑰').click()
                expect(page.locator('#integration-secret')).to_be_visible()
                fresh = page.locator('#integration-secret').inner_text()
                assert fresh != secret
                response = context.request.post(base + '/' + client_id + '/rotate', form=rotation)
                assert '資料已修改' in html.unescape(response.text())
                with sqlite3.connect(fixture.directory / 'sso.db') as db:
                    assert db.execute('SELECT SecretHash FROM IntegrationClients WHERE Id=?', (client_id,)).fetchone()[0] == hashlib.sha256(fresh.encode()).hexdigest().upper()
                    audit_text = str(db.execute('SELECT Detail FROM IntegrationAudits').fetchall())
                    assert secret not in audit_text and fresh not in audit_text
                page.goto(list_url)
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(shots / f'{protocol}-list-{width}.png'), full_page=True)
            page.goto(base + '/' + client_id + '/edit')
            page.get_by_label('帳密驗證', exact=True).uncheck()
            page.get_by_role('button', name='儲存設定', exact=True).click()
            page.wait_for_url(list_url)
            expect(page.locator('table').first.locator('tbody tr').filter(has_text=client_id)).to_contain_text('未授權服務')
            page.goto(base + '/' + client_id + '/edit')
            expect(page.get_by_label('帳密驗證', exact=True)).not_to_be_checked()
            unchanged = form_values(page)
            forged_metadata = context.request.post(base + '/' + client_id + '/edit', form={**unchanged,
                'ImportBatchId': 'forged', 'ImportedAt': '1234567890000',
                'Client.ImportBatchId': 'forged', 'Client.ImportedAt': '1234567890000'})
            assert forged_metadata.ok
            with sqlite3.connect(fixture.directory / 'sso.db') as db:
                assert db.execute('SELECT COUNT(*) FROM IntegrationClientServices WHERE ClientId=?', (client_id,)).fetchone()[0] == 0
                assert db.execute('SELECT ImportBatchId,ImportedAt FROM IntegrationClients WHERE Id=?', (client_id,)).fetchone() == ('', None)
        # 分類分開顯示系統與紀錄，新增入口帶入分類；使用一般連結支援鍵盤及瀏覽器上一頁。
        for kind, included, excluded in [('existing', 'managed-soap', 'managed-json'), ('new', 'managed-json', 'managed-soap')]:
            page.goto(base + '?kind=' + kind)
            expect(page.locator('table').first).to_contain_text(included)
            expect(page.locator('table').first).not_to_contain_text(excluded)
            expect(page.locator('table').nth(1)).to_contain_text(included)
            expect(page.locator('table').nth(1)).not_to_contain_text(excluded)
            expect(page.locator('.admin-head a.primary')).to_have_attribute('href', '/admin/systems/api/new?kind=' + kind)
            for width in [1440, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1000})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                page.screenshot(path=str(shots / f'{kind}-tabs-{width}.png'), full_page=True)
        page.locator('.integration-tabs a').first.focus()
        page.keyboard.press('Enter')
        page.wait_for_url(base + '?kind=existing')
        page.go_back()
        page.wait_for_url(base + '?kind=new')
        assert context.request.get(base + '?kind=invalid').status == 400
        assert context.request.get(base + '/new?kind=invalid').status == 400
        # SOAP cannot identify two enabled clients at the same source IP.
        page.goto(base + '/new?kind=existing')
        page.get_by_label('系統代碼', exact=True).fill('duplicate-soap')
        page.get_by_label('系統名稱', exact=True).fill('重複來源')
        page.get_by_label('允許來源 IP', exact=True).fill('::ffff:127.0.0.1')
        page.get_by_label('啟用此串接', exact=True).check()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        expect(page.locator('.validation')).to_contain_text('另一個啟用中的 SOAP')
        # No production verification is enabled by registering a client.
        assert context.request.post(fixture.url + '/api/integration/v1/verify', data={}).status == 503
        assert context.request.post(fixture.url + '/KuasSSOWebService.asmx', data='',
            headers={'Content-Type': 'text/xml'}).status == 503
        visitor = browser.new_context(ignore_https_errors=True)
        student = visitor.new_page()
        student.goto(fixture.url + '/account/login')
        student.get_by_label('校園帳號', exact=True).fill('002')
        student.get_by_label('密碼', exact=True).fill('123456')
        student.get_by_role('button', name='登入', exact=True).click()
        student.wait_for_url(fixture.url + '/')
        assert visitor.request.get(base).status == 403
        assert not errors and not violations, (errors, violations)
        browser.close()
        print('PASS actual JSON/SOAP admin CRUD, enable/disable, IP rules, secret issuance/rotation, stale forms, authorization, CSRF, audit secrecy, responsive layout and zero CSP/JS errors')
        print('Screenshots:', shots)
finally:
    fixture.close()
