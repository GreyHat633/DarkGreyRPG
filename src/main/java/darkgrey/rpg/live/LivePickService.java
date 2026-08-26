package darkgrey.rpg.live;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityList;
import net.minecraft.entity.item.EntityItem;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

import com.google.gson.JsonObject;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.runtime.ChatMessages;

public final class LivePickService {

    private final Map<UUID, PickRequest> requests = new ConcurrentHashMap<UUID, PickRequest>();

    public void begin(EntityPlayerMP player, String kind, String requestId, LiveMessageSink sink) {
        requests.put(player.getUniqueID(), new PickRequest(kind, requestId, sink));
        ChatMessages.info(player, instructions(kind));
    }

    public boolean handleEntity(EntityPlayerMP player, Entity target) {
        PickRequest request = requests.get(player.getUniqueID());
        if (request == null) {
            return false;
        }
        JsonObject value = new JsonObject();
        if ("actor".equals(request.kind)) {
            if (!CustomNpcActorBinding.isCustomNpc(target)) {
                ChatMessages.error(player, "Pick Actor requires a CustomNPC+ NPC.");
                return true;
            }
            String actorId = CustomNpcActorBinding.getActorId(target);
            if (actorId == null) {
                ChatMessages.error(player, "The selected NPC has no Actor binding.");
                return true;
            }
            value.addProperty("actor_id", actorId);
            addEntity(value, target);
            finish(player, request, value);
            return true;
        }
        if ("entity_type".equals(request.kind)) {
            String entityType = EntityList.getEntityString(target);
            value.addProperty(
                "entity_type",
                entityType == null ? target.getClass()
                    .getSimpleName() : entityType);
            addEntity(value, target);
            finish(player, request, value);
            return true;
        }
        if ("item".equals(request.kind) && target instanceof EntityItem) {
            ItemStack stack = ((EntityItem) target).getEntityItem();
            Object itemId = Item.itemRegistry.getNameForObject(stack.getItem());
            value.addProperty("item", String.valueOf(itemId));
            value.addProperty("metadata", stack.getItemDamage());
            finish(player, request, value);
            return true;
        }
        ChatMessages.error(player, "This target cannot satisfy the active " + request.kind + " pick.");
        return true;
    }

    public boolean handleBlock(EntityPlayerMP player, int x, int y, int z) {
        PickRequest request = requests.get(player.getUniqueID());
        if (request == null || (!"position".equals(request.kind) && !"region".equals(request.kind))) {
            return false;
        }
        if ("position".equals(request.kind)) {
            JsonObject value = position(player, x, y, z);
            finish(player, request, value);
            return true;
        }
        if (request.firstPosition == null) {
            request.firstPosition = position(player, x, y, z);
            JsonObject progress = envelope("pick.progress", request);
            progress.addProperty("message", "First region corner selected; pick the opposite corner.");
            progress.add("first", request.firstPosition);
            request.sink.send(progress);
            ChatMessages.info(player, "First region corner selected. Right-click the opposite corner.");
            return true;
        }
        JsonObject value = new JsonObject();
        value.add("min", minimum(request.firstPosition, position(player, x, y, z)));
        value.add("max", maximum(request.firstPosition, position(player, x, y, z)));
        finish(player, request, value);
        return true;
    }

    private void finish(EntityPlayerMP player, PickRequest request, JsonObject value) {
        requests.remove(player.getUniqueID());
        JsonObject result = envelope("pick.result", request);
        result.add("value", value);
        request.sink.send(result);
        ChatMessages.success(player, "Live pick completed: " + request.kind);
    }

    private static JsonObject position(EntityPlayerMP player, int x, int y, int z) {
        JsonObject result = new JsonObject();
        result.addProperty("dimension", player.dimension);
        result.addProperty("x", x);
        result.addProperty("y", y);
        result.addProperty("z", z);
        return result;
    }

    private static JsonObject minimum(JsonObject left, JsonObject right) {
        JsonObject value = new JsonObject();
        value.addProperty(
            "dimension",
            left.get("dimension")
                .getAsInt());
        value.addProperty(
            "x",
            Math.min(
                left.get("x")
                    .getAsInt(),
                right.get("x")
                    .getAsInt()));
        value.addProperty(
            "y",
            Math.min(
                left.get("y")
                    .getAsInt(),
                right.get("y")
                    .getAsInt()));
        value.addProperty(
            "z",
            Math.min(
                left.get("z")
                    .getAsInt(),
                right.get("z")
                    .getAsInt()));
        return value;
    }

    private static JsonObject maximum(JsonObject left, JsonObject right) {
        JsonObject value = new JsonObject();
        value.addProperty(
            "dimension",
            left.get("dimension")
                .getAsInt());
        value.addProperty(
            "x",
            Math.max(
                left.get("x")
                    .getAsInt(),
                right.get("x")
                    .getAsInt()));
        value.addProperty(
            "y",
            Math.max(
                left.get("y")
                    .getAsInt(),
                right.get("y")
                    .getAsInt()));
        value.addProperty(
            "z",
            Math.max(
                left.get("z")
                    .getAsInt(),
                right.get("z")
                    .getAsInt()));
        return value;
    }

    private static void addEntity(JsonObject value, Entity entity) {
        value.addProperty("entity_id", entity.getEntityId());
        value.addProperty("dimension", entity.dimension);
        value.addProperty("x", entity.posX);
        value.addProperty("y", entity.posY);
        value.addProperty("z", entity.posZ);
    }

    private static JsonObject envelope(String type, PickRequest request) {
        JsonObject message = new JsonObject();
        message.addProperty("type", type);
        message.addProperty("request_id", request.requestId);
        message.addProperty("kind", request.kind);
        return message;
    }

    private static String instructions(String kind) {
        if ("position".equals(kind)) {
            return "Live Pick Position: right-click a block with the DarkGrey RPG Editor Tool.";
        }
        if ("region".equals(kind)) {
            return "Live Pick Region: right-click two opposite block corners with the Editor Tool.";
        }
        if ("item".equals(kind)) {
            return "Live Pick Item: right-click a dropped item entity with the Editor Tool.";
        }
        return "Live Pick " + kind + ": right-click the target entity with the Editor Tool.";
    }

    private static final class PickRequest {

        private final String kind;
        private final String requestId;
        private final LiveMessageSink sink;
        private JsonObject firstPosition;

        private PickRequest(String kind, String requestId, LiveMessageSink sink) {
            this.kind = kind;
            this.requestId = requestId;
            this.sink = sink;
        }
    }
}
