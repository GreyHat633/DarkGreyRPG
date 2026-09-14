package darkgrey.rpg.task.forge;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.util.MathHelper;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame;
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
    private final CanonicalTaskSubmitChoiceStore submitChoices = new CanonicalTaskSubmitChoiceStore();
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

    /**
     * Handles one real server-side entity interaction. Submit objectives are
     * deliberately resolved from the actual entity identity and the player's
     * current active snapshot; no client supplied actor or task identity is
     * trusted for the inventory transaction.
     */
    public boolean handleEntityInteraction(EntityPlayerMP player, Entity target) {
        if (player == null || target == null || player instanceof net.minecraftforge.common.util.FakePlayer)
            return false;
        if (target.isDead || player.worldObj != target.worldObj
            || player.getDistanceSqToEntity(target) > 36D
            || !player.canEntityBeSeen(target)) return false;
        java.util.List<String> actorIds = EntityDgrIdentityResolver.resolveActorIds(target);
        if (actorIds.isEmpty()) return false;
        Context context = context(player);
        UUID uuid = requirePlayerUuid(player);
        java.util.List<CanonicalTaskSubmitChoiceStore.Candidate> candidates = new ArrayList<CanonicalTaskSubmitChoiceStore.Candidate>();
        for (CanonicalTaskInstanceSnapshot snapshot : context.data.snapshots()) {
            if (!uuid.equals(snapshot.getPlayerUuid())
                || snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) continue;
            CanonicalGraphResource resource = currentTask(context.project, snapshot.getTaskResourceId());
            for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
                .getNodes()) {
                if (!"objective".equals(node.getType()) || !CanonicalTaskEvent.SUBMIT_ITEM.equals(
                    node.getProperties()
                        .get("objective_type")
                        .getAsString())
                    || snapshot.getRuntimeSnapshot()
                        .getObjectiveStatuses()
                        .get(node.getId()) != darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE)
                    continue;
                String requiredActor = node.getProperties()
                    .get("actor_id")
                    .getAsString();
                if (!actorIds.contains(requiredActor)) continue;
                String title = resource.getDisplayName();
                String description = node.getProperties()
                    .get("description")
                    .getAsString();
                candidates.add(
                    new CanonicalTaskSubmitChoiceStore.Candidate(
                        snapshot.getStoryInstanceId(),
                        snapshot.getTaskNodePlacementId(),
                        node.getId(),
                        requiredActor,
                        snapshot.getActivationTime(),
                        title + "：" + description));
            }
        }
        if (candidates.isEmpty()) return false;
        if (candidates.size() > 1) {
            CanonicalTaskSubmitChoiceStore.Choice choice = submitChoices.offer(
                uuid,
                target.getUniqueID(),
                target.getEntityId(),
                target.worldObj.provider.dimensionId,
                candidates,
                System.currentTimeMillis());
            List<CanonicalTaskSubmitChoiceFrame.Option> options = new ArrayList<CanonicalTaskSubmitChoiceFrame.Option>();
            for (CanonicalTaskSubmitChoiceStore.Candidate candidate : candidates) options.add(
                new CanonicalTaskSubmitChoiceFrame.Option(
                    candidate.getStoryId() + "\u0000"
                        + candidate.getPlacementId()
                        + "\u0000"
                        + candidate.getObjectiveId(),
                    candidate.getDisplayName()));
            DialogueNetwork.CHANNEL.sendTo(new CanonicalTaskSubmitChoiceFrame(choice.getToken(), options), player);
            return true;
        }
        CanonicalTaskSubmitChoiceStore.Candidate candidate = candidates.get(0);
        return submitItem(
            player,
            candidate.getStoryId(),
            candidate.getPlacementId(),
            candidate.getObjectiveId(),
            candidate.getActivationTime());
    }

    /** Revalidates a pending physical actor choice before committing inventory. */
    public boolean selectSubmitCandidate(EntityPlayerMP player, long token, int optionIndex) {
        if (player == null || optionIndex < 0) return false;
        UUID uuid = requirePlayerUuid(player);
        CanonicalTaskSubmitChoiceStore.Choice choice = submitChoices.consume(uuid, token, System.currentTimeMillis());
        if (choice == null || choice.getDimension() != player.worldObj.provider.dimensionId) return false;
        Entity target = player.worldObj.getEntityByID(choice.getEntityId());
        if (target == null || !choice.getEntity()
            .equals(target.getUniqueID()) || !validActor(player, target)) return false;
        List<String> actorIds = EntityDgrIdentityResolver.resolveActorIds(target);
        if (optionIndex >= choice.getCandidates()
            .size()) return false;
        CanonicalTaskSubmitChoiceStore.Candidate candidate = choice.getCandidates()
            .get(optionIndex);
        if (!actorIds.contains(candidate.getActorId())) return false;
        boolean accepted = submitItem(
            player,
            candidate.getStoryId(),
            candidate.getPlacementId(),
            candidate.getObjectiveId(),
            candidate.getActivationTime());
        darkgrey.rpg.creator.CanonicalTaskPresentationServer.push(player, true);
        return accepted;
    }

    public void forgetSubmitChoices(UUID playerUuid) {
        submitChoices.forget(playerUuid);
    }

    /** Samples actual inventory and continuous position, without fabricating pickup history. */
    public void synchronizeObjectives(EntityPlayerMP player) {
        if (player instanceof net.minecraftforge.common.util.FakePlayer) return;
        CanonicalTaskPlayerTransactions.recover(player);
        Context context = context(player);
        UUID uuid = requirePlayerUuid(player);
        for (CanonicalTaskInstanceSnapshot snapshot : context.data.snapshots()) {
            if (!uuid.equals(snapshot.getPlayerUuid())
                || snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) continue;
            CanonicalGraphResource resource = currentTask(context.project, snapshot.getTaskResourceId());
            for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
                .getNodes()) {
                if (!"objective".equals(node.getType()) || snapshot.getRuntimeSnapshot()
                    .getObjectiveStatuses()
                    .get(node.getId()) != darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE) continue;
                String type = node.getProperties()
                    .get("objective_type")
                    .getAsString();
                java.util.Map<String, String> values = new java.util.LinkedHashMap<String, String>();
                int amount = 1;
                if (CanonicalTaskEvent.SUBMIT_ITEM.equals(type)) {
                    if (CanonicalTaskPlayerTransactions
                        .hasReceipt(player, CanonicalTaskPlayerTransactions.key(snapshot, node.getId(), "submit")))
                        submitItem(
                            player,
                            snapshot.getStoryInstanceId(),
                            snapshot.getTaskNodePlacementId(),
                            node.getId(),
                            snapshot.getActivationTime());
                    continue;
                }
                if (CanonicalTaskEvent.COLLECT_ITEM.equals(type)) {
                    values.put(
                        "item",
                        node.getProperties()
                            .get("item")
                            .getAsString());
                    for (java.util.Map.Entry<String, com.google.gson.JsonElement> field : node.getProperties()
                        .get("metadata")
                        .getAsJsonObject()
                        .entrySet())
                        values.put(
                            field.getKey(),
                            field.getValue()
                                .getAsString());
                    amount = CanonicalTaskInventory.count(
                        player.inventory.mainInventory,
                        node,
                        darkgrey.rpg.item.identity.ItemIdentitySavedData.get());
                } else if (CanonicalTaskEvent.REACH_REGION.equals(type)) {
                    values.put("dimension_id", String.valueOf(player.dimension));
                    values.put("x", String.valueOf(MathHelper.floor_double(player.posX)));
                    values.put("y", String.valueOf(MathHelper.floor_double(player.posY)));
                    values.put("z", String.valueOf(MathHelper.floor_double(player.posZ)));
                } else continue;
                CanonicalTaskInstanceSnapshot changed = context.data.sampleObjective(
                    uuid,
                    snapshot.getStoryInstanceId(),
                    snapshot.getTaskNodePlacementId(),
                    node.getId(),
                    new CanonicalTaskEvent(type, values, amount),
                    System.currentTimeMillis(),
                    null);
                notifySettlement(player, changed);
            }
        }
    }

    /** Delivers eligible packages before notifying Story of final Task settlement. */
    public void synchronizeRewards(EntityPlayerMP player) {
        Context context = context(player);
        UUID uuid = requirePlayerUuid(player);
        for (CanonicalTaskInstanceSnapshot snapshot : context.data.snapshots()) {
            if (!uuid.equals(snapshot.getPlayerUuid())
                || snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) continue;
            CanonicalGraphResource resource = currentTask(context.project, snapshot.getTaskResourceId());
            for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
                .getNodes()) {
                if (!Boolean.FALSE.equals(
                    snapshot.getRuntimeSnapshot()
                        .getRewardStates()
                        .get(node.getId())))
                    continue;
                String key = CanonicalTaskPlayerTransactions.key(snapshot, node.getId(), "reward");
                if (!CanonicalTaskPlayerTransactions.hasReceipt(player, key)) {
                    net.minecraft.nbt.NBTTagCompound image = CanonicalTaskPlayerTransactions
                        .rewardImage(player, darkgrey.rpg.task.runtime.CanonicalTaskRewardPackage.read(node));
                    if (image == null) continue;
                    CanonicalTaskPlayerTransactions.commit(player, key, image);
                }
                CanonicalTaskInstanceSnapshot changed = context.data.grantReward(
                    uuid,
                    snapshot.getStoryInstanceId(),
                    snapshot.getTaskNodePlacementId(),
                    node.getId(),
                    System.currentTimeMillis());
                notifySettlement(player, changed);
            }
        }
    }

    /** Consumes exactly one selected active submit Objective; repeated/stale submissions are no-ops. */
    boolean submitItem(final EntityPlayerMP player, String storyId, String placementId, String objectiveId,
        long activationTime) {
        if (player instanceof net.minecraftforge.common.util.FakePlayer) return false;
        CanonicalTaskPlayerTransactions.recover(player);
        Context context = context(player);
        UUID uuid = requirePlayerUuid(player);
        CanonicalTaskInstanceSnapshot snapshot = context.data.getSnapshot(uuid, storyId, placementId);
        if (snapshot == null || snapshot.getActivationTime() != activationTime
            || snapshot.getStatus() != darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE) return false;
        CanonicalGraphResource resource = currentTask(context.project, snapshot.getTaskResourceId());
        for (final darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (!node.getId()
                .equals(objectiveId)
                || !"objective".equals(node.getType())
                || !CanonicalTaskEvent.SUBMIT_ITEM.equals(
                    node.getProperties()
                        .get("objective_type")
                        .getAsString())
                || snapshot.getRuntimeSnapshot()
                    .getObjectiveStatuses()
                    .get(objectiveId) != darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE)
                continue;
            final int required = node.getProperties()
                .get("required")
                .getAsInt();
            final darkgrey.rpg.item.identity.ItemIdentitySavedData identities = darkgrey.rpg.item.identity.ItemIdentitySavedData
                .get();
            final String receipt = CanonicalTaskPlayerTransactions.key(snapshot, objectiveId, "submit");
            final boolean committed = CanonicalTaskPlayerTransactions.hasReceipt(player, receipt);
            final net.minecraft.item.ItemStack[] after = CanonicalTaskInventory.copy(player.inventory.mainInventory);
            if (!committed && !CanonicalTaskInventory.removeExact(after, node, identities, required)) return false;
            final net.minecraft.nbt.NBTTagCompound image = CanonicalTaskPlayerTransactions.image(player, after);
            java.util.Map<String, String> values = new java.util.LinkedHashMap<String, String>();
            // Both callers have already validated the physical server entity.
            // Carry that selected actor into the runtime event's matching contract.
            values.put(
                "actor_id",
                node.getProperties()
                    .get("actor_id")
                    .getAsString());
            values.put(
                "item",
                node.getProperties()
                    .get("item")
                    .getAsString());
            for (java.util.Map.Entry<String, com.google.gson.JsonElement> field : node.getProperties()
                .get("metadata")
                .getAsJsonObject()
                .entrySet())
                values.put(
                    field.getKey(),
                    field.getValue()
                        .getAsString());
            CanonicalTaskInstanceSnapshot changed = context.data.sampleObjective(
                uuid,
                storyId,
                placementId,
                objectiveId,
                new CanonicalTaskEvent(CanonicalTaskEvent.SUBMIT_ITEM, values, required),
                System.currentTimeMillis(),
                new CanonicalTaskSavedData.ObjectiveCommit() {

                    public void commit() {
                        if (!committed) CanonicalTaskPlayerTransactions.commit(player, receipt, image);
                    }

                    public void rollback() {
                        // A durable journal decision is recovered forward, never undone.
                        // Before the journal commit point, the player was not modified.
                    }
                });
            if (changed == null) return false;
            player.inventoryContainer.detectAndSendChanges();
            notifySettlement(player, changed);
            return true;
        }
        return false;
    }

    private void notifySettlement(EntityPlayerMP player, CanonicalTaskInstanceSnapshot snapshot) {
        if (snapshot != null && snapshot.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED
            && storySettlementListener != null) storySettlementListener.onStoryTaskSettled(player, snapshot);
    }

    private static boolean validActor(EntityPlayerMP player, Entity actor) {
        return actor != null && !actor.isDead
            && player.worldObj == actor.worldObj
            && player.getDistanceSqToEntity(actor) <= 36D
            && player.canEntityBeSeen(actor);
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

    /** Permanently discards every Task placement for this exact player/Story identity. */
    public int discardByPlayerStory(EntityPlayerMP player, String storyId) {
        Context context = context(player);
        return context.data.discardByPlayerStory(requirePlayerUuid(player), requireText(storyId, "Story ID"));
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
