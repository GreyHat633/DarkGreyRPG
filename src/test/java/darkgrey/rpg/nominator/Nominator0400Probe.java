package darkgrey.rpg.nominator;

import java.io.OutputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Proxy;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.monster.EntitySkeleton;
import net.minecraft.entity.monster.EntityZombie;
import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.ISaveHandler;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.identity.NpcHostIdentity;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import sun.misc.Unsafe;

/** Exercises real Minecraft MapStorage, including its cached failed-reader behavior. */
public final class Nominator0400Probe {

    static final String STORY = "ST-2345-6789-ABCD-EFGH";
    static final String HERO = STORY + "~actor~hero";
    static final String BANDITS = STORY + "~actor~bandits";
    static final UUID A = UUID.fromString("00000000-0000-0000-0000-000000000401");
    static final UUID B = UUID.fromString("00000000-0000-0000-0000-000000000402");
    static final UUID C = UUID.fromString("00000000-0000-0000-0000-000000000403");

    private Nominator0400Probe() {}

    public static void main(String[] args) throws Exception {
        Path root = Files.createTempDirectory(Paths.get(".tooling"), "0400-nominator-world-");
        Path current = Files.createDirectories(root.resolve("current"));
        MapStorage storage = storage(current);
        NominatorSavedData fresh = NominatorSavedData.get(storage);
        fresh.put(new NominatorEntityBinding(A, HERO, Arrays.asList(BANDITS, BANDITS), STORY));
        storage.saveAllData();
        NominatorSavedData restarted = NominatorSavedData.get(storage(current));
        require(
            restarted.getRevision() == 1 && HERO.equals(
                restarted.get(A)
                    .getIndividualId())
                && restarted.get(A)
                    .getGroupIds()
                    .equals(Collections.singletonList(BANDITS)),
            "real save/restart");
        restarted.put(new NominatorEntityBinding(C, null, Collections.singletonList(BANDITS), STORY));
        NBTTagCompound currentNbt = new NBTTagCompound();
        restarted.writeToNBT(currentNbt);
        require(currentNbt.getInteger("schema_version") == 4 && !currentNbt.hasKey("type_groups"), "current format");
        Map<String, NBTTagCompound> invalid = new LinkedHashMap<String, NBTTagCompound>();
        NBTTagCompound legacy = copy(currentNbt);
        legacy.setInteger("schema_version", 3);
        legacy.setTag("type_groups", new NBTTagList());
        invalid.put("schema3", legacy);
        NBTTagCompound emptyTypes = copy(currentNbt);
        emptyTypes.setTag("type_groups", new NBTTagList());
        invalid.put("empty-retired-field", emptyTypes);
        NBTTagCompound types = copy(currentNbt);
        NBTTagList rules = new NBTTagList();
        NBTTagCompound rule = new NBTTagCompound();
        rule.setString("entity_type", "minecraft:zombie");
        rules.appendTag(rule);
        types.setTag("type_groups", rules);
        invalid.put("type-rule", types);
        NBTTagCompound unknown = copy(currentNbt);
        unknown.setBoolean("unknown", true);
        invalid.put("unknown-field", unknown);
        NBTTagCompound malformed = copy(currentNbt);
        malformed.getTagList("entities", 10)
            .getCompoundTagAt(0)
            .setString("entity_uuid", "broken");
        invalid.put("invalid-uuid", malformed);
        NBTTagCompound negative = copy(currentNbt);
        negative.setLong("revision", -1);
        invalid.put("negative-revision", negative);
        for (Map.Entry<String, NBTTagCompound> sample : invalid.entrySet()) {
            Path directory = Files.createDirectories(root.resolve(sample.getKey()));
            write(file(directory), sample.getValue());
            protect(directory);
        }
        Path corrupt = Files.createDirectories(root.resolve("corrupt-compressed"));
        Files.write(file(corrupt), new byte[] { 1, 2, 3, 4 });
        protect(corrupt);

        NominatorSavedData pending = new NominatorSavedData("pending");
        rejected(pending::markDirty);
        rejected(() -> pending.writeToNBT(new NBTTagCompound()));
        NominatorSavedData failed = new NominatorSavedData();
        failed.readFromNBT(currentNbt);
        try {
            failed.readFromNBT(legacy);
            throw new AssertionError("legacy root accepted");
        } catch (IllegalArgumentException expected) {}
        NpcIdentitySavedData npc = new NpcIdentitySavedData();
        npc.bind(HERO, new NpcHostIdentity(A, "minecraft:zombie", 0));
        long npcRevision = npc.getRevision();
        require(
            !NominatorService
                .bindEntity(
                    true,
                    B,
                    "minecraft:zombie",
                    0,
                    HERO,
                    Collections.singletonList(BANDITS),
                    STORY,
                    true,
                    project(),
                    npc,
                    failed)
                .isAccepted(),
            "quarantine blocked transfer");
        rejected(() -> NominatorService.releaseEntityResource(true, HERO, project(), npc, failed));
        require(
            npc.getRevision() == npcRevision && npc.getHost(HERO)
                .getEntityUuid()
                .equals(A),
            "quarantine did not mutate separate NPC registry");
        failed.readFromNBT(currentNbt);
        require(
            failed.getRevision() == restarted.getRevision() && HERO.equals(
                failed.get(A)
                    .getIndividualId()),
            "transactional failed read retained memory");

        Entity zombieA = entity(EntityZombie.class, A);
        Entity zombieB = entity(EntityZombie.class, B);
        Entity skeletonC = entity(EntitySkeleton.class, C);
        require(
            EntityDgrIdentityResolver.resolve(zombieA, npc, restarted)
                .getActorIds()
                .equals(Arrays.asList(HERO, BANDITS)),
            "A nominated zombie has identities");
        require(
            !EntityDgrIdentityResolver.resolve(zombieB, npc, restarted)
                .isResolved(),
            "B unbound same-type zombie does not inherit identities");
        require(
            EntityDgrIdentityResolver.resolve(skeletonC, npc, restarted)
                .getActorIds()
                .equals(Collections.singletonList(BANDITS)),
            "C nominated skeleton shares concrete group");
        System.out.println("NOMINATOR_0400_MAPSTORAGE_RESTART=PASS");
        System.out.println("NOMINATOR_0400_FAILED_READ_NO_REWRITE=PASS vectors=" + (invalid.size() + 1));
        System.out.println("NOMINATOR_0400_QUARANTINE_BEFORE_NPC_MUTATION=PASS");
        System.out.println("NOMINATOR_0400_CONCRETE_ZOMBIE_SKELETON_GROUPS=PASS");
        System.out.println("NOMINATOR_0400_ISOLATED_FIXTURES=" + root.toAbsolutePath());
    }

