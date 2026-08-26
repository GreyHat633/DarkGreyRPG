package darkgrey.rpg.compat.customnpcs;

import net.minecraft.entity.Entity;

import noppes.npcs.api.entity.ICustomNpc;
import noppes.npcs.entity.EntityNPCInterface;

public final class CustomNpcActorBinding {

    public static final String ACTOR_ID_KEY = "darkgrey_rpg.actor_id";

    private CustomNpcActorBinding() {}

    public static boolean isCustomNpc(Entity entity) {
        return entity instanceof EntityNPCInterface && ((EntityNPCInterface) entity).wrappedNPC != null;
    }

    public static String getActorId(Entity entity) {
        ICustomNpc<?> npc = requireCustomNpc(entity);
        Object value = npc.getStoredData(ACTOR_ID_KEY);
        if (value == null) {
            return null;
        }
        String actorId = String.valueOf(value)
            .trim();
        return actorId.isEmpty() ? null : actorId;
    }

    public static void bind(Entity entity, String actorId) {
        ICustomNpc<?> npc = requireCustomNpc(entity);
        npc.setStoredData(ACTOR_ID_KEY, actorId);
        npc.updateClient();
    }

    public static void unbind(Entity entity) {
        ICustomNpc<?> npc = requireCustomNpc(entity);
        npc.removeStoredData(ACTOR_ID_KEY);
        npc.updateClient();
    }

    public static String getNpcName(Entity entity) {
        return requireCustomNpc(entity).getName();
    }

    private static ICustomNpc<?> requireCustomNpc(Entity entity) {
        if (!(entity instanceof EntityNPCInterface)) {
            throw new IllegalArgumentException("Entity is not a CustomNPC+ NPC");
        }
        ICustomNpc<?> wrappedNpc = ((EntityNPCInterface) entity).wrappedNPC;
        if (wrappedNpc == null) {
            throw new IllegalStateException("CustomNPC+ API wrapper is not initialized");
        }
        return wrappedNpc;
    }
}
