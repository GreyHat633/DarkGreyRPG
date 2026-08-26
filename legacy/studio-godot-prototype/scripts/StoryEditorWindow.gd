class_name StoryEditorWindow
extends Window

signal story_saved(story_id: String)
signal story_deleted(story_id: String)

const NODE_TYPES := [
	"interact_actor",
	"enter_region",
	"quest_completed",
	"play_dialogue",
	"start_quest",
	"complete_quest",
	"branch",
	"sequence",
	"quest_state",
	"has_item",
	"variable_compare",
	"give_item",
	"give_xp",
	"send_message",
	"set_variable",
	"end",
]

var store: RpgProjectStore
var story: Dictionary = {}
var original_id := ""
var selected_node_id := ""
var dirty := false
var updating_ui := false
var undo_stack: Array[Dictionary] = []
var redo_stack: Array[Dictionary] = []
var copied_node: Dictionary = {}
var output_ports := {}

var id_edit: LineEdit
var title_edit: LineEdit
var entry_picker: OptionButton
var add_search: LineEdit
var add_type_picker: OptionButton
var node_search: LineEdit
var graph: GraphEdit
var inspector: VBoxContainer
var problems_list: ItemList
var status_label: Label
var save_button: Button


func setup(project_store: RpgProjectStore, value: Dictionary, source_id: String, focus_node := "") -> void:
	store = project_store
	story = value.duplicate(true)
	original_id = source_id
	selected_node_id = focus_node


func _ready() -> void:
	title = "DarkGrey RPG — Story Graph"
	initial_position = Window.WINDOW_INITIAL_POSITION_CENTER_MAIN_WINDOW_SCREEN
	size = Vector2i(1380, 840)
	min_size = Vector2i(1000, 650)
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
	root.add_theme_constant_override("separation", 5)
	add_child(root)

	var header := HBoxContainer.new()
	root.add_child(header)
	_add_label(header, "ID")
	id_edit = LineEdit.new()
	id_edit.custom_minimum_size.x = 180
	header.add_child(id_edit)
	_add_label(header, "标题 (Title)")
	title_edit = LineEdit.new()
	title_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title_edit)
	_add_label(header, "Entry")
	entry_picker = OptionButton.new()
	entry_picker.custom_minimum_size.x = 160
	header.add_child(entry_picker)
	save_button = Button.new()
	save_button.text = "保存剧情  Ctrl+S"
	save_button.pressed.connect(_save)
	header.add_child(save_button)
	id_edit.text_changed.connect(_on_header_changed)
	title_edit.text_changed.connect(_on_header_changed)
	entry_picker.item_selected.connect(_on_entry_selected)

	var toolbar := HBoxContainer.new()
	root.add_child(toolbar)
	add_search = LineEdit.new()
	add_search.placeholder_text = "Search Add Node types"
	add_search.custom_minimum_size.x = 220
	add_search.text_changed.connect(_filter_add_types)
	toolbar.add_child(add_search)
	add_type_picker = OptionButton.new()
	add_type_picker.custom_minimum_size.x = 190
	toolbar.add_child(add_type_picker)
	var add_button := Button.new()
	add_button.text = "+ Add Node"
	add_button.pressed.connect(_add_selected_type)
	toolbar.add_child(add_button)
	node_search = LineEdit.new()
	node_search.placeholder_text = "Find node ID, type, or property"
	node_search.custom_minimum_size.x = 220
	node_search.text_submitted.connect(_search_nodes)
	toolbar.add_child(node_search)
	var find_button := Button.new()
	find_button.text = "Find Next"
	find_button.pressed.connect(func(): _search_nodes(node_search.text))
	toolbar.add_child(find_button)
	for spec in [
		["Duplicate", _duplicate_selected],
		["Copy", _copy_selected],
		["Paste", _paste_node],
		["Delete", _delete_selected],
		["Undo", _undo],
		["Redo", _redo],
	]:
		var button := Button.new()
		button.text = spec[0]
		button.pressed.connect(spec[1])
		toolbar.add_child(button)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	toolbar.add_child(spacer)
	var delete_story_button := Button.new()
	delete_story_button.text = "删除剧情"
	delete_story_button.pressed.connect(_delete_story)
	toolbar.add_child(delete_story_button)

	var split := HSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	split.split_offset = 1000
	root.add_child(split)
	graph = GraphEdit.new()
	graph.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	graph.size_flags_vertical = Control.SIZE_EXPAND_FILL
	graph.minimap_enabled = true
	graph.show_zoom_label = true
	graph.connection_request.connect(_on_connection_request)
	graph.disconnection_request.connect(_on_disconnection_request)
	graph.node_selected.connect(_on_graph_node_selected)
	split.add_child(graph)

	var right := VBoxContainer.new()
	right.custom_minimum_size.x = 350
	split.add_child(right)
	var inspector_scroll := ScrollContainer.new()
	inspector_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right.add_child(inspector_scroll)
	inspector = VBoxContainer.new()
	inspector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	inspector_scroll.add_child(inspector)
	_add_label(right, "PROBLEMS — double-click to locate")
	problems_list = ItemList.new()
	problems_list.custom_minimum_size.y = 175
	problems_list.item_activated.connect(_on_problem_activated)
	right.add_child(problems_list)

	status_label = Label.new()
	root.add_child(status_label)
	_filter_add_types("")


