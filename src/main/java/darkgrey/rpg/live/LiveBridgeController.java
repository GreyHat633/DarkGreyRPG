package darkgrey.rpg.live;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.project.ProjectRepository;

public final class LiveBridgeController {

    private final ProjectRepository repository;
    private final LivePickService picks;
    private final LiveStateSnapshotBuilder snapshots;
    private darkgrey.rpg.project.packages.StoryPackageLoader packages;

    public LiveBridgeController(ProjectRepository repository, LivePickService picks,
        darkgrey.rpg.project.packages.StoryPackageLoader packages) {
        if (repository == null || picks == null || packages == null)
            throw new IllegalArgumentException("Live Bridge services are required.");
        this.repository = repository;
        this.picks = picks;
        this.packages = packages;
        this.snapshots = new LiveStateSnapshotBuilder(repository);
    }

    public void onMessage(final JsonObject message, final LiveMessageSink sink) {
        MainThreadScheduler.scheduleServer(MainThreadScheduler.serverScope(), new Runnable() {

            @Override
            public void run() {
                try {
                    handle(message, sink);
                } catch (RuntimeException failure) {
                    sink.send(
                        response(
                            string(message, "request_id"),
                            false,
                            failure instanceof darkgrey.rpg.session.persistence.CanonicalSessionDataUnavailableException
                                ? darkgrey.rpg.session.persistence.CanonicalSessionDataUnavailableException.PLAYER_MESSAGE
                                : "服务器请求执行失败；请稍后重试。"));
                    MainThreadScheduler.reportFailure("live-bridge", failure);
                }
            }
        }, () -> sink.send(response(string(message, "request_id"), false, "服务器请求未执行；请稍后重试。")));
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
            darkgrey.rpg.project.packages.StoryPackageRuntimeReloader.Result result = darkgrey.rpg.project.packages.StoryPackageRuntimeReloader
                .reload(repository, packages);
            if (result.isPackageSetCommitted()) darkgrey.rpg.project.packages.StoryPackageGenerationLifecycle.reconcile(
                MinecraftServer.getServer()
                    .worldServerForDimension(0).mapStorage,
                packages.getPackages());
            sink.send(
                response(
                    requestId,
                    result.isSuccessful(),
                    result.getProjectReload()
                        .getSummary()));
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
                if (!(value instanceof Entity)) {
                    continue;
                }
                Entity entity = (Entity) value;
                if (!darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds(entity)
                    .contains(actorId)) {
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
