package darkgrey.rpg.story.canonical.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;

/** Synchronized owner of at most one canonical Story instance per player and Story ID. */
public final class CanonicalStoryInstanceStore {

    private final Map<Key, CanonicalStoryInstance> instances = new LinkedHashMap<Key, CanonicalStoryInstance>();

    /**
     * Active re-entry returns the existing instance without moving its cursor. A terminal ONCE instance also remains
     * authoritative; only a terminal REPEATABLE instance may be replaced by a new run.
     */
    public synchronized CanonicalStoryInstance start(UUID playerUuid, CanonicalGraphResource resource,
        String triggerPortId, CanonicalStoryRepeatPolicy repeatPolicy, long activationTime) {
        return start(
            playerUuid,
            resource,
            triggerPortId,
            repeatPolicy,
            Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    public synchronized CanonicalStoryInstance start(UUID playerUuid, CanonicalGraphResource resource,
        String triggerPortId, CanonicalStoryRepeatPolicy repeatPolicy, Map<String, Boolean> logicInputs,
        long activationTime) {
        if (playerUuid == null || resource == null || repeatPolicy == null)
            throw new IllegalArgumentException("Canonical Story start inputs are required.");
        Key key = new Key(playerUuid, resource.getId());
        CanonicalStoryInstance existing = instances.get(key);
        if (existing != null) {
            if (existing.isActive()) return existing;
            if (existing.snapshot()
                .getRuntimeSnapshot()
                .getRepeatPolicy() == CanonicalStoryRepeatPolicy.ONCE) return existing;
        }
        CanonicalStoryInstance created = CanonicalStoryInstance
            .start(playerUuid, resource, triggerPortId, repeatPolicy, logicInputs, activationTime);
        instances.put(key, created);
        return created;
    }

    public synchronized CanonicalStoryInstance get(UUID playerUuid, String storyId) {
        if (playerUuid == null || blank(storyId)) return null;
        return instances.get(new Key(playerUuid, storyId));
    }

    public synchronized CanonicalStoryInstanceSnapshot getSnapshot(UUID playerUuid, String storyId) {
        CanonicalStoryInstance instance = get(playerUuid, storyId);
        return instance == null ? null : instance.snapshot();
    }

    public synchronized List<CanonicalStoryInstanceSnapshot> snapshots() {
        List<CanonicalStoryInstanceSnapshot> result = new ArrayList<CanonicalStoryInstanceSnapshot>();
        for (CanonicalStoryInstance instance : instances.values()) result.add(instance.snapshot());
        Collections.sort(result, new Comparator<CanonicalStoryInstanceSnapshot>() {

            @Override
            public int compare(CanonicalStoryInstanceSnapshot left, CanonicalStoryInstanceSnapshot right) {
                int result = left.getPlayerUuid()
                    .toString()
                    .compareTo(
                        right.getPlayerUuid()
                            .toString());
                return result == 0 ? left.getStoryId()
                    .compareTo(right.getStoryId()) : result;
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized int size() {
        return instances.size();
    }

    public synchronized int activeCount(UUID playerUuid) {
        int result = 0;
        for (CanonicalStoryInstance instance : instances.values())
            if (playerUuid.equals(instance.getPlayerUuid()) && instance.getStatus() == CanonicalStoryStatus.ACTIVE)
                result++;
        return result;
    }

    public synchronized NBTTagCompound writeToNbt() {
        return CanonicalStoryInstanceNbtCodec.encode(snapshots());
    }

    public synchronized void readFromNbt(NBTTagCompound root, CanonicalStoryResourceResolver resolver) {
        if (resolver == null) throw new IllegalArgumentException("Canonical Story resolver is required.");
        List<CanonicalStoryInstanceSnapshot> decoded = CanonicalStoryInstanceNbtCodec.decode(root);
        LinkedHashMap<Key, CanonicalStoryInstance> replacement = new LinkedHashMap<Key, CanonicalStoryInstance>();
        for (CanonicalStoryInstanceSnapshot snapshot : decoded) {
            CanonicalGraphResource resource = resolver.resolve(snapshot.getStoryId());
            if (resource == null)
                throw new IllegalStateException("Missing canonical Story resource: " + snapshot.getStoryId());
            CanonicalStoryInstance instance = CanonicalStoryInstance.restore(snapshot, resource);
            if (replacement.put(new Key(snapshot.getPlayerUuid(), snapshot.getStoryId()), instance) != null)
                throw new IllegalArgumentException("Duplicate canonical Story instance identity.");
        }
        instances.clear();
        instances.putAll(replacement);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static final class Key {

        private final UUID playerUuid;
        private final String storyId;

        Key(UUID playerUuid, String storyId) {
            if (playerUuid == null || blank(storyId))
                throw new IllegalArgumentException("Canonical Story identity is required.");
            this.playerUuid = playerUuid;
            this.storyId = storyId;
        }

        @Override
        public boolean equals(Object other) {
            if (!(other instanceof Key)) return false;
            Key that = (Key) other;
            return playerUuid.equals(that.playerUuid) && storyId.equals(that.storyId);
        }

        @Override
        public int hashCode() {
            return 31 * playerUuid.hashCode() + storyId.hashCode();
        }
    }
}
