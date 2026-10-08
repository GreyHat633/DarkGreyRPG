package darkgrey.rpg.persistence;

import java.io.File;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.nio.file.Files;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.List;

import cpw.mods.fml.common.FMLCommonHandler;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.event.FMLInitializationEvent;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

/** Isolated Forge acceptance driver. Never included in the DGR runtime JAR or Studio delivery. */
@Mod(modid = "dgr0401storagetest", name = "DGR 0401 isolated storage acceptance", version = "1")
public final class StorageServer0401Driver {

    private boolean checked;
    private boolean dynamicFinished;
    private Object dynamicStorage, dynamicData, dynamicIds, dynamicSnapshot, dynamicIdentitySnapshot;
    private int[] dynamicBuckets;
    private long dynamicStarted, dynamicRevision, dynamicIdentityRevision;
    private String dynamicFileHash, dynamicIdentityFileHash;
    private int dynamicTicks;

    @Mod.EventHandler
    public void init(FMLInitializationEvent event) {
        FMLCommonHandler.instance()
            .bus()
            .register(this);
    }

    @SubscribeEvent
    public void tick(TickEvent.ServerTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        String mode = System.getProperty("dgr0401.storageMode");
        if (mode == null) return;
        if (checked) {
            if ("dynamic-shrink".equals(mode) && !dynamicFinished && ++dynamicTicks % 20 == 0) {
                try {
                    finishDynamicShrink();
                } catch (Throwable failure) {
                    dynamicFinished = true;
                    failure.printStackTrace();
                    System.out.println("DGR0401_REAL_FORGE_STORAGE=dynamic-shrink FAIL");
                }
            }
            return;
        }
        checked = true;
        try {
            Class<?> serverType = Class.forName("net.minecraft.server.MinecraftServer");
            Object server = null;
            for (Method method : serverType.getMethods())
                if (java.lang.reflect.Modifier.isStatic(method.getModifiers()) && method.getReturnType() == serverType
                    && method.getParameterTypes().length == 0) server = method.invoke(null);
            if (server == null) throw new AssertionError("Server singleton missing");
            Object world = null;
            for (Method method : serverType.getMethods()) if (method.getReturnType()
                .getName()
                .equals("net.minecraft.world.WorldServer")
                && java.util.Arrays.equals(method.getParameterTypes(), new Class<?>[] { int.class }))
                world = method.invoke(server, 0);
            if (world == null) throw new AssertionError("Overworld missing");
            Object storage = worldMapStorage(world);
            if (storage == null) throw new AssertionError("MapStorage missing");
            if (mode.startsWith("dynamic-")) {
                Class<?> selectionType = Class.forName("darkgrey.rpg.nominator.NominatorSavedData");
                require(
                    get(selectionType, storage) == selectionType.getMethod("get")
                        .invoke(null),
                    "Driver must use the production overworld mapStorage");
                System.out.println("DGR0401_DYNAMIC_STORAGE=production-overworld-mapStorage PASS");
            }
            if ("capacity".equals(mode)) capacity(storage);
            else if ("failure".equals(mode)) failure(storage);
            else if ("dynamic-seed".equals(mode)) dynamicSeed(storage);
            else if ("dynamic-shrink".equals(mode)) {
                startDynamicShrink(storage);
                return;
            } else if ("dynamic-verify".equals(mode)) dynamicVerify(storage);
            else throw new AssertionError("Unknown isolated test mode");
            System.out.println("DGR0401_REAL_FORGE_STORAGE=" + mode + " PASS");
        } catch (Throwable failure) {
            dynamicFinished = true;
            failure.printStackTrace();
            System.out.println("DGR0401_REAL_FORGE_STORAGE=" + mode + " FAIL");
        }
    }

