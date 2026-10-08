package darkgrey.rpg.nominator;

import java.io.ByteArrayOutputStream;
import java.io.DataOutputStream;
import java.lang.reflect.Array;
import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.identity.BindingCapacity;
import darkgrey.rpg.identity.NpcHostIdentity;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.persistence.StorageProbeSupport;
import darkgrey.rpg.project.ProjectSnapshot;
import sun.misc.Unsafe;

/** Dynamic sizing, actual Java container compaction and real compressed Minecraft restart. */
public final class NominatorCapacity0401Probe {

    private static final ProjectSnapshot PROJECT = Nominator0400Probe.project();
    private static final long DELAY = BindingCapacity.SHRINK_DELAY_NANOS;

    private NominatorCapacity0401Probe() {}

    public static void main(String[] args) throws Exception {
        Path root = StorageProbeSupport.root("dynamic-capacity-");
        thresholdsAndDelay();
        belowFloorAndIndependentIndex();
        for (int count : new int[] { 0, 1, 511, 512, 513, 3071, 3072, 3455, 3456, 4095, 4096, 4097, 8192 })
            roundTrip(root, count);
        operations(root.resolve("operations"));
        compactionAndMaintenance(root.resolve("compaction"));
        maximumFields(root.resolve("max-fields"));
        System.out.println(
            "NOMINATOR_CAPACITY_0401=PASS default=4096 step=512 grow=75% shrink=50% delay=300s no-final-quota");
        System.out.println("CAPACITY_FIXTURES=" + root);
    }

    private static void thresholdsAndDelay() {
        AtomicLong clock = new AtomicLong();
        NominatorSavedData data = new NominatorSavedData(clock::get);
        require(data.currentCapacity() == 4096, "default capacity");
        for (int i = 1; i <= 3071; i++) data.put(binding(i));
        require(data.currentCapacity() == 4096, "3071 stays default");
        data.put(binding(3072));
        require(data.currentCapacity() == 4608, "3072 grows one 512 step");
        for (int i = 3073; i <= 3455; i++) data.put(binding(i));
        require(data.currentCapacity() == 4608, "3455 stays");
        data.put(binding(3456));
        require(data.currentCapacity() == 5120, "3456 grows next step");
        for (int i = 2049; i <= 3456; i++) data.remove(uuid(i));
        clock.set(DELAY - 1);
        require(!data.maintainMemory(), "no early shrink");
        data.put(binding(4000));
        data.put(binding(4001));
        clock.set(DELAY);
        require(data.maintainMemory() && data.currentCapacity() == 4608, "only eligible lower band");
        data.remove(uuid(4000));
        data.remove(uuid(4001));
        clock.set(DELAY * 2 - 1);
        require(!data.maintainMemory(), "new lower band waits again");
        data.put(binding(4000));
        data.remove(uuid(4000));
        clock.set(DELAY * 2);
        require(!data.maintainMemory(), "crossing threshold resets wait");
        clock.set(DELAY * 3 - 1);
        require(data.maintainMemory() && data.currentCapacity() == 4096, "full new wait shrinks to floor");
        for (int i = 2049; i <= 3072; i++) data.put(binding(i));
        require(data.currentCapacity() == 4608, "regrowth");
        require(BindingCapacity.capacityForCount(Integer.MAX_VALUE) > Integer.MAX_VALUE, "long arithmetic");
        System.out.println("CAPACITY_THRESHOLDS=PASS 3071/3072 3455/3456 wait-reset floor regrowth long-arithmetic");
    }

