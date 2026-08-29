package darkgrey.rpg.graph.canonical;

import java.io.File;
import java.io.IOException;
import java.io.StringReader;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
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

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;
import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

/** Strict, read-only loader for the schema-version-1 canonical graph roots. */
public final class CanonicalGraphResourceLoader {

    private static final Set<String> ROOT = set("schema_version", "resource_kind", "id", "display_name", "graph");
    private static final Set<String> GRAPH = set("nodes", "connections");
    private static final Set<String> NODE = set("id", "type", "display_name", "ports", "properties");
    private static final Set<String> PORT = set("port_id", "display_name", "direction", "kind", "order");
    private static final Set<String> CONNECTION = set(
        "from_node_id",
        "from_port_id",
        "to_node_id",
        "to_port_id",
        "interface_kind");

    public CanonicalGraphResource load(File file) throws CanonicalGraphResourceException {
        return load(file == null ? null : file.toPath());
    }

    public CanonicalGraphResource load(File file, CanonicalGraphResourceKind expectedKind)
        throws CanonicalGraphResourceException {
        return load(file == null ? null : file.toPath(), expectedKind);
    }

    public CanonicalGraphResource load(Path file) throws CanonicalGraphResourceException {
        return load(file, null);
    }

    public CanonicalGraphResource load(Path file, CanonicalGraphResourceKind expectedKind)
        throws CanonicalGraphResourceException {
        if (file == null) throw CanonicalGraphResourceException
            .failure("graph.resource.file.required", "Canonical graph resource file is required.");
        if (!Files.isRegularFile(file)) throw CanonicalGraphResourceException
            .failure("graph.resource.file.missing", "Canonical graph resource file does not exist: " + file);
        try {
            return parse(
                Files.readAllBytes(file),
                file.getFileName()
                    .toString(),
                expectedKind);
        } catch (IOException exception) {
            throw new CanonicalGraphResourceException(
                "graph.resource.file.read",
                "Could not read canonical graph resource '" + file + "'.",
                exception);
        }
    }

    public List<CanonicalGraphResource> loadDirectory(Path directory, CanonicalGraphResourceKind expectedKind)
        throws CanonicalGraphResourceException {
        if (directory == null) throw CanonicalGraphResourceException
            .failure("graph.resource.directory.required", "Canonical graph resource directory is required.");
        if (!Files.isDirectory(directory)) throw CanonicalGraphResourceException.failure(
            "graph.resource.directory.missing",
            "Canonical graph resource directory does not exist: " + directory);

        List<Path> files = new ArrayList<Path>();
        try (DirectoryStream<Path> stream = Files.newDirectoryStream(directory, "*.json")) {
            for (Path path : stream) if (Files.isRegularFile(path)) files.add(path);
        } catch (IOException exception) {
            throw new CanonicalGraphResourceException(
                "graph.resource.directory.read",
                "Could not list canonical graph resource directory '" + directory + "'.",
                exception);
        }
        Collections.sort(files, new Comparator<Path>() {

            @Override
            public int compare(Path left, Path right) {
                return left.getFileName()
                    .toString()
                    .compareTo(
                        right.getFileName()
                            .toString());
            }
        });
        Map<String, CanonicalGraphResource> byId = new LinkedHashMap<String, CanonicalGraphResource>();
        for (Path file : files) {
            CanonicalGraphResource resource = load(file, expectedKind);
            if (byId.put(resource.getId(), resource) != null) throw CanonicalGraphResourceException.failure(
                "graph.resource.id.duplicate",
                "Duplicate canonical graph resource id '" + resource.getId() + "'.");
        }
        return Collections.unmodifiableList(new ArrayList<CanonicalGraphResource>(byId.values()));
    }

    public List<CanonicalGraphResource> loadDirectory(File directory, CanonicalGraphResourceKind expectedKind)
        throws CanonicalGraphResourceException {
        return loadDirectory(directory == null ? null : directory.toPath(), expectedKind);
    }

    public List<CanonicalGraphResource> loadStories(Path directory) throws CanonicalGraphResourceException {
        return loadDirectory(directory, CanonicalGraphResourceKind.STORY);
    }

