package darkgrey.rpg.task.event;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.instance.CanonicalTaskInstance;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Deterministic inverted index over active Task objective subscriptions. */
public final class CanonicalTaskSubscriptionIndex {

    private final Map<CanonicalTaskSubscriptionKey, LinkedHashSet<CanonicalTaskInstanceIdentity>> byKey = new HashMap<CanonicalTaskSubscriptionKey, LinkedHashSet<CanonicalTaskInstanceIdentity>>();
    private final Map<CanonicalTaskInstanceIdentity, LinkedHashSet<CanonicalTaskSubscriptionKey>> byInstance = new HashMap<CanonicalTaskInstanceIdentity, LinkedHashSet<CanonicalTaskSubscriptionKey>>();

    public synchronized void clear() {
        byKey.clear();
        byInstance.clear();
    }

    public synchronized void rebuild(List<CanonicalTaskInstanceSnapshot> snapshots,
        Map<String, CanonicalGraphResource> resources) {
        if (snapshots == null || resources == null)
            throw new IllegalArgumentException("Index rebuild input is required.");
        clear();
        for (CanonicalTaskInstanceSnapshot snapshot : snapshots) {
            CanonicalGraphResource resource = resources.get(snapshot.getTaskResourceId());
            if (resource == null)
                throw new IllegalArgumentException("Missing indexed Task resource: " + snapshot.getTaskResourceId());
            index(snapshot, resource);
        }
    }

    public synchronized void rebuild(List<CanonicalTaskInstanceSnapshot> snapshots,
        CanonicalTaskResourceResolver resolver) {
        if (resolver == null) throw new IllegalArgumentException("Task resource resolver is required.");
        Map<String, CanonicalGraphResource> resources = new HashMap<String, CanonicalGraphResource>();
        for (CanonicalTaskInstanceSnapshot snapshot : snapshots) {
            if (!resources.containsKey(snapshot.getTaskResourceId())) {
                CanonicalGraphResource resource = resolver.resolve(snapshot.getTaskResourceId());
                if (resource == null) throw new IllegalArgumentException(
                    "Missing indexed Task resource: " + snapshot.getTaskResourceId());
                resources.put(snapshot.getTaskResourceId(), resource);
            }
        }
        rebuild(snapshots, resources);
    }

    public synchronized void index(CanonicalTaskInstance instance) {
        if (instance == null) throw new IllegalArgumentException("Task instance is required.");
        reindex(
            instance.snapshot(),
            instance.getRuntime()
                .getResource());
    }

    public synchronized void reindex(CanonicalTaskInstanceSnapshot snapshot, CanonicalGraphResource resource) {
        remove(
            new CanonicalTaskInstanceIdentity(
                snapshot.getPlayerUuid(),
                snapshot.getStoryInstanceId(),
                snapshot.getTaskNodePlacementId()));
        index(snapshot, resource);
    }

    public synchronized void remove(CanonicalTaskInstanceIdentity identity) {
        Set<CanonicalTaskSubscriptionKey> keys = byInstance.remove(identity);
        if (keys == null) return;
        for (CanonicalTaskSubscriptionKey key : keys) {
            Set<CanonicalTaskInstanceIdentity> values = byKey.get(key);
            if (values != null) {
                values.remove(identity);
                if (values.isEmpty()) byKey.remove(key);
            }
        }
    }

    public synchronized List<CanonicalTaskInstanceIdentity> query(UUID playerUuid, CanonicalTaskEvent event) {
        if (playerUuid == null || event == null) return Collections.emptyList();
        String target = primaryTarget(event);
        if (target == null) return Collections.emptyList();
        Set<CanonicalTaskInstanceIdentity> values = byKey
            .get(new CanonicalTaskSubscriptionKey(playerUuid, event.getType(), target));
        if (values == null || values.isEmpty()) return Collections.emptyList();
        List<CanonicalTaskInstanceIdentity> result = new ArrayList<CanonicalTaskInstanceIdentity>(values);
        Collections.sort(result);
        return Collections.unmodifiableList(result);
    }

    public synchronized List<CanonicalTaskInstanceIdentity> queryCandidates(UUID playerUuid, CanonicalTaskEvent event) {
        return query(playerUuid, event);
    }

    public synchronized int candidateCount(UUID playerUuid, CanonicalTaskEvent event) {
        return query(playerUuid, event).size();
    }

    public synchronized int keyCount() {
        return byKey.size();
    }

    public synchronized int subscriptionCount() {
        int count = 0;
        for (Set<CanonicalTaskInstanceIdentity> values : byKey.values()) count += values.size();
        return count;
    }

    public synchronized int getSubscriptionCount() {
        return subscriptionCount();
    }

    public synchronized int getKeyCount() {
        return keyCount();
    }

    public synchronized int size() {
        return subscriptionCount();
    }

    private void index(CanonicalTaskInstanceSnapshot snapshot, CanonicalGraphResource resource) {
        if (snapshot == null || resource == null
            || snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) return;
        CanonicalTaskInstanceIdentity identity = new CanonicalTaskInstanceIdentity(
            snapshot.getPlayerUuid(),
            snapshot.getStoryInstanceId(),
            snapshot.getTaskNodePlacementId());
        Map<String, CanonicalTaskObjectiveStatus> statuses = snapshot.getRuntimeSnapshot()
            .getObjectiveStatuses();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (!"objective".equals(node.getType())
                || statuses.get(node.getId()) != CanonicalTaskObjectiveStatus.ACTIVE) continue;
            CanonicalTaskSubscriptionKey key = objectiveKey(snapshot.getPlayerUuid(), node);
            if (key == null) continue;
            LinkedHashSet<CanonicalTaskInstanceIdentity> values = byKey.get(key);
            if (values == null) {
                values = new LinkedHashSet<CanonicalTaskInstanceIdentity>();
                byKey.put(key, values);
            }
            values.add(identity);
            LinkedHashSet<CanonicalTaskSubscriptionKey> keys = byInstance.get(identity);
            if (keys == null) {
                keys = new LinkedHashSet<CanonicalTaskSubscriptionKey>();
                byInstance.put(identity, keys);
            }
            keys.add(key);
        }
    }

    private static CanonicalTaskSubscriptionKey objectiveKey(UUID player, CanonicalGraphNode node) {
        String type = string(node, "objective_type");
        String target = null;
        if (CanonicalTaskEvent.KILL_ENTITY.equals(type)) target = string(node, "entity");
        else if (CanonicalTaskEvent.COLLECT_ITEM.equals(type)) target = string(node, "item");
        else if (CanonicalTaskEvent.INTERACT_ACTOR.equals(type)) target = string(node, "actor_id");
        return target == null ? null : new CanonicalTaskSubscriptionKey(player, type, target);
    }

    private static String primaryTarget(CanonicalTaskEvent event) {
        if (CanonicalTaskEvent.KILL_ENTITY.equals(event.getType())) return event.get("entity");
        if (CanonicalTaskEvent.COLLECT_ITEM.equals(event.getType())) return event.get("item");
        if (CanonicalTaskEvent.INTERACT_ACTOR.equals(event.getType())) return event.get("actor_id");
        return null;
    }

    private static String string(CanonicalGraphNode node, String key) {
        JsonElement value = node.getProperties()
            .get(key);
        return value != null && value.isJsonPrimitive()
            && value.getAsJsonPrimitive()
                .isString()
            && value.getAsString() != null
            && !value.getAsString()
                .trim()
                .isEmpty() ? value.getAsString() : null;
    }
}
