package darkgrey.rpg.compat.customnpcs;

import java.lang.reflect.Field;
import java.lang.reflect.Method;

import net.minecraft.entity.Entity;

/** Optional, linkage-safe bridge to the legacy CustomNPC+ API. */
public final class CustomNpcActorBinding {

    public static final String ACTOR_ID_KEY = "darkgrey_rpg.actor_id";

    private CustomNpcActorBinding() {}

    public static boolean isCustomNpc(Entity entity) {
        try {
            return wrappedNpc(entity) != null;
        } catch (ReflectiveOperationException ignored) {
            return false;
        } catch (LinkageError ignored) {
            return false;
        }
    }

    public static String getActorId(Entity entity) {
        Object npc = requireCustomNpc(entity);
        Object value = invoke(npc, "getStoredData", new Class<?>[] { String.class }, ACTOR_ID_KEY);
        if (value == null) {
            return null;
        }
        String actorId = String.valueOf(value)
            .trim();
        return actorId.isEmpty() ? null : actorId;
    }

    public static void bind(Entity entity, String actorId) {
        if (actorId == null || actorId.trim()
            .isEmpty()) throw new IllegalArgumentException("Actor ID is required.");
        Object npc = requireCustomNpc(entity);
        invoke(npc, "setStoredData", new Class<?>[] { String.class, Object.class }, ACTOR_ID_KEY, actorId.trim());
        updateClient(npc);
    }

    public static void unbind(Entity entity) {
        Object npc = requireCustomNpc(entity);
        invoke(npc, "removeStoredData", new Class<?>[] { String.class }, ACTOR_ID_KEY);
        updateClient(npc);
    }

    public static String getNpcName(Entity entity) {
        Object value = invoke(requireCustomNpc(entity), "getName", new Class<?>[0]);
        return value == null ? "" : String.valueOf(value);
    }

    private static Object requireCustomNpc(Entity entity) {
        if (entity == null) throw new IllegalArgumentException("Entity is required.");
        try {
            Object npc = wrappedNpc(entity);
            if (npc == null) throw new IllegalStateException("CustomNPC+ API wrapper is not initialized");
            return npc;
        } catch (ReflectiveOperationException exception) {
            throw new IllegalArgumentException("Entity is not a CustomNPC+ NPC", exception);
        } catch (LinkageError error) {
            throw new IllegalArgumentException("CustomNPC+ is unavailable", error);
        }
    }

    private static Object wrappedNpc(Entity entity) throws ReflectiveOperationException {
        if (entity == null) return null;
        Class<?> type = Class.forName(
            "noppes.npcs.entity.EntityNPCInterface",
            false,
            entity.getClass()
                .getClassLoader());
        if (!type.isInstance(entity)) return null;
        Field field = type.getField("wrappedNPC");
        return field.get(entity);
    }

    private static Object invoke(Object receiver, String name, Class<?>[] parameterTypes, Object... arguments) {
        try {
            Method method = receiver.getClass()
                .getMethod(name, parameterTypes);
            return method.invoke(receiver, arguments);
        } catch (ReflectiveOperationException exception) {
            throw new IllegalStateException("CustomNPC+ API method '" + name + "' is unavailable", exception);
        } catch (LinkageError error) {
            throw new IllegalStateException("CustomNPC+ API method '" + name + "' is unavailable", error);
        }
    }

    private static void updateClient(Object npc) {
        invoke(npc, "updateClient", new Class<?>[0]);
    }
}