func _refresh_all() -> void:
	updating_ui = true
	id_edit.text = str(story.get("id", ""))
	title_edit.text = str(story.get("title", ""))
	_refresh_entry_picker()
	_refresh_graph()
	_refresh_inspector()
	_refresh_problems()
	updating_ui = false
	_update_status()


func _refresh_entry_picker() -> void:
	entry_picker.clear()
	var entry := str(story.get("entry", ""))
	for node_value in story.get("nodes", []):
		var node: Dictionary = node_value
		entry_picker.add_item(str(node.get("id", "")))
		if node.get("id", "") == entry:
			entry_picker.select(entry_picker.item_count - 1)


func _refresh_graph() -> void:
	graph.clear_connections()
	for child in graph.get_children():
		if child is GraphNode:
			graph.remove_child(child)
			child.queue_free()
	output_ports.clear()
	for node_value in story.get("nodes", []):
		var node: Dictionary = node_value
		var visual := _create_graph_node(node)
		graph.add_child(visual)
	for connection_value in story.get("connections", []):
		var connection: Dictionary = connection_value
		var from_id := str(connection.get("from", ""))
		var to_id := str(connection.get("to", ""))
		var output := str(connection.get("output", ""))
		var ports: Array = output_ports.get(from_id, [])
		var port := ports.find(output)
		if port >= 0 and graph.has_node(NodePath(from_id)) and graph.has_node(NodePath(to_id)):
			graph.connect_node(from_id, port, to_id, 0)
	if not selected_node_id.is_empty() and graph.has_node(NodePath(selected_node_id)):
		var selected_visual := graph.get_node(NodePath(selected_node_id)) as GraphNode
		selected_visual.selected = true


func apply_live_debug(debug: Dictionary) -> void:
	for child in graph.get_children():
		if child is GraphNode:
			child.modulate = Color.WHITE
	for node_id in debug.get("previous_nodes", []):
		if graph.has_node(NodePath(str(node_id))):
			(graph.get_node(NodePath(str(node_id))) as GraphNode).modulate = Color(0.55, 1.0, 0.65)
	var current := str(debug.get("current_node", ""))
	if debug.get("state", "") != "IDLE" and graph.has_node(NodePath(current)):
		var color := Color(0.45, 0.8, 1.0)
		if debug.get("state", "") == "WAITING":
			color = Color(1.0, 0.82, 0.35)
		elif debug.get("state", "") == "ERROR":
			color = Color(1.0, 0.35, 0.35)
		var visual := graph.get_node(NodePath(current)) as GraphNode
		visual.modulate = color
	status_label.text = (
		"Live %s @ %s — %s"
		% [debug.get("state", ""), current, debug.get("explanation", "")]
	)


