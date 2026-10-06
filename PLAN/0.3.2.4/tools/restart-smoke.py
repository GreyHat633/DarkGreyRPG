from live_driver import *
expected=json.loads((ROOT/'layout-expected.json').read_text())
results={}
for kind,op in [('copier','copier'),('task','tasks'),('item','itemgui'),('entity','entitygui')]:
 call('close');call(op);s=wait(lambda s:'windowGeometry' in s)
 if kind in ('item','entity'):s=wait(lambda s:s.get('nominator',{}).get('initialized'))
 g=s['windowGeometry'];assert all(g[k]==v for k,v in expected[kind]['rect'].items()),(kind,g,expected[kind])
 results[kind]='PROCESS_RESTART_RESTORED'
 shot('13-'+kind+'-after-process-restart')
 call('resize',width=640,height=480);s=state();g=s['windowGeometry'];assert g['x']>=0 and g['y']>=0 and g['x']+g['width']<=s['width'] and g['y']+g['height']<=s['height'];shot('14-'+kind+'-640x480')
 call('resize',width=1100,height=740)
call('close');(ROOT/'restart-results.json').write_text(json.dumps(results,indent=2));print(json.dumps(results));print('FOUR_WINDOW_RESTART_RESOLUTION_RECOVERY=PASS')
