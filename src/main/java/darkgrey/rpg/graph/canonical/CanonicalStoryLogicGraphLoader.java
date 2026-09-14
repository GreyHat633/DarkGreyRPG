package darkgrey.rpg.graph.canonical;

import java.io.File;
import java.io.IOException;
import java.io.StringReader;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;
import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

/** Strict loader for the optional schema-version-2 cross-Story boundary graph. */
public final class CanonicalStoryLogicGraphLoader {

    private static final Set<String> ROOT = set("schema_version", "connections");
    private static final Set<String> CONNECTION = set(
        "source_story_id",
        "source_port_id",
        "target_story_id",
        "target_port_id",
        "interface_kind");

    public CanonicalStoryLogicGraph load(File file, Map<String, CanonicalGraphResource> stories)
        throws CanonicalGraphResourceException {
        return load(file == null ? null : file.toPath(), stories);
    }

    /** Loads the optional graph and checks every endpoint against the supplied Story resources. */
    public CanonicalStoryLogicGraph load(Path file, Map<String, CanonicalGraphResource> stories)
        throws CanonicalGraphResourceException {
        if (file == null) throw CanonicalGraphResourceException
            .failure("story.logic.graph.file.required", "Canonical Story logic graph file is required.");
        if (!Files.exists(file)) return CanonicalStoryLogicGraph.empty();
        if (!Files.isRegularFile(file)) throw CanonicalGraphResourceException
            .failure("story.logic.graph.file.missing", "Canonical Story logic graph file does not exist: " + file);
        if (stories == null) throw CanonicalGraphResourceException.failure(
            "story.logic.graph.stories.required",
            "Known canonical Stories are required when loading the Story logic graph.");
        try {
            return parse(Files.readAllBytes(file), stories, true);
        } catch (IOException exception) {
            throw new CanonicalGraphResourceException(
                "story.logic.graph.file.read",
                "Could not read canonical Story logic graph '" + file + "'.",
                exception);
        }
    }

    public CanonicalStoryLogicGraph load(File file, CanonicalProjectContent content)
        throws CanonicalGraphResourceException {
        return load(file, content == null ? null : content.getStories());
    }

    public CanonicalStoryLogicGraph load(Path file, CanonicalProjectContent content)
        throws CanonicalGraphResourceException {
        return load(file, content == null ? null : content.getStories());
    }

    /** Parses a package-owned outgoing fragment; endpoints are validated after all packages are merged. */
    public CanonicalStoryLogicGraph loadUnresolved(File file) throws CanonicalGraphResourceException {
        if (file == null || !file.isFile()) throw CanonicalGraphResourceException
            .failure("story.logic.graph.file.missing", "Canonical Story logic graph fragment does not exist.");
        try {
            return parse(
                Files.readAllBytes(file.toPath()),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                false);
        } catch (IOException exception) {
            throw new CanonicalGraphResourceException(
                "story.logic.graph.file.read",
                "Could not read canonical Story logic graph fragment '" + file + "'.",
                exception);
        }
    }

    /** Parses a detached package-owned outgoing fragment; merged validation remains the publication gate. */
    public CanonicalStoryLogicGraph loadUnresolved(byte[] bytes, String source) throws CanonicalGraphResourceException {
        if (bytes == null) throw CanonicalGraphResourceException
            .failure("story.logic.graph.bytes.required", "Canonical Story logic graph bytes are required.");
        if (source == null || source.trim()
            .isEmpty())
            throw CanonicalGraphResourceException
                .failure("story.logic.graph.file.required", "Canonical Story logic graph source is required.");
        return parse(bytes, Collections.<String, CanonicalGraphResource>emptyMap(), false);
    }

