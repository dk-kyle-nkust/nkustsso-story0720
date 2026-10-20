"use strict";
(function () {
    var form = document.getElementById("json-test");
    var secret = document.getElementById("json-secret");
    var password = document.getElementById("json-password");
    window.addEventListener("pageshow", function () { secret.value = ""; password.value = ""; });
    if (form.dataset.enabled !== "true") return;
    var button = form.querySelector("button[type=submit]");
    button.disabled = false;
    form.addEventListener("submit", async function (event) {
        event.preventDefault();
        if (button.disabled || !form.reportValidity()) return;
        var payload = JSON.stringify({ userName: document.getElementById("json-user").value, password: password.value });
        var headers = { "Content-Type": "application/json", "X-Client-Id": document.getElementById("json-client").value, "X-Client-Secret": secret.value };
        secret.value = "";
        password.value = "";
        button.disabled = true;
        var status = document.getElementById("test-status");
        var output = document.getElementById("test-response");
        document.getElementById("test-result").hidden = false;
        status.textContent = "正在送出 API 請求…";
        output.textContent = "";
        var controller = new AbortController();
        var timeout = setTimeout(function () { controller.abort(); }, 30000);
        var start = performance.now();
        try {
            var response = await fetch("/api/integration/v1/verify", { method: "POST", headers: headers, body: payload,
                credentials: "omit", cache: "no-store", signal: controller.signal });
            payload = "";
            headers["X-Client-Secret"] = "";
            var text = await response.text();
            var result = null;
            try { result = JSON.parse(text); } catch (_) { /* 非 JSON 回應仍顯示 HTTP 狀態。 */ }
            output.textContent = text.length <= 65536 ? result !== null ? JSON.stringify(result, null, 2) : text : "回應過長，未顯示內容。";
            var message = { 400: "請求格式錯誤", 403: "系統密鑰、來源 IP 或服務授權不符", 429: "呼叫過於頻繁，請稍後再試", 503: "服務尚未開啟或帳號來源不可用" }[response.status] || "請查看回應內容";
            if (response.status === 200) message = result?.verified === true ? "驗證通過" : result?.verified === false ? "驗證未通過" : "回應格式需確認";
            status.textContent = "HTTP " + response.status + " · " + Math.round(performance.now() - start) + " ms · " + message;
        } catch (error) {
            status.textContent = error.name === "AbortError" ? "請求逾時，請確認服務狀態。" : "無法連線，請確認網路及服務狀態。";
        } finally {
            payload = "";
            headers["X-Client-Secret"] = "";
            clearTimeout(timeout);
            button.disabled = false;
        }
    });
}());
