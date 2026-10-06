from live_helpers import *
call('start');s=wait(lambda s:s.get('modelFrame') is not None);button(3000);time.sleep(.3)
for j in range(8):
 s=state()
 if s.get('modelFrame') is None:break
 if s.get('modelFrame',{}).get('kind')=='CHOICE':break
 call('click',x=5,y=5);time.sleep(.35)
call('tasks');time.sleep(.2);s=state();shot('32-task-gray');assert 'GreyHat_' in s['tasks'];print('TASK_PUSH_CACHE_VISIBLE',s['taskRevision'])
call('close');call('copiersetup');time.sleep(.3);call('copier');shot('33-copier-gray');call('close')
call('fixture0323');time.sleep(.4);call('hotbar',slot=0);call('itemgui');ready()