    private static void belowFloorAndIndependentIndex() throws Exception {
        AtomicLong clock = new AtomicLong();
        NominatorSavedData data = new NominatorSavedData(clock::get);
        NpcIdentitySavedData ids = new NpcIdentitySavedData(clock::get);
        for (int i = 1; i <= 400; i++) {
            data.put(binding(i));
            ids.bind(
                Nominator0400Probe.STORY + "~actor~index" + i,
                new NpcHostIdentity(uuid(i), "minecraft:zombie", 0));
        }
        for (int i = 11; i <= 400; i++) ids.unbindHost(uuid(i));
        int[] before = buckets(data, ids);
        clock.set(DELAY);
        require(!data.maintainMemory() && ids.maintainMemory(), "NPC index shrinks independently");
        int[] after = buckets(data, ids);
        require(before[0] == after[0] && after[1] < before[1] && after[2] < before[2], "only unused indexes rebuilt");
        for (int i = 11; i <= 400; i++) data.remove(uuid(i));
        NBTTagCompound snapshot = state(data);
        clock.addAndGet(DELAY);
        require(data.maintainMemory() && data.currentCapacity() == 4096, "physical compaction below logical floor");
        require(
            buckets(data, ids)[0] < before[0] && snapshot.equals(state(data)),
            "floor does not retain oversized table");
        System.out.println("CAPACITY_FLOOR_MEMORY=PASS independent-npc-index physical-compaction-below-4096");
    }

    private static void roundTrip(Path root, int count) throws Exception {
        Path directory = root.resolve("count-" + count);
        MapStorage storage = StorageProbeSupport.storage(directory);
        NominatorSavedData data = NominatorSavedData.get(storage);
        NpcIdentitySavedData ids = NpcIdentitySavedData.get(storage);
        for (int i = 1; i <= count; i++) require(bind(data, ids, i, null, groups(), false).isAccepted(), "public bind");
        data.markDirty();
        storage.saveAllData();
        NominatorSavedData restored = NominatorSavedData.get(StorageProbeSupport.storage(directory));
        require(
            restored.bindings()
                .size() == count && restored.getRevision() == count,
            "compressed restart " + count);
        require(restored.currentCapacity() == data.currentCapacity(), "capacity reconstructed");
        System.out.println(
            "CAPACITY_ROUNDTRIP count=" + count
                + " capacity="
                + restored.currentCapacity()
                + " sha256="
                + StorageProbeSupport.hash(directory.resolve(NominatorSavedData.DATA_NAME + ".dat")));
    }

    private static void operations(Path directory) throws Exception {
        MapStorage storage = StorageProbeSupport.storage(directory);
        NominatorSavedData data = NominatorSavedData.get(storage);
        NpcIdentitySavedData ids = NpcIdentitySavedData.get(storage);
        require(bind(data, ids, 1, Nominator0400Probe.HERO, groups(), false).isAccepted(), "hero with group");
        for (int i = 2; i <= 4096; i++) require(bind(data, ids, i, null, groups(), false).isAccepted(), "fill");
        storage.saveAllData();
        NBTTagCompound before = state(data), beforeIds = state(ids);
        long capacity = data.currentCapacity();
        require(
            !bind(data, ids, 4097, Nominator0400Probe.HERO, groups(), false).isAccepted(),
            "occupied host rejected");
        require(
            before.equals(state(data)) && beforeIds.equals(state(ids)) && capacity == data.currentCapacity(),
            "failed request preserves both owners/capacity");
        require(!data.isDirty() && !ids.isDirty(), "failed dirty unchanged");
        long revision = data.getRevision();
        require(bind(data, ids, 2, null, groups(), false).isAccepted(), "no-op accepted");
        data.put(binding(2));
        require(data.getRevision() == revision && !data.isDirty(), "no-op stable");
        require(
            bind(data, ids, 4097, Nominator0400Probe.HERO, groups(), true).isAccepted(),
            "residual-group transfer beyond 4096");
        require(
            data.bindings()
                .size() == 4097
                && data.get(uuid(1))
                    .getIndividualId() == null,
            "source groups retained");
        require(
            bind(data, ids, 2, Nominator0400Probe.HERO, Collections.emptyList(), true).isAccepted(),
            "existing target");
        require(bind(data, ids, 3, Nominator0400Probe.HERO, groups(), true).isAccepted(), "net decrease");
        require(
            data.bindings()
                .size() == 4096 && data.get(uuid(2)) == null,
            "net count");
        require(bind(data, ids, 3, null, Collections.emptyList(), false).isAccepted(), "unbind");
        storage.saveAllData();
        require(
            NominatorSavedData.get(StorageProbeSupport.storage(directory))
                .bindings()
                .size() == 4095,
            "operations restart");
        AtomicLong clock = new AtomicLong();
        NominatorSavedData boundary = new NominatorSavedData(clock::get);
        NpcIdentitySavedData boundaryIds = new NpcIdentitySavedData(clock::get);
        require(
            bind(boundary, boundaryIds, 1, Nominator0400Probe.HERO, Collections.emptyList(), false).isAccepted(),
            "boundary host");
        for (int i = 2; i <= 3071; i++) boundary.put(binding(i));
        require(
            bind(boundary, boundaryIds, 4000, Nominator0400Probe.HERO, groups(), true).isAccepted(),
            "net-zero transfer");
        require(
            boundary.currentCapacity() == 4096 && boundary.bindings()
                .size() == 3071,
            "final count only");
        System.out
            .println("CAPACITY_OPERATIONS=PASS conflict-no-mutation no-op residual-group net-zero net-reduce unbind");
    }

