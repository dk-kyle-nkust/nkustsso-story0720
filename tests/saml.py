"""Isolated SAML browser checks. The SP fixture signs Redirect requests independently.

Response XML signatures are independently checked by SamlChecks.cs using SignedXml.
This browser fixture exercises navigation, state, administration and CSP.
"""
import base64
from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
from server_fixture import seed_server, BUILD_DIRECTORY
import secrets
import socket
import sqlite3
import ssl
import subprocess
import time
from urllib.parse import urlencode, parse_qs
from urllib.request import urlopen
import xml.etree.ElementTree as ET
import zlib
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa, padding
from cryptography.x509.oid import NameOID
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / '.local/saml-checks' / secrets.token_hex(8)
DATA.mkdir(parents=True)
seed_server(DATA)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    PORT = sock.getsockname()[1]
SERVER = f'https://localhost:{PORT}'
SP = 'https://localhost:7145'
ENTITY = 'urn:nkust:test:saml-sp'
P = 'urn:oasis:names:tc:SAML:2.0:protocol'
A = 'urn:oasis:names:tc:SAML:2.0:assertion'
ALGORITHM = 'http://www.w3.org/2001/04/xmldsig-more#rsa-sha256'
KEY = rsa.generate_private_key(public_exponent=65537, key_size=2048)
subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'SAML test SP')])
CERT = x509.CertificateBuilder().subject_name(subject).issuer_name(subject).public_key(KEY.public_key()).serial_number(
    x509.random_serial_number()).not_valid_before(datetime.now(timezone.utc) - timedelta(days=1)).not_valid_after(
    datetime.now(timezone.utc) + timedelta(days=7)).sign(KEY, hashes.SHA256()).public_bytes(serialization.Encoding.PEM).decode()
process = None
log = None


