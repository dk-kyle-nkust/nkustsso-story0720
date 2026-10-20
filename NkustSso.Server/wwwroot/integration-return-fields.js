(() => {
    'use strict';
    const protocol = document.getElementById('Client_Protocol');
    const fields = document.getElementById('integration-return-fields');
    const json = document.getElementById('integration-json-fields');
    if (!protocol || !fields || !json) return;
    function refresh() {
        const soap = protocol.value === 'soap';
        fields.hidden = !soap;
        json.hidden = soap;
        // 切換回 SOAP 時保留本次選取；JSON 不提交 SOAP 欄位設定。
        fields.querySelectorAll('input[type=checkbox]').forEach(input => { input.disabled = !soap; });
        document.querySelectorAll('[data-soap-only="true"]').forEach(group => {
            group.hidden = !soap;
            group.querySelectorAll('input, textarea').forEach(input => { input.disabled = !soap; });
        });
    }
    protocol.addEventListener('change', refresh);
    refresh();
})();
