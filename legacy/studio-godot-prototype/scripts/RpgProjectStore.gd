class_name RpgProjectStore
extends RefCounted

const ACTOR_FIELDS := {
	"schema_version": true,
	"id": true,
	"display_name": true,
	"notes": true,
	"tags": true,
}
const DIALOGUE_FIELDS := {
	"schema_version": true,
	"id": true,
	"title": true,
	"speakers": true,
	"entry": true,
	"nodes": true,
	"metadata": true,
}
const NODE_FIELDS := {
	"line": ["id", "type", "speaker", "text", "next"],
	"choice": ["id", "type", "prompt", "choices"],
	"jump": ["id", "type", "target"],
	"end": ["id", "type", "result"],
}
const QUEST_FIELDS := {
	"schema_version": true,
	"id": true,
	"title": true,
	"description": true,
	"objectives": true,
	"objective_groups": true,
	"metadata": true,
}
const OBJECTIVE_FIELDS := {
	"kill_entity": ["id", "type", "description", "entity", "required"],
	"collect_item": ["id", "type", "description", "item", "metadata", "required"],
	"reach_location": ["id", "type", "description", "dimension", "x", "y", "z", "radius"],
	"interact_actor": ["id", "type", "description", "actor_id", "required"],
}
const STORY_FIELDS := {
	"schema_version": true,
	"id": true,
	"title": true,
	"entry": true,
	"nodes": true,
	"connections": true,
	"metadata": true,
}
const STORY_NODE_PROPERTIES := {
	"interact_actor": ["actor_id"],
	"enter_region": ["dimension", "x", "y", "z", "radius"],
	"quest_completed": ["quest_id"],
	"play_dialogue": ["dialogue_id"],
	"start_quest": ["quest_id"],
	"complete_quest": ["quest_id"],
	"branch": ["variable", "operator", "value"],
	"sequence": [],
	"quest_state": ["quest_id", "state"],
	"has_item": ["item", "metadata", "amount"],
	"variable_compare": ["variable", "operator", "value"],
	"give_item": ["item", "metadata", "amount"],
	"give_xp": ["amount"],
	"send_message": ["message"],
	"set_variable": ["variable", "value"],
	"end": [],
}
const ID_PATTERN := "^[a-z0-9][a-z0-9_.-]*$"

var project_path := ""
var project: Dictionary = {}
var actors: Dictionary = {}
var dialogues: Dictionary = {}
var quests: Dictionary = {}
var stories: Dictionary = {}
var problems: Array[String] = []


func load_project(path: String) -> bool:
	project_path = path.replace("\\", "/").trim_suffix("/")
	project = {}
	actors = {}
	dialogues = {}
	quests = {}
	stories = {}
	problems = []

	var project_file := project_path.path_join("project.json")
	var loaded_project := _read_json_object(project_file)
	if loaded_project.is_empty():
		return false
	if not _validate_project(loaded_project, project_file):
		return false
	project = loaded_project

	var actors_path := project_path.path_join("actors")
	if not DirAccess.dir_exists_absolute(actors_path):
		problems.append("Missing actors directory: %s" % actors_path)
		return false

	var directory := DirAccess.open(actors_path)
	if directory == null:
		problems.append("Cannot open actors directory: %s" % actors_path)
		return false

	var filenames: Array[String] = []
	directory.list_dir_begin()
	var filename := directory.get_next()
	while not filename.is_empty():
		if not directory.current_is_dir() and filename.to_lower().ends_with(".json"):
			filenames.append(filename)
		filename = directory.get_next()
	directory.list_dir_end()
	filenames.sort()

	for actor_filename in filenames:
		var actor_path := actors_path.path_join(actor_filename)
		var actor := _read_json_object(actor_path)
		if actor.is_empty() or not _validate_actor(actor, actor_path):
			continue
		var actor_id: String = actor["id"]
		if actors.has(actor_id):
			problems.append("Duplicate Actor ID: %s" % actor_id)
			continue
		actors[actor_id] = actor

	var dialogues_path := project_path.path_join("dialogues")
	if not DirAccess.dir_exists_absolute(dialogues_path):
		problems.append("Missing dialogues directory: %s" % dialogues_path)
		return false
	var dialogue_directory := DirAccess.open(dialogues_path)
	if dialogue_directory == null:
		problems.append("Cannot open dialogues directory: %s" % dialogues_path)
		return false
	var dialogue_filenames: Array[String] = []
	dialogue_directory.list_dir_begin()
	var dialogue_filename := dialogue_directory.get_next()
	while not dialogue_filename.is_empty():
		if not dialogue_directory.current_is_dir() and dialogue_filename.to_lower().ends_with(".json"):
			dialogue_filenames.append(dialogue_filename)
		dialogue_filename = dialogue_directory.get_next()
	dialogue_directory.list_dir_end()
	dialogue_filenames.sort()
	for current_filename in dialogue_filenames:
		var dialogue_path := dialogues_path.path_join(current_filename)
		var dialogue := _read_json_object(dialogue_path)
		if dialogue.is_empty() or not _validate_dialogue(dialogue, dialogue_path):
			continue
		var dialogue_id: String = dialogue["id"]
		if dialogues.has(dialogue_id):
			problems.append("Duplicate Dialogue ID: %s" % dialogue_id)
			continue
		dialogues[dialogue_id] = dialogue

	var quests_path := project_path.path_join("quests")
	if not DirAccess.dir_exists_absolute(quests_path):
		problems.append("Missing quests directory: %s" % quests_path)
		return false
	var quest_directory := DirAccess.open(quests_path)
	if quest_directory == null:
		problems.append("Cannot open quests directory: %s" % quests_path)
		return false
	var quest_filenames: Array[String] = []
	quest_directory.list_dir_begin()
	var quest_filename := quest_directory.get_next()
	while not quest_filename.is_empty():
		if not quest_directory.current_is_dir() and quest_filename.to_lower().ends_with(".json"):
			quest_filenames.append(quest_filename)
		quest_filename = quest_directory.get_next()
	quest_directory.list_dir_end()
	quest_filenames.sort()
	for current_filename in quest_filenames:
		var quest_path := quests_path.path_join(current_filename)
		var quest := _read_json_object(quest_path)
		if quest.is_empty() or not _validate_quest(quest, quest_path):
			continue
		var quest_id: String = quest["id"]
		if quests.has(quest_id):
			problems.append("Duplicate Quest ID: %s" % quest_id)
			continue
		quests[quest_id] = quest

	var stories_path := project_path.path_join("stories")
	if not DirAccess.dir_exists_absolute(stories_path):
		problems.append("Missing stories directory: %s" % stories_path)
		return false
	var story_directory := DirAccess.open(stories_path)
	if story_directory == null:
		problems.append("Cannot open stories directory: %s" % stories_path)
		return false
	var story_filenames: Array[String] = []
	story_directory.list_dir_begin()
	var story_filename := story_directory.get_next()
	while not story_filename.is_empty():
		if not story_directory.current_is_dir() and story_filename.to_lower().ends_with(".json"):
			story_filenames.append(story_filename)
		story_filename = story_directory.get_next()
	story_directory.list_dir_end()
	story_filenames.sort()
	for current_filename in story_filenames:
		var story_path := stories_path.path_join(current_filename)
		var story := _read_json_object(story_path)
		if story.is_empty() or not _validate_story(story, story_path):
			continue
		var story_id: String = story["id"]
		if stories.has(story_id):
			problems.append("Duplicate Story ID: %s" % story_id)
			continue
		stories[story_id] = story

	return problems.is_empty()