    private static void compactionAndMaintenance(Path directory) throws Exception {
        AtomicLong clock = new AtomicLong();
        NominatorSavedData data = new NominatorSavedData(clock::get);
        NpcIdentitySavedData ids = new NpcIdentitySavedData(clock::get);
        MapStorage storage = StorageProbeSupport.storage(directory);
        storage.setData(NominatorSavedData.DATA_NAME, data);
        storage.setData(NpcIdentitySavedData.DATA_NAME, ids);
        NominatorSavedData.get(storage);
        NpcIdentitySavedData.get(storage);
        for (int i = 1; i <= 6500; i++) {
            String actor = Nominator0400Probe.STORY + "~actor~npc" + i;
            ids.bind(actor, new NpcHostIdentity(uuid(i), "minecraft:zombie", 0));
            data.put(new NominatorEntityBinding(uuid(i), actor, groups(), Nominator0400Probe.STORY));
        }
        for (int i = 101; i <= 6500; i++) {
            ids.unbindHost(uuid(i));
            data.remove(uuid(i));
        }
        int[] beforeBuckets = buckets(data, ids);
        NBTTagCompound before = state(data), beforeIds = state(ids);
        long revision = data.getRevision(), idsRevision = ids.getRevision();
        List<UUID> order = order(data);
        storage.saveAllData();
        String hash = StorageProbeSupport.hash(directory.resolve(NominatorSavedData.DATA_NAME + ".dat"));
        NominatorCapacityMaintenance maintenance = new NominatorCapacityMaintenance(clock::get);
        clock.set(DELAY - 1);
        require(!maintenance.maintainLoaded(storage), "no early maintenance");
        clock.set(DELAY);
        require(!maintenance.maintainLoaded(storage), "five-second throttle");
        clock.set(DELAY - 1 + 5_000_000_000L);
        require(maintenance.maintainLoaded(storage), "stable five-minute delay");
        int[] afterBuckets = buckets(data, ids);
        for (int i = 0; i < 3; i++) require(afterBuckets[i] < beforeBuckets[i], "real bucket reduction " + i);
        require(data.currentCapacity() == 4096, "large decrease jumps to floor");
        require(
            before.equals(state(data)) && beforeIds.equals(state(ids)) && order.equals(order(data)),
            "same data/order");
        require(
            revision == data.getRevision() && idsRevision == ids.getRevision() && !data.isDirty() && !ids.isDirty(),
            "memory-only operation");
        storage.saveAllData();
        require(
            hash.equals(StorageProbeSupport.hash(directory.resolve(NominatorSavedData.DATA_NAME + ".dat"))),
            "same saved bytes");
        MapStorage untouched = StorageProbeSupport.storage(directory.resolve("untouched"));
        clock.addAndGet(5_000_000_000L);
        maintenance.maintainLoaded(untouched);
        try (java.util.stream.Stream<Path> files = Files.list(directory.resolve("untouched"))) {
            require(
                !files.findAny()
                    .isPresent(),
                "no load/create during maintenance");
        }
        NBTTagCompound broken = state(data);
        broken.setInteger("schema_version", -1);
        try {
            data.readFromNBT(broken);
            throw new AssertionError("bad reread accepted");
        } catch (IllegalArgumentException expected) {}
        clock.addAndGet(DELAY);
        require(!maintenance.maintainLoaded(storage), "failed reader skipped");
        try {
            data.maintainMemory();
            throw new AssertionError("failed reader compacted");
        } catch (NominatorDataUnavailableException expected) {}
        StorageProbeSupport.forceDirty(data);
        storage.saveAllData();
        require(
            hash.equals(StorageProbeSupport.hash(directory.resolve(NominatorSavedData.DATA_NAME + ".dat"))),
            "failed writer preserves file");
        NominatorCapacityMaintenance.clear();
        clock.addAndGet(5_000_000_000L);
        require(!maintenance.maintainLoaded(storage), "shutdown clears maintenance references");
        System.out.println(
            "CAPACITY_COMPACTION=PASS buckets_before=" + java.util.Arrays.toString(beforeBuckets)
                + " buckets_after="
                + java.util.Arrays.toString(afterBuckets)
                + " revisions-dirty-nbt-order-file-unchanged");
    }

