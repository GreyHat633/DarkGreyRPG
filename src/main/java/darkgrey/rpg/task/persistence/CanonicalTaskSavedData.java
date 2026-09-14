package darkgrey.rpg.task.persistence;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.event.CanonicalTaskInstanceIdentity;
import darkgrey.rpg.task.event.CanonicalTaskSubscriptionIndex;
import darkgrey.rpg.task.instance.CanonicalTaskInstance;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStore;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Forge WorldSavedData boundary for canonical Task instances and indexed events. */
public final class CanonicalTaskSavedData extends WorldSavedData {

    private long presentationGeneration;

    public synchronized long getPresentationGeneration() {
        return presentationGeneration;
    }

    @Override
    public synchronized void markDirty() {
        super.markDirty();
        presentationGeneration++;
    }

    public static final String DATA_NAME = "darkgrey_rpg_canonical_tasks";

    private CanonicalTaskInstanceStore store = new CanonicalTaskInstanceStore();
    private CanonicalTaskSubscriptionIndex index = new CanonicalTaskSubscriptionIndex();
    private NBTTagCompound pendingRaw;
    private boolean bound = true;

    /** Constructor required by Forge MapStorage reflective loading. */
    public CanonicalTaskSavedData(String name) {
        super(name);
    }

    /** Convenience constructor for tests and newly-created data. */
    public CanonicalTaskSavedData() {
        this(DATA_NAME);
    }

    public static CanonicalTaskSavedData get(EntityPlayer player) {
        if (player == null) throw new IllegalArgumentException("Player is required.");
        WorldServer overworld = MinecraftServer.getServer()
            .worldServerForDimension(0);
        return get(overworld.mapStorage);
    }

    public static CanonicalTaskSavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(CanonicalTaskSavedData.class, DATA_NAME);
        if (loaded instanceof CanonicalTaskSavedData) return (CanonicalTaskSavedData) loaded;
        CanonicalTaskSavedData created = new CanonicalTaskSavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public static CanonicalTaskSavedData getOrCreate(MapStorage storage) {
        return get(storage);
    }

    public static CanonicalTaskSavedData get(EntityPlayer player, CanonicalTaskResourceResolver resolver) {
        CanonicalTaskSavedData data = get(player);
        data.bind(resolver);
        return data;
    }

    public static CanonicalTaskSavedData get(MapStorage storage, CanonicalTaskResourceResolver resolver) {
        CanonicalTaskSavedData data = get(storage);
        data.bind(resolver);
        return data;
    }

    /** Resolves pending bytes into replacement Store and index before swapping live state. */
    public synchronized void bind(CanonicalTaskResourceResolver resolver) {
        if (resolver == null) throw new IllegalArgumentException("Task resource resolver is required.");
        if (pendingRaw != null) {
            NBTTagCompound raw = pendingRaw;
            List<CanonicalTaskInstanceSnapshot> decoded = CanonicalTaskInstanceNbtCodecBridge.decode(raw);
            Map<String, CanonicalGraphResource> resources = new HashMap<String, CanonicalGraphResource>();
            for (CanonicalTaskInstanceSnapshot snapshot : decoded) {
                CanonicalGraphResource resource = resolver.resolve(snapshot.getTaskResourceId());
                if (resource == null)
                    throw new IllegalArgumentException("Missing Task resource: " + snapshot.getTaskResourceId());
                if (resources.put(snapshot.getTaskResourceId(), resource) != null) continue;
            }
            CanonicalTaskInstanceStore replacementStore = new CanonicalTaskInstanceStore();
            replacementStore.readFromNbt(raw, resolver);
            CanonicalTaskSubscriptionIndex replacementIndex = new CanonicalTaskSubscriptionIndex();
            replacementIndex.rebuild(replacementStore.snapshots(), resources);
            store = replacementStore;
            index = replacementIndex;
            pendingRaw = null;
        } else {
            // Explicit bind validates fingerprints against a replacement Store as well as repairing
            // the
            // index.
            NBTTagCompound raw = store.writeToNbt();
            List<CanonicalTaskInstanceSnapshot> decoded = CanonicalTaskInstanceNbtCodecBridge.decode(raw);
            Map<String, CanonicalGraphResource> resources = new HashMap<String, CanonicalGraphResource>();
            for (CanonicalTaskInstanceSnapshot snapshot : decoded) {
                CanonicalGraphResource resource = resolver.resolve(snapshot.getTaskResourceId());
                if (resource == null)
                    throw new IllegalArgumentException("Missing Task resource: " + snapshot.getTaskResourceId());
                resources.put(snapshot.getTaskResourceId(), resource);
            }
            CanonicalTaskInstanceStore replacementStore = new CanonicalTaskInstanceStore();
            replacementStore.readFromNbt(raw, resolver);
            CanonicalTaskSubscriptionIndex replacementIndex = new CanonicalTaskSubscriptionIndex();
            replacementIndex.rebuild(replacementStore.snapshots(), resources);
            store = replacementStore;
            index = replacementIndex;
        }
        bound = true;
    }