    private static CanonicalStoryLogicGraph parse(byte[] bytes, Map<String, CanonicalGraphResource> stories,
        boolean validateEndpoints) throws CanonicalGraphResourceException {
        JsonObject root = readRoot(bytes);
        exact(root, ROOT, "story.logic.graph.root");
        int version = integer(root, "schema_version", "story.logic.graph.root");
        if (version != CanonicalStoryLogicGraph.CURRENT_SCHEMA_VERSION) throw CanonicalGraphResourceException.failure(
            "story.logic.graph.schema_version.unsupported",
            "Unsupported Story logic graph schema_version " + version + "; expected 2.");
        JsonArray values = array(root, "connections", "story.logic.graph.root");
        List<CanonicalStoryLogicConnection> connections = new ArrayList<CanonicalStoryLogicConnection>();
        Set<CanonicalStoryLogicConnection> duplicateEdges = new HashSet<CanonicalStoryLogicConnection>();
        Map<String, String> targetSources = new LinkedHashMap<String, String>();
        Set<String> flowSources = new HashSet<String>();
        for (JsonElement value : values) {
            CanonicalStoryLogicConnection connection = connection(value);
            if (!duplicateEdges.add(connection)) throw CanonicalGraphResourceException.failure(
                "story.logic.graph.connection.duplicate",
                "Duplicate Story logic connections are not allowed.");
            if (validateEndpoints) {
                requireStory(stories, connection.getSourceStoryId(), "source");
                requireStory(stories, connection.getTargetStoryId(), "target");
                requirePublicPort(
                    stories.get(connection.getSourceStoryId()),
                    connection.getSourcePortId(),
                    true,
                    connection.getInterfaceKind());
                requirePublicPort(
                    stories.get(connection.getTargetStoryId()),
                    connection.getTargetPortId(),
                    false,
                    connection.getInterfaceKind());
            }
            String targetKey = connection.getTargetStoryId() + "\u0000" + connection.getTargetPortId();
            if (connection.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC
                && targetSources.put(targetKey, connection.getSourceStoryId() + "\u0000" + connection.getSourcePortId())
                    != null)
                throw CanonicalGraphResourceException.failure(
                    "story.logic.graph.target.multiple_sources",
                    "A Story logic input may have at most one source.");
            if (connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW
                && !flowSources.add(connection.getSourceStoryId() + "\u0000" + connection.getSourcePortId()))
                throw CanonicalGraphResourceException.failure(
                    "story.logic.graph.source.multiple_targets",
                    "A Story Flow output may have at most one target.");
            connections.add(connection);
        }
        return new CanonicalStoryLogicGraph(version, connections);
    }

    /** Validates one already-parsed aggregate against the complete merged Story set. */
    public void validate(CanonicalStoryLogicGraph graph, Map<String, CanonicalGraphResource> stories)
        throws CanonicalGraphResourceException {
        if (graph == null || stories == null) throw CanonicalGraphResourceException
            .failure("story.logic.graph.validation.required", "Story logic graph and known Stories are required.");
        if (graph.getSchemaVersion() != CanonicalStoryLogicGraph.CURRENT_SCHEMA_VERSION)
            throw CanonicalGraphResourceException.failure(
                "story.logic.graph.schema_version.unsupported",
                "Unsupported Story logic graph schema_version " + graph.getSchemaVersion() + "; expected 2.");
        Set<CanonicalStoryLogicConnection> duplicateEdges = new HashSet<CanonicalStoryLogicConnection>();
        Set<String> targets = new HashSet<String>();
        Set<String> flowSources = new HashSet<String>();
        for (CanonicalStoryLogicConnection connection : graph.getConnections()) {
            if (connection == null) throw CanonicalGraphResourceException
                .failure("story.logic.graph.connection.invalid", "Story logic graph cannot contain a null connection.");
            if (!duplicateEdges.add(connection)) throw CanonicalGraphResourceException.failure(
                "story.logic.graph.connection.duplicate",
                "Duplicate Story logic connections are not allowed.");
            String targetKey = connection.getTargetStoryId() + "\u0000" + connection.getTargetPortId();
            if (connection.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC && !targets.add(targetKey))
                throw CanonicalGraphResourceException.failure(
                    "story.logic.graph.target.multiple_sources",
                    "A Story logic input may have at most one source.");
            if (connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW
                && !flowSources.add(connection.getSourceStoryId() + "\u0000" + connection.getSourcePortId()))
                throw CanonicalGraphResourceException.failure(
                    "story.logic.graph.source.multiple_targets",
                    "A Story Flow output may have at most one target.");
            requireStory(stories, connection.getSourceStoryId(), "source");
            requireStory(stories, connection.getTargetStoryId(), "target");
            requirePublicPort(
                stories.get(connection.getSourceStoryId()),
                connection.getSourcePortId(),
                true,
                connection.getInterfaceKind());
            requirePublicPort(
                stories.get(connection.getTargetStoryId()),
                connection.getTargetPortId(),
                false,
                connection.getInterfaceKind());
        }
    }

