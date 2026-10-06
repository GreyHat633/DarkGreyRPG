from live_driver import *
call('key',key=1);call('interact',actor='GreyHat_:TarvenBoss');time.sleep(1);print(state()['tasks']);call('tasks');print(shot('review-user-world-after-interact'));call('key',key=1)
call('entitygui');time.sleep(.7)
if 'windowGeometry' in state():print(shot('review-entity-final'))
call('shutdown')
