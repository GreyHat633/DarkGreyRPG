package darkgrey.rpg.task.forge;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalProjector;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskWorldLogicBindings;

/** Server-side Forge owner for canonical Task data, dispatch, and read projections. */
public final class CanonicalTaskForgeManager {

    private static final Logger LOG = LogManager.getLogger(CanonicalTaskForgeManager.class);

    /** Production resolves overworld WorldSavedData; probes can provide an isolated ledger. */
    public interface SavedDataProvider {

        CanonicalTaskSavedData get(EntityPlayerMP player, CanonicalTaskResourceResolver resolver);
    }

    public interface StorySettlementListener {

        void onStoryTaskSettled(EntityPlayerMP player, CanonicalTaskInstanceSnapshot task);
    }

    private final ProjectRepository projectRepository;
    private final SavedDataProvider savedDataProvider;
    private StorySettlementListener storySettlementListener;

    public CanonicalTaskForgeManager(ProjectRepository projectRepository) {
        this(projectRepository, new SavedDataProvider() {

            @Override
            public CanonicalTaskSavedData get(EntityPlayerMP player, CanonicalTaskResourceResolver resolver) {
                // Deliberately do not call the resolver overload: binding is fenced below and happens once.
                return CanonicalTaskSavedData.get(player);
            }
        });
    }

    public CanonicalTaskForgeManager(ProjectRepository projectRepository, SavedDataProvider savedDataProvider) {
        if (projectRepository == null || savedDataProvider == null)
            throw new IllegalArgumentException("Canonical Forge Task manager inputs are required.");
        this.projectRepository = projectRepository;
        this.savedDataProvider = savedDataProvider;
    }

    /** Starts or re-enters one current canonical Task placement and returns a detached snapshot. */
    public CanonicalTaskInstanceSnapshot start(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId, String taskResourceId) {
        UUID playerUuid = requirePlayerUuid(player);
        String story = requireText(storyInstanceId, "Story instance ID");
        String placement = requireText(taskNodePlacementId, "Task node placement ID");
        String resourceId = requireText(taskResourceId, "Task resource ID");
        Context context = context(player);
        CanonicalGraphResource resource = currentTask(context.project, resourceId);
        CanonicalTaskInstanceSnapshot snapshot = context.data.start(playerUuid, story, placement, resource);
        snapshot = synchronizeWorldLogic(
            playerUuid,
            context.project,
            context.data,
            snapshot,
            player.worldObj.getWorldTime());
        LOG.debug("Canonical Task {} started/re-entered for player {}", resourceId, playerUuid);
        return snapshot;
    }

    public CanonicalTaskInstanceSnapshot startTask(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId, String taskResourceId) {
        return start(player, storyInstanceId, taskNodePlacementId, taskResourceId);
    }

    public CanonicalTaskInstanceSnapshot startCanonicalTask(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId, String taskResourceId) {
        return start(player, storyInstanceId, taskNodePlacementId, taskResourceId);
    }

    /** Returns one detached placement snapshot, resolving only its current data context. */
    public CanonicalTaskInstanceSnapshot snapshot(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId) {
        String story = requireText(storyInstanceId, "Story instance ID");
        String placement = requireText(taskNodePlacementId, "Task node placement ID");
        Context context = context(player);
        return context.data.getSnapshot(requirePlayerUuid(player), story, placement);
    }

    public CanonicalTaskInstanceSnapshot getSnapshot(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId) {
        return snapshot(player, storyInstanceId, taskNodePlacementId);
    }

    public CanonicalTaskInstanceSnapshot snapshotTask(EntityPlayerMP player, String storyInstanceId,
        String taskNodePlacementId) {
        return snapshot(player, storyInstanceId, taskNodePlacementId);
    }

    /** Dispatches only to the SavedData inverted index; it never invokes Story or legacy Quest code. */
    public CanonicalTaskDispatchResult dispatch(EntityPlayerMP player, CanonicalTaskEvent event) {
        UUID playerUuid = requirePlayerUuid(player);
        if (event == null) throw new IllegalArgumentException("Canonical Task event is required.");
        try {
            CanonicalTaskDispatchResult result = context(player).data.dispatch(playerUuid, event);
            LOG.debug(
                "Canonical Task event {} for player {}: candidates={}, changed={}, settled={}, errors={}",
                event.getType(),
                playerUuid,
                result.getCandidateCount(),
                result.getChangedInstanceCount(),
                result.getSettledInstances()
                    .size(),
                result.getErroredInstances()
                    .size());
            StorySettlementListener listener = storySettlementListener;
            if (listener != null) for (CanonicalTaskInstanceSnapshot settled : result.getSettledInstances())
                listener.onStoryTaskSettled(player, settled);
            return result;
        } catch (RuntimeException failure) {
            LOG.warn(
                "Canonical Task event {} rejected for player {}: {}",
                event.getType(),
                playerUuid,
                failure.getMessage());
            throw failure;
        }
    }

