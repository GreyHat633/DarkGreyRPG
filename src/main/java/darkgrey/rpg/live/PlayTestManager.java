package darkgrey.rpg.live;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.server.MinecraftServer;

import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.runtime.PlayerQuestData;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryNode;
import darkgrey.rpg.story.StoryNodeType;
import darkgrey.rpg.story.runtime.PlayerStoryData;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryRuntimeService;

public final class PlayTestManager {

    private final ProjectRepository repository;
    private final StoryRuntimeService stories;
    private final Map<UUID, TestSnapshot> snapshots = new LinkedHashMap<UUID, TestSnapshot>();

    public PlayTestManager(ProjectRepository repository, StoryRuntimeService stories) {
        this.repository = repository;
        this.stories = stories;
    }

    public boolean start(EntityPlayerMP player, String storyId, String nodeId) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(storyId);
        if (story == null || story.getNode(nodeId) == null || snapshots.containsKey(player.getUniqueID())) {
            return false;
        }
        snapshots.put(
            player.getUniqueID(),
            new TestSnapshot(
                PlayerQuestData.get(player)
                    .snapshotPlayer(player),
                PlayerStoryData.get(player)
                    .snapshotPlayer(player),
                stories.snapshotPlayer(player)));
        for (StoryNode node : story.getNodes()) {
            if (referencesQuest(node.getType())) {
                PlayerQuestData.get(player)
                    .resetQuest(player, node.getProperty("quest_id"));
            }
        }
        PlayerStoryData.get(player)
            .clearStory(player, storyId);
        return stories.startAt(player, storyId, nodeId);
    }

    public boolean stop(EntityPlayerMP player) {
        TestSnapshot snapshot = snapshots.remove(player.getUniqueID());
        if (snapshot == null) {
            return false;
        }
        PlayerQuestData.get(player)
            .restorePlayer(player, snapshot.quests);
        PlayerStoryData.get(player)
            .restorePlayer(player, snapshot.storyVariables);
        stories.restorePlayer(player, snapshot.storyInstances);
        return true;
    }

    public boolean isTesting(EntityPlayerMP player) {
        return snapshots.containsKey(player.getUniqueID());
    }

    public void restoreAllOnlinePlayers() {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null || server.getConfigurationManager() == null) {
            return;
        }
        for (Object value : server.getConfigurationManager().playerEntityList) {
            if (value instanceof EntityPlayerMP && snapshots.containsKey(((EntityPlayerMP) value).getUniqueID())) {
                stop((EntityPlayerMP) value);
            }
        }
    }

    private static boolean referencesQuest(StoryNodeType type) {
        return type == StoryNodeType.QUEST_COMPLETED || type == StoryNodeType.START_QUEST
            || type == StoryNodeType.COMPLETE_QUEST
            || type == StoryNodeType.QUEST_STATE;
    }

    private static final class TestSnapshot {

        private final NBTTagCompound quests;
        private final NBTTagCompound storyVariables;
        private final Map<String, StoryInstance.Snapshot> storyInstances;

        private TestSnapshot(NBTTagCompound quests, NBTTagCompound storyVariables,
            Map<String, StoryInstance.Snapshot> storyInstances) {
            this.quests = quests;
            this.storyVariables = storyVariables;
            this.storyInstances = storyInstances;
        }
    }
}
