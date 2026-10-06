import json,time,urllib.request,pathlib
ROOT=pathlib.Path(__file__).parent
LOG=ROOT/'live-verification.jsonl'
def call(op,**kw):
    req=dict(op=op,**kw)
    raw=json.dumps(req,ensure_ascii=False).encode('utf8')
    with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:32361/',data=raw,headers={'Content-Type':'application/json'}),timeout=55) as f:
        result=json.load(f)
    with LOG.open('a',encoding='utf8') as f:f.write(json.dumps(dict(time=time.time(),request=req,response=result),ensure_ascii=False)+'\n')
    if isinstance(result,dict) and 'error' in result:raise RuntimeError(result['error'])
    return result

def state():return call('status')
def wait(predicate,seconds=15):
    end=time.time()+seconds
    while time.time()<end:
        s=state()
        if predicate(s):return s
        time.sleep(.25)
    raise AssertionError('State timeout: '+str(s))
def shot(name):
    time.sleep(.3)
    return call('shot',name=name)
def button(id):
    s=state();b=next(b for b in s['buttons'] if b['id']==id)
    return call('click',x=b['xPosition']+b['width']//2,y=b['yPosition']+b['height']//2)
def node(s):return s.get('modelFrame',{}).get('currentNodeId')
def assert_same(before):
    s=state();assert s['modelFrame']==before['modelFrame'],(s,before);return s