func apply_pick_result(kind: String, value: Dictionary) -> void:
	var node := _find_node(selected_node_id)
	if node.is_empty():
		return
	var properties: Dictionary = node.get("properties", {})
	var node_type := str(node.get("type", ""))
	var applicable := (
		(kind == "actor" and properties.has("actor_id"))
		or (kind == "item" and properties.has("item"))
		or (kind in ["position", "region"] and node_type == "enter_region")
	)
	if not applicable:
		return
	_record_undo()
	var changed := false
	if kind == "actor" and properties.has("actor_id"):
		properties["actor_id"] = str(value.get("actor_id", ""))
		changed = true
	elif kind == "item" and properties.has("item"):
		properties["item"] = str(value.get("item", ""))
		properties["metadata"] = int(value.get("metadata", 0))
		changed = true
	elif kind == "position" and node_type == "enter_region":
		for field in ["dimension", "x", "y", "z"]:
			properties[field] = value.get(field, properties.get(field, 0))
		changed = true
	elif kind == "region" and node_type == "enter_region":
		var minimum: Dictionary = value.get("min", {})
		var maximum: Dictionary = value.get("max", {})
		properties["dimension"] = minimum.get("dimension", 0)
		properties["x"] = (float(minimum.get("x", 0)) + float(maximum.get("x", 0))) * 0.5
		properties["y"] = (float(minimum.get("y", 0)) + float(maximum.get("y", 0))) * 0.5
		properties["z"] = (float(minimum.get("z", 0)) + float(maximum.get("z", 0))) * 0.5
		properties["radius"] = maxf(
			float(maximum.get("x", 0)) - float(minimum.get("x", 0)),
			float(maximum.get("z", 0)) - float(minimum.get("z", 0))
		) * 0.5
		changed = true
	if changed:
		_mark_dirty()
		_refresh_all()


func _create_graph_node(node: Dictionary) -> GraphNode:
	var visual := GraphNode.new()
	var node_id := str(node.get("id", ""))
	var node_type := str(node.get("type", ""))
	visual.name = node_id
	visual.title = _type_label(node_type) + " — " + node_id
	var position: Dictionary = node.get("position", {})
	visual.position_offset = Vector2(float(position.get("x", 0.0)), float(position.get("y", 0.0)))
	visual.resizable = true
	visual.custom_minimum_size = Vector2(220, 0)

	var outputs := _outputs_for_node(node)
	output_ports[node_id] = outputs
	var row_count: int = maxi(1, outputs.size())
	for index in row_count:
		var row := HBoxContainer.new()
		var label := Label.new()
		if index == 0:
			label.text = _node_summary(node)
		else:
			label.text = ""
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(label)
		var output_label := Label.new()
		output_label.text = str(outputs[index]) if index < outputs.size() else ""
		row.add_child(output_label)
		visual.add_child(row)
		visual.set_slot(
			index,
			index == 0,
			0,
			Color(0.65, 0.72, 0.9),
			index < outputs.size(),
			0,
			_output_color(str(outputs[index])) if index < outputs.size() else Color.WHITE
		)
	visual.position_offset_changed.connect(_on_node_moved.bind(node_id, visual))
	return visual


func _outputs_for_node(node: Dictionary) -> Array[String]:
	var node_type := str(node.get("type", ""))
	if node_type == "end":
		return []
	if node_type in ["branch", "quest_state", "has_item", "variable_compare"]:
		return ["true", "false"]
	if node_type == "sequence":
		var sequence_outputs: Array[String] = []
		var highest_output := 0
		for connection_value in story.get("connections", []):
			if connection_value.get("from") == node.get("id"):
				var output := str(connection_value.get("output", ""))
				sequence_outputs.append(output)
				highest_output = maxi(highest_output, int(output))
		sequence_outputs.sort_custom(func(left: String, right: String): return int(left) < int(right))
		sequence_outputs.append(str(highest_output + 1))
		return sequence_outputs
	if node_type == "play_dialogue":
		var dialogue_id := str(node.get("properties", {}).get("dialogue_id", ""))
		var results: Array[String] = []
		if store.dialogues.has(dialogue_id):
			for dialogue_node_value in store.dialogues[dialogue_id].get("nodes", []):
				if dialogue_node_value.get("type") == "end":
					var result := str(dialogue_node_value.get("result", ""))
					if not results.has(result):
						results.append(result)
		return results
	return ["next"]


