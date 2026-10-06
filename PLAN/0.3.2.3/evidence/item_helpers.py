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
