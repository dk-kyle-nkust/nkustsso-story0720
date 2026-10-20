"""Public directories, deferred login, SSO reuse and responsive UI on an isolated server."""
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
import sqlite3
from urllib.parse import urlencode, urlparse, parse_qs
from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture
from accessibility_checks import check_accessibility
from portal_language_checks import check_languages
from oidc_receiver import OidcBrowserReceiver

fixture = ServerFixture()
server = fixture.url
screenshots = fixture.directory / 'screenshots'
screenshots.mkdir()
receiver = OidcBrowserReceiver(server, fixture.directory)
with sqlite3.connect(fixture.directory / 'sso.db') as db:
    db.execute("UPDATE RegisteredSystems SET PublicStudent=1 WHERE ClientId IN ('academic','equipment')")
    db.execute("UPDATE RegisteredSystems SET PublicEmployee=1 WHERE ClientId='equipment'")
    db.execute("UPDATE RegisteredSystems SET PublicAlumni=1 WHERE ClientId='library'")
    db.execute("UPDATE RegisteredSystems SET RequirePortalLogin=1 WHERE ClientId IN ('link-stdsel','link-leave','link-centapp')")
    db.execute("UPDATE RegisteredSystems SET EntryUrl=? WHERE ClientId='link-stdsel'", (receiver.url + '/public',))
    db.execute("UPDATE RegisteredSystems SET Description=? WHERE ClientId='link-suggest'", ('校務意見反映與處理進度。',))
    for client in ['academic', 'equipment']:
        callback = receiver.url + '/' + client + '/callback'
        db.execute('UPDATE RegisteredSystems SET EntryUrl=?,RedirectUri=? WHERE ClientId=?',
                   (receiver.url + '/' + client, callback, client))
        db.execute('UPDATE OidcApplications SET RedirectUris=? WHERE ClientId=?', (json.dumps([callback]), client))


