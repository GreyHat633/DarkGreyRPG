from live_driver import *
results={}
for kind,op in [('item','itemgui'),('entity','entitygui'),('task','tasks'),('copier','copier')]:
 call('close')
 if kind=='copier':call('copiersetup');time.sleep(.2)
 call(op);s=wait(lambda s:'windowGeometry' in s)
 if kind in ('item','entity'):s=wait(lambda s:s.get('nominator',{}).get('initialized'))
 g=s['windowGeometry'];sx=g['x']+g['width']-2;sy=g['y']+g['height']-2
 path=ROOT/'live-client/config/darkgrey-rpg-windows.properties';stamp=path.stat().st_mtime_ns
 call('down',x=sx,y=sy);fps=[]
 for i in range(30):
  s=call('move',x=sx+(i%10)*3,y=sy+(i%10)*2);fps.append(s['fps'])
 assert path.stat().st_mtime_ns==stamp
 call('up',x=sx+27,y=sy+18);results[kind]=dict(fps_min=min(fps),fps_max=max(fps),no_disk_during_resize=True)
 if kind=='copier':
  button(200);before=state()['windowGeometry'];call('down',x=before['x']+20,y=before['y']+8);call('move',x=before['x']+70,y=before['y']+30);call('up',x=before['x']+70,y=before['y']+30);assert state()['windowGeometry']==before;call('key',key=1);assert 'GuiCopierTemplates' in state()['screen']
call('close');(ROOT/'resize-performance.json').write_text(json.dumps(results,indent=2));print(json.dumps(results));print('RESIZE_PERFORMANCE_AND_COPIER_CONFIRM_ARBITRATION=PASS')