func _node_summary(node: Dictionary) -> String:
	var properties: Dictionary = node.get("properties", {})
	for key in ["actor_id", "dialogue_id", "quest_id", "message", "variable", "item"]:
		if properties.has(key):
			var value := str(properties[key])
			return value.left(26) + ("…" if value.length() > 26 else "")
	return " "


func _output_color(output: String) -> Color:
	if output == "true" or output == "accept":
		return Color(0.35, 0.85, 0.48)
	if output == "false" or output == "refuse":
		return Color(0.9, 0.38, 0.38)
	return Color(0.65, 0.72, 0.9)


func _refresh_inspector() -> void:
	for child in inspector.get_children():
		child.queue_free()
	var node := _find_node(selected_node_id)
	if node.is_empty():
		_add_label(inspector, "Select a Story node.")
		return
	var heading := Label.new()
	heading.text = _type_label(str(node.get("type", ""))).to_upper()
	heading.add_theme_font_size_override("font_size", 22)
	inspector.add_child(heading)
	_add_line_field("Node ID", str(node.get("id", "")), _set_node_id)
	var node_type := str(node.get("type", ""))
	var properties: Dictionary = node.get("properties", {})
	for field in RpgProjectStore.STORY_NODE_PROPERTIES[node_type]:
		if field == "actor_id":
			_add_resource_picker("Actor", store.actors.keys(), str(properties.get(field, "")), field)
		elif field == "dialogue_id":
			_add_resource_picker("Dialogue", store.dialogues.keys(), str(properties.get(field, "")), field)
		elif field == "quest_id":
			_add_resource_picker("Quest", store.quests.keys(), str(properties.get(field, "")), field)
		elif field == "state":
			_add_option_field("Quest State", ["NOT_STARTED", "ACTIVE", "COMPLETED", "FAILED"], str(properties.get(field, "")), field)
		elif field == "operator":
			_add_option_field(
				"Operator",
				["equals", "not_equals", "greater", "greater_or_equal", "less", "less_or_equal"],
				str(properties.get(field, "")),
				field
			)
		elif field in ["message", "value"]:
			_add_text_field(field.capitalize(), str(properties.get(field, "")), field)
		else:
			_add_line_field(field.capitalize(), str(properties.get(field, "")), _set_property.bind(field))


func _refresh_problems() -> void:
	problems_list.clear()
	for problem in store.validate_story(story):
		problems_list.add_item(problem)


func _add_line_field(label_text: String, value: String, callback: Callable) -> void:
	_add_label(inspector, label_text)
	var edit := LineEdit.new()
	edit.text = value
	edit.text_changed.connect(callback)
	inspector.add_child(edit)


func _add_text_field(label_text: String, value: String, field: String) -> void:
	_add_label(inspector, label_text)
	var edit := TextEdit.new()
	edit.text = value
	edit.custom_minimum_size.y = 100
	edit.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	edit.text_changed.connect(func(): _set_property(edit.text, field))
	inspector.add_child(edit)


func _add_resource_picker(label_text: String, values: Array, selected: String, field: String) -> void:
	var sorted_values := values.duplicate()
	sorted_values.sort()
	_add_option_field(label_text, sorted_values, selected, field)


func _add_option_field(label_text: String, values: Array, selected: String, field: String) -> void:
	_add_label(inspector, label_text)
	var picker := OptionButton.new()
	for value in values:
		picker.add_item(str(value))
		if str(value) == selected:
			picker.select(picker.item_count - 1)
	picker.item_selected.connect(func(index: int): _set_property(picker.get_item_text(index), field))
	inspector.add_child(picker)


