"""Isolated HTTPS/browser checks for dynamic categories and strict portal audiences.

Usage: python tests/categories.py <built NkustSso.Server.dll>
Only the temporary database/server owned by this script are used and stopped.
"""
import html
import os
from pathlib import Path
from server_fixture import seed_server
import secrets
import socket
import sqlite3
import ssl
import subprocess
import sys
import time
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'NkustSso.Server/bin/Debug/net10.0/NkustSso.Server.dll'
DATA = ROOT / '.local/category-checks' / secrets.token_hex(8)
DATA.mkdir(parents=True)
seed_server(DATA)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    SERVER = f'https://localhost:{sock.getsockname()[1]}'
process = None
log = None


def start():
    global process, log
    log = (DATA / 'server.log').open('a', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(ASSEMBLY), '--contentRoot', str(ROOT / 'NkustSso.Server'),
        '--urls', SERVER, '--Sso:Issuer', SERVER + '/', '--Sso:DataDirectory', str(DATA), '--Sso:StorageProvider', 'SQLite', '--CampusAuthentication:Enabled', 'false', '--ConnectionStrings:LoginAuth', ''], cwd=ROOT,
        env={**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development'}, stdout=log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    for _ in range(100):
        if process.poll() is not None:
            raise AssertionError('Isolated server exited; inspect its test log.')
        try:
            with urlopen(SERVER + '/health', context=ssl._create_unverified_context(), timeout=1) as response:
                if response.status == 200:
                    return
        except OSError:
            time.sleep(.2)
    raise AssertionError('Isolated server startup timeout.')


def stop():
    if process and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    if log:
        log.close()


def login(page, account):
    page.goto(SERVER + '/account/login')
    page.get_by_label('校園帳號', exact=True).fill(account)
    page.get_by_label('密碼', exact=True).fill('123456')
    page.get_by_role('button', name='登入', exact=False).click()
    page.wait_for_url(SERVER + '/')


def fields(page):
    return page.locator('form.system-form').evaluate('form => Object.fromEntries(new FormData(form))')


try:
    start()
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True)
        student = browser.new_context(ignore_https_errors=True)
        page, pupil = admin.new_page(), student.new_page()
        assert admin.request.get(SERVER + '/admin/categories', max_redirects=0).status == 302
        login(pupil, '002')
        assert student.request.get(SERVER + '/admin/categories').status == 403
        for suffix in ('/portal/student', '/portal/student?all=true'):
            pupil.goto(SERVER + suffix)
            assert pupil.locator('li[data-system-id="link-account03"]').count() == 0
            assert pupil.locator('li[data-system-id="link-signin"]').count() == 0
            assert pupil.locator('a[href="/?all=true"]').count() == 0
            assert 'link-account03' not in pupil.content()
        # Browser history/favorites cannot resurrect an entry the server no longer sends.
        pupil.evaluate("localStorage.setItem('nkust.portal.pins', JSON.stringify(['link-account03']))")
        pupil.reload()
        assert pupil.locator('#portal-pinned-grid .tile').count() == 0
        login(page, '001')
        assert admin.request.post(SERVER + '/admin/categories/new', form={'Category.Key': 'bad'}).status == 400
        assert admin.request.post(SERVER + '/admin/categories/general/delete', form={'revision': 'bad'}).status == 400
        print('PASS live admin authorization, CSRF and strict audience filtering including all=true and favorites')

        page.goto(SERVER + '/admin/categories/new')
        page.get_by_label('分類代碼').fill('test_custom')
        page.get_by_label('分類名稱', exact=True).fill('自訂校務服務')
        page.get_by_label('分類排序').fill('0')
        page.locator('select[name="Category.Accent"]').select_option('violet', force=True)
        page.get_by_role('button', name='儲存分類').click()
        page.wait_for_url(SERVER + '/admin/categories')
        page.goto(SERVER + '/admin/categories/test_custom/edit')
        stale = fields(page)
        page.get_by_label('分類名稱', exact=True).fill('校務測試分類')
        page.get_by_role('button', name='儲存分類').click()
        page.wait_for_url(SERVER + '/admin/categories')
        response = admin.request.post(SERVER + '/admin/categories/test_custom/edit', form=stale)
        assert '已被其他操作更新' in html.unescape(response.text())

        # Move two real demo clients, preserving protocol settings and visibility.
        for client, order in [('academic', 20), ('library', 10)]:
            page.goto(SERVER + f'/admin/systems/{client}/edit')
            page.locator('select[name="System.Category"]').select_option('test_custom', force=True)
            page.get_by_label('首頁排序（分類內）').fill(str(order))
            page.get_by_role('button', name='儲存設定').click()
            page.wait_for_url(SERVER + '/admin/systems?section=sso')
        pupil.goto(SERVER + '/portal/student')
        assert pupil.locator('.directory-group[data-directory-category]').first.get_attribute('data-directory-category') == 'test_custom'
        assert pupil.locator('.directory-group[data-directory-category="test_custom"] li[data-system-id]').evaluate_all(
            'tiles => tiles.map(t => t.dataset.systemId)') == ['library', 'academic']
        pupil.locator('[data-directory-filter="test_custom"]').click()
        expect(pupil.locator('#directory-status')).to_contain_text('共 2 項服務')
        page.goto(SERVER + '/admin/systems?Protocol=oidc&Status=enabled&Visibility=shown&Category=test_custom')
        assert page.locator('.system-registry tbody tr').count() == 1
        expect(page.locator('.system-registry')).to_contain_text('academic')
        page.goto(SERVER + '/admin/categories')
        expect(page.get_by_role('row').filter(has_text='test_custom').first).to_contain_text('2 個系統')
        # A forged delete still fails even though the UI does not offer it for an in-use category.
        page.goto(SERVER + '/admin/categories/test_custom/edit')
        current = fields(page)
        response = admin.request.post(SERVER + '/admin/categories/test_custom/delete', form={
            '__RequestVerificationToken': current['__RequestVerificationToken'], 'revision': current['Category.Revision']})
        assert '此分類仍有系統使用' in html.unescape(response.text())
        page.goto(SERVER + '/admin/systems/academic/edit')
        bad = fields(page)
        bad['System.Category'] = 'deleted-category'
        assert '分類不存在或已被刪除' in html.unescape(admin.request.post(SERVER + '/admin/systems/academic/edit', form=bad).text())
        print('PASS category CRUD, stale form, two-level ordering, combined filters and invalid/in-use category rejection')

        # Remove an unused default category; restart must not seed it again.
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE RegisteredSystems SET Category='general' WHERE Category='hr'")
        page.goto(SERVER + '/admin/categories/hr/edit')
        unused = fields(page)
        response = admin.request.post(SERVER + '/admin/categories/hr/delete', form={
            '__RequestVerificationToken': unused['__RequestVerificationToken'], 'revision': unused['Category.Revision']})
        assert '已刪除分類' in html.unescape(response.text())
        for target in ('/admin/categories', '/admin/categories/test_custom/edit', '/admin/systems'):
            page.goto(SERVER + target)
            page.set_viewport_size({'width': 390, 'height': 844})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), target
        page.goto(SERVER + '/admin/categories')
        page.screenshot(path=str(DATA / 'categories-mobile.png'), full_page=True)
        page.set_viewport_size({'width': 1440, 'height': 1000})
        page.screenshot(path=str(DATA / 'categories-desktop.png'), full_page=True)
        with sqlite3.connect(DATA / 'sso.db') as db:
            assert db.execute('SELECT count(*) FROM PortalCategoryAudits').fetchone()[0] == 3
            assert db.execute("SELECT count(*) FROM PortalCategoryAudits WHERE ActorId='' OR RemoteIp=''").fetchone()[0] == 0
            db.execute("DELETE FROM AspNetUserRoles WHERE UserId='demo-session-admin'")
        assert admin.request.get(SERVER + '/admin/categories').status == 403
        assert admin.request.post(SERVER + '/admin/categories/test_custom/edit', form=current).status == 403
        stop()
        start()
        with sqlite3.connect(DATA / 'sso.db') as db:
            assert db.execute("SELECT Name,SortOrder FROM PortalCategories WHERE Key='test_custom'").fetchone() == ('校務測試分類', 0)
            assert db.execute("SELECT count(*) FROM PortalCategories WHERE Key='hr'").fetchone()[0] == 0
            assert db.execute("SELECT Category FROM RegisteredSystems WHERE ClientId='academic'").fetchone()[0] == 'test_custom'
        print('PASS responsive pages, audit, live role revocation and restart preservation')
        browser.close()
finally:
    stop()
    print('Isolated category verification service stopped.')