    private static JsonObject readRoot(byte[] bytes) throws CanonicalGraphResourceException {
        try {
            JsonReader reader = new JsonReader(new StringReader(new String(bytes, StandardCharsets.UTF_8)));
            reader.setLenient(false);
            JsonElement element = readTree(reader);
            if (reader.peek() != JsonToken.END_DOCUMENT) throw CanonicalGraphResourceException
                .failure("story.logic.graph.json.invalid", "Trailing JSON content is not allowed.");
            if (!element.isJsonObject()) throw CanonicalGraphResourceException
                .failure("story.logic.graph.root.invalid", "Canonical Story logic graph root must be an object.");
            return element.getAsJsonObject();
        } catch (CanonicalGraphResourceException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new CanonicalGraphResourceException(
                "story.logic.graph.json.invalid",
                "Canonical Story logic graph JSON is invalid.",
                exception);
        }
    }

    private static CanonicalStoryLogicConnection connection(JsonElement value) throws CanonicalGraphResourceException {
        JsonObject object = requireObject(value, "story.logic.graph.connection");
        exact(object, CONNECTION, "story.logic.graph.connection");
        String interfaceKind = string(object, "interface_kind", "story.logic.graph.connection");
        CanonicalGraphInterfaceKind kind;
        if ("Flow".equals(interfaceKind)) kind = CanonicalGraphInterfaceKind.FLOW;
        else if ("Logic".equals(interfaceKind)) kind = CanonicalGraphInterfaceKind.LOGIC;
        else throw CanonicalGraphResourceException
            .failure("story.logic.graph.interface_kind.invalid", "interface_kind must be exactly 'Flow' or 'Logic'.");
        return new CanonicalStoryLogicConnection(
            string(object, "source_story_id", "story.logic.graph.connection"),
            string(object, "source_port_id", "story.logic.graph.connection"),
            string(object, "target_story_id", "story.logic.graph.connection"),
            string(object, "target_port_id", "story.logic.graph.connection"),
            kind);
    }

    private static void requireStory(Map<String, CanonicalGraphResource> stories, String storyId, String side)
        throws CanonicalGraphResourceException {
        CanonicalGraphResource resource = stories.get(storyId);
        if (resource == null || resource.getResourceKind() != CanonicalGraphResourceKind.STORY)
            throw CanonicalGraphResourceException.failure(
                "story.logic.graph." + side + ".story.missing",
                "Story logic connection " + side + " Story does not exist: " + storyId);
    }

    private static void requirePublicPort(CanonicalGraphResource story, String portId, boolean output,
        CanonicalGraphInterfaceKind kind) throws CanonicalGraphResourceException {
        String type = kind == CanonicalGraphInterfaceKind.FLOW ? output ? "terminate" : "flow_driven"
            : output ? "logic_output" : "logic_input";
        int matches = 0;
        for (CanonicalGraphNode node : story.getGraph()
            .getNodes()) {
            if (type.equals(node.getType())) {
                JsonElement property = node.getProperties()
                    .get("port_id");
                if (property != null && property.isJsonPrimitive()
                    && property.getAsJsonPrimitive()
                        .isString()
                    && portId.equals(property.getAsString())) matches++;
            }
            // flow_driven is a public projection of a Start trigger and is intentionally
            // not a second node/resource in the canonical Story graph.
            if (!"flow_driven".equals(type) || !"start".equals(node.getType())) continue;
            JsonElement triggers = node.getProperties()
                .get("triggers");
            if (triggers == null || !triggers.isJsonArray()) continue;
            for (JsonElement value : triggers.getAsJsonArray()) {
                if (!value.isJsonObject()) continue;
                JsonElement triggerPort = value.getAsJsonObject()
                    .get("port_id");
                JsonElement triggerType = value.getAsJsonObject()
                    .get("trigger_type");
                if (triggerPort != null && triggerType != null
                    && triggerPort.isJsonPrimitive()
                    && triggerType.isJsonPrimitive()
                    && portId.equals(triggerPort.getAsString())
                    && "flow_driven".equals(triggerType.getAsString())) matches++;
            }
        }
        if (matches == 1) return;
        throw CanonicalGraphResourceException.failure(
            "story.logic.graph." + (output ? "source" : "target") + ".port.missing",
            "Story logic " + (output ? "output" : "input") + " does not exist: " + portId);
    }

