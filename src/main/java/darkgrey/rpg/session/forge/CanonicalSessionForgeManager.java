package darkgrey.rpg.session.forge;

import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRoute;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryResourceResolver;

/** Forge boundary for the canonical server-authoritative Session service. */
public final class CanonicalSessionForgeManager {

    private static final Logger LOG = LogManager.getLogger(CanonicalSessionForgeManager.class);

    /** Small seam used by focused probes; production uses overworld MapStorage. */
    public interface SavedDataProvider {

        CanonicalSessionSavedData get(EntityPlayerMP player, CanonicalSessionResourceResolver resolver);
    }

    /** Small seam used by focused probes; production sends through the canonical channel. */
    public interface Sender {

        void sendFrame(EntityPlayerMP player, CanonicalSessionFrame frame);

        void sendClose(EntityPlayerMP player, CanonicalSessionClose close);
    }

    public interface StoryContinuationListener {

        void onStoryContinuation(EntityPlayerMP player, String storyId);
    }

    private final ProjectRepository projectRepository;
    private final SavedDataProvider savedDataProvider;
    private final Sender sender;
    private StoryContinuationListener storyContinuationListener;

    public CanonicalSessionForgeManager(ProjectRepository projectRepository) {
        this(projectRepository, new SavedDataProvider() {

            @Override
            public CanonicalSessionSavedData get(EntityPlayerMP player, CanonicalSessionResourceResolver resolver) {
                return CanonicalSessionSavedData.get(player);
            }
        }, new Sender() {

            @Override
            public void sendFrame(EntityPlayerMP player, CanonicalSessionFrame frame) {
                DialogueNetwork.CHANNEL.sendTo(frame, player);
            }

            @Override
            public void sendClose(EntityPlayerMP player, CanonicalSessionClose close) {
                DialogueNetwork.CHANNEL.sendTo(close, player);
            }
        });
    }

    public CanonicalSessionForgeManager(ProjectRepository projectRepository, SavedDataProvider savedDataProvider,
        Sender sender) {
        if (projectRepository == null || savedDataProvider == null || sender == null)
            throw new IllegalArgumentException("Canonical Forge Session manager inputs are required.");
        this.projectRepository = projectRepository;
        this.savedDataProvider = savedDataProvider;
        this.sender = sender;
    }

    /** Handles one packet on the server queue and never lets invalid input escape to Netty. */
    public boolean handleAction(EntityPlayerMP player, CanonicalSessionAction action) {
        if (player == null || action == null) {
            LOG.warn("Rejected canonical Session action with missing server identity or action.");
            return false;
        }
        try {
            UUID playerUuid = requirePlayerUuid(player);
            ServiceContext context = context(player);
            CanonicalSessionDispatch dispatch = context.service.dispatch(playerUuid, action);
            send(player, dispatch, context.project, context.savedData);
            return true;
        } catch (RuntimeException exception) {
            LOG.warn(
                "Rejected canonical Session action for player {} and Story {}: {}",
                safePlayer(player),
                safeStory(action),
                exception.getMessage());
            return false;
        }
    }

    /** Starts one canonical Session for a trusted server-side player and routes one client envelope. */
    public boolean start(EntityPlayerMP player, String storyId, String aggregatePlacementId) {
        return start(player, storyId, aggregatePlacementId, false);
    }

    public boolean start(EntityPlayerMP player, String storyId, String aggregatePlacementId,
        final boolean activationLogic) {
        try {
            UUID playerUuid = requirePlayerUuid(player);
            ServiceContext context = context(player);
            CanonicalSessionDispatch dispatch = context.service
                .start(playerUuid, storyId, aggregatePlacementId, activationLogic);
            send(player, dispatch, context.project, context.savedData);
            return true;
        } catch (RuntimeException exception) {
            LOG.warn(
                "Could not start canonical Session for player {} and Story {}: {}",
                safePlayer(player),
                storyId,
                exception.getMessage());
            return false;
        }
    }