func validate_actor(actor: Dictionary, source := "Actor Inspector") -> Array[String]:
	var old_problems := problems
	problems = []
	_validate_actor(actor, source)
	var result := problems.duplicate()
	problems = old_problems
	return result


func validate_dialogue(dialogue: Dictionary, source := "Dialogue Editor") -> Array[String]:
	var old_problems := problems
	problems = []
	_validate_dialogue(dialogue, source)
	var result := problems.duplicate()
	problems = old_problems
	return result


func validate_quest(quest: Dictionary, source := "Quest Editor") -> Array[String]:
	var old_problems := problems
	problems = []
	_validate_quest(quest, source)
	var result := problems.duplicate()
	problems = old_problems
	return result


func validate_story(story: Dictionary, source := "Story Editor") -> Array[String]:
	var old_problems := problems
	problems = []
	_validate_story(story, source)
	var result := problems.duplicate()
	problems = old_problems
	return result


func save_actor(actor: Dictionary, previous_id: String) -> bool:
	var errors := validate_actor(actor)
	if not errors.is_empty():
		problems.append_array(errors)
		return false

	var actors_path := project_path.path_join("actors")
	var make_error := DirAccess.make_dir_recursive_absolute(actors_path)
	if make_error != OK and make_error != ERR_ALREADY_EXISTS:
		problems.append("Cannot create actors directory: %s" % actors_path)
		return false

	var actor_id: String = actor["id"]
	var target_path := actors_path.path_join(actor_id + ".json")
	var serialized := JSON.stringify(actor, "\t", false) + "\n"
	if not _atomic_write(target_path, serialized):
		return false

	if not previous_id.is_empty() and previous_id != actor_id:
		var previous_path := actors_path.path_join(previous_id + ".json")
		if FileAccess.file_exists(previous_path):
			var remove_error := DirAccess.remove_absolute(previous_path)
			if remove_error != OK:
				problems.append("Saved new Actor but could not remove old file: %s" % previous_path)
				return false
		actors.erase(previous_id)

	actors[actor_id] = actor.duplicate(true)
	return true


func delete_actor(actor_id: String) -> bool:
	var actor_path := project_path.path_join("actors").path_join(actor_id + ".json")
	if FileAccess.file_exists(actor_path):
		var error := DirAccess.remove_absolute(actor_path)
		if error != OK:
			problems.append("Could not delete Actor file: %s" % actor_path)
			return false
	actors.erase(actor_id)
	return true


func save_dialogue(dialogue: Dictionary, previous_id: String) -> bool:
	var errors := validate_dialogue(dialogue)
	if not errors.is_empty():
		problems.append_array(errors)
		return false
	var dialogues_path := project_path.path_join("dialogues")
	var make_error := DirAccess.make_dir_recursive_absolute(dialogues_path)
	if make_error not in [OK, ERR_ALREADY_EXISTS]:
		problems.append("Cannot create dialogues directory: %s" % dialogues_path)
		return false
	var dialogue_id: String = dialogue["id"]
	var target_path := dialogues_path.path_join(dialogue_id + ".json")
	if not _atomic_write(target_path, JSON.stringify(dialogue, "\t", false) + "\n"):
		return false
	if not previous_id.is_empty() and previous_id != dialogue_id:
		var previous_path := dialogues_path.path_join(previous_id + ".json")
		if FileAccess.file_exists(previous_path) and DirAccess.remove_absolute(previous_path) != OK:
			problems.append("Saved new Dialogue but could not remove old file: %s" % previous_path)
			return false
		dialogues.erase(previous_id)
	dialogues[dialogue_id] = dialogue.duplicate(true)
	return true