    private static void exact(JsonObject object, Set<String> allowed, String prefix)
        throws CanonicalGraphResourceException {
        for (Map.Entry<String, JsonElement> entry : object.entrySet())
            if (!allowed.contains(entry.getKey())) throw CanonicalGraphResourceException
                .failure(prefix + ".member.unsupported", "Unsupported field '" + entry.getKey() + "'.");
        for (String name : allowed) if (!object.has(name)) throw CanonicalGraphResourceException
            .failure(prefix + ".member.required", "Required field '" + name + "' is missing.");
    }

    private static String string(JsonObject object, String name, String prefix) throws CanonicalGraphResourceException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString())
            throw CanonicalGraphResourceException
                .failure(prefix + ".member.type", "Field '" + name + "' must be a string.");
        String result = value.getAsString();
        if (result.trim()
            .isEmpty())
            throw CanonicalGraphResourceException
                .failure(prefix + "." + name + ".required", "Field '" + name + "' cannot be blank.");
        return result;
    }

    private static int integer(JsonObject object, String name, String prefix) throws CanonicalGraphResourceException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isNumber())
            throw CanonicalGraphResourceException
                .failure(prefix + ".member.type", "Field '" + name + "' must be an integer.");
        try {
            return new BigDecimal(value.getAsString()).intValueExact();
        } catch (ArithmeticException exception) {
            throw CanonicalGraphResourceException
                .failure(prefix + ".member.type", "Field '" + name + "' must be an integer.");
        }
    }

    private static JsonElement required(JsonObject object, String name, String prefix)
        throws CanonicalGraphResourceException {
        if (!object.has(name)) throw CanonicalGraphResourceException
            .failure(prefix + ".member.required", "Required field '" + name + "' is missing.");
        JsonElement value = object.get(name);
        if (value == null || value.isJsonNull()) throw CanonicalGraphResourceException
            .failure(prefix + ".member.null", "Field '" + name + "' cannot be null.");
        return value;
    }

    private static JsonArray array(JsonObject object, String name, String prefix)
        throws CanonicalGraphResourceException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonArray()) throw CanonicalGraphResourceException
            .failure(prefix + ".member.type", "Field '" + name + "' must be an array.");
        return value.getAsJsonArray();
    }

    private static JsonObject requireObject(JsonElement value, String prefix) throws CanonicalGraphResourceException {
        if (value == null || value.isJsonNull())
            throw CanonicalGraphResourceException.failure(prefix + ".required", "Object cannot be null.");
        if (!value.isJsonObject())
            throw CanonicalGraphResourceException.failure(prefix + ".invalid", "Value must be an object.");
        return value.getAsJsonObject();
    }

    private static JsonElement readTree(JsonReader reader) throws IOException, CanonicalGraphResourceException {
        JsonToken token = reader.peek();
        if (token == JsonToken.BEGIN_OBJECT) {
            JsonObject result = new JsonObject();
            reader.beginObject();
            while (reader.hasNext()) {
                String name = reader.nextName();
                if (result.has(name)) throw CanonicalGraphResourceException
                    .failure("story.logic.graph.json.member.duplicate", "Duplicate JSON member '" + name + "'.");
                result.add(name, readTree(reader));
            }
            reader.endObject();
            return result;
        }
        if (token == JsonToken.BEGIN_ARRAY) {
            JsonArray result = new JsonArray();
            reader.beginArray();
            while (reader.hasNext()) result.add(readTree(reader));
            reader.endArray();
            return result;
        }
        if (token == JsonToken.STRING) return new JsonPrimitive(reader.nextString());
        if (token == JsonToken.BOOLEAN) return new JsonPrimitive(reader.nextBoolean());
        if (token == JsonToken.NULL) {
            reader.nextNull();
            return JsonNull.INSTANCE;
        }
        if (token == JsonToken.NUMBER) return new JsonParser().parse(reader.nextString());
        throw CanonicalGraphResourceException
            .failure("story.logic.graph.json.invalid", "Unexpected JSON token '" + token + "'.");
    }

    private static Set<String> set(String... names) {
        return Collections.unmodifiableSet(new HashSet<String>(Arrays.asList(names)));
    }
}
