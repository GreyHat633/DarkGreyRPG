from live_driver import *
import pathlib
settings=ROOT/'live-client/config/darkgrey-rpg-windows.properties'
results={}
def geometry_check(kind,openop,width,height,dx,dy):
 call('close');call(openop);wait(lambda s:'windowGeometry' in s)
 if kind in ('entity','item'):wait(lambda s:s.get('nominator',{}).get('initialized'))
 g=state()['windowGeometry'];call('down',x=g['x']+g['width']-2,y=g['y']+g['height']-2);call('move',x=g['x']+width-2,y=g['y']+height-2);call('up',x=g['x']+width-2,y=g['y']+height-2)
 g=state()['windowGeometry'];assert g['width']==width and g['height']==height,(kind,g)
 filetime=settings.stat().st_mtime_ns;call('down',x=g['x']+25,y=g['y']+8);fps=[]
 for i in range(30):
  s=call('move',x=g['x']+25+int(dx*(i+1)/30),y=g['y']+8+int(dy*(i+1)/30));fps.append(s['fps'])
 assert settings.stat().st_mtime_ns==filetime,'disk write during drag'
 call('up',x=g['x']+25+dx,y=g['y']+8+dy);s=state();g=s['windowGeometry'];shot('12-'+kind+'-custom-layout')
 if kind=='item':
  slots=s['nominator']['slots'];assert slots[2]['x']==slots[29]['x'];assert slots[0]['x']<slots[2]['x'];assert slots[38]['x']>slots[10]['x'];assert len(slots)==42
 call('close');call(openop);s=wait(lambda s:'windowGeometry' in s);actual=s['windowGeometry'];assert all(actual[k]==g[k] for k in ('x','y','width','height')),(kind,actual,g)
 results[kind]=dict(rect={k:g[k] for k in ('x','y','width','height')},fps_min=min(fps),fps_max=max(fps),no_disk_during_drag=True)
call('command',text='/give @p darkgrey_rpg:nominator 1');time.sleep(.3);call('hotbar',slot=0)
geometry_check('entity','entitygui',350,220,40,30)
geometry_check('item','itemgui',308,240,45,30)
geometry_check('task','tasks',340,220,50,50)
call('close');call('copiersetup');time.sleep(.3)
geometry_check('copier','copier',300,210,35,45)
(ROOT/'layout-expected.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
call('close');print(json.dumps(results));print('FOUR_WINDOW_DRAG_RESIZE_SAVE_REOPEN=PASS')
