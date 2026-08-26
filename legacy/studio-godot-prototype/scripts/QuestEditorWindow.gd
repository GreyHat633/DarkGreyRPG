class_name QuestEditorWindow
extends Window

signal quest_saved(quest_id: String)
signal quest_deleted(quest_id: String)

var store: RpgProjectStore
var quest: Dictionary = {}
var original_id := ""
var selected_kind := "objective"
var selected_index := 0
var dirty := false
var updating_ui := false
var undo_stack: Array[Dictionary] = []
var redo_stack: Array[Dictionary] = []

var id_edit: LineEdit
var title_edit: LineEdit
var description_edit: TextEdit
var objective_list: ReorderableNodeList
var group_list: ReorderableNodeList
var inspector: VBoxContainer
var save_button: Button
var status_label: Label


func setup(project_store: RpgProjectStore, value: Dictionary, source_id: String) -> void:
	store = project_store
	quest = value.duplicate(true)
	original_id = source_id


func _ready() -> void:
	title = "DarkGrey RPG — 任务编辑器 (Quest Editor)"
	initial_position = Window.WINDOW_INITIAL_POSITION_CENTER_MAIN_WINDOW_SCREEN
	size = Vector2i(1240, 780)
	min_size = Vector2i(960, 620)
	close_requested.connect(queue_free)
	_build_ui()
	_refresh_all()
	var autosave := Timer.new()
	autosave.wait_time = 10.0
	autosave.autostart = true
	autosave.timeout.connect(_autosave)
	add_child(autosave)


func _build_ui() -> void:
	var root := VBoxContainer.new()
	self.theme = ThemeManager.get_theme()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_theme_constant_override("separation", 6)
	add_child(root)

	var header := GridContainer.new()
	header.columns = 4
	root.add_child(header)
	_add_label(header, "ID")
	id_edit = LineEdit.new()
	id_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(id_edit)
	_add_label(header, "标题 (Title)")
	title_edit = LineEdit.new()
	title_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title_edit)
	_add_label(header, "描述 (Description)")
	description_edit = TextEdit.new()
	description_edit.custom_minimum_size.y = 72
	description_edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	header.add_child(description_edit)
	var scope_note := Label.new()
	scope_note.text = "Quest is independent: no issuer, Dialogue, or Story ownership."
	scope_note.modulate = Color(0.72, 0.68, 0.48)
	header.add_child(scope_note)
	save_button = Button.new()
	save_button.text = "保存任务  Ctrl+S"
	save_button.pressed.connect(_save)
	header.add_child(save_button)
	id_edit.text_changed.connect(_on_header_changed)
	title_edit.text_changed.connect(_on_header_changed)
	description_edit.text_changed.connect(_on_header_text_changed)

	var split := HSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	split.split_offset = 470
	root.add_child(split)

	var structure := VBoxContainer.new()
	structure.custom_minimum_size.x = 450
	split.add_child(structure)
	var objective_toolbar := HBoxContainer.new()
	for objective_type in ["kill_entity", "collect_item", "reach_location", "interact_actor"]:
		var button := Button.new()
		button.text = "+ " + _type_label(objective_type)
		button.pressed.connect(_add_objective.bind(objective_type))
		objective_toolbar.add_child(button)
	structure.add_child(objective_toolbar)
	_add_label(structure, "OBJECTIVES — drag to reorder")
	objective_list = ReorderableNodeList.new()
	objective_list.custom_minimum_size.y = 245
	objective_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	objective_list.item_selected.connect(_select_objective)
	objective_list.item_reordered.connect(_reorder_objective)
	structure.add_child(objective_list)
	var objective_actions := HBoxContainer.new()
	for spec in [
		["Move Up", _move_objective.bind(-1)],
		["Move Down", _move_objective.bind(1)],
		["复制", _duplicate_objective],
		["删除", _delete_objective],
	]:
		var button := Button.new()
		button.text = spec[0]
		button.pressed.connect(spec[1])
		objective_actions.add_child(button)
	structure.add_child(objective_actions)

	var group_toolbar := HBoxContainer.new()
	for mode in ["ALL", "ANY", "SEQUENCE"]:
		var button := Button.new()
		button.text = "+ Group " + mode
		button.pressed.connect(_add_group.bind(mode))
		group_toolbar.add_child(button)
	structure.add_child(group_toolbar)
	_add_label(structure, "OBJECTIVE GROUPS — drag to reorder")
	group_list = ReorderableNodeList.new()
	group_list.custom_minimum_size.y = 125
	group_list.item_selected.connect(_select_group)
	group_list.item_reordered.connect(_reorder_group)
	structure.add_child(group_list)
	var group_actions := HBoxContainer.new()
	for spec in [
		["Duplicate Group", _duplicate_group],
		["Delete Group", _delete_group],
	]:
		var button := Button.new()
		button.text = spec[0]
		button.pressed.connect(spec[1])
		group_actions.add_child(button)
	structure.add_child(group_actions)

	var scroll := ScrollContainer.new()
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	split.add_child(scroll)
	inspector = VBoxContainer.new()
	inspector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(inspector)

	var footer := HBoxContainer.new()
	var undo_button := Button.new()
	undo_button.text = "撤销  Ctrl+Z"
	undo_button.pressed.connect(_undo)
	footer.add_child(undo_button)
	var redo_button := Button.new()
	redo_button.text = "重做  Ctrl+Y"
	redo_button.pressed.connect(_redo)
	footer.add_child(redo_button)
	var delete_button := Button.new()
	delete_button.text = "删除任务"
	delete_button.pressed.connect(_delete_quest)
	footer.add_child(delete_button)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(spacer)
	status_label = Label.new()
	footer.add_child(status_label)
	root.add_child(footer)