func delete_dialogue(dialogue_id: String) -> bool:
	var dialogue_path := project_path.path_join("dialogues").path_join(dialogue_id + ".json")
	if FileAccess.file_exists(dialogue_path) and DirAccess.remove_absolute(dialogue_path) != OK:
		problems.append("Could not delete Dialogue file: %s" % dialogue_path)
		return false
	dialogues.erase(dialogue_id)
	return true


func save_quest(quest: Dictionary, previous_id: String) -> bool:
	var errors := validate_quest(quest)
	if not errors.is_empty():
		problems.append_array(errors)
		return false
	var quests_path := project_path.path_join("quests")
	var make_error := DirAccess.make_dir_recursive_absolute(quests_path)
	if make_error not in [OK, ERR_ALREADY_EXISTS]:
		problems.append("Cannot create quests directory: %s" % quests_path)
		return false
	var quest_id: String = quest["id"]
	var target_path := quests_path.path_join(quest_id + ".json")
	if not _atomic_write(target_path, JSON.stringify(quest, "\t", false) + "\n"):
		return false
	if not previous_id.is_empty() and previous_id != quest_id:
		var previous_path := quests_path.path_join(previous_id + ".json")
		if FileAccess.file_exists(previous_path) and DirAccess.remove_absolute(previous_path) != OK:
			problems.append("Saved new Quest but could not remove old file: %s" % previous_path)
			return false
		quests.erase(previous_id)
	quests[quest_id] = quest.duplicate(true)
	return true


func delete_quest(quest_id: String) -> bool:
	var quest_path := project_path.path_join("quests").path_join(quest_id + ".json")
	if FileAccess.file_exists(quest_path) and DirAccess.remove_absolute(quest_path) != OK:
		problems.append("Could not delete Quest file: %s" % quest_path)
		return false
	quests.erase(quest_id)
	return true


func save_story(story: Dictionary, previous_id: String) -> bool:
	var errors := validate_story(story)
	if not errors.is_empty():
		problems.append_array(errors)
		return false
	var stories_path := project_path.path_join("stories")
	var make_error := DirAccess.make_dir_recursive_absolute(stories_path)
	if make_error not in [OK, ERR_ALREADY_EXISTS]:
		problems.append("Cannot create stories directory: %s" % stories_path)
		return false
	var story_id: String = story["id"]
	var target_path := stories_path.path_join(story_id + ".json")
	if not _atomic_write(target_path, JSON.stringify(story, "\t", false) + "\n"):
		return false
	if not previous_id.is_empty() and previous_id != story_id:
		var previous_path := stories_path.path_join(previous_id + ".json")
		if FileAccess.file_exists(previous_path) and DirAccess.remove_absolute(previous_path) != OK:
			problems.append("Saved new Story but could not remove old file: %s" % previous_path)
			return false
		stories.erase(previous_id)
	stories[story_id] = story.duplicate(true)
	return true


func delete_story(story_id: String) -> bool:
	var story_path := project_path.path_join("stories").path_join(story_id + ".json")
	if FileAccess.file_exists(story_path) and DirAccess.remove_absolute(story_path) != OK:
		problems.append("Could not delete Story file: %s" % story_path)
		return false
	stories.erase(story_id)
	return true


func build_content_pack(target_path: String) -> bool:
	var normalized := target_path.replace("\\", "/").trim_suffix("/")
	if normalized.is_empty() or normalized == project_path:
		problems.append("Content Pack target must differ from the source project.")
		return false
	for actor in actors.values():
		if not validate_actor(actor).is_empty():
			problems.append("Content Pack build stopped by Actor validation errors.")
			return false
	for dialogue in dialogues.values():
		if not validate_dialogue(dialogue).is_empty():
			problems.append("Content Pack build stopped by Dialogue validation errors.")
			return false
	for quest in quests.values():
		if not validate_quest(quest).is_empty():
			problems.append("Content Pack build stopped by Quest validation errors.")
			return false
	for story in stories.values():
		if not validate_story(story).is_empty():
			problems.append("Content Pack build stopped by Story validation errors.")
			return false

	var temporary := normalized + ".tmp"
	if DirAccess.dir_exists_absolute(temporary):
		_remove_tree(temporary)
	if DirAccess.make_dir_recursive_absolute(temporary) not in [OK, ERR_ALREADY_EXISTS]:
		problems.append("Cannot create Content Pack staging directory: %s" % temporary)
		return false
	for directory in ["actors", "dialogues", "quests", "stories", "resources"]:
		if DirAccess.make_dir_recursive_absolute(temporary.path_join(directory)) not in [OK, ERR_ALREADY_EXISTS]:
			problems.append("Cannot create Content Pack directory: %s" % directory)
			_remove_tree(temporary)
			return false
	if not _copy_file(project_path.path_join("project.json"), temporary.path_join("project.json")):
		_remove_tree(temporary)
		return false
	for directory in ["actors", "dialogues", "quests", "stories", "resources"]:
		var source := project_path.path_join(directory)
		if DirAccess.dir_exists_absolute(source) and not _copy_tree(source, temporary.path_join(directory)):
			_remove_tree(temporary)
			return false

	if DirAccess.dir_exists_absolute(normalized):
		var stamp := Time.get_datetime_string_from_system().replace(":", "-")
		var backup := normalized + ".backup-" + stamp
		if DirAccess.rename_absolute(normalized, backup) != OK:
			problems.append("Cannot back up existing Content Pack: %s" % normalized)
			_remove_tree(temporary)
			return false
	if DirAccess.rename_absolute(temporary, normalized) != OK:
		problems.append("Cannot publish Content Pack atomically: %s" % normalized)
		return false
	return true


