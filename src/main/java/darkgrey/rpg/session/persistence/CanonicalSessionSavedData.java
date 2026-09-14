package darkgrey.rpg.session.persistence;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
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
import darkgrey.rpg.session.instance.CanonicalSessionInstance;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceNbtCodec;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceStore;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.runtime.CanonicalSessionStep;
import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;
import darkgrey.rpg.story.canonical.CanonicalStoryFlowTransition;
import darkgrey.rpg.story.canonical.CanonicalStoryPendingContinuation;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRoute;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstance;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryResourceResolver;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Forge WorldSavedData boundary for canonical Session instances. */
public final class CanonicalSessionSavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_canonical_sessions";

    private CanonicalSessionInstanceStore store = new CanonicalSessionInstanceStore();
    private CanonicalStoryInstanceStore storyStore = new CanonicalStoryInstanceStore();
    private List<CanonicalStoryPendingContinuation> continuations = new java.util.ArrayList<CanonicalStoryPendingContinuation>();
    private NBTTagCompound pendingRaw;
    private boolean bound = true;
    private boolean pendingLegacy;
    private CanonicalSessionResourceResolver boundSessionResolver;
    private CanonicalStoryResourceResolver boundStoryResolver;
    /** Idempotency keys for terminal Flow routing; keyed by player, Story run and public port. */
    private final Set<String> claimedStoryTerminalRoutes = new HashSet<String>();
    private final Set<String> pendingStoryTerminalRoutes = new HashSet<String>();
    private final Map<String, String> terminalRouteTargets = new java.util.LinkedHashMap<String, String>();
    private final Map<String, Boolean> observedStoryStartConditions = new java.util.LinkedHashMap<String, Boolean>();

    /** Constructor required by Forge MapStorage reflective loading. */
    public CanonicalSessionSavedData(String name) {
        super(name);
    }

    /** Convenience constructor for tests and newly-created data. */
    public CanonicalSessionSavedData() {
        this(DATA_NAME);
    }

    public static CanonicalSessionSavedData get(EntityPlayer player) {
        if (player == null) throw new IllegalArgumentException("Player is required.");
        WorldServer overworld = MinecraftServer.getServer()
            .worldServerForDimension(0);
        return get(overworld.mapStorage);
    }

    public static CanonicalSessionSavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(CanonicalSessionSavedData.class, DATA_NAME);
        if (loaded instanceof CanonicalSessionSavedData) return (CanonicalSessionSavedData) loaded;
        CanonicalSessionSavedData created = new CanonicalSessionSavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public static CanonicalSessionSavedData get(EntityPlayer player, CanonicalSessionResourceResolver resolver) {
        CanonicalSessionSavedData data = get(player);
        data.bind(resolver);
        return data;
    }

    public static CanonicalSessionSavedData get(MapStorage storage, CanonicalSessionResourceResolver resolver) {
        CanonicalSessionSavedData data = get(storage);
        data.bind(resolver);
        return data;
    }

    public static CanonicalSessionSavedData get(EntityPlayer player, CanonicalSessionResourceResolver sessionResolver,
        CanonicalStoryResourceResolver storyResolver) {
        CanonicalSessionSavedData data = get(player);
        data.bind(sessionResolver, storyResolver);
        return data;
    }

    public static CanonicalSessionSavedData getOrCreate(MapStorage storage) {
        return get(storage);
    }

    /** Restores pending persisted bytes only after all referenced resources resolve. */
    public synchronized void bind(CanonicalSessionResourceResolver resolver) {
        bindInternal(resolver, boundStoryResolver);
    }

    /** Atomically restores Session instances, pending handoffs, and Story cursors from one world checkpoint. */
    public synchronized void bind(CanonicalSessionResourceResolver sessionResolver,
        CanonicalStoryResourceResolver storyResolver) {
        bindInternal(sessionResolver, storyResolver);
    }

    /**
     * Rebinds against the currently installed project and drops runtime instances whose package resources were
     * uninstalled. Strict {@link #bind(CanonicalSessionResourceResolver, CanonicalStoryResourceResolver)} remains the
     * persistence-validation boundary for callers that require every reference to resolve.
     */
    public synchronized void bindAvailable(CanonicalSessionResourceResolver sessionResolver,
        CanonicalStoryResourceResolver storyResolver) {
        bindInternal(sessionResolver, storyResolver, true);
    }

    private void bindInternal(CanonicalSessionResourceResolver resolver, CanonicalStoryResourceResolver storyResolver) {
        bindInternal(resolver, storyResolver, false);
    }

    private void bindInternal(CanonicalSessionResourceResolver resolver, CanonicalStoryResourceResolver storyResolver,
        boolean discardUnavailable) {
        if (resolver == null) throw new IllegalArgumentException("Session resource resolver is required.");
        CanonicalSessionInstanceStore replacementSessions = new CanonicalSessionInstanceStore();
        CanonicalStoryInstanceStore replacementStories = new CanonicalStoryInstanceStore();
        List<CanonicalStoryPendingContinuation> replacementContinuations = new java.util.ArrayList<CanonicalStoryPendingContinuation>();
        List<CanonicalSessionInstanceSnapshot> sessionSnapshots;
        List<CanonicalStoryInstanceSnapshot> storySnapshots;
        List<CanonicalStoryPendingContinuation> pendingContinuations;
        long replacementNextTransportId;
        if (pendingRaw != null && pendingLegacy) {
            sessionSnapshots = CanonicalSessionInstanceNbtCodec.decode(pendingRaw);
            replacementNextTransportId = CanonicalSessionInstanceNbtCodec.nextTransportId(pendingRaw);
            storySnapshots = java.util.Collections.emptyList();
            pendingContinuations = java.util.Collections.emptyList();
        } else if (pendingRaw != null) {
            CanonicalSessionWorldStateNbtCodec.Decoded decoded = CanonicalSessionWorldStateNbtCodec.decode(pendingRaw);
            sessionSnapshots = decoded.getSessions();
            replacementNextTransportId = decoded.getNextTransportId();
            storySnapshots = decoded.getStories();
            pendingContinuations = decoded.getContinuations();
            claimedStoryTerminalRoutes.clear();
            claimedStoryTerminalRoutes.addAll(decoded.getTerminalRoutes());
            pendingStoryTerminalRoutes.clear();
            pendingStoryTerminalRoutes.addAll(decoded.getPendingTerminalRoutes());
            terminalRouteTargets.clear();
            terminalRouteTargets.putAll(decoded.getTerminalRouteTargets());
            observedStoryStartConditions.clear();
            observedStoryStartConditions.putAll(decoded.getStartObservations());
        } else {
            sessionSnapshots = store.snapshots();
            replacementNextTransportId = nextTransportId();
            storySnapshots = storyStore.snapshots();
            pendingContinuations = continuations;
        }
        boolean discarded = false;
        if (discardUnavailable) {
            List<CanonicalSessionInstanceSnapshot> availableSessions = new java.util.ArrayList<CanonicalSessionInstanceSnapshot>();
            for (CanonicalSessionInstanceSnapshot snapshot : sessionSnapshots) {
                if (resolver.resolve(snapshot.getSessionResourceId()) != null) availableSessions.add(snapshot);
                else discarded = true;
            }
            sessionSnapshots = availableSessions;
            List<CanonicalStoryInstanceSnapshot> availableStories = new java.util.ArrayList<CanonicalStoryInstanceSnapshot>();
            for (CanonicalStoryInstanceSnapshot snapshot : storySnapshots) {
                if (storyResolver != null && storyResolver.resolve(snapshot.getStoryId()) != null)
                    availableStories.add(snapshot);
                else discarded = true;
            }
            storySnapshots = availableStories;
            List<CanonicalStoryPendingContinuation> availableContinuations = new java.util.ArrayList<CanonicalStoryPendingContinuation>();
            for (CanonicalStoryPendingContinuation continuation : pendingContinuations) {
                if (storyResolver != null && storyResolver.resolve(continuation.getStoryId()) != null
                    && resolver.resolve(continuation.getSessionResourceId()) != null)
                    availableContinuations.add(continuation);
                else discarded = true;
            }
            pendingContinuations = availableContinuations;
        }
        replacementSessions.readFromNbt(
            CanonicalSessionInstanceNbtCodec.encode(sessionSnapshots, replacementNextTransportId),
            resolver);
        restoreStories(replacementStories, storySnapshots, storyResolver);
        replacementContinuations.addAll(pendingContinuations);
        validateStoryContinuations(replacementStories, replacementContinuations);
        store = replacementSessions;
        storyStore = replacementStories;
        continuations = replacementContinuations;
        pendingRaw = null;
        pendingLegacy = false;
        boundSessionResolver = resolver;
        boundStoryResolver = storyResolver;
        bound = true;
        if (discarded) markDirty();
    }

    public synchronized void restore(CanonicalSessionResourceResolver resolver) {
        bind(resolver);
    }

    public synchronized boolean isBound() {
        return bound;
    }

    public synchronized boolean hasPendingData() {
        return pendingRaw != null;
    }

    /** Returns a detached, deterministic view of all pending Story cursors. */
    public synchronized List<CanonicalStoryPendingContinuation> getPendingContinuations() {
        requireBound();
        return immutableContinuations();
    }

    public synchronized List<CanonicalStoryPendingContinuation> pendingContinuations() {
        return getPendingContinuations();
    }

    public synchronized List<CanonicalStoryPendingContinuation> getContinuations() {
        return getPendingContinuations();
    }

    public synchronized List<CanonicalStoryPendingContinuation> getPendingStoryContinuations() {
        return getPendingContinuations();
    }

    public synchronized CanonicalStoryPendingContinuation getPendingContinuation(UUID playerUuid, String storyId) {
        requireBound();
        for (CanonicalStoryPendingContinuation continuation : continuations) if (continuation.getPlayerUuid()
            .equals(playerUuid)
            && continuation.getStoryId()
                .equals(storyId))
            return continuation;
        return null;
    }

    public synchronized CanonicalStoryPendingContinuation pendingContinuation(UUID playerUuid, String storyId) {
        return getPendingContinuation(playerUuid, storyId);
    }

    public synchronized CanonicalStoryInstanceSnapshot startStory(UUID playerUuid, CanonicalGraphResource resource,
        String triggerPortId, CanonicalStoryRepeatPolicy repeatPolicy, long activationTime) {
        return startStory(
            playerUuid,
            resource,
            triggerPortId,
            repeatPolicy,
            java.util.Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    public synchronized CanonicalStoryInstanceSnapshot startStory(UUID playerUuid, CanonicalGraphResource resource,
        String triggerPortId, CanonicalStoryRepeatPolicy repeatPolicy, Map<String, Boolean> logicInputs,
        long activationTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        boolean restart = storyStore.startDisposition(playerUuid, resource.getId())
            == darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.REPEATABLE_RESTART;
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstanceSnapshot result = candidate
            .start(playerUuid, resource, triggerPortId, repeatPolicy, logicInputs, activationTime)
            .snapshot();
        // Candidate creation validates the trigger first. Failed starts must retain previous-run children.
        if (restart) {
            store.cancelByStory(playerUuid, resource.getId());
            continuations = withoutContinuation(playerUuid, resource.getId());
        }
        storyStore = candidate;
        markWorldIfChanged(before);
        return result;
    }

    public synchronized darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition startDisposition(
        UUID playerUuid, String storyId) {
        requireStoryBound();
        return storyStore.startDisposition(playerUuid, storyId);
    }

    /** Claims a terminal output exactly once for one durable Story run. */
    public synchronized boolean claimStoryTerminalRoute(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId) {
        requireBound();
        if (playerUuid == null || storyId == null || terminalPortId == null || activationTime <= 0)
            throw new IllegalArgumentException("Terminal Story route identity is required.");
        String key = terminalRouteKey(playerUuid, storyId, activationTime, terminalPortId);
        if (claimedStoryTerminalRoutes.contains(key)) return false;
        if (!pendingStoryTerminalRoutes.add(key)) return true;
        markDirty();
        return true;
    }

    public synchronized void markStoryTerminalRouteApplied(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId) {
        String key = terminalRouteKey(playerUuid, storyId, activationTime, terminalPortId);
        pendingStoryTerminalRoutes.remove(key);
        if (claimedStoryTerminalRoutes.add(key)) markDirty();
    }

    /** Resolves one syntactically valid pending route after a bounded cycle abort. */
    public synchronized void markStoryTerminalRouteAppliedKey(String key) {
        if (key == null) return;
        String[] parts = key.split("\\u0000", -1);
        if (parts.length != 4) return;
        UUID player;
        long activation;
        try {
            player = UUID.fromString(parts[0]);
            activation = Long.parseLong(parts[2]);
        } catch (RuntimeException exception) {
            return;
        }
        if (activation <= 0 || parts[1].trim()
            .isEmpty()
            || parts[3].trim()
                .isEmpty())
            return;
        markStoryTerminalRouteApplied(player, parts[1], activation, parts[3]);
    }

    /** Releases a claim after a destination start failed before its route became durable. */
    public synchronized void releaseStoryTerminalRoute(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId) {
        if (playerUuid == null || storyId == null || terminalPortId == null || activationTime <= 0) return;
        String key = terminalRouteKey(playerUuid, storyId, activationTime, terminalPortId);
        if (pendingStoryTerminalRoutes.remove(key)) markDirty();
    }

    /** Associates a source terminal claim with the exact destination run identity. */
    public synchronized void recordStoryTerminalRouteTarget(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId, String targetStoryId, long targetActivationTime) {
        if (playerUuid == null || storyId == null
            || terminalPortId == null
            || activationTime <= 0
            || targetStoryId == null
            || targetStoryId.trim()
                .isEmpty()
            || targetActivationTime <= 0)
            throw new IllegalArgumentException("Terminal route target identity is required.");
        String source = terminalRouteKey(playerUuid, storyId, activationTime, terminalPortId);
        String target = playerUuid.toString() + "\u0000" + targetStoryId + "\u0000" + targetActivationTime;
        String previous = terminalRouteTargets.get(source);
        if (previous != null && !previous.equals(target))
            throw new IllegalStateException("Terminal route target identity changed for " + source);
        terminalRouteTargets.put(source, target);
        if (previous == null) markDirty();
    }

    public synchronized String storyTerminalRouteTarget(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId) {
        return terminalRouteTargets.get(terminalRouteKey(playerUuid, storyId, activationTime, terminalPortId));
    }

    private static String terminalRouteKey(UUID playerUuid, String storyId, long activationTime,
        String terminalPortId) {
        return playerUuid.toString() + "\u0000" + storyId + "\u0000" + activationTime + "\u0000" + terminalPortId;
    }

    /** Returns source Story IDs whose terminal route still needs recovery after a crash. */
    public synchronized List<String> pendingStoryTerminalRouteStoryIds(UUID playerUuid) {
        if (playerUuid == null) return Collections.emptyList();
        Set<String> result = new java.util.TreeSet<String>();
        String prefix = playerUuid.toString() + "\u0000";
        for (String key : pendingStoryTerminalRoutes) if (key.startsWith(prefix)) {
            String[] parts = key.split("\\u0000", -1);
            if (parts.length == 4 && !parts[1].isEmpty()) result.add(parts[1]);
        }
        return Collections.unmodifiableList(new ArrayList<String>(result));
    }

    /** Records the latest evaluated value and returns the prior durable observation (false baseline). */
    public synchronized boolean observeStoryStartCondition(UUID playerUuid, String storyId, String triggerPortId,
        boolean value) {
        requireBound();
        if (playerUuid == null || storyId == null || triggerPortId == null)
            throw new IllegalArgumentException("Story Start observation identity is required.");
        String key = playerUuid.toString() + "\u0000" + storyId + "\u0000" + triggerPortId;
        Boolean prior = observedStoryStartConditions.put(key, Boolean.valueOf(value));
        if (prior == null || prior.booleanValue() != value) markDirty();
        return prior != null && prior.booleanValue();
    }

    public synchronized CanonicalStoryInstanceSnapshot setStoryLogicInput(UUID playerUuid, String storyId,
        String portId, boolean value, long eventTime) {
        return setStoryLogicInputs(
            playerUuid,
            storyId,
            java.util.Collections.singletonMap(portId, Boolean.valueOf(value)),
            eventTime);
    }

    public synchronized CanonicalStoryInstanceSnapshot setStoryLogicInputs(UUID playerUuid, String storyId,
        Map<String, Boolean> values, long eventTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        instance.setLogicInputs(values, eventTime);
        storyStore = candidate;
        markWorldIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalStoryInstanceSnapshot getStorySnapshot(UUID playerUuid, String storyId) {
        requireBound();
        return storyStore.getSnapshot(playerUuid, storyId);
    }

    public synchronized List<CanonicalStoryInstanceSnapshot> storySnapshots() {
        requireBound();
        return storyStore.snapshots();
    }

    public synchronized boolean getStorySessionActivationLogic(UUID playerUuid, String storyId) {
        requireStoryBound();
        return requireStoryInstance(storyStore, playerUuid, storyId).getRuntime()
            .getSessionActivationLogic();
    }

    /** Advances the Story cursor and consumes its durable Session handoff as one checkpoint. */
    public synchronized CanonicalStoryInstanceSnapshot resumeStorySession(UUID playerUuid, String storyId,
        long eventTime) {
        requireStoryBound();
        CanonicalStoryPendingContinuation continuation = findContinuation(playerUuid, storyId);
        if (continuation == null) throw new IllegalStateException("Pending Story continuation does not exist.");
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        instance.resumeSession(continuation, eventTime);
        List<CanonicalStoryPendingContinuation> remaining = withoutContinuation(playerUuid, storyId);
        storyStore = candidate;
        continuations = remaining;
        markWorldIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalStoryInstanceSnapshot resumeStoryTask(UUID playerUuid, String storyId,
        CanonicalTaskInstanceSnapshot task, long eventTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        instance.resumeTask(task, eventTime);
        storyStore = candidate;
        markWorldIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalStoryInstanceSnapshot completeStoryTitle(UUID playerUuid, String storyId,
        String nodeId, long eventTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        instance.completeTitle(nodeId, eventTime);
        storyStore = candidate;
        markWorldIfChanged(before);
        return instance.snapshot();
    }

    public synchronized CanonicalStoryInstanceSnapshot completeStoryAction(UUID playerUuid, String storyId,
        String actionNodeId, long eventTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        instance.completeAction(actionNodeId, eventTime);
        storyStore = candidate;
        markWorldIfChanged(before);
        return instance.snapshot();
    }

    public synchronized boolean markStoryError(UUID playerUuid, String storyId, long eventTime) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        CanonicalStoryInstanceStore candidate = cloneStoryStore();
        CanonicalStoryInstance instance = requireStoryInstance(candidate, playerUuid, storyId);
        boolean changed = instance.markError(eventTime);
        if (changed) {
            storyStore = candidate;
            store.cancelByStory(playerUuid, storyId);
            continuations = withoutContinuation(playerUuid, storyId);
            markWorldIfChanged(before);
        }
        return changed;
    }

    /** Atomically removes all Session-side children owned by one player/Story instance. */
    public synchronized boolean cancelStoryChildren(UUID playerUuid, String storyId) {
        requireBound();
        if (playerUuid == null || storyId == null
            || storyId.trim()
                .isEmpty())
            throw new IllegalArgumentException("Player UUID and Story ID are required.");
        NBTTagCompound before = persistedState();
        boolean removedSession = store.cancelByStory(playerUuid, storyId);
        List<CanonicalStoryPendingContinuation> remaining = withoutContinuation(playerUuid, storyId);
        boolean removedContinuation = remaining.size() != continuations.size();
        if (removedContinuation) continuations = remaining;
        if (removedSession || removedContinuation) markWorldIfChanged(before);
        return removedSession || removedContinuation;
    }

    /** Administrative reset of exactly one player's Story and all Session-side children. */
    public synchronized boolean discardByPlayerStory(UUID playerUuid, String storyId) {
        requireStoryBound();
        NBTTagCompound before = persistedState();
        boolean removed = storyStore.discardByPlayerStory(playerUuid, storyId);
        removed = store.cancelByStory(playerUuid, storyId) || removed;
        List<CanonicalStoryPendingContinuation> remaining = withoutContinuation(playerUuid, storyId);
        removed = remaining.size() != continuations.size() || removed;
        continuations = remaining;
        markWorldIfChanged(before);
        return removed;
    }

    /** Atomically persists one routed completion and consumes its completed Session. */
    public synchronized boolean acceptAndConsume(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        requireBound();
        if (completion == null || route == null)
            throw new IllegalArgumentException("Completion and route are required.");
        validateRouteAgainstCompletion(completion, route);
        CanonicalStoryPendingContinuation existing = findContinuation(
            completion.getPlayerUuid(),
            completion.getStoryId());
        if (existing != null) {
            CanonicalStoryPendingContinuation replay = CanonicalStoryPendingContinuation.from(completion, route);
            if (existing.equals(replay)) return true;
            throw new IllegalStateException("Conflicting pending Story continuation.");
        }
        CanonicalSessionInstanceSnapshot snapshot = store.get(completion.getPlayerUuid(), completion.getStoryId())
            == null ? null
                : store.get(completion.getPlayerUuid(), completion.getStoryId())
                    .snapshot();
        CanonicalStoryPendingContinuation candidate = validateAcceptance(completion, route, snapshot);
        List<CanonicalStoryPendingContinuation> updated = new java.util.ArrayList<CanonicalStoryPendingContinuation>(
            continuations);
        updated.add(candidate);
        NBTTagCompound before = persistedState();
        try {
            if (!store.consume(completion.getPlayerUuid(), completion.getStoryId(), completion.getTransportId()))
                throw new IllegalStateException("Completed Session could not be consumed.");
            continuations = updated;
            markDirty();
            return true;
        } catch (RuntimeException exception) {
            markWorldIfChanged(before);
            throw exception;
        } catch (Error error) {
            markWorldIfChanged(before);
            throw error;
        }
    }

    public synchronized boolean acceptCompleted(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        return acceptAndConsume(completion, route);
    }

    public synchronized boolean acceptAndConsumeCompletion(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        return acceptAndConsume(completion, route);
    }

    public synchronized boolean acceptCompletion(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        return acceptAndConsume(completion, route);
    }

    /** Returns a detached copy for diagnostics without exposing mutable pending state. */
    public synchronized NBTTagCompound getPendingRaw() {
        return pendingRaw == null ? null : copy(pendingRaw);
    }

    public synchronized CanonicalSessionInstanceSnapshot start(UUID playerUuid, String storyId,
        String aggregatePlacementId, CanonicalGraphResource resource) {
        ensureNoPendingContinuation(playerUuid, storyId);
        return mutate(new Mutation<CanonicalSessionInstanceSnapshot>() {

            @Override
            public CanonicalSessionInstanceSnapshot run() {
                return store.start(playerUuid, storyId, aggregatePlacementId, resource)
                    .snapshot();
            }
        });
    }

    public synchronized CanonicalSessionInstanceSnapshot start(String playerUuid, String storyId,
        String aggregatePlacementId, CanonicalGraphResource resource) {
        ensureNoPendingContinuation(playerUuid == null ? null : UUID.fromString(playerUuid), storyId);
        return mutate(new Mutation<CanonicalSessionInstanceSnapshot>() {

            @Override
            public CanonicalSessionInstanceSnapshot run() {
                return store.start(playerUuid, storyId, aggregatePlacementId, resource)
                    .snapshot();
            }
        });
    }

    public synchronized CanonicalSessionInstanceSnapshot start(UUID playerUuid, String storyId,
        String aggregatePlacementId, CanonicalGraphResource resource, boolean activationLogic) {
        ensureNoPendingContinuation(playerUuid, storyId);
        return mutate(new Mutation<CanonicalSessionInstanceSnapshot>() {

            @Override
            public CanonicalSessionInstanceSnapshot run() {
                return store.start(playerUuid, storyId, aggregatePlacementId, resource, activationLogic)
                    .snapshot();
            }
        });
    }

    public synchronized CanonicalSessionStep continueLine(UUID playerUuid, String storyId, long transportId,
        String currentNodeId) {
        return mutate(new Mutation<CanonicalSessionStep>() {

            @Override
            public CanonicalSessionStep run() {
                return store.continueLine(playerUuid, storyId, transportId, currentNodeId);
            }
        });
    }

    public synchronized CanonicalSessionStep continueLine(UUID playerUuid, String storyId, long transportId) {
        return mutate(new Mutation<CanonicalSessionStep>() {

            @Override
            public CanonicalSessionStep run() {
                return store.continueLine(playerUuid, storyId, transportId);
            }
        });
    }

    public synchronized CanonicalSessionStep selectChoice(UUID playerUuid, String storyId, long transportId,
        String currentNodeId, String optionId) {
        return mutate(new Mutation<CanonicalSessionStep>() {

            @Override
            public CanonicalSessionStep run() {
                return store.selectChoice(playerUuid, storyId, transportId, currentNodeId, optionId);
            }
        });
    }

    public synchronized CanonicalSessionStep choose(UUID playerUuid, String storyId, long transportId,
        String currentNodeId, String optionId) {
        return selectChoice(playerUuid, storyId, transportId, currentNodeId, optionId);
    }

    public synchronized boolean consume(UUID playerUuid, String storyId, long transportId) {
        return mutateConsume(new Mutation<Boolean>() {

            @Override
            public Boolean run() {
                return Boolean.valueOf(store.consume(playerUuid, storyId, transportId));
            }
        });
    }

    public synchronized CanonicalSessionInstanceSnapshot getSnapshot(UUID playerUuid, String storyId) {
        requireBound();
        CanonicalSessionInstance instance = store.get(playerUuid, storyId);
        return instance == null ? null : instance.snapshot();
    }

    public synchronized CanonicalSessionInstanceSnapshot getInstanceSnapshot(UUID playerUuid, String storyId) {
        return getSnapshot(playerUuid, storyId);
    }

    public synchronized CanonicalSessionInstanceSnapshot snapshot(UUID playerUuid, String storyId) {
        return getSnapshot(playerUuid, storyId);
    }

    public synchronized CanonicalSessionStep getCurrentStep(UUID playerUuid, String storyId) {
        requireBound();
        CanonicalSessionInstance instance = store.get(playerUuid, storyId);
        return instance == null ? null : instance.getCurrentStep();
    }

    public synchronized List<CanonicalSessionInstanceSnapshot> snapshots() {
        requireBound();
        return store.snapshots();
    }

    public synchronized List<CanonicalSessionInstanceSnapshot> snapshotAll() {
        return snapshots();
    }

    public synchronized int size() {
        requireBound();
        return store.size();
    }

    /** Permanently retires every persisted Session/Story/continuation owned by the selected Story IDs. */
    public synchronized DiscardResult discardByStoryIds(Set<String> storyIds) {
        if (storyIds == null) throw new IllegalArgumentException("Story IDs are required.");
        if (storyIds.isEmpty()) return new DiscardResult(0, 0, 0);
        if (pendingRaw != null) return discardPending(storyIds);
        NBTTagCompound before = persistedState();
        int sessionsRemoved = store.discardByStoryIds(storyIds);
        int storiesRemoved = storyStore.discardByStoryIds(storyIds);
        List<CanonicalStoryPendingContinuation> remaining = filterContinuations(continuations, storyIds);
        int continuationsRemoved = continuations.size() - remaining.size();
        if (continuationsRemoved > 0) continuations = remaining;
        int routesRemoved = removeTerminalRoutes(storyIds);
        if (sessionsRemoved > 0 || storiesRemoved > 0 || continuationsRemoved > 0 || routesRemoved > 0)
            markWorldIfChanged(before);
        return new DiscardResult(storiesRemoved, sessionsRemoved, continuationsRemoved);
    }

    private DiscardResult discardPending(Set<String> storyIds) {
        if (pendingLegacy) {
            List<CanonicalSessionInstanceSnapshot> sessions = CanonicalSessionInstanceNbtCodec.decode(pendingRaw);
            List<CanonicalSessionInstanceSnapshot> retained = filterSessions(sessions, storyIds);
            int removed = sessions.size() - retained.size();
            if (removed > 0) {
                pendingRaw = CanonicalSessionInstanceNbtCodec
                    .encode(retained, CanonicalSessionInstanceNbtCodec.nextTransportId(pendingRaw));
                markDirty();
            }
            return new DiscardResult(0, removed, 0);
        }
        CanonicalSessionWorldStateNbtCodec.Decoded decoded = CanonicalSessionWorldStateNbtCodec.decode(pendingRaw);
        List<CanonicalSessionInstanceSnapshot> sessions = filterSessions(decoded.getSessions(), storyIds);
        List<CanonicalStoryInstanceSnapshot> stories = filterStories(decoded.getStories(), storyIds);
        List<CanonicalStoryPendingContinuation> remaining = filterContinuations(decoded.getContinuations(), storyIds);
        List<String> terminalRoutes = filterTerminalRoutes(decoded.getTerminalRoutes(), storyIds);
        List<String> pendingRoutes = filterTerminalRoutes(decoded.getPendingTerminalRoutes(), storyIds);
        Map<String, Boolean> observations = filterStartObservations(decoded.getStartObservations(), storyIds);
        Map<String, String> routeTargets = filterTerminalRouteTargets(decoded.getTerminalRouteTargets(), storyIds);
        DiscardResult result = new DiscardResult(
            decoded.getStories()
                .size() - stories.size(),
            decoded.getSessions()
                .size() - sessions.size(),
            decoded.getContinuations()
                .size() - remaining.size());
        int metadataRemoved = decoded.getTerminalRoutes()
            .size() - terminalRoutes.size()
            + decoded.getPendingTerminalRoutes()
                .size()
            - pendingRoutes.size()
            + decoded.getStartObservations()
                .size()
            - observations.size()
            + decoded.getTerminalRouteTargets()
                .size()
            - routeTargets.size();
        if (result.total() > 0 || metadataRemoved > 0) {
            pendingRaw = CanonicalSessionWorldStateNbtCodec.encode(
                sessions,
                decoded.getNextTransportId(),
                remaining,
                stories,
                terminalRoutes,
                observations,
                pendingRoutes,
                routeTargets);
            markDirty();
        }
        return result;
    }

    private static List<CanonicalSessionInstanceSnapshot> filterSessions(List<CanonicalSessionInstanceSnapshot> source,
        Set<String> storyIds) {
        List<CanonicalSessionInstanceSnapshot> result = new java.util.ArrayList<CanonicalSessionInstanceSnapshot>();
        for (CanonicalSessionInstanceSnapshot value : source)
            if (!storyIds.contains(value.getStoryId())) result.add(value);
        return result;
    }

    private static List<CanonicalStoryInstanceSnapshot> filterStories(List<CanonicalStoryInstanceSnapshot> source,
        Set<String> storyIds) {
        List<CanonicalStoryInstanceSnapshot> result = new java.util.ArrayList<CanonicalStoryInstanceSnapshot>();
        for (CanonicalStoryInstanceSnapshot value : source)
            if (!storyIds.contains(value.getStoryId())) result.add(value);
        return result;
    }

    private static List<CanonicalStoryPendingContinuation> filterContinuations(
        List<CanonicalStoryPendingContinuation> source, Set<String> storyIds) {
        List<CanonicalStoryPendingContinuation> result = new java.util.ArrayList<CanonicalStoryPendingContinuation>();
        for (CanonicalStoryPendingContinuation value : source)
            if (!storyIds.contains(value.getStoryId())) result.add(value);
        return result;
    }

    private static List<String> filterTerminalRoutes(List<String> source, Set<String> storyIds) {
        List<String> result = new java.util.ArrayList<String>();
        for (String route : source) {
            String[] parts = route.split("\\u0000", -1);
            if (parts.length != 4 || !storyIds.contains(parts[1])) result.add(route);
        }
        return result;
    }

    private static Map<String, Boolean> filterStartObservations(Map<String, Boolean> source, Set<String> storyIds) {
        Map<String, Boolean> result = new java.util.LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : source.entrySet()) {
            String[] parts = entry.getKey()
                .split("\\u0000", -1);
            if (parts.length < 3 || !storyIds.contains(parts[1])) result.put(entry.getKey(), entry.getValue());
        }
        return result;
    }

    private static Map<String, String> filterTerminalRouteTargets(Map<String, String> source, Set<String> storyIds) {
        Map<String, String> result = new java.util.LinkedHashMap<String, String>();
        for (Map.Entry<String, String> entry : source.entrySet()) {
            String[] parts = entry.getKey()
                .split("\\u0000", -1);
            if (parts.length != 4 || !storyIds.contains(parts[1])) result.put(entry.getKey(), entry.getValue());
        }
        return result;
    }

    private int removeTerminalRoutes(Set<String> storyIds) {
        int removed = 0;
        removed += removeTerminalRoutes(claimedStoryTerminalRoutes, storyIds);
        removed += removeTerminalRoutes(pendingStoryTerminalRoutes, storyIds);
        java.util.Iterator<String> targets = terminalRouteTargets.keySet()
            .iterator();
        while (targets.hasNext()) {
            String[] parts = targets.next()
                .split("\\u0000", -1);
            if (parts.length == 4 && storyIds.contains(parts[1])) {
                targets.remove();
                removed++;
            }
        }
        java.util.Iterator<String> observations = observedStoryStartConditions.keySet()
            .iterator();
        while (observations.hasNext()) {
            String key = observations.next();
            String[] parts = key.split("\\u0000", -1);
            if (parts.length >= 3 && storyIds.contains(parts[1])) {
                observations.remove();
                removed++;
            }
        }
        return removed;
    }

    private static int removeTerminalRoutes(Set<String> routes, Set<String> storyIds) {
        int removed = 0;
        java.util.Iterator<String> iterator = routes.iterator();
        while (iterator.hasNext()) {
            String route = iterator.next();
            String[] parts = route.split("\\u0000", -1);
            if (parts.length == 4 && storyIds.contains(parts[1])) {
                iterator.remove();
                removed++;
            }
        }
        return removed;
    }

    public static final class DiscardResult {

        private final int storyInstances;
        private final int sessionInstances;
        private final int continuations;

        DiscardResult(int storyInstances, int sessionInstances, int continuations) {
            this.storyInstances = storyInstances;
            this.sessionInstances = sessionInstances;
            this.continuations = continuations;
        }

        public int getStoryInstances() {
            return storyInstances;
        }

        public int getSessionInstances() {
            return sessionInstances;
        }

        public int getContinuations() {
            return continuations;
        }

        public int total() {
            return storyInstances + sessionInstances + continuations;
        }
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Session NBT is required.");
        // Keep the exact detached payload before any strict validation or resolver lookup.
        pendingRaw = copy(root);
        bound = false;
        pendingLegacy = !root.hasKey(CanonicalSessionWorldStateNbtCodec.SESSIONS_KEY)
            && !root.hasKey(CanonicalSessionWorldStateNbtCodec.CONTINUATIONS_KEY);
        // Validate structure now, but defer resource resolution and runtime restore to bind().
        if (pendingLegacy) {
            CanonicalSessionInstanceNbtCodec.decode(root);
            CanonicalSessionInstanceNbtCodec.nextTransportId(root);
        } else CanonicalSessionWorldStateNbtCodec.decode(root);
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        NBTTagCompound output;
        if (bound) output = CanonicalSessionWorldStateNbtCodec.encode(
            store.snapshots(),
            nextTransportId(),
            continuations,
            storyStore.snapshots(),
            claimedStoryTerminalRoutes,
            observedStoryStartConditions,
            pendingStoryTerminalRoutes,
            terminalRouteTargets);
        else if (pendingRaw != null) output = copy(pendingRaw);
        else output = new CanonicalSessionInstanceStore().writeToNbt();
        for (String key : new java.util.HashSet<String>(root.func_150296_c())) root.removeTag(key);
        for (String key : output.func_150296_c()) root.setTag(
            key,
            output.getTag(key)
                .copy());
    }

    private void requireBound() {
        if (!bound) throw new IllegalStateException("Canonical Session data is not bound to a resource resolver.");
    }

    private void requireStoryBound() {
        requireBound();
        if (boundStoryResolver == null)
            throw new IllegalStateException("Canonical Story data is not bound to a resource resolver.");
    }

    private CanonicalStoryInstanceStore cloneStoryStore() {
        CanonicalStoryInstanceStore result = new CanonicalStoryInstanceStore();
        result.readFromNbt(storyStore.writeToNbt(), boundStoryResolver);
        return result;
    }

    private static void restoreStories(CanonicalStoryInstanceStore target,
        List<CanonicalStoryInstanceSnapshot> snapshots, CanonicalStoryResourceResolver resolver) {
        if (snapshots == null || snapshots.isEmpty()) return;
        if (resolver == null)
            throw new IllegalStateException("Canonical Story resource resolver is required for persisted cursors.");
        target.readFromNbt(CanonicalStoryInstanceNbtCodec.encode(snapshots), resolver);
    }

    private static CanonicalStoryInstance requireStoryInstance(CanonicalStoryInstanceStore source, UUID playerUuid,
        String storyId) {
        CanonicalStoryInstance instance = source.get(playerUuid, storyId);
        if (instance == null) throw new IllegalStateException("Canonical Story instance does not exist.");
        return instance;
    }

    private List<CanonicalStoryPendingContinuation> withoutContinuation(UUID playerUuid, String storyId) {
        List<CanonicalStoryPendingContinuation> result = new java.util.ArrayList<CanonicalStoryPendingContinuation>();
        for (CanonicalStoryPendingContinuation continuation : continuations) if (!continuation.getPlayerUuid()
            .equals(playerUuid)
            || !continuation.getStoryId()
                .equals(storyId))
            result.add(continuation);
        return result;
    }

    private static void validateStoryContinuations(CanonicalStoryInstanceStore stories,
        List<CanonicalStoryPendingContinuation> pending) {
        for (CanonicalStoryPendingContinuation continuation : pending) {
            CanonicalStoryInstance story = stories.get(continuation.getPlayerUuid(), continuation.getStoryId());
            // Version 1 checkpoints predate persisted Story cursors; retain their durable handoff for migration.
            if (story == null) continue;
            if (!story.isActive() || story.getRuntime()
                .getWaitKind() != darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.SESSION
                || !continuation.getAggregatePlacementId()
                    .equals(
                        story.getRuntime()
                            .getCurrentNodeId())
                || !continuation.getSessionResourceId()
                    .equals(
                        story.getRuntime()
                            .getWaitResourceId()))
                throw new IllegalStateException("Pending Session handoff contradicts the persisted Story cursor.");
        }
    }

    private long nextTransportId() {
        NBTTagCompound sessions = store.writeToNbt();
        return CanonicalSessionInstanceNbtCodec.nextTransportId(sessions);
    }

    private NBTTagCompound persistedState() {
        NBTTagCompound state = new NBTTagCompound();
        writeToNBT(state);
        return state;
    }

    private CanonicalStoryPendingContinuation findContinuation(UUID playerUuid, String storyId) {
        for (CanonicalStoryPendingContinuation continuation : continuations) if (continuation.getPlayerUuid()
            .equals(playerUuid)
            && continuation.getStoryId()
                .equals(storyId))
            return continuation;
        return null;
    }

    private void ensureNoPendingContinuation(UUID playerUuid, String storyId) {
        requireBound();
        if (findContinuation(playerUuid, storyId) != null)
            throw new IllegalStateException("Pending Story continuation already exists for player/story.");
    }

    private CanonicalStoryPendingContinuation validateAcceptance(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route, CanonicalSessionInstanceSnapshot snapshot) {
        if (snapshot == null) throw new IllegalStateException("Session instance does not exist.");
        if (!completion.getPlayerUuid()
            .equals(snapshot.getPlayerUuid())
            || !completion.getStoryId()
                .equals(snapshot.getStoryId())
            || !completion.getAggregatePlacementId()
                .equals(snapshot.getAggregatePlacementId())
            || !completion.getSessionResourceId()
                .equals(snapshot.getSessionResourceId())
            || completion.getTransportId() != snapshot.getTransportId())
            throw new IllegalStateException("Completion Session identity does not match persisted state.");
        if (snapshot.getRuntimeSnapshot()
            .getStatus() != darkgrey.rpg.session.runtime.CanonicalSessionStatus.COMPLETED
            || !completion.getEndPortId()
                .equals(
                    snapshot.getRuntimeSnapshot()
                        .getFinalEndPortId()))
            throw new IllegalStateException("Completion is not the persisted completed Session state.");
        CanonicalStoryFlowTransition transition = route.getNextFlow();
        validateRouteAgainstCompletion(completion, route);
        if (!completion.getPublicLogicOutputs()
            .equals(
                route.getPublicLogic()
                    .getValues())
            || !snapshot.getRuntimeSnapshot()
                .getPublicLogicOutputs()
                .equals(
                    route.getPublicLogic()
                        .getValues()))
            throw new IllegalStateException("Completion Logic does not match persisted Story route.");
        return CanonicalStoryPendingContinuation.from(completion, route);
    }

    private static void validateRouteAgainstCompletion(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        CanonicalStoryFlowTransition transition = route.getNextFlow();
        if (transition == null || !completion.getStoryId()
            .equals(transition.getStoryId())
            || !completion.getAggregatePlacementId()
                .equals(transition.getAggregatePlacementId())
            || !completion.getEndPortId()
                .equals(transition.getEndPortId())
            || !completion.getPublicLogicOutputs()
                .equals(
                    route.getPublicLogic()
                        .getValues()))
            throw new IllegalStateException("Story route does not match completion identity.");
    }

    private List<CanonicalStoryPendingContinuation> immutableContinuations() {
        List<CanonicalStoryPendingContinuation> result = new java.util.ArrayList<CanonicalStoryPendingContinuation>(
            continuations);
        java.util.Collections.sort(result, new java.util.Comparator<CanonicalStoryPendingContinuation>() {

            @Override
            public int compare(CanonicalStoryPendingContinuation left, CanonicalStoryPendingContinuation right) {
                int result = left.getPlayerUuid()
                    .toString()
                    .compareTo(
                        right.getPlayerUuid()
                            .toString());
                return result == 0 ? left.getStoryId()
                    .compareTo(right.getStoryId()) : result;
            }
        });
        return java.util.Collections.unmodifiableList(result);
    }

    private <T> T mutate(Mutation<T> mutation) {
        requireBound();
        NBTTagCompound before = store.writeToNbt();
        try {
            T result = mutation.run();
            markDirty();
            return result;
        } catch (RuntimeException exception) {
            markIfChanged(before);
            throw exception;
        } catch (Error error) {
            markIfChanged(before);
            throw error;
        }
    }

    private boolean mutateConsume(Mutation<Boolean> mutation) {
        requireBound();
        NBTTagCompound before = store.writeToNbt();
        try {
            boolean result = mutation.run()
                .booleanValue();
            if (result) markDirty();
            return result;
        } catch (RuntimeException exception) {
            markIfChanged(before);
            throw exception;
        } catch (Error error) {
            markIfChanged(before);
            throw error;
        }
    }

    private void markIfChanged(NBTTagCompound before) {
        if (!before.equals(store.writeToNbt())) markDirty();
    }

    private void markWorldIfChanged(NBTTagCompound before) {
        if (!before.equals(persistedState())) markDirty();
    }

    private interface Mutation<T> {

        T run();
    }

    private static NBTTagCompound copy(NBTTagCompound source) {
        return (NBTTagCompound) source.copy();
    }

    private static boolean finite(double value) {
        return !Double.isNaN(value) && !Double.isInfinite(value);
    }
}