func _refresh_all() -> void:
	updating_ui = true
	id_edit.text = str(quest.get("id", ""))
	title_edit.text = str(quest.get("title", ""))
	description_edit.text = str(quest.get("description", ""))
	_refresh_structure()
	_refresh_inspector()
	updating_ui = false
	_update_status()


func _refresh_structure() -> void:
	objective_list.clear()
	var objectives: Array = quest.get("objectives", [])
	for index in objectives.size():
		var objective: Dictionary = objectives[index]
		objective_list.add_item(
			"%02d  [%s]  %s" % [index + 1, _type_label(str(objective.get("type", ""))), objective.get("id", "")]
		)
	if selected_kind == "objective" and not objectives.is_empty():
		selected_index = clampi(selected_index, 0, objectives.size() - 1)
		objective_list.select(selected_index)

	group_list.clear()
	var groups: Array = quest.get("objective_groups", [])
	for index in groups.size():
		var group: Dictionary = groups[index]
		group_list.add_item(
			"%02d  [%s]  %s  (%d)" % [
				index + 1,
				group.get("mode", ""),
				group.get("id", ""),
				group.get("objectives", []).size(),
			]
		)
	if selected_kind == "group" and not groups.is_empty():
		selected_index = clampi(selected_index, 0, groups.size() - 1)
		group_list.select(selected_index)


func _refresh_inspector() -> void:
	for child in inspector.get_children():
		child.queue_free()
	if selected_kind == "group":
		_build_group_inspector()
	else:
		_build_objective_inspector()