func _validate_project(value: Dictionary, source: String) -> bool:
	var valid := true
	if value.get("schema_version") != 1:
		problems.append("%s: schema_version must be 1" % source)
		valid = false
	if not _valid_id(str(value.get("id", ""))):
		problems.append("%s: invalid project id" % source)
		valid = false
	if str(value.get("display_name", "")).strip_edges().is_empty():
		problems.append("%s: display_name is required" % source)
		valid = false
	return valid


func _validate_actor(value: Dictionary, source: String) -> bool:
	var valid := true
	for key in value:
		if not ACTOR_FIELDS.has(key):
			problems.append("%s: unsupported Actor field '%s'" % [source, key])
			valid = false

	if value.get("schema_version") != 1:
		problems.append("%s: schema_version must be 1" % source)
		valid = false

	var actor_id := str(value.get("id", ""))
	if not _valid_id(actor_id):
		problems.append("%s: invalid Actor ID '%s'" % [source, actor_id])
		valid = false
	elif source.ends_with(".json") and source.get_file() != actor_id + ".json":
		problems.append("%s: filename must match Actor ID" % source)
		valid = false

	if str(value.get("display_name", "")).strip_edges().is_empty():
		problems.append("%s: display_name is required" % source)
		valid = false

	if typeof(value.get("notes", "")) != TYPE_STRING:
		problems.append("%s: notes must be a string" % source)
		valid = false

	var tags_value: Variant = value.get("tags", [])
	if typeof(tags_value) != TYPE_ARRAY:
		problems.append("%s: tags must be an array" % source)
		valid = false
	else:
		for tag in tags_value:
			if typeof(tag) != TYPE_STRING:
				problems.append("%s: tags must contain only strings" % source)
				valid = false
	return valid


func _validate_dialogue(value: Dictionary, source: String) -> bool:
	var valid := true
	for key in value:
		if not DIALOGUE_FIELDS.has(key):
			problems.append("%s: unsupported Dialogue field '%s'" % [source, key])
			valid = false
	if value.get("schema_version") != 1:
		problems.append("%s: schema_version must be 1" % source)
		valid = false
	var dialogue_id := str(value.get("id", ""))
	if not _valid_id(dialogue_id):
		problems.append("%s: invalid Dialogue ID '%s'" % [source, dialogue_id])
		valid = false
	elif source.ends_with(".json") and source.get_file() != dialogue_id + ".json":
		problems.append("%s: filename must match Dialogue ID" % source)
		valid = false
	if str(value.get("title", "")).strip_edges().is_empty():
		problems.append("%s: title is required" % source)
		valid = false

	var speakers_value: Variant = value.get("speakers", [])
	if typeof(speakers_value) != TYPE_ARRAY or speakers_value.is_empty():
		problems.append("%s: speakers must contain at least one Actor ID" % source)
		valid = false
	else:
		for speaker in speakers_value:
			var speaker_id := str(speaker)
			if not _valid_id(speaker_id) or not actors.has(speaker_id):
				problems.append("%s: missing or invalid Actor speaker '%s'" % [source, speaker_id])
				valid = false

	var nodes_value: Variant = value.get("nodes", [])
	if typeof(nodes_value) != TYPE_ARRAY or nodes_value.is_empty():
		problems.append("%s: nodes must be a non-empty array" % source)
		return false
	var node_ids := {}
	for node_value in nodes_value:
		if typeof(node_value) != TYPE_DICTIONARY:
			problems.append("%s: every node must be an object" % source)
			valid = false
			continue
		var node: Dictionary = node_value
		var node_type := str(node.get("type", "")).to_lower()
		var node_id := str(node.get("id", ""))
		if not NODE_FIELDS.has(node_type):
			problems.append("%s: unsupported node type '%s'" % [source, node_type])
			valid = false
			continue
		for key in node:
			if key not in NODE_FIELDS[node_type]:
				problems.append("%s: node '%s' has unsupported field '%s'" % [source, node_id, key])
				valid = false
		if not _valid_id(node_id) or node_ids.has(node_id):
			problems.append("%s: invalid or duplicate node ID '%s'" % [source, node_id])
			valid = false
		node_ids[node_id] = true
		if node_type == "line":
			if str(node.get("speaker", "")) not in speakers_value:
				problems.append("%s: Line '%s' uses an undeclared speaker" % [source, node_id])
				valid = false
			if str(node.get("text", "")).strip_edges().is_empty():
				problems.append("%s: Line '%s' requires text" % [source, node_id])
				valid = false
		elif node_type == "choice":
			var choices_value: Variant = node.get("choices", [])
			if typeof(choices_value) != TYPE_ARRAY or choices_value.is_empty():
				problems.append("%s: Choice '%s' requires options" % [source, node_id])
				valid = false
			else:
				for option_value in choices_value:
					if typeof(option_value) != TYPE_DICTIONARY:
						problems.append("%s: Choice '%s' contains an invalid option" % [source, node_id])
						valid = false
						continue
					var option: Dictionary = option_value
					for key in option:
						if key not in ["text", "next"]:
							problems.append("%s: Choice option has unsupported field '%s'" % [source, key])
							valid = false
					if str(option.get("text", "")).strip_edges().is_empty():
						problems.append("%s: Choice '%s' has an empty label" % [source, node_id])
						valid = false

	var entry := str(value.get("entry", ""))
	if not node_ids.has(entry):
		problems.append("%s: entry references missing node '%s'" % [source, entry])
		valid = false
	for node_value in nodes_value:
		if typeof(node_value) != TYPE_DICTIONARY:
			continue
		var node: Dictionary = node_value
		var node_type := str(node.get("type", "")).to_lower()
		var targets: Array[String] = []
		if node_type == "line":
			targets.append(str(node.get("next", "")))
		elif node_type == "jump":
			targets.append(str(node.get("target", "")))
		elif node_type == "choice":
			for option_value in node.get("choices", []):
				if typeof(option_value) == TYPE_DICTIONARY:
					targets.append(str(option_value.get("next", "")))
		elif node_type == "end" and not _valid_id(str(node.get("result", ""))):
			problems.append("%s: End '%s' requires a valid Result" % [source, node.get("id", "")])
			valid = false
		for target in targets:
			if not node_ids.has(target):
				problems.append("%s: node '%s' references missing node '%s'" % [source, node.get("id", ""), target])
				valid = false

	var metadata_value: Variant = value.get("metadata", {})
	if typeof(metadata_value) != TYPE_DICTIONARY:
		problems.append("%s: metadata must be an object" % source)
		valid = false
	else:
		for key in metadata_value:
			if key not in ["notes", "tags"]:
				problems.append("%s: metadata has unsupported field '%s'" % [source, key])
				valid = false
	return valid


