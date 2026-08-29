package darkgrey.rpg.graph.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

/** Immutable canonical node with detached arbitrary JSON properties. */
public final class CanonicalGraphNode {

    private final String id;
    private final String type;
    private final String displayName;
    private final List<CanonicalGraphPort> ports;
    private final Map<String, JsonElement> properties;

    public CanonicalGraphNode(String id, String type, String displayName, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        this.id = id;
        this.type = type;
        this.displayName = displayName;
        this.ports = Collections.unmodifiableList(new ArrayList<CanonicalGraphPort>(ports));
        Map<String, JsonElement> detached = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : properties.entrySet()) {
            detached.put(entry.getKey(), copy(entry.getValue()));
        }
        this.properties = Collections.unmodifiableMap(detached);
    }

    public String getId() {
        return id;
    }

    public String getNodeId() {
        return id;
    }

    public String getType() {
        return type;
    }

    public String getNodeType() {
        return type;
    }

    public String getDisplayName() {
        return displayName;
    }

    public List<CanonicalGraphPort> getPorts() {
        return ports;
    }

    /** Returns a detached, unmodifiable property map on every call. */
    public Map<String, JsonElement> getProperties() {
        Map<String, JsonElement> copy = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : properties.entrySet()) {
            copy.put(entry.getKey(), copy(entry.getValue()));
        }
        return Collections.unmodifiableMap(copy);
    }

    private static JsonElement copy(JsonElement element) {
        return new JsonParser().parse(element.toString());
    }
}
