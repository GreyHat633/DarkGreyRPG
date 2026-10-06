package darkgrey.rpg.task.runtime;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/** One targeted, pure-Java event delivered to a Task runtime. */
public final class CanonicalTaskEvent {

    public static final String KILL_ENTITY = "kill_entity";
    public static final String COLLECT_ITEM = "collect_item";
    public static final String INTERACT_ACTOR = "interact_actor";
    public static final String SUBMIT_ITEM = "submit_item";
    public static final String REACH_REGION = "reach_region";

    private final String type;
    private final Map<String, String> values;
    private final int amount;

    public CanonicalTaskEvent(String type, Map<String, String> values, int amount) {
        if (type == null || type.trim()
            .isEmpty()) throw new IllegalArgumentException("Event type is required.");
        if (amount < 0 || (amount == 0 && !COLLECT_ITEM.equals(type)))
            throw new IllegalArgumentException("Event amount is invalid.");
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

    /** One physical click, including all identities bound to the same entity. */
    public static CanonicalTaskEvent interactActors(java.util.Collection<String> actorIds) {
        if (actorIds == null || actorIds.isEmpty()) throw new IllegalArgumentException("Actor identities required.");
        Map<String, String> values = new LinkedHashMap<String, String>();
        StringBuilder joined = new StringBuilder();
        for (String id : actorIds) {
            if (id == null || id.indexOf('\u0000') >= 0) throw new IllegalArgumentException("Invalid actor identity.");
            if (!values.containsKey("actor_id")) values.put("actor_id", id);
            if (joined.length() > 0) joined.append('\u0000');
            joined.append(id);
        }
        values.put("actor_ids", joined.toString());
        return new CanonicalTaskEvent(INTERACT_ACTOR, values, 1);
    }

    public boolean targetsActor(String actorId) {
        if (actorId.equals(get("actor_id"))) return true;
        String ids = get("actor_ids");
        if (ids != null) for (String id : ids.split("\u0000", -1)) if (actorId.equals(id)) return true;
        return false;
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
