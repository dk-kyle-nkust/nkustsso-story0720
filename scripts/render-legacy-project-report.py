"""以本機靜態報告產生 PDF；只開啟報告目錄，不啟動應用程式或資料庫。"""
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import threading
import fitz
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / 'reports/legacy-project-analysis'
PREVIEWS = ROOT / '.local/legacy-project-report-preview'
PREVIEWS.mkdir(parents=True, exist_ok=True)

class ReportHandler(SimpleHTTPRequestHandler):
    def log_message(self, *args): pass

server = ThreadingHTTPServer(('127.0.0.1', 0), functools.partial(ReportHandler, directory=str(REPORT)))
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
try:
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(viewport={'width': 1280, 'height': 1000})
        violations, errors = [], []
        context.expose_binding('reportCsp', lambda source, value: violations.append(value))
        context.add_init_script("document.addEventListener('securitypolicyviolation', e => window.reportCsp(e.violatedDirective))")
        page = context.new_page()
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(f'http://127.0.0.1:{server.server_port}/index.html', wait_until='networkidle')
        page.emulate_media(media='print')
        issues = page.locator('.sheet').evaluate_all('''sheets => sheets.flatMap(sheet => {
            const content = sheet.querySelector('.page-content').getBoundingClientRect();
            const footer = sheet.querySelector('footer').getBoundingClientRect();
            return content.bottom > footer.top - 8 ? [{page:sheet.id, overlap:Math.ceil(content.bottom-footer.top+8)}] : [];
        })''')
        if issues: raise RuntimeError(f'Print page content overlaps footer: {issues}')
        pdf = REPORT / 'legacy-project-analysis.pdf'
        page.pdf(path=str(pdf), prefer_css_page_size=True, print_background=True)
        page.emulate_media(media='screen')
        for width in [1280, 390, 320]:
            page.set_viewport_size({'width': width, 'height': 1000})
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), f'overflow {width}'
        page.set_viewport_size({'width':1280,'height':1000})
        page.locator('#p1').screenshot(path=str(REPORT / 'report-cover.png'))
        page.locator('#p2').screenshot(path=str(REPORT / 'overall-flow.png'))
        page.locator('#p6').screenshot(path=str(REPORT / 'account-sync-flow.png'))
        response = context.request.get(f'http://127.0.0.1:{server.server_port}/legacy-project-analysis.pdf')
        assert response.ok and response.body().startswith(b'%PDF')
        assert not violations and not errors, (violations,errors)
        browser.close()
    with fitz.open(pdf) as document:
        assert len(document)==13, f'Expected 13 pages, got {len(document)}'
        for i, sheet in enumerate(document):
            text = sheet.get_text()
            assert f'{i+1:02d} / 13' in text, f'Missing page number {i+1}'
            sheet.get_pixmap(matrix=fitz.Matrix(1,1)).save(PREVIEWS / f'page-{i+1:02d}.png')
        assert '來源與盤點口徑' in document[-1].get_text()
    print('PASS: 13 PDF pages, page boundaries, desktop/mobile, downloadable PDF, CSP and JavaScript')
    print('PDF:',pdf)
finally:
    server.shutdown()
    server.server_close()
    thread.join(timeout=3)
