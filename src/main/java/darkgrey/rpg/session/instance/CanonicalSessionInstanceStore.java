package darkgrey.rpg.session.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.session.runtime.CanonicalSessionChoiceOption;
import darkgrey.rpg.session.runtime.CanonicalSessionRuntime;
import darkgrey.rpg.session.runtime.CanonicalSessionStep;

/** Synchronized server-neutral owner of at most one unconsumed cursor per player and Story. */
public final class CanonicalSessionInstanceStore {

    private final Map<Key, CanonicalSessionInstance> instances = new LinkedHashMap<Key, CanonicalSessionInstance>();
    private long nextTransportId = 1L;

    public synchronized CanonicalSessionInstance start(UUID playerUuid, String storyId, String aggregatePlacementId,
        CanonicalGraphResource resource) {
        return start(playerUuid, storyId, aggregatePlacementId, resource, false);
    }

    public synchronized CanonicalSessionInstance start(String playerUuid, String storyId, String aggregatePlacementId,
        CanonicalGraphResource resource) {
        return start(parseUuid(playerUuid), storyId, aggregatePlacementId, resource);
    }

    public synchronized CanonicalSessionInstance start(UUID playerUuid, String storyId, String aggregatePlacementId,
        CanonicalGraphResource resource, boolean activationLogic) {
        validateIdentity(playerUuid, storyId, aggregatePlacementId);
        if (resource == null) throw new IllegalArgumentException("Session resource is required.");
        Key key = new Key(playerUuid, storyId);
        if (instances.containsKey(key))
            throw new IllegalStateException("Session instance already exists for player/story.");
        long id = nextTransportId;
        if (id <= 0 || id == Long.MAX_VALUE) throw new IllegalStateException("Session transport ID exhausted.");
        CanonicalSessionInstance instance = new CanonicalSessionInstance(
            playerUuid,
            storyId,
            aggregatePlacementId,
            id,
            CanonicalSessionRuntime.start(resource, activationLogic));
        instances.put(key, instance);
        nextTransportId = id + 1L;
        return instance;
    }

    public synchronized CanonicalSessionInstance begin(UUID playerUuid, String storyId, String aggregatePlacementId,
        CanonicalGraphResource resource) {
        return start(playerUuid, storyId, aggregatePlacementId, resource);
    }

    public synchronized CanonicalSessionInstance get(UUID playerUuid, String storyId) {
        if (playerUuid == null || blank(storyId)) return null;
        return instances.get(new Key(playerUuid, storyId));
    }

    public synchronized CanonicalSessionInstance getInstance(UUID playerUuid, String storyId) {
        return get(playerUuid, storyId);
    }

    public synchronized CanonicalSessionInstance get(String playerUuid, String storyId) {
        return get(parseUuid(playerUuid), storyId);
    }

    public synchronized int size() {
        return instances.size();
    }

