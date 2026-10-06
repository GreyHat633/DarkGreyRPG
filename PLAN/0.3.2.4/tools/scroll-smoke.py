from live_driver import *
call('hotbar',slot=1);call('itemgui');s=wait(lambda s:s.get('nominator',{}).get('initialized'))
u=s['nominator'];call('wheel',x=u['x']+6,y=u['y']+38,delta=-120);s=state();assert s['nominator']['packageMotion']['target']>0;time.sleep(.4);assert state()['nominator']['packageMotion']['position']>0
u=state()['nominator'];call('click',x=u['x']+10,y=u['y']+7);call('key',text='GreyHat_',key=0);time.sleep(.2);u=state()['nominator'];assert len(u['rows'])==3
s=call('wheel',x=u['x']+u['split']+10,y=u['y']+40,delta=-120);motion=s['nominator']['resourceMotion'];assert motion['target']>motion['position'];time.sleep(.05)
u=state()['nominator'];y=u['y']+42;i=(8+int(u['resourceMotion']['position']))//20;expected=u['rows'][i]['id'];call('click',x=u['x']+u['split']+12,y=y);s=state();assert s['nominator']['selected']['id']==expected,(expected,s['nominator']['selected']);time.sleep(.45)
u=state()['nominator'];before=dict(search=u['search'],target=u['resourceMotion']['target'],position=u['resourceMotion']['position'],id=u['selected']['id']);button(7);s=state();assert s['nominator']['modal']=='release';b=s['nominator']['left'];call('click',x=b['xPosition']+b['width']//2,y=b['yPosition']+10);s=wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending']);u=s['nominator'];assert u['search']==before['search'] and u['resourceMotion']['target']==before['target'] and u['selected']['id']==before['id'];shot('15-item-smooth-refresh')
call('key',key=1);call('entitygui');s=wait(lambda s:s.get('nominator',{}).get('initialized'));g=s['windowGeometry'];call('down',x=g['x']+g['width']-2,y=g['y']+g['height']-2);call('move',x=g['x']+g['width']-2,y=g['y']+200-2);call('up',x=g['x']+g['width']-2,y=g['y']+200-2)
u=state()['nominator'];call('wheel',x=u['x']+5,y=u['y']+38,delta=-120);time.sleep(.4);u=state()['nominator'];assert u['packageMotion']['position']>0;shot('16-entity-package-scroll')
print('NOMINATOR_LIVE_SMOOTH_CLICK_CLIP_REFRESH=PASS')
