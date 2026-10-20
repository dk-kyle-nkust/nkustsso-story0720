"""Isolated integration checks for linq2db storage and revocable SSO sessions.

Uses demo accounts only. Creates temporary browser sessions and restores account
flags/roles in finally blocks. Does not print session cookies or OIDC tokens.
"""
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier
import sqlite3
import ssl
import time
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from e2e import (DATA, SERVER, SECRETS, ARTIFACTS, Path, os, sync_playwright,
                 expect, login, authorize, exchange, urlencode)


def rows(query, args=(), sessions=True):
    with sqlite3.connect(DATA / ("sessions.db" if sessions else "sso.db")) as db:
        return db.execute(query, args).fetchall()


def session_for(page, username="002", password=None):
    before = {r[0] for r in rows("SELECT Id FROM LoginSessions")}
    login(page, username, password)
    page.wait_for_url(SERVER + "/")
    created = rows("SELECT Id FROM LoginSessions WHERE UserName=?", (username,))
    new = [r[0] for r in created if r[0] not in before]
    assert len(new) == 1, "Expected a new independent session"
    return new[0]


def revoke(page, account="demo-student-active", session=None, reason="Integration test"):
    query = {"accountId": account}
    if session:
        query["sessionId"] = session
    assert page.goto(SERVER + "/admin/sessions/revoke?" + urlencode(query)).status == 200
    page.get_by_label("終止原因").fill(reason)
    page.get_by_role("button", name="確認終止", exact=True).click()


# 探測點必須是「登入就能開」的頁面，否則量到的是權限而不是 Session。
# /integration 要 ManageSessions 權限，學生帳號即使登入也永遠拿不到 200。
def central_active(context):
    return context.request.get(SERVER + "/account/continue", max_redirects=0).status == 200


