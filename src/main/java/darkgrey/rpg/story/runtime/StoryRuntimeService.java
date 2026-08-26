package darkgrey.rpg.story.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.story.StoryConnection;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryNode;

public final class StoryRuntimeService {

    private static final int MAX_STEPS_PER_EVENT = 128;

    private final ProjectRepository repository;
    private final DialogueSessionManager dialogues;
    private final QuestRuntimeService quests;
    private final StoryNodeExecutorRegistry executors;
    private final Map<UUID, Map<String, StoryInstance>> instances = new LinkedHashMap<UUID, Map<String, StoryInstance>>();

    public StoryRuntimeService(ProjectRepository repository, DialogueSessionManager dialogues,
        QuestRuntimeService quests) {
        this.repository = repository;
        this.dialogues = dialogues;
        this.quests = quests;
        this.executors = BuiltinStoryExecutors.createRegistry();
    }

    public void handle(EntityPlayerMP player, StoryEvent event) {
        Set<String> advancedByTransition = new HashSet<String>();
        for (StoryDefinition story : repository.getSnapshot()
            .getStories()
            .values()) {
            StoryInstance instance = getInstance(player, story);
            if (instance.getState() != StoryState.ERROR && !advancedByTransition.contains(story.getId())) {
                advance(player, story, instance, event, advancedByTransition, 0);
            }
        }
    }

