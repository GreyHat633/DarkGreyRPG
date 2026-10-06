from live_driver import *
def ready(): return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def select(id):
 u=state()['nominator']; i=next(i for i,r in enumerate(u['rows']) if r['id']==id); y=u['y']+34+i*20-int(u['resourceMotion']['position'])+8
 call('click',x=u['x']+u['split']+10,y=y)
def drag_to(w,h,dx,dy):
 s=state();g=s['windowGeometry'];call('down',x=g['x']+g['width']-3,y=g['y']+g['height']-3)
 call('move',x=g['x']+w-3,y=g['y']+h-3);call('up',x=g['x']+w-3,y=g['y']+h-3)
 g=state()['windowGeometry'];call('down',x=g['x']+30,y=g['y']+8);call('move',x=g['x']+30+dx,y=g['y']+8+dy);call('up',x=g['x']+30+dx,y=g['y']+8+dy)
 return state()
s=ready();select('GreyHat_:TarvenBoss');button(1);s=ready();assert s['nominator']['message'];shot('02-entity-bound')
call('remapinventory',key=19);u=state()['nominator'];call('click',x=u['x']+12,y=u['y']+7);call('typedkey',key=19,text='r');assert state()['nominator']['search']=='r'
call('key',key=1);call('openhost',host='A');ready();call('typedkey',key=19,text='r');assert 'NominatorEntity' not in state()['screen']
call('openhost',host='A');ready();s=drag_to(380,250,60,45);g=s['windowGeometry'];assert g['width']==380 and g['height']==250 and g['x']>5;shot('03-entity-drag-resize')
call('key',key=1);call('openhost',host='A');s=ready();assert s['windowGeometry']['x']==g['x'] and s['windowGeometry']['width']==380
call('key',key=1);call('itemgui');s=ready();assert len(s['nominator']['slots'])==42;shot('04-item-layout')
print('ENTITY_BIND_FOCUS_REMAP_DRAG_RESIZE_REOPEN=PASS;ITEM_SLOTS=42')