    public synchronized List<CanonicalSessionInstanceSnapshot> snapshots() {
        List<CanonicalSessionInstanceSnapshot> result = new ArrayList<CanonicalSessionInstanceSnapshot>();
        for (CanonicalSessionInstance instance : instances.values()) result.add(instance.snapshot());
        Collections.sort(result, new Comparator<CanonicalSessionInstanceSnapshot>() {

            @Override
            public int compare(CanonicalSessionInstanceSnapshot a, CanonicalSessionInstanceSnapshot b) {
                int result = a.getPlayerUuid()
                    .toString()
                    .compareTo(
                        b.getPlayerUuid()
                            .toString());
                return result == 0 ? a.getStoryId()
                    .compareTo(b.getStoryId()) : result;
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized List<CanonicalSessionInstanceSnapshot> snapshotAll() {
        return snapshots();
    }

    public synchronized CanonicalSessionStep continueLine(UUID playerUuid, String storyId, long transportId,
        String currentNodeId) {
        CanonicalSessionInstance instance = checkedAction(playerUuid, storyId, transportId, currentNodeId);
        return instance.getRuntime()
            .continueLine();
    }

    public synchronized CanonicalSessionStep continueLine(UUID playerUuid, String storyId, long transportId) {
        CanonicalSessionInstance instance = getRequired(playerUuid, storyId);
        return continueLine(playerUuid, storyId, transportId, instance.getCurrentNodeId());
    }

    public synchronized CanonicalSessionStep selectChoice(UUID playerUuid, String storyId, long transportId,
        String currentNodeId, String optionId) {
        CanonicalSessionInstance instance = checkedAction(playerUuid, storyId, transportId, currentNodeId);
        CanonicalSessionStep step = instance.getCurrentStep();
        if (step == null || step.getKind() != CanonicalSessionStep.Kind.CHOICE)
            throw new IllegalStateException("Session is not at Choice.");
        boolean known = false;
        for (CanonicalSessionChoiceOption option : step.getOptions()) if (option.getOptionId()
            .equals(optionId)) known = true;
        if (!known) throw new IllegalArgumentException("Unknown Choice option.");
        return instance.getRuntime()
            .choose(optionId);
    }

    public synchronized CanonicalSessionStep choose(UUID playerUuid, String storyId, long transportId,
        String currentNodeId, String optionId) {
        return selectChoice(playerUuid, storyId, transportId, currentNodeId, optionId);
    }

    public synchronized boolean consume(UUID playerUuid, String storyId, long transportId) {
        if (playerUuid == null || storyId == null) return false;
        Key key = new Key(playerUuid, storyId);
        CanonicalSessionInstance instance = instances.get(key);
        if (instance == null || instance.getTransportId() != transportId || !instance.isCompleted()) return false;
        instances.remove(key);
        return true;
    }

    /** Removes the exact player/Story child regardless of its current Session state. */
    public synchronized boolean cancelByStory(UUID playerUuid, String storyId) {
        if (playerUuid == null || blank(storyId)) return false;
        return instances.remove(new Key(playerUuid, storyId)) != null;
    }

    /** Permanently discards every player Session child owned by any selected Story. */
    public synchronized int discardByStoryIds(Set<String> storyIds) {
        if (storyIds == null) throw new IllegalArgumentException("Story IDs are required.");
        int removed = 0;
        java.util.Iterator<Map.Entry<Key, CanonicalSessionInstance>> iterator = instances.entrySet()
            .iterator();
        while (iterator.hasNext()) if (storyIds.contains(
            iterator.next()
                .getKey().story)) {
                    iterator.remove();
                    removed++;
                }
        return removed;
    }

    public synchronized NBTTagCompound writeToNbt() {
        return CanonicalSessionInstanceNbtCodec.encode(snapshots(), nextTransportId);
    }

    public synchronized NBTTagCompound write() {
        return writeToNbt();
    }

    /** Decodes, resolves, and restores the complete batch before replacing this store. */
    public synchronized void readFromNbt(NBTTagCompound root, CanonicalSessionResourceResolver resolver) {
        List<CanonicalSessionInstanceSnapshot> decoded = CanonicalSessionInstanceNbtCodec.decode(root);
        long restoredNextTransportId = CanonicalSessionInstanceNbtCodec.nextTransportId(root);
        if (resolver == null) throw new IllegalArgumentException("Session resource resolver is required.");
        Map<Key, CanonicalSessionInstance> replacement = new LinkedHashMap<Key, CanonicalSessionInstance>();
        for (CanonicalSessionInstanceSnapshot snapshot : decoded) {
            if (snapshot.getTransportId() >= restoredNextTransportId)
                throw new IllegalArgumentException("Session transport ID is not below next_transport_id.");
            CanonicalGraphResource resource = resolver.resolve(snapshot.getSessionResourceId());
            if (resource == null)
                throw new IllegalArgumentException("Missing Session resource: " + snapshot.getSessionResourceId());
            CanonicalSessionInstance instance = CanonicalSessionInstance.restore(snapshot, resource);
            Key key = new Key(snapshot.getPlayerUuid(), snapshot.getStoryId());
            if (replacement.put(key, instance) != null)
                throw new IllegalArgumentException("Duplicate player/story instance.");
        }
        instances.clear();
        instances.putAll(replacement);
        nextTransportId = restoredNextTransportId;
    }

    public synchronized void restore(NBTTagCompound root, CanonicalSessionResourceResolver resolver) {
        readFromNbt(root, resolver);
    }

    private CanonicalSessionInstance checkedAction(UUID playerUuid, String storyId, long transportId, String nodeId) {
        if (playerUuid == null || blank(storyId))
            throw new IllegalArgumentException("Session action identity is required.");
        CanonicalSessionInstance instance = getRequired(playerUuid, storyId);
        if (transportId <= 0 || instance.getTransportId() != transportId)
            throw new IllegalStateException("Stale Session transport ID.");
        if (nodeId == null || !nodeId.equals(instance.getCurrentNodeId()))
            throw new IllegalStateException("Stale Session node ID.");
        if (!instance.isActive()) throw new IllegalStateException("Session is not active.");
        return instance;
    }

    private CanonicalSessionInstance getRequired(UUID playerUuid, String storyId) {
        CanonicalSessionInstance instance = get(playerUuid, storyId);
        if (instance == null) throw new IllegalStateException("Session instance does not exist.");
        return instance;
    }

    private static void validateIdentity(UUID playerUuid, String storyId, String placement) {
        if (playerUuid == null || blank(storyId) || blank(placement))
            throw new IllegalArgumentException("Session identity is required.");
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static UUID parseUuid(String value) {
        if (blank(value) || value.length() != 36) throw new IllegalArgumentException("Player UUID is required.");
        try {
            UUID uuid = UUID.fromString(value);
            if (!uuid.toString()
                .equals(value.toLowerCase(java.util.Locale.ROOT)))
                throw new IllegalArgumentException("Invalid player UUID.");
            return uuid;
        } catch (RuntimeException exception) {
            throw new IllegalArgumentException("Invalid player UUID.");
        }
    }

    private static final class Key {

        private final UUID player;
        private final String story;

        Key(UUID player, String story) {
            this.player = player;
            this.story = story;
        }

        @Override
        public int hashCode() {
            return 31 * player.hashCode() + story.hashCode();
        }

        @Override
        public boolean equals(Object other) {
            return other instanceof Key && player.equals(((Key) other).player) && story.equals(((Key) other).story);
        }
    }
}
