"""Isolated Server HTTPS/OIDC checks. Build NkustSSO.slnx before running.

Requires Python and playwright (`pip install -r tests/requirements.txt`).
Uses an installed Chromium browser and never prints demo credentials or tokens.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
from urllib.parse import urlencode, urlparse, parse_qs

from playwright.sync_api import sync_playwright, expect
from server_fixture import ServerFixture

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ServerFixture()
DATA = FIXTURE.directory
SECRETS = json.loads((DATA / "demo-credentials.json").read_text(encoding="utf-8-sig"))
SERVER = FIXTURE.url
ARTIFACTS = DATA / "screenshots"
ARTIFACTS.mkdir(exist_ok=True)


def login(page, username="002", password=None):
    page.goto(SERVER + "/account/login")
    page.get_by_label("校園帳號", exact=True).fill(username)
    page.get_by_label("密碼", exact=True).fill(password or SECRETS["Password"])
    page.get_by_role("button", name="登入", exact=False).click()


def authorize(context, scope="openid profile student_profile"):
    verifier = secrets.token_urlsafe(40)
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    query = dict(client_id="academic", redirect_uri="https://localhost:7101/signin-oidc",
                 response_type="code", scope=scope, code_challenge=challenge,
                 code_challenge_method="S256", state="test-state", nonce="test-nonce")
    response = context.request.get(SERVER + "/connect/authorize?" + urlencode(query), max_redirects=0)
    assert response.status == 302, f"Authorization failed: HTTP {response.status}"
    code = parse_qs(urlparse(response.headers["location"]).query)["code"][0]
    return code, verifier


def exchange(context, code, verifier):
    return context.request.post(SERVER + "/connect/token", form={
        "grant_type": "authorization_code", "client_id": "academic",
        "client_secret": SECRETS["AcademicSecret"], "code": code,
        "redirect_uri": "https://localhost:7101/signin-oidc", "code_verifier": verifier,
    })


def claims_from_response(response):
    assert response.status == 200, f"Token exchange failed: HTTP {response.status}"
    payload = response.json()["id_token"].split(".")[1]
    # Decode only to assert server claim release; this is not a client token validator.
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))


if __name__ == "__main__":
    with sync_playwright() as p:
        chrome = Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe"
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        context = browser.new_context(ignore_https_errors=True, viewport={"width": 1440, "height": 1000})
        page = context.new_page()
        page.goto(SERVER + "/account/login")
        page.screenshot(path=str(ARTIFACTS / "login-desktop.png"), full_page=True)
        assert context.request.post(SERVER + "/account/login", form={"UserName": "002", "Password": "invalid"}).status == 400
        print("PASS CSRF: login POST without token rejected")
        login(page, password="DefinitelyWrongPassword!")
        expect(page.get_by_role("alert")).to_contain_text("無法登入")
        login(page, username="004")
        expect(page.get_by_role("alert")).to_contain_text("無法登入")
        print("PASS wrong password and disabled account rejected")
        login(page)
        expect(page.get_by_role("heading", name="學生專區", exact=True)).to_be_visible()
        page.goto(SERVER + "/portal/student")
        page.screenshot(path=str(ARTIFACTS / "portal-desktop.png"), full_page=True)

        search = page.locator("#directory-search")
        search.fill("教務")
        expect(page.locator('li[data-system-id="academic"]')).to_be_visible()
        expect(page.locator('li[data-system-id="equipment"]')).to_be_hidden()
        search.press("Escape")
        expect(page.locator('li[data-system-id="equipment"]')).to_be_visible()
        page.locator('[data-directory-filter="affairs"]').click()
        expect(page.locator('.directory-group[data-directory-category="academic"]')).to_be_hidden()
        page.locator('[data-directory-filter="all"]').click()
        expect(page.locator('#portal-pinned, #portal-recent')).to_have_count(0)
        print("PASS unified directory search and category filter")

        code, verifier = authorize(context)
        claims = claims_from_response(exchange(context, code, verifier))
        assert claims["iss"] == SERVER + "/" and claims["aud"] == "academic"
        assert claims["sub"] == "demo-student-active"
        assert claims["class_code"] == "DEMO-CS-3A"
        assert exchange(context, code, verifier).status == 400
        code, verifier = authorize(context)
        assert exchange(context, code, "wrong-verifier-" + verifier).status == 400
        code, verifier = authorize(context, "openid profile")
        claims = claims_from_response(exchange(context, code, verifier))
        assert "class_code" not in claims and "student_status_code" not in claims
        print("PASS authorization-code replay, incorrect PKCE rejected; profile scope enforced")
        bad_redirect = context.request.get(SERVER + "/connect/authorize?" + urlencode({
            "client_id": "academic", "redirect_uri": "https://example.invalid/callback",
            "response_type": "code", "scope": "openid",
        }), max_redirects=0)
        assert bad_redirect.status == 400 and "location" not in bad_redirect.headers
        assert context.request.post(SERVER + "/account/logout").status == 400
        print("PASS unregistered redirect and logout CSRF rejected")
        page.goto(SERVER + "/account/login?returnUrl=https%3A%2F%2Fexample.invalid")
        page.wait_for_url(SERVER + "/")
        print("PASS external login return URL ignored during SSO reuse")
        page.set_viewport_size({"width": 390, "height": 844})
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.screenshot(path=str(ARTIFACTS / "portal-mobile.png"), full_page=True)
        page.get_by_role("button", name="登出", exact=True).click()
        page.wait_for_url(SERVER + "/portal")
        page.screenshot(path=str(ARTIFACTS / "login-mobile.png"), full_page=True)
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.goto(SERVER + "/")
        expect(page.get_by_role("heading", name="學生專區", exact=True)).to_be_visible()
        print("PASS central logout and mobile layouts")
        context.close()

        browser.close()
    print("All browser and protocol checks passed.")