func _on_header_changed(_value: String) -> void:
	if updating_ui:
		return
	_record_undo()
	story["id"] = id_edit.text.strip_edges()
	story["title"] = title_edit.text.strip_edges()
	_mark_dirty()


func _on_entry_selected(index: int) -> void:
	if updating_ui or index < 0:
		return
	_record_undo()
	story["entry"] = entry_picker.get_item_text(index)
	_mark_dirty()


func _set_node_id(value: String) -> void:
	if updating_ui:
		return
	var node := _find_node(selected_node_id)
	if node.is_empty():
		return
	_record_undo()
	var previous := selected_node_id
	node["id"] = value
	for connection_value in story.get("connections", []):
		if connection_value.get("from") == previous:
			connection_value["from"] = value
		if connection_value.get("to") == previous:
			connection_value["to"] = value
	if story.get("entry") == previous:
		story["entry"] = value
	selected_node_id = value
	_mark_dirty()
	_refresh_all()


func _set_property(value: String, field: String) -> void:
	if updating_ui:
		return
	var node := _find_node(selected_node_id)
	if node.is_empty():
		return
	_record_undo()
	if field in ["dimension", "metadata", "amount"] and value.is_valid_int():
		node["properties"][field] = int(value)
	elif field in ["x", "y", "z", "radius"] and value.is_valid_float():
		node["properties"][field] = float(value)
	else:
		node["properties"][field] = value
	_mark_dirty()
	if field in ["dialogue_id", "actor_id", "quest_id", "message", "variable", "item"]:
		_refresh_graph()
	_refresh_problems()


func _on_node_moved(node_id: String, visual: GraphNode) -> void:
	if updating_ui:
		return
	var node := _find_node(node_id)
	if node.is_empty():
		return
	node["position"] = {"x": visual.position_offset.x, "y": visual.position_offset.y}
	_mark_dirty()


func _on_graph_node_selected(node: Node) -> void:
	selected_node_id = str(node.name)
	updating_ui = true
	_refresh_inspector()
	updating_ui = false


func _on_connection_request(from_node: StringName, from_port: int, to_node: StringName, _to_port: int) -> void:
	var from_id := str(from_node)
	var outputs: Array = output_ports.get(from_id, [])
	if from_port < 0 or from_port >= outputs.size() or from_id == str(to_node):
		return
	_record_undo()
	var output := str(outputs[from_port])
	var connections: Array = story.get("connections", [])
	for index in range(connections.size() - 1, -1, -1):
		if connections[index].get("from") == from_id and connections[index].get("output") == output:
			connections.remove_at(index)
	connections.append({"from": from_id, "output": output, "to": str(to_node)})
	_mark_dirty()
	_refresh_all()


func _on_disconnection_request(from_node: StringName, from_port: int, to_node: StringName, _to_port: int) -> void:
	var outputs: Array = output_ports.get(str(from_node), [])
	if from_port < 0 or from_port >= outputs.size():
		return
	_record_undo()
	var output := str(outputs[from_port])
	var connections: Array = story.get("connections", [])
	for index in range(connections.size() - 1, -1, -1):
		var connection: Dictionary = connections[index]
		if connection.get("from") == str(from_node) and connection.get("output") == output and connection.get("to") == str(to_node):
			connections.remove_at(index)
	_mark_dirty()
	_refresh_all()


func _filter_add_types(query: String) -> void:
	if add_type_picker == null:
		return
	add_type_picker.clear()
	var needle := query.to_lower()
	for node_type in NODE_TYPES:
		if needle.is_empty() or node_type.contains(needle) or _type_label(node_type).to_lower().contains(needle):
			add_type_picker.add_item(_type_label(node_type))
			add_type_picker.set_item_metadata(add_type_picker.item_count - 1, node_type)


func _add_selected_type() -> void:
	if add_type_picker.selected < 0:
		return
	_add_node(str(add_type_picker.get_item_metadata(add_type_picker.selected)))