    public List<CanonicalGraphResource> loadSessions(Path directory) throws CanonicalGraphResourceException {
        return loadDirectory(directory, CanonicalGraphResourceKind.SESSION);
    }

    public List<CanonicalGraphResource> loadTasks(Path directory) throws CanonicalGraphResourceException {
        return loadDirectory(directory, CanonicalGraphResourceKind.TASK);
    }

    private static CanonicalGraphResource parse(byte[] bytes, String fileName, CanonicalGraphResourceKind expectedKind)
        throws CanonicalGraphResourceException {
        JsonObject root;
        try {
            JsonReader reader = new JsonReader(new StringReader(new String(bytes, StandardCharsets.UTF_8)));
            reader.setLenient(false);
            JsonElement element = readTree(reader);
            if (reader.peek() != JsonToken.END_DOCUMENT) throw CanonicalGraphResourceException
                .failure("graph.resource.json.invalid", "Trailing JSON content is not allowed.");
            if (!element.isJsonObject()) throw CanonicalGraphResourceException
                .failure("graph.resource.root.invalid", "Canonical graph resource root must be an object.");
            root = element.getAsJsonObject();
        } catch (CanonicalGraphResourceException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new CanonicalGraphResourceException(
                "graph.resource.json.invalid",
                "Canonical graph resource JSON is invalid.",
                exception);
        }
        exact(root, ROOT, "graph.resource.root");
        int version = integer(root, "schema_version", "graph.resource.root");
        if (version != 1) throw CanonicalGraphResourceException.failure(
            "graph.resource.schema_version.unsupported",
            "Unsupported canonical graph resource schema_version " + version + "; expected 1.");
        CanonicalGraphResourceKind kind = CanonicalGraphResourceKind
            .parse(string(root, "resource_kind", "graph.resource.root"));
        if (expectedKind != null && kind != expectedKind) requireKind(kind, expectedKind);
        String id = string(root, "id", "graph.resource.root");
        String displayName = string(root, "display_name", "graph.resource.root");
        if (id.trim()
            .isEmpty())
            throw CanonicalGraphResourceException
                .failure("graph.resource.id.required", "Canonical graph resource id cannot be blank.");
        if (displayName.trim()
            .isEmpty())
            throw CanonicalGraphResourceException.failure(
                "graph.resource.display_name.required",
                "Canonical graph resource display_name cannot be blank.");
        if (!fileName.equals(id + ".json")) throw CanonicalGraphResourceException.failure(
            "graph.resource.filename.mismatch",
            "Canonical graph resource filename must equal id + '.json': " + fileName);
        CanonicalGraph graph = graph(object(root, "graph", "graph.resource"), kind);
        return new CanonicalGraphResource(version, kind, id, displayName, graph);
    }

