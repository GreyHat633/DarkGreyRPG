package darkgrey.rpg.live;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.story.runtime.StoryRuntimeService;

public final class LiveBridgeController {

    private final ProjectRepository repository;
    private final DialogueSessionManager dialogues;
    private final StoryRuntimeService stories;
    private final LivePickService picks;
    private final PlayTestManager playTests;
    private final LiveStateSnapshotBuilder snapshots;

    public LiveBridgeController(ProjectRepository repository, DialogueSessionManager dialogues,
        StoryRuntimeService stories, LivePickService picks, PlayTestManager playTests) {
        this.repository = repository;
        this.dialogues = dialogues;
        this.stories = stories;
        this.picks = picks;
        this.playTests = playTests;
        this.snapshots = new LiveStateSnapshotBuilder(repository, stories, playTests);
    }

    public void onMessage(final JsonObject message, final LiveMessageSink sink) {
        MainThreadScheduler.scheduleServer(new Runnable() {

            @Override
            public void run() {
                handle(message, sink);
            }
        });
    }

    private void handle(JsonObject message, LiveMessageSink sink) {
        String type = string(message, "type");
        String requestId = string(message, "request_id");
        if ("state.request".equals(type)) {
            JsonObject snapshot = snapshots.build();
            addRequestId(snapshot, requestId);
            sink.send(snapshot);
            return;
        }
        if ("project.reload".equals(type)) {
            ProjectRepository.ReloadResult result = repository.reload();
            if (result.isSuccessful()) {
                dialogues.clearSessions();
                stories.resetInstances();
            }
            sink.send(response(requestId, result.isSuccessful(), result.getSummary()));
            return;
        }
        EntityPlayerMP player = findPlayer(string(message, "player"));
        if (player == null) {
            sink.send(response(requestId, false, "No matching online player"));
            return;
        }
        if ("pick.begin".equals(type)) {
            String kind = string(message, "kind");
            if (!isPickKind(kind)) {
                sink.send(response(requestId, false, "Unsupported pick kind: " + kind));
                return;
            }
            picks.begin(player, kind, requestId, sink);
            sink.send(response(requestId, true, "Pick mode started: " + kind));
            return;
        }
        if ("locate.actor".equals(type)) {
            int found = locateActor(player, string(message, "actor_id"));
            sink.send(response(requestId, found > 0, "Located " + found + " Actor instance(s)"));
            return;
        }
        if ("test.start".equals(type)) {
            String storyId = string(message, "story_id");
            String nodeId = string(message, "node_id");
            boolean started = playTests.start(player, storyId, nodeId);
            sink.send(response(requestId, started, started ? "Play test started" : "Could not start play test"));
            return;
        }
        if ("test.stop".equals(type)) {
            boolean stopped = playTests.stop(player);
            sink.send(response(requestId, stopped, stopped ? "RPG state restored" : "No active play test"));
            return;
        }
        sink.send(response(requestId, false, "Unsupported message type: " + type));
    }

    private static EntityPlayerMP findPlayer(String requested) {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null || server.getConfigurationManager() == null) {
            return null;
        }
        for (Object value : server.getConfigurationManager().playerEntityList) {
            if (!(value instanceof EntityPlayerMP)) {
                continue;
            }
            EntityPlayerMP player = (EntityPlayerMP) value;
            if (requested.isEmpty() || requested.equalsIgnoreCase(player.getCommandSenderName())
                || requested.equalsIgnoreCase(
                    player.getUniqueID()
                        .toString())) {
                return player;
            }
        }
        return null;
    }

    private static int locateActor(EntityPlayerMP player, String actorId) {
        MinecraftServer server = MinecraftServer.getServer();
        int found = 0;
        if (server == null || server.worldServers == null) {
            return found;
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
                if (!actorId.equals(CustomNpcActorBinding.getActorId(entity))) {
                    continue;
                }
                world.func_147487_a(
                    "happyVillager",
                    entity.posX,
                    entity.posY + entity.height * 0.7D,
                    entity.posZ,
                    40,
                    0.7D,
                    1.0D,
                    0.7D,
                    0.02D);
                found++;
            }
        }
        if (found > 0) {
            darkgrey.rpg.runtime.ChatMessages.info(player, "Locate Actor '" + actorId + "': highlighted " + found);
        }
        return found;
    }

    private static boolean isPickKind(String kind) {
        return "actor".equals(kind) || "position".equals(kind)
            || "region".equals(kind)
            || "item".equals(kind)
            || "entity_type".equals(kind);
    }

    private static JsonObject response(String requestId, boolean success, String message) {
        JsonObject response = new JsonObject();
        response.addProperty("type", "response");
        response.addProperty("ok", success);
        response.addProperty("message", message);
        addRequestId(response, requestId);
        return response;
    }

    private static void addRequestId(JsonObject message, String requestId) {
        if (!requestId.isEmpty()) {
            message.addProperty("request_id", requestId);
        }
    }

    private static String string(JsonObject message, String field) {
        JsonElement value = message.get(field);
        return value != null && value.isJsonPrimitive() ? value.getAsString() : "";
    }
}
