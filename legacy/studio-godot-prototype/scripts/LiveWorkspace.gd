class_name LiveWorkspace
extends Node

const LiveBridgeClientScript := preload("res://scripts/LiveBridgeClient.gd")

signal story_debug_updated(story_id: String, debug: Dictionary)
signal output_message(message: String)
signal pick_received(kind: String, value: Dictionary)

var store: RpgProjectStore
var status_label: Label
var client: Node
var debugger_tree: Tree
var minecraft_tree: Tree
var player_picker: OptionButton
var story_picker: OptionButton
var node_edit: LineEdit
var pick_result_label: Label
var latest_state: Dictionary = {}
var request_number := 0
var selected_actor_id := ""


func setup(project_store: RpgProjectStore, connection_status: Label) -> void:
	store = project_store
	status_label = connection_status
	client = LiveBridgeClientScript.new()
	client.connection_changed.connect(_on_connection_changed)
	client.message_received.connect(_on_message)
	add_child(client)
	var poll := Timer.new()
	poll.wait_time = 0.5
	poll.autostart = true
	poll.timeout.connect(request_state)
	add_child(poll)


func build_debugger_panel() -> Control:
	var panel := VBoxContainer.new()
	panel.name = "调试器 (Debugger)"
	var toolbar := HBoxContainer.new()
	panel.add_child(toolbar)
	_add_label(toolbar, "玩家 (Player)")
	player_picker = OptionButton.new()
	player_picker.custom_minimum_size.x = 170
	player_picker.item_selected.connect(func(_index: int): _render_debugger())
	toolbar.add_child(player_picker)
	_add_label(toolbar, "剧情 (Story)")
	story_picker = OptionButton.new()
	story_picker.custom_minimum_size.x = 190
	story_picker.item_selected.connect(_on_story_selected)
	toolbar.add_child(story_picker)
	node_edit = LineEdit.new()
	node_edit.placeholder_text = "起始节点 ID"
	node_edit.custom_minimum_size.x = 170
	toolbar.add_child(node_edit)
	var test_button := Button.new()
	test_button.text = "▶ 测试"
	test_button.pressed.connect(_start_test)
	toolbar.add_child(test_button)
	var stop_button := Button.new()
	stop_button.text = "■ 停止并恢复"
	stop_button.pressed.connect(_stop_test)
	toolbar.add_child(stop_button)
	var refresh_button := Button.new()
	refresh_button.text = "刷新"
	refresh_button.pressed.connect(request_state)
	toolbar.add_child(refresh_button)
	debugger_tree = Tree.new()
	debugger_tree.hide_root = true
	debugger_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	debugger_tree.columns = 2
	debugger_tree.set_column_title(0, "运行时")
	debugger_tree.set_column_title(1, "状态 / 为何未触发")
	debugger_tree.column_titles_visible = true
	panel.add_child(debugger_tree)
	_refresh_story_picker()
	return panel


func build_minecraft_panel() -> Control:
	var panel := VBoxContainer.new()
	panel.name = "Minecraft (游戏内)"
	var toolbar := HBoxContainer.new()
	panel.add_child(toolbar)
	for spec in [
		["拾取角色", "actor"],
		["拾取位置", "position"],
		["拾取区域", "region"],
		["拾取物品", "item"],
		["拾取实体类型", "entity_type"],
	]:
		var button := Button.new()
		button.text = spec[0]
		button.pressed.connect(_begin_pick.bind(spec[1]))
		toolbar.add_child(button)
	var locate := Button.new()
	locate.text = "定位选中的角色"
	locate.pressed.connect(_locate_actor)
	toolbar.add_child(locate)
	pick_result_label = Label.new()
	pick_result_label.text = "拾取结果将显示于此。"
	pick_result_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	panel.add_child(pick_result_label)
	minecraft_tree = Tree.new()
	minecraft_tree.hide_root = true
	minecraft_tree.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.add_child(minecraft_tree)
	return panel


func notify_resource_saved(resource_type: String, resource_id: String) -> void:
	if not client.connected:
		output_message.emit("离线保存了 %s '%s'；请在游戏内使用 /dgrpg reload。" % [resource_type, resource_id])
		return
	_send(
		{
			"type": "project.reload",
			"resource_type": resource_type,
			"resource_id": resource_id,
		}
	)


