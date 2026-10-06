from pathlib import Path
import gzip,struct,json,zipfile,hashlib
root=Path.cwd(); out=root/'PLAN/0.3.2.4/evidence/user-review-fixes'; out.mkdir(exist_ok=True)
def parse(path):
 data=gzip.decompress(path.read_bytes()); pos=0
 def read(n):
  nonlocal pos
  x=data[pos:pos+n]; pos+=n; return x
 def num(f): return struct.unpack('>'+f,read(struct.calcsize('>'+f)))[0]
 def string(): return read(num('H')).decode('utf8')
 def payload(t):
  if t in range(1,7): return num({1:'b',2:'h',3:'i',4:'q',5:'f',6:'d'}[t])
  if t==7:return list(read(num('i')))
  if t==8:return string()
  if t==9:
   v=num('b');return [payload(v) for _ in range(num('i'))]
  if t==10:
   d={}
   while True:
    v=num('b')
    if not v:return d
    n=string(); d[n]=payload(v)
  if t in (11,12):return [num('i' if t==11 else 'q') for _ in range(num('i'))]
  raise ValueError(t)
 t=num('b'); name=string();return payload(t)
for name in ['darkgrey_rpg_canonical_tasks','darkgrey_rpg_story_package_generations']:
 file=root/'run/client/saves/新的世界/data'/(name+'.dat')
 result=parse(file); (out/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8'); print(json.dumps(result,ensure_ascii=False))
f=root/'run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs'
print('PACKAGE_SHA256',hashlib.sha256(f.read_bytes()).hexdigest())
with zipfile.ZipFile(f) as z:
 for name in z.namelist():
  if '/tasks/' in name and name.endswith('.json'):
   d=json.loads(z.read(name));print(json.dumps(d,ensure_ascii=False))
