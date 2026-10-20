"use strict";
// 角色編輯頁內查詢並加入校務人員。
// 原本這段是另開一頁，離開編輯頁就會丟掉成員欄上還沒儲存的變更；改成在本頁
// 以 fetch 查詢與建立帳號，人直接進成員欄，最後按一次「儲存角色」寫入。
(function () {
    var panel = document.querySelector("[data-campus-add]");
    var select = document.querySelector("select[name=Members]");
    if (!panel || !select) {
        return;
    }

    var input = panel.querySelector("[data-campus-input]");
    var status = panel.querySelector("[data-campus-status]");
    var candidate = panel.querySelector("[data-campus-candidate]");
    var summary = panel.querySelector("[data-campus-summary]");
    var findButton = panel.querySelector("[data-campus-find]");
    var addButton = panel.querySelector("[data-campus-add-member]");
    var token = document.querySelector("input[name=__RequestVerificationToken]");
    var confirmedAccountName = "";

    function show(text) {
        status.textContent = text;
    }

    function clearCandidate() {
        candidate.hidden = true;
        summary.textContent = "";
        confirmedAccountName = "";
    }

    function busy(waiting) {
        findButton.disabled = waiting;
        addButton.disabled = waiting;
    }

    // 後端對可預期的錯誤回 ok:false 與訊息，非預期的才給非 2xx。
    function post(url, accountName) {
        var body = new FormData();
        body.append("accountName", accountName);
        if (token) {
            body.append("__RequestVerificationToken", token.value);
        }

        return fetch(url, { method: "POST", body: body, credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error(String(response.status));
                }
                return response.json();
            });
    }

    function selected() {
        var enhanced = select.tomselect;
        if (enhanced) {
            return enhanced.items.slice();
        }

        return Array.prototype.filter.call(select.options, function (option) {
            return option.selected;
        }).map(function (option) {
            return option.value;
        });
    }

    // Tom Select 尚未初始化時直接補 option，表單照樣送得出去。
    function addMember(accountId, label) {
        var enhanced = select.tomselect;
        if (enhanced) {
            enhanced.addOption({ value: accountId, text: label });
            enhanced.addItem(accountId);
            return;
        }

        var option = select.querySelector('option[value="' + accountId + '"]');
        if (!option) {
            option = document.createElement("option");
            option.value = accountId;
            option.textContent = label;
            select.appendChild(option);
        }
        option.selected = true;
    }

    // 這個欄位在角色表單裡面，不攔 Enter 的話會直接送出表單變成「儲存角色」。
    input.addEventListener("keydown", function (event) {
        if (event.key !== "Enter") {
            return;
        }

        event.preventDefault();
        findButton.click();
    });

    findButton.addEventListener("click", function () {
        var accountName = (input.value || "").trim();
        clearCandidate();
        if (accountName === "") {
            show("請先輸入完整校務帳號。");
            return;
        }

        busy(true);
        show("查詢中…");
        post(panel.getAttribute("data-find-url"), accountName)
            .then(function (result) {
                if (!result.ok) {
                    show(result.message);
                    return;
                }

                confirmedAccountName = result.accountName;
                summary.textContent = result.accountName + "　" + result.displayName + "　" + result.statusName;
                candidate.hidden = false;
                show("");
            })
            .catch(function () {
                show("查詢失敗，請稍後重試。");
            })
            .then(function () {
                busy(false);
            });
    });

    addButton.addEventListener("click", function () {
        if (confirmedAccountName === "") {
            return;
        }

        busy(true);
        show("加入中…");
        post(panel.getAttribute("data-provision-url"), confirmedAccountName)
            .then(function (result) {
                if (!result.ok) {
                    show(result.message);
                    return;
                }

                if (selected().indexOf(result.accountId) !== -1) {
                    show(result.label + " 已經在成員欄裡。");
                } else {
                    addMember(result.accountId, result.label);
                    show(result.label + " 已放進成員欄，請按「儲存角色」完成。");
                }

                input.value = "";
                clearCandidate();
            })
            .catch(function () {
                show("加入失敗，請稍後重試。");
            })
            .then(function () {
                busy(false);
            });
    });
}());