func _validate_quest(value: Dictionary, source: String) -> bool:
	var valid := true
	for key in value:
		if not QUEST_FIELDS.has(key):
			problems.append("%s: unsupported Quest field '%s'" % [source, key])
			valid = false
	if value.get("schema_version") != 1:
		problems.append("%s: schema_version must be 1" % source)
		valid = false
	var quest_id := str(value.get("id", ""))
	if not _valid_id(quest_id):
		problems.append("%s: invalid Quest ID '%s'" % [source, quest_id])
		valid = false
	elif source.ends_with(".json") and source.get_file() != quest_id + ".json":
		problems.append("%s: filename must match Quest ID" % source)
		valid = false
	if str(value.get("title", "")).strip_edges().is_empty():
		problems.append("%s: title is required" % source)
		valid = false
	if str(value.get("description", "")).strip_edges().is_empty():
		problems.append("%s: description is required" % source)
		valid = false

	var objectives_value: Variant = value.get("objectives", [])
	if typeof(objectives_value) != TYPE_ARRAY or objectives_value.is_empty():
		problems.append("%s: objectives must be a non-empty array" % source)
		return false
	var objective_ids := {}
	for objective_value in objectives_value:
		if typeof(objective_value) != TYPE_DICTIONARY:
			problems.append("%s: every objective must be an object" % source)
			valid = false
			continue
		var objective: Dictionary = objective_value
		var objective_id := str(objective.get("id", ""))
		var objective_type := str(objective.get("type", "")).to_lower()
		if not OBJECTIVE_FIELDS.has(objective_type):
			problems.append("%s: unsupported Objective type '%s'" % [source, objective_type])
			valid = false
			continue
		for key in objective:
			if key not in OBJECTIVE_FIELDS[objective_type]:
				problems.append("%s: Objective '%s' has unsupported field '%s'" % [source, objective_id, key])
				valid = false
		if not _valid_id(objective_id) or objective_ids.has(objective_id):
			problems.append("%s: invalid or duplicate Objective ID '%s'" % [source, objective_id])
			valid = false
		objective_ids[objective_id] = true
		if str(objective.get("description", "")).strip_edges().is_empty():
			problems.append("%s: Objective '%s' requires a description" % [source, objective_id])
			valid = false
		if objective_type == "kill_entity":
			valid = _validate_positive_target(objective, "entity", source, objective_id) and valid
		elif objective_type == "collect_item":
			valid = _validate_positive_target(objective, "item", source, objective_id) and valid
			if not _is_integer_number(objective.get("metadata", -1)):
				problems.append("%s: CollectItem '%s' metadata must be an integer" % [source, objective_id])
				valid = false
		elif objective_type == "reach_location":
			for field in ["dimension", "x", "y", "z", "radius"]:
				if typeof(objective.get(field)) not in [TYPE_INT, TYPE_FLOAT]:
					problems.append("%s: ReachLocation '%s' field '%s' must be numeric" % [source, objective_id, field])
					valid = false
			if float(objective.get("radius", 0.0)) <= 0.0:
				problems.append("%s: ReachLocation '%s' radius must be positive" % [source, objective_id])
				valid = false
		elif objective_type == "interact_actor":
			var actor_id := str(objective.get("actor_id", ""))
			if not _valid_id(actor_id) or not actors.has(actor_id):
				problems.append("%s: InteractActor '%s' references missing Actor '%s'" % [source, objective_id, actor_id])
				valid = false
			if not _is_positive_integer(objective.get("required", 0)):
				problems.append("%s: InteractActor '%s' required must be positive" % [source, objective_id])
				valid = false

	var groups_value: Variant = value.get("objective_groups", [])
	if typeof(groups_value) != TYPE_ARRAY or groups_value.is_empty():
		problems.append("%s: objective_groups must be a non-empty array" % source)
		return false
	var group_ids := {}
	var assigned := {}
	for group_value in groups_value:
		if typeof(group_value) != TYPE_DICTIONARY:
			problems.append("%s: every Objective Group must be an object" % source)
			valid = false
			continue
		var group: Dictionary = group_value
		for key in group:
			if key not in ["id", "mode", "objectives"]:
				problems.append("%s: Objective Group has unsupported field '%s'" % [source, key])
				valid = false
		var group_id := str(group.get("id", ""))
		if not _valid_id(group_id) or group_ids.has(group_id):
			problems.append("%s: invalid or duplicate Objective Group ID '%s'" % [source, group_id])
			valid = false
		group_ids[group_id] = true
		var mode := str(group.get("mode", "")).to_upper()
		if mode not in ["ALL", "ANY", "SEQUENCE"]:
			problems.append("%s: Objective Group '%s' mode must be ALL, ANY, or SEQUENCE" % [source, group_id])
			valid = false
		var members: Variant = group.get("objectives", [])
		if typeof(members) != TYPE_ARRAY or members.is_empty():
			problems.append("%s: Objective Group '%s' must contain Objective IDs" % [source, group_id])
			valid = false
			continue
		for member_value in members:
			var member := str(member_value)
			if not objective_ids.has(member):
				problems.append("%s: Objective Group '%s' references missing Objective '%s'" % [source, group_id, member])
				valid = false
			elif assigned.has(member):
				problems.append("%s: Objective '%s' belongs to more than one group" % [source, member])
				valid = false
			assigned[member] = true
	for objective_id in objective_ids:
		if not assigned.has(objective_id):
			problems.append("%s: Objective '%s' is not assigned to a group" % [source, objective_id])
			valid = false

	var metadata_value: Variant = value.get("metadata", {})
	if typeof(metadata_value) != TYPE_DICTIONARY:
		problems.append("%s: metadata must be an object" % source)
		valid = false
	else:
		for key in metadata_value:
			if key not in ["notes", "tags"]:
				problems.append("%s: metadata has unsupported field '%s'" % [source, key])
				valid = false
	return valid


