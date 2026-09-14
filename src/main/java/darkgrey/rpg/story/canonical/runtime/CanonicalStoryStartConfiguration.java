package darkgrey.rpg.story.canonical.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Strict runtime projection of Story Start trigger metadata authored by Studio. */
public final class CanonicalStoryStartConfiguration {

    public static final String ENTER_STORY = "enter_story";
    /** Public Story boundary trigger. Unlike the legacy enter_story trigger this is selected by its stable port ID. */
    public static final String FLOW_DRIVEN = "flow_driven";
    public static final String INTERACT_ACTOR = "interact_actor";
    public static final String ENTER_REGION = "enter_region";
    public static final String LOGIC = "logic";

    private final CanonicalStoryRepeatPolicy repeatPolicy;
    private final List<Trigger> triggers;

    private CanonicalStoryStartConfiguration(CanonicalStoryRepeatPolicy repeatPolicy, List<Trigger> triggers) {
        this.repeatPolicy = repeatPolicy;
        this.triggers = Collections.unmodifiableList(new ArrayList<Trigger>(triggers));
    }

    public static CanonicalStoryStartConfiguration parse(CanonicalGraphResource resource) {
        if (resource == null || resource.getGraph() == null)
            throw failure("story.start.resource", "Story is required.");
        CanonicalGraphNode start = null;
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) if (node != null && "start".equals(node.getType())) {
                if (start != null) throw failure("story.start.unique", "Story requires exactly one Start node.");
                start = node;
            }
        if (start == null) throw failure("story.start.required", "Story Start node is missing.");
        Map<String, JsonElement> properties = start.getProperties();
        requireExactKeys(properties.keySet(), set("repeat_policy", "triggers"), "Story Start properties");
        JsonElement repeat = properties.get("repeat_policy");
        if (repeat == null || !repeat.isJsonPrimitive()
            || !repeat.getAsJsonPrimitive()
                .isString())
            throw failure("story.start.repeat_policy", "Story repeat_policy must be a string.");
        CanonicalStoryRepeatPolicy repeatPolicy;
        try {
            repeatPolicy = CanonicalStoryRepeatPolicy.fromJsonName(repeat.getAsString());
        } catch (RuntimeException exception) {
            throw failure("story.start.repeat_policy", "Unsupported Story repeat_policy.");
        }
        JsonElement triggerValue = properties.get("triggers");
        if (triggerValue == null || !triggerValue.isJsonArray())
            throw failure("story.start.triggers", "Story Start triggers must be an array.");
        JsonArray array = triggerValue.getAsJsonArray();
        if (array.size() == 0) throw failure("story.start.triggers", "Story Start requires at least one trigger.");