    /** Detached per-player snapshots for a future UI boundary. */
    public List<CanonicalTaskInstanceSnapshot> snapshots(EntityPlayerMP player) {
        UUID playerUuid = requirePlayerUuid(player);
        List<CanonicalTaskInstanceSnapshot> result = new ArrayList<CanonicalTaskInstanceSnapshot>();
        for (CanonicalTaskInstanceSnapshot snapshot : context(player).data.snapshots())
            if (playerUuid.equals(snapshot.getPlayerUuid())) result.add(snapshot);
        return Collections.unmodifiableList(result);
    }

    /** Synchronizes formal Minecraft-backed Logic Inputs for every active Task owned by this player. */
    public void synchronizeWorldLogic(EntityPlayerMP player) {
        Context context = context(player);
        UUID playerUuid = requirePlayerUuid(player);
        List<CanonicalTaskInstanceSnapshot> snapshots = new ArrayList<CanonicalTaskInstanceSnapshot>(
            context.data.snapshots());
        for (CanonicalTaskInstanceSnapshot snapshot : snapshots) if (playerUuid.equals(snapshot.getPlayerUuid())
            && snapshot.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE)
            synchronizeWorldLogic(player, context, snapshot);
    }

    public List<CanonicalTaskInstanceSnapshot> snapshotPlayer(EntityPlayerMP player) {
        return snapshots(player);
    }

    /** Canonical Journal projection; legacy Quest Journal remains untouched. */
    public List<CanonicalTaskJournalEntry> journal(EntityPlayerMP player) {
        Context context = context(player);
        return CanonicalTaskJournalProjector
            .project(requirePlayerUuid(player), context.data.snapshots(), context.resolver);
    }

    public List<CanonicalTaskJournalEntry> journalSnapshot(EntityPlayerMP player) {
        return journal(player);
    }

    public List<CanonicalTaskJournalEntry> getJournal(EntityPlayerMP player) {
        return journal(player);
    }

    public synchronized void bindStorySettlementListener(StorySettlementListener listener) {
        if (listener == null) throw new IllegalArgumentException("Story settlement listener is required.");
        if (storySettlementListener != null && storySettlementListener != listener)
            throw new IllegalStateException("Story settlement listener is already bound.");
        storySettlementListener = listener;
    }

    public int cancelByStory(EntityPlayerMP player, String storyId) {
        Context context = context(player);
        return context.data.cancelByStory(requirePlayerUuid(player), requireText(storyId, "Story ID"));
    }

