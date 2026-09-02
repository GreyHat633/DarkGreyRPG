package darkgrey.rpg.story;

import java.io.BufferedReader;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.DialogueNode;
import darkgrey.rpg.dialogue.EndNode;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.quest.QuestDefinition;

public final class StoryLoader {

    private static final Pattern RESOURCE_ID = Pattern.compile("[a-z0-9][a-z0-9_.-]*");
    private static final Set<String> TOP_FIELDS = set(
        "schema_version",
        "id",
        "title",
        "entry",
        "nodes",
        "connections",
        "metadata",
        "display_name",
        "description",
        "tags",
        "entry_presentation",
        "owned_resources",
        "referenced_resources",
        "flow_ref");
    private static final Set<String> NODE_FIELDS = set("id", "type", "position", "properties");
    private static final Set<String> POSITION_FIELDS = set("x", "y");
    private static final Set<String> CONNECTION_FIELDS = set("from", "output", "to");
    private static final Set<String> METADATA_FIELDS = set("notes", "tags");
    private static final Map<StoryNodeType, Set<String>> PROPERTY_FIELDS = propertyFields();

    private StoryLoader() {}

    public static Map<String, StoryDefinition> load(File projectDirectory, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        File directory = new File(projectDirectory, "stories");
        if (!directory.isDirectory()) {
            throw new ProjectLoadException("Missing stories directory: " + directory.getAbsolutePath());
        }
        File[] files = directory.listFiles();
        if (files == null) {
            throw new ProjectLoadException("Cannot list stories directory: " + directory.getAbsolutePath());
        }
        Arrays.sort(files, new Comparator<File>() {

            @Override
            public int compare(File left, File right) {
                return left.getName()
                    .compareToIgnoreCase(right.getName());
            }
        });
        Map<String, StoryDefinition> stories = new LinkedHashMap<String, StoryDefinition>();
        for (File file : files) {
            if (!file.isFile() || !file.getName()
                .toLowerCase()
                .endsWith(".json")) {
                continue;
            }
            StoryDefinition story = loadStory(file, actors, dialogues, quests);
            if (stories.put(story.getId(), story) != null) {
                throw new ProjectLoadException("Duplicate Story id '" + story.getId() + "'");
            }
        }
        validateStoryTargets(stories);
        return stories;
    }

    /** Reuses the strict legacy Story parser for one detached DGRS entry. */
    public static StoryDefinition loadPackagedStory(byte[] bytes, String source, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        if (bytes == null) throw new ProjectLoadException("Legacy Story bytes are required");
        if (source == null || source.trim()
            .isEmpty()) throw new ProjectLoadException("Legacy Story source is required");
        File file = new File(source);
        return loadStory(file, actors, dialogues, quests, readObject(bytes, file));
    }

    public static void validatePackagedStories(Map<String, StoryDefinition> stories) throws ProjectLoadException {
        if (stories == null) throw new ProjectLoadException("Legacy Story map is required");
        validateStoryTargets(stories);
    }

    private static StoryDefinition loadStory(File file, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        return loadStory(file, actors, dialogues, quests, readObject(file));
    }

    private static StoryDefinition loadStory(File file, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests, JsonObject json)
        throws ProjectLoadException {
        rejectUnknown(file, json, TOP_FIELDS);
        int schema = requiredInt(file, json, "schema_version");
        if (schema != 1 && schema != 2) {
            throw new ProjectLoadException("Unsupported schema_version " + schema + " in " + file);
        }
        String id = requiredId(file, json, "id");
        if (!file.getName()
            .equals(id + ".json")) {
            throw new ProjectLoadException("Story file name must match its id: " + file);
        }
        String title = requiredString(file, json, "title");
        String entry = requiredId(file, json, "entry");
        List<StoryNode> nodes = loadNodes(file, json.get("nodes"), actors, dialogues, quests);
        Map<String, StoryNode> byId = new LinkedHashMap<String, StoryNode>();
        for (StoryNode node : nodes) {
            if (byId.put(node.getId(), node) != null) {
                throw new ProjectLoadException("Duplicate Story node id '" + node.getId() + "' in " + file);
            }
        }
        if (!byId.containsKey(entry)) {
            throw new ProjectLoadException("Story entry references missing node '" + entry + "' in " + file);
        }
        List<StoryConnection> connections = loadConnections(file, json.get("connections"), byId, dialogues);
        validateGraph(file, entry, nodes, connections);
        JsonObject metadata = optionalObject(file, json, "metadata");
        rejectUnknown(file, metadata, METADATA_FIELDS);
        return new StoryDefinition(
            schema,
            id,
            title,
            entry,
            nodes,
            connections,
            optionalString(file, metadata, "notes", ""),
            optionalStringList(file, metadata, "tags"));
    }

