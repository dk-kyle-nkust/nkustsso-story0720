"use strict";
(function () {
    const search = document.getElementById("directory-search");
    if (!search) return;
    const groups = [...document.querySelectorAll("[data-directory-category]")];
    const filters = [...document.querySelectorAll("[data-directory-filter]")];
    const status = document.getElementById("directory-status");
    const empty = document.getElementById("directory-empty");
    const directory = document.querySelector('.directory');
    let activeDetail = null;
    function closeDetail() {
        if (!activeDetail) return;
        activeDetail.tip.hidden = true;
        activeDetail.button.setAttribute('aria-expanded', 'false');
        activeDetail.pinned = false;
        activeDetail = null;
    }
    function openDetail(detail) {
        if (activeDetail !== detail) closeDetail();
        activeDetail = detail;
        detail.tip.hidden = false;
        detail.button.setAttribute('aria-expanded', 'true');
    }
    // 說明按鈕不包在服務連結內，觸控查看詳述時不會誤開服務。
    document.querySelectorAll('[data-service-info]').forEach(button => {
        const detail = { button, card: button.closest('li'), tip: document.getElementById(button.getAttribute('aria-controls')), hovered: false, pinned: false };
        detail.card.addEventListener('pointerenter', event => {
            if (event.pointerType !== 'mouse') return;
            detail.hovered = true;
            openDetail(detail);
        });
        detail.card.addEventListener('pointerleave', () => {
            detail.hovered = false;
            if (activeDetail === detail && !detail.pinned && !detail.card.contains(document.activeElement)) closeDetail();
        });
        detail.card.addEventListener('focusin', () => openDetail(detail));
        detail.card.addEventListener('focusout', event => {
            if (activeDetail === detail && !detail.hovered && !detail.card.contains(event.relatedTarget)) closeDetail();
        });
        button.addEventListener('click', () => {
            if (activeDetail === detail && detail.pinned) closeDetail();
            else { openDetail(detail); detail.pinned = true; }
        });
    });
    directory.setAttribute('data-details-ready', '');
    document.addEventListener('keydown', event => { if (event.key === 'Escape') closeDetail(); });
    document.addEventListener('pointerdown', event => {
        if (activeDetail && !activeDetail.card.contains(event.target)) closeDetail();
    });
    let selected = "all";
    function update() {
        closeDetail();
        const term = search.value.trim().toLocaleLowerCase();
        let count = 0;
        for (const group of groups) {
            let visible = 0;
            for (const item of group.querySelectorAll("[data-directory-search]")) {
                item.hidden = (selected !== "all" && group.dataset.directoryCategory !== selected) ||
                    !(item.dataset.directorySearch + " " + item.querySelector(".directory-service-name").textContent.toLocaleLowerCase()).includes(term);
                if (!item.hidden) visible++;
            }
            group.hidden = visible === 0;
            count += visible;
        }
        status.textContent = window.PortalLanguage?.language === "en" ? `${count} ${count === 1 ? "service" : "services"}` : `共 ${count} 項服務`;
        empty.hidden = count !== 0;
        filters.forEach(button => button.setAttribute("aria-pressed", String(button.dataset.directoryFilter === selected)));
    }
    filters.forEach(button => button.addEventListener("click", () => {
        selected = button.dataset.directoryFilter;
        update();
    }));
    search.addEventListener("input", update);
    search.addEventListener("keydown", event => {
        if (event.key === "Escape") { search.value = ""; update(); }
    });
    document.addEventListener("portal-language-change", update);
    update();
})();