    /** Package-private seam for focused probes that cannot construct a live EntityPlayerMP. */
    CanonicalTaskInstanceSnapshot startTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data, String storyInstanceId, String taskNodePlacementId, String taskResourceId) {
        if (trustedPlayerUuid == null || project == null || data == null)
            throw new IllegalArgumentException("Trusted Task probe inputs are required.");
        String story = requireText(storyInstanceId, "Story instance ID");
        String placement = requireText(taskNodePlacementId, "Task node placement ID");
        String resourceId = requireText(taskResourceId, "Task resource ID");
        CanonicalTaskResourceResolver resolver = resolver(project);
        bindIfNeeded(data, resolver);
        return data.start(trustedPlayerUuid, story, placement, currentTask(project, resourceId));
    }

    CanonicalTaskDispatchResult dispatchTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data, CanonicalTaskEvent event) {
        if (trustedPlayerUuid == null || project == null || data == null || event == null)
            throw new IllegalArgumentException("Trusted Task probe inputs are required.");
        CanonicalTaskResourceResolver resolver = resolver(project);
        bindIfNeeded(data, resolver);
        return data.dispatch(trustedPlayerUuid, event);
    }

    CanonicalTaskInstanceSnapshot snapshotTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data, String storyInstanceId, String taskNodePlacementId) {
        if (trustedPlayerUuid == null || project == null || data == null)
            throw new IllegalArgumentException("Trusted Task probe inputs are required.");
        String story = requireText(storyInstanceId, "Story instance ID");
        String placement = requireText(taskNodePlacementId, "Task node placement ID");
        bindIfNeeded(data, resolver(project));
        return data.getSnapshot(trustedPlayerUuid, story, placement);
    }

    List<CanonicalTaskInstanceSnapshot> snapshotsTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data) {
        if (trustedPlayerUuid == null || project == null || data == null)
            throw new IllegalArgumentException("Trusted Task probe inputs are required.");
        bindIfNeeded(data, resolver(project));
        List<CanonicalTaskInstanceSnapshot> result = new ArrayList<CanonicalTaskInstanceSnapshot>();
        for (CanonicalTaskInstanceSnapshot snapshot : data.snapshots())
            if (trustedPlayerUuid.equals(snapshot.getPlayerUuid())) result.add(snapshot);
        return Collections.unmodifiableList(result);
    }

    List<CanonicalTaskJournalEntry> journalTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data) {
        if (trustedPlayerUuid == null || project == null || data == null)
            throw new IllegalArgumentException("Trusted Task probe inputs are required.");
        CanonicalTaskResourceResolver resolver = resolver(project);
        bindIfNeeded(data, resolver);
        return CanonicalTaskJournalProjector.project(trustedPlayerUuid, data.snapshots(), resolver);
    }

    CanonicalTaskInstanceSnapshot synchronizeWorldLogicTrustedForProbe(UUID trustedPlayerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data, CanonicalTaskInstanceSnapshot snapshot, long worldTime) {
        if (trustedPlayerUuid == null || project == null || data == null || snapshot == null)
            throw new IllegalArgumentException("Trusted Task world Logic probe inputs are required.");
        bindIfNeeded(data, resolver(project));
        return synchronizeWorldLogic(trustedPlayerUuid, project, data, snapshot, worldTime);
    }

    private CanonicalTaskInstanceSnapshot synchronizeWorldLogic(EntityPlayerMP player, Context context,
        CanonicalTaskInstanceSnapshot snapshot) {
        CanonicalTaskInstanceSnapshot result = synchronizeWorldLogic(
            requirePlayerUuid(player),
            context.project,
            context.data,
            snapshot,
            player.worldObj.getWorldTime());
        if (snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED
            && result.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED) {
            StorySettlementListener listener = storySettlementListener;
            if (listener != null) listener.onStoryTaskSettled(player, result);
        }
        return result;
    }

    private CanonicalTaskInstanceSnapshot synchronizeWorldLogic(UUID playerUuid, ProjectSnapshot project,
        CanonicalTaskSavedData data, CanonicalTaskInstanceSnapshot snapshot, long worldTime) {
        CanonicalGraphResource resource = currentTask(project, snapshot.getTaskResourceId());
        CanonicalTaskInstanceSnapshot current = snapshot;
        for (CanonicalTaskWorldLogicBindings.Binding binding : CanonicalTaskWorldLogicBindings.parse(resource)) {
            if (current.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) break;
            current = data.setLogicInput(
                playerUuid,
                current.getStoryInstanceId(),
                current.getTaskNodePlacementId(),
                binding.getPortId(),
                CanonicalTaskWorldLogicBindings.value(binding.getSource(), worldTime),
                Math.max(System.currentTimeMillis(), current.getActivationTime()));
        }
        return current;
    }

    private Context context(EntityPlayerMP player) {
        requirePlayerUuid(player);
        ProjectSnapshot project = projectRepository.getSnapshot();
        if (project == null) throw new IllegalStateException("Canonical project snapshot is unavailable.");
        CanonicalTaskResourceResolver resolver = new CanonicalTaskResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String taskResourceId) {
                return project.getCanonicalTask(taskResourceId);
            }
        };
        CanonicalTaskSavedData data = savedDataProvider.get(player, resolver);
        if (data == null) throw new IllegalStateException("Canonical Task data is unavailable.");
        bindIfNeeded(data, resolver);
        return new Context(project, resolver, data);
    }

    private static void bindIfNeeded(CanonicalTaskSavedData data, CanonicalTaskResourceResolver resolver) {
        if (!data.isBound()) data.bind(resolver);
    }

    private static CanonicalTaskResourceResolver resolver(final ProjectSnapshot project) {
        return new CanonicalTaskResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String taskResourceId) {
                return project.getCanonicalTask(taskResourceId);
            }
        };
    }

    private static CanonicalGraphResource currentTask(ProjectSnapshot project, String resourceId) {
        String id = requireText(resourceId, "Task resource ID");
        CanonicalGraphResource resource = project.getCanonicalTask(id);
        if (resource == null) throw new IllegalArgumentException("Missing canonical Task resource: " + id);
        if (resource.getResourceKind() != CanonicalGraphResourceKind.TASK)
            throw new IllegalArgumentException("Resource is not a canonical Task: " + id);
        return resource;
    }

    private static UUID requirePlayerUuid(EntityPlayerMP player) {
        if (player == null) throw new IllegalArgumentException("Server-side player is required.");
        UUID uuid = player.getUniqueID();
        if (uuid == null) throw new IllegalStateException("Server-side player UUID is unavailable.");
        return uuid;
    }

    private static String requireText(String value, String label) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(label + " is required.");
        return value.trim();
    }

    private static final class Context {

        private final ProjectSnapshot project;
        private final CanonicalTaskResourceResolver resolver;
        private final CanonicalTaskSavedData data;

        Context(ProjectSnapshot project, CanonicalTaskResourceResolver resolver, CanonicalTaskSavedData data) {
            this.project = project;
            this.resolver = resolver;
            this.data = data;
        }
    }
}