func _add_node(node_type: String) -> void:
	_record_undo()
	var node_id := _unique_node_id(node_type)
	var center := graph.scroll_offset + Vector2(graph.size.x * 0.4, graph.size.y * 0.35)
	var node := {
		"id": node_id,
		"type": node_type,
		"position": {"x": center.x, "y": center.y},
		"properties": _default_properties(node_type),
	}
	story["nodes"].append(node)
	if str(story.get("entry", "")).is_empty():
		story["entry"] = node_id
	selected_node_id = node_id
	_mark_dirty()
	_refresh_all()


func _default_properties(node_type: String) -> Dictionary:
	var actor_ids := store.actors.keys()
	actor_ids.sort()
	var dialogue_ids := store.dialogues.keys()
	dialogue_ids.sort()
	var quest_ids := store.quests.keys()
	quest_ids.sort()
	match node_type:
		"interact_actor":
			return {"actor_id": str(actor_ids[0]) if not actor_ids.is_empty() else ""}
		"enter_region":
			return {"dimension": 0, "x": 0.0, "y": 64.0, "z": 0.0, "radius": 4.0}
		"quest_completed", "start_quest", "complete_quest":
			return {"quest_id": str(quest_ids[0]) if not quest_ids.is_empty() else ""}
		"play_dialogue":
			return {"dialogue_id": str(dialogue_ids[0]) if not dialogue_ids.is_empty() else ""}
		"branch", "variable_compare":
			return {"variable": "variable", "operator": "equals", "value": "true"}
		"quest_state":
			return {"quest_id": str(quest_ids[0]) if not quest_ids.is_empty() else "", "state": "NOT_STARTED"}
		"has_item":
			return {"item": "minecraft:stone", "metadata": -1, "amount": 1}
		"give_item":
			return {"item": "minecraft:emerald", "metadata": 0, "amount": 1}
		"give_xp":
			return {"amount": 10}
		"send_message":
			return {"message": "Story message"}
		"set_variable":
			return {"variable": "variable", "value": "true"}
	return {}


func _duplicate_selected() -> void:
	var node := _find_node(selected_node_id)
	if node.is_empty():
		return
	_record_undo()
	var copy := node.duplicate(true)
	copy["id"] = _unique_node_id(str(node.get("id", "node")) + "_copy")
	copy["position"]["x"] = float(copy["position"].get("x", 0)) + 40.0
	copy["position"]["y"] = float(copy["position"].get("y", 0)) + 40.0
	story["nodes"].append(copy)
	selected_node_id = copy["id"]
	_mark_dirty()
	_refresh_all()


func _copy_selected() -> void:
	var node := _find_node(selected_node_id)
	if not node.is_empty():
		copied_node = node.duplicate(true)
		status_label.text = "Copied " + selected_node_id


func _paste_node() -> void:
	if copied_node.is_empty():
		return
	_record_undo()
	var copy := copied_node.duplicate(true)
	copy["id"] = _unique_node_id(str(copy.get("id", "node")) + "_copy")
	copy["position"]["x"] = float(copy["position"].get("x", 0)) + 60.0
	copy["position"]["y"] = float(copy["position"].get("y", 0)) + 60.0
	story["nodes"].append(copy)
	selected_node_id = copy["id"]
	_mark_dirty()
	_refresh_all()


func _delete_selected() -> void:
	if selected_node_id.is_empty() or story.get("nodes", []).size() <= 1:
		return
	_record_undo()
	var nodes: Array = story.get("nodes", [])
	for index in range(nodes.size() - 1, -1, -1):
		if nodes[index].get("id") == selected_node_id:
			nodes.remove_at(index)
	var connections: Array = story.get("connections", [])
	for index in range(connections.size() - 1, -1, -1):
		if connections[index].get("from") == selected_node_id or connections[index].get("to") == selected_node_id:
			connections.remove_at(index)
	if story.get("entry") == selected_node_id:
		story["entry"] = str(nodes[0].get("id", ""))
	selected_node_id = str(nodes[0].get("id", ""))
	_mark_dirty()
	_refresh_all()


