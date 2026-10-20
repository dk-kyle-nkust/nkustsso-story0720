"use strict";
// 使用外部腳本，讓密碼顯示功能符合認證頁的 CSP。
(function () {
    // 完整頁面重載後主動帶到錯誤訊息，方便鍵盤及讀屏使用者繼續操作。
    var validation = document.querySelector('.login-form .validation');
    if (validation) validation.focus();
    var toggle = document.getElementById("toggle-pwd");
    var password = document.getElementById("Password");
    if (!toggle || !password) return;
    function updateLabel() {
        const label = password.type === "text" ? "隱藏密碼" : "顯示密碼";
        toggle.setAttribute("aria-label", window.PortalLanguage?.text(label) ?? label);
    }
    document.addEventListener("portal-language-change", updateLabel);
    toggle.addEventListener("click", function () {
        var visible = password.type !== "text";
        password.type = visible ? "text" : "password";
        toggle.classList.toggle("is-showing", visible);
        toggle.setAttribute("aria-pressed", String(visible));
        updateLabel();
        password.focus();
    });
})();
