class_name DialogueEditorWindow
extends Window

signal dialogue_saved(dialogue_id: String)
signal dialogue_deleted(dialogue_id: String)

var store: RpgProjectStore
var dialogue: Dictionary = {}
var original_id := ""
var selected_node := -1
var dirty := false
var updating_ui := false
var undo_stack: Array[Dictionary] = []
var redo_stack: Array[Dictionary] = []
var search_cursor := -1

var id_edit: LineEdit
var title_edit: LineEdit
var speakers_edit: LineEdit
var entry_picker: OptionButton
var node_list: ReorderableNodeList
var inspector: VBoxContainer
var search_edit: LineEdit
var replace_edit: LineEdit
var save_button: Button
var status_label: Label


func setup(project_store: RpgProjectStore, value: Dictionary, source_id: String) -> void:
	store = project_store
	dialogue = value.duplicate(true)
	original_id = source_id


func _ready() -> void:
	title = "DarkGrey RPG — 对话编辑器 (Dialogue Editor)"
	initial_position = Window.WINDOW_INITIAL_POSITION_CENTER_MAIN_WINDOW_SCREEN
	size = Vector2i(1180, 760)
	min_size = Vector2i(900, 600)
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
	header.columns = 6
	root.add_child(header)
	_add_label(header, "ID")
	id_edit = LineEdit.new()
	id_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(id_edit)
	_add_label(header, "标题 (Title)")
	title_edit = LineEdit.new()
	title_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title_edit)
	_add_label(header, "入口节点 (Entry)")
	entry_picker = OptionButton.new()
	header.add_child(entry_picker)
	_add_label(header, "说话者 (Speakers)")
	speakers_edit = LineEdit.new()
	speakers_edit.placeholder_text = "actor_id, another_actor"
	speakers_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(speakers_edit)
	var speaker_hint := Label.new()
	speaker_hint.text = "角色 (Actor) 资源 IDs"
	speaker_hint.modulate = Color(0.6, 0.65, 0.72)
	header.add_child(speaker_hint)
	var spacer := Control.new()
	header.add_child(spacer)
	var save := Button.new()
	save.text = "保存对话  Ctrl+S"
	save.pressed.connect(_save)
	save_button = save
	header.add_child(save)

	id_edit.text_changed.connect(_on_header_changed)
	title_edit.text_changed.connect(_on_header_changed)
	speakers_edit.text_changed.connect(_on_header_changed)
	entry_picker.item_selected.connect(_on_entry_selected)

	var split := HSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	split.split_offset = 400
	root.add_child(split)

	var left := VBoxContainer.new()
	left.custom_minimum_size.x = 380
	split.add_child(left)
	var search_row := HBoxContainer.new()
	search_edit = LineEdit.new()
	search_edit.placeholder_text = "搜索对话内容"
	search_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	search_row.add_child(search_edit)
	var find_button := Button.new()
	find_button.text = "查找下一个"
	find_button.pressed.connect(_find_next)
	search_row.add_child(find_button)
	left.add_child(search_row)
	var replace_row := HBoxContainer.new()
	replace_edit = LineEdit.new()
	replace_edit.placeholder_text = "替换为"
	replace_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	replace_row.add_child(replace_edit)
	var replace_button := Button.new()
	replace_button.text = "全部替换"
	replace_button.pressed.connect(_replace_all)
	replace_row.add_child(replace_button)
	left.add_child(replace_row)

	var add_row := HBoxContainer.new()
	for node_type in ["Line", "Choice", "Jump", "End"]:
		var button := Button.new()
		button.text = "+ " + node_type
		button.pressed.connect(_add_node.bind(node_type.to_lower()))
		add_row.add_child(button)
	left.add_child(add_row)

	node_list = ReorderableNodeList.new()
	node_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	node_list.item_selected.connect(_select_node)
	node_list.item_reordered.connect(_reorder_node)
	left.add_child(node_list)

	var node_actions := HBoxContainer.new()
	for spec in [
		["上移", _move_node.bind(-1)],
		["下移", _move_node.bind(1)],
		["复制", _duplicate_node],
		["删除", _delete_node],
	]:
		var button := Button.new()
		button.text = spec[0]
		button.pressed.connect(spec[1])
		node_actions.add_child(button)
	left.add_child(node_actions)

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
	var delete_dialogue_button := Button.new()
	delete_dialogue_button.text = "删除对话"
	delete_dialogue_button.pressed.connect(_delete_dialogue)
	footer.add_child(delete_dialogue_button)
	var footer_spacer := Control.new()
	footer_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(footer_spacer)
	status_label = Label.new()
	footer.add_child(status_label)
	root.add_child(footer)


