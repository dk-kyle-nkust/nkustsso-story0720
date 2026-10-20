"""Server tests use disposable databases; fixtures never ship in the server."""
import atexit
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import time
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
CREATE_FLAGS = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
BUILD_DIRECTORY = Path(os.environ['NKUST_TEST_BUILD_DIRECTORY']).resolve() if os.environ.get('NKUST_TEST_BUILD_DIRECTORY') else None


def seed_server(directory):
    assembly = (BUILD_DIRECTORY / 'NkustSso.StorageChecks.dll' if BUILD_DIRECTORY else
                ROOT / 'tests/NkustSso.StorageChecks/bin/Debug/net10.0/NkustSso.StorageChecks.dll')
    if not assembly.is_file():
        raise RuntimeError('Build NkustSSO.slnx before running browser checks.')
    subprocess.run(['dotnet', str(assembly), '--seed-server', str(directory)],
                   cwd=ROOT, check=True, creationflags=CREATE_FLAGS)
    if os.environ.get('NKUST_TEST_SQLSERVER_CONNECTION'):
        subprocess.run(['dotnet', str(assembly), '--import-sqlserver-fixture', str(directory)],
                       cwd=ROOT, check=True, creationflags=CREATE_FLAGS)


class ServerFixture:
    def __init__(self):
        self.directory = ROOT / '.local/browser-checks' / secrets.token_hex(8)
        self.directory.mkdir(parents=True)
        seed_server(self.directory)
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            self.url = f'https://localhost:{sock.getsockname()[1]}'
        self.log = (self.directory / 'server.log').open('w', encoding='utf-8')
        sql_connection = os.environ.get('NKUST_TEST_SQLSERVER_CONNECTION')
        storage_args = (['--Sso:StorageProvider', 'SqlServer', '--ConnectionStrings:Sso', sql_connection]
                        if sql_connection else ['--Sso:StorageProvider', 'SQLite'])
        self.process = subprocess.Popen([
            'dotnet', str(BUILD_DIRECTORY / 'NkustSso.Server.dll' if BUILD_DIRECTORY else
                          ROOT / 'NkustSso.Server/bin/Debug/net10.0/NkustSso.Server.dll'),
            '--contentRoot', str(ROOT / 'NkustSso.Server'), '--urls', self.url,
            '--Sso:Issuer', self.url + '/', '--Sso:DataDirectory', str(self.directory),
            *storage_args, '--CampusAuthentication:Enabled', 'false', '--ConnectionStrings:LoginAuth', '',
            '--IntegrationServices:SoapEnabled', 'false', '--IntegrationServices:JsonEnabled', 'false'
        ], cwd=ROOT, env={**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development'},
            stdout=self.log, stderr=subprocess.STDOUT, creationflags=CREATE_FLAGS)
        atexit.register(self.close)
        try:
            for _ in range(100):
                if self.process.poll() is not None:
                    raise RuntimeError('Test server exited; inspect its isolated log.')
                try:
                    with urlopen(self.url + '/health', context=ssl._create_unverified_context(), timeout=1):
                        return
                except OSError:
                    time.sleep(.2)
            raise RuntimeError('Test server startup timeout.')
        except BaseException:
            self.close()
            raise

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=5)
        self.log.close()
