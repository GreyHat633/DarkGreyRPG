class_name LiveBridgeClient
extends Node

signal connection_changed(connected: bool)
signal message_received(message: Dictionary)

const HOST := "127.0.0.1"
const DEFAULT_PORT := 32145
const MAX_BUFFER_BYTES := 1024 * 1024

var peer := StreamPeerTCP.new()
var port := DEFAULT_PORT
var connected := false
var reconnect_delay := 0.0
var receive_buffer := ""


func _ready() -> void:
	set_process(true)
	_connect_now()


func _process(delta: float) -> void:
	peer.poll()
	var status := peer.get_status()
	if status == StreamPeerTCP.STATUS_CONNECTED:
		if not connected:
			connected = true
			connection_changed.emit(true)
		_read_available()
		return
	if connected:
		connected = false
		receive_buffer = ""
		connection_changed.emit(false)
	reconnect_delay -= delta
	if reconnect_delay <= 0.0 and status in [StreamPeerTCP.STATUS_NONE, StreamPeerTCP.STATUS_ERROR]:
		reconnect_delay = 2.0
		_connect_now()


func send(message: Dictionary) -> bool:
	if not connected:
		return false
	var payload := (JSON.stringify(message, "", false) + "\n").to_utf8_buffer()
	return peer.put_data(payload) == OK


func disconnect_bridge() -> void:
	peer.disconnect_from_host()
	connected = false
	connection_changed.emit(false)


func _connect_now() -> void:
	peer.disconnect_from_host()
	var error := peer.connect_to_host(HOST, port)
	if error != OK:
		reconnect_delay = 2.0


func _read_available() -> void:
	var available := peer.get_available_bytes()
	if available <= 0:
		return
	var packet := peer.get_data(available)
	if packet[0] != OK:
		return
	receive_buffer += (packet[1] as PackedByteArray).get_string_from_utf8()
	if receive_buffer.to_utf8_buffer().size() > MAX_BUFFER_BYTES:
		receive_buffer = ""
		peer.disconnect_from_host()
		return
	var newline := receive_buffer.find("\n")
	while newline >= 0:
		var line := receive_buffer.substr(0, newline).strip_edges()
		receive_buffer = receive_buffer.substr(newline + 1)
		if not line.is_empty():
			var parsed: Variant = JSON.parse_string(line)
			if typeof(parsed) == TYPE_DICTIONARY:
				message_received.emit(parsed)
		newline = receive_buffer.find("\n")
