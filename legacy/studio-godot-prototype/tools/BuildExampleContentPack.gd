extends SceneTree


func _initialize() -> void:
	var source := ProjectSettings.globalize_path("res://../examples/phase4_project").simplify_path()
	var target := ProjectSettings.globalize_path(
		"res://../build/content-packs/darkgrey_rpg_phase5_example"
	).simplify_path()
	var store := RpgProjectStore.new()
	if not store.load_project(source):
		push_error("Cannot load example project: %s" % [store.problems])
		quit(1)
		return
	if not store.build_content_pack(target):
		push_error("Cannot build example Content Pack: %s" % [store.problems])
		quit(1)
		return
	print("CONTENT_PACK_BUILD=PASS")
	print("CONTENT_PACK_PATH=" + target)
	quit(0)
