class_name ReorderableNodeList
extends ItemList

signal item_reordered(from_index: int, to_index: int)


func _get_drag_data(position: Vector2) -> Variant:
	var index := get_item_at_position(position, true)
	if index < 0:
		return null
	var preview := Label.new()
	preview.text = get_item_text(index)
	set_drag_preview(preview)
	return {"node_index": index}


func _can_drop_data(position: Vector2, data: Variant) -> bool:
	return typeof(data) == TYPE_DICTIONARY and data.has("node_index") and get_item_at_position(position, true) >= 0


func _drop_data(position: Vector2, data: Variant) -> void:
	var target := get_item_at_position(position, true)
	var source := int(data["node_index"])
	if source != target:
		item_reordered.emit(source, target)