def start():
    global process, log
    log = (DATA / 'server.log').open('a', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(BUILD_DIRECTORY / 'NkustSso.Server.dll' if BUILD_DIRECTORY else ROOT / 'NkustSso.Server/bin/Debug/net10.0/NkustSso.Server.dll'),
        '--contentRoot', str(ROOT / 'NkustSso.Server'), '--urls', SERVER, '--Sso:Issuer', SERVER + '/',
        '--Sso:DataDirectory', str(DATA), '--Sso:StorageProvider', 'SQLite', '--CampusAuthentication:Enabled', 'false', '--ConnectionStrings:LoginAuth', ''], cwd=ROOT,
        env={**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development'}, stdout=log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    for _ in range(100):
        if process.poll() is not None:
            raise AssertionError('SAML test service exited; inspect local log.')
        try:
            with urlopen(SERVER + '/health', context=ssl._create_unverified_context(), timeout=1) as response:
                if response.status == 200:
                    return
        except OSError:
            time.sleep(.2)
    raise AssertionError('Startup timeout')


def stop():
    if process and process.poll() is None:
        process.terminate()
        process.wait(timeout=10)
    if log:
        log.close()


def request_url(*, issuer=ENTITY, acs=SP + '/acs', destination=None, age=0, passive=False,
                force=False, key=KEY, relay='state', request_id=None):
    request_id = request_id or '_' + secrets.token_hex(16)
    root = ET.Element('{' + P + '}AuthnRequest', ID=request_id, Version='2.0',
        IssueInstant=(datetime.now(timezone.utc) + timedelta(seconds=age)).strftime('%Y-%m-%dT%H:%M:%SZ'),
        Destination=destination or SERVER + '/saml/sso', AssertionConsumerServiceURL=acs,
        ProtocolBinding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST')
    if passive:
        root.set('IsPassive', 'true')
    if force:
        root.set('ForceAuthn', 'true')
    ET.SubElement(root, '{' + A + '}Issuer').text = issuer
    compressor = zlib.compressobj(wbits=-15)
    encoded = base64.b64encode(compressor.compress(ET.tostring(root)) + compressor.flush()).decode()
    query = urlencode([('SAMLRequest', encoded), ('RelayState', relay), ('SigAlg', ALGORITHM)])
    signature = key.sign(query.encode(), padding.PKCS1v15(), hashes.SHA256())
    return SERVER + '/saml/sso?' + query + '&' + urlencode({'Signature': base64.b64encode(signature).decode()})


def login(page, account):
    page.goto(SERVER + '/account/login')
    page.locator('[name="UserName"]').fill(account)
    page.locator('[name="Password"]').fill('123456')
    page.get_by_role('button', name='登入', exact=True).click()
    page.wait_for_url(SERVER + '/')


try:
    start()
    with sync_playwright() as p:
        chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
        browser = p.chromium.launch(executable_path=str(chrome) if chrome.exists() else None)
        admin = browser.new_context(ignore_https_errors=True)
        admin.add_init_script("window.cspViolations=[]; document.addEventListener('securitypolicyviolation', e => window.cspViolations.push({directive:e.violatedDirective, source:e.sourceFile, line:e.lineNumber, blocked:e.blockedURI}));")
        page = admin.new_page()
        login(page, '001')
        page.goto(SERVER + '/admin/systems/new')
        page.locator('[name="System.ClientId"]').fill('saml-test')
        page.locator('[name="System.DisplayName"]').fill('SAML test')
        page.locator('select[name="System.Protocol"]').select_option('saml', force=True)
        expect(page.locator('#saml-settings')).to_be_visible()
        page.locator('[name="System.EntryUrl"]').fill(SP + '/login')
        page.locator('[name="System.RedirectUri"]').fill(SP + '/acs')
        page.locator('[name="System.SamlEntityId"]').fill(ENTITY)
        page.locator('[name="System.SamlSigningCertificate"]').fill(CERT)
        page.locator('[name="AllowedClaims"][value="2"]').check()
        assert page.evaluate('window.cspViolations') == [], page.evaluate('window.cspViolations')
        page.locator('.system-form button[type="submit"]').click()
        page.wait_for_url(SERVER + '/admin/systems?section=sso')
        page.goto(SERVER + '/admin/systems?protocol=saml')
        assert page.locator('.system-registry tbody tr').count() == 1
        assert 'SAML 2.0' in page.locator('.system-registry tbody').inner_text()
        page.goto(SERVER + '/admin/systems/saml-test/edit')
        expect(page.locator('[name="System.SamlEntityId"]')).to_have_value(ENTITY)
        page.set_viewport_size({'width': 390, 'height': 844})
        assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth')
        assert page.evaluate('window.cspViolations') == [], page.evaluate('window.cspViolations')
        (ROOT / 'test-results').mkdir(exist_ok=True)
        page.screenshot(path=str(ROOT / 'test-results/saml-editor-mobile.png'), full_page=True)
        metadata = admin.request.get(SERVER + '/saml/metadata')
        assert metadata.status == 200 and 'WantAuthnRequestsSigned="true"' in metadata.text()
        original_metadata_cert = ET.fromstring(metadata.text()).find('.//{http://www.w3.org/2000/09/xmldsig#}X509Certificate').text
        print('PASS SAML administration, metadata, mobile layout and CSP')

        received = []
        saml_csp = []
        student = browser.new_context(ignore_https_errors=True)
        student.expose_binding('reportSamlCsp', lambda source, uri, directive: saml_csp.append(directive) if '/saml/' in uri else None)
        student.add_init_script("document.addEventListener('securitypolicyviolation', e => window.reportSamlCsp(e.documentURI, e.violatedDirective));")
        student.add_init_script("window.cspViolations=[]; document.addEventListener('securitypolicyviolation', e => window.cspViolations.push({directive:e.violatedDirective, source:e.sourceFile, line:e.lineNumber, blocked:e.blockedURI}));")

        def acs(route):
            posted = parse_qs(route.request.post_data)
            received.append(ET.fromstring(base64.b64decode(posted['SAMLResponse'][0])))
            assert posted.get('RelayState') == ['state']
            route.fulfill(status=200, content_type='text/html', body='<h1>SP signed in</h1>')

        student.route(SP + '/acs', acs)
        student.route(SP + '/login', lambda route: route.fulfill(status=302, headers={'Location': request_url()}))
        portal = student.new_page()
        login(portal, '002')
        portal.goto(SERVER + '/portal/student')
        entry = portal.locator('[data-system-id="saml-test"] a')
        expect(entry).to_have_attribute('target', '_blank')
        opened = student.request.get(SERVER + entry.get_attribute('href'), max_redirects=0)
        assert opened.status == 302 and opened.headers['location'] == SP + '/login'
        # 入口轉址與模擬 SP 簽署分開驗證，避免瀏覽器攔截器略過轉址鏈上的虛擬主機。
        sp_page = student.new_page()
        sp_page.goto(request_url())
        sp_page.wait_for_url(SP + '/acs')
        assert len(received) == 1
        response = received[-1]
        assert response.find('.//{' + A + '}NameID').text == 'demo-student-active'
        assert [a.get('Name') for a in response.findall('.//{' + A + '}Attribute')] == ['name']
        assert saml_csp == [], saml_csp
        print('PASS portal redirect, SP-initiated SAML login and claim allowlist')

        # Manually inspect the intermediate form without executing its script.
        url = request_url()
        first = student.request.get(url, max_redirects=0)
        assert first.status == 302
        continuation = SERVER + first.headers['location']
        post = student.request.get(continuation)
        assert post.status == 200 and 'name="SAMLResponse"' in post.text()
        assert 'form-action ' + SP in post.headers['content-security-policy']
        assert student.request.get(continuation).status == 400
        assert student.request.get(url, max_redirects=0).status == 400
        outsider = browser.new_context(ignore_https_errors=True)
        start_flow = student.request.get(request_url(), max_redirects=0)
        assert outsider.request.get(SERVER + start_flow.headers['location']).status == 400
        print('PASS request replay, single-use completion and browser correlation')

        for kwargs in [dict(acs='https://unregistered.example/acs'), dict(issuer='urn:unknown'), dict(age=-600),
                       dict(destination=SERVER + '/wrong'), dict(force=True), dict(relay='x' * 81),
                       dict(key=rsa.generate_private_key(public_exponent=65537, key_size=2048))]:
            assert student.request.get(request_url(**kwargs), max_redirects=0).status == 400, kwargs.keys()
        print('PASS wrong signature, unknown issuer, ACS, destination, expiry and unsupported request rejection')

        stop()
        start()
        assert student.request.get(url, max_redirects=0).status == 400
        print('PASS replay protection survives restart')

        passive = outsider.request.get(request_url(passive=True))
        assert passive.status == 200 and 'SAMLResponse' in passive.text()
        cold_page = outsider.new_page()
        outsider.route(SP + '/acs', acs)
        cold_page.goto(request_url())
        cold_page.wait_for_url(SERVER + '/account/login?**')
        cold_page.locator('[name="UserName"]').fill('003')
        cold_page.locator('[name="Password"]').fill('123456')
        cold_page.get_by_role('button', name='登入', exact=True).click()
        cold_page.wait_for_url(SP + '/acs')
        assert received[-1].find('.//{' + A + '}NameID').text == 'demo-student-leave'
        print('PASS unauthenticated login continuation and passive response')

        pending = student.request.get(request_url(), max_redirects=0)
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE RegisteredSystems SET Enabled=0 WHERE ClientId='saml-test'")
        assert student.request.get(SERVER + pending.headers['location']).status == 400
        assert student.request.get(request_url(), max_redirects=0).status == 400
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE RegisteredSystems SET Enabled=1 WHERE ClientId='saml-test'")
            db.execute("UPDATE AspNetUsers SET Enabled=0 WHERE Id='demo-student-active'")
        assert '/account/login' in student.request.get(request_url()).url
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE AspNetUsers SET Enabled=1 WHERE Id='demo-student-active'")
        login(portal, '002')
        with sqlite3.connect(DATA / 'sessions.db') as db:
            assert db.execute("SELECT count(*) FROM SamlAudits WHERE Outcome='success'").fetchone()[0] >= 3
            db.execute("UPDATE LoginSessions SET RevokedAt=1 WHERE AccountId='demo-student-active'")
        with sqlite3.connect(DATA / 'sso.db') as db:
            db.execute("UPDATE RegisteredSystems SET Enabled=1 WHERE ClientId='saml-test'")
        invalid_session = student.request.get(request_url())
        assert '/account/login' in invalid_session.url
        stop()
        start()
        restarted_metadata = admin.request.get(SERVER + '/saml/metadata').text()
        assert ET.fromstring(restarted_metadata).find('.//{http://www.w3.org/2000/09/xmldsig#}X509Certificate').text == original_metadata_cert
        print('PASS disabled SP/account, revoked session, audit and persistent signing key')
        browser.close()
finally:
    stop()
    print('Isolated SAML service stopped.')
