from live_driver import *
def ready():return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def audit():call('audit0323');time.sleep(.15);return state()['audit']
def choose(id):
 u=state()['nominator'];call('click',x=u['x']+5,y=u['y']+6);call('key',text=id,key=0);time.sleep(.15);u=state()['nominator'];i=next(i for i,r in enumerate(u['rows']) if r['id']==id);call('click',x=u['x']+u['split']+10,y=u['y']+34+i*20+8)
def modal(left=True):
 u=state()['nominator'];b=u['left' if left else 'right'];call('click',x=b['xPosition']+b['width']//2,y=b['yPosition']+10);return ready()
def openhost(host):call('close');call('openhost',host=host);ready();choose('GreyHat_:TarvenBoss')
call('close');call('fixture0323');time.sleep(.3);call('hotbar',slot=0);openhost('A');button(3);modal();button(1);ready();a=audit();assert a['npc'][0]['host']['entityUuid']==a['A']
call('removeA');time.sleep(.2);assert audit()['Adead'];button(3);modal();assert not audit()['npc'];shot('17-orphan-release')
openhost('B');button(1);ready();assert audit()['npc'][0]['host']['entityUuid']==audit()['B'];button(3);modal()
call('close');call('fixture0323');time.sleep(.3);openhost('A');button(1);ready();openhost('B');button(1);ready();assert state()['nominator']['modal']=='transfer';before=audit()['npc'];modal(False);assert audit()['npc']==before;button(1);ready();modal();assert audit()['npc'][0]['host']['entityUuid']==audit()['B'];shot('18-transfer-confirmed')
call('close');call('seedgroups',id='GreyHat_:Slimes');time.sleep(.2);call('openhost',host='B');ready();button(2);ready();a=audit();assert not a['npc'] and len(a['entities'])==1 and a['typeGroups'];choose('GreyHat_:Slimes');button(3);modal();a=audit();assert not a['entities'] and not a['typeGroups'];shot('19-group-release')
print('LIVE_NPC_TRANSFER_ORPHAN_RELEASE_HOST_UNBIND_GROUP_RELEASE=PASS')