    private static CanonicalGraph graph(JsonObject value, CanonicalGraphResourceKind kind)
        throws CanonicalGraphResourceException {
        exact(value, GRAPH, "graph.resource.graph");
        JsonArray nodeValues = array(value, "nodes", "graph.resource.graph");
        JsonArray connectionValues = array(value, "connections", "graph.resource.graph");
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        Map<String, CanonicalGraphNode> byId = new LinkedHashMap<String, CanonicalGraphNode>();
        for (int index = 0; index < nodeValues.size(); index++) {
            CanonicalGraphNode node = node(nodeValues.get(index), kind);
            if (byId.put(node.getId(), node) != null) throw CanonicalGraphResourceException
                .failure("graph.node.id.duplicate", "Graph node ID '" + node.getId() + "' is duplicated.");
            nodes.add(node);
        }
        List<CanonicalGraphConnection> connections = new ArrayList<CanonicalGraphConnection>();
        Set<CanonicalGraphConnection> duplicateEdges = new HashSet<CanonicalGraphConnection>();
        Map<String, CanonicalGraphPort> portIndex = new HashMap<String, CanonicalGraphPort>();
        for (CanonicalGraphNode node : nodes) {
            for (CanonicalGraphPort port : node.getPorts()) portIndex.put(portKey(node.getId(), port.getId()), port);
        }
        Map<String, Integer> flowOutputs = new HashMap<String, Integer>();
        Map<String, Integer> logicInputs = new HashMap<String, Integer>();
        Map<String, List<String>> logicAdjacency = new HashMap<String, List<String>>();
        for (JsonElement element : connectionValues) {
            CanonicalGraphConnection connection = connection(element);
            if (!duplicateEdges.add(connection)) throw CanonicalGraphResourceException
                .failure("graph.connection.duplicate", "Duplicate graph edges are not allowed.");
            CanonicalGraphNode from = byId.get(connection.getFromNodeId());
            CanonicalGraphNode to = byId.get(connection.getToNodeId());
            if (from == null) throw CanonicalGraphResourceException.failure(
                "graph.connection.from.node.missing",
                "Connection source node does not exist: " + connection.getFromNodeId());
            if (to == null) throw CanonicalGraphResourceException.failure(
                "graph.connection.to.node.missing",
                "Connection target node does not exist: " + connection.getToNodeId());
            CanonicalGraphPort source = portIndex.get(portKey(from.getId(), connection.getFromPortId()));
            CanonicalGraphPort target = portIndex.get(portKey(to.getId(), connection.getToPortId()));
            if (source == null) throw CanonicalGraphResourceException.failure(
                "graph.connection.from.port.missing",
                "Connection source port does not exist: " + connection.getFromPortId());
            if (target == null) throw CanonicalGraphResourceException.failure(
                "graph.connection.to.port.missing",
                "Connection target port does not exist: " + connection.getToPortId());
            if (!source.isOutput()) throw CanonicalGraphResourceException
                .failure("graph.connection.source.direction", "Connection source port must be an output.");
            if (!target.isInput()) throw CanonicalGraphResourceException
                .failure("graph.connection.target.direction", "Connection target port must be an input.");
            if (source.getKind() != connection.getInterfaceKind()) throw CanonicalGraphResourceException.failure(
                "graph.connection.source.kind.mismatch",
                "Connection interface kind does not match source port.");
            if (target.getKind() != connection.getInterfaceKind()) throw CanonicalGraphResourceException.failure(
                "graph.connection.target.kind.mismatch",
                "Connection interface kind does not match target port.");
            if (kind == CanonicalGraphResourceKind.TASK
                && connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW)
                throw CanonicalGraphResourceException
                    .failure("graph.resource.task.flow.unsupported", "Task resources are logic-only.");
            if (connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) {
                String key = portKey(from.getId(), source.getId());
                if (flowOutputs.put(key, 1) != null) throw CanonicalGraphResourceException.failure(
                    "graph.connection.flow.output.multiple_targets",
                    "A flow output may have at most one target.");
            } else {
                String key = portKey(to.getId(), target.getId());
                if (logicInputs.put(key, 1) != null) throw CanonicalGraphResourceException.failure(
                    "graph.connection.logic.input.multiple_sources",
                    "A logic input may have at most one source.");
                List<String> outgoing = logicAdjacency.get(from.getId());
                if (outgoing == null) {
                    outgoing = new ArrayList<String>();
                    logicAdjacency.put(from.getId(), outgoing);
                }
                outgoing.add(to.getId());
            }
            connections.add(connection);
        }
        requireNoLogicCycles(logicAdjacency);
        requiredNodes(nodes, kind);
        return new CanonicalGraph(nodes, connections);
    }