    private static void protect(Path directory) throws Exception {
        byte[] original = Files.readAllBytes(file(directory));
        MapStorage storage = storage(directory);
        rejected(() -> NominatorSavedData.get(storage));
        rejected(() -> NominatorSavedData.get(storage));
        NominatorSavedData cached = (NominatorSavedData) storage
            .loadData(NominatorSavedData.class, NominatorSavedData.DATA_NAME);
        require(cached != null, "MapStorage cached failed instance");
        rejected(() -> cached.get(A));
        rejected(cached::bindings);
        rejected(cached::getRevision);
        rejected(() -> cached.put(new NominatorEntityBinding(B, null, Collections.singletonList(BANDITS), STORY)));
        rejected(() -> cached.remove(A));
        rejected(cached::markDirty);
        NBTTagCompound output = new NBTTagCompound();
        output.setString("sentinel", "preserved");
        NBTTagCompound before = copy(output);
        rejected(() -> cached.writeToNBT(output));
        require(before.equals(output), "writer checked quarantine before modifying output");
        // Force Minecraft's private dirty flag: serialization must still precede opening the original file.
        Field dirty = WorldSavedData.class.getDeclaredField("dirty");
        dirty.setAccessible(true);
        dirty.setBoolean(cached, true);
        storage.saveAllData();
        require(Arrays.equals(original, Files.readAllBytes(file(directory))), "failed data file rewritten");
        require(
            storage.loadData(NominatorSavedData.class, NominatorSavedData.DATA_NAME) == cached,
            "failure replaced with empty success");
    }

    static ProjectSnapshot project() {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            HERO,
            new ActorDefinition(2, ActorDefinition.TYPE_INDIVIDUAL, HERO, "Hero", "", Collections.emptyList(), STORY));
        actors.put(
            BANDITS,
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_COLLECTIVE,
                BANDITS,
                "Bandits",
                "",
                Collections.emptyList(),
                STORY));
        CanonicalGraphResource story = new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.STORY,
            STORY,
            "Story",
            new CanonicalGraph(Collections.emptyList(), Collections.emptyList()));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                Collections.singletonMap(STORY, story),
                Collections.emptyMap(),
                Collections.emptyMap(),
                Collections.emptyMap()));
    }

    private static Path file(Path directory) {
        return directory.resolve(NominatorSavedData.DATA_NAME + ".dat");
    }

    private static MapStorage storage(Path directory) {
        ISaveHandler save = (ISaveHandler) Proxy.newProxyInstance(
            Nominator0400Probe.class.getClassLoader(),
            new Class<?>[] { ISaveHandler.class },
            (proxy, method, args) -> {
                if ("getMapFileFromName".equals(method.getName())) return directory.resolve(args[0] + ".dat")
                    .toFile();
                if ("getWorldDirectory".equals(method.getName())) return directory.toFile();
                return null;
            });
        return new MapStorage(save);
    }

    private static void write(Path file, NBTTagCompound data) throws Exception {
        NBTTagCompound root = new NBTTagCompound();
        root.setTag("data", data);
        try (OutputStream output = Files.newOutputStream(file)) {
            CompressedStreamTools.writeCompressed(root, output);
        }
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return (NBTTagCompound) value.copy();
    }

    static <T> T allocate(Class<T> type) throws Exception {
        Field field = Unsafe.class.getDeclaredField("theUnsafe");
        field.setAccessible(true);
        return type.cast(((Unsafe) field.get(null)).allocateInstance(type));
    }

    static Entity entity(Class<? extends Entity> type, UUID uuid) throws Exception {
        Entity value = allocate(type);
        Field field = Entity.class.getDeclaredField("entityUniqueID");
        field.setAccessible(true);
        field.set(value, uuid);
        return value;
    }

    private static void rejected(Runnable operation) {
        try {
            operation.run();
        } catch (IllegalStateException expected) {
            return;
        }
        throw new AssertionError("quarantined data was usable");
    }

    static void require(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }
}