try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 1100})
        violations = []
        context.expose_binding('recordCsp', lambda source, directive: violations.append(directive))
        context.add_init_script("window.cspErrors=[]; document.addEventListener('securitypolicyviolation', e=>{ window.cspErrors.push(e.violatedDirective); window.recordCsp(e.violatedDirective); });")
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        assert page.goto(server + '/').status == 200
        expect(page.locator('header.site-header')).to_have_count(1)
        for name in ['學生專區', '教職員專區', '校友專區']:
            expect(page.get_by_role('heading', name=name, exact=True)).to_be_visible()
        check_accessibility(page)
        assert page.locator('.tile').count() == 0
        assert context.request.get(server + '/integration', max_redirects=0).status == 302
        assert context.request.get(server + '/admin/systems', max_redirects=0).status == 302
        assert page.locator('.gateway-alumni').bounding_box()['height'] < page.locator('.gateway-student').bounding_box()['height']
        for width in [1440, 1024, 768, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1100})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
            assert page.evaluate('window.cspErrors.length') == 0
            page.screenshot(path=str(screenshots / f'gateway-{width}.png'), full_page=True)
        expect(page.locator('.gateway-guides, .gateway-help')).to_have_count(0)
        page.goto(server + '/faq')
        expect(page.get_by_role('heading', name='常見問題', exact=True)).to_be_visible()
        expect(page.locator('.faq-guide-grid a')).to_have_count(4)
        check_accessibility(page, expanded=True)
        page.screenshot(path=str(screenshots / 'faq-320.png'), full_page=True)
        print('PASS public identity selection, compact alumni area, public FAQ and protected services')

        page.set_viewport_size({'width': 1440, 'height': 1100})
        for area, label in [('student', '學生專區'), ('employee', '教職員專區'), ('alumni', '校友專區')]:
            page.goto(server + '/portal')
            page.get_by_role('link', name='進入' + label, exact=False).click()
            page.wait_for_url(server + '/portal/' + area)
            expect(page.locator('header.site-header')).to_have_count(1)
            expect(page.locator('form.login-form')).to_have_count(0)
            expect(page.get_by_role('heading', name=label, exact=True)).to_be_visible()
            assert page.locator('[data-directory-filter]').count() >= 3
            assert not any(c['name'] == 'NkustSso.Session.v1' for c in context.cookies())
            for width in [1440, 1024, 768, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1100})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                assert page.evaluate('window.cspErrors.length') == 0
                page.screenshot(path=str(screenshots / f'directory-{area}-{width}.png'), full_page=True)
            check_accessibility(page)
            search = page.get_by_label('搜尋服務', exact=True)
            search.fill('no-such-service-xxxxx')
            expect(page.locator('#directory-empty')).to_be_visible()
            search.fill('')
            page.locator('[data-directory-filter="academic"]').click()
            assert page.locator('.directory-group:visible').count() == 1
            page.locator('[data-directory-filter="all"]').click()
            page.goto(server + '/account/login?area=' + area)
            expect(page.locator('.login-identity')).to_have_text(label)
            expect(page.locator('.topbar')).to_have_count(0)
            expect(page.locator('.login-area-nav')).to_have_count(0)
            assert page.locator('[name="Area"]').input_value() == area
            assert page.locator('form.login-form').get_attribute('action') == '/account/login'
            check_accessibility(page)
            password = page.get_by_label('密碼', exact=True)
            password.fill('sample-value')
            page.get_by_role('button', name='顯示密碼', exact=True).click()
            expect(password).to_have_attribute('type', 'text')
            page.get_by_role('button', name='隱藏密碼', exact=True).click()
            expect(password).to_have_attribute('type', 'password')
            password.fill('')
            for width in [1440, 1024, 768, 390, 320]:
                page.set_viewport_size({'width': width, 'height': 1100})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                assert page.evaluate('window.cspErrors.length') == 0
                page.screenshot(path=str(screenshots / f'login-{area}-{width}.png'), full_page=True)
            check_accessibility(page, expanded=True)
        assert not errors, errors
        print('PASS three anonymous directories, search/categories, compact login, mobile layout and zero CSP/JS errors')

        page.goto(server + '/portal/student')
        service = page.locator('a[href="/portal/student/systems/link-suggest"]')
        tip = page.locator('#service-description-link-suggest')
        info = page.get_by_role('button', name='校務建言：功能說明', exact=True)
        expect(tip).to_be_hidden()
        service.hover()
        expect(tip).to_have_text('校務意見反映與處理進度。')
        expect(tip).to_be_visible()
        tip.hover()
        expect(tip).to_be_visible()
        page.keyboard.press('Escape')
        expect(tip).to_be_hidden()
        page.mouse.move(0, 0)
        service.focus()
        expect(tip).to_be_visible()
        page.keyboard.press('Escape')
        expect(tip).to_be_hidden()
        expect(service).to_be_focused()
        info.click()
        expect(tip).to_be_visible()
        expect(info).to_have_attribute('aria-expanded', 'true')
        page.get_by_label('搜尋服務', exact=True).fill('處理進度')
        expect(tip).to_be_hidden()
        expect(service).to_be_visible()
        page.get_by_label('搜尋服務', exact=True).fill('')
        page.locator('a[href="/portal/student/systems/link-elearning"]').hover()
        assert page.locator('li:has(a[href="/portal/student/systems/link-elearning"]) [data-service-info]').count() == 0
        touch = browser.new_context(ignore_https_errors=True, has_touch=True, is_mobile=True, viewport={'width': 390, 'height': 844})
        mobile = touch.new_page()
        mobile.goto(server + '/portal/student')
        mobile_info = mobile.get_by_role('button', name='校務建言：功能說明', exact=True)
        mobile_info.tap()
        expect(mobile.locator('#service-description-link-suggest')).to_be_visible()
        assert mobile.url == server + '/portal/student'
        mobile_info.tap()
        expect(mobile.locator('#service-description-link-suggest')).to_be_hidden()
        touch.close()
        print('PASS description hover, tooltip hover persistence, keyboard/Escape, touch toggle and description search')

        response = context.request.get(server + '/portal/student/systems/link-elearning', max_redirects=0)
        assert response.status == 302 and response.headers['location'] == 'https://elearning.nkust.edu.tw/'
        for client in ['link-stdsel', 'link-leave', 'link-centapp']:
            response = context.request.get(server + '/portal/student/systems/' + client, max_redirects=0)
            assert response.status == 302 and response.headers['location'].startswith('/account/login?')
            assert parse_qs(urlparse(response.headers['location']).query)['returnUrl'] == ['/portal/student/systems/' + client]
        assert context.request.get(server + '/portal/student/systems/library', max_redirects=0).status == 404
        assert context.request.get(server + '/portal/unknown', max_redirects=0).status == 404
        response = context.request.get(server + '/portal/student/systems/academic?returnUrl=https://example.invalid', max_redirects=0)
        assert response.status == 302 and response.headers['location'].startswith('/account/login?')
        assert parse_qs(urlparse(response.headers['location']).query)['returnUrl'] == ['/portal/student/systems/academic']

        page.goto(server + '/portal/student/systems/academic')
        page.get_by_label('校園帳號', exact=True).fill('002')
        page.get_by_label('密碼', exact=True).fill('Wrong!Password123')
        page.get_by_role('button', name='登入', exact=True).click()
        expect(page.get_by_role('alert')).to_contain_text('無法登入')
        expect(page.get_by_role('alert')).to_be_focused()
        expect(page.locator('.login-identity')).to_have_text('學生專區')
        assert page.locator('[name="Area"]').input_value() == 'student'
        assert page.locator('[name="ReturnUrl"]').input_value() == '/portal/student/systems/academic'
        second = context.new_page()
        second.goto(server + '/account/login?area=employee')
        expect(second.locator('.login-identity')).to_have_text('教職員專區')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        expect(page.get_by_role('heading', name='Service ready')).to_be_visible()
        page.goto(server + '/portal/student')
        with context.expect_page() as opened:
            page.locator('a[href="/portal/student/systems/equipment"]').click()
        service_tab = opened.value
        expect(service_tab.get_by_role('heading', name='Service ready')).to_be_visible()
        assert page.url == server + '/portal/student'
        assert service_tab.evaluate('window.opener === null')
        service_tab.close()
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT COUNT(*) FROM LoginEvents WHERE Outcome='success'").fetchone()[0] == 1
        page.goto(server + '/')
        expect(page.locator('header.site-header')).to_have_count(1)
        expect(page.locator('header').get_by_role('button', name='登出', exact=True)).to_be_visible()
        expect(page.get_by_role('heading', name='學生專區', exact=True)).to_be_visible()
        expect(page.locator('#portal-pinned, #portal-recent, #portal-search')).to_have_count(0)
        assert context.request.get(server + '/admin/systems').status == 403
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT IdentityType FROM AspNetUsers WHERE UserName='002'").fetchone()[0] == 'student'
        second.reload()
        second.wait_for_url(server + '/')
        expect(second.locator('form.login-form')).to_have_count(0)
        response = context.request.get(server + '/account/login?' + urlencode({'area': 'employee', 'returnUrl': '/integration'}), max_redirects=0)
        assert response.status == 302 and response.headers['location'] == '/integration'
        response = context.request.get(server + '/account/login?' + urlencode({'returnUrl': 'https://example.invalid'}), max_redirects=0)
        assert response.status == 302 and response.headers['location'] == '/'
        print('PASS shared session across entry choices/tabs, safe return URL and unchanged account privileges')

        verifier = secrets.token_urlsafe(40)
        challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('=')
        query = {'client_id': 'academic', 'redirect_uri': receiver.url + '/academic/callback',
                 'response_type': 'code', 'scope': 'openid', 'code_challenge': challenge,
                 'code_challenge_method': 'S256', 'state': 'gateway-test', 'nonce': 'gateway-nonce'}
        response = context.request.get(server + '/connect/authorize?' + urlencode(query), max_redirects=0)
        assert response.status == 302 and 'code' in parse_qs(urlparse(response.headers['location']).query)
        query['prompt'] = 'login'
        response = context.request.get(server + '/connect/authorize?' + urlencode(query), max_redirects=0)
        location = response.headers['location']
        assert response.status == 302 and 'reauthenticate=true' in location
        response = context.request.get(server + location, max_redirects=0)
        assert response.status == 200 and 'name="Password"' in response.text()
        print('PASS automatic OIDC authorization and explicit reauthentication')

        page.get_by_role('button', name='登出', exact=True).click()
        page.wait_for_url(server + '/portal')
        expect(page.get_by_role('heading', name='校友專區', exact=True)).to_be_visible()
        second.goto(server + '/account/login?area=employee')
        expect(second.locator('form.login-form')).to_be_visible()
        page.goto(server + '/account/login?area=unknown')
        expect(page.locator('.login-identity')).to_have_text('統一登入')
        assert page.locator('[name="Area"]').input_value() == ''
        assert page.evaluate('window.cspErrors.length') == 0 and not errors

        # 未登入時帶 prompt=login 也只能要求一次帳密，登入後直接返回授權結果。
        page.goto(server + '/connect/authorize?' + urlencode(query))
        expect(page.locator('form.login-form')).to_be_visible()
        assert 'prompt=login' not in page.locator('[name="ReturnUrl"]').input_value()
        page.get_by_label('校園帳號', exact=True).fill('002')
        page.get_by_label('密碼', exact=True).fill('123456')
        page.get_by_role('button', name='登入', exact=True).click()
        expect(page.get_by_role('heading', name='Service ready')).to_be_visible()
        print('PASS first-time prompt=login does not ask for a second login')

        # 外部連結的入口驗證：登入後回到原服務，再開另一服務沿用登入狀態。
        gated = browser.new_context(ignore_https_errors=True)
        gated.expose_binding('recordCsp', lambda source, directive: violations.append(directive))
        gated.add_init_script("document.addEventListener('securitypolicyviolation', e=>window.recordCsp(e.violatedDirective));")
        gate = gated.new_page()
        gate.goto(server + '/portal/student')
        gate.locator('#directory-search').fill('選課')
        with gated.expect_page() as opened:
            gate.locator('a[href="/portal/student/systems/link-stdsel"]').click()
        login_tab = opened.value
        expect(login_tab.locator('form.login-form')).to_be_visible()
        login_tab.get_by_label('校園帳號', exact=True).fill('002')
        login_tab.get_by_label('密碼', exact=True).fill('123456')
        login_tab.get_by_role('button', name='登入', exact=True).click()
        expect(login_tab.get_by_role('heading', name='External service')).to_be_visible()
        assert login_tab.evaluate('window.opener === null')
        assert gate.url == server + '/portal/student'
        expect(gate.locator('#directory-search')).to_have_value('選課')
        gate.locator('#directory-search').fill('')
        with gated.expect_page() as opened:
            gate.locator('a[href="/portal/student/systems/equipment"]').click()
        service_tab = opened.value
        expect(service_tab.get_by_role('heading', name='Service ready')).to_be_visible()
        expect(service_tab.locator('form.login-form')).to_have_count(0)
        assert gate.url == server + '/portal/student'
        response = gated.request.get(server + '/portal/student/systems/link-leave', max_redirects=0)
        assert response.status == 302 and response.headers['location'].startswith('https://')
        gated.close()
        print('PASS external login gate returns to requested service and reuses session')

        check_languages(browser, server, screenshots)

        # 刊登設定可保存，隱藏或停用後的舊入口不能繼續要求登入或轉往外部。
        admin = browser.new_context(ignore_https_errors=True)
        editor = admin.new_page()
        editor.goto(server + '/admin/systems/academic/edit')
        editor.get_by_label('校園帳號', exact=True).fill('001')
        editor.get_by_label('密碼', exact=True).fill('123456')
        editor.get_by_role('button', name='登入', exact=True).click()
        editor.wait_for_url(server + '/admin/systems/academic/edit')
        editor.get_by_label('學生專區', exact=True).uncheck()
        editor.get_by_label('教職員專區', exact=True).check()
        editor.get_by_role('button', name='儲存設定', exact=True).click()
        editor.wait_for_url(server + '/admin/systems?section=sso')
        assert context.request.get(server + '/portal/student/systems/academic', max_redirects=0).status == 404
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT PublicStudent,PublicEmployee FROM RegisteredSystems WHERE ClientId='academic'").fetchone() == (0, 1)
            db.execute("UPDATE RegisteredSystems SET Enabled=0 WHERE ClientId='equipment'")
        assert context.request.get(server + '/portal/student/systems/equipment', max_redirects=0).status == 404
        response = context.request.get(server + '/account/continue?' + urlencode({'returnUrl': 'https://example.invalid'}))
        assert response.status == 200 and 'id="login-continue" href="/"' in response.text()
        assert not violations and not errors, (violations, errors)
        editor.goto(server + '/admin/systems/link-stdsel/edit')
        expect(editor.get_by_label('需先登入本站', exact=True)).to_be_checked()
        editor.get_by_label('需先登入本站', exact=True).uncheck()
        editor.get_by_role('button', name='儲存設定', exact=True).click()
        editor.wait_for_url(server + '/admin/systems?section=links')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT RequirePortalLogin,Protocol FROM RegisteredSystems WHERE ClientId='link-stdsel'").fetchone() == (0, 'link')
        editor.goto(server + '/admin/systems/link-suggest/edit')
        editor.get_by_label('功能詳述', exact=True).fill('更新後的校務意見說明。')
        editor.get_by_label('英文系統名稱', exact=True).fill('Campus suggestions')
        editor.get_by_label('英文功能詳述', exact=True).fill('Submit feedback and track progress. <script> is plain text.')
        editor.get_by_label('首頁排序（分類內）', exact=True).fill('9999')
        editor.get_by_role('button', name='儲存設定', exact=True).click()
        editor.wait_for_url(server + '/admin/systems?section=links')
        editor.goto(server + '/admin/categories/general/edit')
        editor.get_by_label('分類排序', exact=True).fill('0')
        editor.get_by_label('英文分類名稱', exact=True).fill('Campus support')
        editor.get_by_role('button', name='儲存分類', exact=True).click()
        editor.wait_for_url(server + '/admin/categories')
        page.goto(server + '/portal/student')
        expect(page.locator('.directory-group').first).to_have_attribute('data-directory-category', 'general')
        expect(page.locator('[data-directory-category="general"] .directory-services li').last.locator('a')).to_have_attribute('href', '/portal/student/systems/link-suggest')
        page.locator('a[href="/portal/student/systems/link-suggest"]').focus()
        expect(page.locator('#service-description-link-suggest')).to_have_text('更新後的校務意見說明。')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT Description,SortOrder FROM RegisteredSystems WHERE ClientId='link-suggest'").fetchone() == ('更新後的校務意見說明。', 9999)
            assert db.execute("SELECT SortOrder FROM PortalCategories WHERE Key='general'").fetchone() == (0,)
        page.get_by_role('button', name='Switch to English').click()
        expect(page.locator('.directory-group h2').first).to_have_text('Campus support')
        expect(page.locator('li:has(a[href="/portal/student/systems/link-suggest"]) .directory-service-name')).to_have_text('Campus suggestions')
        expect(page.locator('#service-description-link-suggest')).to_have_text('Submit feedback and track progress. <script> is plain text.')
        expect(page.locator('#service-description-link-suggest script')).to_have_count(0)
        page.get_by_label('Search services', exact=True).fill('track progress')
        expect(page.locator('#directory-status')).to_have_text('1 service')
        page.reload()
        expect(page.locator('.directory-group h2').first).to_have_text('Campus support')
        page.get_by_role('button', name='切換為繁體中文').click()
        expect(page.locator('.directory-group h2').first).to_have_text('綜合服務')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT DisplayNameEn,DescriptionEn FROM RegisteredSystems WHERE ClientId='link-suggest'").fetchone() == ('Campus suggestions', 'Submit feedback and track progress. <script> is plain text.')
            assert db.execute("SELECT NameEn FROM PortalCategories WHERE Key='general'").fetchone() == ('Campus support',)
        assert not violations and not errors, (violations, errors)
        print('PASS administrator-managed descriptions, service order and category order persist and update the public menu')
        editor.goto(server + '/admin/systems/link-suggest/edit')
        editor.get_by_label('登入後才顯示', exact=True).check()
        editor.get_by_role('button', name='儲存設定', exact=True).click()
        editor.wait_for_url(server + '/admin/systems?section=links')
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            assert db.execute("SELECT RequireLoginToView FROM RegisteredSystems WHERE ClientId='link-suggest'").fetchone() == (1,)
            db.execute("UPDATE PortalAudiences SET IdentityTypesJson='[\"student\"]', RulesJson='' WHERE ClientId='link-suggest'")
        visitor = browser.new_context(ignore_https_errors=True)
        hidden = visitor.request.get(server + '/portal/student').text()
        assert 'link-suggest' not in hidden and 'Campus suggestions' not in hidden
        assert visitor.request.get(server + '/portal/student/systems/link-suggest', max_redirects=0).status == 404
        assert admin.request.get(server + '/portal/student/systems/link-suggest', max_redirects=0).status == 404
        entitled = visitor.new_page()
        entitled.goto(server + '/portal/student')
        entitled.get_by_role('link', name='登入', exact=True).click()
        entitled.get_by_label('校園帳號', exact=True).fill('002')
        entitled.get_by_label('密碼', exact=True).fill('123456')
        entitled.get_by_role('button', name='登入', exact=True).click()
        entitled.wait_for_url(server + '/portal/student')
        expect(entitled.locator('li[data-system-id="link-suggest"]')).to_be_visible()
        assert visitor.request.get(server + '/portal/student/systems/link-suggest', max_redirects=0).status == 302
        with sqlite3.connect(fixture.directory / 'sso.db') as db:
            db.execute("UPDATE PortalAudiences SET IdentityTypesJson='[\"staff\"]' WHERE ClientId='link-suggest'")
        assert visitor.request.get(server + '/portal/student/systems/link-suggest', max_redirects=0).status == 404
        entitled.reload()
        expect(entitled.locator('li[data-system-id="link-suggest"]')).to_have_count(0)
        visitor.close()
        print('PASS private names/descriptions excluded before login, identity filtering and direct-link rechecks')
        admin.close()
        print('PASS administrator publication settings and withdrawn/disabled service checks')
        browser.close()
        print('PASS logout returns to identity selection and invalid area falls back safely')
finally:
    receiver.close()
    fixture.close()
print('All identity gateway checks passed.')
