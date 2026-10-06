from live_driver import *
def ready():return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def choose(id):
 u=state()['nominator'];call('click',x=u['x']+5,y=u['y']+6)
 call('key',key=207)
 for c in u['search']:call('key',key=14)
 call('key',text=id,key=0);time.sleep(.15);u=state()['nominator'];call('click',x=u['x']+u['split']+10,y=u['y']+42)
def slot(i):
 p=state()['nominator']['slots'][i];call('click',x=p['x']+8,y=p['y']+8);time.sleep(.1)
def put(damage,target=0):
 i=next(p['slot'] for p in state()['nominator']['slots'] if p['slot']>=2 and 'swordIron@'+str(damage) in p['stack']);slot(i);slot(target)
def modal(left=True):
 b=state()['nominator']['left' if left else 'right'];call('click',x=b['xPosition']+b['width']//2,y=b['yPosition']+10);return ready()
def audit():call('audit0323');time.sleep(.15);return state()['audit']
call('close');call('fixture0323');time.sleep(.3);call('hotbar',slot=0);call('itemgui');ready();choose('GreyHat_:TEST');button(7);modal();put(1);button(6);ready();assert 'GreyHat_:TEST' in audit()['matches']['sword1Items']
put(2);button(6);ready();assert state()['nominator']['modal']=='transfer';modal(False);assert state()['nominator']['slots'][0]['stack'];assert 'GreyHat_:TEST' in audit()['matches']['sword1Items'];button(6);ready();modal();assert 'GreyHat_:TEST' in audit()['matches']['sword2Items'];shot('20-item-transfer')
choose('GreyHat_:TestItemGroup');put(1);button(6);assert state()['nominator']['modal']=='group';modal();a=audit();assert 'GreyHat_:TestItemGroup' in a['matches']['sword1Groups'] and 'GreyHat_:TestItemGroup' not in a['matches']['sword2Groups']
put(1);button(6);modal(False);a=audit();assert 'GreyHat_:TestItemGroup' in a['matches']['sword2Groups'];shot('21-item-exact-fuzzy')
put(2,1);button(8);ready();a=audit();assert 'GreyHat_:TEST' not in a['matches']['sword2Items'] and 'GreyHat_:TestItemGroup' not in a['matches']['sword2Groups'];shot('22-item-unbind')
choose('GreyHat_:TestItemGroup');button(7);modal();assert 'GreyHat_:TestItemGroup' not in audit()['matches']['sword1Groups'];print('LIVE_ITEM_TRANSFER_EXACT_FUZZY_UNBIND_RELEASE_BATCH=PASS')