func _refresh_all() -> void:
	updating_ui = true
	id_edit.text = str(dialogue.get("id", ""))
	title_edit.text = str(dialogue.get("title", ""))
	speakers_edit.text = ", ".join(dialogue.get("speakers", []))
	_refresh_node_list()
	_refresh_entry_picker()
	_refresh_inspector()
	updating_ui = false
	_update_status()


func _refresh_node_list() -> void:
	node_list.clear()
	var nodes: Array = dialogue.get("nodes", [])
	for index in nodes.size():
		var node: Dictionary = nodes[index]
		node_list.add_item("%02d  [%s]  %s" % [index + 1, str(node.get("type", "")).to_upper(), node.get("id", "")])
	if not nodes.is_empty():
		selected_node = clampi(selected_node, 0, nodes.size() - 1)
		node_list.select(selected_node)
	else:
		selected_node = -1


func _refresh_entry_picker() -> void:
	var current := str(dialogue.get("entry", ""))
	entry_picker.clear()
	for node_value in dialogue.get("nodes", []):
		var node: Dictionary = node_value
		entry_picker.add_item(str(node.get("id", "")))
		if node.get("id", "") == current:
			entry_picker.select(entry_picker.item_count - 1)


func _refresh_inspector() -> void:
	for child in inspector.get_children():
		child.queue_free()
	var nodes: Array = dialogue.get("nodes", [])
	if selected_node < 0 or selected_node >= nodes.size():
		_add_label(inspector, "请选择一个对话节点。")
		return
	var node: Dictionary = nodes[selected_node]
	var heading := Label.new()
	heading.text = "%s NODE" % str(node.get("type", "")).to_upper()
	heading.add_theme_font_size_override("font_size", 22)
	inspector.add_child(heading)
	_add_line_field("节点 ID", str(node.get("id", "")), _set_node_string.bind("id"))
	var node_type := str(node.get("type", ""))
	if node_type == "line":
		_add_picker_field("说话者 (Speaker)", dialogue.get("speakers", []), str(node.get("speaker", "")), _set_node_picker.bind("speaker"))
		_add_text_field("文本内容 (Text)", str(node.get("text", "")), _set_node_string.bind("text"), 220)
		_add_node_target("下一步 (Next)", str(node.get("next", "")), _set_node_picker.bind("next"))
	elif node_type == "choice":
		_add_text_field("提示词 (Prompt)", str(node.get("prompt", "")), _set_node_string.bind("prompt"), 100)
		var option_lines: Array[String] = []
		for option_value in node.get("choices", []):
			var option: Dictionary = option_value
			option_lines.append("%s | %s" % [option.get("text", ""), option.get("next", "")])
		_add_text_field(
			"选项分支 (每行一个) 格式: 标签文本 | 目标节点",
			"\n".join(option_lines),
			_set_choices,
			260
		)
		var hint := Label.new()
		hint.text = "选项只能通过 End 节点返回结果。它不能直接在这里开始任务。"
		hint.modulate = Color(0.7, 0.65, 0.5)
		inspector.add_child(hint)
	elif node_type == "jump":
		_add_node_target("目标节点 (Target)", str(node.get("target", "")), _set_node_picker.bind("target"))
	elif node_type == "end":
		_add_line_field("命名结果 (Named Result)", str(node.get("result", "")), _set_node_string.bind("result"))


