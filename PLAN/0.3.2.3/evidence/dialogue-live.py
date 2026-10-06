from live_helpers import *
for j in range(6):
 s=state()
 if s.get('modelFrame',{}).get('kind')=='CHOICE':break
 call('click',x=5,y=5);time.sleep(.35)
s=state();assert s['modelFrame']['kind']=='CHOICE';shot('26-choice-gray');before=s['modelFrame'];call('key',key=1);s=state();assert s['screen']=='net.minecraft.client.gui.GuiIngameMenu' and s['modelFrame']==before;shot('27-pause-preserved');button(4);time.sleep(.3);call('inventory');call('click',x=200,y=80);assert state()['modelFrame']==before;shot('28-dialogue-underlay');call('close');time.sleep(.3)
call('die');s=wait(lambda s:'GuiGameOver' in s['screen']);shot('29-death');time.sleep(1.5);button(0);s=wait(lambda s:'GuiCanonicalSessionScreen' in s['screen']);assert s['modelFrame']['currentNodeId']==before['currentNodeId'];shot('30-respawn-preserved')
call('quit');wait(lambda s:'GuiMainMenu' in s['screen']);call('start');s=wait(lambda s:'GuiCanonicalSessionScreen' in s['screen']);assert s['modelFrame']['currentNodeId']==before['currentNodeId'];shot('31-reconnect-preserved');print('DIALOGUE_ESC_UNDERLAY_DEATH_RECONNECT=PASS')
print(json.dumps(s.get('buttons'),ensure_ascii=False))
