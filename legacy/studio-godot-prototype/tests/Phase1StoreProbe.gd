extends SceneTree


func _initialize() -> void:
	var probe_path := ProjectSettings.globalize_path("res://../build/studio-probe-project").simplify_path()
	for directory in ["actors", "dialogues", "quests", "stories"]:
		if (
			DirAccess.make_dir_recursive_absolute(probe_path.path_join(directory))
			not in [OK, ERR_ALREADY_EXISTS]
		):
			_fail("Cannot create probe project directory: %s" % directory)
			return

	var project_file := FileAccess.open(probe_path.path_join("project.json"), FileAccess.WRITE)
	if project_file == null:
		_fail("Cannot write probe project.json")
		return
	project_file.store_string(
		JSON.stringify(
			{
				"schema_version": 1,
				"id": "studio_probe",
				"display_name": "Studio Probe",
			},
			"\t",
			false
		)
		+ "\n"
	)
	project_file.close()

	var store := RpgProjectStore.new()
	if not store.load_project(probe_path):
		_fail("Cannot load empty probe project: %s" % store.problems)
		return

	var actor := {
		"schema_version": 1,
		"id": "probe_actor",
		"display_name": "Probe Actor",
		"notes": "Created by the Studio behavior probe.",
		"tags": ["probe"],
	}
	if not store.save_actor(actor, ""):
		_fail("Cannot save Actor: %s" % store.problems)
		return

	var reloaded := RpgProjectStore.new()
	if not reloaded.load_project(probe_path):
		_fail("Cannot reload Actor: %s" % reloaded.problems)
		return
	if not reloaded.actors.has("probe_actor"):
		_fail("Reloaded project does not contain probe_actor")
		return
	if reloaded.actors["probe_actor"].get("display_name") != "Probe Actor":
		_fail("Actor fields changed after save/reload")
		return

	var invalid_actor := actor.duplicate(true)
	invalid_actor["dialogue"] = "forbidden"
	if reloaded.validate_actor(invalid_actor).is_empty():
		_fail("Actor ownership scope guard accepted a Dialogue field")
		return

	print("STUDIO_ACTOR_CRUD_PROBE=PASS")
	print("STUDIO_ACTOR_SCOPE_GUARD=PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
