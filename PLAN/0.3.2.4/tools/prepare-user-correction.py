from pathlib import Path
import json,gzip,hashlib,shutil,zipfile,copy
root=Path.cwd();out=root/'PLAN/0.3.2.4/evidence/user-review-fixes';stage=out/'migration-candidate';backup=out/'backup';stage.mkdir(exist_ok=True);backup.mkdir(exist_ok=True)
fp=json.loads((out/'fingerprints.json').read_text());changes=[]
def prepare(source,name,data):
 before=source.read_bytes();target=stage/name;target.write_bytes(data);shutil.copy2(source,backup/name)
 changes.append(dict(path=str(source),candidate=str(target),backup=str(backup/name),before_sha256=hashlib.sha256(before).hexdigest(),after_sha256=hashlib.sha256(data).hexdigest()))
source=root/'run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs';corrected=root/'PLAN/0.3.2.4/evidence/kill_slimes-description-corrected.dgrs'
entry='resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json'
with zipfile.ZipFile(source) as a,zipfile.ZipFile(corrected) as b:
 assert a.namelist()==b.namelist();assert [n for n in a.namelist() if a.read(n)!=b.read(n)]==[entry]
 old=json.loads(a.read(entry));new=json.loads(b.read(entry));check=copy.deepcopy(new);next(n for n in check['graph']['nodes'] if n['id']=='node_4feb6ff8c4944e078f9914cd808c9f17')['properties']['description']='消灭史莱姆';assert check==old
prepare(source,'kill_slimes.dgrs',corrected.read_bytes())
source=Path('E:/Java/MinecraftMod/RPGProject/TestProject')/entry
assert json.loads(source.read_text(encoding='utf-8-sig'))==old
prepare(source,'KillSlimes-source.json',json.dumps(new,ensure_ascii=False,indent=2).encode('utf8'))
for name,key in [('darkgrey_rpg_canonical_tasks','task'),('darkgrey_rpg_story_package_generations','pack')]:
 source=root/'run/client/saves/新的世界/data'/(name+'.dat');raw=gzip.decompress(source.read_bytes());oldhash=fp['old'+key].encode();newhash=fp['new'+key].encode();assert raw.count(oldhash)==1
 after=raw.replace(oldhash,newhash);assert after.replace(newhash,oldhash)==raw
 prepare(source,name+'.dat',gzip.compress(after,mtime=0))
(out/'migration-manifest.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf8');print('CANDIDATES_READY',len(changes))