func set_selected_actor(actor_id: String) -> void:
	selected_actor_id = actor_id


func request_state() -> void:
	if client != null and client.connected:
		_send({"type": "state.request"})


func _on_connection_changed(is_connected: bool) -> void:
	status_label.text = "  ● Minecraft 已连接  " if is_connected else "  ● Offline  "
	status_label.modulate = Color(0.35, 0.9, 0.5) if is_connected else Color(0.72, 0.75, 0.82)
	status_label.tooltip_text = (
		"Live Bridge connected to 127.0.0.1:%d" % client.port
		if is_connected
		else "Studio remains fully editable offline; reconnecting to localhost."
	)
	output_message.emit("Minecraft 实时桥接已连接" if is_connected else "Minecraft 实时桥接已离线")
	if is_connected:
		request_state()


func _on_message(message: Dictionary) -> void:
	var message_type := str(message.get("type", ""))
	if message_type == "bridge.hello":
		output_message.emit(
			"实时桥接协议版本 %s, DarkGrey RPG 版本 %s"
			% [message.get("protocol", "?"), message.get("mod_version", "?")]
		)
	elif message_type == "state.snapshot":
		latest_state = message
		_refresh_player_picker()
		_render_debugger()
		_render_minecraft()
	elif message_type == "pick.result":
		var value: Dictionary = message.get("value", {})
		pick_result_label.text = JSON.stringify(value)
		output_message.emit("Minecraft 拾取结果： " + pick_result_label.text)
		pick_received.emit(str(message.get("kind", "")), value)
	elif message_type == "pick.progress":
		pick_result_label.text = str(message.get("message", "正在拾取中"))
	elif message_type == "response":
		var color := "green" if message.get("ok", false) else "red"
		output_message.emit("[color=%s]Live:[/color] %s" % [color, message.get("message", "")])


func _refresh_player_picker() -> void:
	if player_picker == null:
		return
	var selected_uuid := _selected_player_uuid()
	player_picker.clear()
	for player_value in latest_state.get("players", []):
		var player: Dictionary = player_value
		player_picker.add_item(str(player.get("name", "玩家 (Player)")))
		player_picker.set_item_metadata(player_picker.item_count - 1, str(player.get("uuid", "")))
		if str(player.get("uuid", "")) == selected_uuid:
			player_picker.select(player_picker.item_count - 1)


func _refresh_story_picker() -> void:
	if story_picker == null:
		return
	story_picker.clear()
	var ids := store.stories.keys()
	ids.sort()
	for story_id in ids:
		story_picker.add_item(str(story_id))
	if story_picker.item_count > 0:
		_on_story_selected(0)


func _on_story_selected(index: int) -> void:
	if index < 0 or index >= story_picker.item_count:
		return
	var story_id := story_picker.get_item_text(index)
	if store.stories.has(story_id):
		node_edit.text = str(store.stories[story_id].get("entry", ""))


