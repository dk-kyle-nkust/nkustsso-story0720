"""Browser checks for the read-only source catalog using an isolated fixture host.

Pass the StorageChecks assembly as argv[1] for an alternate build.
No real source connection, account or password is used.
"""
import os
import json
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import time
from urllib.request import urlopen
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'tests/NkustSso.StorageChecks/bin/Debug/net10.0/NkustSso.StorageChecks.dll'
DATA = ROOT / '.local/catalog-ui-checks' / secrets.token_hex(8)
DATA.mkdir(parents=True)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    PORT = sock.getsockname()[1]
SERVER = f'http://127.0.0.1:{PORT}'

with (DATA / 'server.log').open('w', encoding='utf-8') as log:
    process = subprocess.Popen(['dotnet', str(ASSEMBLY), '--catalog-ui', SERVER], cwd=ROOT,
        stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise AssertionError('Fixture host exited; inspect its isolated log.')
            try:
                with urlopen(SERVER + '/health', timeout=1):
                    break
            except OSError:
                time.sleep(.2)
        with sync_playwright() as p:
            chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
            browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
            context = browser.new_context(viewport={'width': 1440, 'height': 1000})
            context.add_init_script("window.cspErrors=[]; document.addEventListener('securitypolicyviolation', e=>window.cspErrors.push(e.violatedDirective));")
            page = context.new_page()
            errors = []
            page.on('pageerror', lambda e: errors.append(str(e)))
            for dimension, label in [('identity_type', '來源學生'), ('division', '來源日間'), ('department', '來源單位'), ('campus', '來源校區全名'), ('student_kind', '復學生')]:
                response = page.goto(SERVER + '/admin/identity-catalog?dimension=' + dimension)
                assert response.status == 200
                expect(page.locator('main table')).to_contain_text(label)
                expect(page.locator('main')).not_to_contain_text('DEMO_')
                assert page.locator('main a[href*="/edit"], main a[href*="/new"], main input[type=checkbox]').count() == 0
                expect(page.locator('main')).not_to_contain_text('啟用中')
                assert page.evaluate('window.cspErrors.length') == 0
            page.goto(SERVER + '/admin/identity-catalog?dimension=student_status')
            expect(page.locator('main')).to_contain_text('尚待確認資料來源')
            page.goto(SERVER + '/fixture/audience')
            token = page.locator('[name="__RequestVerificationToken"]').first.input_value()
            for path in ['/options/new', '/options/fake/edit', '/dimensions/identity_type/edit', '/dimensions/new']:
                assert context.request.get(SERVER + '/admin/identity-catalog' + path).status == 404
                assert context.request.post(SERVER + '/admin/identity-catalog' + path,
                    form={'__RequestVerificationToken': token}).status == 404
            for width, height in [(1440, 1000), (390, 844)]:
                page.set_viewport_size({'width': width, 'height': height})
                page.goto(SERVER + '/admin/identity-catalog?dimension=department')
                expect(page.locator('[data-parent-code="IC00"]')).to_have_count(0)
                toggle = page.get_by_role('button', name='展開 電算與網路中心 的下層單位', exact=True)
                toggle.focus()
                toggle.press('Enter')
                expect(page.locator('[data-unit-code="IC01"]')).to_have_attribute('data-parent-code', 'IC00')
                expect(page.locator('[data-unit-code="IC01"] td[data-label="上層單位"]')).to_contain_text('電算與網路中心')
                assert page.locator('tbody tr').evaluate_all('(rows) => rows.slice(0, 3).map(r => r.dataset.unitCode)') == ['IC00', 'IC01', 'IC02']
                page.locator('.dt-search input').fill('IC01')
                expect(page.locator('tbody tr')).to_have_count(1)
                expect(page.locator('tbody tr')).to_contain_text('電算與網路中心')
                page.locator('.dt-search input').fill('')
                expect(page.locator('[data-parent-code="IC00"]')).to_have_count(0)
                expect(page.locator('tbody tr')).to_have_count(13)
                page.locator('.dt-length select').select_option('10')
                expect(page.locator('tbody tr')).to_have_count(10)
                page.locator('.dt-paging-button.next').click()
                expect(page.locator('tbody tr')).to_have_count(3)
                expect(page.locator('[data-parent-code="IC00"]')).to_have_count(0)
                page.locator('.dt-length select').select_option('25')
                page.get_by_role('button', name='展開 電算與網路中心 的下層單位', exact=True).click()
                expect(page.locator('[data-parent-code="IC00"]')).to_have_count(2)
                page.locator('.dt-search input').fill('不存在的單位')
                expect(page.locator('tbody .dt-empty')).to_be_visible()
                page.locator('.dt-search input').fill('')
                expect(page.locator('[data-parent-code="IC00"]')).to_have_count(0)
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                assert page.evaluate('window.cspErrors.length') == 0
                page.screenshot(path=str(DATA / f'catalog-{width}.png'), full_page=True)
            page.goto(SERVER + '/fixture/audience')
            page.locator('[name="audience-mode"][value="rules"]').check()
            page.locator('[data-add-family="student"]').click()
            card = page.locator('.audience-card').first
            mode = card.locator('select.condition-mode')
            mode.evaluate("el => el.tomselect.setValue('specified')")
            expect(card).to_contain_text('來源學生')
            card.locator('input[type=checkbox][value="02"]').check()
            expect(page.locator('#audience-summary')).to_contain_text('來源學生')
            add = card.locator('.audience-add-condition select')
            assert 'student_status' not in add.locator('option').evaluate_all('(xs)=>xs.map(x=>x.value)')
            add.evaluate("el => el.tomselect.setValue('student_kind')")
            card.get_by_role('button', name='新增限制').click()
            card.locator('[data-dimension=student_kind] input[value="1"]').check()
            expect(page.locator('#audience-summary')).to_contain_text('復學生')
            add.evaluate("el => el.tomselect.setValue('department')")
            card.get_by_role('button', name='新增限制').click()
            units = card.locator('[data-dimension=department]')
            expect(units.locator('input[type=checkbox][value="IC01"]')).not_to_be_visible()
            units.get_by_role('button', name='展開 電算與網路中心 的下層單位', exact=True).click()
            expect(units.locator('input[type=checkbox][value="IC01"]')).to_be_visible()
            units.locator('input[type=checkbox][value="IC00"]').check()
            selected = lambda: next(c['values'] for c in json.loads(page.locator('#AudienceJson').input_value())['groups'][0]['conditions'] if c['dimension'] == 'department')
            assert selected() == ['IC00']
            units.get_by_role('button', name='收合 電算與網路中心 的下層單位', exact=True).click()
            expect(units.locator('input[type=checkbox][value="IC01"]')).not_to_be_visible()
            search = units.get_by_role('searchbox', name='搜尋單位名稱或代碼')
            search.fill('IC01')
            expect(units.locator('input[type=checkbox][value="IC01"]')).to_be_visible()
            expect(units.locator('input[type=checkbox][value="IC02"]')).not_to_be_visible()
            units.locator('input[type=checkbox][value="IC01"]').check()
            assert selected() == ['IC00', 'IC01']
            search.fill('不存在的單位')
            expect(units.get_by_text('沒有符合的單位，請調整搜尋條件。')).to_be_visible()
            assert selected() == ['IC00', 'IC01']
            search.fill('')
            expect(units.locator('input[type=checkbox][value="IC01"]')).not_to_be_visible()
            assert selected() == ['IC00', 'IC01']
            expect(units.locator('[role=status]')).to_contain_text('已選 2 個單位')
            card.get_by_role('button', name='複製這組').click()
            expect(page.locator('[data-dimension=department] input[value="IC01"]:checked')).to_have_count(2)
            expect(page.locator('.audience-card').nth(1).locator('input[value="IC01"]')).not_to_be_visible()
            page.locator('.audience-card').nth(1).get_by_role('button', name='移除這組').click()
            assert selected() == ['IC00', 'IC01']
            expect(page.locator('#audience-summary')).to_contain_text('測試網路組')
            add.evaluate("el => el.tomselect.setValue('campus')")
            card.get_by_role('button', name='新增限制').click()
            card.locator('[data-dimension=campus] input[value="1"]').check()
            expect(page.locator('#audience-summary')).to_contain_text('來源校區全名')
            for width, height in [(1440, 1000), (390, 844)]:
                page.set_viewport_size({'width': width, 'height': height})
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
                card.screenshot(path=str(DATA / f'unit-picker-{width}.png'))
            assert page.evaluate('window.cspErrors.length') == 0 and not errors, errors
            browser.close()
            print('PASS collapsed departments, keyboard expansion, all-level search, accurate paging, retained selections, campus source, responsive layout and zero CSP/JS errors')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
        print('Isolated catalog fixture host stopped.')
