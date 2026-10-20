"use strict";
(function () {
    var table = document.querySelector(".department-catalog");
    if (!table || !window.jQuery || !window.jQuery.fn.dataTable.isDataTable(table)) return;
    var api = window.jQuery(table).DataTable();
    var expanded = new Set();
    var rows = api.rows().nodes().toArray();
    var buttons = rows.flatMap(function (row) { return Array.from(row.querySelectorAll("[data-expand-unit]")); });
    var previousSearch = "";
    function searching() { return api.search().trim().length > 0; }
    function syncSearch() {
        var query = api.search();
        if (query !== previousSearch) expanded.clear();
        previousSearch = query;
    }
    // 交由 DataTables 篩選後再分頁，避免僅用 CSS 隱藏導致空白頁或錯誤筆數。
    api.search.fixed("department-level", function (_, data, index) {
        syncSearch();
        var parent = api.row(index).node().dataset.parentCode;
        return searching() || !parent || expanded.has(parent);
    });
    // 無結果時不會呼叫逐列篩選，仍須清除先前的展開狀態。
    api.on("search", syncSearch);
    function refreshButtons() {
        buttons.forEach(function (button) {
            var open = expanded.has(button.dataset.expandUnit);
            button.hidden = searching();
            button.setAttribute("aria-expanded", String(open));
            button.textContent = (open ? "收合下層" : "展開下層") + "（" + button.dataset.childCount + "）";
            var name = button.closest("tr").querySelector("td > span").textContent;
            button.setAttribute("aria-label", (open ? "收合 " : "展開 ") + name + " 的下層單位");
        });
    }
    buttons.forEach(function (button) {
        button.addEventListener("click", function () {
            var code = button.dataset.expandUnit;
            if (expanded.has(code)) expanded.delete(code);
            else expanded.add(code);
            api.draw(false);
            button.focus({ preventScroll: true });
        });
    });
    api.on("draw", refreshButtons);
    api.draw();
}());
