from live_driver import *
call('start',world='DGR0324UserReview');wait(lambda s:s.get('screen')=='world',30);time.sleep(2);call('tasks');s=state();assert '与酒馆老板对话' in s['tasks'],s;assert 'current:0' in s['tasks'];print('USER_WORLD_NEXT_OBJECTIVE_VISIBLE=PASS');print(shot('review-user-world-next-objective'));call('serverstate');time.sleep(.4)
(ROOT/'review-user-world-state.json').write_text(json.dumps(s,ensure_ascii=False,indent=2),encoding='utf8')
call('key',key=1);call('itemgui');time.sleep(.5)
if 'windowGeometry' not in state():
 call('command',text='/give @p darkgrey_rpg:nominator 1');call('hotbar',slot=0);time.sleep(.5);call('itemgui')
wait(lambda s:s.get('nominator',{}).get('initialized'));call('resize',width=1920,height=1030);call('scale',value=4);time.sleep(1);print(shot('review-item-final'));print(json.dumps(state()['windowGeometry']))