func _save() -> void:
	story["id"] = id_edit.text.strip_edges()
	story["title"] = title_edit.text.strip_edges()
	if entry_picker.selected >= 0:
		story["entry"] = entry_picker.get_item_text(entry_picker.selected)
	if story["id"] != original_id and store.stories.has(story["id"]):
		status_label.text = "Duplicate Story ID"
		return
	if store.save_story(story, original_id):
		original_id = story["id"]
		dirty = false
		status_label.text = "已保存"
		story_saved.emit(original_id)
	else:
		status_label.text = "Validation failed — see Problems"
	_refresh_problems()


func _delete_story() -> void:
	if original_id.is_empty():
		queue_free()
		return
	if store.delete_story(original_id):
		story_deleted.emit(original_id)
		queue_free()


func _on_problem_activated(index: int) -> void:
	var text := problems_list.get_item_text(index)
	var marker := "/node:"
	var marker_index := text.find(marker)
	if marker_index < 0:
		return
	var end_index := text.find("]", marker_index)
	if end_index < 0:
		return
	var node_id := text.substr(marker_index + marker.length(), end_index - marker_index - marker.length())
	_locate_node(node_id)


func _search_nodes(query: String) -> void:
	var needle := query.strip_edges().to_lower()
	if needle.is_empty():
		return
	var nodes: Array = story.get("nodes", [])
	if nodes.is_empty():
		return
	var start_index := 0
	for index in nodes.size():
		if str(nodes[index].get("id", "")) == selected_node_id:
			start_index = (index + 1) % nodes.size()
			break
	for offset in nodes.size():
		var node: Dictionary = nodes[(start_index + offset) % nodes.size()]
		if JSON.stringify(node).to_lower().contains(needle):
			_locate_node(str(node.get("id", "")))
			status_label.text = "Found node: " + selected_node_id
			return
	status_label.text = "No Story node matches: " + query


func _locate_node(node_id: String) -> void:
	if graph.has_node(NodePath(node_id)):
		selected_node_id = node_id
		var visual := graph.get_node(NodePath(node_id)) as GraphNode
		visual.selected = true
		graph.scroll_offset = visual.position_offset - graph.size * 0.4
		_refresh_inspector()


func _record_undo() -> void:
	if updating_ui:
		return
	undo_stack.append(story.duplicate(true))
	if undo_stack.size() > 100:
		undo_stack.pop_front()
	redo_stack.clear()


func _undo() -> void:
	if undo_stack.is_empty():
		return
	redo_stack.append(story.duplicate(true))
	story = undo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _redo() -> void:
	if redo_stack.is_empty():
		return
	undo_stack.append(story.duplicate(true))
	story = redo_stack.pop_back()
	_mark_dirty()
	_refresh_all()


func _find_node(node_id: String) -> Dictionary:
	for node_value in story.get("nodes", []):
		if node_value.get("id") == node_id:
			return node_value
	return {}


func _unique_node_id(base: String) -> String:
	var normalized := base.to_lower().replace(" ", "_")
	var result := normalized
	var suffix := 2
	while not _find_node(result).is_empty():
		result = "%s_%d" % [normalized, suffix]
		suffix += 1
	return result


func _type_label(node_type: String) -> String:
	var parts := node_type.split("_")
	var result := ""
	for part in parts:
		result += part.capitalize()
	return result


func _mark_dirty() -> void:
	dirty = true
	_update_status()


func _autosave() -> void:
	if dirty and not original_id.is_empty():
		_save()


func _update_status() -> void:
	save_button.text = "保存剧情 *  Ctrl+S" if dirty else "保存剧情  Ctrl+S"


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
	elif event.keycode == KEY_C:
		_copy_selected()
	elif event.keycode == KEY_V:
		_paste_node()
	elif event.keycode == KEY_D:
		_duplicate_selected()
	else:
		return
	get_viewport().set_input_as_handled()
