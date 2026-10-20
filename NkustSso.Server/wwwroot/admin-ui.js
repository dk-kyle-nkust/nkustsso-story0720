"use strict";
// 管理頁的表格與下拉強化。DataTables 負責排序、分頁與表格內搜尋，Tom Select 負責下拉。
// 兩者都自 /lib 本機載入：CSP 是 default-src 'self'，外部 CDN 會被擋。
(function () {
    var $ = window.jQuery;
    if ($ && $.fn && $.fn.DataTable) {
        var language = {
            search: "表格內搜尋：",
            searchPlaceholder: "輸入關鍵字",
            lengthMenu: "每頁 _MENU_ 筆",
            info: "第 _START_–_END_ 筆，共 _TOTAL_ 筆",
            infoEmpty: "沒有資料",
            infoFiltered: "（自 _MAX_ 筆中篩選）",
            zeroRecords: "沒有符合的資料",
            emptyTable: "目前沒有資料",
            paginate: { first: "第一頁", previous: "上一頁", next: "下一頁", last: "最後一頁" }
        };
        $("table[data-datatable]").each(function () {
            var table = $(this);
            var options = {
                language: language,
                pageLength: Number(table.attr("data-page-length")) || 25,
                lengthMenu: [10, 25, 50, 100],
                order: [],
                drawCallback: function () {
                    var api = this.api();
                    var info = api.page.info();
                    var container = api.table().container();
                    container.classList.toggle("dt-single-page", info.pages <= 1);
                    container.classList.toggle("dt-no-data", info.recordsTotal === 0);
                }
            };
            // 短的參考表全部放得下，data-paging="false" 可省掉分頁與每頁筆數的雜訊。
            if (table.attr("data-paging") === "false") {
                options.paging = false;
                options.info = false;
            }
            // data-no-sort 填不該排序的欄位序號（0 起算），例如操作欄。
            var fixed = (table.attr("data-no-sort") || "").split(",")
                .filter(function (value) { return value !== ""; })
                .map(Number);
            if (fixed.length !== 0) { options.columnDefs = [{ orderable: false, targets: fixed }]; }
            table.DataTable(options);
        });
    }

    if (window.TomSelect) {
        Array.prototype.forEach.call(document.querySelectorAll("select[data-enhance]"), function (select) {
            var options = {
                create: false,
                allowEmptyOption: true,
                plugins: select.multiple ? ["remove_button"] : []
            };
            // 只對選項少的單選關閉搜尋；傳入 undefined 也會覆蓋元件預設的輸入框。
            if (!select.multiple && select.options.length <= 8) { options.controlInput = null; }
            new window.TomSelect(select, options);
        });
    }
}());