with sync_playwright() as p:
    chrome = Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe"
    browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
    contexts = [browser.new_context(ignore_https_errors=True) for _ in range(4)]
    admin, first, second, other = contexts
    ap, fp, sp, op = [c.new_page() for c in contexts]
    admin_id = session_for(ap, "001", "123456")
    first_id = session_for(fp)
    second_id = session_for(sp)
    other_id = session_for(op, "003")

    assert first.request.get(SERVER + "/admin/sessions").status == 403
    assert first.request.post(SERVER + "/admin/sessions/revoke", form={"AccountId": "demo-student-active"}).status == 403
    assert admin.request.post(SERVER + "/admin/sessions/revoke", form={"AccountId": "demo-student-active"}).status == 400
    assert ap.goto(SERVER + "/admin/sessions").status == 200
    expect(ap.get_by_role("heading", name="登入管理", exact=True)).to_be_visible()
    assert ap.locator(f'tr[data-session-id="{admin_id}"]').count() == 1
    ap.set_viewport_size({"width": 1440, "height": 1000})
    ap.screenshot(path=str(ARTIFACTS / "sessions-desktop.png"), full_page=True)
    ap.set_viewport_size({"width": 390, "height": 844})
    assert ap.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
    ap.screenshot(path=str(ARTIFACTS / "sessions-mobile.png"), full_page=True)
    print("PASS administrator 001, role isolation, CSRF, desktop/mobile management")

    # Current role membership is authoritative even with an existing admin cookie.
    role = rows("SELECT RoleId FROM AspNetUserRoles WHERE UserId='demo-session-admin'", sessions=False)[0][0]
    try:
        rows("DELETE FROM AspNetUserRoles WHERE UserId='demo-session-admin' AND RoleId=?", (role,), False)
        assert admin.request.get(SERVER + "/admin/sessions").status == 403
    finally:
        rows("INSERT OR IGNORE INTO AspNetUserRoles(UserId,RoleId) VALUES('demo-session-admin',?)", (role,), False)
    assert admin.request.get(SERVER + "/admin/sessions").status == 200
    print("PASS live administrator role check")

    cookies = first.cookies()
    code, verifier = authorize(first)
    revoke(ap, session=first_id, reason="Lost test device")
    ap.wait_for_url(SERVER + "/admin/sessions")
    assert not central_active(first)
    assert central_active(second) and central_active(other)
    assert exchange(first, code, verifier).status == 400
    replay = browser.new_context(ignore_https_errors=True)
    replay.add_cookies(cookies)
    assert not central_active(replay)
    replay.close()
    assert rows("SELECT length(ProtectedTicket), RevocationReason FROM LoginSessions WHERE Id=?", (first_id,))[0] == (0, "Lost test device")
    assert rows("SELECT AffectedCount FROM SessionAudits WHERE SessionId=?", (first_id,))[0][0] == 1
    print("PASS individual revoke, stale cookie/code rejected, other session preserved")

    fresh_id = session_for(fp)
    revoke(ap)
    ap.wait_for_url(SERVER + "/admin/sessions")
    assert not central_active(first) and not central_active(second)
    assert central_active(other) and central_active(admin)
    print("PASS account-wide revoke and fresh login after individual revoke")

    # Change only a newly created test session's expiry, never a pre-existing user's.
    expired_id = session_for(fp)
    rows("UPDATE LoginSessions SET ExpiresAt=? WHERE Id=?", (int(time.time() * 1000) - 1, expired_id))
    assert not central_active(first)
    live_id = session_for(fp)
    before = rows("SELECT CreatedAt,ExpiresAt FROM LoginSessions WHERE Id=?", (live_id,))[0]
    assert before[1] - before[0] <= 3600000
    assert central_active(first)
    assert rows("SELECT CreatedAt,ExpiresAt FROM LoginSessions WHERE Id=?", (live_id,))[0] == before
    # Make only this test session eligible, then exercise the actual OIDC flow.
    rows("UPDATE LoginSessions SET ExpiresAt=? WHERE Id=?", (int(time.time() * 1000) + 20 * 60000, live_id))
    code, verifier = authorize(first)
    assert exchange(first, code, verifier).status == 200
    renewed = rows("SELECT CreatedAt,ExpiresAt FROM LoginSessions WHERE Id=?", (live_id,))[0]
    assert renewed[0] == before[0]
    assert 59 * 60000 <= renewed[1] - int(time.time() * 1000) <= 60 * 60000
    assert central_active(first)
    print("PASS server expiry, renewal threshold and OIDC session renewal")

    # Two simultaneous exchanges must not both consume one authorization code.
    code, verifier = authorize(first)
    gate = Barrier(2)
    body = urlencode({"grant_type": "authorization_code", "client_id": "academic",
        "client_secret": SECRETS["AcademicSecret"], "code": code,
        "redirect_uri": "https://localhost:7101/signin-oidc", "code_verifier": verifier}).encode()

    def concurrent_exchange(_):
        request = Request(SERVER + "/connect/token", data=body,
                          headers={"Content-Type": "application/x-www-form-urlencoded"})
        gate.wait(timeout=10)
        try:
            with urlopen(request, context=ssl._create_unverified_context(), timeout=30) as response:
                return response.status
        except HTTPError as error:
            return error.code

    with ThreadPoolExecutor(max_workers=2) as executor:
        statuses = sorted(executor.map(concurrent_exchange, range(2)))
    assert statuses == [200, 400], f"Concurrent code redemption statuses: {statuses}"
    print("PASS concurrent authorization-code redemption: one success only")

    # A changed security stamp invalidates a server session immediately.
    stamp = rows("SELECT SecurityStamp FROM AspNetUsers WHERE Id='demo-student-active'", sessions=False)[0][0]
    try:
        rows("UPDATE AspNetUsers SET SecurityStamp='session-integration-test' WHERE Id='demo-student-active'", sessions=False)
        assert not central_active(first)
    finally:
        rows("UPDATE AspNetUsers SET SecurityStamp=? WHERE Id='demo-student-active'", (stamp,), False)
    assert not central_active(first)
    session_for(fp)
    enabled = rows("SELECT Enabled FROM AspNetUsers WHERE Id='demo-student-active'", sessions=False)[0][0]
    try:
        rows("UPDATE AspNetUsers SET Enabled=0 WHERE Id='demo-student-active'", sessions=False)
        assert not central_active(first)
    finally:
        rows("UPDATE AspNetUsers SET Enabled=? WHERE Id='demo-student-active'", (enabled,), False)
    assert not central_active(first)
    print("PASS security stamp and account disable revoke current session")

    session_for(fp)
    fp.get_by_role("button", name="登出", exact=True).click()
    assert not central_active(first)
    op.goto(SERVER + "/")
    op.get_by_role("button", name="登出", exact=True).click()
    assert rows("SELECT RevocationReason,length(ProtectedTicket) FROM LoginSessions WHERE Id=?", (other_id,))[0] == ("user_logout", 0)
    ap.goto(SERVER + "/")
    ap.get_by_role("button", name="登出", exact=True).click()
    assert not central_active(admin)
    for context in contexts:
        context.close()
    browser.close()
print("All session and linq2db browser checks passed.")