func apply_pick_result(kind: String, value: Dictionary) -> void:
	if selected_kind != "objective":
		return
	var objectives: Array = quest.get("objectives", [])
	if selected_index < 0 or selected_index >= objectives.size():
		return
	var objective: Dictionary = objectives[selected_index]
	var objective_type := str(objective.get("type", ""))
	var applicable := (
		(kind == "actor" and objective_type == "interact_actor")
		or (kind == "item" and objective_type == "collect_item")
		or (kind == "entity_type" and objective_type == "kill_entity")
		or (kind in ["position", "region"] and objective_type == "reach_location")
	)
	if not applicable:
		return
	_record_undo()
	var changed := false
	if kind == "actor" and objective_type == "interact_actor":
		objective["actor_id"] = str(value.get("actor_id", ""))
		changed = true
	elif kind == "item" and objective_type == "collect_item":
		objective["item"] = str(value.get("item", ""))
		objective["metadata"] = int(value.get("metadata", 0))
		changed = true
	elif kind == "entity_type" and objective_type == "kill_entity":
		objective["entity"] = str(value.get("entity_type", ""))
		changed = true
	elif kind == "position" and objective_type == "reach_location":
		for field in ["dimension", "x", "y", "z"]:
			objective[field] = value.get(field, objective.get(field, 0))
		changed = true
	elif kind == "region" and objective_type == "reach_location":
		var minimum: Dictionary = value.get("min", {})
		var maximum: Dictionary = value.get("max", {})
		objective["dimension"] = minimum.get("dimension", 0)
		objective["x"] = (float(minimum.get("x", 0)) + float(maximum.get("x", 0))) * 0.5
		objective["y"] = (float(minimum.get("y", 0)) + float(maximum.get("y", 0))) * 0.5
		objective["z"] = (float(minimum.get("z", 0)) + float(maximum.get("z", 0))) * 0.5
		objective["radius"] = maxf(
			float(maximum.get("x", 0)) - float(minimum.get("x", 0)),
			float(maximum.get("z", 0)) - float(minimum.get("z", 0))
		) * 0.5
		changed = true
	if changed:
		_mark_dirty()
		_refresh_all()


func _build_objective_inspector() -> void:
	var objectives: Array = quest.get("objectives", [])
	if selected_index < 0 or selected_index >= objectives.size():
		_add_label(inspector, "Select an Objective.")
		return
	var objective: Dictionary = objectives[selected_index]
	var objective_type := str(objective.get("type", ""))
	_add_heading(_type_label(objective_type) + " OBJECTIVE")
	_add_line_field("Objective ID", str(objective.get("id", "")), _set_objective_string.bind("id"))
	_add_text_field("描述 (Description)", str(objective.get("description", "")), _set_objective_string.bind("description"))
	if objective_type == "kill_entity":
		_add_line_field("Entity ID", str(objective.get("entity", "")), _set_objective_string.bind("entity"))
		_add_number_field("Required", float(objective.get("required", 1)), 1.0, 100000.0, 1.0, _set_objective_number.bind("required"))
	elif objective_type == "collect_item":
		_add_line_field("Item Registry ID", str(objective.get("item", "")), _set_objective_string.bind("item"))
		_add_number_field("Metadata (-1 = any)", float(objective.get("metadata", -1)), -1.0, 32767.0, 1.0, _set_objective_number.bind("metadata"))
		_add_number_field("Required", float(objective.get("required", 1)), 1.0, 100000.0, 1.0, _set_objective_number.bind("required"))
	elif objective_type == "reach_location":
		_add_number_field("Dimension", float(objective.get("dimension", 0)), -1024.0, 1024.0, 1.0, _set_objective_number.bind("dimension"))
		for field in ["x", "y", "z"]:
			_add_number_field(field.to_upper(), float(objective.get(field, 0.0)), -30000000.0, 30000000.0, 0.5, _set_objective_float.bind(field))
		_add_number_field("Radius", float(objective.get("radius", 3.0)), 0.1, 1024.0, 0.1, _set_objective_float.bind("radius"))
	elif objective_type == "interact_actor":
		var actor_ids: Array = store.actors.keys()
		actor_ids.sort()
		_add_picker_field("Target Actor", actor_ids, str(objective.get("actor_id", "")), _set_objective_picker.bind("actor_id"))
		_add_number_field("Required", float(objective.get("required", 1)), 1.0, 100000.0, 1.0, _set_objective_number.bind("required"))