    /** Resumes one persisted canonical Session for a trusted server-side player and routes one envelope. */
    public boolean resume(EntityPlayerMP player, String storyId) {
        try {
            UUID playerUuid = requirePlayerUuid(player);
            ServiceContext context = context(player);
            CanonicalSessionDispatch dispatch = context.service.resume(playerUuid, storyId);
            send(player, dispatch, context.project, context.savedData);
            return true;
        } catch (RuntimeException exception) {
            LOG.warn(
                "Could not resume canonical Session for player {} and Story {}: {}",
                safePlayer(player),
                storyId,
                exception.getMessage());
            return false;
        }
    }

    /**
     * Package-private deterministic seam for Forge-bound probes. The UUID is already trusted by the caller; public
     * entry points above always derive it from EntityPlayerMP before reaching the service.
     */
    boolean startTrustedForProbe(UUID trustedPlayerUuid, EntityPlayerMP routePlayer, ProjectSnapshot project,
        CanonicalSessionSavedData savedData, String storyId, String aggregatePlacementId) {
        return routeTrustedForProbe(trustedPlayerUuid, routePlayer, project, savedData, new DispatchOperation() {

            @Override
            public CanonicalSessionDispatch dispatch(CanonicalSessionServerService service, UUID playerUuid) {
                return service.start(playerUuid, storyId, aggregatePlacementId);
            }
        });
    }

    /** Package-private deterministic seam for probing completed Session resume and exclusive Close routing. */
    boolean resumeTrustedForProbe(UUID trustedPlayerUuid, EntityPlayerMP routePlayer, ProjectSnapshot project,
        CanonicalSessionSavedData savedData, String storyId) {
        return routeTrustedForProbe(trustedPlayerUuid, routePlayer, project, savedData, new DispatchOperation() {

            @Override
            public CanonicalSessionDispatch dispatch(CanonicalSessionServerService service, UUID playerUuid) {
                return service.resume(playerUuid, storyId);
            }
        });
    }

    public boolean dispatch(EntityPlayerMP player, CanonicalSessionAction action) {
        return handleAction(player, action);
    }

    public synchronized void bindStoryContinuationListener(StoryContinuationListener listener) {
        if (listener == null) throw new IllegalArgumentException("Story continuation listener is required.");
        if (storyContinuationListener != null && storyContinuationListener != listener)
            throw new IllegalStateException("Story continuation listener is already bound.");
        storyContinuationListener = listener;
    }

    public boolean cancelByStory(EntityPlayerMP player, String storyId) {
        UUID playerUuid = requirePlayerUuid(player);
        ServiceContext context = context(player);
        return context.savedData.cancelStoryChildren(playerUuid, requireText(storyId, "Story ID"));
    }

    private void send(EntityPlayerMP player, CanonicalSessionDispatch dispatch, ProjectSnapshot project,
        CanonicalSessionSavedData savedData) {
        routeAccepted(sender, player, dispatch, project, savedData, storyContinuationListener);
    }

    /** Package-private pure routing seam for probes; no Minecraft process is required. */
    static void route(Sender sender, EntityPlayerMP player, CanonicalSessionDispatch dispatch) {
        if (sender == null) throw new IllegalArgumentException("Canonical Session sender is required.");
        if (dispatch == null) throw new IllegalStateException("Canonical Session dispatch is missing.");
        if (dispatch.isFrame()) {
            sender.sendFrame(player, dispatch.getFrame());
        } else if (dispatch.isClosed()) {
            sender.sendClose(player, dispatch.getClose());
        } else {
            throw new IllegalStateException("Canonical Session dispatch has no client envelope.");
        }
    }