    public boolean start(EntityPlayerMP player, String storyId) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(storyId);
        if (story == null) {
            return false;
        }
        StoryInstance instance = getInstance(player, story);
        instance.startAt(story.getEntry());
        advance(player, story, instance, StoryEvent.manual(), new HashSet<String>(), 0);
        return instance.getState() != StoryState.ERROR;
    }

    public StoryInstance getInstance(EntityPlayerMP player, StoryDefinition story) {
        Map<String, StoryInstance> playerInstances = instances.get(player.getUniqueID());
        if (playerInstances == null) {
            playerInstances = new LinkedHashMap<String, StoryInstance>();
            instances.put(player.getUniqueID(), playerInstances);
        }
        StoryInstance instance = playerInstances.get(story.getId());
        if (instance == null || instance.getDefinition() != story) {
            instance = new StoryInstance(story);
            playerInstances.put(story.getId(), instance);
        }
        return instance;
    }

    public void resetInstances() {
        instances.clear();
    }

    public Map<String, StoryInstance> getInstances(EntityPlayerMP player) {
        Map<String, StoryInstance> result = new LinkedHashMap<String, StoryInstance>();
        for (StoryDefinition story : repository.getSnapshot()
            .getStories()
            .values()) {
            result.put(story.getId(), getInstance(player, story));
        }
        return Collections.unmodifiableMap(result);
    }

    public Map<String, StoryInstance.Snapshot> snapshotPlayer(EntityPlayerMP player) {
        Map<String, StoryInstance.Snapshot> snapshots = new LinkedHashMap<String, StoryInstance.Snapshot>();
        for (Map.Entry<String, StoryInstance> entry : getInstances(player).entrySet()) {
            snapshots.put(
                entry.getKey(),
                entry.getValue()
                    .snapshot());
        }
        return snapshots;
    }

    public void restorePlayer(EntityPlayerMP player, Map<String, StoryInstance.Snapshot> snapshots) {
        for (StoryDefinition story : repository.getSnapshot()
            .getStories()
            .values()) {
            StoryInstance instance = getInstance(player, story);
            StoryInstance.Snapshot snapshot = snapshots.get(story.getId());
            if (snapshot == null) {
                instance.reset();
            } else {
                instance.restore(snapshot);
            }
        }
    }

    public boolean startAt(EntityPlayerMP player, String storyId, String nodeId) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(storyId);
        if (story == null || story.getNode(nodeId) == null) {
            return false;
        }
        getInstance(player, story).startAt(nodeId);
        return true;
    }

    public boolean resetInstance(EntityPlayerMP player, String storyId) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(storyId);
        if (story == null) {
            return false;
        }
        getInstance(player, story).reset();
        return true;
    }

    private void advance(EntityPlayerMP player, StoryDefinition story, StoryInstance instance, StoryEvent event,
        Set<String> advancedByTransition, int transitionDepth) {
        for (int step = 0; step < MAX_STEPS_PER_EVENT; step++) {
            StoryNode node = story.getNode(instance.getCurrentNodeId());
            if (node == null) {
                fail(player, story, instance, "Missing current node '" + instance.getCurrentNodeId() + "'");
                return;
            }
            StoryNodeExecutor executor = executors.get(node.getType());
            if (executor == null) {
                fail(player, story, instance, "No executor registered for " + node.getType());
                return;
            }
            StoryExecutionContext context = new StoryExecutionContext(player, story, instance, dialogues, quests);
            NodeExecutionResult result = executor.execute(context, node, event);
            instance.recordDebug(
                node.getId(),
                result.getKind()
                    .name()
                    + (result.getOutput()
                        .isEmpty() ? "" : ":" + result.getOutput()),
                StoryDebugExplainer.explain(context, node, event, result));
            if (result.getKind() == NodeExecutionResult.Kind.WAIT) {
                if (instance.getState() != StoryState.IDLE) {
                    instance.setState(StoryState.WAITING);
                }
                return;
            }
            if (result.getKind() == NodeExecutionResult.Kind.ERROR) {
                fail(player, story, instance, result.getOutput());
                return;
            }
            if (result.getKind() == NodeExecutionResult.Kind.END) {
                String pending = instance.popPending();
                if (pending == null) {
                    instance.reset();
                    return;
                }
                instance.moveTo(pending);
                continue;
            }
            if (result.getKind() == NodeExecutionResult.Kind.ENTER_STORY) {
                StoryDefinition target = repository.getSnapshot()
                    .getStory(result.getOutput());
                if (target == null) {
                    fail(player, story, instance, "Unknown target Story '" + result.getOutput() + "'");
                    return;
                }
                if (transitionDepth >= MAX_STEPS_PER_EVENT) {
                    fail(player, story, instance, "Exceeded cross-Story transition limit");
                    return;
                }
                instance.reset();
                StoryInstance targetInstance = getInstance(player, target);
                targetInstance.startAt(target.getEntry());
                advancedByTransition.add(target.getId());
                advance(player, target, targetInstance, event, advancedByTransition, transitionDepth + 1);
                return;
            }
            if (result.getKind() == NodeExecutionResult.Kind.SEQUENCE) {
                if (!beginSequence(story, instance, node)) {
                    fail(player, story, instance, "Sequence node has no connected steps: " + node.getId());
                    return;
                }
                continue;
            }
            StoryConnection connection = story.findConnection(node.getId(), result.getOutput());
            if (connection == null) {
                fail(
                    player,
                    story,
                    instance,
                    "No connection from '" + node.getId() + "' output '" + result.getOutput() + "'");
                return;
            }
            instance.moveTo(connection.getTo());
        }
        fail(player, story, instance, "Exceeded " + MAX_STEPS_PER_EVENT + " execution steps");
    }

    private static boolean beginSequence(StoryDefinition story, StoryInstance instance, StoryNode node) {
        List<StoryConnection> outgoing = new ArrayList<StoryConnection>(story.getOutgoing(node.getId()));
        if (outgoing.isEmpty()) {
            return false;
        }
        Collections.sort(outgoing, new Comparator<StoryConnection>() {

            @Override
            public int compare(StoryConnection left, StoryConnection right) {
                return Integer.compare(Integer.parseInt(left.getOutput()), Integer.parseInt(right.getOutput()));
            }
        });
        for (int index = outgoing.size() - 1; index >= 1; index--) {
            instance.pushPending(
                outgoing.get(index)
                    .getTo());
        }
        instance.moveTo(
            outgoing.get(0)
                .getTo());
        return true;
    }

    private static void fail(EntityPlayerMP player, StoryDefinition story, StoryInstance instance, String message) {
        instance.fail(message);
        ChatMessages.error(player, "Story " + story.getId() + " stopped: " + message);
    }
}
