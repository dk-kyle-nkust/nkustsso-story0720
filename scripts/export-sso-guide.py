"""將串接流程書的 Mermaid 圖表轉成 SVG，匯出離線 HTML 與 PDF。

需要 markdown-it-py、Playwright 與 Chrome；渲染器由 --mermaid 指定本機檔案。
不啟動 SSO，不修改資料庫，也不在輸出的文件中載入 JavaScript。
"""
import argparse
import html
import os
from pathlib import Path
import re
from markdown_it import MarkdownIt
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mermaid', type=Path, default=ROOT / '.local/doc-tools/mermaid-10.9.3.min.js')
args = parser.parse_args()
if not args.mermaid.is_file():
    parser.error('請用 --mermaid 指定已下載的 Mermaid 瀏覽器渲染器。')
source = ROOT / 'docs/sso-integration-guide.md'
assets = ROOT / 'docs/assets/sso-integration-guide'
assets.mkdir(parents=True, exist_ok=True)
markdown = source.read_text(encoding='utf-8')
diagrams = re.findall(r'```mermaid\r?\n(.*?)```', markdown, re.S)
with sync_playwright() as p:
    chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
    browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
    page = browser.new_page(viewport={'width': 1280, 'height': 1000}, device_scale_factor=1)
    page.set_content('<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8"></head><body></body></html>')
    page.add_script_tag(path=str(args.mermaid.resolve()))
    page.evaluate('''() => mermaid.initialize({startOnLoad:false,securityLevel:'strict',theme:'base',
        themeVariables:{fontFamily:'Microsoft JhengHei, Segoe UI, sans-serif',fontSize:'16px',
            primaryColor:'#edf3ff',primaryTextColor:'#162b48',primaryBorderColor:'#5478ac',
            lineColor:'#64748b',secondaryColor:'#f0faf4',tertiaryColor:'#fff7e7'},
        flowchart:{htmlLabels:false,useMaxWidth:true},
        sequence:{useMaxWidth:true,wrap:false,mirrorActors:false,actorMargin:75,messageMargin:20,
            actorFontSize:17,messageFontSize:16,noteFontSize:16}})''')
    for index, diagram in enumerate(diagrams, 1):
        svg = page.evaluate('async ({code,id}) => (await mermaid.render(id, code)).svg',
                            {'code': diagram, 'id': f'integration{index}'})
        (assets / f'diagram-{index:02}.svg').write_text(svg, encoding='utf-8')
    md = MarkdownIt('commonmark', {'html': False}).enable('table')
    tokens = md.parse(markdown)
    toc = []
    for index, token in enumerate(tokens):
        if token.type == 'heading_open' and token.tag == 'h2':
            anchor = f'section-{len(toc) + 1}'
            token.attrSet('id', anchor)
            toc.append((anchor, tokens[index + 1].content))
    count = 0
    def fence(tokens, index, options, env):
        global count
        token = tokens[index]
        if token.info.strip() == 'mermaid':
            count += 1
            kind = 'sequence' if token.content.lstrip().startswith('sequenceDiagram') else 'flow'
            return f'<figure class="{kind}"><img src="assets/sso-integration-guide/diagram-{count:02}.svg" alt="流程圖 {count}"><figcaption>圖 {count}</figcaption></figure>\n'
        return '<pre><code>' + html.escape(token.content) + '</code></pre>\n'
    md.renderer.rules['fence'] = fence
    body = md.renderer.render(tokens, md.options, {})
    navigation = '<nav aria-label="文件目錄"><strong>文件目錄</strong><ul>' + ''.join(
        f'<li><a href="#{anchor}">{html.escape(title)}</a></li>' for anchor, title in toc) + '</ul></nav>'
    body = body.replace('<h2', navigation + '<h2', 1)
    result = '<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
    result += '<title>NKUST SSO 系統串接流程書</title><link rel="stylesheet" href="assets/sso-integration-guide/guide.css"></head><body>'
    result += '<div class="document"><main>' + body + '</main></div></body></html>'
    target = source.with_suffix('.html')
    target.write_text(result, encoding='utf-8')
    # 輸出使用外部本機圖片與樣式，不含任何執行中的 Mermaid 或 CDN 相依性。
    page.goto(target.as_uri())
    page.wait_for_load_state('networkidle')
    page.evaluate('document.fonts.ready')
    assert page.locator('figure img').count() == len(diagrams)
    assert page.locator('img').evaluate_all('(images) => images.every(i => i.complete && i.naturalWidth > 0)')
    assert page.locator('script').count() == 0
    page.pdf(path=str(source.with_suffix('.pdf')), format='A4', print_background=True, prefer_css_page_size=True,
             display_header_footer=True, header_template='<span></span>',
             footer_template='<div style="font-size:9px;width:100%;text-align:center;color:#64748b"><span class="pageNumber"></span> / <span class="totalPages"></span></div>')
    preview = ROOT / '.local/doc-tools/guide-preview.png'
    page.screenshot(path=str(preview))
    page.set_viewport_size({'width':390, 'height':844})
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth'), '行動版出現水平溢出'
    browser.close()
print(f'Exported {len(diagrams)} diagrams, HTML and PDF: {source.parent}')
