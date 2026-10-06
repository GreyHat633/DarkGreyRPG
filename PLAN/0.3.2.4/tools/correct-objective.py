import copy, hashlib, json, pathlib, zipfile
root = pathlib.Path(__file__).resolve().parents[2]
source = root / 'run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs'
out = root / 'PLAN/0.3.2.4/evidence/kill_slimes-description-corrected.dgrs'
expected = '9B0F08C48BD702FE1F6060C52A455790DD7B0151E677EE72101C87852669EABD'
assert hashlib.sha256(source.read_bytes()).hexdigest().upper() == expected
entry = 'resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json'
nodeid = 'node_4feb6ff8c4944e078f9914cd808c9f17'
with zipfile.ZipFile(source) as src:
    original = json.loads(src.read(entry))
    corrected = copy.deepcopy(original)
    node = next(n for n in corrected['graph']['nodes'] if n['id'] == nodeid)
    assert node['properties']['objective_type'] == 'interact_actor'
    assert node['properties']['actor_id'] == 'GreyHat_:TarvenBoss'
    assert node['properties']['description'] == '消灭史莱姆'
    node['properties']['description'] = '与酒馆老板对话'
    with zipfile.ZipFile(out, 'w', compression=zipfile.ZIP_DEFLATED) as dst:
        for info in src.infolist():
            data = json.dumps(corrected, ensure_ascii=False, indent=2).encode('utf-8') if info.filename == entry else src.read(info.filename)
            dst.writestr(info, data)
with zipfile.ZipFile(source) as src, zipfile.ZipFile(out) as dst:
    assert src.namelist() == dst.namelist()
    changed = [n for n in src.namelist() if src.read(n) != dst.read(n)]
    assert changed == [entry]
    check = json.loads(dst.read(entry))
    next(n for n in check['graph']['nodes'] if n['id'] == nodeid)['properties']['description'] = '消灭史莱姆'
    assert check == original
report = dict(source=str(source), source_sha256=expected, corrected=str(out), corrected_sha256=hashlib.sha256(out.read_bytes()).hexdigest().upper(), only_changed_entry=entry, only_changed_field='graph.nodes['+nodeid+'].properties.description', AUTHOR_DESCRIPTION_FIXED='PASS_ISOLATED_PACKAGE', RUNTIME_TRANSITION='PENDING', SOURCE_AUTHORING='UNAVAILABLE_REQUIRES_SAME_CORRECTION_WHEN_RECOVERED')
(root / 'PLAN/0.3.2.4/evidence/objective-correction.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=True))
