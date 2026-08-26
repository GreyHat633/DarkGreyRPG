extends SceneTree

const LiveWorkspaceScript := preload("res://scripts/LiveWorkspace.gd")


func _initialize() -> void:
	var example_path := ProjectSettings.globalize_path("res://../examples/phase4_project").simplify_path()
	var target_path := ProjectSettings.globalize_path("res://../build/phase5-content-pack").simplify_path()
	var store := RpgProjectStore.new()
	if not store.load_project(example_path):
		_fail("Cannot load Phase 5 source project: %s" % [store.problems])
		return
	if not store.build_content_pack(target_path):
		_fail("Cannot build Content Pack: %s" % [store.problems])
		return
	for required in ["project.json", "actors", "dialogues", "quests", "stories", "resources"]:
		var path := target_path.path_join(required)
		if not FileAccess.file_exists(path) and not DirAccess.dir_exists_absolute(path):
			_fail("Content Pack is missing: %s" % required)
			return
	var built := RpgProjectStore.new()
	if not built.load_project(target_path):
		_fail("Built Content Pack cannot be loaded: %s" % [built.problems])
		return
	if built.actors.size() != 1 or built.dialogues.size() != 2 or built.quests.size() != 1 or built.stories.size() != 1:
		_fail("Built Content Pack resource counts changed")
		return

	var actor: Dictionary = built.actors["tavern_owner"].duplicate(true)
	actor["notes"] = "Phase 5 backup probe"
	if not built.save_actor(actor, "tavern_owner"):
		_fail("Cannot save backup probe Actor")
		return
	actor["notes"] = "Phase 5 second save"
	if not built.save_actor(actor, "tavern_owner"):
		_fail("Cannot save Actor twice")
		return
	if not FileAccess.file_exists(target_path.path_join("actors/tavern_owner.json.bak")):
		_fail("Atomic save did not retain the previous resource backup")
		return

	var status := Label.new()
	var workspace := LiveWorkspaceScript.new()
	workspace.setup(store, status)
	root.add_child(workspace)
	var debugger := workspace.build_debugger_panel()
	var minecraft := workspace.build_minecraft_panel()
	root.add_child(debugger)
	root.add_child(minecraft)
	await process_frame
	if debugger.name != "Debugger" or minecraft.name != "Minecraft":
		_fail("Live workspace panels did not render")
		return
	if workspace.story_picker.item_count != 1:
		_fail("Play Test Story picker did not load local Stories")
		return
	var story_editor := StoryEditorWindow.new()
	story_editor.setup(store, store.stories["tavern_slime_request"], "tavern_slime_request", "give_emeralds")
	root.add_child(story_editor)
	story_editor.popup()
	await process_frame
	story_editor.apply_live_debug(
		{
			"state": "WAITING",
			"current_node": "give_emeralds",
			"previous_nodes": ["give_xp"],
			"explanation": "Waiting probe",
		}
	)
	var current_visual := story_editor.graph.get_node(NodePath("give_emeralds")) as GraphNode
	if current_visual.modulate == Color.WHITE:
		_fail("Story Graph current/waiting highlight was not applied")
		return
	story_editor.apply_pick_result("item", {"item": "minecraft:diamond", "metadata": 0})
	var selected_story_node: Dictionary = story_editor._find_node("give_emeralds")
	if selected_story_node["properties"]["item"] != "minecraft:diamond":
		_fail("Minecraft Item Pick did not fill the selected Story node")
		return

	var quest_editor := QuestEditorWindow.new()
	quest_editor.setup(store, store.quests["kill_10_slimes"], "kill_10_slimes")
	root.add_child(quest_editor)
	quest_editor.popup()
	await process_frame
	quest_editor.apply_pick_result("entity_type", {"entity_type": "Zombie"})
	if quest_editor.quest["objectives"][0]["entity"] != "Zombie":
		_fail("Minecraft Entity Type Pick did not fill the selected Quest Objective")
		return

	workspace.queue_free()
	debugger.queue_free()
	minecraft.queue_free()
	status.queue_free()
	story_editor.queue_free()
	quest_editor.queue_free()
	await process_frame
	print("STUDIO_CONTENT_PACK_BUILD_PROBE=PASS")
	print("STUDIO_CONTENT_PACK_RUNTIME_SHAPE=PASS")
	print("STUDIO_ATOMIC_BACKUP_PROBE=PASS")
	print("STUDIO_LIVE_DEBUGGER_UI_PROBE=PASS")
	print("STUDIO_STORY_GRAPH_LIVE_HIGHLIGHT_PROBE=PASS")
	print("STUDIO_PICK_TO_INSPECTOR_PROBE=PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
