package darkgrey.rpg.task.runtime;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/** One targeted, pure-Java event delivered to a Task runtime. */
public final class CanonicalTaskEvent {

    public static final String KILL_ENTITY = "kill_entity";
    public static final String COLLECT_ITEM = "collect_item";
    public static final String INTERACT_ACTOR = "interact_actor";

    private final String type;
    private final Map<String, String> values;
    private final int amount;

    public CanonicalTaskEvent(String type, Map<String, String> values, int amount) {
        if (type == null || type.trim()
            .isEmpty()) throw new IllegalArgumentException("Event type is required.");
        if (amount <= 0) throw new IllegalArgumentException("Event amount must be positive.");
        this.type = type;
        this.values = Collections.unmodifiableMap(
            new LinkedHashMap<String, String>(values == null ? Collections.<String, String>emptyMap() : values));
        this.amount = amount;
    }

    public CanonicalTaskEvent(String type, String key, String value) {
        this(type, singleton(key, value), 1);
    }

    public static CanonicalTaskEvent killEntity(String entity) {
        return new CanonicalTaskEvent(KILL_ENTITY, "entity", entity);
    }

    public static CanonicalTaskEvent kill(String entity) {
        return killEntity(entity);
    }

    public static CanonicalTaskEvent killEntity(String entity, int amount) {
        return new CanonicalTaskEvent(KILL_ENTITY, singleton("entity", entity), amount);
    }

    public static CanonicalTaskEvent collectItem(String item, int amount) {
        return new CanonicalTaskEvent(COLLECT_ITEM, singleton("item", item), amount);
    }

    public static CanonicalTaskEvent collect(String item, int amount) {
        return collectItem(item, amount);
    }

    public static CanonicalTaskEvent collectItem(String item, Map<String, String> metadata, int amount) {
        Map<String, String> values = new LinkedHashMap<String, String>();
        values.put("item", item);
        if (metadata != null) values.putAll(metadata);
        return new CanonicalTaskEvent(COLLECT_ITEM, values, amount);
    }

    public static CanonicalTaskEvent interactActor(String actorId) {
        return new CanonicalTaskEvent(INTERACT_ACTOR, "actor_id", actorId);
    }

    public static CanonicalTaskEvent interact(String actorId) {
        return interactActor(actorId);
    }

    public String getType() {
        return type;
    }

    public Map<String, String> getValues() {
        return values;
    }

    public String get(String key) {
        return values.get(key);
    }

    public int getAmount() {
        return amount;
    }

    private static Map<String, String> singleton(String key, String value) {
        Map<String, String> result = new LinkedHashMap<String, String>();
        result.put(key, value);
        return result;
    }
}
