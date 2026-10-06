from live_driver import *
call('start');wait(lambda s:s.get('screen')=='world',30);call('fixture0323');time.sleep(1);call('hotbar',slot=0);call('itemgui');time.sleep(.6);wait(lambda s:s.get('nominator',{}).get('initialized'));call('resize',width=1920,height=1030);call('scale',value=4);time.sleep(.5)
u=state()['nominator'];call('click',x=u['x']+12,y=u['y']+40+20+8);time.sleep(.3);s=state();b=next(b for b in s['buttons'] if b['id']==7);assert b['yPosition']+b['height']<s['nominator']['y']-1
slots=s['nominator']['slots'];unbind=next(b for b in s['buttons'] if b['id']==8);assert unbind['yPosition']-(slots[1]['y']+17)>=4
shot('review-delivery-item');call('key',key=1);call('openhost',host='A');time.sleep(.7);u=state()['nominator'];call('click',x=u['x']+12,y=u['y']+68);shot('review-delivery-entity');call('key',key=1)
call('tasks');shot('review-delivery-task');call('key',key=1)
print('FINAL_LAYOUT_TITLE_GAP_SLOT_GAP=PASS')
