from live_driver import *
def ready(): return wait(lambda s:s.get('nominator',{}).get('initialized') and not s['nominator']['pending'])
def slot(i):
 s=state()['nominator']['slots'][i];call('click',x=s['x']+8,y=s['y']+8);time.sleep(.15)
def select(id):
 u=state()['nominator'];i=next(i for i,r in enumerate(u['rows']) if r['id']==id);call('click',x=u['x']+u['split']+10,y=u['y']+34+i*20-int(u['resourceMotion']['position'])+8)
s=ready();slot(34);slot(39);assert state()['nominator']['slots'][39]['stack']=='';slot(38);assert 'helmetIron' in state()['nominator']['slots'][38]['stack'];shot('05-real-armor-equipped')
slot(38);slot(34);assert state()['nominator']['slots'][38]['stack']=='' and 'helmetIron' in state()['nominator']['slots'][34]['stack']
slot(31);slot(0);select('GreyHat_:TEST');before=state()['nominator'];button(6);s=ready();assert s['nominator']['window']==before['window'] and s['nominator']['slots'][0]['stack']==''
select('GreyHat_:TestItemGroup');slot(32);slot(0);button(6);s=state();assert s['nominator']['modal']=='group';g=s['windowGeometry'];call('down',x=g['x']+25,y=g['y']+8);call('move',x=g['x']+70,y=g['y']+28);call('up',x=g['x']+70,y=g['y']+28);assert state()['windowGeometry']==g
call('key',key=1);assert state()['nominator']['slots'][0]['stack'];call('key',key=1)
call('itemgui');s=ready();assert s['nominator']['slots'][0]['stack']=='';call('typedkey',key=19,text='r');assert 'GuiNominatorInventory' not in state()['screen'];call('itemgui');s=ready()
u=s['nominator'];call('click',x=u['x']+10,y=u['y']+6);call('typedkey',key=19,text='r');assert state()['nominator']['search']=='r';call('key',key=1)
call('tasks');s=state();assert all('Esc' not in b.get('displayString','') for b in s['buttons']);call('typedkey',key=19,text='r');assert 'GuiCanonicalTaskScreen' not in state()['screen']
print('ARMOR_PLACE_REMOVE_VALIDITY=PASS;ITEM_BATCH_MODAL_CLOSE_RETURN=PASS;TASK_REMAP_CLOSE=PASS')