    private static void dynamicSeed(Object storage) throws Exception {
        Object data = get(Class.forName("darkgrey.rpg.nominator.NominatorSavedData"), storage);
        Object ids = get(Class.forName("darkgrey.rpg.identity.NpcIdentitySavedData"), storage);
        require(((List<?>) invoke(data, "bindings")).isEmpty(), "Dynamic seed world is not empty");
        Class<?> binding = Class.forName("darkgrey.rpg.nominator.NominatorEntityBinding");
        Class<?> host = Class.forName("darkgrey.rpg.identity.NpcHostIdentity");
        for (int i = 1; i <= 6500; i++) {
            java.util.UUID uuid = dynamicUuid(i);
            String actor = dynamicActor(i);
            Object selected = binding.getConstructor(java.util.UUID.class, String.class, List.class, String.class)
                .newInstance(
                    uuid,
                    actor,
                    java.util.Collections.singletonList("ST-AAAA-BBBB-CCCC-DDDD~actor~group"),
                    "ST-AAAA-BBBB-CCCC-DDDD");
            data.getClass()
                .getMethod("put", binding)
                .invoke(data, selected);
            Object actualHost = host.getConstructor(java.util.UUID.class, String.class, int.class)
                .newInstance(uuid, "minecraft:zombie", 0);
            ids.getClass()
                .getMethod("bind", String.class, host)
                .invoke(ids, actor, actualHost);
        }
        save(storage);
        require(((List<?>) invoke(data, "bindings")).size() == 6500, "Dynamic count");
        System.out.println(
            "DGR0401_DYNAMIC_SEED count=6500 capacity=" + capacityValue(data)
                + " revision="
                + invoke(data, "getRevision"));
    }

    private void startDynamicShrink(Object storage) throws Exception {
        dynamicStorage = storage;
        dynamicData = get(Class.forName("darkgrey.rpg.nominator.NominatorSavedData"), storage);
        dynamicIds = get(Class.forName("darkgrey.rpg.identity.NpcIdentitySavedData"), storage);
        require(((List<?>) invoke(dynamicData, "bindings")).size() == 6500, "6500 bindings did not restart");
        require(((List<?>) invoke(dynamicIds, "bindings")).size() == 6500, "6500 NPC identities did not restart");
        editLast(dynamicData, dynamicIds, 6500);
        for (int i = 101; i <= 6500; i++) {
            dynamicIds.getClass()
                .getMethod("unbindHost", java.util.UUID.class)
                .invoke(dynamicIds, dynamicUuid(i));
            dynamicData.getClass()
                .getMethod("remove", java.util.UUID.class)
                .invoke(dynamicData, dynamicUuid(i));
        }
        save(storage);
        dynamicBuckets = dynamicBucketCounts(dynamicData, dynamicIds);
        dynamicSnapshot = snapshot(dynamicData);
        dynamicIdentitySnapshot = snapshot(dynamicIds);
        dynamicRevision = (Long) invoke(dynamicData, "getRevision");
        dynamicIdentityRevision = (Long) invoke(dynamicIds, "getRevision");
        dynamicFileHash = dynamicHash("darkgrey_rpg_nominator");
        dynamicIdentityFileHash = dynamicHash("darkgrey_rpg_npc_identities");
        dynamicStarted = (Long) field(field(dynamicData, "capacity"), "lowSince");
        System.out.println(
            "DGR0401_DYNAMIC_WAIT count=100 capacity=" + capacityValue(
                dynamicData) + " buckets=" + Arrays.toString(dynamicBuckets) + " revision=" + dynamicRevision);
    }

    private void finishDynamicShrink() throws Exception {
        int[] after = dynamicBucketCounts(dynamicData, dynamicIds);
        if (after[0] >= dynamicBuckets[0] || after[1] >= dynamicBuckets[1] || after[2] >= dynamicBuckets[2]) {
            require(System.nanoTime() - dynamicStarted < 340_000_000_000L, "Real five-minute maintenance timed out");
            return;
        }
        require(System.nanoTime() - dynamicStarted >= 300_000_000_000L, "Native shrink occurred early");
        require(capacityValue(dynamicData) == 4096, "Native capacity did not shrink to floor");
        require(
            dynamicSnapshot.equals(snapshot(dynamicData)) && dynamicIdentitySnapshot.equals(snapshot(dynamicIds)),
            "Native compaction changed content");
        require(
            dynamicRevision == (Long) invoke(dynamicData, "getRevision")
                && dynamicIdentityRevision == (Long) invoke(dynamicIds, "getRevision"),
            "Native compaction changed revision");
        require(!dirty(dynamicData) && !dirty(dynamicIds), "Native compaction changed dirty");
        save(dynamicStorage);
        require(
            dynamicFileHash.equals(dynamicHash("darkgrey_rpg_nominator"))
                && dynamicIdentityFileHash.equals(dynamicHash("darkgrey_rpg_npc_identities")),
            "Native compaction changed files");
        editLast(dynamicData, dynamicIds, 100);
        save(dynamicStorage);
        dynamicFinished = true;
        System.out.println(
            "DGR0401_DYNAMIC_SHRINK count=100 capacity=4096 waited_ns=" + (System.nanoTime() - dynamicStarted)
                + " buckets_before="
                + java.util.Arrays.toString(dynamicBuckets)
                + " buckets_after="
                + java.util.Arrays.toString(after)
                + " nbt-revision-dirty-files-unchanged=PASS post-shrink-edit-save=PASS");
        System.out.println("DGR0401_REAL_FORGE_STORAGE=dynamic-shrink PASS");
    }