    private static void maximumFields(Path directory) throws Exception {
        MapStorage storage = StorageProbeSupport.storage(directory);
        NominatorSavedData data = NominatorSavedData.get(storage);
        List<String> groups = new ArrayList<String>();
        String prefix = String.join("", Collections.nCopies(60, "g"));
        for (int i = 0; i < 32; i++)
            groups.add(Nominator0400Probe.STORY + "~actor~" + prefix + String.format("%03d", i));
        for (int i = 1; i <= 8192; i++)
            data.put(new NominatorEntityBinding(uuid(i), groups.get(0), groups, Nominator0400Probe.STORY));
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        CompressedStreamTools.write(state(data), new DataOutputStream(bytes));
        require(bytes.size() > 32 * 1024 * 1024, "crosses retired engineering reference");
        storage.saveAllData();
        require(
            NominatorSavedData.get(StorageProbeSupport.storage(directory))
                .bindings()
                .size() == 8192,
            "large file restarts");
        System.out.println(
            "CAPACITY_MAX_FIELDS count=8192 uncompressed_bytes=" + bytes.size()
                + " compressed_bytes="
                + Files.size(directory.resolve(NominatorSavedData.DATA_NAME + ".dat")));
    }

    private static int[] buckets(NominatorSavedData data, NpcIdentitySavedData ids) throws Exception {
        Object registry = field(ids, "registry");
        return new int[] { bucketCount(field(data, "entities")), bucketCount(field(registry, "byNpcId")),
            bucketCount(field(registry, "byHostUuid")) };
    }

    private static Object field(Object owner, String name) throws Exception {
        Field field = owner.getClass()
            .getDeclaredField(name);
        field.setAccessible(true);
        return field.get(owner);
    }

    private static int bucketCount(Object map) throws Exception {
        Field singleton = Unsafe.class.getDeclaredField("theUnsafe");
        singleton.setAccessible(true);
        Unsafe unsafe = (Unsafe) singleton.get(null);
        Object table = unsafe.getObject(map, unsafe.objectFieldOffset(HashMap.class.getDeclaredField("table")));
        return table == null ? 0 : Array.getLength(table);
    }

    private static List<UUID> order(NominatorSavedData data) {
        List<UUID> result = new ArrayList<UUID>();
        for (NominatorEntityBinding binding : data.bindings()) result.add(binding.getEntityUuid());
        return result;
    }

    private static NominatorResult bind(NominatorSavedData data, NpcIdentitySavedData ids, int entity,
        String individual, List<String> groups, boolean transfer) {
        return NominatorService.bindEntity(
            true,
            uuid(entity),
            "minecraft:zombie",
            0,
            individual,
            groups,
            Nominator0400Probe.STORY,
            transfer,
            PROJECT,
            ids,
            data);
    }

    private static NominatorEntityBinding binding(int id) {
        return new NominatorEntityBinding(uuid(id), null, groups(), Nominator0400Probe.STORY);
    }

    private static List<String> groups() {
        return Collections.singletonList(Nominator0400Probe.BANDITS);
    }

    private static UUID uuid(int id) {
        return new UUID(0, id);
    }

    private static NBTTagCompound state(WorldSavedData data) {
        NBTTagCompound root = new NBTTagCompound();
        data.writeToNBT(root);
        return root;
    }

    private static void require(boolean value, String message) {
        StorageProbeSupport.require(value, message);
    }
}
