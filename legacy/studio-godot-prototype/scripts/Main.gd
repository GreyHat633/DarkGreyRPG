extends Control

const LiveWorkspaceScript := preload("res://scripts/LiveWorkspace.gd")
const PORTABLE_PROJECTS_DIRECTORY := "DarkGreyRPGProjects"
const PORTABLE_DEFAULT_PROJECT := "darkgrey_rpg_project"
const PORTABLE_SETTINGS_FILE := "DarkGreyRPGStudio.settings.json"

var store := RpgProjectStore.new()
var selected_actor: Dictionary = {}
var selected_original_id := ""
var dirty := false

var project_tree: Tree
var actor_list: ItemList
var id_edit: LineEdit
var display_name_edit: LineEdit
var notes_edit: TextEdit
var tags_edit: LineEdit
var save_button: Button
var output_text: RichTextLabel
var problems_list: ItemList
var project_label: Label
var open_dialog: FileDialog
var build_dialog: FileDialog
var delete_dialog: ConfirmationDialog
var live_status: Label
var live_workspace: Node


func _ready() -> void:
	_build_ui()
	var startup_project := _resolve_startup_project()
	if startup_project.is_empty():
		_log("[color=yellow]未找到可写的便携式项目。请选择一个 RPG 项目目录。[/color]")
		open_dialog.call_deferred("popup_centered_ratio", 0.75)
	else:
		_open_project(startup_project)
	if "--portable-smoke-test" in OS.get_cmdline_user_args():
		if startup_project.is_empty() or store.project.is_empty():
			print("DGRPG_STUDIO_PORTABLE_SMOKE=FAIL")
			get_tree().quit(1)
		else:
			print("DGRPG_STUDIO_PORTABLE_SMOKE=PASS path=" + store.project_path)
			get_tree().quit()


func _resolve_startup_project() -> String:
	if OS.has_feature("editor"):
		return ProjectSettings.globalize_path("res://../examples/phase4_project").simplify_path()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--project="):
			var requested := argument.trim_prefix("--project=").replace("\\", "/").simplify_path()
			if FileAccess.file_exists(requested.path_join("project.json")):
				return requested
	var last_project := _read_last_portable_project()
	if not last_project.is_empty() and FileAccess.file_exists(last_project.path_join("project.json")):
		return last_project
	return _create_portable_default_project()


func _portable_base_directory() -> String:
	return OS.get_executable_path().get_base_dir().replace("\\", "/").simplify_path()


func _portable_settings_path() -> String:
	return _portable_base_directory().path_join(PORTABLE_SETTINGS_FILE)


func _read_last_portable_project() -> String:
	var settings_path := _portable_settings_path()
	if not FileAccess.file_exists(settings_path):
		return ""
	var file := FileAccess.open(settings_path, FileAccess.READ)
	if file == null:
		return ""
	var parsed = JSON.parse_string(file.get_as_text())
	if parsed is not Dictionary:
		return ""
	return str(parsed.get("last_project", "")).replace("\\", "/").simplify_path()


func _create_portable_default_project() -> String:
	var project_path := _portable_base_directory().path_join(PORTABLE_PROJECTS_DIRECTORY).path_join(
		PORTABLE_DEFAULT_PROJECT
	)
	for directory in ["", "actors", "dialogues", "quests", "stories", "resources"]:
		var target := project_path if directory.is_empty() else project_path.path_join(directory)
		var make_error := DirAccess.make_dir_recursive_absolute(target)
		if make_error not in [OK, ERR_ALREADY_EXISTS]:
			push_error("无法创建便携式 RPG 项目目录： " + target)
			return ""
	var project_file := project_path.path_join("project.json")
	if not FileAccess.file_exists(project_file):
		var template := FileAccess.open("res://templates/blank_project/project.json", FileAccess.READ)
		if template == null:
			push_error("缺失打包的空白项目模板。")
			return ""
		var output := FileAccess.open(project_file, FileAccess.WRITE)
		if output == null:
			push_error("无法写入便携式 RPG 项目文件： " + project_file)
			return ""
		output.store_buffer(template.get_buffer(template.get_length()))
	return project_path


