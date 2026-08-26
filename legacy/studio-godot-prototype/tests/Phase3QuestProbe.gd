extends SceneTree


func _initialize() -> void:
	var example_path := ProjectSettings.globalize_path("res://../examples/phase3_project").simplify_path()
	var store := RpgProjectStore.new()
	if not store.load_project(example_path):
		_fail("Cannot load Phase 3 example: %s" % [store.problems])
		return
	if not store.quests.has("kill_10_slimes") or not store.quests.has("objective_showcase"):
		_fail("Phase 3 example Quests were not loaded")
		return
	var showcase: Dictionary = store.quests["objective_showcase"]
	if showcase.get("objectives", []).size() != 4:
		_fail("Unexpected Objective count")
		return
	var types := {}
	for objective_value in showcase.get("objectives", []):
		types[str(objective_value.get("type", ""))] = true
	for required_type in ["kill_entity", "collect_item", "reach_location", "interact_actor"]:
		if not types.has(required_type):
			_fail("Missing Objective type: %s" % required_type)
			return
	var modes := {}
	for group_value in showcase.get("objective_groups", []):
		modes[str(group_value.get("mode", ""))] = true
	for required_mode in ["ALL", "ANY", "SEQUENCE"]:
		if not modes.has(required_mode):
			_fail("Missing Objective Group mode: %s" % required_mode)
			return

	var probe_path := ProjectSettings.globalize_path("res://../build/studio-quest-probe").simplify_path()
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("actors"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("dialogues"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("quests"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("stories"))
	var project_file := FileAccess.open(probe_path.path_join("project.json"), FileAccess.WRITE)
	project_file.store_string(
		'{\n  "schema_version": 1,\n  "id": "quest_probe",\n  "display_name": "Quest Probe"\n}\n'
	)
	project_file.close()
	var actor_file := FileAccess.open(probe_path.path_join("actors/probe_actor.json"), FileAccess.WRITE)
	actor_file.store_string(
		'{\n  "schema_version": 1,\n  "id": "probe_actor",\n  "display_name": "Probe Actor"\n}\n'
	)
	actor_file.close()

	var probe_store := RpgProjectStore.new()
	if not probe_store.load_project(probe_path):
		_fail("Cannot load empty Quest probe: %s" % [probe_store.problems])
		return
	var probe_quest := {
		"schema_version": 1,
		"id": "probe_quest",
		"title": "Probe Quest",
		"description": "Save and reload probe",
		"objectives": [
			{
				"id": "kill",
				"type": "kill_entity",
				"description": "Kill ten Slimes",
				"entity": "Slime",
				"required": 10,
			},
		],
		"objective_groups": [{"id": "main", "mode": "ALL", "objectives": ["kill"]}],
		"metadata": {"notes": "", "tags": ["probe"]},
	}
	if not probe_store.save_quest(probe_quest, ""):
		_fail("Cannot save Quest: %s" % [probe_store.problems])
		return
	var reloaded := RpgProjectStore.new()
	if not reloaded.load_project(probe_path) or not reloaded.quests.has("probe_quest"):
		_fail("Cannot reload saved Quest: %s" % [reloaded.problems])
		return
	for forbidden_field in ["issuerNpc", "dialogue", "story"]:
		var forbidden := probe_quest.duplicate(true)
		forbidden[forbidden_field] = "forbidden"
		if reloaded.validate_quest(forbidden).is_empty():
			_fail("Quest validator accepted forbidden field: %s" % forbidden_field)
			return

	var editor := QuestEditorWindow.new()
	editor.setup(store, showcase, "objective_showcase")
	root.add_child(editor)
	editor.popup()
	await process_frame
	if editor.objective_list == null or editor.objective_list.item_count != 4:
		_fail("Quest editor did not render the Objective list")
		return
	if editor.group_list == null or editor.group_list.item_count != 3:
		_fail("Quest editor did not render the Objective Group list")
		return
	editor.queue_free()

	print("STUDIO_QUEST_SAVE_RELOAD_PROBE=PASS")
	print("STUDIO_QUEST_OBJECTIVE_TYPES=PASS")
	print("STUDIO_QUEST_GROUP_MODES=PASS")
	print("STUDIO_QUEST_SCOPE_GUARD=PASS")
	print("STUDIO_QUEST_EDITOR_RENDER_PROBE=PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
