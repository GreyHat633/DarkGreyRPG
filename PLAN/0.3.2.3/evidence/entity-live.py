import sys,json,time
sys.path.insert(0,'.tooling/0.3.2.3')
from live_driver import *
def audit():
 call('audit0323');time.sleep(.2);return state()['audit']
def select(id):
 u=state()['nominator'];idx=next(i for i,r in enumerate(u['rows']) if r['id']==id)
 call('click',x=u['x']+u['split']+12,y=u['y']+34+(idx-u['resourceScroll'])*20+8)
def ready():return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def modal(left=True):
 u=state()['nominator'];b=u['left' if left else 'right'];call('click',x=b['xPosition']+b['width']//2,y=b['yPosition']+10);return ready()
call('removeA');time.sleep(.3);a=audit();assert a['Adead'] and a['npc'][0]['host']['entityUuid']==a['A'];shot('02-dead-host-still-occupied')
button(3);time.sleep(.2);shot('03-orphan-release-confirm');modal();a=audit();assert not a['npc'] and not a['entities'];shot('04-orphan-released')
call('close');call('openhost',host='B');ready();select('GreyHat_:TarvenBoss');button(1);ready();a=audit();assert a['npc'][0]['host']['entityUuid']==a['B'];shot('05-rebind-B');print('GATE_C_ORPHAN_RECOVERY=PASS')
# Transfer from live B to newly spawned A, fixture refresh preserves previous B binding until explicit release below.
button(3);time.sleep(.2);modal();call('close');call('fixture0323');time.sleep(.3);call('hotbar',slot=0)
call('openhost',host='A');ready();select('GreyHat_:TarvenBoss');button(1);ready();call('close');call('openhost',host='B');ready();select('GreyHat_:TarvenBoss');button(1);ready();time.sleep(.2)
assert state()['nominator']['modal']=='transfer';shot('06-NPC-transfer-modal');before=audit();modal(False);assert audit()['npc']==before['npc'];button(1);ready();time.sleep(.2);modal();a=audit();assert a['npc'][0]['host']['entityUuid']==a['B'];shot('07-NPC-transfer-B');print('GATE_D_TRANSFER_CONFIRM_CANCEL=PASS')
# Seed direct/type memberships, then refresh through reopen; use actual UI for removal.
call('close');call('seedgroups',id='GreyHat_:Slimes');time.sleep(.2);call('openhost',host='B');ready();button(2);ready();a=audit();assert not a['npc'] and len(a['entities'])==1 and a['entities'][0]['entityUuid']==a['A'];assert a['typeGroups'];shot('08-entity-unbind-host-only')
select('GreyHat_:Slimes');button(3);time.sleep(.2);modal();a=audit();assert not a['entities'] and not a['typeGroups'];assert any('GreyHat_:Slimes' in p['actorIds'] for p in a['packages']);shot('09-entity-group-resource-release');print('GATE_E_ENTITY_UNBIND_GROUP_RELEASE=PASS')
call('close');call('itemgui');ready();print(json.dumps(state()['nominator'],ensure_ascii=False));shot('10-item-layout')