    public synchronized void restore(CanonicalTaskResourceResolver resolver) {
        bind(resolver);
    }

    public synchronized boolean isBound() {
        return bound;
    }

    public synchronized boolean hasPendingData() {
        return pendingRaw != null;
    }

    public synchronized NBTTagCompound getPendingRaw() {
        return pendingRaw == null ? null : copy(pendingRaw);
    }

    public synchronized CanonicalTaskInstanceSnapshot start(UUID playerUuid, String storyId, String placementId,
        CanonicalGraphResource resource) {
        requireBound();
        NBTTagCompound before = persistedState();
        CanonicalTaskInstance instance = store.start(playerUuid, storyId, placementId, resource);
        index.reindex(
            instance.snapshot(),
            instance.getRuntime()
                .getResource());
        markIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalTaskInstanceSnapshot start(String playerUuid, String storyId, String placementId,
        CanonicalGraphResource resource) {
        return start(UUID.fromString(playerUuid), storyId, placementId, resource);
    }

    public synchronized CanonicalTaskInstanceSnapshot start(UUID playerUuid, String storyId, String placementId,
        CanonicalGraphResource resource, long activationTime) {
        requireBound();
        NBTTagCompound before = persistedState();
        CanonicalTaskInstance instance = store.start(playerUuid, storyId, placementId, resource, activationTime);
        index.reindex(
            instance.snapshot(),
            instance.getRuntime()
                .getResource());
        markIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalTaskInstanceSnapshot getSnapshot(UUID playerUuid, String storyId, String placementId) {
        requireBound();
        CanonicalTaskInstance instance = store.get(playerUuid, storyId, placementId);
        return instance == null ? null : instance.snapshot();
    }

    public synchronized CanonicalTaskInstanceSnapshot setLogicInput(UUID playerUuid, String storyId, String placementId,
        String portId, boolean value, long eventTime) {
        requireBound();
        CanonicalTaskInstance instance = store.get(playerUuid, storyId, placementId);
        if (instance == null) throw new IllegalStateException("Canonical Task instance does not exist.");
        NBTTagCompound before = persistedState();
        instance.setLogicInput(portId, value, eventTime);
        index.reindex(
            instance.snapshot(),
            instance.getRuntime()
                .getResource());
        markIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalTaskInstanceSnapshot grantReward(UUID player, String story, String placement,
        String nodeId, long eventTime) {
        requireBound();
        CanonicalTaskInstance instance = store.get(player, story, placement);
        if (instance == null || !instance.grantReward(nodeId, eventTime)) return null;
        index.reindex(
            instance.snapshot(),
            instance.getRuntime()
                .getResource());
        markDirty();
        return instance.snapshot();
    }

    public interface ObjectiveCommit {

        void commit();

        void rollback();
    }

    /** Stages one exact Objective update before committing an optional inventory deduction. */
    public synchronized CanonicalTaskInstanceSnapshot sampleObjective(UUID playerUuid, String storyId,
        String placementId, String objectiveId, CanonicalTaskEvent event, long time, ObjectiveCommit effect) {
        requireBound();
        CanonicalTaskInstance original = store.get(playerUuid, storyId, placementId);
        if (original == null || !original.isActive()
            || !original.getRuntime()
                .isObjectiveActive(objectiveId))
            return null;
        Map<String, String> values = new LinkedHashMap<String, String>(event.getValues());
        values.put("objective_id", objectiveId);
        CanonicalGraphResource resource = original.getRuntime()
            .getResource();
        CanonicalTaskInstance staged = CanonicalTaskInstance.restore(original.snapshot(), resource);
        if (!staged.accept(new CanonicalTaskEvent(event.getType(), values, event.getAmount()), time)) return null;
        try {
            if (effect != null) effect.commit();
            store.replaceExisting(staged);
            index.reindex(staged.snapshot(), resource);
            markDirty();
            return staged.snapshot();
        } catch (RuntimeException failure) {
            store.replaceExisting(original);
            index.reindex(original.snapshot(), resource);
            if (effect != null) effect.rollback();
            throw failure;
        }
    }

    public synchronized CanonicalTaskInstanceSnapshot getInstanceSnapshot(UUID playerUuid, String storyId,
        String placementId) {
        return getSnapshot(playerUuid, storyId, placementId);
    }

    public synchronized List<CanonicalTaskInstanceSnapshot> snapshots() {
        requireBound();
        return store.snapshots();
    }

    public synchronized List<CanonicalTaskInstanceSnapshot> snapshotAll() {
        return snapshots();
    }

    public synchronized int size() {
        requireBound();
        return store.size();
    }

    /** Permanently retires every persisted Task instance owned by the selected Story IDs. */
    public synchronized int discardByStoryIds(Set<String> storyIds) {
        if (storyIds == null) throw new IllegalArgumentException("Story IDs are required.");
        if (storyIds.isEmpty()) return 0;
        if (pendingRaw != null) {
            List<CanonicalTaskInstanceSnapshot> snapshots = CanonicalTaskInstanceNbtCodecBridge.decode(pendingRaw);
            List<CanonicalTaskInstanceSnapshot> retained = new ArrayList<CanonicalTaskInstanceSnapshot>();
            for (CanonicalTaskInstanceSnapshot snapshot : snapshots)
                if (!storyIds.contains(snapshot.getStoryInstanceId())) retained.add(snapshot);
            int removed = snapshots.size() - retained.size();
            if (removed > 0) {
                pendingRaw = CanonicalTaskInstanceNbtCodecBridge.encode(retained);
                markDirty();
            }
            return removed;
        }
        NBTTagCompound before = persistedState();
        for (CanonicalTaskInstanceSnapshot snapshot : store.snapshots())
            if (storyIds.contains(snapshot.getStoryInstanceId())) index.remove(
                new CanonicalTaskInstanceIdentity(
                    snapshot.getPlayerUuid(),
                    snapshot.getStoryInstanceId(),
                    snapshot.getTaskNodePlacementId()));
        int removed = store.discardByStoryIds(storyIds);
        markIfChanged(before);
        return removed;
    }

    /** Permanently discards every persisted Task for one exact player/Story identity. */
    public synchronized int discardByPlayerStory(UUID playerUuid, String storyId) {
        if (playerUuid == null || storyId == null
            || storyId.trim()
                .isEmpty())
            throw new IllegalArgumentException("Player and Story identity are required.");
        if (pendingRaw != null) {
            List<CanonicalTaskInstanceSnapshot> snapshots = CanonicalTaskInstanceNbtCodecBridge.decode(pendingRaw);
            List<CanonicalTaskInstanceSnapshot> retained = new ArrayList<CanonicalTaskInstanceSnapshot>();
            int removed = 0;
            for (CanonicalTaskInstanceSnapshot snapshot : snapshots) {
                if (playerUuid.equals(snapshot.getPlayerUuid()) && storyId.equals(snapshot.getStoryInstanceId())) {
                    index.remove(
                        new CanonicalTaskInstanceIdentity(
                            snapshot.getPlayerUuid(),
                            snapshot.getStoryInstanceId(),
                            snapshot.getTaskNodePlacementId()));
                    removed++;
                } else retained.add(snapshot);
            }
            if (removed > 0) {
                pendingRaw = CanonicalTaskInstanceNbtCodecBridge.encode(retained);
                markDirty();
            }
            return removed;
        }
        NBTTagCompound before = persistedState();
        for (CanonicalTaskInstanceSnapshot snapshot : store.snapshots())
            if (playerUuid.equals(snapshot.getPlayerUuid()) && storyId.equals(snapshot.getStoryInstanceId()))
                index.remove(
                    new CanonicalTaskInstanceIdentity(
                        snapshot.getPlayerUuid(),
                        snapshot.getStoryInstanceId(),
                        snapshot.getTaskNodePlacementId()));
        int removed = store.discardByPlayerStory(playerUuid, storyId);
        markIfChanged(before);
        return removed;
    }

    public synchronized CanonicalTaskSubscriptionIndex getSubscriptionIndex() {
        return index;
    }

    public synchronized CanonicalTaskSubscriptionIndex index() {
        return index;
    }

    /** Queries only indexed candidates, then reindexes those candidates after one event. */
    public synchronized CanonicalTaskDispatchResult dispatch(UUID playerUuid, CanonicalTaskEvent event,
        long eventTime) {
        requireBound();
        if (playerUuid == null || event == null)
            throw new IllegalArgumentException("Player and Task event are required.");
        List<CanonicalTaskInstanceIdentity> candidates = index.query(playerUuid, event);
        int changed = 0;
        List<CanonicalTaskInstanceSnapshot> settled = new ArrayList<CanonicalTaskInstanceSnapshot>();
        Map<CanonicalTaskInstanceIdentity, String> results = new LinkedHashMap<CanonicalTaskInstanceIdentity, String>();
        List<CanonicalTaskInstanceIdentity> errored = new ArrayList<CanonicalTaskInstanceIdentity>();
        NBTTagCompound before = persistedState();
        for (CanonicalTaskInstanceIdentity identity : candidates) {
            CanonicalTaskInstance instance = store
                .get(identity.getPlayerUuid(), identity.getStoryInstanceId(), identity.getTaskNodePlacementId());
            if (instance == null || !instance.isActive()) {
                index.remove(identity);
                continue;
            }
            try {
                if (instance.accept(event, eventTime)) changed++;
                if (instance.isSettled()) {
                    CanonicalTaskInstanceSnapshot snapshot = instance.snapshot();
                    settled.add(snapshot);
                    results.put(identity, snapshot.getResultPortId());
                }
                index.reindex(
                    instance.snapshot(),
                    instance.getRuntime()
                        .getResource());
            } catch (RuntimeException failure) {
                if (instance.markError()) {
                    errored.add(identity);
                    index.remove(identity);
                }
            }
        }
        markIfChanged(before);
        return new CanonicalTaskDispatchResult(candidates.size(), changed, settled, results, errored);
    }

    public synchronized CanonicalTaskDispatchResult dispatch(UUID playerUuid, CanonicalTaskEvent event) {
        return dispatch(playerUuid, event, System.currentTimeMillis());
    }

    public synchronized CanonicalTaskDispatchResult acceptEvent(UUID playerUuid, CanonicalTaskEvent event,
        long eventTime) {
        return dispatch(playerUuid, event, eventTime);
    }

    public synchronized CanonicalTaskDispatchResult acceptEvent(UUID playerUuid, CanonicalTaskEvent event) {
        return dispatch(playerUuid, event);
    }

    public synchronized CanonicalTaskDispatchResult handleEvent(UUID playerUuid, CanonicalTaskEvent event,
        long eventTime) {
        return dispatch(playerUuid, event, eventTime);
    }

    public synchronized CanonicalTaskDispatchResult dispatchEvent(UUID playerUuid, CanonicalTaskEvent event,
        long eventTime) {
        return dispatch(playerUuid, event, eventTime);
    }

    public synchronized CanonicalTaskDispatchResult dispatchEvent(UUID playerUuid, CanonicalTaskEvent event) {
        return dispatch(playerUuid, event);
    }

    public synchronized boolean markError(UUID playerUuid, String storyId, String placementId) {
        requireBound();
        NBTTagCompound before = persistedState();
        boolean changed = store.markError(playerUuid, storyId, placementId);
        if (changed) index.remove(new CanonicalTaskInstanceIdentity(playerUuid, storyId, placementId));
        markIfChanged(before);
        return changed;
    }

    public synchronized int cancelByStory(UUID playerUuid, String storyId) {
        requireBound();
        NBTTagCompound before = persistedState();
        int changed = store.cancelByStory(playerUuid, storyId);
        if (changed > 0) for (CanonicalTaskInstanceSnapshot snapshot : store.snapshots())
            if (playerUuid.equals(snapshot.getPlayerUuid()) && storyId.equals(snapshot.getStoryInstanceId()))
                index.remove(new CanonicalTaskInstanceIdentity(playerUuid, storyId, snapshot.getTaskNodePlacementId()));
        markIfChanged(before);
        return changed;
    }

    public synchronized int cancelStory(UUID playerUuid, String storyId) {
        return cancelByStory(playerUuid, storyId);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Task NBT is required.");
        CanonicalTaskInstanceNbtCodecBridge.decode(root); // validate before replacing pending fence
        pendingRaw = copy(root);
        presentationGeneration++;
        bound = false;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        NBTTagCompound output = bound ? store.writeToNbt()
            : pendingRaw == null ? new CanonicalTaskInstanceStore().writeToNbt() : copy(pendingRaw);
        for (String key : new java.util.HashSet<String>(root.func_150296_c())) root.removeTag(key);
        for (String key : output.func_150296_c()) root.setTag(
            key,
            output.getTag(key)
                .copy());
    }

    private void requireBound() {
        if (!bound) throw new IllegalStateException("Canonical Task data is not bound to a resource resolver.");
    }

    private NBTTagCompound persistedState() {
        NBTTagCompound state = new NBTTagCompound();
        writeToNBT(state);
        return state;
    }

    private void markIfChanged(NBTTagCompound before) {
        if (!before.equals(persistedState())) markDirty();
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return (NBTTagCompound) value.copy();
    }

    /** Kept private so the public boundary remains focused on WorldSavedData. */
    private static final class CanonicalTaskInstanceNbtCodecBridge {

        static List<CanonicalTaskInstanceSnapshot> decode(NBTTagCompound root) {
            return darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec.decode(root);
        }

        static NBTTagCompound encode(List<CanonicalTaskInstanceSnapshot> snapshots) {
            return darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec.encode(snapshots);
        }
    }
}
