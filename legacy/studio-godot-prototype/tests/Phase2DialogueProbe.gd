extends SceneTree


func _initialize() -> void:
	var example_path := ProjectSettings.globalize_path("res://../examples/phase2_project").simplify_path()
	var store := RpgProjectStore.new()
	if not store.load_project(example_path):
		_fail("Cannot load Phase 2 example: %s" % store.problems)
		return
	if not store.dialogues.has("tavern_offer"):
		_fail("Phase 2 example Dialogue was not loaded")
		return
	var dialogue: Dictionary = store.dialogues["tavern_offer"]
	if dialogue.get("nodes", []).size() != 8:
		_fail("Unexpected example node count")
		return
	for node_value in dialogue.get("nodes", []):
		var node: Dictionary = node_value
		if node.has("StartQuest") or node.has("start_quest"):
			_fail("Dialogue contains forbidden Quest action")
			return

	var probe_path := ProjectSettings.globalize_path("res://../build/studio-dialogue-probe").simplify_path()
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("actors"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("dialogues"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("quests"))
	DirAccess.make_dir_recursive_absolute(probe_path.path_join("stories"))
	var project_file := FileAccess.open(probe_path.path_join("project.json"), FileAccess.WRITE)
	project_file.store_string(
		'{\n  "schema_version": 1,\n  "id": "dialogue_probe",\n  "display_name": "Dialogue Probe"\n}\n'
	)
	project_file.close()
	var actor_file := FileAccess.open(probe_path.path_join("actors/probe_actor.json"), FileAccess.WRITE)
	actor_file.store_string(
		'{\n  "schema_version": 1,\n  "id": "probe_actor",\n  "display_name": "Probe Actor"\n}\n'
	)
	actor_file.close()

	var probe_store := RpgProjectStore.new()
	if not probe_store.load_project(probe_path):
		_fail("Cannot load empty Dialogue probe: %s" % probe_store.problems)
		return
	var probe_dialogue := {
		"schema_version": 1,
		"id": "probe_dialogue",
		"title": "Probe Dialogue",
		"speakers": ["probe_actor"],
		"entry": "line",
		"nodes": [
			{"id": "line", "type": "line", "speaker": "probe_actor", "text": "Hello", "next": "end"},
			{"id": "end", "type": "end", "result": "done"},
		],
		"metadata": {"notes": "", "tags": ["probe"]},
	}
	if not probe_store.save_dialogue(probe_dialogue, ""):
		_fail("Cannot save Dialogue: %s" % probe_store.problems)
		return
	var reloaded := RpgProjectStore.new()
	if not reloaded.load_project(probe_path) or not reloaded.dialogues.has("probe_dialogue"):
		_fail("Cannot reload saved Dialogue: %s" % reloaded.problems)
		return
	var forbidden := probe_dialogue.duplicate(true)
	forbidden["nodes"][0]["StartQuest"] = "forbidden"
	if reloaded.validate_dialogue(forbidden).is_empty():
		_fail("Dialogue validator accepted StartQuest")
		return

	var editor := DialogueEditorWindow.new()
	editor.setup(store, dialogue, "tavern_offer")
	root.add_child(editor)
	editor.popup()
	await process_frame
	if editor.node_list == null or editor.node_list.item_count != 8:
		_fail("Dialogue editor did not render the node list")
		return
	editor.queue_free()

	print("STUDIO_DIALOGUE_SAVE_RELOAD_PROBE=PASS")
	print("STUDIO_DIALOGUE_NODE_TYPES=PASS")
	print("STUDIO_DIALOGUE_QUEST_SCOPE_GUARD=PASS")
	print("STUDIO_DIALOGUE_EDITOR_RENDER_PROBE=PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
