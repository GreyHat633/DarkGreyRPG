extends SceneTree


func _initialize() -> void:
	var example_path := ProjectSettings.globalize_path("res://../examples/phase4_project").simplify_path()
	var store := RpgProjectStore.new()
	if not store.load_project(example_path):
		_fail("Cannot load Phase 4 example: %s" % [store.problems])
		return
	if not store.stories.has("tavern_slime_request"):
		_fail("Acceptance Story was not loaded")
		return
	var story: Dictionary = store.stories["tavern_slime_request"]
	if story.get("nodes", []).size() != 20 or story.get("connections", []).size() != 20:
		_fail("Unexpected acceptance Story graph size")
		return
	if story.has("actor") or story.has("dialogue") or story.has("quest"):
		_fail("Story resources must reference through nodes, not ownership fields")
		return

	var probe_path := ProjectSettings.globalize_path("res://../build/studio-story-probe").simplify_path()
	for directory in ["actors", "dialogues", "quests", "stories"]:
		DirAccess.make_dir_recursive_absolute(probe_path.path_join(directory))
	_copy(example_path.path_join("project.json"), probe_path.path_join("project.json"))
	_copy(example_path.path_join("actors/tavern_owner.json"), probe_path.path_join("actors/tavern_owner.json"))
	_copy(example_path.path_join("dialogues/tavern_offer.json"), probe_path.path_join("dialogues/tavern_offer.json"))
	_copy(example_path.path_join("dialogues/tavern_complete.json"), probe_path.path_join("dialogues/tavern_complete.json"))
	_copy(example_path.path_join("quests/kill_10_slimes.json"), probe_path.path_join("quests/kill_10_slimes.json"))
	var probe_store := RpgProjectStore.new()
	if not probe_store.load_project(probe_path):
		_fail("Cannot load Story probe dependencies: %s" % [probe_store.problems])
		return
	if not probe_store.save_story(story, ""):
		_fail("Cannot save Story: %s" % [probe_store.problems])
		return
	var reloaded := RpgProjectStore.new()
	if not reloaded.load_project(probe_path) or not reloaded.stories.has("tavern_slime_request"):
		_fail("Cannot reload saved Story: %s" % [reloaded.problems])
		return

	var broken_reference := story.duplicate(true)
	broken_reference["nodes"][0]["properties"]["actor_id"] = "missing_actor"
	var broken_problems := reloaded.validate_story(broken_reference)
	if not _contains_problem(broken_problems, "Missing Actor"):
		_fail("Story validator did not detect Missing Actor")
		return
	var unconnected := story.duplicate(true)
	unconnected["nodes"].append(
		{"id": "orphan", "type": "end", "position": {"x": 0.0, "y": 0.0}, "properties": {}}
	)
	var unconnected_problems := reloaded.validate_story(unconnected)
	if not _contains_problem(unconnected_problems, "Unconnected Node"):
		_fail("Story validator did not detect Unconnected Node")
		return

	var editor := StoryEditorWindow.new()
	editor.setup(store, story, "tavern_slime_request", "reward_claimed")
	root.add_child(editor)
	editor.popup()
	await process_frame
	var graph_nodes := 0
	for child in editor.graph.get_children():
		if child is GraphNode:
			graph_nodes += 1
	if graph_nodes != 20:
		_fail("Story editor did not render all GraphNodes")
		return
	if editor.graph.get_connection_list().size() != 20:
		_fail("Story editor did not render all connections")
		return
	editor._search_nodes("give_emeralds")
	if editor.selected_node_id != "give_emeralds":
		_fail("Story node search did not locate the requested GraphNode")
		return
	editor.queue_free()

	print("STUDIO_STORY_SAVE_RELOAD_PROBE=PASS")
	print("STUDIO_STORY_REFERENCE_PROBLEMS=PASS")
	print("STUDIO_STORY_UNCONNECTED_PROBLEMS=PASS")
	print("STUDIO_STORY_GRAPH_RENDER_PROBE=PASS")
	print("STUDIO_STORY_NODE_SEARCH_PROBE=PASS")
	quit(0)


func _copy(source: String, target: String) -> void:
	if FileAccess.file_exists(target):
		DirAccess.remove_absolute(target)
	var error := DirAccess.copy_absolute(source, target)
	if error != OK:
		_fail("Cannot copy probe dependency: %s" % source)


func _contains_problem(problems: Array[String], needle: String) -> bool:
	for problem in problems:
		if problem.contains(needle):
			return true
	return false


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