func _validate_story(value: Dictionary, source: String) -> bool:
	var valid := true
	for key in value:
		if not STORY_FIELDS.has(key):
			problems.append("%s: unsupported Story field '%s'" % [source, key])
			valid = false
	if value.get("schema_version") != 1:
		problems.append("%s: schema_version must be 1" % source)
		valid = false
	var story_id := str(value.get("id", ""))
	if not _valid_id(story_id):
		problems.append("%s: invalid Story ID '%s'" % [source, story_id])
		valid = false
	elif source.ends_with(".json") and source.get_file() != story_id + ".json":
		problems.append("%s: filename must match Story ID" % source)
		valid = false
	if str(value.get("title", "")).strip_edges().is_empty():
		problems.append("%s: title is required" % source)
		valid = false

	var nodes_value: Variant = value.get("nodes", [])
	if typeof(nodes_value) != TYPE_ARRAY or nodes_value.is_empty():
		problems.append("%s: nodes must be a non-empty array" % source)
		return false
	var nodes_by_id := {}
	var end_count := 0
	for node_value in nodes_value:
		if typeof(node_value) != TYPE_DICTIONARY:
			problems.append("%s: every Story node must be an object" % source)
			valid = false
			continue
		var node: Dictionary = node_value
		var node_id := str(node.get("id", ""))
		var node_type := str(node.get("type", "")).to_lower()
		for key in node:
			if key not in ["id", "type", "position", "properties"]:
				_story_problem(story_id, node_id, "unsupported node field '%s'" % key)
				valid = false
		if not STORY_NODE_PROPERTIES.has(node_type):
			_story_problem(story_id, node_id, "unsupported node type '%s'" % node_type)
			valid = false
			continue
		if not _valid_id(node_id) or nodes_by_id.has(node_id):
			_story_problem(story_id, node_id, "invalid or duplicate node ID")
			valid = false
		nodes_by_id[node_id] = node
		if node_type == "end":
			end_count += 1
		var position: Variant = node.get("position", {})
		if typeof(position) != TYPE_DICTIONARY or typeof(position.get("x")) not in [TYPE_INT, TYPE_FLOAT] or typeof(position.get("y")) not in [TYPE_INT, TYPE_FLOAT]:
			_story_problem(story_id, node_id, "position requires numeric x and y")
			valid = false
		var properties: Variant = node.get("properties", {})
		if typeof(properties) != TYPE_DICTIONARY:
			_story_problem(story_id, node_id, "properties must be an object")
			valid = false
			continue
		for key in properties:
			if key not in STORY_NODE_PROPERTIES[node_type]:
				_story_problem(story_id, node_id, "unsupported property '%s'" % key)
				valid = false
		for required_property in STORY_NODE_PROPERTIES[node_type]:
			if not properties.has(required_property) or typeof(properties[required_property]) not in [TYPE_STRING, TYPE_INT, TYPE_FLOAT, TYPE_BOOL]:
				_story_problem(story_id, node_id, "missing scalar property '%s'" % required_property)
				valid = false
		valid = _validate_story_node_reference(story_id, node_id, node_type, properties) and valid
		valid = _validate_story_node_numbers(story_id, node_id, node_type, properties) and valid

	if end_count == 0:
		problems.append("[story:%s] No Exit: Story requires at least one End node" % story_id)
		valid = false
	var entry := str(value.get("entry", ""))
	if not nodes_by_id.has(entry):
		problems.append("[story:%s] entry references missing node '%s'" % [story_id, entry])
		valid = false

	var connections_value: Variant = value.get("connections", [])
	if typeof(connections_value) != TYPE_ARRAY or connections_value.is_empty():
		problems.append("[story:%s] Story requires connections" % story_id)
		return false
	var incoming := {}
	var outgoing := {}
	var adjacency := {}
	var connection_keys := {}
	for node_id in nodes_by_id:
		outgoing[node_id] = {}
		adjacency[node_id] = []
	for connection_value in connections_value:
		if typeof(connection_value) != TYPE_DICTIONARY:
			problems.append("[story:%s] Invalid Connection: connection must be an object" % story_id)
			valid = false
			continue
		var connection: Dictionary = connection_value
		for key in connection:
			if key not in ["from", "output", "to"]:
				problems.append("[story:%s] Invalid Connection field '%s'" % [story_id, key])
				valid = false
		var from_id := str(connection.get("from", ""))
		var output := str(connection.get("output", ""))
		var to_id := str(connection.get("to", ""))
		if not nodes_by_id.has(from_id) or not nodes_by_id.has(to_id):
			problems.append("[story:%s] Invalid Connection: %s.%s -> %s" % [story_id, from_id, output, to_id])
			valid = false
			continue
		var connection_key := from_id + "\n" + output
		if connection_keys.has(connection_key):
			_story_problem(story_id, from_id, "duplicate output connection '%s'" % output)
			valid = false
		connection_keys[connection_key] = true
		outgoing[from_id][output] = true
		incoming[to_id] = true
		adjacency[from_id].append(to_id)
		valid = _validate_story_output(story_id, nodes_by_id[from_id], output) and valid

	for node_id in nodes_by_id:
		var node: Dictionary = nodes_by_id[node_id]
		var node_type := str(node.get("type", ""))
		if node_id != entry and not incoming.has(node_id):
			_story_problem(story_id, node_id, "Unconnected Node")
			valid = false
		if node_type != "end" and outgoing[node_id].is_empty():
			_story_problem(story_id, node_id, "No Exit")
			valid = false
		if node_type in ["branch", "quest_state", "has_item", "variable_compare"]:
			if not outgoing[node_id].has("true") or not outgoing[node_id].has("false"):
				_story_problem(story_id, node_id, "condition requires true and false exits")
				valid = false

	if nodes_by_id.has(entry):
		var reachable := {}
		var pending: Array[String] = [entry]
		while not pending.is_empty():
			var current: String = pending.pop_back()
			if reachable.has(current):
				continue
			reachable[current] = true
			for target in adjacency[current]:
				pending.append(str(target))
		for node_id in nodes_by_id:
			if not reachable.has(node_id):
				_story_problem(story_id, node_id, "Unconnected Node: unreachable from entry")
				valid = false

	var metadata_value: Variant = value.get("metadata", {})
	if typeof(metadata_value) != TYPE_DICTIONARY:
		problems.append("%s: metadata must be an object" % source)
		valid = false
	else:
		for key in metadata_value:
			if key not in ["notes", "tags"]:
				problems.append("%s: metadata has unsupported field '%s'" % [source, key])
				valid = false
	return valid


