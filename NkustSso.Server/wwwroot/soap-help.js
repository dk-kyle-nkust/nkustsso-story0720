"use strict";
(function () {
    var search = document.getElementById("method-search");
    var links = Array.from(document.querySelectorAll("[data-method]"));
    search.addEventListener("input", function () {
        var query = search.value.trim().toLowerCase();
        var count = 0;
        links.forEach(function (link) {
            link.hidden = !link.dataset.search.toLowerCase().includes(query);
            if (!link.hidden) count++;
        });
        document.getElementById("method-count").textContent = count + " 個符合的方法";
    });
    var form = document.getElementById("soap-test");
    if (!form) return;
    var button = form.querySelector("button[type=submit]");
    var password = document.getElementById("test-password");
    window.addEventListener("pageshow", function () { password.value = ""; });
    if (form.dataset.enabled !== "true") return;
    button.disabled = false;
    form.addEventListener("submit", async function (event) {
        event.preventDefault();
        if (button.disabled || !form.reportValidity()) return;
        var soap12 = document.getElementById("test-version").value === "1.2";
        var env = soap12 ? "http://www.w3.org/2003/05/soap-envelope" : "http://schemas.xmlsoap.org/soap/envelope/";
        var ns = "http://tempuri.org/";
        var action = ns + "KuasSSO_Certify2";
        var xml = document.implementation.createDocument(env, "soap:Envelope");
        var body = xml.createElementNS(env, "soap:Body");
        var operation = xml.createElementNS(ns, "KuasSSO_Certify2");
        var user = xml.createElementNS(ns, "usr_id");
        user.textContent = document.getElementById("test-user").value;
        var credential = xml.createElementNS(ns, "usr_pwd");
        credential.textContent = password.value;
        operation.append(user, credential);
        body.append(operation);
        xml.documentElement.append(body);
        var payload = new XMLSerializer().serializeToString(xml);
        password.value = "";
        credential.textContent = "";
        button.disabled = true;
        var status = document.getElementById("test-status");
        var output = document.getElementById("test-response");
        document.getElementById("test-result").hidden = false;
        status.textContent = "正在送出 SOAP 請求…";
        output.textContent = "";
        var controller = new AbortController();
        var timeout = setTimeout(function () { controller.abort(); }, 30000);
        var start = performance.now();
        try {
            var headers = soap12 ? { "Content-Type": "application/soap+xml; charset=utf-8; action=\"" + action + "\"" }
                : { "Content-Type": "text/xml; charset=utf-8", "SOAPAction": "\"" + action + "\"" };
            var response = await fetch("/KuasSSOWebService.asmx", { method: "POST", headers: headers,
                body: payload, credentials: "omit", cache: "no-store", signal: controller.signal });
            payload = "";
            var text = await response.text();
            output.textContent = text.length <= 65536 ? text : "回應過長，未顯示內容。";
            var message = { 403: "來源 IP 或服務授權不符", 429: "呼叫過於頻繁，請稍後再試", 503: "服務尚未開啟", 500: "請求未完成，請查看 SOAP Fault" }[response.status] || "請查看回應內容";
            if (response.status === 200) {
                var result = new DOMParser().parseFromString(text, "application/xml");
                var items = result.getElementsByTagNameNS(ns, "anyType");
                message = items.length === 2 && items[0].textContent === "true" ? "驗證通過" : items.length === 2 && items[0].textContent === "false" ? "驗證未通過" : "回應格式需確認";
            }
            status.textContent = "HTTP " + response.status + " · " + Math.round(performance.now() - start) + " ms · " + message;
        } catch (error) {
            status.textContent = error.name === "AbortError" ? "請求逾時，請確認服務狀態。" : "無法連線，請確認網路及服務狀態。";
        } finally {
            payload = "";
            clearTimeout(timeout);
            button.disabled = false;
        }
    });
}());