func _save_portable_settings(path: String) -> void:
	if OS.has_feature("editor"):
		return
	var settings_path := _portable_settings_path()
	var temporary := settings_path + ".tmp"
	var file := FileAccess.open(temporary, FileAccess.WRITE)
	if file == null:
		_log("[color=yellow]无法在可执行文件旁记住上一次的项目。[/color]")
		return
	file.store_string(JSON.stringify({"last_project": path}, "\t") + "\n")
	file.close()
	if FileAccess.file_exists(settings_path) and DirAccess.remove_absolute(settings_path) != OK:
		_log("[color=yellow]无法替换便携式设置。[/color]")
		return
	if DirAccess.rename_absolute(temporary, settings_path) != OK:
		_log("[color=yellow]无法完成便携式设置。[/color]")


func _build_ui() -> void:
	var root := PanelContainer.new()
	self.theme = ThemeManager.get_theme()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(root)

	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 0)
	root.add_child(main_vbox)

	main_vbox.add_child(_build_menu_bar())

	var main_split := VSplitContainer.new()
	main_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_split.split_offset = 650
	main_vbox.add_child(main_split)

	var upper := HBoxContainer.new()
	upper.add_theme_constant_override("separation", 16)
	upper.size_flags_vertical = Control.SIZE_EXPAND_FILL
	upper.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_split.add_child(upper)

	upper.add_child(_build_project_panel())
	upper.add_child(_build_workspace_panel())
	upper.add_child(_build_inspector_panel())

	var bottom := TabContainer.new()
	bottom.custom_minimum_size.y = 190
	main_split.add_child(bottom)

	output_text = RichTextLabel.new()
	output_text.name = "Output"
	output_text.bbcode_enabled = true
	bottom.add_child(output_text)

	problems_list = ItemList.new()
	problems_list.name = "Problems"
	bottom.add_child(problems_list)

	live_workspace = LiveWorkspaceScript.new()
	live_workspace.setup(store, live_status)
	live_workspace.output_message.connect(_log)
	live_workspace.story_debug_updated.connect(_on_story_debug_updated)
	live_workspace.pick_received.connect(_on_pick_received)
	add_child(live_workspace)
	bottom.add_child(live_workspace.build_debugger_panel())
	bottom.add_child(live_workspace.build_minecraft_panel())

	open_dialog = FileDialog.new()
	open_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	open_dialog.access = FileDialog.ACCESS_FILESYSTEM
	open_dialog.use_native_dialog = true
	open_dialog.dir_selected.connect(_open_project)
	add_child(open_dialog)
	build_dialog = FileDialog.new()
	build_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	build_dialog.access = FileDialog.ACCESS_FILESYSTEM
	build_dialog.use_native_dialog = true
	build_dialog.dir_selected.connect(_build_content_pack)
	add_child(build_dialog)

	delete_dialog = ConfirmationDialog.new()
	delete_dialog.title = "删除角色"
	delete_dialog.confirmed.connect(_delete_selected_actor)
	add_child(delete_dialog)
	var autosave := Timer.new()
	autosave.wait_time = 10.0
	autosave.autostart = true
	autosave.timeout.connect(_autosave_actor)
	add_child(autosave)


