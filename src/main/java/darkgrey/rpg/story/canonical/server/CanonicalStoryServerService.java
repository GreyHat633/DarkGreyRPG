package darkgrey.rpg.story.canonical.server;

import java.util.Collections;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryResourceResolver;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Pure server orchestration boundary for Story triggers and durable aggregate handoffs. */
public final class CanonicalStoryServerService {

    private final ProjectSnapshot project;
    private final CanonicalSessionSavedData data;

    public CanonicalStoryServerService(ProjectSnapshot project, CanonicalSessionSavedData data) {
        if (project == null || data == null)
            throw new IllegalArgumentException("Canonical Story service inputs are required.");
        this.project = project;
        this.data = data;
        data.bindAvailable(new CanonicalSessionResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String sessionResourceId) {
                return CanonicalStoryServerService.this.project.getCanonicalSession(sessionResourceId);
            }
        }, new CanonicalStoryResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String storyId) {
                return CanonicalStoryServerService.this.project.getCanonicalStory(storyId);
            }
        });
    }

    public CanonicalStoryDispatch startByEntry(UUID playerUuid, String storyId, long activationTime) {
        CanonicalStoryDispatch blocked = blockedStart(playerUuid, storyId);
        if (blocked != null) return blocked;
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return startDispatch(
            requirePlayer(playerUuid),
            story,
            start.selectEnterStory()
                .getPortId(),
            start.getRepeatPolicy(),
            Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    public CanonicalStoryDispatch startByActor(UUID playerUuid, String storyId, String actorId, long activationTime) {
        CanonicalStoryDispatch blocked = blockedStart(playerUuid, storyId);
        if (blocked != null) return blocked;
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return startDispatch(
            requirePlayer(playerUuid),
            story,
            start.selectActor(actorId)
                .getPortId(),
            start.getRepeatPolicy(),
            Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    public CanonicalStoryDispatch startByRegion(UUID playerUuid, String storyId, int dimension, double x, double y,
        double z, long activationTime) {
        CanonicalStoryDispatch blocked = blockedStart(playerUuid, storyId);
        if (blocked != null) return blocked;
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return startDispatch(
            requirePlayer(playerUuid),
            story,
            start.selectRegion(dimension, x, y, z)
                .getPortId(),
            start.getRepeatPolicy(),
            Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    /** Starts a Story through the first authored Logic trigger whose condition is satisfied by the supplied inputs. */
    public CanonicalStoryDispatch startByLogic(UUID playerUuid, String storyId, Map<String, Boolean> logicInputs,
        long activationTime) {
        if (!isStartEligible(playerUuid, storyId)) return null;
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        for (CanonicalStoryStartConfiguration.Trigger trigger : start.getLogicTriggers()) try {
            return startDispatch(
                requirePlayer(playerUuid),
                story,
                trigger.getPortId(),
                start.getRepeatPolicy(),
                logicInputs,
                activationTime);
        } catch (CanonicalGraphResourceException exception) {
            if (!"story.start.trigger.condition".equals(exception.getCode())) throw exception;
        }
        return null;
    }

    public CanonicalStoryStartDisposition startDisposition(UUID playerUuid, String storyId) {
        return data.startDisposition(requirePlayer(playerUuid), requireText(storyId, "Story ID"));
    }

    public boolean isStartEligible(UUID playerUuid, String storyId) {
        return startDisposition(playerUuid, storyId).isEligible();
    }

    public java.util.Set<String> eligibleStartStoryIds(UUID playerUuid) {
        java.util.Set<String> eligible = new java.util.LinkedHashSet<String>();
        for (String storyId : project.getCanonicalStories()
            .keySet()) if (isStartEligible(playerUuid, storyId)) eligible.add(storyId);
        return Collections.unmodifiableSet(eligible);
    }

    private CanonicalStoryDispatch blockedStart(UUID playerUuid, String storyId) {
        CanonicalStoryStartDisposition disposition = startDisposition(playerUuid, storyId);
        return disposition.isEligible() ? null : snapshot(playerUuid, storyId).withStartDisposition(disposition);
    }

    /** Collects one choice per Story without changing runtime, including every identity on the interacted entity. */
    public java.util.List<CanonicalActorCandidate> actorCandidates(UUID playerUuid,
        java.util.Collection<String> actorIds) {
        requirePlayer(playerUuid);
        if (actorIds == null) throw new IllegalArgumentException("Actor identities are required.");
        java.util.List<CanonicalActorCandidate> result = new java.util.ArrayList<CanonicalActorCandidate>();
        for (String storyId : project.getCanonicalStories()
            .keySet()) {
            CanonicalStoryInstanceSnapshot previous = data.getStorySnapshot(playerUuid, storyId);
            CanonicalStoryStartDisposition disposition = startDisposition(playerUuid, storyId);

            if (!disposition.isEligible()) continue;
            CanonicalGraphResource resource = story(storyId);
            CanonicalStoryStartConfiguration configuration = CanonicalStoryStartConfiguration.parse(resource);
            for (CanonicalStoryStartConfiguration.Trigger trigger : configuration.getTriggers()) {
                if (!"interact_actor".equals(trigger.getType()) || !actorIds.contains(trigger.getString("actor_id")))
                    continue;
                try {
                    // Pure runtime preview validates Start Logic and graph routing, without committing any state.
                    darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntime.start(
                        resource,
                        trigger.getPortId(),
                        configuration.getRepeatPolicy(),
                        actorStartInputs(playerUuid, storyId, previous));
                } catch (CanonicalGraphResourceException invalid) {
                    if ("story.start.trigger.condition".equals(invalid.getCode())) continue;
                    throw invalid;
                }
                result.add(
                    new CanonicalActorCandidate(
                        playerUuid,
                        project,
                        storyId,
                        trigger.getString("actor_id"),
                        trigger.getPortId(),
                        disposition == CanonicalStoryStartDisposition.NEW ? "start" : "restart",
                        previous));
                break;
            }
        }
        return Collections.unmodifiableList(result);
    }

    /** Rechecks the selected Story only and returns null for stale, foreign or no-longer-eligible choices. */
    public CanonicalStoryDispatch executeActorCandidate(UUID playerUuid, java.util.Collection<String> actorIds,
        CanonicalActorCandidate selected, long eventTime) {
        if (selected == null) return null;
        for (CanonicalActorCandidate current : actorCandidates(playerUuid, actorIds)) {
            if (!selected.matches(playerUuid, project, current)) continue;
            CanonicalGraphResource resource = story(current.getStoryId());
            CanonicalStoryStartConfiguration configuration = CanonicalStoryStartConfiguration.parse(resource);
            return startDispatch(
                playerUuid,
                resource,
                current.getPortId(),
                configuration.getRepeatPolicy(),
                actorStartInputs(
                    playerUuid,
                    current.getStoryId(),
                    data.getStorySnapshot(playerUuid, current.getStoryId())),
                eventTime);
        }
        return null;
    }

    private Map<String, Boolean> actorStartInputs(UUID playerUuid, String storyId,
        CanonicalStoryInstanceSnapshot previous) {
        Map<String, Boolean> inputs = new java.util.LinkedHashMap<String, Boolean>();
        if (previous != null) inputs.putAll(
            previous.getRuntimeSnapshot()
                .getExternalLogicInputs());
        for (darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection connection : project
            .getCanonicalStoryLogicConnections()) {
            if (!storyId.equals(connection.getTargetStoryId())) continue;
            CanonicalStoryInstanceSnapshot source = data.getStorySnapshot(playerUuid, connection.getSourceStoryId());
            Boolean value = source == null ? null
                : darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntime
                    .restore(story(connection.getSourceStoryId()), source.getRuntimeSnapshot())
                    .getPublicLogicOutputs()
                    .get(connection.getSourcePortId());
            inputs.put(connection.getTargetPortId(), Boolean.valueOf(Boolean.TRUE.equals(value)));
        }
        return inputs;
    }

    private CanonicalStoryDispatch startDispatch(UUID playerUuid, CanonicalGraphResource resource, String portId,
        CanonicalStoryRepeatPolicy repeatPolicy, Map<String, Boolean> logicInputs, long activationTime) {
        CanonicalStoryStartDisposition disposition = startDisposition(playerUuid, resource.getId());
        return dispatch(data.startStory(playerUuid, resource, portId, repeatPolicy, logicInputs, activationTime))
            .withStartDisposition(disposition);
    }

    public CanonicalStoryDispatch setLogicInput(UUID playerUuid, String storyId, String portId, boolean value,
        long eventTime) {
        return setLogicInputs(
            playerUuid,
            storyId,
            Collections.singletonMap(requireText(portId, "Story Logic input port ID"), Boolean.valueOf(value)),
            eventTime);
    }

    public CanonicalStoryDispatch setLogicInputs(UUID playerUuid, String storyId, Map<String, Boolean> values,
        long eventTime) {
        return dispatch(
            data.setStoryLogicInputs(requirePlayer(playerUuid), requireText(storyId, "Story ID"), values, eventTime));
    }

    public CanonicalStoryDispatch resumeSession(UUID playerUuid, String storyId, long eventTime) {
        return dispatch(
            data.resumeStorySession(requirePlayer(playerUuid), requireText(storyId, "Story ID"), eventTime));
    }

    public CanonicalStoryDispatch resumeTask(UUID playerUuid, String storyId, CanonicalTaskInstanceSnapshot task,
        long eventTime) {
        return dispatch(
            data.resumeStoryTask(requirePlayer(playerUuid), requireText(storyId, "Story ID"), task, eventTime));
    }

    public CanonicalStoryDispatch completeTitle(UUID playerUuid, String storyId, String nodeId, long eventTime) {
        return dispatch(
            data.completeStoryTitle(requirePlayer(playerUuid), requireText(storyId, "Story ID"), nodeId, eventTime));
    }

    public CanonicalStoryDispatch completeAction(UUID playerUuid, String storyId, String actionNodeId, long eventTime) {
        return dispatch(
            data.completeStoryAction(
                requirePlayer(playerUuid),
                requireText(storyId, "Story ID"),
                requireText(actionNodeId, "Action placement ID"),
                eventTime));
    }

    public CanonicalStoryDispatch snapshot(UUID playerUuid, String storyId) {
        CanonicalStoryInstanceSnapshot snapshot = data
            .getStorySnapshot(requirePlayer(playerUuid), requireText(storyId, "Story ID"));
        return snapshot == null ? null : dispatch(snapshot);
    }

    private CanonicalStoryDispatch dispatch(CanonicalStoryInstanceSnapshot instance) {
        CanonicalStorySnapshot snapshot = instance.getRuntimeSnapshot();
        if (snapshot.getStatus() == CanonicalStoryStatus.TERMINATED)
            return terminal(CanonicalStoryDispatchKind.TERMINATED, instance, null);
        if (snapshot.getStatus() == CanonicalStoryStatus.ERROR)
            return terminal(CanonicalStoryDispatchKind.ERROR, instance, null);
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.SESSION) return new CanonicalStoryDispatch(
            CanonicalStoryDispatchKind.SESSION,
            instance,
            snapshot.getCurrentNodeId(),
            snapshot.getWaitResourceId(),
            data.getStorySessionActivationLogic(instance.getPlayerUuid(), instance.getStoryId()),
            Collections.<String, JsonElement>emptyMap(),
            null);
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.TASK) return new CanonicalStoryDispatch(
            CanonicalStoryDispatchKind.TASK,
            instance,
            snapshot.getCurrentNodeId(),
            snapshot.getWaitResourceId(),
            false,
            Collections.<String, JsonElement>emptyMap(),
            null);
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.TITLE) {
            Map<String, JsonElement> properties = node(story(snapshot.getResourceId()), snapshot.getCurrentNodeId())
                .getProperties();
            darkgrey.rpg.story.canonical.runtime.CanonicalTitleConfiguration.parse(properties);
            return new CanonicalStoryDispatch(
                CanonicalStoryDispatchKind.TITLE,
                instance,
                snapshot.getCurrentNodeId(),
                null,
                false,
                properties,
                null);
        }
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.ACTION) {
            Map<String, JsonElement> properties = node(story(snapshot.getResourceId()), snapshot.getCurrentNodeId())
                .getProperties();
            CanonicalStoryActionConfiguration.parse(properties);
            return new CanonicalStoryDispatch(
                CanonicalStoryDispatchKind.ACTION,
                instance,
                snapshot.getCurrentNodeId(),
                null,
                false,
                properties,
                null);
        }
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.CONDITION) return new CanonicalStoryDispatch(
            CanonicalStoryDispatchKind.CONDITION,
            instance,
            snapshot.getCurrentNodeId(),
            null,
            false,
            Collections.<String, JsonElement>emptyMap(),
            null);
        throw new IllegalStateException("Active canonical Story did not stop at an external boundary.");
    }

    private static CanonicalStoryDispatch terminal(CanonicalStoryDispatchKind kind,
        CanonicalStoryInstanceSnapshot instance, String targetStoryId) {
        return new CanonicalStoryDispatch(
            kind,
            instance,
            null,
            null,
            false,
            Collections.<String, JsonElement>emptyMap(),
            targetStoryId);
    }

    private CanonicalGraphResource story(String storyId) {
        String id = requireText(storyId, "Story ID");
        CanonicalGraphResource resource = project.getCanonicalStory(id);
        if (resource == null) throw new IllegalArgumentException("Missing canonical Story resource: " + id);
        if (resource.getResourceKind() != CanonicalGraphResourceKind.STORY)
            throw new IllegalArgumentException("Resource is not a canonical Story: " + id);
        return resource;
    }

    private static CanonicalGraphNode node(CanonicalGraphResource resource, String nodeId) {
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) if (node != null && nodeId.equals(node.getId())) return node;
        throw new IllegalStateException("Canonical Story placement does not exist: " + nodeId);
    }

    private static UUID requirePlayer(UUID playerUuid) {
        if (playerUuid == null) throw new IllegalArgumentException("Player UUID is required.");
        return playerUuid;
    }

    private static String requireText(String value, String label) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(label + " is required.");
        return value.trim();
    }
}