    private static void dynamicVerify(Object storage) throws Exception {
        Object data = get(Class.forName("darkgrey.rpg.nominator.NominatorSavedData"), storage);
        Object ids = get(Class.forName("darkgrey.rpg.identity.NpcIdentitySavedData"), storage);
        require(
            ((List<?>) invoke(data, "bindings")).size() == 100 && ((List<?>) invoke(ids, "bindings")).size() == 100,
            "Shrunk files did not restart");
        require(capacityValue(data) == 4096, "Shrunk restart capacity");
        editLast(data, ids, 100);
        save(storage);
        System.out.println(
            "DGR0401_DYNAMIC_VERIFY count=100 restored-and-edit-save=PASS revision=" + invoke(data, "getRevision"));
    }

    private static void editLast(Object data, Object ids, int id) throws Exception {
        java.util.UUID uuid = dynamicUuid(id);
        Object last = data.getClass()
            .getMethod("get", java.util.UUID.class)
            .invoke(data, uuid);
        long revision = (Long) invoke(data, "getRevision");
        data.getClass()
            .getMethod("put", last.getClass())
            .invoke(data, last);
        require(revision == (Long) invoke(data, "getRevision"), "Native no-op changed revision");
        Object host = ids.getClass()
            .getMethod("getHost", String.class)
            .invoke(ids, dynamicActor(id));
        require(host != null, "Native identity missing");
        data.getClass()
            .getMethod("remove", java.util.UUID.class)
            .invoke(data, uuid);
        ids.getClass()
            .getMethod("unbindHost", java.util.UUID.class)
            .invoke(ids, uuid);
        ids.getClass()
            .getMethod("bind", String.class, host.getClass())
            .invoke(ids, dynamicActor(id), host);
        data.getClass()
            .getMethod("put", last.getClass())
            .invoke(data, last);
    }

    private static java.util.UUID dynamicUuid(int id) {
        return new java.util.UUID(0x04010000L, id);
    }

    private static String dynamicActor(int id) {
        return "ST-AAAA-BBBB-CCCC-DDDD~actor~npc" + id;
    }

    private static Object invoke(Object data, String method) throws Exception {
        return data.getClass()
            .getMethod(method)
            .invoke(data);
    }

    private static Object field(Object owner, String name) throws Exception {
        Field field = owner.getClass()
            .getDeclaredField(name);
        field.setAccessible(true);
        return field.get(owner);
    }

    private static long capacityValue(Object data) throws Exception {
        Method method = data.getClass()
            .getDeclaredMethod("currentCapacity");
        method.setAccessible(true);
        return (Long) method.invoke(data);
    }

    private static int[] dynamicBucketCounts(Object data, Object ids) throws Exception {
        Object registry = field(ids, "registry");
        Field table = java.util.HashMap.class.getDeclaredField("table");
        table.setAccessible(true);
        Object[] maps = { field(data, "entities"), field(registry, "byNpcId"), field(registry, "byHostUuid") };
        int[] result = new int[3];
        for (int i = 0; i < 3; i++) {
            Object array = table.get(maps[i]);
            result[i] = array == null ? 0 : java.lang.reflect.Array.getLength(array);
        }
        return result;
    }

    private static Object snapshot(Object data) throws Exception {
        Class<?> nbt = Class.forName("net.minecraft.nbt.NBTTagCompound");
        Object root = nbt.newInstance();
        Method writer;
        try {
            writer = data.getClass()
                .getMethod("writeToNBT", nbt);
        } catch (NoSuchMethodException srg) {
            writer = data.getClass()
                .getMethod("func_76187_b", nbt);
        }
        writer.invoke(data, root);
        return root;
    }

    private static boolean dirty(Object data) throws Exception {
        for (Field field : data.getClass()
            .getSuperclass()
            .getDeclaredFields()) if (field.getType() == boolean.class) {
                field.setAccessible(true);
                return field.getBoolean(data);
            }
        throw new AssertionError("SavedData dirty field missing");
    }

