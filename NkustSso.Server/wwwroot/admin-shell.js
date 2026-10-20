"use strict";
// 手機使用可收合的頁內導覽；未載入 JavaScript 時仍可看到所有連結。
(function () {
    const button = document.querySelector('.workspace-menu');
    const navigation = document.getElementById('workspace-navigation');
    if (!button || !navigation) return;
    const mobile = window.matchMedia('(max-width: 1000px)');
    function setOpen(open) {
        button.setAttribute('aria-expanded', String(open));
        navigation.hidden = mobile.matches && !open;
    }
    function resize() {
        button.hidden = !mobile.matches;
        setOpen(false);
    }
    button.addEventListener('click', () => setOpen(button.getAttribute('aria-expanded') !== 'true'));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && mobile.matches && button.getAttribute('aria-expanded') === 'true') {
            setOpen(false);
            button.focus();
        }
    });
    mobile.addEventListener('change', resize);
    resize();
}());
