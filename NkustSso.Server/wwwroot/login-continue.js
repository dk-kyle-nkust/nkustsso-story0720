"use strict";
// 帳密表單已在本站完成；新的導覽可接續跨站協定回呼，不放寬 form-action。
(function () {
    const link = document.getElementById("login-continue");
    if (link && new URL(link.href).origin === window.location.origin) window.location.replace(link.href);
})();