    private static String dynamicHash(String name) throws Exception {
        File root = new File(System.getProperty("dgr0401.testRoot")).getCanonicalFile();
        File file = new File(root, "Dynamic0401/data/" + name + ".dat").getCanonicalFile();
        require(
            file.toPath()
                .startsWith(root.toPath()),
            "Unsafe dynamic test path");
        byte[] digest = MessageDigest.getInstance("SHA-256")
            .digest(Files.readAllBytes(file.toPath()));
        StringBuilder result = new StringBuilder();
        for (byte value : digest) result.append(String.format("%02x", value & 255));
        return result.toString();
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void capacity(Object storage) throws Exception {
        Class<?> type = Class.forName("darkgrey.rpg.nominator.NominatorSavedData");
        Object data = get(type, storage);
        List<?> bindings = (List<?>) type.getMethod("bindings")
            .invoke(data);
        int expected = Integer.parseInt(System.getProperty("dgr0401.expectedBindings", "513"));
        if (bindings.size() != expected) throw new AssertionError("Capacity table count " + bindings.size());
        long before = (Long) type.getMethod("getRevision")
            .invoke(data);
        Object last = bindings.get(bindings.size() - 1);
        type.getMethod("put", last.getClass())
            .invoke(data, last);
        if ((Long) type.getMethod("getRevision")
            .invoke(data) != before) throw new AssertionError("No-op changed revision");
        Object uuid = last.getClass()
            .getMethod("getEntityUuid")
            .invoke(last);
        type.getMethod("remove", java.util.UUID.class)
            .invoke(data, uuid);
        type.getMethod("put", last.getClass())
            .invoke(data, last);
        save(storage);
        System.out.println(
            "DGR0401_CAPACITY_SERVER count=" + expected
                + " revision_before="
                + before
                + " revision_after="
                + type.getMethod("getRevision")
                    .invoke(data)
                + " edit_save=PASS");
    }

    private static void failure(Object storage) throws Exception {
        String configured = System.getProperty("dgr0401.protectedFile");
        File file = new File(configured).getCanonicalFile();
        File testRoot = new File(System.getProperty("dgr0401.testRoot")).getCanonicalFile();
        if (!file.toPath()
            .startsWith(testRoot.toPath()) || !file.isFile()) throw new AssertionError("Unsafe fault target");
        byte[] before = MessageDigest.getInstance("SHA-256")
            .digest(Files.readAllBytes(file.toPath()));
        Class<?> type = Class.forName("darkgrey.rpg.session.persistence.CanonicalSessionSavedData");
        try {
            get(type, storage);
            throw new AssertionError("Failed file exposed as usable");
        } catch (InvocationTargetException rejected) {
            if (!rejected.getCause()
                .getClass()
                .getName()
                .endsWith("CanonicalSessionDataUnavailableException")) throw rejected;
        }
        Object failed = null;
        for (Method method : storage.getClass()
            .getMethods())
            if (java.util.Arrays.equals(method.getParameterTypes(), new Class<?>[] { Class.class, String.class }))
                failed = method.invoke(storage, type, "darkgrey_rpg_canonical_sessions");
        if (failed == null) throw new AssertionError("Real server did not cache failed reader");
        Class<?> savedData = failed.getClass()
            .getSuperclass();
        for (Field field : savedData.getDeclaredFields()) if (field.getType() == boolean.class) {
            field.setAccessible(true);
            field.setBoolean(failed, true);
        }
        save(storage);
        byte[] after = MessageDigest.getInstance("SHA-256")
            .digest(Files.readAllBytes(file.toPath()));
        if (!java.util.Arrays.equals(before, after)) throw new AssertionError("Server overwrote protected data");
        System.out.println("DGR0401_FAILURE_SERVER cached_failure=PASS forced_dirty_preserves_file=PASS");
    }

    private static Object get(Class<?> type, Object storage) throws Exception {
        return type.getMethod("get", storage.getClass())
            .invoke(null, storage);
    }

    private static Object worldMapStorage(Object owner) throws Exception {
        for (Class<?> type = owner.getClass(); type != null; type = type.getSuperclass())
            for (Field field : type.getDeclaredFields()) if (field.getName()
                .equals("mapStorage")
                || field.getName()
                    .equals("field_72988_C")) {
                        field.setAccessible(true);
                        return field.get(owner);
                    }
        throw new AssertionError("Production overworld mapStorage field missing; perWorldStorage is not equivalent");
    }

    private static void save(Object storage) throws Exception {
        for (Method method : storage.getClass()
            .getMethods())
            if (method.getDeclaringClass() == storage.getClass() && method.getReturnType() == void.class
                && method.getParameterTypes().length == 0) {
                    method.invoke(storage);
                    return;
                }
        throw new AssertionError("Public MapStorage saveAllData method missing");
    }
}