    private static CanonicalGraphNode node(JsonElement value, CanonicalGraphResourceKind scope)
        throws CanonicalGraphResourceException {
        JsonObject object = requireObject(value, "graph.node");
        exact(object, NODE, "graph.node");
        String id = string(object, "id", "graph.node");
        String type = string(object, "type", "graph.node");
        String display = string(object, "display_name", "graph.node");
        if (id.trim()
            .isEmpty())
            throw CanonicalGraphResourceException.failure("graph.node.id.required", "Graph node ID is required.");
        if (type.trim()
            .isEmpty())
            throw CanonicalGraphResourceException.failure("graph.node.type.required", "Graph node type is required.");
        if (scope == CanonicalGraphResourceKind.TASK && "activate".equals(type))
            throw CanonicalGraphResourceException.failure(
                "graph.resource.task.legacy_activate",
                "Legacy Task activate node is not part of schema 1. Remove activate and let the parent Story Flow start the Task.");
        if (!allowedType(scope, type)) throw CanonicalGraphResourceException.failure(
            "graph.node.type.scope",
            "Node type '" + type + "' is not allowed in " + scope.getJsonName() + " scope.");
        JsonArray portValues = array(object, "ports", "graph.node");
        List<CanonicalGraphPort> ports = new ArrayList<CanonicalGraphPort>();
        Set<String> ids = new HashSet<String>();
        for (JsonElement portValue : portValues) {
            CanonicalGraphPort port = port(portValue);
            if (!ids.add(port.getId())) throw CanonicalGraphResourceException.failure(
                "graph.port.id.duplicate",
                "Port ID '" + port.getId() + "' is duplicated on node '" + id + "'.");
            if (scope == CanonicalGraphResourceKind.TASK && port.getKind() != CanonicalGraphInterfaceKind.LOGIC)
                throw CanonicalGraphResourceException
                    .failure("graph.resource.task.flow.unsupported", "Task resources are logic-only.");
            ports.add(port);
        }
        JsonObject propertyObject = object(object, "properties", "graph.node");
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : propertyObject.entrySet())
            properties.put(entry.getKey(), copy(entry.getValue()));
        return new CanonicalGraphNode(id, type, display, ports, properties);
    }

    private static CanonicalGraphPort port(JsonElement value) throws CanonicalGraphResourceException {
        JsonObject object = requireObject(value, "graph.port");
        exact(object, PORT, "graph.port");
        String id = string(object, "port_id", "graph.port");
        String display = string(object, "display_name", "graph.port");
        if (id.trim()
            .isEmpty())
            throw CanonicalGraphResourceException.failure("graph.port.id.required", "Graph port ID is required.");
        CanonicalGraphPortDirection direction = CanonicalGraphPortDirection
            .parse(string(object, "direction", "graph.port"));
        CanonicalGraphInterfaceKind kind = CanonicalGraphInterfaceKind.parse(string(object, "kind", "graph.port"));
        int order = integer(object, "order", "graph.port");
        if (order < 0) throw CanonicalGraphResourceException
            .failure("graph.port.order.invalid", "Graph port order cannot be negative.");
        return new CanonicalGraphPort(id, display, direction, kind, order);
    }

    private static CanonicalGraphConnection connection(JsonElement value) throws CanonicalGraphResourceException {
        JsonObject object = requireObject(value, "graph.connection");
        exact(object, CONNECTION, "graph.connection");
        return new CanonicalGraphConnection(
            string(object, "from_node_id", "graph.connection"),
            string(object, "from_port_id", "graph.connection"),
            string(object, "to_node_id", "graph.connection"),
            string(object, "to_port_id", "graph.connection"),
            CanonicalGraphInterfaceKind.parse(string(object, "interface_kind", "graph.connection")));
    }

    private static void requiredNodes(List<CanonicalGraphNode> nodes, CanonicalGraphResourceKind kind)
        throws CanonicalGraphResourceException {
        String required = kind == CanonicalGraphResourceKind.TASK ? null : "start";
        int count = 0;
        int settle = 0;
        for (CanonicalGraphNode node : nodes) {
            if (required != null && required.equals(node.getType())) count++;
            if ("settle".equals(node.getType())) settle++;
        }
        if (required != null && count != 1) throw CanonicalGraphResourceException
            .failure("graph.node.required.unique", "Scope requires exactly one '" + required + "' node.");
        if (kind == CanonicalGraphResourceKind.TASK && settle != 1) throw CanonicalGraphResourceException
            .failure("graph.node.required.unique", "Task scope requires exactly one 'settle' node.");
    }

    private static void requireNoLogicCycles(Map<String, List<String>> adjacency)
        throws CanonicalGraphResourceException {
        Map<String, Integer> states = new HashMap<String, Integer>();
        for (String node : adjacency.keySet()) if (visit(node, adjacency, states)) throw CanonicalGraphResourceException
            .failure("graph.logic.cycle", "Directed logic connections must be acyclic.");
    }

    private static boolean visit(String node, Map<String, List<String>> adjacency, Map<String, Integer> states) {
        Integer state = states.get(node);
        if (state != null) return state.intValue() == 1;
        states.put(node, 1);
        List<String> targets = adjacency.get(node);
        if (targets != null) for (String target : targets) if (visit(target, adjacency, states)) return true;
        states.put(node, 2);
        return false;
    }

    private static boolean allowedType(CanonicalGraphResourceKind scope, String type) {
        if (scope == CanonicalGraphResourceKind.STORY) return Arrays
            .asList("start", "terminate", "session", "task", "condition", "and", "or", "not", "action", "enter_story")
            .contains(type);
        if (scope == CanonicalGraphResourceKind.SESSION) return Arrays
            .asList("start", "line", "choice", "condition", "and", "or", "not", "logic_output", "end", "legacy_jump")
            .contains(type);
        // Task activation is owned by the parent Story Flow. A legacy Task
        // containing activate is rejected deterministically below by the
        // scope policy rather than becoming part of the new schema.
        return Arrays.asList("objective", "logic_input", "and", "or", "not", "logic_output", "settle")
            .contains(type);
    }

    private static void requireKind(CanonicalGraphResource resource, CanonicalGraphResourceKind expected)
        throws CanonicalGraphResourceException {
        requireKind(resource.getResourceKind(), expected);
    }

    private static void requireKind(CanonicalGraphResourceKind actual, CanonicalGraphResourceKind expected)
        throws CanonicalGraphResourceException {
        if (expected == null) return;
        if (actual != expected) throw CanonicalGraphResourceException.failure(
            "graph.resource.scope.mismatch",
            "Resource kind '" + actual.getJsonName() + "' does not match expected '" + expected.getJsonName() + "'.");
    }

    private static JsonElement readTree(JsonReader reader) throws IOException, CanonicalGraphResourceException {
        JsonToken token = reader.peek();
        if (token == JsonToken.BEGIN_OBJECT) {
            JsonObject result = new JsonObject();
            reader.beginObject();
            while (reader.hasNext()) {
                String name = reader.nextName();
                if (result.has(name)) throw CanonicalGraphResourceException
                    .failure("graph.json.member.duplicate", "Duplicate JSON member '" + name + "'.");
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
            .failure("graph.resource.json.invalid", "Unexpected JSON token '" + token + "'.");
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
        return value.getAsString();
    }

    private static int integer(JsonObject object, String name, String prefix) throws CanonicalGraphResourceException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isNumber())
            throw CanonicalGraphResourceException
                .failure(prefix + ".member.type", "Field '" + name + "' must be an integer.");
        try {
            BigDecimal number = new BigDecimal(value.getAsString());
            int result = number.intValueExact();
            return result;
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

    private static JsonObject object(JsonObject object, String name, String prefix)
        throws CanonicalGraphResourceException {
        return requireObject(required(object, name, prefix), prefix + "." + name);
    }

    private static JsonObject requireObject(JsonElement value, String prefix) throws CanonicalGraphResourceException {
        if (value == null || value.isJsonNull())
            throw CanonicalGraphResourceException.failure(prefix + ".required", "Object cannot be null.");
        if (!value.isJsonObject())
            throw CanonicalGraphResourceException.failure(prefix + ".invalid", "Value must be an object.");
        return value.getAsJsonObject();
    }

    private static Set<String> set(String... names) {
        return Collections.unmodifiableSet(new HashSet<String>(Arrays.asList(names)));
    }

    private static String portKey(String nodeId, String portId) {
        return nodeId.length() + ":" + nodeId + portId;
    }

    private static JsonElement copy(JsonElement element) {
        return new JsonParser().parse(element.toString());
    }
}
