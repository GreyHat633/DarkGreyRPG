import json
from pathlib import Path

proof = Path(__file__).resolve().parent

def read(name):
    return json.loads((proof / name).read_text(encoding='utf-8-sig'))

checks = {key: value for key, value in read('native-fold-checks.json').items() if isinstance(value, bool)}
checks.update({key: value for key, value in read('native-keyboard-checks.json').items() if isinstance(value, bool)})
checks['InspectorImageUnchanged'] = read('native-inspector-image-check.json')['InspectorImageUnchanged']

def position(snapshot, name):
    item = next(row for row in read(snapshot) if row['Name'] == name and row['Type'] == 'ControlType.Text')
    return [float(value) for value in item['Bounds'].split(',')]

ordered = 'sidebar-group-reordered-uia.json'
undone = 'sidebar-group-reorder-undone-uia.json'
checks['GroupDragMovesWholeSectionAboveSingleStory'] = position(ordered, '组内甲')[1] < position(ordered, '交互验证')[1]
checks['ReorderUndoRestoresSectionOrder'] = position(undone, '交互验证')[1] < position(undone, '组内甲')[1]
checks['MemberStoriesAreIndented'] = position(undone, '组内甲')[0] > position(undone, '交互验证')[0]
checks['DoubleClickOpensStoryWorkspace'] = read('native-doubleclick-check.json')['CanonicalStoryWorkspacePresent'] and read('native-doubleclick-check.json')['Responding']
names_path = proof.parent / 'Studio/Data/Projects/OutputReferenceFix/resources/editor/story-group-names.json'
names = json.loads(names_path.read_text(encoding='utf-8-sig'))['names']
checks['RenameUndoRestoresGroupNames'] = {row['display_name'] for row in names} == {'故事组（1）', '故事组（2）'}
(proof / 'native-checks.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'Checks': len(checks), 'Passed': sum(checks.values()), 'Results': checks}, ensure_ascii=False, indent=2))
assert all(checks.values())
