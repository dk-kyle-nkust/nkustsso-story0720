"use strict";
// 公開入口的介面詞彙集中維護；不傳送帳密或資料庫內容至外部翻譯服務。
(function () {
    if (document.body.dataset.englishEnabled !== 'true') return;
    const english = {
        '國立高雄科技大學': 'National Kaohsiung University of Science and Technology',
        '版權所有': 'All rights reserved.',
        '電算中心（另開新分頁）': 'Computer Center (opens in a new tab)',
        '國立高雄科技大學，返回專區選擇': 'NKUST — Back to service areas',
        '校務資訊入口': 'Campus Services',
        '跳至主要內容': 'Skip to main content',
        '回校首頁': 'University home',
        '忘記密碼': 'Forgot password',
        '忘記密碼？': 'Forgot password?',
        '（另開新分頁）': '(opens in a new tab)',
        '專區選單': 'Service areas',
        '主要導覽': 'Main navigation',
        '目前登入者': 'Signed in as',
        '帳號': 'Account', '學號': 'Student ID', '員編': 'Employee ID', '學年期': 'Academic term',
        '系所': 'Department', '學制': 'Degree program', '部別': 'Division', '校區': 'Campus',
        '學生狀況': 'Student category', '在學狀況': 'Enrollment status', '所屬單位': 'Department',
        '個人資料暫時無法讀取，請稍後再試。': 'Profile information is temporarily unavailable. Please try again later.',
        '查無唯一的學生資料。': 'A unique student record could not be found.',
        '查無唯一的教職員資料。': 'A unique employee record could not be found.',
        '目前學年期尚無法確認。': 'The current academic term could not be confirmed.',
        '查無當學期就學資料。': 'No enrollment record was found for the current term.',
        '當學期資料有多筆對應，暫不顯示。': 'Multiple matching records were found for the current term. Details are unavailable.',
        '選單管理': 'Manage services',
        '登出': 'Sign out',
        '班級：': 'Class: ',
        '學生': 'Student',
        '教職員': 'Faculty & staff',
        '教師': 'Faculty',
        '職員': 'Staff',
        '校友': 'Alumni',
        '使用者': 'User',
        '專區選擇': 'Service areas',
        '學生專區': 'Students',
        '教職員專區': 'Faculty & staff',
        '校友專區': 'Alumni',
        '進入學生專區': 'Student services',
        '進入教職員專區': 'Faculty & staff services',
        '進入校友專區': 'Alumni services',
        '課程與學籍、學習與獎助、校園生活': 'Courses, student records, scholarships and campus life',
        '教學與研究、人事與行政、總務與財務': 'Teaching, research, personnel, administration and finance',
        '校友服務、證明申請、職涯資訊': 'Alumni support, certificates and career opportunities',
        '← 返回專區選擇': '← Back to service areas',
        '搜尋服務': 'Search services',
        '輸入服務名稱或關鍵字': 'Service name or keyword',
        '服務分類': 'Service categories',
        '全部服務': 'All services',
        '單一登入 · 進入服務': 'Single sign-on · Open service',
        '單一登入 · 需登入': 'Single sign-on · Sign-in required',
        '外部網站': 'External website', '外部網站 ↗': 'External website ↗',
        '需先登入': 'Sign-in required', '登入使用': 'Sign in', '開啟服務': 'Open service',
        '沒有符合的服務，請調整關鍵字或選擇其他分類。': 'No matching services. Try another keyword or category.',
        '「單一登入」服務可沿用有效的登入狀態；外部網站依各系統的帳號與登入規則使用。': 'Single sign-on services can reuse your active session. External websites have their own account and sign-in requirements.',
        '教務類': 'Academic affairs', '學務類': 'Student affairs', '人事類': 'Personnel',
        '總務類': 'General affairs', '財務類': 'Finance', '主計類': 'Accounting', '研發類': 'Research',
        '研究類': 'Research', '文書類': 'Documents', '資訊類': 'IT services', '綜合服務': 'General services',
        '統一登入': 'Single sign-on', '學生專區登入': 'Student sign-in',
        '教職員專區登入': 'Faculty & staff sign-in', '校友專區登入': 'Alumni sign-in',
        '校園帳號登入': 'Sign in with your campus account',
        '請使用校務系統的學號與密碼登入。': 'Use your student ID and campus password.',
        '請使用教職員校園帳號與密碼登入。': 'Use your faculty or staff campus account and password.',
        '請使用仍可登入的校園帳號與密碼。': 'Use your active campus account and password.',
        '請使用校園帳號與密碼登入。': 'Use your campus account and password.',
        '校園帳號': 'Campus account', '密碼': 'Password', '登入': 'Sign in',
        '請輸入完整學號': 'Enter your full student ID', '請輸入校園帳號': 'Enter your campus account',
        '請輸入帳號': 'Enter your account', '請輸入密碼': 'Enter your password',
        '顯示密碼': 'Show password', '隱藏密碼': 'Hide password', '切換密碼顯示': 'Toggle password visibility',
        '前往高科大密碼重設系統': 'Open NKUST password reset',
        '原帳號已停用或尚未開通時，請先查看': 'If your account is inactive or has not been activated, see the',
        '校方校友服務指南 ↗': 'university alumni service guide ↗',
        '，洽詢適用的服務與帳號使用方式。': 'for available services and account assistance.',
        '密碼手冊 (PDF)': 'Password reset guide (PDF)',
        '校友服務中心 ↗': 'Alumni Service Center ↗', '校友服務指南 ↗': 'Alumni service guide ↗',
        '教師服務指南 ↗': 'Faculty service guide ↗', '職員服務指南 ↗': 'Staff service guide ↗',
        '學號查詢 ↗': 'Find your student ID ↗', '新生專區 ↗': 'New students ↗',
        '帳號使用說明與服務聯絡窗口': 'Account help and contacts',
        '帳號登入說明': 'Account sign-in help', '帳號規則：': 'Account:',
        '依校方核發的帳號登入；學生使用完整學號，教師與職員使用教職員帳號。校友帳號須仍在有效使用範圍內。': 'Use your university-issued account: full student ID for students, or staff account for faculty and staff. Alumni accounts must still be active.',
        '密碼規則：': 'Password:',
        '請使用目前有效的校園帳號密碼；首次使用或無法登入時，依校方的帳號啟用與重設說明操作。': 'Use your current campus password. For first-time access or sign-in problems, follow the university account activation and reset instructions.',
        '重設密碼：': 'Reset password:',
        '若忘記密碼或密碼遭鎖定，請至': 'If you forgot your password or your account is locked, visit',
        '忘記密碼系統': 'Password reset', '或參閱': 'or read the', '忘記密碼操作手冊': 'password reset guide',
        '瀏覽器推薦：': 'Browsers:',
        '支援 Chrome、Edge、Firefox、Safari 等現代瀏覽器最新版本。': 'Use a current version of Chrome, Edge, Firefox or Safari.',
        '相關下載：': 'Downloads:', '校務系統造字檔下載 (115/09更新)': 'Campus system character fonts (September 2026)',
        '條碼字型安裝檔': 'Barcode font installer', '服務聯絡窗口': 'Service contacts',
        '總機電話：': 'Main phone:', '（各校區直撥分機）': '(campus extensions)',
        '問題': 'Topic', '承辦單位': 'Office', '分機或窗口': 'Extension or contact', '分機': 'Ext.',
        '無法登入 / 軟體諮詢': 'Sign-in / software help', '電算中心軟體組': 'Computer Center, Software Division',
        '校務系統密碼修改': 'Campus system password changes', '系統管理中心': 'System Administration Center',
        '學籍與註冊相關': 'Student records and registration', '教務處註冊組': 'Registration Division',
        '建工/燕巢/楠梓/旗津/第一': 'Jiangong / Yanchao / Nanzih / Cijin / First Campus',
        '選課與課務相關': 'Courses and enrollment', '教務處課務組': 'Curriculum Division',
        '各校區課務窗口': 'Campus curriculum offices', '成績相關問題': 'Grades',
        '學習輔導組': 'Learning Support Division', '各校區綜合業務處': 'Campus general administration offices',
        '新生入學與報到': 'New student admission', '教務處招生組': 'Admissions Division', '招生諮詢窗口': 'Admissions office',
        '請先登出目前帳號，再登入其他帳號。': 'Sign out of your current account before signing in with another account.',
        '登入服務暫時無法使用，請稍後再試。': 'Sign-in is temporarily unavailable. Please try again later.',
        '無法登入，請確認帳密及帳號狀態；連續錯誤後請稍候再試。': 'Unable to sign in. Check your credentials and account status. After repeated failures, wait before trying again.',
        '登入成功': 'Signed in', '正在前往服務。若頁面沒有自動開啟，請點選下方連結。': 'Opening your service. If it does not open automatically, use the link below.',
        '繼續前往服務': 'Continue to service', '沒有存取權限': 'Access denied',
        '此頁面僅供具有登入管理權限的人員使用。': 'This page requires administrative access.', '返回入口': 'Back to portal',
        '串接系統管理': 'Systems', '身分分類查詢': 'Identity types', '首頁分類管理': 'Categories',
        '登入管理': 'Sessions', '整合資訊': 'Integration information',
        '選課系統': 'Course enrollment', '選課管理': 'Course enrollment management', '教學平台': 'E-learning',
        '學分抵免申請': 'Credit transfer applications', '選課表單申請暨簽核': 'Course applications and approvals',
        '學籍成績證明申請': 'Student records and transcript requests', '學務資訊系統': 'Student affairs system',
        '線上請假': 'Leave requests', '預約諮商輔導': 'Counseling appointments', '入學獎學金申請': 'Admission scholarship applications',
        '活動訊息平台': 'Campus events', '兼任助理簽到': 'Part-time assistant attendance',
        '在職服務證明申請': 'Employment certificate requests', '進用暨保費管理': 'Hiring and insurance management',
        '主計請購系統': 'Purchase requisitions', '經費結報簽核': 'Expense reports and approvals',
        '校車預約': 'Campus shuttle reservations', '停車證申請': 'Parking permit applications',
        '場地租借暨門禁': 'Venue reservations and access', '各式維修申請': 'Maintenance requests', '郵件查詢': 'Mail tracking',
        '政府產學計畫': 'Government-funded industry projects', '非政府產學計畫': 'Non-government industry projects',
        '教學補助申請': 'Teaching grant applications', '文件流程查詢': 'Document tracking', '業務流程 SOP': 'Administrative procedures',
        '公務電子郵件平台': 'Staff email', '校務建言': 'Campus feedback', '圖書資源查詢': 'Library search',
        '捐贈興學': 'Giving to NKUST', '校友服務中心': 'Alumni Service Center', '校友服務系統': 'Alumni services system',
        '校友證申請': 'Alumni card applications', '就業職缺資訊': 'Job opportunities', '校友優惠資訊': 'Alumni benefits',
        '登出': 'Sign out', '結束中央登入？': 'Sign out of SSO?',
        '這會結束 SSO 的登入狀態。其他已登入系統的狀態，依各系統的登出及到期設定處理。': 'This ends your central SSO session. Other systems follow their own sign-out and session expiry rules.',
        '確認登出': 'Confirm sign-out', '回到入口': 'Back to portal',
        '正在登入系統': 'Signing in', '正在確認你的身分，稍後將進入系統。': 'Verifying your identity. Your service will open shortly.', '繼續': 'Continue',
        '常見問題': 'FAQ', '登入與帳號': 'Sign-in and accounts',
        '為什麼登入後會看到更多服務？': 'Why do more services appear after sign-in?',
        '部分系統只對符合身分條件的登入者顯示，未登入時不會公開名稱或說明。可點選專區中的「登入」，登入後仍使用同一套搜尋與分類。': 'Some services are visible only to signed-in users who meet the identity requirements. Their names and descriptions are hidden from visitors. Select Sign in to see them in the same directory.',
        '點選服務會離開目前的選單嗎？': 'Will opening a service leave this directory?',
        '服務會在新分頁開啟。需要登入時，也在新分頁完成登入並接續前往服務；原本的專區、搜尋與分類會保留。': 'Services open in a new tab. If sign-in is required, complete it in that tab to continue to the service. Your original directory, search and category selection stay in place.',
        '進入專區就需要登入嗎？': 'Do I need to sign in to browse a service area?',
        '學生、教職員與校友專區皆可直接瀏覽。點選需要登入的服務時，才會導向統一登入頁，登入後接續前往原本的服務。': 'Student, faculty and staff, and alumni directories are public. Selecting a service that requires sign-in takes you to the central sign-in page, then back to your selected service.',
        '已經登入，使用其他系統還要再登入嗎？': 'Do I need to sign in again for another system?',
        '已串接單一登入的服務可沿用有效的中央登入狀態。未串接的外部網站仍可能要求自己的帳號密碼；登入逾時、被撤銷或服務要求重新驗證時，也需要再次登入。': 'Integrated single sign-on services can reuse your active central session. Other websites may require their own credentials. You must sign in again if your session expires, is revoked, or a service requests verification.',
        '忘記密碼或帳號被鎖定怎麼辦？': 'What if I forget my password or my account is locked?',
        '請依校方密碼重設系統的指示操作；仍無法使用時，請聯絡該系統的服務窗口。': 'Follow the university password reset instructions. If the problem continues, contact the relevant service support team.',
        '選擇不同專區，會改變我的帳號權限嗎？': 'Does selecting another area change my access permissions?',
        '專區只用來整理服務選單，不會改變帳號身分或權限。實際能否使用服務，仍由帳號資料及各系統的授權規則決定。': 'Areas organize service menus. They do not change your identity or permissions. Access depends on your account and each system’s authorization rules.',
        '畢業後可以繼續使用原本帳號嗎？': 'Can I keep using my account after graduation?',
        '校友帳號與服務資格依校方及各系統規定。原帳號已停用或尚未開通時，請查閱校友服務指南，或聯絡校友服務中心。': 'Alumni accounts and eligibility follow university and service rules. If your account is inactive or has not been activated, consult the alumni service guide or contact the Alumni Service Center.',
        '各身分的校方服務指南在哪裡？': 'Where can I find the university service guides?',
        '以下連結前往高科大校方網站，提供各身分的完整服務資訊。': 'These links open the university website with service information for each group.',
        '學生服務指南': 'Student service guide', '教師服務指南': 'Faculty service guide',
        '職員服務指南': 'Staff service guide', '校友服務指南': 'Alumni service guide'
    };
    const key = 'nkust.portal.language';
    let language = 'zh-Hant';
    try { if (localStorage.getItem(key) === 'en') language = 'en'; } catch { /* 私密模式仍可在當頁切換。 */ }
    const text = value => language === 'en' ? (english[value] ?? value) : value;
    const button = document.querySelector('[data-language-switch]');
    const textNodes = [];
    const attributes = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) {
        const node = walker.currentNode;
        if (node.parentElement.closest('script, style, noscript, [data-no-translate], [data-catalog-text], [data-language-switch], #directory-status, #portal-count, [data-pin], [data-service-info]')) continue;
        if (Object.hasOwn(english, node.textContent.trim())) textNodes.push([node, node.textContent]);
    }
    for (const element of document.querySelectorAll('[aria-label], [title], [placeholder], img[alt]')) {
        if (element.closest('[data-no-translate]') || element.matches('[data-language-switch], [data-service-info], #toggle-pwd')) continue;
        for (const attribute of ['aria-label', 'title', 'placeholder', 'alt']) {
            const value = element.getAttribute(attribute);
            if (Object.hasOwn(english, value)) attributes.push([element, attribute, value]);
        }
    }
    const originalTitle = document.title;
    const catalog = [...document.querySelectorAll('[data-catalog-text]')].map(el => [el, el.textContent.trim()]);
    function apply() {
        document.documentElement.lang = language;
        document.title = originalTitle.split(' · ').map(text).join(' · ');
        for (const [node, original] of textNodes) {
            node.textContent = original.replace(original.trim(), text(original.trim()));
        }
        for (const [el, name, original] of attributes) el.setAttribute(name, text(original));
        // 自訂服務名稱及詳述沒有人工翻譯時保留原文，讓讀屏使用正確語言。
        for (const [el, original] of catalog) {
            const translated = el.dataset.en?.trim() || english[original];
            el.textContent = (language === 'en' ? translated || original : original || translated) || '';
            el.lang = (language === 'en' && translated) || (!original && translated) ? 'en' : 'zh-Hant';
        }
        for (const info of document.querySelectorAll('[data-service-info]')) {
            info.setAttribute('aria-label', language === 'en' ? (info.dataset.serviceNameEn?.trim() || text(info.dataset.serviceName)) + ': Service details' : info.dataset.serviceName + '：功能說明');
        }
        const passwordToggle = document.getElementById('toggle-pwd');
        if (passwordToggle) {
            passwordToggle.setAttribute('aria-label', text(document.getElementById('Password').type === 'text' ? '隱藏密碼' : '顯示密碼'));
            passwordToggle.title = text('切換密碼顯示');
        }
        if (button) {
            button.textContent = language === 'en' ? '繁體中文' : 'English';
            button.lang = language === 'en' ? 'zh-Hant' : 'en';
            button.setAttribute('aria-label', language === 'en' ? '切換為繁體中文' : 'Switch to English');
            button.hidden = false;
        }
        document.dispatchEvent(new Event('portal-language-change'));
    }
    window.PortalLanguage = { text, get language() { return language; } };
    button?.addEventListener('click', () => {
        language = language === 'en' ? 'zh-Hant' : 'en';
        try { localStorage.setItem(key, language); } catch { /* 不阻擋當頁切換。 */ }
        apply();
    });
    window.addEventListener('storage', event => {
        if (event.key === key || event.key === null) {
            language = event.newValue === 'en' ? 'en' : 'zh-Hant';
            apply();
        }
    });
    apply();
})();
