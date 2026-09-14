package darkgrey.rpg.task.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.function.LongSupplier;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Synchronized owner of unique Task instances, independent of Forge event plumbing. */
public final class CanonicalTaskInstanceStore {

    private final Map<Key, CanonicalTaskInstance> instances = new LinkedHashMap<Key, CanonicalTaskInstance>();
    private final LongSupplier clock;

    public CanonicalTaskInstanceStore() {
        this(new LongSupplier() {

            @Override
            public long getAsLong() {
                return System.currentTimeMillis();
            }
        });
    }

    public CanonicalTaskInstanceStore(LongSupplier clock) {
        if (clock == null) throw new IllegalArgumentException("Task clock is required.");
        this.clock = clock;
    }

    public synchronized CanonicalTaskInstance start(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalGraphResource resource) {
        return start(playerUuid, storyInstanceId, taskNodePlacementId, resource, now());
    }

    public synchronized CanonicalTaskInstance start(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalGraphResource resource, long activationTime) {
        validateIdentity(playerUuid, storyInstanceId, taskNodePlacementId);
        if (resource == null) throw new IllegalArgumentException("Task resource is required.");
        Key key = new Key(playerUuid, storyInstanceId, taskNodePlacementId);
        CanonicalTaskInstance existing = instances.get(key);
        if (existing != null) {
            if (!resource.getId()
                .equals(existing.getTaskResourceId()))
                throw new IllegalStateException("Task placement already uses another resource.");
            return existing;
        }
        CanonicalTaskInstance created = CanonicalTaskInstance
            .start(playerUuid, storyInstanceId, taskNodePlacementId, resource, activationTime);
        instances.put(key, created);
        return created;
    }

    public synchronized CanonicalTaskInstance begin(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalGraphResource resource) {
        return start(playerUuid, storyInstanceId, taskNodePlacementId, resource);
    }

    public synchronized CanonicalTaskInstance start(String playerUuid, String storyInstanceId,
        String taskNodePlacementId, CanonicalGraphResource resource) {
        return start(parseUuid(playerUuid), storyInstanceId, taskNodePlacementId, resource);
    }

    public synchronized CanonicalTaskInstance get(UUID playerUuid, String storyInstanceId, String taskNodePlacementId) {
        if (playerUuid == null || blank(storyInstanceId) || blank(taskNodePlacementId)) return null;
        return instances.get(new Key(playerUuid, storyInstanceId, taskNodePlacementId));
    }

    /** Replaces only an existing identity after its staged snapshot has been validated. */
    public synchronized void replaceExisting(CanonicalTaskInstance instance) {
        if (instance == null) throw new IllegalArgumentException("Task instance is required.");
        Key key = new Key(instance.getPlayerUuid(), instance.getStoryInstanceId(), instance.getTaskNodePlacementId());
        if (!instances.containsKey(key)) throw new IllegalStateException("Task instance is missing.");
        instances.put(key, instance);
    }

    public synchronized CanonicalTaskInstance getInstance(UUID playerUuid, String storyInstanceId,
        String taskNodePlacementId) {
        return get(playerUuid, storyInstanceId, taskNodePlacementId);
    }

    public synchronized CanonicalTaskInstance get(String playerUuid, String storyInstanceId,
        String taskNodePlacementId) {
        return get(parseUuid(playerUuid), storyInstanceId, taskNodePlacementId);
    }

    public synchronized int size() {
        return instances.size();
    }

    /** Permanently discards active and terminal Task instances owned by the selected Stories. */
    public synchronized int discardByStoryIds(Set<String> storyIds) {
        if (storyIds == null) throw new IllegalArgumentException("Story IDs are required.");
        int removed = 0;
        java.util.Iterator<Map.Entry<Key, CanonicalTaskInstance>> iterator = instances.entrySet()
            .iterator();
        while (iterator.hasNext()) if (storyIds.contains(
            iterator.next()
                .getKey().story)) {
                    iterator.remove();
                    removed++;
                }
        return removed;
    }

    /** Permanently discards every Task instance for one exact player/Story identity. */
    public synchronized int discardByPlayerStory(UUID playerUuid, String storyInstanceId) {
        if (playerUuid == null || blank(storyInstanceId))
            throw new IllegalArgumentException("Player and Story identity are required.");
        int removed = 0;
        java.util.Iterator<Map.Entry<Key, CanonicalTaskInstance>> iterator = instances.entrySet()
            .iterator();
        while (iterator.hasNext()) {
            Key key = iterator.next()
                .getKey();
            if (playerUuid.equals(key.player) && storyInstanceId.equals(key.story)) {
                iterator.remove();
                removed++;
            }
        }
        return removed;
    }

    public synchronized boolean acceptEvent(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalTaskEvent event) {
        return acceptEvent(playerUuid, storyInstanceId, taskNodePlacementId, event, now());
    }

    public synchronized boolean acceptEvent(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalTaskEvent event, long eventTime) {
        CanonicalTaskInstance instance = getRequired(playerUuid, storyInstanceId, taskNodePlacementId);
        return instance.accept(event, eventTime);
    }

    public synchronized boolean accept(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalTaskEvent event) {
        return acceptEvent(playerUuid, storyInstanceId, taskNodePlacementId, event);
    }

    public synchronized boolean handleEvent(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalTaskEvent event) {
        return acceptEvent(playerUuid, storyInstanceId, taskNodePlacementId, event);
    }

