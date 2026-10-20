"use strict";
(function () {
    var root = document.getElementById("audience-editor");
    if (!root) return;
    var catalog = JSON.parse(root.dataset.catalog);
    var field = document.getElementById("AudienceJson");
    var host = document.getElementById("audience-groups");
    var error = document.getElementById("audience-error");
    var families = { any: "全部身分類別", student: "學生", employee: "教職員", other: "其他身分" };
    var sequence = 0;
    var initial;
    try {
        initial = JSON.parse(field.value);
        if (!initial || initial.version !== 2 || !Array.isArray(initial.groups) || initial.groups.length > 16 ||
            initial.groups.some(function (g) { return !g || !families[g.family] || !Array.isArray(g.conditions) || g.conditions.length > 16 ||
                g.conditions.some(function (c) { return !c || typeof c.dimension !== "string" || !Array.isArray(c.values) ||
                    c.values.some(function (v) { return typeof v !== "string"; }); }); })) throw new Error("Invalid rules");
    } catch (_) { initial = { mode: "invalid", groups: [] }; }
    var invalid = initial.mode !== "all" && initial.mode !== "rules";
    var groups = Array.isArray(initial.groups) ? initial.groups : [];
    root.querySelector('[name="audience-mode"][value="' + (initial.mode === "all" ? "all" : "rules") + '"]').checked = true;

    function el(tag, cls, text) {
        var node = document.createElement(tag);
        if (cls) node.className = cls;
        if (text !== undefined) node.textContent = text;
        return node;
    }
    function button(text, action, cls) {
        var node = el("button", cls || "", text);
        node.type = "button";
        node.addEventListener("click", action);
        return node;
    }
    function enhance(select) {
        if (!window.TomSelect) return;
        var settings = { create: false, allowEmptyOption: true,
            plugins: select.multiple ? ["remove_button"] : [], lockOptgroupOrder: true };
        // 要搜尋時保留套件預設輸入框；明確傳 undefined 會覆蓋預設值而讓輸入框消失。
        if (select.options.length <= 8 && !select.multiple) settings.controlInput = null;
        new window.TomSelect(select, settings);
    }
    function dimension(key) { return catalog.dimensions.find(function (d) { return d.key === key; }); }
    function optionLabel(key, code) {
        var option = catalog.options.concat(catalog.compatibilityOptions || []).find(function (o) { return o.dimension === key && o.code === code; });
        return option ? option.label + (option.enabled ? "" : "（已停用）") : code + "（未登記）";
    }
    function readGroup(card) {
        var conditions = [];
        card.querySelectorAll(".audience-condition").forEach(function (part) {
            if (part.querySelector(".condition-mode").value !== "specified") return;
            var multi = part.querySelector("select[multiple]");
            var values = multi ? Array.from(multi.selectedOptions).map(function (o) { return o.value; }) :
                Array.from(part.querySelectorAll('input[type="checkbox"]:checked')).map(function (input) { return input.value; });
            conditions.push({ dimension: part.dataset.dimension, values: values });
        });
        return { family: card.dataset.family, conditions: conditions };
    }
    function capture() { groups = Array.from(host.children).map(readGroup); }
    function describe(group) {
        return (families[group.family] || "未知身分") + (group.conditions.length ? " · " + group.conditions.map(function (c) {
            var d = dimension(c.dimension);
            return (d ? d.name : c.dimension) + "：" + (c.values.length ? c.values.map(function (v) { return optionLabel(c.dimension, v); }).join("、") : "尚未選擇");
        }).join("；") : "（不限其他條件）");
    }
    function update() {
        capture();
        var mode = root.querySelector('[name="audience-mode"]:checked').value;
        document.getElementById("audience-rules").hidden = mode === "all";
        field.value = JSON.stringify({ version: 2, mode: mode, groups: mode === "all" ? [] : groups });
        var problem = mode === "rules" && !groups.length ? "請至少新增一組身分，或明確選擇全部身分。" : "";
        if (mode === "rules" && groups.some(function (g) { return g.conditions.some(function (c) { return !c.values.length; }); }))
            problem = "指定條件至少要勾選一項；若不限制，請改選「全部」。";
        if (invalid) problem = "原有規則格式異常，請重新設定開放範圍。";
        error.textContent = problem;
        error.hidden = !problem;
        var summary = document.getElementById("audience-summary");
        summary.replaceChildren();
        (mode === "all" ? ["全部已登入使用者"] : groups.map(describe)).forEach(function (text, index) {
            summary.append(el("li", "", (index ? "或 " : "") + text));
        });
        Array.from(host.children).forEach(function (card, index) {
            card.querySelector("summary strong").textContent = "第 " + (index + 1) + " 組 · " + (families[card.dataset.family] || "未知身分");
            card.querySelector("summary small").textContent = describe(groups[index]);
        });
        return !problem;
    }
    function departmentChoices(values, available, selected) {
        var picker = el("div", "department-picker");
        var searchId = "department-search-" + (++sequence);
        var label = el("label", "", "搜尋單位名稱或代碼");
        label.htmlFor = searchId;
        var search = el("input", "department-picker-search");
        search.type = "search";
        search.id = searchId;
        search.placeholder = "輸入名稱或代碼，搜尋所有層級";
        var status = el("p", "fine");
        status.setAttribute("role", "status");
        var list = el("div", "department-picker-list");
        var empty = el("p", "fine", "沒有符合的單位，請調整搜尋條件。");
        empty.hidden = true;
        var byCode = new Map(available.map(function (o) { return [o.code, o]; }));
        var parents = catalog.departmentParents || {};
        var children = new Map();
        available.forEach(function (o) {
            var parent = parents[o.code];
            if (!parent || !byCode.has(parent)) return;
            if (!children.has(parent)) children.set(parent, []);
            children.get(parent).push(o);
        });
        var groups = [];
        function choice(option) {
            var row = el("label", "department-choice");
            var input = el("input");
            input.type = "checkbox";
            input.value = option.code;
            input.checked = selected.includes(option.code);
            input.disabled = !option.enabled && !input.checked;
            input.addEventListener("change", function () {
                if (!option.enabled) input.disabled = true;
                update();
                renderVisibility();
            });
            var caption = el("span", "", option.label + (option.enabled ? "" : "（已停用）"));
            caption.append(el("small", "", option.code));
            row.append(input, caption);
            return row;
        }
        function matches(option, terms) {
            var text = (option.label + " " + option.code).toLocaleLowerCase();
            return terms.every(function (term) { return text.includes(term); });
        }
        function renderVisibility() {
            var query = search.value.trim().toLocaleLowerCase();
            var terms = query.split(/\s+/).filter(Boolean);
            var visible = 0;
            groups.forEach(function (group) {
                var rootMatch = matches(group.option, terms);
                var hasChildMatch = group.children.some(function (child) { return matches(child.option, terms); });
                group.node.hidden = !!query && !rootMatch && !hasChildMatch;
                if (!group.node.hidden) visible++;
                group.childHost.hidden = query ? !rootMatch && !hasChildMatch : !group.open;
                group.children.forEach(function (child) { child.node.hidden = !!query && !rootMatch && !matches(child.option, terms); });
                group.toggle.hidden = !!query || !group.children.length;
                group.toggle.setAttribute("aria-expanded", String(group.open));
                group.toggle.setAttribute("aria-label", (group.open ? "收合 " : "展開 ") + group.option.label + " 的下層單位");
                group.toggle.textContent = (group.open ? "收合" : "展開") + "（" + group.children.length + "）";
            });
            empty.hidden = visible !== 0;
            var count = list.querySelectorAll('input[type="checkbox"]:checked').length;
            status.textContent = "已選 " + count + " 個單位 · " + (query ? "搜尋所有層級" : "預設顯示第一層，可展開下層");
        }
        available.filter(function (o) { return !parents[o.code] || !byCode.has(parents[o.code]); }).forEach(function (option) {
            var group = { option: option, open: false, children: [] };
            group.node = el("div", "department-choice-group");
            var row = el("div", "department-choice-head");
            group.childHost = el("div", "department-choice-children");
            group.childHost.id = "department-children-" + (++sequence);
            group.childHost.setAttribute("role", "group");
            group.childHost.setAttribute("aria-label", option.label + " 的下層單位");
            group.toggle = button("", function () { group.open = !group.open; renderVisibility(); }, "department-toggle");
            group.toggle.setAttribute("aria-controls", group.childHost.id);
            row.append(choice(option), group.toggle);
            (children.get(option.code) || []).forEach(function (child) {
                var node = choice(child);
                group.children.push({ option: child, node: node });
                group.childHost.append(node);
            });
            group.node.append(row, group.childHost);
            list.append(group.node);
            groups.push(group);
        });
        search.addEventListener("input", function () {
            groups.forEach(function (group) { group.open = false; });
            renderVisibility();
        });
        picker.append(label, search, status, list, empty);
        values.append(picker, el("p", "fine", "上下層各自選取；收合或清除搜尋不會取消已選單位。"));
        renderVisibility();
    }
    function addCondition(card, key, selected, specified) {
        var d = dimension(key);
        var part = el("div", "audience-condition");
        part.dataset.dimension = key;
        var head = el("div", "audience-condition-head");
        var mode = el("select", "condition-mode");
        mode.id = "audience-mode-" + (++sequence);
        mode.append(new Option("全部", "all"), new Option("指定", "specified"));
        mode.value = specified ? "specified" : "all";
        var label = el("label", "", (d ? d.name : key) + (d && !d.enabled ? "（已停用）" : ""));
        label.htmlFor = mode.id;
        head.append(label, mode);
        var values = el("div", "audience-values");
        var available = catalog.options.filter(function (o) { return o.dimension === key &&
            (key !== "identity_type" || card.dataset.family === "any" || o.family === card.dataset.family) && (o.enabled || selected.includes(o.code)); });
        selected.forEach(function (code) {
            if (!available.some(function (o) { return o.code === code; })) {
                var legacy = (catalog.compatibilityOptions || []).find(function (o) { return o.dimension === key && o.code === code; });
                available.push({ code: code, label: legacy ? legacy.label + "（既有條件）" : code + "（來源未提供）", enabled: !!legacy && legacy.enabled });
            }
        });
        if (key === "department") {
            departmentChoices(values, available, selected);
        } else if (available.length > 8) {
            var multi = el("select");
            multi.multiple = true;
            multi.setAttribute("aria-label", (d ? d.name : key) + "選項");
            available.forEach(function (o) {
                var text = o.label + (o.enabled ? "" : "（已停用）");
                var item = new Option(text, o.code, false, selected.includes(o.code));
                item.disabled = !o.enabled;
                multi.append(item);
            });
            multi.addEventListener("change", update);
            values.append(multi);
        } else {
            available.forEach(function (o) {
                var choice = el("label");
                var input = el("input");
                input.type = "checkbox";
                input.value = o.code;
                input.checked = selected.includes(o.code);
                input.disabled = !o.enabled && !input.checked;
                input.addEventListener("change", function () { if (!o.enabled) input.disabled = true; update(); });
                var caption = el("span", "", o.label + (o.enabled ? "" : "（已停用）"));
                if (o.code.startsWith("DEMO_")) caption.append(el("small", "", "示範分類"));
                choice.append(input, caption);
                values.append(choice);
            });
        }
        if (!available.length) values.append(el("p", "fine", "來源尚未提供選項，請至身分分類查詢查看串接狀態。"));
        if (key === "student_kind" && catalog.fromCampus)
            values.append(el("p", "fine", "學生紀錄對應尚待確認；缺少學生狀況資料的使用者不會符合這項條件。"));
        part.append(head, values);
        card.querySelector(".audience-conditions").append(part);
        values.hidden = mode.value === "all";
        mode.addEventListener("change", function () { values.hidden = mode.value === "all"; invalid = false; update(); });
        enhance(mode);
        if (values.querySelector("select")) enhance(values.querySelector("select"));
    }
    function render(openIndex) {
        host.querySelectorAll("select").forEach(function (s) { if (s.tomselect) s.tomselect.destroy(); });
        host.replaceChildren();
        groups.forEach(function (group, index) {
            var card = el("details", "audience-card");
            card.dataset.family = group.family;
            card.open = index === openIndex;
            var summary = el("summary");
            summary.append(el("strong"), el("small"));
            var body = el("div", "audience-card-body");
            var actions = el("div", "audience-card-actions");
            actions.append(button("複製這組", function () { capture(); if (groups.length >= 16) return; groups.splice(index + 1, 0, JSON.parse(JSON.stringify(groups[index]))); render(index + 1); }),
                button("移除這組", function () { capture(); groups.splice(index, 1); render(Math.max(0, index - 1)); }));
            body.append(actions, el("div", "audience-conditions"));
            card.append(summary, body);
            host.append(card);
            var keys = group.family === "student" ? ["student_status"] : group.family === "employee" ? ["identity_type", "employment_status"] : ["identity_type"];
            if (catalog.fromCampus) keys = ["identity_type"];
            (group.conditions || []).forEach(function (c) { if (!keys.includes(c.dimension)) keys.push(c.dimension); });
            keys.forEach(function (key) {
                var condition = group.conditions.find(function (c) { return c.dimension === key; });
                addCondition(card, key, condition ? condition.values : [], !!condition);
            });
            var add = el("div", "audience-add-condition");
            var select = el("select");
            select.id = "audience-add-" + (++sequence);
            select.append(new Option("選擇更細的條件…", ""));
            catalog.dimensions.filter(function (d) { return d.enabled && !keys.includes(d.key) &&
                (group.family === "any" || d.family === "any" || d.family === group.family); }).forEach(function (d) { select.append(new Option(d.name, d.key)); });
            var label = el("label", "", "繼續細分");
            label.htmlFor = select.id;
            add.append(label, select, button("新增限制", function () {
                if (!select.value || card.querySelectorAll(".audience-condition").length >= 16) return;
                var key = select.value;
                addCondition(card, key, [], true);
                if (select.tomselect) { select.tomselect.clear(); select.tomselect.removeOption(key); }
                else select.querySelectorAll("option").forEach(function (o) { if (o.value === key) o.remove(); });
                invalid = false;
                update();
            }, "button"));
            body.append(add);
            enhance(select);
        });
        update();
    }
    root.querySelectorAll("[data-add-family]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            capture();
            if (groups.length >= 16) return;
            invalid = false;
            groups.push({ family: btn.dataset.addFamily, conditions: [] });
            render(groups.length - 1);
        });
    });
    root.querySelectorAll('[name="audience-mode"]').forEach(function (radio) { radio.addEventListener("change", function () { invalid = false; update(); }); });
    root.closest("form").addEventListener("submit", function (event) {
        if (!update()) { event.preventDefault(); error.scrollIntoView({ block: "center" }); }
    });
    render(groups.length === 1 ? 0 : -1);
}());
