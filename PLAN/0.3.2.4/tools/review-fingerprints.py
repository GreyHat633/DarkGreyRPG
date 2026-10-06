from pathlib import Path
import json,hashlib,struct,zipfile,gzip
root=Path.cwd(); out=root/'PLAN/0.3.2.4/evidence/user-review-fixes'
entry='resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json'
old=root/'run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs'; new=root/'PLAN/0.3.2.4/evidence/kill_slimes-description-corrected.dgrs'
def taskfp(d):
 s=str(d['schema_version'])+'|'+d['resource_kind'].upper()+'|'+d['id']+'|'+d['display_name']
 for n in d['graph']['nodes']:
  s+='|N:'+n['id']+':'+n['type']+':'+n['display_name']
  for p in n['ports']:s+='|P:'+p['port_id']+':'+p['direction'].upper()+':'+p['kind'].upper()+':'+str(p['order'])+':'+p['display_name']
  for k,v in n['properties'].items():s+='|K:'+k+'='+json.dumps(v,ensure_ascii=False,separators=(',',':'))
 for e in d['graph']['connections']:s+='|E:'+e['from_node_id']+':'+e['from_port_id']+'->'+e['to_node_id']+':'+e['to_port_id']+':'+e['interface_kind'].upper()
 return hashlib.sha256(s.encode()).hexdigest()
def packagefp(z):
 m=json.loads(z.read('manifest.json'));h=hashlib.sha256(b'DGR-PACKAGE-CONTENT-FINGERPRINT-V1\0')
 def text(v):
  b=str(v).encode();h.update(struct.pack('>i',len(b)));h.update(b)
 for k in ('format','format_version','schema_version','story_schema_version'):text(m.get(k,''))
 records=[('project','project.json')]
 roles={'story':'story','actors':'actor','items':'item','item_groups':'item_group','dialogues':'dialogue','quests':'quest','canonical_stories':'canonical_story','canonical_memberships':'canonical_membership','sessions':'session','tasks':'task','story_logic_graph':'story_logic_graph'}
 for k,v in m['required_resources'].items():
  if v is None:continue
  for path in v if isinstance(v,list) else [v]:records.append((roles[k],path))
 for role,path in sorted(records):
  b=z.read(path);text(role);text(path);h.update(struct.pack('>q',len(b)));h.update(b)
 return h.hexdigest()
with zipfile.ZipFile(old) as a,zipfile.ZipFile(new) as b:
 oldtask=taskfp(json.loads(a.read(entry)));newtask=taskfp(json.loads(b.read(entry)))
 oldpack=packagefp(a);newpack=packagefp(b)
print(oldtask,newtask,oldpack,newpack)
assert oldtask=='33703e6c2b4d70dbedf6c0068ef51c3b5b28a4dedd35ea57ebf5f3dd8fa11704'
assert oldpack=='7af7283bbbedd7b9732a5a8072831d761dc102f9ca92fc29e26fdcf2098dee97'
(out/'fingerprints.json').write_text(json.dumps(dict(oldtask=oldtask,newtask=newtask,oldpack=oldpack,newpack=newpack),indent=2))
