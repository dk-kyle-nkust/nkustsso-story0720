"""Disposable HTTPS receiver for browser navigation tests; never shipped with Server."""
import base64
from datetime import datetime, timedelta, timezone
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import secrets
import ssl
import threading
from urllib.parse import parse_qs, urlencode, urlparse
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID


class OidcBrowserReceiver:
    def __init__(self, server, directory):
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass  # 回呼帶有一次性授權碼，不寫到測試輸出。

            def do_GET(self):
                parsed = urlparse(self.path)
                if parsed.path == '/public':
                    self.send_response(200)
                    self.send_header('Content-Type', 'text/html; charset=utf-8')
                    self.end_headers()
                    self.wfile.write(b'<h1>External service</h1>')
                    return
                if parsed.path.endswith('/callback'):
                    valid = bool(parse_qs(parsed.query).get('code'))
                    self.send_response(200 if valid else 400)
                    self.send_header('Content-Type', 'text/html; charset=utf-8')
                    self.end_headers()
                    self.wfile.write(b'<h1>Service ready</h1>' if valid else b'Missing code')
                    return
                client = parsed.path.strip('/').split('/')[0]
                if client not in ('academic', 'equipment'):
                    self.send_error(404)
                    return
                verifier = secrets.token_urlsafe(40)
                challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('=')
                query = {'client_id': client, 'redirect_uri': owner.url + '/' + client + '/callback',
                         'response_type': 'code', 'scope': 'openid', 'code_challenge': challenge,
                         'code_challenge_method': 'S256', 'state': secrets.token_urlsafe(24), 'nonce': secrets.token_urlsafe(24)}
                self.send_response(302)
                self.send_header('Location', server + '/connect/authorize?' + urlencode(query))
                self.end_headers()

        key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'localhost')])
        certificate = x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key()).serial_number(
            x509.random_serial_number()).not_valid_before(datetime.now(timezone.utc) - timedelta(minutes=1)).not_valid_after(
            datetime.now(timezone.utc) + timedelta(days=1)).add_extension(
            x509.SubjectAlternativeName([x509.DNSName('localhost')]), critical=False).sign(key, hashes.SHA256())
        pem = directory / 'receiver.pem'
        pem.write_bytes(certificate.public_bytes(serialization.Encoding.PEM) + key.private_bytes(
            serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
        self.http = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.url = f'https://localhost:{self.http.server_port}'
        tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        tls.load_cert_chain(pem)
        self.http.socket = tls.wrap_socket(self.http.socket, server_side=True)
        self.thread = threading.Thread(target=self.http.serve_forever, daemon=True)
        self.thread.start()

    def close(self):
        self.http.shutdown()
        self.http.server_close()
        self.thread.join(timeout=3)
