from live_driver import *
call('scale',value=2)
results={}
def ready():return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def check(kind,op):
 call(op);time.sleep(.5);wait(lambda s:'windowGeometry' in s)
 if kind in ('item','entity'):ready()
 for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
  g=state()['windowGeometry'];px=g['x']+(2 if sx<0 else g['width']-2);py=g['y']+(2 if sy<0 else g['height']-2)
  call('down',x=px,y=py);call('move',x=px+sx*10,y=py+sy*10);call('up',x=px+sx*10,y=py+sy*10)
  n=state()['windowGeometry'];assert n['width']==g['width']+10 and n['height']==g['height']+10,(kind,g,n)
  assert (n['x']+n['width'] if sx<0 else n['x'])==(g['x']+g['width'] if sx<0 else g['x'])
  assert (n['y']+n['height'] if sy<0 else n['y'])==(g['y']+g['height'] if sy<0 else g['y'])
 results[kind]='FOUR_CORNERS_OPPOSITE_ANCHOR_PASS'
 if kind in ('item','entity'):
  s=ready();u=s['nominator'];call('click',x=u['x']+u['split']+10,y=u['y']+48);time.sleep(.1);g=state()['windowGeometry'];button(7 if kind=='item' else 3);s=state();assert s['nominator']['modal']=='release',s;assert s['windowGeometry']==g
  call('key',key=1)
 call('key',key=1)
check('item','itemgui');check('entity','entitygui');check('task','tasks')
call('copiersetup');time.sleep(.4);check('copier','copier')
call('copier');call('remapinventory',key=19);call('typedkey',key=19,text='r');assert state()['screen']=='world';results['copier_inventory_key']='REMAPPED_R_CLOSE_PASS'
print(json.dumps(results));(ROOT/'review-input-results.json').write_text(json.dumps(results,indent=2))
call('shutdown')