    private static List<StoryNode> loadNodes(File file, JsonElement value, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        if (value == null || !value.isJsonArray()
            || value.getAsJsonArray()
                .size() == 0) {
            throw new ProjectLoadException("Story nodes must be a non-empty array in " + file);
        }
        List<StoryNode> nodes = new ArrayList<StoryNode>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonObject()) {
                throw new ProjectLoadException("Story nodes must be objects in " + file);
            }
            JsonObject json = element.getAsJsonObject();
            rejectUnknown(file, json, NODE_FIELDS);
            String id = requiredId(file, json, "id");
            StoryNodeType type;
            try {
                type = parseType(requiredString(file, json, "type"));
            } catch (IllegalArgumentException exception) {
                throw new ProjectLoadException("Unsupported Story node type in " + file + ": " + json.get("type"));
            }
            JsonObject position = requiredObject(file, json, "position");
            rejectUnknown(file, position, POSITION_FIELDS);
            JsonObject propertiesJson = requiredObject(file, json, "properties");
            rejectUnknown(file, propertiesJson, PROPERTY_FIELDS.get(type));
            Map<String, String> properties = new LinkedHashMap<String, String>();
            for (String property : PROPERTY_FIELDS.get(type)) {
                JsonElement propertyValue = propertiesJson.get(property);
                if (propertyValue == null || !propertyValue.isJsonPrimitive()) {
                    throw new ProjectLoadException(
                        "Story node '" + id + "' requires scalar property '" + property + "' in " + file);
                }
                properties.put(property, propertyValue.getAsString());
            }
            validateReferences(file, id, type, properties, actors, dialogues, quests);
            validateNumbers(file, id, type, properties);
            nodes.add(
                new StoryNode(
                    id,
                    type,
                    requiredDouble(file, position, "x"),
                    requiredDouble(file, position, "y"),
                    properties));
        }
        return nodes;
    }

    private static List<StoryConnection> loadConnections(File file, JsonElement value, Map<String, StoryNode> nodes,
        Map<String, DialogueDefinition> dialogues) throws ProjectLoadException {
        if (value == null || !value.isJsonArray()) {
            throw new ProjectLoadException("Story connections must be an array in " + file);
        }
        List<StoryConnection> result = new ArrayList<StoryConnection>();
        Set<String> outputKeys = new HashSet<String>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonObject()) {
                throw new ProjectLoadException("Story connections must be objects in " + file);
            }
            JsonObject json = element.getAsJsonObject();
            rejectUnknown(file, json, CONNECTION_FIELDS);
            String from = requiredId(file, json, "from");
            String output = requiredString(file, json, "output");
            String to = requiredId(file, json, "to");
            if (!nodes.containsKey(from) || !nodes.containsKey(to)) {
                throw new ProjectLoadException("Story connection references a missing node in " + file);
            }
            if (!outputKeys.add(from + "\u0000" + output)) {
                throw new ProjectLoadException(
                    "Story node '" + from + "' has duplicate output connection '" + output + "' in " + file);
            }
            validateOutput(file, nodes.get(from), output, dialogues);
            result.add(new StoryConnection(from, output, to));
        }
        return result;
    }

    private static void validateGraph(File file, String entry, List<StoryNode> nodes, List<StoryConnection> connections)
        throws ProjectLoadException {
        Set<String> incoming = new HashSet<String>();
        Set<String> reachable = new HashSet<String>();
        Map<String, List<String>> adjacency = new HashMap<String, List<String>>();
        Map<String, Set<String>> outputs = new HashMap<String, Set<String>>();
        boolean hasTerminal = false;
        for (StoryNode node : nodes) {
            adjacency.put(node.getId(), new ArrayList<String>());
            outputs.put(node.getId(), new HashSet<String>());
            if (isTerminal(node.getType())) {
                hasTerminal = true;
            }
        }
        for (StoryConnection connection : connections) {
            incoming.add(connection.getTo());
            adjacency.get(connection.getFrom())
                .add(connection.getTo());
            outputs.get(connection.getFrom())
                .add(connection.getOutput());
        }
        if (!hasTerminal) {
            throw new ProjectLoadException("Story has no terminal End, EndStory, or EnterStory node in " + file);
        }
        for (StoryNode node : nodes) {
            if (!node.getId()
                .equals(entry) && !incoming.contains(node.getId())) {
                throw new ProjectLoadException("Unconnected Story node '" + node.getId() + "' in " + file);
            }
            if (!isTerminal(node.getType()) && outputs.get(node.getId())
                .isEmpty()) {
                throw new ProjectLoadException("Story node '" + node.getId() + "' has no exit in " + file);
            }
            if ((node.getType() == StoryNodeType.QUEST_STATE || node.getType() == StoryNodeType.HAS_ITEM
                || node.getType() == StoryNodeType.VARIABLE_COMPARE
                || node.getType() == StoryNodeType.BRANCH)
                && (!outputs.get(node.getId())
                    .contains("true")
                    || !outputs.get(node.getId())
                        .contains("false"))) {
                throw new ProjectLoadException(
                    "Condition node '" + node.getId() + "' must connect true and false outputs in " + file);
            }
            if (node.getType() == StoryNodeType.DIALOGUE_EXIT_BRANCH) {
                Set<String> configured = parseExitNames(node.getProperty("exit_names"));
                if (configured.isEmpty() || !outputs.get(node.getId())
                    .containsAll(configured)) {
                    throw new ProjectLoadException(
                        "DialogueExitBranch node '" + node.getId() + "' must connect every configured exit in " + file);
                }
            }
        }
        visit(entry, adjacency, reachable);
        if (reachable.size() != nodes.size()) {
            throw new ProjectLoadException("Story contains nodes unreachable from entry in " + file);
        }
    }

    private static void visit(String node, Map<String, List<String>> adjacency, Set<String> visited) {
        if (!visited.add(node)) {
            return;
        }
        for (String target : adjacency.get(node)) {
            visit(target, adjacency, visited);
        }
    }

    private static void validateOutput(File file, StoryNode node, String output,
        Map<String, DialogueDefinition> dialogues) throws ProjectLoadException {
        StoryNodeType type = node.getType();
        if (isTerminal(type)) {
            throw new ProjectLoadException("Terminal Story node cannot have outgoing connections in " + file);
        }
        if (type == StoryNodeType.QUEST_STATE || type == StoryNodeType.HAS_ITEM
            || type == StoryNodeType.VARIABLE_COMPARE
            || type == StoryNodeType.BRANCH) {
            if (!"true".equals(output) && !"false".equals(output)) {
                throw new ProjectLoadException("Condition output must be true or false in " + file);
            }
            return;
        }
        if (type == StoryNodeType.SEQUENCE) {
            try {
                if (Integer.parseInt(output) < 1) {
                    throw new NumberFormatException();
                }
            } catch (NumberFormatException exception) {
                throw new ProjectLoadException("Sequence outputs must be positive integers in " + file);
            }
            return;
        }
        if (type == StoryNodeType.DIALOGUE_EXIT_BRANCH) {
            if (!parseExitNames(node.getProperty("exit_names")).contains(output)) {
                throw new ProjectLoadException(
                    "DialogueExitBranch output '" + output + "' is not configured on " + node.getId() + " in " + file);
            }
            return;
        }
        if (type == StoryNodeType.PLAY_DIALOGUE) {
            if ("next".equals(output)) {
                return;
            }
            DialogueDefinition dialogue = dialogues.get(node.getProperty("dialogue_id"));
            for (DialogueNode dialogueNode : dialogue.getNodes()) {
                if (dialogueNode instanceof EndNode && ((EndNode) dialogueNode).getResult()
                    .equals(output)) {
                    return;
                }
            }
            throw new ProjectLoadException(
                "PlayDialogue output '" + output + "' is not a Result of " + dialogue.getId() + " in " + file);
        }
        if (!"next".equals(output)) {
            throw new ProjectLoadException("Story node '" + node.getId() + "' requires output 'next' in " + file);
        }
    }

    private static void validateReferences(File file, String nodeId, StoryNodeType type, Map<String, String> properties,
        Map<String, ActorDefinition> actors, Map<String, DialogueDefinition> dialogues,
        Map<String, QuestDefinition> quests) throws ProjectLoadException {
        if (type == StoryNodeType.INTERACT_ACTOR && !actors.containsKey(properties.get("actor_id"))) {
            throw missing(file, nodeId, "Actor", properties.get("actor_id"));
        }
        if (type == StoryNodeType.PLAY_DIALOGUE && !dialogues.containsKey(properties.get("dialogue_id"))) {
            throw missing(file, nodeId, "Dialogue", properties.get("dialogue_id"));
        }
        if ((type == StoryNodeType.QUEST_COMPLETED || type == StoryNodeType.START_QUEST
            || type == StoryNodeType.COMPLETE_QUEST
            || type == StoryNodeType.QUEST_STATE) && !quests.containsKey(properties.get("quest_id"))) {
            throw missing(file, nodeId, "Quest", properties.get("quest_id"));
        }
    }

    private static ProjectLoadException missing(File file, String nodeId, String resourceType, String id) {
        return new ProjectLoadException(
            "Story node '" + nodeId + "' references missing " + resourceType + " '" + id + "' in " + file);
    }

    private static void validateNumbers(File file, String nodeId, StoryNodeType type, Map<String, String> properties)
        throws ProjectLoadException {
        try {
            if (type == StoryNodeType.ENTER_REGION) {
                Integer.parseInt(properties.get("dimension"));
                Double.parseDouble(properties.get("x"));
                Double.parseDouble(properties.get("y"));
                Double.parseDouble(properties.get("z"));
                if (Double.parseDouble(properties.get("radius")) <= 0.0D) {
                    throw new NumberFormatException();
                }
            } else if (type == StoryNodeType.HAS_ITEM || type == StoryNodeType.GIVE_ITEM) {
                Integer.parseInt(properties.get("metadata"));
                if (Integer.parseInt(properties.get("amount")) <= 0) {
                    throw new NumberFormatException();
                }
            } else if (type == StoryNodeType.GIVE_XP && Integer.parseInt(properties.get("amount")) <= 0) {
                throw new NumberFormatException();
            }
        } catch (NumberFormatException exception) {
            throw new ProjectLoadException("Invalid numeric property on Story node '" + nodeId + "' in " + file);
        }
    }

    private static Map<StoryNodeType, Set<String>> propertyFields() {
        Map<StoryNodeType, Set<String>> fields = new HashMap<StoryNodeType, Set<String>>();
        fields.put(StoryNodeType.STORY_START, Collections.<String>emptySet());
        fields.put(StoryNodeType.INTERACT_ACTOR, set("actor_id"));
        fields.put(StoryNodeType.ENTER_REGION, set("dimension", "x", "y", "z", "radius"));
        fields.put(StoryNodeType.QUEST_COMPLETED, set("quest_id"));
        fields.put(StoryNodeType.PLAY_DIALOGUE, set("dialogue_id"));
        fields.put(StoryNodeType.DIALOGUE_EXIT_BRANCH, set("exit_names"));
        fields.put(StoryNodeType.START_QUEST, set("quest_id"));
        fields.put(StoryNodeType.COMPLETE_QUEST, set("quest_id"));
        fields.put(StoryNodeType.BRANCH, set("variable", "operator", "value"));
        fields.put(StoryNodeType.SEQUENCE, Collections.<String>emptySet());
        fields.put(StoryNodeType.QUEST_STATE, set("quest_id", "state"));
        fields.put(StoryNodeType.HAS_ITEM, set("item", "metadata", "amount"));
        fields.put(StoryNodeType.VARIABLE_COMPARE, set("variable", "operator", "value"));
        fields.put(StoryNodeType.GIVE_ITEM, set("item", "metadata", "amount"));
        fields.put(StoryNodeType.GIVE_XP, set("amount"));
        fields.put(StoryNodeType.SEND_MESSAGE, set("message"));
        fields.put(StoryNodeType.SET_VARIABLE, set("variable", "value"));
        fields.put(StoryNodeType.ENTER_STORY, set("target_story_id"));
        fields.put(StoryNodeType.END_STORY, Collections.<String>emptySet());
        fields.put(StoryNodeType.END, Collections.<String>emptySet());
        return fields;
    }

    private static StoryNodeType parseType(String configured) {
        String normalized = configured.toUpperCase();
        if ("ACTOR_INTERACT".equals(normalized)) {
            normalized = "INTERACT_ACTOR";
        } else if ("WAIT_QUEST_COMPLETE".equals(normalized)) {
            normalized = "QUEST_COMPLETED";
        }
        return StoryNodeType.valueOf(normalized);
    }

    private static boolean isTerminal(StoryNodeType type) {
        return type == StoryNodeType.END || type == StoryNodeType.END_STORY || type == StoryNodeType.ENTER_STORY;
    }

    private static Set<String> parseExitNames(String configured) {
        Set<String> exits = new HashSet<String>();
        if (configured == null) {
            return exits;
        }
        for (String value : configured.split("[,，;；]")) {
            String exit = value.trim();
            if (!exit.isEmpty()) {
                exits.add(exit);
            }
        }
        return exits;
    }

    private static void validateStoryTargets(Map<String, StoryDefinition> stories) throws ProjectLoadException {
        for (StoryDefinition story : stories.values()) {
            for (StoryNode node : story.getNodes()) {
                if (node.getType() == StoryNodeType.ENTER_STORY
                    && !stories.containsKey(node.getProperty("target_story_id"))) {
                    throw new ProjectLoadException(
                        "Story node '" + story.getId()
                            + "."
                            + node.getId()
                            + "' references missing Story '"
                            + node.getProperty("target_story_id")
                            + "'");
                }
            }
        }
    }

    private static Set<String> set(String... values) {
        return Collections.unmodifiableSet(new HashSet<String>(Arrays.asList(values)));
    }

    private static JsonObject readObject(File file) throws ProjectLoadException {
        try {
            BufferedReader reader = new BufferedReader(
                new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8));
            try {
                JsonElement root = new JsonParser().parse(reader);
                if (!root.isJsonObject()) {
                    throw new ProjectLoadException("JSON root must be an object: " + file);
                }
                return root.getAsJsonObject();
            } finally {
                reader.close();
            }
        } catch (IOException exception) {
            throw new ProjectLoadException("Cannot read " + file, exception);
        } catch (RuntimeException exception) {
            throw new ProjectLoadException("Invalid JSON in " + file + ": " + exception.getMessage(), exception);
        }
    }

    private static JsonObject readObject(byte[] bytes, File file) throws ProjectLoadException {
        try {
            BufferedReader reader = new BufferedReader(
                new InputStreamReader(new ByteArrayInputStream(bytes), StandardCharsets.UTF_8));
            try {
                JsonElement root = new JsonParser().parse(reader);
                if (!root.isJsonObject()) throw new ProjectLoadException("JSON root must be an object: " + file);
                return root.getAsJsonObject();
            } finally {
                reader.close();
            }
        } catch (IOException exception) {
            throw new ProjectLoadException("Cannot read " + file, exception);
        } catch (RuntimeException exception) {
            throw new ProjectLoadException("Invalid JSON in " + file + ": " + exception.getMessage(), exception);
        }
    }

    private static void rejectUnknown(File file, JsonObject json, Set<String> allowed) throws ProjectLoadException {
        for (Map.Entry<String, JsonElement> entry : json.entrySet()) {
            if (!allowed.contains(entry.getKey())) {
                throw new ProjectLoadException("Unsupported field '" + entry.getKey() + "' in " + file);
            }
        }
    }

    private static String requiredId(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = requiredString(file, json, field);
        if (!RESOURCE_ID.matcher(value)
            .matches()) {
            throw new ProjectLoadException("Invalid resource id '" + value + "' for '" + field + "' in " + file);
        }
        return value;
    }

    private static String requiredString(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = optionalString(file, json, field, null);
        if (value == null || value.trim()
            .isEmpty()) {
            throw new ProjectLoadException("Field '" + field + "' must be a non-empty string in " + file);
        }
        return value.trim();
    }

    private static String optionalString(File file, JsonObject json, String field, String fallback)
        throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return fallback;
        }
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString()) {
            throw new ProjectLoadException("Field '" + field + "' must be a string in " + file);
        }
        return value.getAsString();
    }

    private static int requiredInt(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber()) {
            throw new ProjectLoadException("Field '" + field + "' must be an integer in " + file);
        }
        return value.getAsInt();
    }

    private static double requiredDouble(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber()) {
            throw new ProjectLoadException("Field '" + field + "' must be numeric in " + file);
        }
        return value.getAsDouble();
    }

    private static JsonObject requiredObject(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || !value.isJsonObject()) {
            throw new ProjectLoadException("Field '" + field + "' must be an object in " + file);
        }
        return value.getAsJsonObject();
    }

    private static JsonObject optionalObject(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return new JsonObject();
        }
        if (!value.isJsonObject()) {
            throw new ProjectLoadException("Field '" + field + "' must be an object in " + file);
        }
        return value.getAsJsonObject();
    }

    private static List<String> optionalStringList(File file, JsonObject json, String field)
        throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return Collections.emptyList();
        }
        if (!value.isJsonArray()) {
            throw new ProjectLoadException("Field '" + field + "' must be an array in " + file);
        }
        List<String> result = new ArrayList<String>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonPrimitive() || !element.getAsJsonPrimitive()
                .isString()) {
                throw new ProjectLoadException("Field '" + field + "' must contain strings in " + file);
            }
            result.add(element.getAsString());
        }
        return result;
    }
}
