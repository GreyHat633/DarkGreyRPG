from live_helpers import *
call('close');call('openhost',host='B');ready();select('GreyHat_:TarvenBoss');button(1);ready();a=audit();assert a['npc'][0]['host']['entityUuid']==a['B'];shot('05-rebind-B');print('GATE_C_ORPHAN_RECOVERY=PASS')
# Transfer from live B to newly spawned A, fixture refresh preserves previous B binding until explicit release below.
button(3);time.sleep(.2);modal();call('close');call('fixture0323');time.sleep(.3);call('hotbar',slot=0)
call('openhost',host='A');ready();select('GreyHat_:TarvenBoss');button(1);ready();call('close');call('openhost',host='B');ready();select('GreyHat_:TarvenBoss');button(1);ready();time.sleep(.2)
assert state()['nominator']['modal']=='transfer';shot('06-NPC-transfer-modal');before=audit();modal(False);assert audit()['npc']==before['npc'];button(1);ready();time.sleep(.2);modal();a=audit();assert a['npc'][0]['host']['entityUuid']==a['B'];shot('07-NPC-transfer-B');print('GATE_D_TRANSFER_CONFIRM_CANCEL=PASS')
# Seed direct/type memberships, then refresh through reopen; use actual UI for removal.
call('close');call('seedgroups',id='GreyHat_:Slimes');time.sleep(.2);call('openhost',host='B');ready();button(2);ready();a=audit();assert not a['npc'] and len(a['entities'])==1 and a['entities'][0]['entityUuid']==a['A'];assert a['typeGroups'];shot('08-entity-unbind-host-only')
select('GreyHat_:Slimes');button(3);time.sleep(.2);modal();a=audit();assert not a['entities'] and not a['typeGroups'];assert any('GreyHat_:Slimes' in p['actorIds'] for p in a['packages']);shot('09-entity-group-resource-release');print('GATE_E_ENTITY_UNBIND_GROUP_RELEASE=PASS')
call('close');call('itemgui');ready();print(json.dumps(state()['nominator'],ensure_ascii=False));shot('10-item-layout')