func _build_menu_bar() -> Control:
	var bar := HBoxContainer.new()
	bar.custom_minimum_size.y = 40
	bar.add_theme_constant_override("separation", 4)

	var file_menu := MenuButton.new()
	file_menu.text = "文件"
	file_menu.get_popup().add_item("打开项目…", 1)
	file_menu.get_popup().add_separator()
	file_menu.get_popup().add_item("保存角色    Ctrl+S", 2)
	file_menu.get_popup().id_pressed.connect(_on_file_menu)
	bar.add_child(file_menu)

	for title in ["编辑"]:
		var menu := MenuButton.new()
		menu.text = title
		menu.get_popup().add_item("在后续开发阶段可用")
		menu.get_popup().set_item_disabled(0, true)
		bar.add_child(menu)
	var project_menu := MenuButton.new()
	project_menu.text = "项目"
	project_menu.get_popup().add_item("新建对话 (Dialogue)…", 1)
	project_menu.get_popup().add_item("新建任务 (Quest)…", 2)
	project_menu.get_popup().add_item("新建剧情节点 (Story Graph)…", 3)
	project_menu.get_popup().add_separator()
	project_menu.get_popup().add_item("构建内容包…", 4)
	project_menu.get_popup().id_pressed.connect(_on_project_menu)
	bar.add_child(project_menu)

	var theme_menu := MenuButton.new()
	theme_menu.text = "主题 (Theme)"
	theme_menu.get_popup().add_item("深空系统紫蓝 (Deep Space)", 1)
	theme_menu.get_popup().add_item("轻科技薄荷蓝 (Mint Tech)", 2)
	theme_menu.get_popup().id_pressed.connect(func(id: int):
		if id == 1:
			ThemeManager.apply_theme("deep_space")
		elif id == 2:
			ThemeManager.apply_theme("mint_tech")
	)
	bar.add_child(theme_menu)

	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(spacer)

	project_label = Label.new()
	project_label.text = "未打开项目"
	bar.add_child(project_label)

	live_status = Label.new()
	live_status.text = "  ● 离线 (Offline)  "
	live_status.modulate = Color(0.72, 0.75, 0.82)
	live_status.tooltip_text = "Studio 离线状态下仍然可编辑；正在连接至 127.0.0.1。"
	bar.add_child(live_status)
	return bar


func _build_project_panel() -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size.x = 280
	var panel := VBoxContainer.new()
	card.add_child(panel)
	panel.custom_minimum_size.x = 260

	var title := Label.new()
	title.text = "  项目 (Project)"
	title.custom_minimum_size.y = 32
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	panel.add_child(title)

	project_tree = Tree.new()
	project_tree.hide_root = true
	project_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	project_tree.item_selected.connect(_on_tree_selected)
	panel.add_child(project_tree)
	return card


func _build_workspace_panel() -> Control:
	var card := PanelContainer.new()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var panel := VBoxContainer.new()
	card.add_child(panel)
	panel.custom_minimum_size.x = 560
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var toolbar := HBoxContainer.new()
	var new_button := Button.new()
	new_button.text = "+ 新建角色"
	new_button.pressed.connect(_new_actor)
	toolbar.add_child(new_button)
	var duplicate_button := Button.new()
	duplicate_button.text = "复制"
	duplicate_button.pressed.connect(_duplicate_actor)
	toolbar.add_child(duplicate_button)
	var delete_button := Button.new()
	delete_button.text = "删除"
	delete_button.pressed.connect(_confirm_delete)
	toolbar.add_child(delete_button)
	panel.add_child(toolbar)

	var heading := Label.new()
	heading.text = "角色列表 (ACTORS)"
	heading.add_theme_font_size_override("font_size", 22)
	panel.add_child(heading)

	actor_list = ItemList.new()
	actor_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	actor_list.item_selected.connect(_on_actor_list_selected)
	panel.add_child(actor_list)
	return card


func _build_inspector_panel() -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size.x = 380
	var panel := VBoxContainer.new()
	card.add_child(panel)
	panel.custom_minimum_size.x = 350

	var title := Label.new()
	title.text = "  属性检查器 (Inspector)"
	title.custom_minimum_size.y = 32
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	panel.add_child(title)

	var form := GridContainer.new()
	form.columns = 1
	form.add_theme_constant_override("v_separation", 5)
	panel.add_child(form)

	id_edit = _add_field(form, "ID")
	id_edit.placeholder_text = "小写的资源ID"
	display_name_edit = _add_field(form, "显示名称 (Display Name)")
	notes_edit = TextEdit.new()
	_add_label(form, "备注 (Notes)")
	notes_edit.custom_minimum_size.y = 160
	notes_edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	form.add_child(notes_edit)
	tags_edit = _add_field(form, "标签 (Tags)")
	tags_edit.placeholder_text = "使用逗号, 分隔, 多个标签"

	id_edit.text_changed.connect(_mark_dirty_text)
	display_name_edit.text_changed.connect(_mark_dirty_text)
	notes_edit.text_changed.connect(_mark_dirty)
	tags_edit.text_changed.connect(_mark_dirty_text)
	
	save_button = Button.new()
	save_button.text = "保存角色"
	save_button.disabled = true
	save_button.pressed.connect(_save_actor)
	panel.add_child(save_button)

	var scope_note := Label.new()
	scope_note.text = "角色 (Actor) 仅存储 RPG 身份。\nCNPC 模型、AI、生命值、战斗及动画\n仍然在 CustomNPC+ 中管理。"
	scope_note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	scope_note.modulate = Color(0.65, 0.68, 0.75)
	panel.add_child(scope_note)
	return card