func _render_debugger() -> void:
	if debugger_tree == null:
		return
	debugger_tree.clear()
	var root := debugger_tree.create_item()
	var player := _selected_player()
	if player.is_empty():
		var empty := debugger_tree.create_item(root)
		empty.set_text(0, "暂无在线玩家")
		return
	var heading := debugger_tree.create_item(root)
	heading.set_text(0, "%s (%s)" % [player.get("name", ""), player.get("uuid", "")])
	heading.set_text(1, "调试中 (PLAY TEST)" if player.get("testing", false) else "实时 (Live)")
	var quests_root := debugger_tree.create_item(heading)
	quests_root.set_text(0, "任务列表 (Quests)")
	for quest_value in player.get("quests", []):
		var quest: Dictionary = quest_value
		var item := debugger_tree.create_item(quests_root)
		item.set_text(0, str(quest.get("id", "")))
		item.set_text(1, str(quest.get("status", "")))
		for objective_value in quest.get("objectives", []):
			var objective: Dictionary = objective_value
			var progress := debugger_tree.create_item(item)
			progress.set_text(0, str(objective.get("description", objective.get("id", ""))))
			progress.set_text(1, "%s/%s" % [objective.get("current", 0), objective.get("required", 0)])
	var stories_root := debugger_tree.create_item(heading)
	stories_root.set_text(0, "剧情实例 (Story Instances)")
	for story_value in player.get("stories", []):
		var story: Dictionary = story_value
		var item := debugger_tree.create_item(stories_root)
		item.set_text(0, str(story.get("id", "")))
		item.set_text(
			1,
			"%s @ %s" % [story.get("state", ""), story.get("current_node", "")]
		)
		var why := debugger_tree.create_item(item)
		why.set_text(0, "为何未触发 / 最后条件")
		why.set_text(1, str(story.get("explanation", "")))
		for node_id in story.get("conditions", {}):
			var condition := debugger_tree.create_item(why)
			condition.set_text(0, str(node_id))
			condition.set_text(1, str(story["conditions"][node_id]))
		story_debug_updated.emit(str(story.get("id", "")), story)
	var variables_root := debugger_tree.create_item(heading)
	variables_root.set_text(0, "变量 (Variables)")
	for story_id in player.get("variables", {}):
		var story_vars := debugger_tree.create_item(variables_root)
		story_vars.set_text(0, str(story_id))
		for variable in player["variables"][story_id]:
			var value := debugger_tree.create_item(story_vars)
			value.set_text(0, str(variable))
			value.set_text(1, str(player["variables"][story_id][variable]))


func _render_minecraft() -> void:
	if minecraft_tree == null:
		return
	minecraft_tree.clear()
	var root := minecraft_tree.create_item()
	var actors := minecraft_tree.create_item(root)
	actors.set_text(0, "实时活跃角色 (%d)" % latest_state.get("actors", []).size())
	for actor_value in latest_state.get("actors", []):
		var actor: Dictionary = actor_value
		var item := minecraft_tree.create_item(actors)
		item.set_text(
			0,
			"%s — %s [dim %s, %.1f %.1f %.1f]"
			% [
				actor.get("actor_id", ""),
				actor.get("name", ""),
				actor.get("dimension", 0),
				actor.get("x", 0.0),
				actor.get("y", 0.0),
				actor.get("z", 0.0),
			]
		)
	var players := minecraft_tree.create_item(root)
	players.set_text(0, "玩家 (%d)" % latest_state.get("players", []).size())
	for player_value in latest_state.get("players", []):
		var player: Dictionary = player_value
		var item := minecraft_tree.create_item(players)
		item.set_text(0, "%s — %s" % [player.get("name", ""), player.get("uuid", "")])


func _start_test() -> void:
	if story_picker.selected < 0:
		return
	_send(
		{
			"type": "test.start",
			"player": _selected_player_uuid(),
			"story_id": story_picker.get_item_text(story_picker.selected),
			"node_id": node_edit.text.strip_edges(),
		}
	)


func _stop_test() -> void:
	_send({"type": "test.stop", "player": _selected_player_uuid()})


func _begin_pick(kind: String) -> void:
	_send({"type": "pick.begin", "player": _selected_player_uuid(), "kind": kind})


func _locate_actor() -> void:
	var actor_id := selected_actor_id
	if actor_id.is_empty() and not store.actors.is_empty():
		var ids := store.actors.keys()
		ids.sort()
		actor_id = str(ids[0])
	_send({"type": "locate.actor", "player": _selected_player_uuid(), "actor_id": actor_id})


func _selected_player() -> Dictionary:
	var uuid := _selected_player_uuid()
	for player_value in latest_state.get("players", []):
		if str(player_value.get("uuid", "")) == uuid:
			return player_value
	return {}


func _selected_player_uuid() -> String:
	if player_picker == null or player_picker.selected < 0:
		return ""
	return str(player_picker.get_item_metadata(player_picker.selected))


func _send(message: Dictionary) -> void:
	request_number += 1
	message["request_id"] = "studio-%d" % request_number
	if not client.send(message):
		output_message.emit("[color=yellow]Live command skipped: Minecraft is offline.[/color]")


func _add_label(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	parent.add_child(label)
