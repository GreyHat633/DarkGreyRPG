package darkgrey.rpg.story.canonical.forge;

import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryTriggerIndex;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatch;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatchKind;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;

/** Forge coordinator for Story triggers and Session/Task aggregate boundaries. */
public final class CanonicalStoryForgeManager implements CanonicalSessionForgeManager.StoryContinuationListener,
    CanonicalTaskForgeManager.StorySettlementListener {

    private static final Logger LOG = LogManager.getLogger(CanonicalStoryForgeManager.class);
    private static final int MAX_EXTERNAL_CHAIN = 256;

    public interface SavedDataProvider {

        CanonicalSessionSavedData get(EntityPlayerMP player);
    }

    public interface ActionExecutor {

        boolean execute(EntityPlayerMP player, CanonicalStoryDispatch action);
    }

    /** Server-neutral aggregate seam used to verify coordinator routing without a Minecraft process. */
    interface AggregateGateway {

        boolean startSession(String storyId, String placementId, boolean activationLogic);

        CanonicalTaskInstanceSnapshot startTask(String storyId, String placementId, String resourceId);

        boolean executeAction(CanonicalStoryDispatch action);

        void cleanup(String storyId);
    }

    private final ProjectRepository projectRepository;
    private final CanonicalSessionForgeManager sessions;
    private final CanonicalTaskForgeManager tasks;
    private final SavedDataProvider savedDataProvider;
    private final ActionExecutor actionExecutor;
    private ProjectSnapshot indexedProject;
    private CanonicalStoryTriggerIndex triggerIndex;
    private RuntimeException triggerIndexFailure;
    private boolean triggerIndexFailureReported;

    public CanonicalStoryForgeManager(ProjectRepository projectRepository, CanonicalSessionForgeManager sessions,
        CanonicalTaskForgeManager tasks) {
        this(projectRepository, sessions, tasks, new SavedDataProvider() {

            @Override
            public CanonicalSessionSavedData get(EntityPlayerMP player) {
                return CanonicalSessionSavedData.get(player);
            }
        }, new CanonicalStoryForgeActionExecutor());
    }

    public CanonicalStoryForgeManager(ProjectRepository projectRepository, CanonicalSessionForgeManager sessions,
        CanonicalTaskForgeManager tasks, SavedDataProvider savedDataProvider, ActionExecutor actionExecutor) {
        if (projectRepository == null || sessions == null
            || tasks == null
            || savedDataProvider == null
            || actionExecutor == null)
            throw new IllegalArgumentException("Canonical Forge Story manager inputs are required.");
        this.projectRepository = projectRepository;
        this.sessions = sessions;
        this.tasks = tasks;
        this.savedDataProvider = savedDataProvider;
        this.actionExecutor = actionExecutor;
    }

    public void bindAggregateListeners() {
        sessions.bindStoryContinuationListener(this);
        tasks.bindStorySettlementListener(this);
    }

    public boolean startByEntry(EntityPlayerMP player, String storyId) {
        try {
            Context context = context(player);
            return route(player, context, context.service.startByEntry(requirePlayerUuid(player), storyId, now()));
        } catch (RuntimeException exception) {
            return failAndCleanup(player, storyId, "entry", exception);
        }
    }

    public boolean startByActor(EntityPlayerMP player, String storyId, String actorId) {
        try {
            Context context = context(player);
            return route(
                player,
                context,
                context.service.startByActor(requirePlayerUuid(player), storyId, actorId, now()));
        } catch (RuntimeException exception) {
            return failAndCleanup(player, storyId, "actor", exception);
        }
    }

    public boolean startByRegion(EntityPlayerMP player, String storyId, int dimension, double x, double y, double z) {
        try {
            Context context = context(player);
            return route(
                player,
                context,
                context.service.startByRegion(requirePlayerUuid(player), storyId, dimension, x, y, z, now()));
        } catch (RuntimeException exception) {
            return failAndCleanup(player, storyId, "region", exception);
        }
    }

    /** Routes an ActorInteract event to a waiting cursor before considering any Start trigger. */
    public boolean handleActorInteraction(EntityPlayerMP player, String actorId) {
        try {
            Context context = context(player);
            UUID playerUuid = requirePlayerUuid(player);
            CanonicalStoryDispatch continuation = context.service.resumeActor(playerUuid, actorId, now());
            if (continuation != null) return route(player, context, continuation);
            boolean started = false;
            for (CanonicalStoryTriggerIndex.Match match : matchingActorTriggers(actorId))
                started = startByActor(player, match.getStoryId(), actorId) || started;
            return started;
        } catch (RuntimeException exception) {
            return failAndCleanupEvent(player, actorId, true, 0, 0D, 0D, 0D, exception);
        }
    }

    public boolean onActorInteract(EntityPlayerMP player, String actorId) {
        return handleActorInteraction(player, actorId);
    }

    /** Routes a periodic PlayerPosition event to a waiting region cursor before Start edge matches. */
    public boolean handleRegionPosition(EntityPlayerMP player, int dimension, double x, double y, double z,
        List<CanonicalStoryTriggerIndex.Match> enteredStarts) {
        try {
            Context context = context(player);
            CanonicalStoryDispatch continuation = context.service
                .resumeRegion(requirePlayerUuid(player), dimension, x, y, z, now());
            if (continuation != null) return route(player, context, continuation);
            boolean started = false;
            if (enteredStarts != null) for (CanonicalStoryTriggerIndex.Match match : enteredStarts)
                started = startByRegion(player, match.getStoryId(), dimension, x, y, z) || started;
            return started;
        } catch (RuntimeException exception) {
            return failAndCleanupEvent(player, null, false, dimension, x, y, z, exception);
        }
    }

    public boolean onEnterRegion(EntityPlayerMP player, int dimension, double x, double y, double z,
        List<CanonicalStoryTriggerIndex.Match> enteredStarts) {
        return handleRegionPosition(player, dimension, x, y, z, enteredStarts);
    }

    public List<CanonicalStoryTriggerIndex.Match> matchingActorTriggers(String actorId) {
        try {
            CanonicalStoryTriggerIndex index = triggerIndex();
            return index == null ? Collections.<CanonicalStoryTriggerIndex.Match>emptyList()
                : index.matchActor(actorId);
        } catch (RuntimeException exception) {
            reportTriggerQueryFailure("actor", exception);
            return Collections.emptyList();
        }
    }

    public List<CanonicalStoryTriggerIndex.Match> matchingRegionTriggers(int dimension, double x, double y, double z) {
        try {
            CanonicalStoryTriggerIndex index = triggerIndex();
            return index == null ? Collections.<CanonicalStoryTriggerIndex.Match>emptyList()
                : index.matchRegion(dimension, x, y, z);
        } catch (RuntimeException exception) {
            reportTriggerQueryFailure("region", exception);
            return Collections.emptyList();
        }
    }

    public boolean resume(EntityPlayerMP player, String storyId) {
        try {
            Context context = context(player);
            UUID playerUuid = requirePlayerUuid(player);
            CanonicalStoryDispatch dispatch = context.service.snapshot(playerUuid, storyId);
            if (dispatch == null) return false;
            return route(player, context, dispatch);
        } catch (RuntimeException exception) {
            return failAndCleanup(player, storyId, "resume", exception);
        }
    }

    @Override
    public void onStoryContinuation(EntityPlayerMP player, String storyId) {
        try {
            Context context = context(player);
            route(player, context, context.service.resumeSession(requirePlayerUuid(player), storyId, now()));
        } catch (RuntimeException exception) {
            failAndCleanup(player, storyId, "Session continuation", exception);
        }
    }

    @Override
    public void onStoryTaskSettled(EntityPlayerMP player, CanonicalTaskInstanceSnapshot task) {
        if (task == null) throw new IllegalArgumentException("Settled canonical Task is required.");
        String storyId = task.getStoryInstanceId();
        try {
            Context context = context(player);
            route(player, context, context.service.resumeTask(requirePlayerUuid(player), storyId, task, now()));
        } catch (RuntimeException exception) {
            failAndCleanup(player, storyId, "Task continuation", exception);
        }
    }

    private boolean route(EntityPlayerMP player, Context context, CanonicalStoryDispatch first) {
        final EntityPlayerMP routePlayer = player;
        return routeTrusted(requirePlayerUuid(player), context.service, context.data, first, new AggregateGateway() {

            @Override
            public boolean startSession(String storyId, String placementId, boolean activationLogic) {
                return sessions.start(routePlayer, storyId, placementId, activationLogic);
            }

            @Override
            public CanonicalTaskInstanceSnapshot startTask(String storyId, String placementId, String resourceId) {
                return tasks.start(routePlayer, storyId, placementId, resourceId);
            }

            @Override
            public boolean executeAction(CanonicalStoryDispatch action) {
                return actionExecutor.execute(routePlayer, action);
            }

            @Override
            public void cleanup(String storyId) {
                CanonicalStoryForgeManager.this.cleanup(routePlayer, storyId);
            }
        });
    }

    static boolean routeTrusted(UUID playerUuid, CanonicalStoryServerService service, CanonicalSessionSavedData data,
        CanonicalStoryDispatch first, AggregateGateway gateway) {
        if (playerUuid == null || service == null || data == null || first == null || gateway == null)
            throw new IllegalArgumentException("Trusted canonical Story routing inputs are required.");
        CanonicalStoryDispatch dispatch = first;
        String routedStoryId = first.getSnapshot()
            .getStoryId();
        try {
            for (int step = 0; step < MAX_EXTERNAL_CHAIN; step++) {
                CanonicalStoryDispatchKind kind = dispatch.getKind();
                String storyId = dispatch.getSnapshot()
                    .getStoryId();
                routedStoryId = storyId;
                if (kind == CanonicalStoryDispatchKind.SESSION) {
                    if (data.getPendingContinuation(playerUuid, storyId) != null) {
                        dispatch = service.resumeSession(playerUuid, storyId, now());
                        continue;
                    }
                    if (!gateway.startSession(storyId, dispatch.getPlacementId(), dispatch.getActivationLogic()))
                        throw new IllegalStateException(
                            "Canonical Story Session was not started: " + dispatch.getPlacementId());
                    return true;
                }
                if (kind == CanonicalStoryDispatchKind.TASK) {
                    CanonicalTaskInstanceSnapshot task = gateway
                        .startTask(storyId, dispatch.getPlacementId(), dispatch.getResourceId());
                    if (task == null) throw new IllegalStateException("Canonical Story Task start returned no state.");
                    if (task.getStatus() == CanonicalTaskInstanceStatus.SETTLED) {
                        dispatch = service.resumeTask(playerUuid, storyId, task, now());
                        continue;
                    }
                    return true;
                }
                if (kind == CanonicalStoryDispatchKind.ACTION) {
                    if (!gateway.executeAction(dispatch)) throw new IllegalStateException(
                        "Canonical Story action was not executed: " + dispatch.getPlacementId());
                    dispatch = service.completeAction(playerUuid, storyId, dispatch.getPlacementId(), now());
                    continue;
                }
                if (kind == CanonicalStoryDispatchKind.ACTOR_INTERACT
                    || kind == CanonicalStoryDispatchKind.INTERACT_ACTOR
                    || kind == CanonicalStoryDispatchKind.ENTER_REGION) {
                    // A Start trigger may encounter an already-active cursor waiting for a later event.
                    // It is an idempotent no-op; only the event-first branch above advances it.
                    return true;
                }
                if (kind == CanonicalStoryDispatchKind.TRANSFERRED) {
                    gateway.cleanup(storyId);
                    routedStoryId = dispatch.getTargetStoryId();
                    dispatch = service.startByEntry(playerUuid, routedStoryId, now());
                    continue;
                }
                if (kind == CanonicalStoryDispatchKind.TERMINATED) {
                    gateway.cleanup(storyId);
                    return true;
                }
                if (kind == CanonicalStoryDispatchKind.ERROR) {
                    gateway.cleanup(storyId);
                    return false;
                }
                throw new IllegalStateException("Unsupported canonical Story dispatch: " + kind);
            }
            throw new IllegalStateException("Canonical Story external dispatch chain exceeded its safety bound.");
        } catch (RuntimeException failure) {
            try {
                if (routedStoryId != null && data.getStorySnapshot(playerUuid, routedStoryId) != null)
                    data.markStoryError(playerUuid, routedStoryId, now());
                if (routedStoryId != null) gateway.cleanup(routedStoryId);
            } catch (RuntimeException cleanupFailure) {
                failure.addSuppressed(cleanupFailure);
            }
            throw failure;
        }
    }

    private void cleanup(EntityPlayerMP player, String storyId) {
        sessions.cancelByStory(player, storyId);
        tasks.cancelByStory(player, storyId);
    }

    private Context context(EntityPlayerMP player) {
        requirePlayerUuid(player);
        ProjectSnapshot project = projectRepository.getSnapshot();
        if (project == null) throw new IllegalStateException("Canonical project snapshot is unavailable.");
        CanonicalSessionSavedData data = savedDataProvider.get(player);
        if (data == null) throw new IllegalStateException("Canonical Story world data is unavailable.");
        return new Context(project, data, new CanonicalStoryServerService(project, data));
    }

    private synchronized CanonicalStoryTriggerIndex triggerIndex() {
        ProjectSnapshot project = projectRepository.getSnapshot();
        if (project == null) throw new IllegalStateException("Canonical project snapshot is unavailable.");
        if (project != indexedProject) {
            indexedProject = project;
            triggerIndex = null;
            triggerIndexFailure = null;
            triggerIndexFailureReported = false;
            try {
                triggerIndex = CanonicalStoryTriggerIndex.build(project);
            } catch (RuntimeException exception) {
                triggerIndexFailure = exception;
            }
        }
        if (triggerIndexFailure != null) {
            if (!triggerIndexFailureReported) {
                triggerIndexFailureReported = true;
                LOG.warn("Canonical Story trigger index is unavailable: {}", triggerIndexFailure.getMessage());
            }
            return null;
        }
        return triggerIndex;
    }

    private void reportTriggerQueryFailure(String kind, RuntimeException exception) {
        LOG.warn("Canonical Story {} trigger query failed: {}", kind, exception.getMessage());
    }

    private static UUID requirePlayerUuid(EntityPlayerMP player) {
        if (player == null) throw new IllegalArgumentException("Server-side player is required.");
        UUID uuid = player.getUniqueID();
        if (uuid == null) throw new IllegalStateException("Server-side player UUID is unavailable.");
        return uuid;
    }

    private static long now() {
        return System.currentTimeMillis();
    }

    private boolean failAndCleanup(EntityPlayerMP player, String storyId, String trigger, RuntimeException exception) {
        try {
            if (player != null && storyId != null
                && !storyId.trim()
                    .isEmpty()) {
                Context context = context(player);
                UUID playerUuid = requirePlayerUuid(player);
                if (context.data.getStorySnapshot(playerUuid, storyId) != null)
                    context.data.markStoryError(playerUuid, storyId, now());
                cleanup(player, storyId);
            }
        } catch (RuntimeException cleanupFailure) {
            LOG.warn(
                "Canonical Story failure cleanup also failed for Story {}: {}",
                storyId,
                cleanupFailure.getMessage());
        }
        LOG.warn(
            "Canonical Story {} operation failed for player {} and Story {}: {}",
            trigger,
            player == null ? "<missing>" : player.getUniqueID(),
            storyId,
            exception.getMessage());
        return false;
    }

    private boolean failAndCleanupEvent(EntityPlayerMP player, String actorId, boolean actor, int dimension, double x,
        double y, double z, RuntimeException exception) {
        try {
            Context context = context(player);
            UUID playerUuid = requirePlayerUuid(player);
            List<darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot> matches = actor
                ? context.data.matchingStoryActorWaits(playerUuid, actorId)
                : context.data.matchingStoryRegionWaits(playerUuid, dimension, x, y, z);
            for (darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot snapshot : matches)
                failAndCleanup(player, snapshot.getStoryId(), actor ? "ActorInteract" : "EnterRegion", exception);
        } catch (RuntimeException cleanupFailure) {
            LOG.warn("Canonical Story event failure cleanup also failed: {}", cleanupFailure.getMessage());
        }
        LOG.warn("Canonical Story event routing failed: {}", exception.getMessage());
        return false;
    }

    private static final class Context {

        private final CanonicalSessionSavedData data;
        private final CanonicalStoryServerService service;

        Context(ProjectSnapshot project, CanonicalSessionSavedData data, CanonicalStoryServerService service) {
            this.data = data;
            this.service = service;
        }
    }
}