func _add_line_field(label_text: String, value: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var edit := LineEdit.new()
	edit.text = value
	edit.text_changed.connect(callback)
	inspector.add_child(edit)


func _add_text_field(label_text: String, value: String, callback: Callable, height: int) -> void:
	_add_label(inspector, label_text)
	var edit := TextEdit.new()
	edit.text = value
	edit.custom_minimum_size.y = height
	edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	edit.text_changed.connect(func(): callback.call(edit.text))
	inspector.add_child(edit)


func _add_picker_field(label_text: String, values: Array, selected: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var picker := OptionButton.new()
	for value in values:
		picker.add_item(str(value))
		if str(value) == selected:
			picker.select(picker.item_count - 1)
	picker.item_selected.connect(func(index: int): callback.call(picker.get_item_text(index)))
	inspector.add_child(picker)


func _add_node_target(label_text: String, selected: String, callback: Callable) -> void:
	var ids: Array = []
	for node_value in dialogue.get("nodes", []):
		ids.append(str(node_value.get("id", "")))
	_add_picker_field(label_text, ids, selected, callback)


func _on_header_changed(_value: String) -> void:
	if updating_ui:
		return
	_record_undo()
	dialogue["id"] = id_edit.text.strip_edges()
	dialogue["title"] = title_edit.text.strip_edges()
	dialogue["speakers"] = _parse_ids(speakers_edit.text)
	_mark_dirty()


func _on_entry_selected(index: int) -> void:
	if updating_ui or index < 0:
		return
	_record_undo()
	dialogue["entry"] = entry_picker.get_item_text(index)
	_mark_dirty()


func _set_node_string(value: String, field: String) -> void:
	if updating_ui:
		return
	_record_undo()
	dialogue["nodes"][selected_node][field] = value
	_mark_dirty()
	if field == "id":
		_refresh_node_list()
		_refresh_entry_picker()


func _set_node_picker(value: String, field: String) -> void:
	_set_node_string(value, field)


func _set_choices(value: String) -> void:
	if updating_ui:
		return
	_record_undo()
	var choices: Array[Dictionary] = []
	for line in value.split("\n"):
		var separator := line.rfind("|")
		if separator < 0:
			continue
		var label := line.substr(0, separator).strip_edges()
		var target := line.substr(separator + 1).strip_edges()
		choices.append({"text": label, "next": target})
	dialogue["nodes"][selected_node]["choices"] = choices
	_mark_dirty()


func _add_node(node_type: String) -> void:
	_record_undo()
	var node_id := _unique_node_id(node_type)
	var nodes: Array = dialogue.get("nodes", [])
	var fallback_target := str(nodes[0].get("id", "")) if not nodes.is_empty() else node_id
	var node: Dictionary
	if node_type == "line":
		var speakers: Array = dialogue.get("speakers", [])
		node = {
			"id": node_id,
			"type": "line",
			"speaker": str(speakers[0]) if not speakers.is_empty() else "",
			"text": "新对话内容。",
			"next": fallback_target,
		}
	elif node_type == "choice":
		node = {
			"id": node_id,
			"type": "choice",
			"prompt": "",
			"choices": [{"text": "继续", "next": fallback_target}],
		}
	elif node_type == "jump":
		node = {"id": node_id, "type": "jump", "target": fallback_target}
	else:
		node = {"id": node_id, "type": "end", "result": "done"}
	nodes.append(node)
	dialogue["nodes"] = nodes
	if str(dialogue.get("entry", "")).is_empty():
		dialogue["entry"] = node_id
	selected_node = nodes.size() - 1
	_mark_dirty()
	_refresh_all()


func _duplicate_node() -> void:
	var nodes: Array = dialogue.get("nodes", [])
	if selected_node < 0 or selected_node >= nodes.size():
		return
	_record_undo()
	var copy: Dictionary = nodes[selected_node].duplicate(true)
	copy["id"] = _unique_node_id(str(copy.get("id", "node")) + "_copy")
	nodes.insert(selected_node + 1, copy)
	selected_node += 1
	_mark_dirty()
	_refresh_all()


func _delete_node() -> void:
	var nodes: Array = dialogue.get("nodes", [])
	if selected_node < 0 or selected_node >= nodes.size() or nodes.size() <= 1:
		return
	_record_undo()
	nodes.remove_at(selected_node)
	selected_node = min(selected_node, nodes.size() - 1)
	_mark_dirty()
	_refresh_all()


func _move_node(offset: int) -> void:
	_reorder_node(selected_node, selected_node + offset)


func _reorder_node(from_index: int, to_index: int) -> void:
	var nodes: Array = dialogue.get("nodes", [])
	if from_index < 0 or to_index < 0 or from_index >= nodes.size() or to_index >= nodes.size():
		return
	_record_undo()
	var node: Variant = nodes.pop_at(from_index)
	nodes.insert(to_index, node)
	selected_node = to_index
	_mark_dirty()
	_refresh_all()


func _select_node(index: int) -> void:
	selected_node = index
	updating_ui = true
	_refresh_inspector()
	updating_ui = false


func _find_next() -> void:
	var needle := search_edit.text.to_lower()
	if needle.is_empty():
		return
	var nodes: Array = dialogue.get("nodes", [])
	for step in nodes.size():
		var index := (search_cursor + 1 + step) % nodes.size()
		if _node_search_text(nodes[index]).to_lower().contains(needle):
			search_cursor = index
			selected_node = index
			_refresh_all()
			status_label.text = "在节点 %s 中找到" % nodes[index].get("id", "")
			return
	status_label.text = "未找到匹配项"


func _replace_all() -> void:
	var needle := search_edit.text
	if needle.is_empty():
		return
	_record_undo()
	var replacements := 0
	for node_value in dialogue.get("nodes", []):
		var node: Dictionary = node_value
		for field in ["text", "prompt"]:
			if node.has(field):
				var old := str(node[field])
				var updated := old.replace(needle, replace_edit.text)
				if updated != old:
					node[field] = updated
					replacements += 1
		if node.get("type") == "choice":
			for option_value in node.get("choices", []):
				var option: Dictionary = option_value
				var old := str(option.get("text", ""))
				var updated := old.replace(needle, replace_edit.text)
				if updated != old:
					option["text"] = updated
					replacements += 1
	if replacements > 0:
		_mark_dirty()
		_refresh_all()
	status_label.text = "替换了 %d 个字段" % replacements


func _node_search_text(node: Dictionary) -> String:
	var result := "%s %s %s" % [node.get("id", ""), node.get("text", ""), node.get("prompt", "")]
	for option_value in node.get("choices", []):
		result += " " + str(option_value.get("text", ""))
	return result


func _save() -> void:
	dialogue["id"] = id_edit.text.strip_edges()
	dialogue["title"] = title_edit.text.strip_edges()
	dialogue["speakers"] = _parse_ids(speakers_edit.text)
	if entry_picker.selected >= 0:
		dialogue["entry"] = entry_picker.get_item_text(entry_picker.selected)
	if dialogue["id"] != original_id and store.dialogues.has(dialogue["id"]):
		status_label.text = "重复的对话 ID"
		return
	if store.save_dialogue(dialogue, original_id):
		original_id = dialogue["id"]
		dirty = false
		status_label.text = "已保存"
		dialogue_saved.emit(original_id)
	else:
		status_label.text = "验证失败 — 请查看主界面的“问题”面板"


func _delete_dialogue() -> void:
	if original_id.is_empty():
		queue_free()
		return
	if store.delete_dialogue(original_id):
		dialogue_deleted.emit(original_id)
		queue_free()


func _record_undo() -> void:
	if updating_ui:
		return
	undo_stack.append(dialogue.duplicate(true))
	if undo_stack.size() > 100:
		undo_stack.pop_front()
	redo_stack.clear()


func _undo() -> void:
	if undo_stack.is_empty():
		return
	redo_stack.append(dialogue.duplicate(true))
	dialogue = undo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _redo() -> void:
	if redo_stack.is_empty():
		return
	undo_stack.append(dialogue.duplicate(true))
	dialogue = redo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _mark_dirty() -> void:
	dirty = true
	_update_status()


func _autosave() -> void:
	if dirty and not original_id.is_empty():
		_save()


func _update_status() -> void:
	save_button.text = "保存对话 *  Ctrl+S" if dirty else "保存对话  Ctrl+S"


func _unique_node_id(base: String) -> String:
	var normalized := base.to_lower().replace(" ", "_")
	var used := {}
	for node_value in dialogue.get("nodes", []):
		used[str(node_value.get("id", ""))] = true
	var result := normalized
	var suffix := 2
	while used.has(result):
		result = "%s_%d" % [normalized, suffix]
		suffix += 1
	return result


func _parse_ids(value: String) -> Array[String]:
	var result: Array[String] = []
	for part in value.split(","):
		var actor_id := part.strip_edges()
		if not actor_id.is_empty() and not result.has(actor_id):
			result.append(actor_id)
	return result


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
