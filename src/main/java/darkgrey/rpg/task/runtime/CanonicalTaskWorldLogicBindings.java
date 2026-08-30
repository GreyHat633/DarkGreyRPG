package darkgrey.rpg.task.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Formal projection of Task Logic Inputs that are supplied by Minecraft world state. */
public final class CanonicalTaskWorldLogicBindings {

    public static final String DAY = "minecraft:day";
    public static final String NIGHT = "minecraft:night";

    private CanonicalTaskWorldLogicBindings() {}

    public static List<Binding> parse(CanonicalGraphResource resource) {
        if (resource == null || resource.getGraph() == null)
            throw new IllegalArgumentException("Canonical Task resource is required.");
        List<Binding> result = new ArrayList<Binding>();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (node == null || !"logic_input".equals(node.getType())) continue;
            Map<String, JsonElement> properties = node.getProperties();
            JsonElement source = properties.get("source");
            if (source == null) continue;
            if (!source.isJsonPrimitive() || !source.getAsJsonPrimitive()
                .isString()) throw failure("task.logic_input.source", "Task Logic Input source must be a string.");
            String value = source.getAsString();
            if (!DAY.equals(value) && !NIGHT.equals(value))
                throw failure("task.logic_input.source", "Unsupported Task Logic Input source: " + value);
            JsonElement port = properties.get("port_id");
            if (port == null || !port.isJsonPrimitive()
                || !port.getAsJsonPrimitive()
                    .isString()
                || blank(port.getAsString()))
                throw failure("task.logic_input.source", "World-bound Task Logic Input requires port_id.");
            result.add(new Binding(port.getAsString(), value));
        }
        return Collections.unmodifiableList(result);
    }

    public static boolean value(String source, long worldTime) {
        long timeOfDay = worldTime % 24000L;
        if (timeOfDay < 0L) timeOfDay += 24000L;
        boolean night = timeOfDay >= 13000L && timeOfDay < 23000L;
        if (NIGHT.equals(source)) return night;
        if (DAY.equals(source)) return !night;
        throw new IllegalArgumentException("Unsupported Task world Logic source: " + source);
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    public static final class Binding {

        private final String portId;
        private final String source;

        private Binding(String portId, String source) {
            this.portId = portId;
            this.source = source;
        }

        public String getPortId() {
            return portId;
        }

        public String getSource() {
            return source;
        }
    }
}
