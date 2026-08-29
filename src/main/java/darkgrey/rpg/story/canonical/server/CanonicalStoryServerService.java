package darkgrey.rpg.story.canonical.server;

import java.util.Collections;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryResourceResolver;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
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
        data.bind(new CanonicalSessionResourceResolver() {

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
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return dispatch(
            data.startStory(
                requirePlayer(playerUuid),
                story,
                start.selectEnterStory()
                    .getPortId(),
                start.getRepeatPolicy(),
                activationTime));
    }

    public CanonicalStoryDispatch startByActor(UUID playerUuid, String storyId, String actorId, long activationTime) {
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return dispatch(
            data.startStory(
                requirePlayer(playerUuid),
                story,
                start.selectActor(actorId)
                    .getPortId(),
                start.getRepeatPolicy(),
                activationTime));
    }

    public CanonicalStoryDispatch startByRegion(UUID playerUuid, String storyId, int dimension, double x, double y,
        double z, long activationTime) {
        CanonicalGraphResource story = story(storyId);
        CanonicalStoryStartConfiguration start = CanonicalStoryStartConfiguration.parse(story);
        return dispatch(
            data.startStory(
                requirePlayer(playerUuid),
                story,
                start.selectRegion(dimension, x, y, z)
                    .getPortId(),
                start.getRepeatPolicy(),
                activationTime));
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

    public CanonicalStoryDispatch completeAction(UUID playerUuid, String storyId, String actionNodeId, long eventTime) {
        return dispatch(
            data.completeStoryAction(
                requirePlayer(playerUuid),
                requireText(storyId, "Story ID"),
                requireText(actionNodeId, "Action placement ID"),
                eventTime));
    }

    /** Attempts to continue the sole active Story waiting for this actor. Null means Start matching may proceed. */
    public CanonicalStoryDispatch resumeActor(UUID playerUuid, String actorId, long eventTime) {
        CanonicalStoryInstanceSnapshot snapshot = data
            .resumeStoryActor(requirePlayer(playerUuid), requireText(actorId, "Actor ID"), eventTime);
        return snapshot == null ? null : dispatch(snapshot);
    }

    public CanonicalStoryDispatch resumeActorInteraction(UUID playerUuid, String actorId, long eventTime) {
        return resumeActor(playerUuid, actorId, eventTime);
    }

    public CanonicalStoryDispatch resumeActor(UUID playerUuid, String storyId, String actorId, long eventTime) {
        CanonicalStoryInstanceSnapshot snapshot = data.resumeStoryActor(
            requirePlayer(playerUuid),
            requireText(storyId, "Story ID"),
            requireText(actorId, "Actor ID"),
            eventTime);
        return snapshot == null ? null : dispatch(snapshot);
    }

    /** Attempts to continue the sole active Story whose EnterRegion sphere contains this position. */
    public CanonicalStoryDispatch resumeRegion(UUID playerUuid, int dimension, double x, double y, double z,
        long eventTime) {
        CanonicalStoryInstanceSnapshot snapshot = data
            .resumeStoryRegion(requirePlayer(playerUuid), dimension, x, y, z, eventTime);
        return snapshot == null ? null : dispatch(snapshot);
    }

    public CanonicalStoryDispatch resumeEnterRegion(UUID playerUuid, int dimension, double x, double y, double z,
        long eventTime) {
        return resumeRegion(playerUuid, dimension, x, y, z, eventTime);
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
        if (snapshot.getStatus() == CanonicalStoryStatus.TRANSFERRED)
            return terminal(CanonicalStoryDispatchKind.TRANSFERRED, instance, snapshot.getTargetStoryId());
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
        if (snapshot.getWaitKind()
            .isActorInteraction())
            return new CanonicalStoryDispatch(
                CanonicalStoryDispatchKind.ACTOR_INTERACT,
                instance,
                snapshot.getCurrentNodeId(),
                snapshot.getWaitActorId(),
                false,
                Collections.<String, JsonElement>emptyMap(),
                null);
        if (snapshot.getWaitKind() == CanonicalStoryWaitKind.ENTER_REGION) return new CanonicalStoryDispatch(
            CanonicalStoryDispatchKind.ENTER_REGION,
            instance,
            snapshot.getCurrentNodeId(),
            null,
            false,
            Collections.<String, JsonElement>emptyMap(),
            null,
            snapshot.getWaitDimension(),
            snapshot.getWaitX(),
            snapshot.getWaitY(),
            snapshot.getWaitZ(),
            snapshot.getWaitRadius());
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
