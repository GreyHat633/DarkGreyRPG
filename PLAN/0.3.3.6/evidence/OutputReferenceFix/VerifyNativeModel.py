import json
from pathlib import Path

root = Path(__file__).parent
model = root / 'Model'
def read(tag, name):
    return json.loads((model / tag / (name + '.json')).read_text(encoding='utf-8-sig'))
def ids(tag, name, kind):
    return [n['id'] for n in sorted(read(tag, name)['graph']['nodes'], key=lambda n: n['properties'].get('display_order', 0)) if n['type'] == kind]
checks = {}
for before, after, name, kind in [
    ('gap-before','gap-session-flow','session','end'),
    ('gap-redo','gap-session-logic','session','logic_output'),
    ('gap-session-logic','gap-task-flow','task','settle'),
    ('gap-task-flow','gap-task-logic','task','logic_output'),
    ('inspector-before2','inspector-flow2','task','settle'),
    ('inspector-flow2','inspector-logic2','task','logic_output'),
    ('narrow-before','narrow-after','task','settle'),
    ('zoom-before','zoom-session-after','session','end'),
    ('zoom-session-after','zoom-task-after','task','settle'),
    ('cancel-before3','project-sort-after','story','terminate')]:
    old = ids(before,name,kind)
    checks[after] = ids(after,name,kind) == old[1:] + old[:1]
checks['one_undo_restores_before'] = all(read('gap-undo',name)==read('gap-before',name) for name in ('session','task','story'))
checks['redo_restores_sort'] = all(read('gap-redo',name)==read('gap-session-flow',name) for name in ('session','task','story'))
checks['cancel-escape2'] = all(read('cancel-escape2',name)==read('cancel-before2',name) for name in ('session','task','story'))
for tag in ('cancel-leave3','cancel-cross3','same-position3','cancel-inactive3'):
    checks[tag] = all(read(tag,name)==read('cancel-before3',name) for name in ('session','task','story'))
baseline = read('gap-before','layout')['graphs']
for tag in ('gap-session-flow','gap-session-logic','gap-task-flow','gap-task-logic','inspector-flow2','inspector-logic2','narrow-after','zoom-session-after','zoom-task-after','project-sort-after'):
    checks[tag+'_coordinates'] = read(tag,'layout')['graphs']==baseline
    checks[tag+'_connections'] = read(tag,'story')['graph']['connections']==read('gap-before','story')['graph']['connections']
    for name in ('session','task'):
        old = {n['id']:(n['properties'].get('port_id'),n['properties'].get('display_name')) for n in read('gap-before',name)['graph']['nodes']}
        new = {n['id']:(n['properties'].get('port_id'),n['properties'].get('display_name')) for n in read(tag,name)['graph']['nodes']}
        checks[tag+'_'+name+'_stable_ids_names'] = old==new
def root_json(name):
    return json.loads((root / (name + '.json')).read_text(encoding='utf-8-sig'))
checks['project_native_cut'] = len(root_json('project-wire-cut')['connections']) == 0
checks['project_native_new_connection'] = len(root_json('project-wire-new')['connections']) == 1 and root_json('project-wire-new')['connections'][0]['source_story_id'] == 'ST-2345-6789-ABCD-EFGH'
checks['project_native_wire_undo'] = root_json('project-wire-restored') == root_json('project-wire-before')
logic_before = ids('project-logic-before','story','logic_output')
checks['project_logic_native_order'] = ids('project-logic-after','story','logic_output') == logic_before[1:] + logic_before[:1]
checks['project_logic_one_undo'] = read('project-logic-undo','story') == read('project-logic-before','story')
checks['project_logic_redo'] = read('project-logic-redo','story') == read('project-logic-after','story')
checks['project_logic_coordinates'] = read('project-logic-before','layout')['graphs'] == read('project-logic-after','layout')['graphs']
checks['project_logic_connections'] = read('project-logic-before','story')['graph']['connections'] == read('project-logic-after','story')['graph']['connections']
for kind in ('session','task','story'):
    old = {n['id']:n['properties'].get('port_id') for n in read('project-logic-before',kind)['graph']['nodes']}
    new = {n['id']:n['properties'].get('port_id') for n in read('project-logic-after',kind)['graph']['nodes']}
    checks[f'project_logic_{kind}_stable_ids'] = old == new
for action, counts in [('undo', (2,1,0)), ('redo', (1,2,3))]:
    for count in counts:
        checks[f'reference_{action}_{count}'] = len(root_json(f'ref-{action}-count{count}')['referenced_resources']['actors']) == count
dump = (root / 'ref-external-story-search-uia.txt').read_text(encoding='utf-8-sig')
checks['reference_six_chinese_labels'] = all(label in dump for label in ('[角色]', '[角色组]', '[物品]', '[物品组]', '[会话]', '[任务]'))
checks['reference_no_resource_uid'] = 'ST-' not in dump and '~' not in dump and '资源 ID' not in dump
for label, result in root_json('native-frame-results').items():
    checks[label+'_no_pixel_change'] = result['frames'] == 45 and all(value == 0 for value in result['changedPixels'])
(root/'native-model-results.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(checks,indent=2))
assert all(checks.values()), 'Native verification has failing checks'