        List<Trigger> triggers = new ArrayList<Trigger>();
        Set<String> portIds = new HashSet<String>();
        Set<Integer> orders = new HashSet<Integer>();
        for (JsonElement element : array) {
            if (element == null || !element.isJsonObject())
                throw failure("story.start.trigger", "Each Story Start trigger must be an object.");
            JsonObject object = element.getAsJsonObject();
            Set<String> triggerKeys = set("port_id", "display_name", "trigger_type", "trigger_properties", "order");
            Set<String> actualTriggerKeys = new HashSet<String>();
            for (Map.Entry<String, JsonElement> entry : object.entrySet()) actualTriggerKeys.add(entry.getKey());
            if (!actualTriggerKeys.equals(triggerKeys) && !(actualTriggerKeys.equals(
                set("port_id", "display_name", "trigger_type", "trigger_properties", "order", "logic_port_id"))))
                throw failure("story.start.schema", "Story Start trigger contain unknown or missing fields.");
            String portId = string(object, "port_id");
            String displayName = string(object, "display_name");
            String type = string(object, "trigger_type");
            int order = integer(object, "order");
            if (order < 0 || !orders.add(Integer.valueOf(order)))
                throw failure("story.start.trigger.order", "Story Start trigger order is invalid or duplicated.");
            if (!portIds.add(portId))
                throw failure("story.start.trigger.port", "Story Start trigger port_id is duplicated.");
            JsonElement triggerProperties = object.get("trigger_properties");
            if (triggerProperties == null || !triggerProperties.isJsonObject())
                throw failure("story.start.trigger.properties", "trigger_properties must be an object.");
            String logicPortId = null;
            if (object.has("logic_port_id")) logicPortId = string(object, "logic_port_id");
            triggers
                .add(parseTrigger(portId, displayName, type, order, triggerProperties.getAsJsonObject(), logicPortId));
        }
        Collections.sort(triggers);
        for (int index = 0; index < triggers.size(); index++) {
            Trigger trigger = triggers.get(index);
            if (trigger.getOrder() != index)
                throw failure("story.start.trigger.order", "Story Start trigger order must be contiguous from zero.");
            validatePort(start, trigger);
        }
        if (flowOutputCount(start) != triggers.size())
            throw failure("story.start.trigger.port", "Story Start requires exactly one Flow output per trigger.");
        return new CanonicalStoryStartConfiguration(repeatPolicy, triggers);
    }

    public CanonicalStoryRepeatPolicy getRepeatPolicy() {
        return repeatPolicy;
    }

    public List<Trigger> getTriggers() {
        return triggers;
    }

    public Trigger selectEnterStory() {
        return select(new Matcher() {

            @Override
            public boolean matches(Trigger trigger) {
                return ENTER_STORY.equals(trigger.getType());
            }
        }, "enter_story");
    }

    public Trigger selectFlowDriven(String portId) {
        if (blank(portId)) throw new IllegalArgumentException("Flow-driven Story port ID is required.");
        for (Trigger trigger : triggers)
            if (FLOW_DRIVEN.equals(trigger.getType()) && portId.equals(trigger.getPortId())) return trigger;
        throw failure("story.start.trigger.missing", "No Story flow_driven trigger matches " + portId + ".");
    }

    public Trigger selectActor(final String actorId) {
        Trigger result = findActor(actorId);
        if (result == null)
            throw failure("story.start.trigger.missing", "No Story trigger matches interact_actor:" + actorId + ".");
        return result;
    }

    public Trigger findActor(final String actorId) {
        if (blank(actorId)) throw new IllegalArgumentException("Actor ID is required.");
        return find(new Matcher() {

            @Override
            public boolean matches(Trigger trigger) {
                return INTERACT_ACTOR.equals(trigger.getType()) && actorId.equals(trigger.getString("actor_id"));
            }
        }, "interact_actor:" + actorId);
    }

    public Trigger selectRegion(final int dimension, final double x, final double y, final double z) {
        Trigger result = findRegion(dimension, x, y, z);
        if (result == null) throw failure("story.start.trigger.missing", "No Story trigger matches enter_region.");
        return result;
    }

    public Trigger findRegion(final int dimension, final double x, final double y, final double z) {
        return find(new Matcher() {

            @Override
            public boolean matches(Trigger trigger) {
                if (!ENTER_REGION.equals(trigger.getType()) || trigger.getInt("dimension") != dimension) return false;
                double dx = x - trigger.getDouble("x");
                double dy = y - trigger.getDouble("y");
                double dz = z - trigger.getDouble("z");
                double radius = trigger.getDouble("radius");
                return dx * dx + dy * dy + dz * dz <= radius * radius;
            }
        }, "enter_region");
    }

    public List<Trigger> getLogicTriggers() {
        List<Trigger> result = new ArrayList<Trigger>();
        for (Trigger trigger : triggers) if (LOGIC.equals(trigger.getType())) result.add(trigger);
        return Collections.unmodifiableList(result);
    }

    private Trigger find(Matcher matcher, String label) {
        Trigger result = null;
        for (Trigger trigger : triggers) if (matcher.matches(trigger)) {
            if (result != null)
                throw failure("story.start.trigger.ambiguous", "Multiple Story triggers match " + label + ".");
            result = trigger;
        }
        return result;
    }

    private Trigger select(Matcher matcher, String label) {
        Trigger result = find(matcher, label);
        if (result == null) throw failure("story.start.trigger.missing", "No Story trigger matches " + label + ".");
        return result;
    }

    private static Trigger parseTrigger(String portId, String displayName, String type, int order,
        JsonObject properties, String logicPortId) {
        Map<String, JsonElement> values = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : properties.entrySet()) values.put(entry.getKey(), entry.getValue());
        if (ENTER_STORY.equals(type) || FLOW_DRIVEN.equals(type)) {
            requireExactKeys(values.keySet(), Collections.<String>emptySet(), "enter_story properties");
        } else if (INTERACT_ACTOR.equals(type)) {
            requireExactKeys(values.keySet(), set("actor_id"), "interact_actor properties");
            string(properties, "actor_id");
        } else if (ENTER_REGION.equals(type)) {
            requireExactKeys(values.keySet(), set("dimension", "x", "y", "z", "radius"), "enter_region properties");
            integer(properties, "dimension");
            number(properties, "x");
            number(properties, "y");
            number(properties, "z");
            if (number(properties, "radius") <= 0D)
                throw failure("story.start.trigger.properties", "Region radius must be positive.");
        } else if (LOGIC.equals(type)) {
            requireExactKeys(values.keySet(), Collections.<String>emptySet(), "logic properties");
            if (blank(logicPortId))
                throw failure("story.start.trigger.condition", "Logic Story Start requires logic_port_id.");
        } else throw failure("story.start.trigger.type", "Unsupported Story Start trigger type: " + type);
        return new Trigger(portId, displayName, type, order, values, logicPortId);
    }

    private static void validatePort(CanonicalGraphNode start, Trigger trigger) {
        CanonicalGraphPort found = null;
        for (CanonicalGraphPort port : start.getPorts()) if (port != null && trigger.getPortId()
            .equals(port.getId())) {
                if (found != null) throw failure("story.start.trigger.port", "Story Start trigger port is duplicated.");
                found = port;
            }
        if (found == null || found.getDirection() != CanonicalGraphPortDirection.OUTPUT
            || found.getInterfaceKind() != CanonicalGraphInterfaceKind.FLOW
            || found.getOrder() != trigger.getOrder()
            || !trigger.getDisplayName()
                .equals(found.getDisplayName()))
            throw failure("story.start.trigger.port", "Story Start trigger metadata does not match its Flow port.");
        if (trigger.getLogicPortId() != null) {
            CanonicalGraphPort logic = null;
            for (CanonicalGraphPort port : start.getPorts()) if (port != null && trigger.getLogicPortId()
                .equals(port.getId())) {
                    if (logic != null)
                        throw failure("story.start.trigger.port", "Story Start trigger Logic port is duplicated.");
                    logic = port;
                }
            if (logic == null || logic.getDirection() != CanonicalGraphPortDirection.INPUT
                || logic.getInterfaceKind() != CanonicalGraphInterfaceKind.LOGIC)
                throw failure("story.start.trigger.port", "Story Start trigger Logic condition port is invalid.");
        }
    }

    private static int flowOutputCount(CanonicalGraphNode start) {
        int result = 0;
        for (CanonicalGraphPort port : start.getPorts())
            if (port != null && port.getDirection() == CanonicalGraphPortDirection.OUTPUT
                && port.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) result++;
        return result;
    }

    private static String string(JsonObject object, String key) {
        JsonElement value = object.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || blank(value.getAsString()))
            throw failure("story.start.trigger.properties", "Nonblank string property is required: " + key);
        return value.getAsString();
    }

    private static int integer(JsonObject object, String key) {
        JsonElement value = object.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure("story.start.trigger.properties", "Integer property is required: " + key);
        double number = value.getAsDouble();
        int integer = value.getAsInt();
        if (Double.isNaN(number) || Double.isInfinite(number) || number != integer)
            throw failure("story.start.trigger.properties", "Integer property is required: " + key);
        return integer;
    }

    private static double number(JsonObject object, String key) {
        JsonElement value = object.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure("story.start.trigger.properties", "Numeric property is required: " + key);
        double result = value.getAsDouble();
        if (Double.isNaN(result) || Double.isInfinite(result))
            throw failure("story.start.trigger.properties", "Finite numeric property is required: " + key);
        return result;
    }

    private static void requireExactKeys(Iterable<?> actualEntries, Set<String> expected, String label) {
        Set<String> actual = new HashSet<String>();
        for (Object item : actualEntries) {
            if (item instanceof String) actual.add((String) item);
            else if (item instanceof Map.Entry<?, ?>) actual.add(String.valueOf(((Map.Entry<?, ?>) item).getKey()));
        }
        if (!actual.equals(expected))
            throw failure("story.start.schema", label + " contain unknown or missing fields.");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        Collections.addAll(result, values);
        return result;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }

    private interface Matcher {

        boolean matches(Trigger trigger);
    }

    public static final class Trigger implements Comparable<Trigger> {

        private final String portId;
        private final String displayName;
        private final String type;
        private final int order;
        private final Map<String, JsonElement> properties;
        private final String logicPortId;

        Trigger(String portId, String displayName, String type, int order, Map<String, JsonElement> properties,
            String logicPortId) {
            this.portId = portId;
            this.displayName = displayName;
            this.type = type;
            this.order = order;
            this.properties = Collections.unmodifiableMap(new LinkedHashMap<String, JsonElement>(properties));
            this.logicPortId = logicPortId;
        }

        public String getPortId() {
            return portId;
        }

        public String getDisplayName() {
            return displayName;
        }

        public String getType() {
            return type;
        }

        public int getOrder() {
            return order;
        }

        public String getString(String key) {
            return properties.get(key)
                .getAsString();
        }

        public int getInt(String key) {
            return properties.get(key)
                .getAsInt();
        }

        public double getDouble(String key) {
            return properties.get(key)
                .getAsDouble();
        }

        public String getLogicPortId() {
            return logicPortId;
        }

        public String getConditionPortId() {
            return logicPortId;
        }

        @Override
        public int compareTo(Trigger other) {
            int result = Integer.compare(order, other.order);
            return result == 0 ? portId.compareTo(other.portId) : result;
        }
    }
}
