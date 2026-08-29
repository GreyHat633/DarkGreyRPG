package darkgrey.rpg.session.server;

import java.util.UUID;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.runtime.CanonicalSessionSnapshot;
import darkgrey.rpg.session.runtime.CanonicalSessionStatus;
import darkgrey.rpg.session.runtime.CanonicalSessionStep;

/** Server-neutral orchestration and presentation boundary for canonical Sessions. */
public final class CanonicalSessionServerService {

    private final ProjectSnapshot project;
    private final CanonicalSessionSavedData savedData;

    public CanonicalSessionServerService(ProjectSnapshot project, CanonicalSessionSavedData savedData) {
        if (project == null || savedData == null)
            throw new IllegalArgumentException("Session service inputs required.");
        this.project = project;
        this.savedData = savedData;
        if (!savedData.isBound()) savedData.bind(new CanonicalSessionResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String sessionResourceId) {
                return CanonicalSessionServerService.this.project.getCanonicalSession(sessionResourceId);
            }
        });
    }

    public CanonicalSessionServerService(CanonicalSessionSavedData savedData, ProjectSnapshot project) {
        this(project, savedData);
    }

    public CanonicalSessionDispatch start(UUID playerUuid, String storyId, String aggregatePlacementId) {
        return start(playerUuid, storyId, aggregatePlacementId, false);
    }

    public CanonicalSessionDispatch start(UUID playerUuid, String storyId, String aggregatePlacementId,
        boolean activationLogic) {
        Binding binding = resolveBinding(storyId, aggregatePlacementId);
        validateActors(binding);
        CanonicalSessionInstanceSnapshot snapshot = savedData
            .start(playerUuid, storyId, aggregatePlacementId, binding.session, activationLogic);
        return projectSnapshot(snapshot, binding);
    }

    public CanonicalSessionDispatch begin(UUID playerUuid, String storyId, String aggregatePlacementId) {
        return start(playerUuid, storyId, aggregatePlacementId);
    }

    public CanonicalSessionDispatch open(UUID playerUuid, String storyId, String aggregatePlacementId) {
        return start(playerUuid, storyId, aggregatePlacementId);
    }

    public CanonicalSessionDispatch resume(UUID playerUuid, String storyId) {
        CanonicalSessionInstanceSnapshot snapshot = savedData.getSnapshot(playerUuid, storyId);
        if (snapshot == null) throw new IllegalStateException("Session instance does not exist.");
        Binding binding = resolveBinding(snapshot.getStoryId(), snapshot.getAggregatePlacementId());
        requireResourceBinding(snapshot, binding);
        validateActors(binding);
        return projectSnapshot(snapshot, binding);
    }

    public CanonicalSessionDispatch resume(UUID playerUuid, String storyId, String aggregatePlacementId) {
        CanonicalSessionInstanceSnapshot snapshot = savedData.getSnapshot(playerUuid, storyId);
        if (snapshot == null || aggregatePlacementId == null
            || !aggregatePlacementId.equals(snapshot.getAggregatePlacementId()))
            throw new IllegalStateException("Session aggregate placement does not match server context.");
        return resume(playerUuid, storyId);
    }

    public CanonicalSessionDispatch dispatch(UUID playerUuid, CanonicalSessionAction action) {
        if (action == null) throw new IllegalArgumentException("Session action is required.");
        return dispatch(playerUuid, action.getStoryId(), action);
    }

    public CanonicalSessionDispatch dispatch(UUID playerUuid, String expectedStoryId, CanonicalSessionAction action) {
        if (playerUuid == null || action == null) throw new IllegalArgumentException("Session action is required.");
        if (expectedStoryId == null || !expectedStoryId.equals(action.getStoryId()))
            throw new IllegalStateException("Session action Story does not match server context.");
        CanonicalSessionInstanceSnapshot before = savedData.getSnapshot(playerUuid, expectedStoryId);
        if (before == null) throw new IllegalStateException("Session instance does not exist.");
        Binding binding = resolveBinding(before.getStoryId(), before.getAggregatePlacementId());
        requireResourceBinding(before, binding);
        validateActors(binding);
        CanonicalSessionStep current = savedData.getCurrentStep(playerUuid, expectedStoryId);
        if (current == null || before.getRuntimeSnapshot()
            .getStatus() != CanonicalSessionStatus.ACTIVE) throw new IllegalStateException("Session is not active.");
        CanonicalSessionStep next;
        if (action.getKind() == CanonicalSessionAction.Kind.CONTINUE) {
            if (current.getKind() != CanonicalSessionStep.Kind.LINE)
                throw new IllegalStateException("Continue requires a Session Line.");
            next = savedData
                .continueLine(playerUuid, expectedStoryId, action.getTransportId(), action.getCurrentNodeId());
        } else if (action.getKind() == CanonicalSessionAction.Kind.CHOICE) {
            if (current.getKind() != CanonicalSessionStep.Kind.CHOICE)
                throw new IllegalStateException("Choice requires a Session Choice.");
            next = savedData.selectChoice(
                playerUuid,
                expectedStoryId,
                action.getTransportId(),
                action.getCurrentNodeId(),
                action.getOptionId());
        } else throw new IllegalArgumentException("Unknown Session action kind.");
        CanonicalSessionInstanceSnapshot after = savedData.getSnapshot(playerUuid, expectedStoryId);
        if (after == null) throw new IllegalStateException("Session instance disappeared after action.");
        // The returned step is intentionally not used as authority; SavedData is re-read for projection.
        if (next == null) throw new IllegalStateException("Session transition did not produce a step.");
        return projectSnapshot(after, binding);
    }

    public CanonicalSessionDispatch handleAction(UUID playerUuid, CanonicalSessionAction action) {
        return dispatch(playerUuid, action);
    }

    public CanonicalSessionDispatch advance(UUID playerUuid, CanonicalSessionAction action) {
        return dispatch(playerUuid, action);
    }

    public CanonicalSessionDispatch continueLine(UUID playerUuid, String storyId, long transportId,
        String currentNodeId) {
        return dispatch(
            playerUuid,
            storyId,
            new CanonicalSessionAction(
                transportId,
                storyId,
                currentNodeId,
                CanonicalSessionAction.Kind.CONTINUE,
                null));
    }

    public CanonicalSessionDispatch selectChoice(UUID playerUuid, String storyId, long transportId,
        String currentNodeId, String optionId) {
        return dispatch(
            playerUuid,
            storyId,
            new CanonicalSessionAction(
                transportId,
                storyId,
                currentNodeId,
                CanonicalSessionAction.Kind.CHOICE,
                optionId));
    }

    private CanonicalSessionDispatch projectSnapshot(CanonicalSessionInstanceSnapshot snapshot, Binding binding) {
        if (snapshot == null || binding == null)
            throw new IllegalArgumentException("Session projection input required.");
        validateActors(binding);
        CanonicalSessionSnapshot runtime = snapshot.getRuntimeSnapshot();
        CanonicalSessionStatus status = runtime.getStatus();
        if (status == CanonicalSessionStatus.FAILED)
            throw new IllegalStateException("FAILED Session snapshots cannot be projected.");
        CanonicalSessionStep step = savedData.getCurrentStep(snapshot.getPlayerUuid(), snapshot.getStoryId());
        if (step == null || blank(
            snapshot.getRuntimeSnapshot()
                .getCurrentNodeId())
            || !snapshot.getRuntimeSnapshot()
                .getCurrentNodeId()
                .equals(step.getNodeId()))
            throw new IllegalStateException("Session snapshot has no coherent current step.");
        if (status == CanonicalSessionStatus.ACTIVE) {
            if (step.getKind() != CanonicalSessionStep.Kind.LINE && step.getKind() != CanonicalSessionStep.Kind.CHOICE)
                throw new IllegalStateException("Active Session snapshot has an incoherent step.");
            return CanonicalSessionDispatch.frame(frame(snapshot, step));
        }
        if (status != CanonicalSessionStatus.COMPLETED || step.getKind() != CanonicalSessionStep.Kind.END
            || blank(step.getEndPortId())
            || !step.getEndPortId()
                .equals(runtime.getFinalEndPortId()))
            throw new IllegalStateException("Completed Session snapshot has an incoherent End.");
        CanonicalSessionCompletionResult result = new CanonicalSessionCompletionResult(
            snapshot.getPlayerUuid(),
            snapshot.getStoryId(),
            snapshot.getAggregatePlacementId(),
            snapshot.getSessionResourceId(),
            snapshot.getTransportId(),
            step.getEndPortId(),
            runtime.getPublicLogicOutputs());
        return CanonicalSessionDispatch
            .completed(new CanonicalSessionClose(snapshot.getTransportId(), snapshot.getStoryId()), result);
    }

    private CanonicalSessionFrame frame(CanonicalSessionInstanceSnapshot snapshot, CanonicalSessionStep step) {
        if (step.getKind() == CanonicalSessionStep.Kind.LINE) {
            ActorDefinition actor = project.getActor(step.getSpeakerActorId());
            if (actor == null || blank(actor.getDisplayName()))
                throw new IllegalStateException("Session Line actor is missing or has a blank display name.");
            return new CanonicalSessionFrame(
                snapshot.getTransportId(),
                snapshot.getStoryId(),
                snapshot.getSessionResourceId(),
                step.getNodeId(),
                CanonicalSessionFrame.Kind.LINE,
                actor.getDisplayName(),
                step.getText(),
                java.util.Collections.<CanonicalSessionChoiceOption>emptyList());
        }
        java.util.ArrayList<CanonicalSessionChoiceOption> choices = new java.util.ArrayList<CanonicalSessionChoiceOption>();
        for (darkgrey.rpg.session.runtime.CanonicalSessionChoiceOption option : step.getOptions())
            choices.add(new CanonicalSessionChoiceOption(option.getOptionId(), option.getDisplayText()));
        return new CanonicalSessionFrame(
            snapshot.getTransportId(),
            snapshot.getStoryId(),
            snapshot.getSessionResourceId(),
            step.getNodeId(),
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            step.getPrompt(),
            choices);
    }

    private Binding resolveBinding(String storyId, String placementId) {
        if (blank(storyId) || blank(placementId))
            throw new IllegalArgumentException("Story and aggregate placement required.");
        CanonicalGraphResource story = project.getCanonicalStory(storyId);
        if (story == null || story.getResourceKind() != CanonicalGraphResourceKind.STORY
            || !storyId.equals(story.getId())
            || story.getGraph() == null) throw new IllegalStateException("Canonical Story is missing or incoherent.");
        CanonicalGraphNode placement = null;
        for (CanonicalGraphNode node : story.getGraph()
            .getNodes()) if (node != null && placementId.equals(node.getId())) {
                if (placement != null) throw new IllegalStateException("Aggregate placement is ambiguous.");
                placement = node;
            }
        if (placement == null || !"session".equals(placement.getType()))
            throw new IllegalStateException("Aggregate placement is not a Session node.");
        String resourceId = requiredString(placement, "resource_id");
        CanonicalGraphResource session = project.getCanonicalSession(resourceId);
        if (session == null || session.getResourceKind() != CanonicalGraphResourceKind.SESSION
            || !resourceId.equals(session.getId()))
            throw new IllegalStateException("Session resource binding is missing or incoherent.");
        CanonicalStoryMembership membership = project.getCanonicalStoryMembership(storyId);
        if (membership == null || !storyId.equals(membership.getStoryId()))
            throw new IllegalStateException("Story membership is missing.");
        boolean owned = membership.getOwnedResources()
            .getSessionIds()
            .contains(resourceId);
        boolean referenced = membership.getReferencedResources()
            .getSessionIds()
            .contains(resourceId);
        if (owned == referenced) throw new IllegalStateException("Session membership is missing or ambiguous.");
        return new Binding(session, membership);
    }

    private void validateActors(Binding binding) {
        CanonicalGraph graph = binding.session.getGraph();
        if (graph == null) throw new IllegalStateException("Session graph is missing.");
        for (CanonicalGraphNode node : graph.getNodes()) if (node != null && "line".equals(node.getType())) {
            String actorId = requiredString(node, "speaker_actor_id");
            ActorDefinition actor = project.getActor(actorId);
            boolean owned = binding.membership.getOwnedResources()
                .getActorIds()
                .contains(actorId);
            boolean referenced = binding.membership.getReferencedResources()
                .getActorIds()
                .contains(actorId);
            if (owned == referenced || actor == null || blank(actor.getDisplayName()))
                throw new IllegalStateException("Session Line actor is missing or has a blank display name.");
        }
    }

    private static void requireResourceBinding(CanonicalSessionInstanceSnapshot snapshot, Binding binding) {
        if (!binding.session.getId()
            .equals(snapshot.getSessionResourceId()))
            throw new IllegalStateException("Session resource binding changed.");
    }

    private static String requiredString(CanonicalGraphNode node, String key) {
        if (node == null) throw new IllegalStateException("Session graph node is missing.");
        JsonElement value = node.getProperties()
            .get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || blank(value.getAsString()))
            throw new IllegalStateException("Session graph property '" + key + "' is missing or invalid.");
        return value.getAsString();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static final class Binding {

        private final CanonicalGraphResource session;
        private final CanonicalStoryMembership membership;

        Binding(CanonicalGraphResource session, CanonicalStoryMembership membership) {
            this.session = session;
            this.membership = membership;
        }
    }
}
