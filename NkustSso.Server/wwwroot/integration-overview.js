"use strict";
// 情境只切換說明，不傳送帳密或呼叫任何驗證來源。
const scenarios = {
    verify: { title: "校務帳密驗證", description: "確認帳密與帳號狀態，回傳是否通過及來源身分代碼。", fields: ["驗證結果", "身分代碼"], note: "不回傳姓名或信箱；接收系統自行建立登入狀態。" },
    leaving: { title: "畢業離校系統", description: "協助離校系統確認登入者，回覆原方法成功分支所定義的基本資料。", fields: ["狀態", "帳號", "姓名"], note: "原程式此分支未回傳信箱，不能因其他方法有提供就假設這裡也有。" },
    consultation: { title: "諮商輔導中心系統", description: "驗證登入者後，提供此方法定義的身分與聯絡欄位。", fields: ["狀態", "成功標記", "學號／員編", "姓名", "信箱"], note: "這是登入身分資料，不是諮商紀錄；不代表其他系統也能取得相同欄位。" }
};
document.querySelectorAll("[data-scenario]").forEach(button => button.addEventListener("click", () => {
    const scenario = scenarios[button.dataset.scenario];
    document.querySelectorAll("[data-scenario]").forEach(item => item.setAttribute("aria-pressed", String(item === button)));
    document.getElementById("scenario-title").textContent = scenario.title;
    document.getElementById("scenario-description").textContent = scenario.description;
    document.getElementById("scenario-note").textContent = scenario.note;
    document.getElementById("scenario-fields").replaceChildren(...scenario.fields.map(field => {
        const chip = document.createElement("span"); chip.textContent = field; return chip;
    }));
}));
document.getElementById("print-brief").addEventListener("click", () => window.print());
