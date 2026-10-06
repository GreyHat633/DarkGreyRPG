from pathlib import Path
import json,hashlib,os,shutil
out=Path('PLAN/0.3.2.4/evidence/user-review-fixes');changes=json.loads((out/'migration-manifest.json').read_text(encoding='utf8'))
for c in changes:
 assert hashlib.sha256(Path(c['path']).read_bytes()).hexdigest()==c['before_sha256'],c['path']
 assert hashlib.sha256(Path(c['candidate']).read_bytes()).hexdigest()==c['after_sha256']
for c in changes:
 p=Path(c['path']);tmp=p.with_name(p.name+'.0324-description.tmp');assert not tmp.exists();shutil.copyfile(c['candidate'],tmp);os.replace(tmp,p)
for c in changes:assert hashlib.sha256(Path(c['path']).read_bytes()).hexdigest()==c['after_sha256']
(out/'migration-applied.json').write_text(json.dumps(dict(status='APPLIED_VERIFIED',changes=changes,progress='all decompressed NBT bytes unchanged except two validated 64-byte fingerprints'),ensure_ascii=False,indent=2),encoding='utf8')
print('ACTUAL_PROJECT_PACKAGE_SAVE_CORRECTION=PASS')
