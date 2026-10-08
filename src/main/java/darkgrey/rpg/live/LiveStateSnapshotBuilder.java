package darkgrey.rpg.live;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;

import darkgrey.rpg.project.ProjectRepository;

public final class LiveStateSnapshotBuilder {

    private final ProjectRepository repository;

    public LiveStateSnapshotBuilder(ProjectRepository repository) {
        if (repository == null) throw new IllegalArgumentException("Project repository is required.");
        this.repository = repository;
    }

    public JsonObject build() {
        return canonicalSnapshot();
    }

    /** Production projection reads only Canonical state; it never instantiates legacy player SavedData. */
    private JsonObject canonicalSnapshot() {
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
            "stories",
            repository.getSnapshot()
                .getCanonicalStories()
                .size());
        counts.addProperty(
            "sessions",
            repository.getSnapshot()
                .getCanonicalSessions()
                .size());
        counts.addProperty(
            "tasks",
            repository.getSnapshot()
                .getCanonicalTasks()
                .size());
        root.add("counts", counts);
        root.add("actors", liveActors());
        JsonArray players = new JsonArray();
        MinecraftServer server = MinecraftServer.getServer();
        if (server != null && server.getConfigurationManager() != null)
            for (Object value : server.getConfigurationManager().playerEntityList) {
                if (!(value instanceof EntityPlayerMP)) continue;
                EntityPlayerMP player = (EntityPlayerMP) value;
                JsonObject entry = new JsonObject();
                entry.addProperty(
                    "uuid",
                    player.getUniqueID()
                        .toString());
                entry.addProperty("name", player.getCommandSenderName());
                entry.addProperty("dimension", player.dimension);
                entry.addProperty("testing", false);
                JsonArray storyStates = new JsonArray();
                for (String id : repository.getSnapshot()
                    .getCanonicalStories()
                    .keySet()) {
                    darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot snapshot = darkgrey.rpg.DarkGreyRpg
                        .getCanonicalStoryManager()
                        .snapshot(player, id);
                    if (snapshot == null) continue;
                    JsonObject story = new JsonObject();
                    story.addProperty("id", id);
                    story.addProperty("story_id", id);
                    story.addProperty(
                        "state",
                        snapshot.getRuntimeSnapshot()
                            .getStatus()
                            .name());
                    story.addProperty(
                        "current_node",
                        snapshot.getRuntimeSnapshot()
                            .getCurrentNodeId());
                    story.addProperty(
                        "waiting_event",
                        snapshot.getRuntimeSnapshot()
                            .getWaitKind()
                            .name());
                    story.add("previous_nodes", new JsonArray());
                    story.add("conditions", new JsonObject());
                    story.addProperty("error", "");
                    story.addProperty("explanation", "Canonical Story cursor");
                    storyStates.add(story);
                }
                entry.add("stories", storyStates);
                JsonArray tasks = new JsonArray();
                for (darkgrey.rpg.task.journal.CanonicalTaskJournalEntry task : darkgrey.rpg.DarkGreyRpg
                    .getCanonicalTaskManager()
                    .getJournal(player)) {
                    JsonObject item = new JsonObject();
                    item.addProperty("id", task.getTaskResourceId());
                    item.addProperty("story_id", task.getStoryId());
                    item.addProperty("placement_id", task.getTaskNodePlacementId());
                    item.addProperty("title", task.getTitle());
                    item.addProperty(
                        "status",
                        task.getStatus()
                            .name());
                    JsonArray objectives = new JsonArray();
                    for (darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow row : task.getObjectiveRows()) {
                        JsonObject objective = new JsonObject();
                        objective.addProperty("id", row.getObjectiveId());
                        objective.addProperty("description", row.getDescription());
                        objective.addProperty("current", row.getCurrent());
                        objective.addProperty("required", row.getRequiredProgress());
                        objectives.add(objective);
                    }
                    item.add("objectives", objectives);
                    tasks.add(item);
                }
                entry.add("tasks", tasks);
                entry.add("quests", tasks);
                entry.add("variables", new JsonArray());
                players.add(entry);
            }
        root.add("players", players);
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
                if (!(value instanceof Entity)) {
                    continue;
                }
                Entity entity = (Entity) value;
                for (String actorId : darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds(entity)) {
                    JsonObject actor = new JsonObject();
                    actor.addProperty("actor_id", actorId);
                    actor.addProperty("name", entity.getCommandSenderName());
                    actor.addProperty("entity_id", entity.getEntityId());
                    actor.addProperty("dimension", entity.dimension);
                    actor.addProperty("x", entity.posX);
                    actor.addProperty("y", entity.posY);
                    actor.addProperty("z", entity.posZ);
                    actors.add(actor);
                }
            }
        }
        return actors;
    }

}