    public synchronized boolean applyEvent(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalTaskEvent event) {
        return acceptEvent(playerUuid, storyInstanceId, taskNodePlacementId, event);
    }

    /** Controlled failure transition for the exact instance identity. */
    public synchronized boolean markError(UUID playerUuid, String storyInstanceId, String taskNodePlacementId) {
        return getRequired(playerUuid, storyInstanceId, taskNodePlacementId).markError();
    }

    public synchronized boolean error(UUID playerUuid, String storyInstanceId, String taskNodePlacementId) {
        return markError(playerUuid, storyInstanceId, taskNodePlacementId);
    }

    /** Cancels only active instances for the exact player/story, retaining their snapshots. */
    public synchronized int cancelByStory(UUID playerUuid, String storyInstanceId) {
        if (playerUuid == null || blank(storyInstanceId))
            throw new IllegalArgumentException("Story identity is required.");
        int count = 0;
        for (CanonicalTaskInstance instance : instances.values())
            if (playerUuid.equals(instance.getPlayerUuid()) && storyInstanceId.equals(instance.getStoryInstanceId())
                && instance.isActive()) {
                    instance.cancel();
                    count++;
                }
        return count;
    }

    public synchronized int cancelStory(UUID playerUuid, String storyInstanceId) {
        return cancelByStory(playerUuid, storyInstanceId);
    }

    public synchronized List<CanonicalTaskInstanceSnapshot> snapshots() {
        List<CanonicalTaskInstanceSnapshot> result = new ArrayList<CanonicalTaskInstanceSnapshot>();
        for (CanonicalTaskInstance instance : instances.values()) result.add(instance.snapshot());
        Collections.sort(result, new Comparator<CanonicalTaskInstanceSnapshot>() {

            @Override
            public int compare(CanonicalTaskInstanceSnapshot a, CanonicalTaskInstanceSnapshot b) {
                int c = a.getPlayerUuid()
                    .toString()
                    .compareTo(
                        b.getPlayerUuid()
                            .toString());
                if (c == 0) c = a.getStoryInstanceId()
                    .compareTo(b.getStoryInstanceId());
                if (c == 0) c = a.getTaskNodePlacementId()
                    .compareTo(b.getTaskNodePlacementId());
                return c;
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized List<CanonicalTaskInstanceSnapshot> snapshotAll() {
        return snapshots();
    }

    public synchronized NBTTagCompound writeToNbt() {
        return CanonicalTaskInstanceNbtCodec.encode(snapshots());
    }

    public synchronized NBTTagCompound write() {
        return writeToNbt();
    }

    /** Decodes and restores every entry before replacing the current map. */
    public synchronized void readFromNbt(NBTTagCompound root, CanonicalTaskResourceResolver resolver) {
        List<CanonicalTaskInstanceSnapshot> decoded = CanonicalTaskInstanceNbtCodec.decode(root);
        if (resolver == null) throw new IllegalArgumentException("Task resource resolver is required.");
        Map<Key, CanonicalTaskInstance> replacement = new LinkedHashMap<Key, CanonicalTaskInstance>();
        for (CanonicalTaskInstanceSnapshot snapshot : decoded) {
            CanonicalGraphResource resource = resolver.resolve(snapshot.getTaskResourceId());
            if (resource == null)
                throw new IllegalArgumentException("Missing Task resource: " + snapshot.getTaskResourceId());
            CanonicalTaskInstance instance = CanonicalTaskInstance.restore(snapshot, resource);
            Key key = new Key(
                snapshot.getPlayerUuid(),
                snapshot.getStoryInstanceId(),
                snapshot.getTaskNodePlacementId());
            if (replacement.put(key, instance) != null) throw new IllegalArgumentException("Duplicate Task instance.");
        }
        instances.clear();
        instances.putAll(replacement);
    }

    public synchronized void restore(NBTTagCompound root, CanonicalTaskResourceResolver resolver) {
        readFromNbt(root, resolver);
    }

    private CanonicalTaskInstance getRequired(UUID player, String story, String placement) {
        CanonicalTaskInstance instance = get(player, story, placement);
        if (instance == null) throw new IllegalStateException("Task instance does not exist.");
        return instance;
    }

    private long now() {
        long value = clock.getAsLong();
        if (value <= 0) throw new IllegalStateException("Clock timestamp must be positive.");
        return value;
    }

    private static void validateIdentity(UUID player, String story, String placement) {
        if (player == null || blank(story) || blank(placement))
            throw new IllegalArgumentException("Task identity is required.");
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static UUID parseUuid(String value) {
        if (value == null || value.length() != 36) throw new IllegalArgumentException("Invalid player UUID.");
        try {
            UUID uuid = UUID.fromString(value);
            if (!uuid.toString()
                .equals(value)) throw new IllegalArgumentException("Invalid player UUID.");
            return uuid;
        } catch (RuntimeException exception) {
            throw new IllegalArgumentException("Invalid player UUID.");
        }
    }

    private static final class Key {

        private final UUID player;
        private final String story;
        private final String placement;

        Key(UUID player, String story, String placement) {
            this.player = player;
            this.story = story;
            this.placement = placement;
        }

        @Override
        public int hashCode() {
            return ((31 * player.hashCode() + story.hashCode()) * 31) + placement.hashCode();
        }

        @Override
        public boolean equals(Object value) {
            return value instanceof Key && player.equals(((Key) value).player)
                && story.equals(((Key) value).story)
                && placement.equals(((Key) value).placement);
        }
    }
}
