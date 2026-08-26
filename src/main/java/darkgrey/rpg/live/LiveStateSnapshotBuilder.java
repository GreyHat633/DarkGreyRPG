package darkgrey.rpg.live;

import java.util.Map;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;
import darkgrey.rpg.quest.runtime.PlayerQuestData;
import darkgrey.rpg.quest.runtime.QuestProgressRecord;
import darkgrey.rpg.story.runtime.PlayerStoryData;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryRuntimeService;

public final class LiveStateSnapshotBuilder {

    private final ProjectRepository repository;
    private final StoryRuntimeService stories;
    private final PlayTestManager playTests;

    public LiveStateSnapshotBuilder(ProjectRepository repository, StoryRuntimeService stories,
        PlayTestManager playTests) {
        this.repository = repository;
        this.stories = stories;
        this.playTests = playTests;
    }

    public JsonObject build() {
        JsonObject root = new JsonObject();
        root.addProperty("type", "state.snapshot");
        root.addProperty(
            "project_id",
            repository.getSnapshot()
                .getProject()
                .getId());
        root.addProperty(
            "project_name",
            repository.getSnapshot()
                .getProject()
                .getDisplayName());
        JsonObject counts = new JsonObject();
        counts.addProperty(
            "actors",
            repository.getSnapshot()
                .getActors()
                .size());
        counts.addProperty(
            "dialogues",
            repository.getSnapshot()
                .getDialogues()
                .size());
        counts.addProperty(
            "quests",
            repository.getSnapshot()
                .getQuests()
                .size());
        counts.addProperty(
            "stories",
            repository.getSnapshot()
                .getStories()
                .size());
        root.add("counts", counts);
        root.add("actors", liveActors());
        root.add("players", players());
        return root;
    }

    private JsonArray liveActors() {
        JsonArray actors = new JsonArray();
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null || server.worldServers == null) {
            return actors;
        }
        for (WorldServer world : server.worldServers) {
            if (world == null) {
                continue;
            }
            for (Object value : world.loadedEntityList) {
                if (!(value instanceof Entity) || !CustomNpcActorBinding.isCustomNpc((Entity) value)) {
                    continue;
                }
                Entity entity = (Entity) value;
                String actorId = CustomNpcActorBinding.getActorId(entity);
                if (actorId == null) {
                    continue;
                }
                JsonObject actor = new JsonObject();
                actor.addProperty("actor_id", actorId);
                actor.addProperty("name", CustomNpcActorBinding.getNpcName(entity));
                actor.addProperty("entity_id", entity.getEntityId());
                actor.addProperty("dimension", entity.dimension);
                actor.addProperty("x", entity.posX);
                actor.addProperty("y", entity.posY);
                actor.addProperty("z", entity.posZ);
                actors.add(actor);
            }
        }
        return actors;
    }

    private JsonArray players() {
        JsonArray players = new JsonArray();
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null || server.getConfigurationManager() == null) {
            return players;
        }
        for (Object value : server.getConfigurationManager().playerEntityList) {
            if (value instanceof EntityPlayerMP) {
                players.add(player((EntityPlayerMP) value));
            }
        }
        return players;
    }

    private JsonObject player(EntityPlayerMP player) {
        JsonObject result = new JsonObject();
        result.addProperty(
            "uuid",
            player.getUniqueID()
                .toString());
        result.addProperty("name", player.getCommandSenderName());
        result.addProperty("dimension", player.dimension);
        result.addProperty("testing", playTests.isTesting(player));
        result.add("quests", quests(player));
        result.add("stories", storyInstances(player));
        result.add("variables", variables(player));
        return result;
    }

    private JsonArray quests(EntityPlayerMP player) {
        JsonArray quests = new JsonArray();
        for (QuestProgressRecord record : PlayerQuestData.get(player)
            .getQuests(player)) {
            JsonObject quest = new JsonObject();
            quest.addProperty("id", record.getQuestId());
            quest.addProperty(
                "status",
                record.getStatus()
                    .name());
            JsonArray objectives = new JsonArray();
            QuestDefinition definition = repository.getSnapshot()
                .getQuest(record.getQuestId());
            if (definition != null) {
                for (QuestObjective objective : definition.getObjectives()) {
                    JsonObject progress = new JsonObject();
                    progress.addProperty("id", objective.getId());
                    progress.addProperty("description", objective.getDescription());
                    progress.addProperty("current", record.getProgress(objective.getId()));
                    progress.addProperty("required", objective.getRequiredAmount());
                    objectives.add(progress);
                }
            }
            quest.add("objectives", objectives);
            quests.add(quest);
        }
        return quests;
    }

    private JsonArray storyInstances(EntityPlayerMP player) {
        JsonArray result = new JsonArray();
        for (Map.Entry<String, StoryInstance> entry : stories.getInstances(player)
            .entrySet()) {
            StoryInstance instance = entry.getValue();
            JsonObject story = new JsonObject();
            story.addProperty("id", entry.getKey());
            story.addProperty(
                "state",
                instance.getState()
                    .name());
            story.addProperty("current_node", instance.getCurrentNodeId());
            story.addProperty("last_node", instance.getLastNodeId());
            story.addProperty("last_result", instance.getLastResult());
            story.addProperty("explanation", instance.getLastExplanation());
            story.addProperty("error", instance.getError());
            story.addProperty(
                "waiting_event",
                instance.getState()
                    .name()
                    .equals("WAITING") ? instance.getCurrentNodeId() : "");
            JsonArray previous = new JsonArray();
            for (String nodeId : instance.getPreviousNodes()) {
                previous.add(new com.google.gson.JsonPrimitive(nodeId));
            }
            story.add("previous_nodes", previous);
            JsonObject conditions = new JsonObject();
            for (Map.Entry<String, String> condition : instance.getNodeExplanations()
                .entrySet()) {
                conditions.addProperty(condition.getKey(), condition.getValue());
            }
            story.add("conditions", conditions);
            result.add(story);
        }
        return result;
    }

    private JsonObject variables(EntityPlayerMP player) {
        JsonObject storiesObject = new JsonObject();
        for (Map.Entry<String, Map<String, String>> story : PlayerStoryData.get(player)
            .getVariables(player)
            .entrySet()) {
            JsonObject variables = new JsonObject();
            for (Map.Entry<String, String> variable : story.getValue()
                .entrySet()) {
                variables.addProperty(variable.getKey(), variable.getValue());
            }
            storiesObject.add(story.getKey(), variables);
        }
        return storiesObject;
    }
}