func _validate_story_node_reference(story_id: String, node_id: String, node_type: String, properties: Dictionary) -> bool:
	if node_type == "interact_actor":
		var actor_id := str(properties.get("actor_id", ""))
		if not actors.has(actor_id):
			_story_problem(story_id, node_id, "Broken Resource Reference: Missing Actor '%s'" % actor_id)
			return false
	elif node_type == "play_dialogue":
		var dialogue_id := str(properties.get("dialogue_id", ""))
		if not dialogues.has(dialogue_id):
			_story_problem(story_id, node_id, "Broken Resource Reference: Missing Dialogue '%s'" % dialogue_id)
			return false
	elif node_type in ["quest_completed", "start_quest", "complete_quest", "quest_state"]:
		var quest_id := str(properties.get("quest_id", ""))
		if not quests.has(quest_id):
			_story_problem(story_id, node_id, "Broken Resource Reference: Missing Quest '%s'" % quest_id)
			return false
	return true


func _validate_story_node_numbers(story_id: String, node_id: String, node_type: String, properties: Dictionary) -> bool:
	var fields: Array[String] = []
	if node_type == "enter_region":
		fields.assign(["dimension", "x", "y", "z", "radius"])
	elif node_type in ["has_item", "give_item"]:
		fields.assign(["metadata", "amount"])
	elif node_type == "give_xp":
		fields.assign(["amount"])
	for field in fields:
		if typeof(properties.get(field)) not in [TYPE_INT, TYPE_FLOAT]:
			_story_problem(story_id, node_id, "property '%s' must be numeric" % field)
			return false
	if node_type == "enter_region" and float(properties.get("radius", 0)) <= 0.0:
		_story_problem(story_id, node_id, "radius must be positive")
		return false
	if node_type in ["has_item", "give_item", "give_xp"] and int(properties.get("amount", 0)) <= 0:
		_story_problem(story_id, node_id, "amount must be positive")
		return false
	return true