func _add_label(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	parent.add_child(label)


func _add_field(parent: Control, title: String) -> LineEdit:
	_add_label(parent, title)
	var edit := LineEdit.new()
	parent.add_child(edit)
	return edit


func _on_file_menu(id: int) -> void:
	if id == 1:
		open_dialog.popup_centered_ratio(0.75)
	elif id == 2:
		_save_actor()


func _on_project_menu(id: int) -> void:
	if id == 1:
		_new_dialogue()
	elif id == 2:
		_new_quest()
	elif id == 3:
		_new_story()
	elif id == 4:
		build_dialog.popup_centered_ratio(0.75)


func _new_dialogue() -> void:
	if store.actors.is_empty():
		store.problems.append("创建对话 (Dialogue) 前，请至少创建一个角色 (Actor)。")
		_refresh_problems()
		return
	var dialogue_id := "new_dialogue"
	var suffix := 2
	while store.dialogues.has(dialogue_id):
		dialogue_id = "new_dialogue_%d" % suffix
		suffix += 1
	var first_actor := str(_sorted_actor_ids()[0])
	var value := {
		"schema_version": 1,
		"id": dialogue_id,
		"title": "新对话",
		"speakers": [first_actor],
		"entry": "line_1",
		"nodes": [
			{
				"id": "line_1",
				"type": "line",
				"speaker": first_actor,
				"text": "新对话内容。",
				"next": "end",
			},
			{"id": "end", "type": "end", "result": "done"},
		],
		"metadata": {"notes": "", "tags": []},
	}
	_show_dialogue_window(value, "")


func _open_dialogue_editor(dialogue_id: String) -> void:
	if not store.dialogues.has(dialogue_id):
		return
	_show_dialogue_window(store.dialogues[dialogue_id], dialogue_id)


func _show_dialogue_window(value: Dictionary, source_id: String) -> void:
	var editor := DialogueEditorWindow.new()
	editor.setup(store, value, source_id)
	editor.dialogue_saved.connect(_on_dialogue_saved)
	editor.dialogue_deleted.connect(_on_dialogue_deleted)
	add_child(editor)
	editor.popup()


func _on_dialogue_saved(dialogue_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=green]Saved Dialogue:[/color] %s" % dialogue_id)
	live_workspace.notify_resource_saved("dialogue", dialogue_id)


func _on_dialogue_deleted(dialogue_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=orange]Deleted Dialogue:[/color] %s" % dialogue_id)


func _new_quest() -> void:
	var quest_id := "new_quest"
	var suffix := 2
	while store.quests.has(quest_id):
		quest_id = "new_quest_%d" % suffix
		suffix += 1
	var value := {
		"schema_version": 1,
		"id": quest_id,
		"title": "新任务",
		"description": "描述玩家的目标任务。",
		"objectives": [
			{
				"id": "objective_1",
				"type": "kill_entity",
				"description": "击败一只史莱姆 (Slime)",
				"entity": "Slime",
				"required": 1,
			},
		],
		"objective_groups": [
			{"id": "main", "mode": "ALL", "objectives": ["objective_1"]},
		],
		"metadata": {"notes": "", "tags": []},
	}
	_show_quest_window(value, "")


func _open_quest_editor(quest_id: String) -> void:
	if not store.quests.has(quest_id):
		return
	_show_quest_window(store.quests[quest_id], quest_id)


func _show_quest_window(value: Dictionary, source_id: String) -> void:
	var editor := QuestEditorWindow.new()
	editor.setup(store, value, source_id)
	editor.quest_saved.connect(_on_quest_saved)
	editor.quest_deleted.connect(_on_quest_deleted)
	add_child(editor)
	editor.popup()


func _on_quest_saved(quest_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=green]Saved Quest:[/color] %s" % quest_id)
	live_workspace.notify_resource_saved("quest", quest_id)


func _on_quest_deleted(quest_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=orange]Deleted Quest:[/color] %s" % quest_id)


func _new_story() -> void:
	if store.actors.is_empty():
		store.problems.append("创建剧情节点前，请至少创建一个角色 (Actor)。")
		_refresh_problems()
		return
	var story_id := "new_story"
	var suffix := 2
	while store.stories.has(story_id):
		story_id = "new_story_%d" % suffix
		suffix += 1
	var actor_ids := store.actors.keys()
	actor_ids.sort()
	var value := {
		"schema_version": 1,
		"id": story_id,
		"title": "新剧情",
		"entry": "interact",
		"nodes": [
			{
				"id": "interact",
				"type": "interact_actor",
				"position": {"x": 0.0, "y": 100.0},
				"properties": {"actor_id": str(actor_ids[0])},
			},
			{"id": "end", "type": "end", "position": {"x": 300.0, "y": 100.0}, "properties": {}},
		],
		"connections": [{"from": "interact", "output": "next", "to": "end"}],
		"metadata": {"notes": "", "tags": []},
	}
	_show_story_window(value, "")


func _open_story_editor(story_id: String, focus_node := "") -> void:
	if not store.stories.has(story_id):
		return
	_show_story_window(store.stories[story_id], story_id, focus_node)


func _show_story_window(value: Dictionary, source_id: String, focus_node := "") -> void:
	var editor := StoryEditorWindow.new()
	editor.setup(store, value, source_id, focus_node)
	editor.story_saved.connect(_on_story_saved)
	editor.story_deleted.connect(_on_story_deleted)
	add_child(editor)
	editor.popup()


func _on_story_saved(story_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=green]Saved Story:[/color] %s" % story_id)
	live_workspace.notify_resource_saved("story", story_id)


func _on_story_deleted(story_id: String) -> void:
	_rebuild_project_tree()
	_refresh_problems()
	_log("[color=orange]Deleted Story:[/color] %s" % story_id)


func _open_project(path: String) -> void:
	if dirty:
		_log("[color=yellow]Discarded unsaved Actor Inspector changes.[/color]")
	_clear_selection()
	var loaded := store.load_project(path)
	project_label.text = store.project.get("display_name", "无效的项目")
	project_label.tooltip_text = path
	_rebuild_project_tree()
	_rebuild_actor_list()
	_refresh_problems()
	if loaded:
		_log(
			"[color=green]Opened project:[/color] %s (%d Actor(s), %d Dialogue(s), %d Quest(s), %d Story/Stories)"
			% [path, store.actors.size(), store.dialogues.size(), store.quests.size(), store.stories.size()]
		)
		_save_portable_settings(path)
		live_workspace._refresh_story_picker()
	else:
		_log("[color=red]Project opened with validation errors:[/color] %s" % path)


func _rebuild_project_tree() -> void:
	project_tree.clear()
	var root := project_tree.create_item()
	var actors_item := project_tree.create_item(root)
	actors_item.set_text(0, "角色 (%d)" % store.actors.size())
	actors_item.set_metadata(0, "actors")
	for actor_id in _sorted_actor_ids():
		var actor_item := project_tree.create_item(actors_item)
		actor_item.set_text(0, str(store.actors[actor_id].get("display_name", actor_id)))
		actor_item.set_tooltip_text(0, actor_id)
		actor_item.set_metadata(0, "actor:" + actor_id)
	actors_item.set_collapsed(false)

	var dialogues_item := project_tree.create_item(root)
	dialogues_item.set_text(0, "对话 (%d)" % store.dialogues.size())
	dialogues_item.set_metadata(0, "dialogues")
	for dialogue_id in _sorted_dialogue_ids():
		var dialogue_item := project_tree.create_item(dialogues_item)
		dialogue_item.set_text(0, str(store.dialogues[dialogue_id].get("title", dialogue_id)))
		dialogue_item.set_tooltip_text(0, dialogue_id)
		dialogue_item.set_metadata(0, "dialogue:" + dialogue_id)
	dialogues_item.set_collapsed(false)

	var quests_item := project_tree.create_item(root)
	quests_item.set_text(0, "任务 (%d)" % store.quests.size())
	quests_item.set_metadata(0, "quests")
	for quest_id in _sorted_quest_ids():
		var quest_item := project_tree.create_item(quests_item)
		quest_item.set_text(0, str(store.quests[quest_id].get("title", quest_id)))
		quest_item.set_tooltip_text(0, quest_id)
		quest_item.set_metadata(0, "quest:" + quest_id)
	quests_item.set_collapsed(false)

	var stories_item := project_tree.create_item(root)
	stories_item.set_text(0, "剧情 (%d)" % store.stories.size())
	stories_item.set_metadata(0, "stories")
	for story_id in _sorted_story_ids():
		var story_item := project_tree.create_item(stories_item)
		story_item.set_text(0, str(store.stories[story_id].get("title", story_id)))
		story_item.set_tooltip_text(0, story_id)
		story_item.set_metadata(0, "story:" + story_id)
	stories_item.set_collapsed(false)


func _rebuild_actor_list(select_id := "") -> void:
	actor_list.clear()
	var index := 0
	for actor_id in _sorted_actor_ids():
		var actor: Dictionary = store.actors[actor_id]
		actor_list.add_item("%s\n%s" % [actor.get("display_name", actor_id), actor_id])
		actor_list.set_item_metadata(index, actor_id)
		if actor_id == select_id:
			actor_list.select(index)
		index += 1


func _sorted_actor_ids() -> Array:
	var ids := store.actors.keys()
	ids.sort()
	return ids


func _sorted_dialogue_ids() -> Array:
	var ids := store.dialogues.keys()
	ids.sort()
	return ids


func _sorted_quest_ids() -> Array:
	var ids := store.quests.keys()
	ids.sort()
	return ids


func _sorted_story_ids() -> Array:
	var ids := store.stories.keys()
	ids.sort()
	return ids


func _on_tree_selected() -> void:
	var item := project_tree.get_selected()
	if item == null:
		return
	var metadata := str(item.get_metadata(0))
	if metadata.begins_with("actor:"):
		_select_actor(metadata.trim_prefix("actor:"))
	elif metadata.begins_with("dialogue:"):
		_open_dialogue_editor(metadata.trim_prefix("dialogue:"))
	elif metadata.begins_with("quest:"):
		_open_quest_editor(metadata.trim_prefix("quest:"))
	elif metadata.begins_with("story:"):
		_open_story_editor(metadata.trim_prefix("story:"))


func _on_actor_list_selected(index: int) -> void:
	_select_actor(str(actor_list.get_item_metadata(index)))


func _select_actor(actor_id: String) -> void:
	if not store.actors.has(actor_id):
		return
	selected_actor = store.actors[actor_id].duplicate(true)
	selected_original_id = actor_id
	_populate_inspector()
	live_workspace.set_selected_actor(actor_id)
	_log("已选择角色：[b]%s[/b]" % actor_id)


func _populate_inspector() -> void:
	id_edit.text = str(selected_actor.get("id", ""))
	display_name_edit.text = str(selected_actor.get("display_name", ""))
	notes_edit.text = str(selected_actor.get("notes", ""))
	var tags: Array = selected_actor.get("tags", [])
	tags_edit.text = ", ".join(tags)
	dirty = false
	save_button.disabled = selected_actor.is_empty()


func _clear_selection() -> void:
	selected_actor = {}
	selected_original_id = ""
	id_edit.text = ""
	display_name_edit.text = ""
	notes_edit.text = ""
	tags_edit.text = ""
	dirty = false
	save_button.disabled = true


func _new_actor() -> void:
	var suffix := 1
	var actor_id := "new_actor"
	while store.actors.has(actor_id):
		suffix += 1
		actor_id = "new_actor_%d" % suffix
	selected_actor = {
		"schema_version": 1,
		"id": actor_id,
		"display_name": "新角色",
		"notes": "",
		"tags": [],
	}
	selected_original_id = ""
	_populate_inspector()
	dirty = true
	save_button.disabled = false
	_log("创建未保存角色：%s" % actor_id)
	id_edit.grab_focus()
	id_edit.select_all()


func _duplicate_actor() -> void:
	if selected_actor.is_empty():
		return
	var base_id := str(selected_actor.get("id", "actor")) + "_copy"
	var actor_id := base_id
	var suffix := 1
	while store.actors.has(actor_id):
		suffix += 1
		actor_id = "%s_%d" % [base_id, suffix]
	selected_actor = selected_actor.duplicate(true)
	selected_actor["id"] = actor_id
	selected_actor["display_name"] = str(selected_actor.get("display_name", actor_id)) + " 副本"
	selected_original_id = ""
	_populate_inspector()
	dirty = true
	save_button.disabled = false
	_log("已复制角色为未保存资源：%s" % actor_id)


func _confirm_delete() -> void:
	if selected_actor.is_empty() or selected_original_id.is_empty():
		return
	delete_dialog.dialog_text = "确定要删除角色 '%s' 吗？\n此操作将移除对应的 JSON 文件。" % selected_original_id
	delete_dialog.popup_centered()


func _delete_selected_actor() -> void:
	var actor_id := selected_original_id
	if actor_id.is_empty():
		return
	if store.delete_actor(actor_id):
		_log("[color=orange]Deleted Actor:[/color] %s" % actor_id)
		_clear_selection()
		_rebuild_project_tree()
		_rebuild_actor_list()
	else:
		_log("[color=red]Failed to delete Actor:[/color] %s" % actor_id)
	_refresh_problems()


func _save_actor() -> void:
	if selected_actor.is_empty():
		return
	var actor_id := id_edit.text.strip_edges()
	if actor_id != selected_original_id and store.actors.has(actor_id):
		store.problems.append("角色 ID 已存在：%s" % actor_id)
		_refresh_problems()
		_log("[color=red]Save rejected: duplicate Actor ID.[/color]")
		return

	selected_actor = {
		"schema_version": 1,
		"id": actor_id,
		"display_name": display_name_edit.text.strip_edges(),
		"notes": notes_edit.text,
		"tags": _parse_tags(tags_edit.text),
	}
	if store.save_actor(selected_actor, selected_original_id):
		selected_original_id = actor_id
		dirty = false
		save_button.text = "已保存"
		get_tree().create_timer(1.0).timeout.connect(func(): save_button.text = "保存角色")
		_rebuild_project_tree()
		_rebuild_actor_list(actor_id)
		_log("[color=green]Saved Actor:[/color] %s" % actor_id)
		live_workspace.notify_resource_saved("actor", actor_id)
	else:
		_log("[color=red]Actor save failed. See Problems.[/color]")
	_refresh_problems()


func _parse_tags(text: String) -> Array[String]:
	var result: Array[String] = []
	for raw_tag in text.split(","):
		var tag := raw_tag.strip_edges()
		if not tag.is_empty() and not result.has(tag):
			result.append(tag)
	return result


func _mark_dirty_text(_value: String) -> void:
	_mark_dirty()


func _mark_dirty() -> void:
	if selected_actor.is_empty():
		return
	dirty = true
	save_button.disabled = false
	save_button.text = "保存角色 *"


func _refresh_problems() -> void:
	problems_list.clear()
	for problem in store.problems:
		problems_list.add_item(problem)


func _log(message: String) -> void:
	output_text.append_text(message + "\n")
	output_text.scroll_to_line(output_text.get_line_count())


func _build_content_pack(path: String) -> void:
	var target := path.replace("\\", "/").trim_suffix("/").path_join(str(store.project.get("id", "content_pack")))
	if store.build_content_pack(target):
		_log("[color=green]Built Content Pack:[/color] %s" % target)
	else:
		_log("[color=red]Content Pack build failed. See Problems.[/color]")
	_refresh_problems()


func _on_story_debug_updated(story_id: String, debug: Dictionary) -> void:
	for child in get_children():
		if child is StoryEditorWindow and child.original_id == story_id:
			child.apply_live_debug(debug)


func _on_pick_received(kind: String, value: Dictionary) -> void:
	if kind == "actor" and store.actors.has(str(value.get("actor_id", ""))):
		_select_actor(str(value.get("actor_id", "")))
	for child in get_children():
		if child is StoryEditorWindow:
			child.apply_pick_result(kind, value)
		elif child is QuestEditorWindow:
			child.apply_pick_result(kind, value)


func _autosave_actor() -> void:
	if dirty and not selected_original_id.is_empty():
		_save_actor()


func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.ctrl_pressed and event.keycode == KEY_S:
		_save_actor()
		get_viewport().set_input_as_handled()
