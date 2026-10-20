"use strict";
// 依「登入方式」顯示適用的設定。每個區塊以 data-protocol 宣告適用範圍，
// 新增協定時只需改 Razor，不必再動這支程式。
(function () {
    var protocol = document.querySelector('[name="System.Protocol"]');
    var sections = document.querySelectorAll("[data-protocol]");
    if (!protocol || sections.length === 0) return;

    // 巢狀區塊要同時符合自己與所有上層的條件，否則上層隱藏時內層仍會被啟用。
    function applies(element, value) {
        for (var node = element; node; node = node.parentElement && node.parentElement.closest("[data-protocol]")) {
            if (node.getAttribute("data-protocol").split(" ").indexOf(value) === -1) return false;
        }
        return true;
    }

    function update() {
        Array.prototype.forEach.call(sections, function (element) {
            var ok = applies(element, protocol.value);
            element.hidden = !ok;
            // 只停用文字欄位：核取方塊與 ASP.NET 的 false 伴隨欄位要保留，
            // 既有設定才能原值往返，不會因為隱藏而被清掉。
            var fields = element.querySelectorAll('input:not([type="checkbox"]):not([type="hidden"]), textarea');
            Array.prototype.forEach.call(fields, function (field) { field.disabled = !ok; });
        });
    }

    // 左側導覽的編號依目前顯示的項目重排，不讓隱藏區塊留下跳號。
    function renumber() {
        var links = document.querySelectorAll(".system-editor-rail nav a > span");
        var index = 0;
        Array.prototype.forEach.call(links, function (slot) {
            var link = slot.parentElement;
            if (link.hidden) { slot.textContent = ""; return; }
            index += 1;
            slot.textContent = index < 10 ? "0" + index : String(index);
        });
    }

    protocol.addEventListener("change", function () { update(); renumber(); });
    update();
    renumber();
}());
