from live_driver import *
s=state();before=s['modelFrame'];call('key',key=1);s=state();assert s['screen']=='net.minecraft.client.gui.GuiIngameMenu' and s['modelFrame']==before;shot('07-dialogue-pause');button(4);call('inventory');call('click',x=200,y=80);assert state()['modelFrame']==before;shot('08-dialogue-underlay');call('close')
call('die');s=wait(lambda s:'GuiGameOver' in s['screen']);time.sleep(1.5);button(0);s=wait(lambda s:'GuiCanonicalSessionScreen' in s['screen']);assert s['modelFrame']['currentNodeId']==before['currentNodeId']
call('quit');wait(lambda s:'GuiMainMenu' in s['screen']);call('start');s=wait(lambda s:'GuiCanonicalSessionScreen' in s['screen'],30);assert s['modelFrame']['currentNodeId']==before['currentNodeId'];shot('09-dialogue-reconnect')
button(3000);time.sleep(.3)
for i in range(15):
 s=state()
 if not s.get('modelFrame'):break
 assert s['modelFrame']['kind']!='CHOICE',s['modelFrame']
 call('click',x=5,y=5);time.sleep(.3)
call('tasks');s=state();assert 'required:3' in s['tasks'];shot('10-task-kill-active');call('serverstate')
for i in range(3):
 call('slime',actor='GreyHat_:Slimes');time.sleep(.3);call('kill',actor='GreyHat_:Slimes');time.sleep(.6)
s=wait(lambda s:'与酒馆老板对话' in s.get('tasks',''));assert 'GuiCanonicalTaskScreen' in s['screen'];shot('11-task-interact-active');call('serverstate')
print('DIALOGUE_LAYERING_DEATH_RECONNECT=PASS;LIVE_TASK_TRANSITION_AND_DESCRIPTION=PASS')
