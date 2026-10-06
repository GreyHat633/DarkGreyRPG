package darkgrey.rpg.compat.customnpcs;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.LinkedHashMap;
import java.util.Map;

import noppes.npcs.api.entity.ICustomNpc;
import noppes.npcs.entity.EntityCustomNpc;
import sun.misc.Unsafe;

public final class CustomNpcActorBindingProbe {

    private CustomNpcActorBindingProbe() {}

    public static void main(String[] arguments) throws Exception {
        Map<String, Object> persistedData = new LinkedHashMap<String, Object>();
        int[] clientUpdates = new int[1];
        ICustomNpc<?> wrapper = createWrapper(persistedData, clientUpdates, "Acceptance Detective");
        EntityCustomNpc firstEntity = allocateNpc(wrapper);

        require(CustomNpcActorBinding.isCustomNpc(firstEntity), "CustomNPC+ entity was not recognized");
        persistedData.put("darkgrey_rpg.actor_id", "legacy:detective");
        Field id = net.minecraft.entity.Entity.class.getDeclaredField("entityUniqueID");
        id.setAccessible(true);
        id.set(firstEntity, java.util.UUID.randomUUID());
        darkgrey.rpg.identity.NpcIdentitySavedData identities = new darkgrey.rpg.identity.NpcIdentitySavedData();
        require(
            !darkgrey.rpg.identity.EntityDgrIdentityResolver
                .resolve(firstEntity, "customnpcs:customnpc", identities, null)
                .isResolved(),
            "Legacy CNPC stored identity leaked into current resolution");
        String actor = "ST-2345-6789-ABCD-EFGH~actor~detective";
        identities.bind(
            actor,
            new darkgrey.rpg.identity.NpcHostIdentity(firstEntity.getUniqueID(), "customnpcs:customnpc", 0));
        require(
            actor.equals(
                darkgrey.rpg.identity.EntityDgrIdentityResolver
                    .resolve(firstEntity, "customnpcs:customnpc", identities, null)
                    .getActorId()),
            "Current external identity did not resolve");
        identities.unbindHost(firstEntity.getUniqueID());
        require(
            !darkgrey.rpg.identity.EntityDgrIdentityResolver
                .resolve(firstEntity, "customnpcs:customnpc", identities, null)
                .isResolved(),
            "Unbind resurrected legacy stored identity");
        require("Acceptance Detective".equals(CustomNpcActorBinding.getNpcName(firstEntity)), "NPC name unavailable");
        require("legacy:detective".equals(persistedData.get("darkgrey_rpg.actor_id")), "Probe modified legacy data");
        require(clientUpdates[0] == 0, "Identity operations mutated CNPC client data");
        System.out.println("CUSTOMNPC_CURRENT_EXTERNAL_IDENTITY=PASS");
        System.out.println("CUSTOMNPC_LEGACY_IDENTITY_IGNORED=PASS");
        System.out.println("CUSTOMNPC_UNBIND_NO_LEGACY_RESURRECTION=PASS");
    }

    private static EntityCustomNpc allocateNpc(ICustomNpc<?> wrapper) throws Exception {
        Field unsafeField = Unsafe.class.getDeclaredField("theUnsafe");
        unsafeField.setAccessible(true);
        Unsafe unsafe = (Unsafe) unsafeField.get(null);
        EntityCustomNpc entity = (EntityCustomNpc) unsafe.allocateInstance(EntityCustomNpc.class);
        entity.wrappedNPC = wrapper;
        return entity;
    }

    private static ICustomNpc<?> createWrapper(final Map<String, Object> data, final int[] updates, final String name) {
        InvocationHandler handler = new InvocationHandler() {

            @Override
            public Object invoke(Object proxy, Method method, Object[] arguments) {
                String methodName = method.getName();
                if ("getStoredData".equals(methodName)) {
                    return data.get(arguments[0]);
                }
                if ("setStoredData".equals(methodName)) {
                    data.put((String) arguments[0], arguments[1]);
                    return null;
                }
                if ("removeStoredData".equals(methodName)) {
                    data.remove(arguments[0]);
                    return null;
                }
                if ("updateClient".equals(methodName)) {
                    updates[0]++;
                    return null;
                }
                if ("getName".equals(methodName)) {
                    return name;
                }
                if ("toString".equals(methodName)) {
                    return "CustomNpcActorBindingProbeWrapper";
                }
                return defaultValue(method.getReturnType());
            }
        };
        return (ICustomNpc<?>) Proxy.newProxyInstance(
            CustomNpcActorBindingProbe.class.getClassLoader(),
            new Class<?>[] { ICustomNpc.class },
            handler);
    }

    private static Object defaultValue(Class<?> type) {
        if (!type.isPrimitive()) {
            return null;
        }
        if (type == Boolean.TYPE) {
            return Boolean.FALSE;
        }
        if (type == Character.TYPE) {
            return Character.valueOf('\0');
        }
        if (type == Byte.TYPE) {
            return Byte.valueOf((byte) 0);
        }
        if (type == Short.TYPE) {
            return Short.valueOf((short) 0);
        }
        if (type == Integer.TYPE) {
            return Integer.valueOf(0);
        }
        if (type == Long.TYPE) {
            return Long.valueOf(0L);
        }
        if (type == Float.TYPE) {
            return Float.valueOf(0F);
        }
        if (type == Double.TYPE) {
            return Double.valueOf(0D);
        }
        return null;
    }

    private static void require(boolean condition, String message) {
        if (!condition) {
            throw new IllegalStateException(message);
        }
    }
}
