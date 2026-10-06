import hashlib
import json
from pathlib import Path
import zipfile

proof = Path(__file__).resolve().parent
repo = proof.parents[2]
dist = repo / 'dist' / 'DarkGreyRPGStudio'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def digest(path):
    return hashlib.file_digest(path.open('rb'), 'sha256').hexdigest().upper()

before = read(proof / 'data-before.json')
original = {row['Path'].lstrip('\\/').replace('\\', '/'): (row['Size'], row['SHA256'].upper()) for row in before}
now = {path.relative_to(dist / 'Data').as_posix(): (path.stat().st_size, digest(path)) for path in (dist / 'Data').rglob('*') if path.is_file()}
differences = [name for name in sorted(set(original) | set(now)) if original.get(name) != now.get(name)]
manifest = read(dist / 'Docs' / 'StudioProgramFiles.json')
missing = [name for name in manifest if not (dist / name).is_file()]
jar = repo / 'build' / 'libs' / 'darkgrey_rpg-0.3.3.6.jar'
with zipfile.ZipFile(jar) as archive:
    corrupt = archive.testzip()
    metadata = json.loads(archive.read('mcmod.info').decode('utf-8'))
    versions = [mod['version'] for mod in metadata]
exports = {}
for package in sorted((proof / 'RuntimeReferencedExports').glob('*.dgrs*')):
    with zipfile.ZipFile(package) as archive:
        exports[package.name] = {'Bytes': package.stat().st_size, 'SHA256': digest(package), 'CRCValid': archive.testzip() is None}
result = {
    'DataFiles': len(now), 'DataBytes': sum(size for size, _ in now.values()),
    'DataDifferences': differences, 'ManifestEntries': len(manifest), 'MissingProgramFiles': missing,
    'RootFiles': sorted(path.name for path in dist.iterdir() if path.is_file()),
    'RuntimeJarCRCValid': corrupt is None, 'RuntimeVersions': versions,
    'CandidateDllMatchesDist': digest(proof.parent / 'Studio' / 'Program' / 'DarkGreyRPGStudio.dll') == digest(dist / 'Program' / 'DarkGreyRPGStudio.dll'),
    'Exports': exports,
}
(proof / 'final-integrity.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
assert not differences and not missing and corrupt is None and versions == ['0.3.3.6']
assert result['RootFiles'] == ['DarkGreyRPGStudio.exe'] and result['CandidateDllMatchesDist']
assert all(package['CRCValid'] for package in exports.values())
