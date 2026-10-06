package darkgrey.rpg.story.canonical.server;

import java.util.ArrayList;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.UUID;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Pure candidate projection shared by the physical Forge entry and offline regression. */
public final class CanonicalNpcArbitration {

    private CanonicalNpcArbitration() {}

    public static List<CanonicalActorCandidate> collect(UUID player, ProjectSnapshot project,
        CanonicalStoryServerService service, CanonicalSessionSavedData stories,
        List<CanonicalTaskInstanceSnapshot> tasks, Collection<String> actorIds) {
        LinkedHashMap<String, CanonicalActorCandidate> candidates = new LinkedHashMap<String, CanonicalActorCandidate>();
        for (CanonicalActorCandidate start : service.actorCandidates(player, actorIds))
            candidates.put(start.getStoryId(), start);
        for (CanonicalTaskInstanceSnapshot task : tasks) {
            if (!player.equals(task.getPlayerUuid()) || task.getStatus() != CanonicalTaskInstanceStatus.ACTIVE)
                continue;
            CanonicalStoryInstanceSnapshot story = stories.getStorySnapshot(player, task.getStoryInstanceId());
            if (story == null || story.getRuntimeSnapshot()
                .getWaitKind() != CanonicalStoryWaitKind.TASK
                || !task.getTaskNodePlacementId()
                    .equals(
                        story.getRuntimeSnapshot()
                            .getCurrentNodeId()))
                continue;
            CanonicalGraphResource resource = project.getCanonicalTask(task.getTaskResourceId());
            if (resource == null) continue;
            for (CanonicalGraphNode node : resource.getGraph()
                .getNodes()) {
                if (!"objective".equals(node.getType()) || task.getRuntimeSnapshot()
                    .getObjectiveStatuses()
                    .get(node.getId()) != CanonicalTaskObjectiveStatus.ACTIVE) continue;
                String type = node.getProperties()
                    .get("objective_type")
                    .getAsString();
                if (!(CanonicalTaskEvent.INTERACT_ACTOR.equals(type) || CanonicalTaskEvent.SUBMIT_ITEM.equals(type)))
                    continue;
                String actor = node.getProperties()
                    .get("actor_id")
                    .getAsString();
                if (actorIds.contains(actor)) {
                    candidates
                        .put(story.getStoryId(), CanonicalActorCandidate.forTask(player, project, actor, story, task));
                    break;
                }
            }
        }
        return new ArrayList<CanonicalActorCandidate>(candidates.values());
    }
}
