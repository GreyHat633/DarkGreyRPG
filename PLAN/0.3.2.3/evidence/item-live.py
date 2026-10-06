from live_helpers import *
def slot(n,b=0):
 u=state()['nominator'];s=u['slots'][n];call('click',x=s['x']+8,y=s['y']+8,button=b)
def putstack(source,target=0):
 name={30:'item.stick@0',31:'item.swordIron@1',32:'item.swordIron@2',33:'item.diamond@0'}[source]
 source=next(s['slot'] for s in ui()['slots'][2:] if name in s['stack'])
 slot(source);slot(target,1);slot(source);time.sleep(.2)
def ui():return state()['nominator']
def pickpackage(index):
 u=ui();call('click',x=u['x']+8,y=u['y']+34+(index-u['packageScroll'])*20+8)
def stable(before):
 after=ready()['nominator'];assert after['window']==before['window'];assert all(after[k]==before[k] for k in ['search','selectedPackage','packageScroll','resourceScroll']);assert after['selected']==before['selected'];return after
# Batch ItemID and conflict cancel/confirm.
select('GreyHat_:CopperCoin');putstack(30);before=ui();button(6);after=stable(before);assert not after['slots'][0]['stack'];a=audit();assert 'GreyHat_:CopperCoin' in a['matches']['stickItems'];shot('11-item-bind-keep-open')
putstack(31);button(6);ready();time.sleep(.2);assert ui()['modal']=='transfer';shot('12-item-transfer-modal');a=audit();modal(False);assert audit()['items']==a['items'] and ui()['slots'][0]['stack'];button(6);ready();time.sleep(.2);modal();a=audit();assert 'GreyHat_:CopperCoin' in a['matches']['sword1Items'] and not a['matches']['stickItems'];assert not ui()['slots'][0]['stack'];shot('13-item-transfer-success');print('GATE_G_H_BATCH_TRANSFER=PASS')
# Global search and package provenance; exact group popup closes on Esc without slot or selection loss.
u=ui();call('click',x=u['x']+40,y=u['y']+8);call('key',text='keys',key=0);time.sleep(.3);u=ui();print('SEARCH',[(r['id'],r['source']['packageId'])for r in u['rows']]);select('Provider:keys');putstack(31);before=ui();button(6);time.sleep(.2);assert ui()['modal']=='group';shot('14-group-match-modal');call('key',key=1);assert ui()['slots'][0]['stack']==before['slots'][0]['stack'];stable(before)
button(6);time.sleep(.2);modal(True);after=stable(before);a=audit();assert 'Provider:keys' in a['matches']['sword1Groups'] and 'Provider:keys' not in a['matches']['sword2Groups'];shot('15-group-exact-batch');print('EXACT_POPUP_ESC_STATE=PASS')
# Fuzzy group membership covers same registry / different damage.
u=ui();call('click',x=u['x']+40,y=u['y']+8);call('key',key=14);call('key',key=14);call('key',key=14);call('key',key=14);call('key',text='tools',key=0);time.sleep(.3);select('Provider:tools');putstack(31);before=ui();button(6);time.sleep(.2);modal(False);stable(before);a=audit();assert 'Provider:tools' in a['matches']['sword1Groups'] and 'Provider:tools' in a['matches']['sword2Groups'];shot('16-group-fuzzy-batch')
# Physical unbind slot removes exact ItemID, exact group and applicable fuzzy rule.
putstack(31,1);before=ui();button(8);stable(before);a=audit();assert not a['matches']['sword1Items'] and not a['matches']['sword1Groups'] and 'Provider:tools' not in a['matches']['sword2Groups'];assert not ui()['slots'][1]['stack'];shot('17-item-unbind-returned');print('GATE_I_J_EXACT_FUZZY_UNBIND=PASS')
# Group release and ItemID release keep browser resources.
putstack(31);button(6);time.sleep(.2);modal(False);before=ui();button(7);time.sleep(.2);shot('18-item-group-release-modal');modal();stable(before);a=audit();assert 'Provider:tools' not in a['matches']['sword1Groups'];assert ui()['selected']['id']=='Provider:tools'
pickpackage(0);select('GreyHat_:CopperCoin');putstack(30);button(6);ready();button(7);time.sleep(.2);modal();assert 'GreyHat_:CopperCoin' not in audit()['matches']['stickItems'];assert ui()['selected']['id']=='GreyHat_:CopperCoin';shot('19-item-id-release');print('GATE_K_ITEM_RELEASES=PASS')
# low resolution geometry / two slots remain visible.
call('resize',width=640,height=480);time.sleep(.4);shot('20-item-lowres');print(json.dumps(ui(),ensure_ascii=False))
