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
