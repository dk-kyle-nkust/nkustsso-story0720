"""從 npm 官方封存檔更新本機資源；固定版本並驗證套件完整性，不執行套件腳本。"""
import base64
import hashlib
import io
import json
from pathlib import Path
import tarfile
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
DESTINATION = ROOT / 'NkustSso.Server/wwwroot/lib'
PACKAGES = {
    'jquery': ('4.0.0', {'dist/jquery.min.js': 'jquery.min.js'}),
    'datatables.net': ('3.1.4', {'js/dataTables.min.js': 'dataTables.min.js'}),
    'datatables.net-dt': ('3.1.4', {
        'js/dataTables.dataTables.min.js': 'dataTables.dataTables.min.js',
        'css/dataTables.dataTables.min.css': 'dataTables.dataTables.min.css'}),
    'tom-select': ('2.6.2', {
        'dist/js/tom-select.complete.min.js': 'tom-select.complete.min.js',
        'dist/css/tom-select.min.css': 'tom-select.min.css'}),
}

manifest = {}
for name, (version, files) in PACKAGES.items():
    with urlopen(f'https://registry.npmjs.org/{name}/{version}', timeout=30) as response:
        metadata = json.load(response)
    distribution = metadata['dist']
    if not distribution['tarball'].startswith('https://registry.npmjs.org/'):
        raise RuntimeError('套件下載位址不是 npm 官方來源。')
    with urlopen(distribution['tarball'], timeout=30) as response:
        data = response.read()
    algorithm, expected = distribution['integrity'].split('-', 1)
    actual = base64.b64encode(hashlib.new(algorithm, data).digest()).decode('ascii')
    if actual != expected:
        raise RuntimeError('套件完整性驗證失敗。')
    with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
        for source, target in files.items():
            resource = archive.extractfile('package/' + source)
            if resource is None:
                raise RuntimeError('找不到指定資源。')
            (DESTINATION / target).write_bytes(resource.read())
        licenses = [m for m in archive.getmembers() if m.name.lower() in ('package/license', 'package/license.txt', 'package/license.md')]
        for license_file in licenses:
            (DESTINATION / (name + '.LICENSE.txt')).write_bytes(archive.extractfile(license_file).read())
    manifest[name] = {'version': version, 'integrity': distribution['integrity'], 'source': distribution['tarball']}
    print(f'Updated {name} {version}')
(DESTINATION / 'versions.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
