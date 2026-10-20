"""Focused accessibility regressions; does not replace assistive technology review."""
from playwright.sync_api import expect


def check_accessibility(page, expanded=False):
    page.reload()
    if expanded:
        for summary in page.locator('details > summary').all():
            summary.click()
        page.evaluate("document.body.tabIndex = -1; document.body.focus(); document.body.removeAttribute('tabindex')")
    page.keyboard.press('Tab')
    skip = page.get_by_role('link', name='跳至主要內容', exact=True)
    expect(skip).to_be_focused()
    expect(skip).to_be_in_viewport()
    page.keyboard.press('Enter')
    expect(page.locator('main')).to_be_focused()
    page.keyboard.press('Tab')
    assert page.evaluate("getComputedStyle(document.activeElement).outlineStyle === 'solid'")
    assert page.locator('h1').count() == 1
    assert page.locator('html').get_attribute('lang') == 'zh-Hant'
    # 實際渲染文字和祖先背景計算對比，不僅驗證 CSS 裡的色碼。
    failures = page.evaluate("""() => {
        const rgb = value => (value.match(/[\\d.]+/g) || []).map(Number);
        const luminance = c => c.slice(0, 3).map(v => {
            v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
        }).reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
        const results = [];
        for (const el of document.querySelectorAll('header *, main *, footer *')) {
            if (!el.checkVisibility() || el.closest('.sr-only') ||
                ![...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) continue;
            const style = getComputedStyle(el);
            let background = [255, 255, 255];
            for (let parent = el; parent; parent = parent.parentElement) {
                const c = rgb(getComputedStyle(parent).backgroundColor);
                if (c.length === 3 || c[3] === 1) { background = c; break; }
            }
            const foreground = luminance(rgb(style.color)), back = luminance(background);
            const ratio = (Math.max(foreground, back) + .05) / (Math.min(foreground, back) + .05);
            const large = parseFloat(style.fontSize) >= 24 ||
                (parseFloat(style.fontSize) >= 18.66 && parseInt(style.fontWeight) >= 700);
            if (ratio + .01 < (large ? 3 : 4.5))
                results.push({text: el.textContent.trim().slice(0, 40), color: style.color, ratio});
        }
        return results;
    }""")
    assert not failures, failures
    for target in page.locator('.directory-filters button, .pwd-toggle, .gateway-area, .gateway-alumni').all():
        box = target.bounding_box()
        assert box and box['width'] >= 44 and box['height'] >= 44, box
    if page.locator('.directory').count():
        expect(page.get_by_label('搜尋服務', exact=True)).to_be_visible()
        assert page.locator('section[aria-labelledby]').count() > 0
        button = page.locator('[data-directory-filter]').nth(1)
        button.focus()
        page.keyboard.press('Space')
        expect(button).to_have_attribute('aria-pressed', 'true')
        expect(page.locator('.directory-group:visible')).to_have_count(1)
        page.locator('[data-directory-filter="all"]').click()
    page.emulate_media(forced_colors='active', reduced_motion='reduce')
    page.locator('main').focus()
    page.keyboard.press('Tab')
    assert page.evaluate("getComputedStyle(document.activeElement).outlineStyle === 'solid'")
    assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
    page.emulate_media(forced_colors='none', reduced_motion='no-preference')
    page.locator('main').focus()
    print('PASS keyboard navigation, text contrast, headings, control targets and forced colors:', page.url)