func _build_group_inspector() -> void:
	var groups: Array = quest.get("objective_groups", [])
	if selected_index < 0 or selected_index >= groups.size():
		_add_label(inspector, "Select an Objective Group.")
		return
	var group: Dictionary = groups[selected_index]
	_add_heading("OBJECTIVE GROUP")
	_add_line_field("Group ID", str(group.get("id", "")), _set_group_string.bind("id"))
	_add_picker_field("Mode", ["ALL", "ANY", "SEQUENCE"], str(group.get("mode", "ALL")), _set_group_picker.bind("mode"))
	_add_text_field(
		"Objective IDs — one per line, order matters for SEQUENCE",
		"\n".join(group.get("objectives", [])),
		_set_group_members
	)
	var hint := Label.new()
	hint.text = "ALL: every member. ANY: one member. SEQUENCE: members unlock in order."
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint.modulate = Color(0.65, 0.7, 0.78)
	inspector.add_child(hint)


func _add_heading(value: String) -> void:
	var heading := Label.new()
	heading.text = value
	heading.add_theme_font_size_override("font_size", 22)
	inspector.add_child(heading)


func _add_line_field(label_text: String, value: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var edit := LineEdit.new()
	edit.text = value
	edit.text_changed.connect(callback)
	inspector.add_child(edit)


func _add_text_field(label_text: String, value: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var edit := TextEdit.new()
	edit.text = value
	edit.custom_minimum_size.y = 120
	edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	edit.text_changed.connect(func(): callback.call(edit.text))
	inspector.add_child(edit)


func _add_number_field(label_text: String, value: float, minimum: float, maximum: float, step: float, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var spin := SpinBox.new()
	spin.min_value = minimum
	spin.max_value = maximum
	spin.step = step
	spin.value = value
	spin.value_changed.connect(callback)
	inspector.add_child(spin)


func _add_picker_field(label_text: String, values: Array, selected: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var picker := OptionButton.new()
	for value in values:
		picker.add_item(str(value))
		if str(value) == selected:
			picker.select(picker.item_count - 1)
	picker.item_selected.connect(func(index: int): callback.call(picker.get_item_text(index)))
	inspector.add_child(picker)


func _on_header_changed(_value: String) -> void:
	if updating_ui:
		return
	_record_undo()
	quest["id"] = id_edit.text.strip_edges()
	quest["title"] = title_edit.text.strip_edges()
	_mark_dirty()


func _on_header_text_changed() -> void:
	if updating_ui:
		return
	_record_undo()
	quest["description"] = description_edit.text
	_mark_dirty()


func _set_objective_string(value: String, field: String) -> void:
	if updating_ui:
		return
	_record_undo()
	var objective: Dictionary = quest["objectives"][selected_index]
	if field == "id":
		var previous := str(objective.get("id", ""))
		for group_value in quest.get("objective_groups", []):
			var group: Dictionary = group_value
			var members: Array = group.get("objectives", [])
			for index in members.size():
				if members[index] == previous:
					members[index] = value
	objective[field] = value
	_mark_dirty()
	_refresh_structure()


func _set_objective_number(value: float, field: String) -> void:
	_set_objective_value(int(value), field)


func _set_objective_float(value: float, field: String) -> void:
	_set_objective_value(value, field)


func _set_objective_picker(value: String, field: String) -> void:
	_set_objective_value(value, field)


func _set_objective_value(value: Variant, field: String) -> void:
	if updating_ui:
		return
	_record_undo()
	quest["objectives"][selected_index][field] = value
	_mark_dirty()


func _set_group_string(value: String, field: String) -> void:
	_set_group_value(value, field)


func _set_group_picker(value: String, field: String) -> void:
	_set_group_value(value, field)


func _set_group_value(value: Variant, field: String) -> void:
	if updating_ui:
		return
	_record_undo()
	quest["objective_groups"][selected_index][field] = value
	_mark_dirty()
	_refresh_structure()


func _set_group_members(value: String) -> void:
	if updating_ui:
		return
	_record_undo()
	var members: Array[String] = []
	for line in value.split("\n"):
		var objective_id := line.strip_edges()
		if not objective_id.is_empty() and not members.has(objective_id):
			members.append(objective_id)
	quest["objective_groups"][selected_index]["objectives"] = members
	_mark_dirty()
	_refresh_structure()


func _add_objective(objective_type: String) -> void:
	_record_undo()
	var objective_id := _unique_id(objective_type)
	var objective := _default_objective(objective_type, objective_id)
	quest["objectives"].append(objective)
	var groups: Array = quest.get("objective_groups", [])
	if groups.is_empty():
		groups.append({"id": "main", "mode": "ALL", "objectives": []})
	groups[0]["objectives"].append(objective_id)
	selected_kind = "objective"
	selected_index = quest["objectives"].size() - 1
	_mark_dirty()
	_refresh_all()


func _default_objective(objective_type: String, objective_id: String) -> Dictionary:
	var base := {"id": objective_id, "type": objective_type, "description": "New objective"}
	if objective_type == "kill_entity":
		base.merge({"entity": "Slime", "required": 1})
	elif objective_type == "collect_item":
		base.merge({"item": "minecraft:stone", "metadata": -1, "required": 1})
	elif objective_type == "reach_location":
		base.merge({"dimension": 0, "x": 0.0, "y": 64.0, "z": 0.0, "radius": 3.0})
	else:
		var actor_ids: Array = store.actors.keys()
		actor_ids.sort()
		base.merge({"actor_id": str(actor_ids[0]) if not actor_ids.is_empty() else "", "required": 1})
	return base


func _duplicate_objective() -> void:
	var objectives: Array = quest.get("objectives", [])
	if selected_kind != "objective" or selected_index < 0 or selected_index >= objectives.size():
		return
	_record_undo()
	var original: Dictionary = objectives[selected_index]
	var copy := original.duplicate(true)
	copy["id"] = _unique_id(str(original.get("id", "objective")) + "_copy")
	objectives.insert(selected_index + 1, copy)
	for group_value in quest.get("objective_groups", []):
		var members: Array = group_value.get("objectives", [])
		var member_index := members.find(original.get("id", ""))
		if member_index >= 0:
			members.insert(member_index + 1, copy["id"])
			break
	selected_index += 1
	_mark_dirty()
	_refresh_all()


func _delete_objective() -> void:
	var objectives: Array = quest.get("objectives", [])
	if selected_kind != "objective" or objectives.size() <= 1 or selected_index < 0 or selected_index >= objectives.size():
		return
	_record_undo()
	var removed_id := str(objectives[selected_index].get("id", ""))
	objectives.remove_at(selected_index)
	for group_value in quest.get("objective_groups", []):
		group_value["objectives"].erase(removed_id)
	selected_index = min(selected_index, objectives.size() - 1)
	_mark_dirty()
	_refresh_all()


func _move_objective(offset: int) -> void:
	if selected_kind == "objective":
		_reorder_objective(selected_index, selected_index + offset)


func _reorder_objective(from_index: int, to_index: int) -> void:
	var objectives: Array = quest.get("objectives", [])
	if from_index < 0 or to_index < 0 or from_index >= objectives.size() or to_index >= objectives.size():
		return
	_record_undo()
	var objective: Variant = objectives.pop_at(from_index)
	objectives.insert(to_index, objective)
	selected_kind = "objective"
	selected_index = to_index
	_mark_dirty()
	_refresh_all()


func _add_group(mode: String) -> void:
	_record_undo()
	var groups: Array = quest.get("objective_groups", [])
	groups.append({"id": _unique_group_id("group"), "mode": mode, "objectives": []})
	selected_kind = "group"
	selected_index = groups.size() - 1
	_mark_dirty()
	_refresh_all()


func _duplicate_group() -> void:
	var groups: Array = quest.get("objective_groups", [])
	if selected_kind != "group" or selected_index < 0 or selected_index >= groups.size():
		return
	_record_undo()
	var copy: Dictionary = groups[selected_index].duplicate(true)
	copy["id"] = _unique_group_id(str(copy.get("id", "group")) + "_copy")
	copy["objectives"] = []
	groups.insert(selected_index + 1, copy)
	selected_index += 1
	_mark_dirty()
	_refresh_all()


func _delete_group() -> void:
	var groups: Array = quest.get("objective_groups", [])
	if selected_kind != "group" or groups.size() <= 1 or selected_index < 0 or selected_index >= groups.size():
		return
	if not groups[selected_index].get("objectives", []).is_empty():
		status_label.text = "Move this group's Objectives before deleting it."
		return
	_record_undo()
	groups.remove_at(selected_index)
	selected_index = min(selected_index, groups.size() - 1)
	_mark_dirty()
	_refresh_all()


func _reorder_group(from_index: int, to_index: int) -> void:
	var groups: Array = quest.get("objective_groups", [])
	if from_index < 0 or to_index < 0 or from_index >= groups.size() or to_index >= groups.size():
		return
	_record_undo()
	var group: Variant = groups.pop_at(from_index)
	groups.insert(to_index, group)
	selected_kind = "group"
	selected_index = to_index
	_mark_dirty()
	_refresh_all()


func _select_objective(index: int) -> void:
	selected_kind = "objective"
	selected_index = index
	updating_ui = true
	_refresh_inspector()
	updating_ui = false


func _select_group(index: int) -> void:
	selected_kind = "group"
	selected_index = index
	updating_ui = true
	_refresh_inspector()
	updating_ui = false


func _save() -> void:
	quest["id"] = id_edit.text.strip_edges()
	quest["title"] = title_edit.text.strip_edges()
	quest["description"] = description_edit.text
	if quest["id"] != original_id and store.quests.has(quest["id"]):
		status_label.text = "Duplicate Quest ID"
		return
	if store.save_quest(quest, original_id):
		original_id = quest["id"]
		dirty = false
		status_label.text = "已保存"
		quest_saved.emit(original_id)
	else:
		status_label.text = "验证失败 — 请查看主界面的“问题”面板"


func _delete_quest() -> void:
	if original_id.is_empty():
		queue_free()
		return
	if store.delete_quest(original_id):
		quest_deleted.emit(original_id)
		queue_free()


func _record_undo() -> void:
	if updating_ui:
		return
	undo_stack.append(quest.duplicate(true))
	if undo_stack.size() > 100:
		undo_stack.pop_front()
	redo_stack.clear()


func _undo() -> void:
	if undo_stack.is_empty():
		return
	redo_stack.append(quest.duplicate(true))
	quest = undo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _redo() -> void:
	if redo_stack.is_empty():
		return
	undo_stack.append(quest.duplicate(true))
	quest = redo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _mark_dirty() -> void:
	dirty = true
	_update_status()


func _autosave() -> void:
	if dirty and not original_id.is_empty():
		_save()


func _update_status() -> void:
	save_button.text = "保存任务 *  Ctrl+S" if dirty else "保存任务  Ctrl+S"


func _unique_id(base: String) -> String:
	var normalized := base.to_lower().replace(" ", "_")
	var used := {}
	for objective_value in quest.get("objectives", []):
		used[str(objective_value.get("id", ""))] = true
	var result := normalized
	var suffix := 2
	while used.has(result):
		result = "%s_%d" % [normalized, suffix]
		suffix += 1
	return result


func _unique_group_id(base: String) -> String:
	var used := {}
	for group_value in quest.get("objective_groups", []):
		used[str(group_value.get("id", ""))] = true
	var result := base.to_lower().replace(" ", "_")
	var suffix := 2
	while used.has(result):
		result = "%s_%d" % [base, suffix]
		suffix += 1
	return result


func _type_label(value: String) -> String:
	return {
		"kill_entity": "KillEntity",
		"collect_item": "CollectItem",
		"reach_location": "ReachLocation",
		"interact_actor": "InteractActor",
	}.get(value, value)


func _add_label(parent: Control, value: String) -> void:
	var label := Label.new()
	label.text = value
	parent.add_child(label)


func _unhandled_key_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed and event.ctrl_pressed):
		return
	if event.keycode == KEY_S:
		_save()
	elif event.keycode == KEY_Z:
		_undo()
	elif event.keycode == KEY_Y:
		_redo()
	else:
		return
	get_viewport().set_input_as_handled()