func _validate_story_output(story_id: String, node: Dictionary, output: String) -> bool:
	var node_id := str(node.get("id", ""))
	var node_type := str(node.get("type", ""))
	if node_type == "end":
		_story_problem(story_id, node_id, "End cannot have an outgoing connection")
		return false
	if node_type in ["branch", "quest_state", "has_item", "variable_compare"]:
		if output not in ["true", "false"]:
			_story_problem(story_id, node_id, "Invalid Connection output '%s'" % output)
			return false
		return true
	if node_type == "sequence":
		if not output.is_valid_int() or int(output) < 1:
			_story_problem(story_id, node_id, "Sequence output must be a positive integer")
			return false
		return true
	if node_type == "play_dialogue":
		var dialogue_id := str(node.get("properties", {}).get("dialogue_id", ""))
		if not dialogues.has(dialogue_id):
			return false
		var results := {}
		for dialogue_node_value in dialogues[dialogue_id].get("nodes", []):
			if dialogue_node_value.get("type") == "end":
				results[str(dialogue_node_value.get("result", ""))] = true
		if not results.has(output):
			_story_problem(story_id, node_id, "output '%s' is not a Dialogue Result" % output)
			return false
		return true
	if output != "next":
		_story_problem(story_id, node_id, "output must be 'next'")
		return false
	return true


func _story_problem(story_id: String, node_id: String, message: String) -> void:
	problems.append("[story:%s/node:%s] %s" % [story_id, node_id, message])


func _validate_positive_target(objective: Dictionary, field: String, source: String, objective_id: String) -> bool:
	var valid := true
	if str(objective.get(field, "")).strip_edges().is_empty():
		problems.append("%s: Objective '%s' requires '%s'" % [source, objective_id, field])
		valid = false
	if not _is_positive_integer(objective.get("required", 0)):
		problems.append("%s: Objective '%s' required must be a positive integer" % [source, objective_id])
		valid = false
	return valid


func _is_integer_number(value: Variant) -> bool:
	return typeof(value) in [TYPE_INT, TYPE_FLOAT] and float(value) == floor(float(value))


func _is_positive_integer(value: Variant) -> bool:
	return _is_integer_number(value) and int(value) > 0


func _valid_id(value: String) -> bool:
	var regex := RegEx.new()
	regex.compile(ID_PATTERN)
	return regex.search(value) != null


func _read_json_object(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		problems.append("Missing JSON file: %s" % path)
		return {}
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		problems.append("Cannot read JSON file: %s" % path)
		return {}
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	if error != OK:
		problems.append("%s:%d: %s" % [path, json.get_error_line(), json.get_error_message()])
		return {}
	if typeof(json.data) != TYPE_DICTIONARY:
		problems.append("JSON root must be an object: %s" % path)
		return {}
	return json.data


func _atomic_write(path: String, contents: String) -> bool:
	var temporary := path + ".tmp"
	var backup := path + ".bak"
	var file := FileAccess.open(temporary, FileAccess.WRITE)
	if file == null:
		problems.append("Cannot write temporary file: %s" % temporary)
		return false
	file.store_string(contents)
	file.flush()
	file.close()

	if FileAccess.file_exists(backup):
		DirAccess.remove_absolute(backup)
	if FileAccess.file_exists(path):
		var backup_error := DirAccess.rename_absolute(path, backup)
		if backup_error != OK:
			DirAccess.remove_absolute(temporary)
			problems.append("Cannot prepare atomic save for: %s" % path)
			return false

	var replace_error := DirAccess.rename_absolute(temporary, path)
	if replace_error != OK:
		if FileAccess.file_exists(backup):
			DirAccess.rename_absolute(backup, path)
		problems.append("Cannot replace file atomically: %s" % path)
		return false

	return true


func _copy_tree(source: String, target: String) -> bool:
	var directory := DirAccess.open(source)
	if directory == null:
		problems.append("Cannot open Content Pack source directory: %s" % source)
		return false
	directory.list_dir_begin()
	var entry := directory.get_next()
	while not entry.is_empty():
		if entry not in [".", ".."]:
			var source_entry := source.path_join(entry)
			var target_entry := target.path_join(entry)
			if directory.current_is_dir():
				if DirAccess.make_dir_recursive_absolute(target_entry) not in [OK, ERR_ALREADY_EXISTS]:
					directory.list_dir_end()
					return false
				if not _copy_tree(source_entry, target_entry):
					directory.list_dir_end()
					return false
			elif not entry.ends_with(".tmp") and not entry.ends_with(".bak"):
				if not _copy_file(source_entry, target_entry):
					directory.list_dir_end()
					return false
		entry = directory.get_next()
	directory.list_dir_end()
	return true


func _copy_file(source: String, target: String) -> bool:
	var error := DirAccess.copy_absolute(source, target)
	if error != OK:
		problems.append("Cannot copy Content Pack file: %s" % source)
		return false
	return true


func _remove_tree(path: String) -> bool:
	var directory := DirAccess.open(path)
	if directory == null:
		return true
	directory.list_dir_begin()
	var entry := directory.get_next()
	while not entry.is_empty():
		if entry not in [".", ".."]:
			var child := path.path_join(entry)
			if directory.current_is_dir():
				if not _remove_tree(child):
					directory.list_dir_end()
					return false
			elif DirAccess.remove_absolute(child) != OK:
				directory.list_dir_end()
				return false
		entry = directory.get_next()
	directory.list_dir_end()
	return DirAccess.remove_absolute(path) == OK
