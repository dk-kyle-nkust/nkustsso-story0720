"""隔離驗證 Web API 單一登入設定、JSON 兌換及跨端點重放防護。"""
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
from urllib.parse import urlencode, urlparse, parse_qs

from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture


def login(page, url, account):
    page.goto(url + '/account/login')
    page.get_by_label('校園帳號', exact=True).fill(account)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=False).click()
    page.wait_for_url(url + '/')


server = ServerFixture()
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True)
        student = browser.new_context(ignore_https_errors=True)
        anonymous = browser.new_context(ignore_https_errors=True)
        csp, errors = [], []
        admin.expose_binding('__webApiCsp', lambda source, directive: csp.append(directive))
        admin.add_init_script("document.addEventListener('securitypolicyviolation', e => window.__webApiCsp(e.violatedDirective));")
        page = admin.new_page()
        page.on('pageerror', lambda e: errors.append(str(e)))
        login(page, server.url, '001')
        login(student.new_page(), server.url, '002')
        page.goto(server.url + '/admin/systems/new?section=sso')
        expect(page.locator('option[value="ticket"]')).to_have_text('Web API 單一登入')
        def choose_protocol(value):
            wrapper = page.locator('select[name="System.Protocol"]').locator('..').locator('.ts-wrapper')
            wrapper.locator('.ts-control').click()
            wrapper.locator(f'.ts-dropdown .option[data-value="{value}"]').click()
            expect(wrapper.locator('.ts-control')).to_contain_text('Web API 單一登入' if value == 'ticket' else 'OIDC')

        choose_protocol('ticket')
        account_field = page.locator('.claim-options label').filter(has=page.locator('input[value="1"]'))
        expect(account_field.locator('small:visible')).to_have_text('username')
        expect(page.locator('#web-api-settings')).to_be_visible()
        expect(page.locator('#web-api-settings')).to_contain_text('/api/sso/v1/redeem')
        artifacts = server.directory / 'screenshots'
        artifacts.mkdir()
        for width in (1440, 390, 320):
            page.set_viewport_size(dict(width=width, height=960))
            page.locator('#web-api-settings').scroll_into_view_if_needed()
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), width
            page.locator('#web-api-settings').screenshot(path=str(artifacts / f'webapi-{width}.png'))
            expect(page.locator('.web-api-diagram img')).to_be_visible()
            assert page.locator('.web-api-diagram img').evaluate('(img) => img.complete && img.naturalWidth > 0')
            # 原生展開區塊支援鍵盤；長網址與 JSON 在手機寬度仍可閱讀。
            technical = page.locator('.web-api-technical').first
            technical.locator('summary').focus()
            page.keyboard.press('Enter')
            expect(technical.locator('pre').last).to_be_visible()
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), width
            technical.screenshot(path=str(artifacts / f'webapi-technical-{width}.png'))
            technical.locator('summary').focus()
            page.keyboard.press('Enter')
        choose_protocol('oidc')
        expect(account_field.locator('small:visible')).to_have_text('preferred_username')
        expect(page.locator('#web-api-settings')).to_be_hidden()
        choose_protocol('ticket')
        callback = 'https://app.example.edu/sso/callback'
        page.locator('[name="System.ClientId"]').fill('webapi_test')
        page.locator('[name="System.DisplayName"]').fill('Web API 測試')
        page.locator('[name="System.EntryUrl"]').fill('https://app.example.edu/sso/login')
        page.locator('[name="System.RedirectUri"]').fill(callback)
        page.locator('input[name="AllowedClaims"][value="2"]').check()
        page.get_by_role('button', name='建立系統', exact=True).click()
        expect(page.get_by_role('heading', name='系統已建立')).to_be_visible()
        secret = page.locator('#client-secret').inner_text()
        expect(page.get_by_role('heading', name='Web API 單一登入串接指南')).to_be_visible()
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
        page.locator('.web-api-technical').first.locator('summary').click()
        expect(page.get_by_label('JSON 票券兌換請求範例', exact=True)).to_contain_text(callback)
        example = json.loads(page.get_by_label('JSON 身分回應範例', exact=True).inner_text())
        assert 'name' in example and 'username' not in example and 'class_name' not in example
        expect(page.locator('#handoff-fields')).to_contain_text('使用者姓名')
        expect(page.locator('#handoff-fields')).not_to_contain_text('班級名稱')
        for width in (1440, 390, 320):
            page.set_viewport_size(dict(width=width, height=960))
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), width
            page.screenshot(path=str(artifacts / f'created-{width}.png'), full_page=True, mask=[page.locator('#client-secret')])
        # 僅截取說明區，不將建立時顯示的密鑰存入截圖。
        page.locator('.web-api-guide').screenshot(path=str(artifacts / 'webapi-created-guide-320.png'))
        for asset in ('web-api-sso.svg', 'web-api-sso.png', 'web-api-sso.http.txt'):
            download = anonymous.request.get(server.url + '/guides/' + asset)
            assert download.status == 200, asset
            assert secret.encode() not in download.body()
        headers = {'Authorization': 'Basic ' + base64.b64encode(('webapi_test:' + secret).encode()).decode()}
        endpoint = server.url + '/api/sso/v1/redeem'

        def issue(context=student):
            verifier = secrets.token_urlsafe(32)
            state = secrets.token_urlsafe(32)
            challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('=')
            response = context.request.get(server.url + '/ticket/authorize?' + urlencode(dict(
                client_id='webapi_test', redirect_uri=callback, state=state,
                code_challenge=challenge, code_challenge_method='S256')), max_redirects=0)
            assert response.status == 302
            query = parse_qs(urlparse(response.headers['location']).query)
            if context == anonymous:
                assert urlparse(response.headers['location']).path == '/account/login'
                return
            assert query['state'] == [state]
            return dict(ticket=query['ticket'][0], redirect_uri=callback, code_verifier=verifier)

        def exchange(data, auth=headers):
            return anonymous.request.post(endpoint, data=data, headers=auth)

        issue(anonymous)
        body = issue()
        assert exchange(body, {}).status == 401
        assert exchange({**body, 'code_verifier': secrets.token_urlsafe(32)}).status == 400
        assert exchange({**body, 'redirect_uri': 'https://other.example/callback'}).status == 400
        for invalid in ({}, {**body, 'ticket': None}, {**body, 'code_verifier': 'short'}, {**body, 'ticket': 12}):
            assert exchange(invalid).status == 400
        for raw in ('null', '{broken'):
            assert anonymous.request.post(endpoint, data=raw, headers={**headers, 'Content-Type': 'application/json'}).status == 400
        assert anonymous.request.post(endpoint, form=body, headers=headers).status == 415
        response = exchange(body)
        assert response.status == 200
        result = response.json()
        assert set(result) == {'issuer', 'client_id', 'sub', 'name', 'authenticated_at', 'session_expires_at'}
        assert result['sub'] == 'demo-student-active' and result['client_id'] == 'webapi_test'
        assert result['issuer'] == server.url + '/' and result['name']
        assert 'no-store' in response.headers['cache-control']
        assert exchange(body).status == 400
        assert anonymous.request.post(server.url + '/ticket/redeem', form=body, headers=headers).status == 400
        body = issue()
        assert anonymous.request.post(server.url + '/ticket/redeem', form=body, headers=headers).status == 200
        assert exchange(body).status == 400
        # 同一中央 Cookie 可再次取得新票券，接收後端不需要使用者 Cookie 即可兌換。
        assert exchange(issue()).status == 200
        body = issue()
        page.goto(server.url + '/admin/systems/webapi_test/edit')
        page.locator('[name="System.Enabled"][type="checkbox"]').uncheck()
        page.get_by_role('button', name='儲存設定', exact=True).click()
        page.wait_for_url(server.url + '/admin/systems?section=sso')
        assert exchange(body).status == 401
        assert not csp and not errors, (csp, errors)
        browser.close()
        print('PASS Web API creation, responsive UI, CSP, SSO reuse, JSON validation, field policy, PKCE/callback checks, cross-endpoint replay and disable')
        print('Screenshots:', artifacts)
finally:
    server.close()
