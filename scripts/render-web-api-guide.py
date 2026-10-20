"""以本機 Mermaid 與 Chrome 產生可分享的 Web API SSO 流程圖，不連線 SSO 或資料庫。"""
import os
from pathlib import Path
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[1]
assets = root / 'NkustSso.Server/wwwroot/guides'
mermaid = root / '.local/doc-tools/mermaid-10.9.3.min.js'
with sync_playwright() as p:
    chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
    browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
    page = browser.new_page(viewport={'width': 1200, 'height': 1000}, device_scale_factor=2)
    page.set_content('<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><body></body></html>')
    page.add_script_tag(path=str(mermaid))
    page.evaluate("""() => mermaid.initialize({startOnLoad:false,securityLevel:'strict',theme:'base',
        themeVariables:{fontFamily:'Microsoft JhengHei, Segoe UI, sans-serif',fontSize:'16px',
            primaryColor:'#f0edfc',primaryTextColor:'#28334b',primaryBorderColor:'#8e83bc',
            lineColor:'#66738b',secondaryColor:'#eff8f4',tertiaryColor:'#fafbfe'},
        sequence:{useMaxWidth:true,wrap:false,mirrorActors:false,actorMargin:70,messageMargin:22}})""")
    svg = page.evaluate("async code => (await mermaid.render('webApiSsoFlow', code)).svg", (assets / 'web-api-sso.mmd').read_text(encoding='utf-8'))
    (assets / 'web-api-sso.svg').write_text(svg, encoding='utf-8')
    page.set_content('<!doctype html><html><body>' + svg + '</body></html>')
    page.evaluate('document.fonts.ready')
    page.locator('svg').screenshot(path=str(assets / 'web-api-sso.png'))
    browser.close()
print('Exported Web API SSO flow: SVG + PNG')
