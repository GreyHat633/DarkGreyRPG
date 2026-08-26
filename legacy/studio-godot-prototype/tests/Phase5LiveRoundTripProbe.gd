extends SceneTree

const LiveBridgeClientScript := preload("res://scripts/LiveBridgeClient.gd")

var connected := false
var hello_received := false
var snapshot_received := false
var counts_valid := false
var client: Node


func _initialize() -> void:
	client = LiveBridgeClientScript.new()
	client.connection_changed.connect(_on_connection_changed)
	client.message_received.connect(_on_message)
	root.add_child(client)
	var deadline := Time.get_ticks_msec() + 8000
	while Time.get_ticks_msec() < deadline and not (connected and hello_received and snapshot_received):
		await process_frame
	if not connected:
		_fail("Studio did not display Minecraft Connected state")
		return
	if not hello_received:
		_fail("Studio did not receive the Live Bridge hello")
		return
	if not snapshot_received or not counts_valid:
		_fail("Studio did not receive the expected runtime snapshot")
		return
	client.queue_free()
	await process_frame
	print("STUDIO_MINECRAFT_CONNECTED_PROBE=PASS")
	print("LIVE_JSON_LINES_ROUND_TRIP_PROBE=PASS")
	print("LIVE_CONTENT_PACK_COUNTS_PROBE=PASS")
	quit(0)


func _on_connection_changed(value: bool) -> void:
	connected = value
	if value:
		client.send({"type": "state.request", "request_id": "godot-live-probe"})


func _on_message(message: Dictionary) -> void:
	if message.get("type") == "bridge.hello":
		hello_received = int(message.get("protocol", 0)) == 1
	elif message.get("type") == "state.snapshot":
		snapshot_received = true
		var counts: Dictionary = message.get("counts", {})
		counts_valid = (
			int(counts.get("actors", -1)) == 1
			and int(counts.get("dialogues", -1)) == 2
			and int(counts.get("quests", -1)) == 1
			and int(counts.get("stories", -1)) == 1
		)


func _fail(message: String) -> void:
	push_error(message)
	if client != null:
		client.queue_free()
	quit(1)