    private ServiceContext context(EntityPlayerMP player) {
        ProjectSnapshot project = projectRepository.getSnapshot();
        if (project == null) throw new IllegalStateException("Canonical project snapshot is unavailable.");
        CanonicalSessionResourceResolver resolver = new CanonicalSessionResourceResolver() {

            @Override
            public darkgrey.rpg.graph.canonical.CanonicalGraphResource resolve(String sessionResourceId) {
                return project.getCanonicalSession(sessionResourceId);
            }
        };
        CanonicalSessionSavedData savedData = savedDataProvider.get(player, resolver);
        if (savedData == null) throw new IllegalStateException("Canonical Session data is unavailable.");
        savedData.bind(resolver, new CanonicalStoryResourceResolver() {

            @Override
            public darkgrey.rpg.graph.canonical.CanonicalGraphResource resolve(String storyId) {
                return project.getCanonicalStory(storyId);
            }
        });
        return new ServiceContext(project, savedData, new CanonicalSessionServerService(project, savedData));
    }

    /** Completion acceptance is the durable precondition for sending Close. */
    private static void routeAccepted(Sender sender, EntityPlayerMP player, CanonicalSessionDispatch dispatch,
        ProjectSnapshot project, CanonicalSessionSavedData savedData, StoryContinuationListener listener) {
        if (dispatch == null) throw new IllegalStateException("Canonical Session dispatch is missing.");
        if (dispatch.isCompleted()) {
            if (project == null || savedData == null) throw new IllegalStateException("Completion context is missing.");
            CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(
                project,
                dispatch.getCompletionResult()
                    .getStoryId()).route(dispatch.getCompletionResult());
            savedData.acceptAndConsume(dispatch.getCompletionResult(), route);
            route(sender, player, dispatch);
            if (listener != null) listener.onStoryContinuation(
                player,
                dispatch.getCompletionResult()
                    .getStoryId());
            return;
        }
        route(sender, player, dispatch);
    }

    private static final class ServiceContext {

        private final ProjectSnapshot project;
        private final CanonicalSessionSavedData savedData;
        private final CanonicalSessionServerService service;

        ServiceContext(ProjectSnapshot project, CanonicalSessionSavedData savedData,
            CanonicalSessionServerService service) {
            this.project = project;
            this.savedData = savedData;
            this.service = service;
        }
    }

    private static UUID requirePlayerUuid(EntityPlayerMP player) {
        if (player == null) throw new IllegalArgumentException("Server-side player is required.");
        UUID playerUuid = player.getUniqueID();
        if (playerUuid == null) throw new IllegalStateException("Server-side player UUID is unavailable.");
        return playerUuid;
    }

    private boolean routeTrustedForProbe(UUID trustedPlayerUuid, EntityPlayerMP routePlayer, ProjectSnapshot project,
        CanonicalSessionSavedData savedData, DispatchOperation operation) {
        try {
            if (trustedPlayerUuid == null || project == null || savedData == null || operation == null)
                throw new IllegalArgumentException("Trusted Session probe inputs are required.");
            CanonicalSessionDispatch dispatch = operation
                .dispatch(new CanonicalSessionServerService(project, savedData), trustedPlayerUuid);
            routeAccepted(sender, routePlayer, dispatch, project, savedData, storyContinuationListener);
            return true;
        } catch (RuntimeException exception) {
            LOG.warn("Canonical Session probe operation failed: {}", exception.getMessage());
            return false;
        }
    }

    private interface DispatchOperation {

        CanonicalSessionDispatch dispatch(CanonicalSessionServerService service, UUID playerUuid);
    }

    private static Object safePlayer(EntityPlayerMP player) {
        if (player == null) return "<missing>";
        try {
            return player.getUniqueID();
        } catch (RuntimeException exception) {
            return "<unavailable>";
        }
    }

    private static String safeStory(CanonicalSessionAction action) {
        return action == null || action.getStoryId() == null ? "<unknown>" : action.getStoryId();
    }

    private static String requireText(String value, String label) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(label + " is required.");
        return value.trim();
    }
}
