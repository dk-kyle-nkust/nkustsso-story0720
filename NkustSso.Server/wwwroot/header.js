"use strict";
(function () {
    const header = document.querySelector('.site-header');
    const toggle = header?.querySelector('.site-nav-toggle');
    const nav = document.getElementById('site-navigation');
    if (!toggle || !nav) return;
    const mobile = matchMedia('(max-width: 760px)');
    function setOpen(open) {
        nav.hidden = mobile.matches && !open;
        toggle.setAttribute('aria-expanded', String(!nav.hidden));
    }
    function resize() {
        // 收合時先移走導覽中的焦點，避免鍵盤停在隱藏項目。
        toggle.hidden = !mobile.matches;
        if (mobile.matches && nav.contains(document.activeElement)) toggle.focus();
        if (!mobile.matches && document.activeElement === toggle) nav.querySelector('a').focus();
        setOpen(false);
    }
    toggle.addEventListener('click', () => setOpen(nav.hidden));
    header.addEventListener('keydown', event => {
        if (event.key === 'Escape' && mobile.matches && !nav.hidden) {
            setOpen(false);
            toggle.focus();
        }
    });
    document.addEventListener('pointerdown', event => {
        if (mobile.matches && !header.contains(event.target)) setOpen(false);
    });
    mobile.addEventListener('change', resize);
    resize();
})();

// 切回入口時更新目前頁面的帳號區塊，不重載服務清單，以保留搜尋與分類。
(function () {
    const panel = document.querySelector('[data-current-user]');
    if (!panel) return;
    const account = panel.querySelector('[data-current-user-account], [data-current-user-name]');
    const displayName = panel.querySelector('[data-current-user-display-name]');
    const accountLabel = panel.querySelector('[data-current-user-account-label]');
    const identity = panel.querySelector('[data-current-user-identity]');
    // 單位只有教職員才有值；是選填元素，舊分頁沒有時照常運作。
    const unitRow = panel.querySelector('[data-current-user-unit-row]');
    const unit = panel.querySelector('[data-current-user-unit]');
    // 舊分頁載入新版腳本時，同步移除已取消的個人資訊。
    panel.querySelectorAll('[data-current-user-class-row], [data-current-user-fields], [data-current-user-notice]').forEach(el => el.remove());
    // 管理入口已移到專區導覽，不再位於使用者面板內。
    const manageLink = document.querySelector('[data-manage-systems]');
    const loginLink = document.querySelector('[data-account-login], [data-directory-login]');
    const form = document.querySelector('[data-header-logout]');
    const token = form?.querySelector('input[type="hidden"]');
    // 舊分頁或開發時的局部更新可能仍使用先前的 HTML。
    // 核心欄位不完整時保留原生操作。
    if (!account || !identity || !token) return;
    let pending = false;
    function translateIdentity() {
        const label = identity.dataset.identityLabel || '';
        identity.textContent = window.PortalLanguage?.text(label) ?? label;
        if (accountLabel) {
            const accountText = accountLabel.dataset.accountLabel || '';
            accountLabel.textContent = window.PortalLanguage?.text(accountText) ?? accountText;
        }
        if (loginLink) loginLink.hidden = !panel.hidden;
        form.hidden = panel.hidden;
    }
    function clear() {
        panel.hidden = true;
        account.textContent = '';
        if (displayName) { displayName.textContent = ''; displayName.hidden = true; }
        if (unit) unit.textContent = '';
        if (unitRow) unitRow.hidden = true;
        if (accountLabel) accountLabel.dataset.accountLabel = '';
        identity.dataset.identityLabel = '';
        if (manageLink) manageLink.hidden = true;
        token.value = '';
        translateIdentity();
    }
    async function refresh() {
        if (document.hidden || pending) return;
        pending = true;
        try {
            const response = await fetch('/account/current-user', {
                credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
            });
            if (!response.ok) { clear(); return; }
            const user = await response.json();
            if (!user.signedIn) { clear(); return; }
            account.textContent = user.userName || '';
            if (displayName) {
                displayName.textContent = user.displayName || '';
                displayName.hidden = !user.displayName?.trim();
            }
            if (unit) unit.textContent = user.unitName || '';
            if (unitRow) unitRow.hidden = !user.unitName?.trim();
            if (accountLabel) accountLabel.dataset.accountLabel = user.accountLabel || '帳號';
            identity.dataset.identityLabel = user.identityLabel;
            if (manageLink) manageLink.hidden = user.canManageSystems !== true;
            token.name = user.tokenField;
            token.value = user.logoutToken;
            panel.hidden = false;
            translateIdentity();
        } catch { clear(); }
        finally { pending = false; }
    }
    document.addEventListener('portal-language-change', translateIdentity);
    window.addEventListener('focus', refresh);
    window.addEventListener('pageshow', event => { if (event.persisted) refresh(); });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
    translateIdentity();
})();
