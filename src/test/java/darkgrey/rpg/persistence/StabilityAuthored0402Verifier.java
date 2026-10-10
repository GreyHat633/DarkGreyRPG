package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardOpenOption;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.world.storage.MapStorage;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.media.CanonicalMediaServer;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageInventoryEntry;
import darkgrey.rpg.project.packages.StoryPackageRuntimeReloader;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;

/** Test-only world provisioning and observation; native clients drive the authored business chain. */
public final class StabilityAuthored0402Verifier {

    public static final String STORY = "ST-4EUB-29DN-V8J6-VTY3";
    private static final String PLACEMENT = "node_ca2c21edb75241728a39c266b2259ddf";
    private static final String GROUP = "ST-5UTG-DFCB-NQAQ-X4QF";
    private static final String GROUP_ACTOR = GROUP + "~actor~r6d7c51d48cb5e734a7b7daed7d233c36";
    private static final String KILL_PLACEMENT = "aggregate_44c20cf736634c92ae54f2837c933442";
    private static final String ZERO_PLACEMENT = "aggregate_84cf72614996424889944d783bd83fc1";
    private final Map<UUID, Expected> users = new HashMap<>();
    private Object server, world;
    private MapStorage storage;
    private Entity host;
    private Entity victim;
    private Entity transferTarget;
    private final java.util.Set<String> victimTypes = new java.util.LinkedHashSet<>();
    private Item apple;
    private int ticks, peak;
    private boolean initialized, finished;
    private CountDownLatch mediaRelease;

    @SubscribeEvent
    public void tick(TickEvent.ServerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || finished
            || !("NormalAuthored".equals(System.getProperty("dgr0402.scenario"))
                || "MixedAuthored".equals(System.getProperty("dgr0402.scenario"))))
            return;
        long callbackStarted = System.nanoTime();
        try {
            if (!initialized) {
                initialize();
                initialized = true;
            }
            ticks++;
            if (host != null && !(Boolean) Stability0402Reflect.read(host, "isDead", "field_70128_L"))
                Stability0402Reflect
                    .method(host.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
                    .invoke(host, groupMode() ? 8D : 3D, 4D, 0D);
            if (victim != null && !(Boolean) Stability0402Reflect.read(victim, "isDead", "field_70128_L"))
                Stability0402Reflect
                    .method(victim.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
                    .invoke(victim, 12D, 4D, 0D);
            if (transferTarget != null
                && !(Boolean) Stability0402Reflect.read(transferTarget, "isDead", "field_70128_L"))
                Stability0402Reflect
                    .method(
                        transferTarget.getClass(),
                        "setPosition",
                        "func_70107_b",
                        double.class,
                        double.class,
                        double.class)
                    .invoke(transferTarget, 16D, 4D, 0D);
            List<EntityPlayerMP> players = players();
            peak = Math.max(peak, players.size());
            for (EntityPlayerMP player : players) {
                UUID id = (UUID) Stability0402Reflect.call(player, "getUniqueID", "func_110124_au");
                Expected expected = users.get(id);
                if (expected == null) {
                    expected = new Expected(
                        (Integer) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb"));
                    expected.initialReceipts = receipts(player);
                    users.put(id, expected);
                    teleport(player, 0, 4, users.size() > 1 ? 2 : 0);
                }
                int identity = System.identityHashCode(player);
                if (expected.entity != identity) {
                    expected.entity = identity;
                    expected.connections++;
                }
                CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                    .snapshot(player, STORY, PLACEMENT);
                if (!groupMode() && task != null
                    && task.getStatus() == CanonicalTaskInstanceStatus.SETTLED
                    && task.getActivationTime() != expected.ignoredSettledRunForReplay) {
                    require(
                        receipts(player) == expected.initialReceipts + 2,
                        "Exactly one submission and one reward receipt");
                    require(inventory(player) == 0, "Native submit consumes exactly two bound apples");
                    require(
                        (Integer) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb")
                            == expected.initialXp + 7,
                        "Exactly one native reward");
                    if (expected.settledRun != 0)
                        require(task.getActivationTime() == expected.settledRun, "Reconnect preserves run");
                    expected.settledRun = task.getActivationTime();
                }
            }
            File command = new File(root(), "authored-command.json");
            if (command.isFile()) {
                JsonObject request = new JsonParser()
                    .parse(new String(Files.readAllBytes(command.toPath()), StandardCharsets.UTF_8))
                    .getAsJsonObject();
                Files.delete(command.toPath());
                command(request, players);
            }
            if (ticks % 10 == 0) {
                observe(players);
                write("heartbeat.txt", ticks + " " + System.nanoTime());
            }
            if (ticks % 1200 == 0) Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
        } catch (Exception failure) {
            finished = true;
            if (mediaRelease != null) mediaRelease.countDown();
            failure.printStackTrace();
            try {
                write("authored-FAIL.txt", failure.toString());
            } catch (Exception ignored) {
                failure.printStackTrace();
            }
            System.out.println("DGR0402_AUTHORED=FAIL");
            if (failure.getCause() instanceof Error) throw (Error) failure.getCause();
            throw new IllegalStateException("Authored native test failed", failure);
        } finally {
            Stability0402TickStats.stage(1, System.nanoTime() - callbackStarted);
        }
    }

    private void initialize() throws Exception {
        File caseRoot = root();
        require(
            caseRoot.getCanonicalPath()
                .contains("\\.tooling\\0402\\Worlds\\"),
            "Isolated root only");
        Class<?> serverClass = Class.forName("net.minecraft.server.MinecraftServer");
        server = Stability0402Reflect.method(serverClass, "getServer", "func_71276_C")
            .invoke(null);
        world = Stability0402Reflect.method(serverClass, "worldServerForDimension", "func_71218_a", int.class)
            .invoke(server, 0);
        storage = (MapStorage) Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
        Object rules = Stability0402Reflect.call(world, "getGameRules", "func_82736_K");
        Stability0402Reflect.method(rules.getClass(), "setOrCreateGameRule", "func_82764_b", String.class, String.class)
            .invoke(rules, "keepInventory", "true");
        require(
            DarkGreyRpg.getStoryPackageLoader()
                .getPackageForStory(STORY) != null,
            "Normal Studio export is installed");
        require(
            DarkGreyRpg.getProjectRepository()
                .getSnapshot()
                .getCanonicalStories()
                .size() == Integer.getInteger("dgr0402.authoredStoryCount", mixed() ? 7 : 4),
            "Both unchanged authored containers load");
        File ledger = new File(root(), ledgerName());
        if (ledger.isFile()) for (Map.Entry<String, com.google.gson.JsonElement> entry : new JsonParser()
            .parse(new String(Files.readAllBytes(ledger.toPath()), StandardCharsets.UTF_8))
            .getAsJsonObject()
            .entrySet()) {
                JsonObject value = entry.getValue()
                    .getAsJsonObject();
                Expected expected = new Expected(
                    value.get("initial_xp")
                        .getAsInt());
                expected.settledRun = value.get("settled_run")
                    .getAsLong();
                if (value.has("initial_receipts")) expected.initialReceipts = value.get("initial_receipts")
                    .getAsInt();
                users.put(UUID.fromString(entry.getKey()), expected);
            }
        Class<?> items = Class.forName("net.minecraft.init.Items");
        try {
            apple = (Item) items.getField("apple")
                .get(null);
        } catch (NoSuchFieldException formal) {
            apple = (Item) items.getField("field_151034_e")
                .get(null);
        }
        ItemStackDefinition definition = ItemStackDefinition.capture(new ItemStack(apple, 1, 0));
        ItemIdentitySavedData identities = ItemIdentitySavedData.get(storage);
        if (identities.getItem(STORY + "~item~sample") == null) identities.bindItem(STORY + "~item~sample", definition);
        identities.addGroupMember(STORY + "~item_group~supplies", new ItemGroupMember(ItemMatchMode.EXACT, definition));
        createHost();
        write(
            "authored-ready.json",
            "{\"status\":\"READY\",\"network_players\":0,\"generated_business_resources\":false,\"formal_cnpc\":true}");
    }

    private void createHost() throws Exception {
        Stability0402Reflect.method(world.getClass(), "getChunkFromBlockCoords", "func_72938_d", int.class, int.class)
            .invoke(world, 3, 0);
        host = (Entity) Class.forName("noppes.npcs.entity.EntityCustomNpc")
            .getConstructor(Class.forName("net.minecraft.world.World"))
            .newInstance(world);
        Stability0402Reflect
            .method(host.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
            .invoke(host, groupMode() ? 8D : 3D, 4D, 0D);
        require(
            (Boolean) Stability0402Reflect.method(world.getClass(), "spawnEntityInWorld", "func_72838_d", Entity.class)
                .invoke(world, host),
            "Physical CNPC spawned");
        UUID id = (UUID) Stability0402Reflect.call(host, "getUniqueID", "func_110124_au");
        NpcIdentitySavedData identities = NpcIdentitySavedData.get(storage);
        // Prepare the physical host through the same dual-table operation as the native nominator.
        // Directly transferring only NpcIdentitySavedData leaves a former GUI-bound selection behind.
        darkgrey.rpg.nominator.NominatorResult binding = darkgrey.rpg.nominator.NominatorService.bindEntity(
            true,
            id,
            "CustomNpc",
            0,
            STORY + "~actor~host",
            groupMode() ? java.util.Collections.singletonList(GROUP_ACTOR) : java.util.Collections.emptyList(),
            STORY,
            true,
            DarkGreyRpg.getProjectRepository()
                .getSnapshot(),
            identities,
            darkgrey.rpg.nominator.NominatorSavedData.get(storage));
        require(binding.isAccepted(), "Physical CNPC dual-table identity prepared: " + binding.getExplanation());
        require(
            EntityDgrIdentityResolver.resolveActorIds(host)
                .contains(STORY + "~actor~host"),
            "Physical CNPC resolves external identity");
        if (groupMode()) {
            require(
                EntityDgrIdentityResolver.resolveActorIds(host)
                    .contains(GROUP_ACTOR),
                "Physical CNPC group binding resolves");
        }
    }

    private void command(JsonObject request, List<EntityPlayerMP> players) throws Exception {
        String action = request.get("action")
            .getAsString();
        EntityPlayerMP target = null;
        if (request.has("user")) for (EntityPlayerMP player : players) if (name(player).equals(
            request.get("user")
                .getAsString()))
            target = player;
        if (action.equals("media-start") || action.equals("media-reset")) {
            require(target != null, "Owned connected media target required");
            String uid = request.get("story")
                .getAsString();
            darkgrey.rpg.identity.StoryUid.parse(uid);
            require(!uid.equals(STORY) && !uid.equals(GROUP), "Generated media fixture identity only");
            require(
                DarkGreyRpg.getStoryPackageLoader()
                    .getPackages()
                    .containsKey(uid),
                "Installed fixture package required");
            if (action.equals("media-start")) require(
                DarkGreyRpg.getCanonicalStoryManager()
                    .startByEntry(target, uid),
                "True generated Story Start");
            else require(
                DarkGreyRpg.getCanonicalStoryManager()
                    .reset(target, uid),
                "Explicit generated Story reset");
        } else if (action.equals("boundary-stop") || action.equals("frame-stop")) {
            if (action.equals("frame-stop")) require(
                root().getName()
                    .startsWith("Final-frames-") && peak >= 1,
                "Owned frame comparison with a native client required");
            else require(peak >= 2, "Two native connections observed");
            require(
                CanonicalMediaServer.getMediaInFlightCount() == 0 && queueMetrics()[0] == 0,
                "Media and main queue drained before normal stop");
            write(
                "boundary-result.json",
                "{\"status\":\"PASS\",\"network_peak\":" + peak
                    + ",\"assertions\":\"explicit generated-media boundary stop with zero in-flight and queue\"}");
            finished = true;
            Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
        } else if (action.equals("authored-new-run")) {
            require(!groupMode() && target != null, "Owned single-package replay target required");
            UUID id = (UUID) Stability0402Reflect.call(target, "getUniqueID", "func_110124_au");
            Expected expected = users.get(id);
            require(expected != null && expected.settledRun > 0, "Previously completed native run required");
            expected.initialXp = ((Number) Stability0402Reflect.read(target, "experienceTotal", "field_71067_cb"))
                .intValue();
            expected.initialReceipts = receipts(target);
            expected.ignoredSettledRunForReplay = expected.settledRun;
            expected.settledRun = 0;
            require(
                DarkGreyRpg.getCanonicalStoryManager()
                    .reset(target, STORY),
                "Explicit isolated authored replay reset");
        } else if (action.equals("op")) {
            require(target != null && name(target).startsWith("DGR402Client"), "Owned native profile only");
            Object configuration = Stability0402Reflect.call(server, "getConfigurationManager", "func_71203_ab");
            Object profile = Stability0402Reflect.call(target, "getGameProfile", "func_146103_bH");
            Stability0402Reflect
                .method(
                    configuration.getClass(),
                    "func_152605_a",
                    "func_152605_a",
                    com.mojang.authlib.GameProfile.class)
                .invoke(configuration, profile);
        } else if (action.equals("inventory")) {
            require(target != null, "Owned connected target required");
            int count = request.get("count")
                .getAsInt();
            require(count >= 0 && count <= 64, "Fixture inventory bounds");
            Object inv = Stability0402Reflect.read(target, "inventory", "field_71071_by");
            ItemStack[] slots = (ItemStack[]) Stability0402Reflect.read(inv, "mainInventory", "field_70462_a");
            java.util.Arrays.fill(slots, null);
            if (count > 0) slots[0] = new ItemStack(apple, count, 0);
            Object container = Stability0402Reflect.read(target, "inventoryContainer", "field_71069_bz");
            Stability0402Reflect.call(container, "detectAndSendChanges", "func_75142_b");
        } else if (action.equals("position")) {
            require(target != null, "Owned connected target required");
            double x = request.get("x")
                .getAsDouble(),
                z = request.has("z") ? request.get("z")
                    .getAsDouble() : 0;
            if (request.has("yaw")) teleportAt(
                target,
                x,
                4,
                z,
                request.get("yaw")
                    .getAsFloat(),
                request.has("pitch") ? request.get("pitch")
                    .getAsFloat() : 0F);
            else teleport(target, x, 4, z);
        } else if (action.equals("clear-host-area")) {
            int removed = 0;
            Class<?> npcType = Class.forName("noppes.npcs.entity.EntityCustomNpc");
            Class<?> villagerType = Class.forName("net.minecraft.entity.passive.EntityVillager");
            java.util.List<Entity> entities = new java.util.ArrayList<Entity>(
                (java.util.List<Entity>) Stability0402Reflect.read(world, "loadedEntityList", "field_72996_f"));
            for (Entity entity : entities) {
                if (entity == host || !(npcType.isInstance(entity) || villagerType.isInstance(entity))) continue;
                if (darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds(entity)
                    .contains(StabilityMixed0402Fixtures.ACTOR)) continue;
                double x = ((Number) Stability0402Reflect.read(entity, "posX", "field_70165_t")).doubleValue();
                double z = ((Number) Stability0402Reflect.read(entity, "posZ", "field_70161_v")).doubleValue();
                if (Math.abs(x - 3D) > 8D || Math.abs(z) > 8D) continue;
                Stability0402Reflect.method(world.getClass(), "removeEntityDangerously", "func_72973_f", Entity.class)
                    .invoke(world, entity);
                removed++;
            }
            write(
                "authored-host-area-cleanup.json",
                "{\"scope\":\"isolated fixture spawn area\",\"removed\":" + removed
                    + ",\"source_and_mixed_hosts_retained\":true}");
        } else if (action.equals("source-host-position")) {
            require(host != null, "Owned source host required");
            double x = request.get("x")
                .getAsDouble(),
                z = request.get("z")
                    .getAsDouble();
            require(x >= 120 && x <= 160 && z >= 10 && z <= 30, "Isolated host relocation bounds");
            Stability0402Reflect
                .method(world.getClass(), "getChunkFromBlockCoords", "func_72938_d", int.class, int.class)
                .invoke(world, (int) x, (int) z);
            Stability0402Reflect
                .method(host.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
                .invoke(host, x, 4D, z);
        } else if (action.equals("victim-reset")) {
            require(groupMode(), "Group fixture cleanup only");
            if (victim != null)
                Stability0402Reflect.method(world.getClass(), "removeEntityDangerously", "func_72973_f", Entity.class)
                    .invoke(world, victim);
            victim = null;
        } else if (action.equals("victim") || action.equals("cnpc-victim")) {
            require(groupMode() && target != null, "Owned group victim fixture only");
            require(
                victim == null || (Boolean) Stability0402Reflect.read(victim, "isDead", "field_70128_L"),
                "Previous victim was killed");
            victim = (Entity) Class
                .forName(
                    action.equals("cnpc-victim") ? "noppes.npcs.entity.EntityCustomNpc"
                        : "net.minecraft.entity.passive.EntityVillager")
                .getConstructor(Class.forName("net.minecraft.world.World"))
                .newInstance(world);
            victimTypes.add(
                victim.getClass()
                    .getName());
            Stability0402Reflect
                .method(world.getClass(), "getChunkFromBlockCoords", "func_72938_d", int.class, int.class)
                .invoke(world, 12, 0);
            Stability0402Reflect
                .method(victim.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
                .invoke(victim, 12D, 4D, 0D);
            Stability0402Reflect.method(victim.getClass(), "setHealth", "func_70606_j", float.class)
                .invoke(victim, 1F);
            require(
                (Boolean) Stability0402Reflect
                    .method(world.getClass(), "spawnEntityInWorld", "func_72838_d", Entity.class)
                    .invoke(world, victim),
                "Owned native victim spawned");
            UUID id = (UUID) Stability0402Reflect.call(victim, "getUniqueID", "func_110124_au");
            darkgrey.rpg.nominator.NominatorSavedData.get(storage)
                .put(
                    new darkgrey.rpg.nominator.NominatorEntityBinding(
                        id,
                        null,
                        java.util.Collections.singletonList(GROUP_ACTOR),
                        GROUP));
            Object connection = Stability0402Reflect.read(target, "playerNetServerHandler", "field_71135_a");
            Stability0402Reflect
                .method(
                    connection.getClass(),
                    "setPlayerLocation",
                    "func_147364_a",
                    double.class,
                    double.class,
                    double.class,
                    float.class,
                    float.class)
                .invoke(connection, 12D, 4D, 2D, 180F, 0F);
        } else if (action.equals("npc-unbound")) {
            require(target != null && transferTarget == null, "One owned unbound CNPC target only");
            transferTarget = (Entity) Class.forName("noppes.npcs.entity.EntityCustomNpc")
                .getConstructor(Class.forName("net.minecraft.world.World"))
                .newInstance(world);
            Stability0402Reflect
                .method(world.getClass(), "getChunkFromBlockCoords", "func_72938_d", int.class, int.class)
                .invoke(world, 16, 0);
            Stability0402Reflect
                .method(
                    transferTarget.getClass(),
                    "setPosition",
                    "func_70107_b",
                    double.class,
                    double.class,
                    double.class)
                .invoke(transferTarget, 16D, 4D, 0D);
            require(
                (Boolean) Stability0402Reflect
                    .method(world.getClass(), "spawnEntityInWorld", "func_72838_d", Entity.class)
                    .invoke(world, transferTarget),
                "Owned unbound CNPC spawned");
            require(
                EntityDgrIdentityResolver.resolveActorIds(transferTarget)
                    .isEmpty(),
                "Target starts with no DGR binding");
            teleportAt(target, 16D, 4D, 2D, 180F);
        } else if (action.equals("dimension")) {
            require(target != null, "Owned connected target required");
            int dim = request.get("dimension")
                .getAsInt();
            require(dim == 0 || dim == -1, "Existing vanilla dimensions only");
            // The native portal path sets this before travel; direct fixture travel must do the same.
            java.lang.reflect.Field cooldown;
            try {
                cooldown = target.getClass()
                    .getField("timeUntilPortal");
            } catch (NoSuchFieldException formal) {
                cooldown = target.getClass()
                    .getField("field_71088_bW");
            }
            cooldown.setInt(target, 300);
            Stability0402Reflect.method(target.getClass(), "travelToDimension", "func_71027_c", int.class)
                .invoke(target, dim);
        } else if (action.equals("npc-remove")) {
            Stability0402Reflect.call(host, "setDead", "func_70106_y");
        } else if (action.equals("death")) {
            require(target != null, "Owned connected target required");
            Class<?> damage = Class.forName("net.minecraft.util.DamageSource");
            Object source;
            try {
                source = damage.getField("outOfWorld")
                    .get(null);
            } catch (NoSuchFieldException formal) {
                source = damage.getField("field_76380_i")
                    .get(null);
            }
            Stability0402Reflect.method(target.getClass(), "attackEntityFrom", "func_70097_a", damage, float.class)
                .invoke(target, source, 10000F);
        } else if (action.equals("npc-recreate")) {
            createHost();
        } else if (action.equals("reload")) {
            require(
                StoryPackageRuntimeReloader
                    .reload(DarkGreyRpg.getProjectRepository(), DarkGreyRpg.getStoryPackageLoader())
                    .isSuccessful(),
                "Normal package reload succeeds");
        } else if (action.equals("disable") || action.equals("enable")) {
            for (StoryPackageInventoryEntry entry : DarkGreyRpg.getStoryPackageLoader()
                .getInventory())
                if (entry.getStoryUids()
                    .contains(STORY)) {
                        DarkGreyRpg.getStoryPackageLoader()
                            .setEnabled(
                                entry.getSourceName(),
                                action.equals("enable"),
                                DarkGreyRpg.getStoryPackageLoader()
                                    .getInventoryRevision());
                        StoryPackageRuntimeReloader
                            .reload(DarkGreyRpg.getProjectRepository(), DarkGreyRpg.getStoryPackageLoader());
                        break;
                    }
        } else if (action.equals("hold-media")) {
            require(mediaRelease == null, "Single controlled worker hold");
            java.lang.reflect.Field field = CanonicalMediaServer.class.getDeclaredField("MEDIA_WORKER");
            field.setAccessible(true);
            ThreadPoolExecutor worker = (ThreadPoolExecutor) field.get(null);
            mediaRelease = new CountDownLatch(1);
            final CountDownLatch release = mediaRelease, entered = new CountDownLatch(2);
            for (int i = 0; i < 2; i++) worker.execute(() -> {
                entered.countDown();
                try {
                    require(release.await(90, TimeUnit.SECONDS), "Controlled media hold deadline");
                } catch (InterruptedException failure) {
                    Thread.currentThread()
                        .interrupt();
                    throw new AssertionError(failure);
                }
            });
            require(entered.await(2, TimeUnit.SECONDS), "Both real media workers held");
        } else if (action.equals("release-media")) {
            require(mediaRelease != null, "Controlled media hold exists");
            mediaRelease.countDown();
            mediaRelease = null;
        } else if (action.equals("finish")) {
            require(peak >= 2 && users.size() >= 2, "Two genuine registered clients observed");
            if (groupMode()) {
                for (EntityPlayerMP player : players) {
                    UUID id = (UUID) Stability0402Reflect.call(player, "getUniqueID", "func_110124_au");
                    Expected expected = users.get(id);
                    CanonicalTaskInstanceSnapshot kill = DarkGreyRpg.getCanonicalTaskManager()
                        .snapshot(player, GROUP, KILL_PLACEMENT);
                    CanonicalTaskInstanceSnapshot zero = DarkGreyRpg.getCanonicalTaskManager()
                        .snapshot(player, GROUP, ZERO_PLACEMENT);
                    require(
                        kill != null && kill.getStatus() == CanonicalTaskInstanceStatus.SETTLED
                            && kill.getRuntimeSnapshot()
                                .getProgress()
                                .get("objective") == 2,
                        "Group native two-kill ledger");
                    require(
                        zero != null && zero.getStatus() == CanonicalTaskInstanceStatus.ACTIVE
                            && zero.getRuntimeSnapshot()
                                .getProgress()
                                .get("objective") == 0,
                        "Zero-settlement remains ACTIVE");
                    require(
                        receipts(player) == expected.initialReceipts,
                        "Settlement ports without reward nodes add no receipt");
                    require(
                        (Integer) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb")
                            == expected.initialXp,
                        "Empty group rewards do not alter XP");
                }
            } else for (Expected expected : users.values())
                require(expected.settledRun > 0, "Both native inventory/reward chains completed");
            require(
                CanonicalMediaServer.getMediaInFlightCount() == 0 && queueMetrics()[0] == 0,
                "Owned queues/resources drained");
            Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
            observe(players);
            write(
                groupMode() ? "authored-group-result.json" : "authored-result.json",
                "{\"status\":\"PASS\",\"network_peak\":" + peak
                    + ",\"distinct_profiles\":"
                    + users.size()
                    + ",\"assertions\":\""
                    + (groupMode()
                        ? "unchanged Studio group: two native kills of bound fixture entities (exact classes in authored-state), CNPC interaction, first ordered settlement, no extra receipts, zero-settlement ACTIVE"
                        : "each player completes the unchanged Studio single-package native chain, consumes two apples and grants seven XP once; group container loaded separately")
                    + "\"}");
            if (mediaRelease != null) mediaRelease.countDown();
            finished = true;
            System.out.println("DGR0402_AUTHORED=PASS");
            if (!mixed()) Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
        } else throw new IllegalArgumentException("Unknown isolated action " + action);
        request.addProperty("server_tick", ticks);
        request.addProperty("status", "APPLIED");
        Files.write(
            new File(root(), "authored-commands.jsonl").toPath(),
            (request + "\n").getBytes(StandardCharsets.UTF_8),
            StandardOpenOption.CREATE,
            StandardOpenOption.APPEND);
    }

    private void observe(List<EntityPlayerMP> players) throws Exception {
        JsonObject state = new JsonObject();
        state.addProperty("tick", ticks);
        state.addProperty("network", players.size());
        state.addProperty("peak_network", peak);
        state.addProperty("media_in_flight", CanonicalMediaServer.getMediaInFlightCount());
        state.addProperty("queue_pending", queueMetrics()[0]);
        if (host != null) state.add("source_host", identityState(host));
        if (transferTarget != null) state.add("transfer_target", identityState(transferTarget));
        JsonArray exactVictimTypes = new JsonArray();
        for (String type : victimTypes) exactVictimTypes.add(new com.google.gson.JsonPrimitive(type));
        state.add("spawned_victim_types", exactVictimTypes);
        JsonArray array = new JsonArray();
        for (EntityPlayerMP player : players) {
            UUID id = (UUID) Stability0402Reflect.call(player, "getUniqueID", "func_110124_au");
            JsonObject user = new JsonObject();
            user.addProperty("name", name(player));
            user.addProperty("uuid", id.toString());
            user.addProperty("inventory", inventory(player));
            user.addProperty("receipts", receipts(player));
            user.addProperty("xp", (Integer) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb"));
            user.addProperty("dimension", (Integer) Stability0402Reflect.read(player, "dimension", "field_71093_bK"));
            CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                .snapshot(player, STORY, PLACEMENT);
            if (task != null) {
                user.addProperty(
                    "task_status",
                    task.getStatus()
                        .name());
                user.addProperty("task_run", task.getActivationTime());
                user.add(
                    "progress",
                    new com.google.gson.Gson().toJsonTree(
                        task.getRuntimeSnapshot()
                            .getProgress()));
            }
            CanonicalStoryInstanceSnapshot story = DarkGreyRpg.getCanonicalStoryManager()
                .snapshot(player, STORY);
            if (story != null) {
                user.addProperty("story_run", story.getActivationTime());
                user.addProperty(
                    "story_status",
                    story.getRuntimeSnapshot()
                        .getStatus()
                        .name());
            }
            darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot session = CanonicalSessionSavedData
                .get(storage)
                .getSnapshot(id, STORY);
            if (session != null) {
                user.addProperty("transport", session.getTransportId());
                user.addProperty(
                    "session_status",
                    session.getRuntimeSnapshot()
                        .getStatus()
                        .name());
                user.addProperty(
                    "session_node",
                    session.getRuntimeSnapshot()
                        .getCurrentNodeId());
            }
            user.addProperty("entity_changes", users.get(id).connections);
            JsonArray additionalStories = new JsonArray();
            // The frozen 0.4.0.1 frame baseline has no diagnostic snapshot-list API.
            // Its ordinary story/session fields above remain observed; do not claim
            // additional-story or queue coverage from this compatibility path.
            for (CanonicalStoryInstanceSnapshot extra : Boolean.getBoolean("dgr0402.baseline")
                ? java.util.Collections.<CanonicalStoryInstanceSnapshot>emptyList()
                : CanonicalSessionSavedData.get(storage)
                    .storySnapshots(id)) {
                if (extra.getStoryId()
                    .equals(STORY)) continue;
                JsonObject row = new JsonObject();
                row.addProperty("story", extra.getStoryId());
                row.addProperty("run", extra.getActivationTime());
                row.addProperty(
                    "status",
                    extra.getStatus()
                        .name());
                additionalStories.add(row);
            }
            user.add("additional_stories", additionalStories);
            if (groupMode()) {
                for (String placement : new String[] { KILL_PLACEMENT, ZERO_PLACEMENT }) {
                    CanonicalTaskInstanceSnapshot groupTask = DarkGreyRpg.getCanonicalTaskManager()
                        .snapshot(player, GROUP, placement);
                    if (groupTask != null) {
                        JsonObject item = new JsonObject();
                        item.addProperty(
                            "status",
                            groupTask.getStatus()
                                .name());
                        item.addProperty("run", groupTask.getActivationTime());
                        item.add(
                            "progress",
                            new com.google.gson.Gson().toJsonTree(
                                groupTask.getRuntimeSnapshot()
                                    .getProgress()));
                        user.add(placement.equals(KILL_PLACEMENT) ? "group_kill" : "group_zero", item);
                    }
                }
                CanonicalStoryInstanceSnapshot groupStory = DarkGreyRpg.getCanonicalStoryManager()
                    .snapshot(player, GROUP);
                if (groupStory != null) user.addProperty(
                    "group_story_status",
                    groupStory.getRuntimeSnapshot()
                        .getStatus()
                        .name());
                darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot groupSession = CanonicalSessionSavedData
                    .get(storage)
                    .getSnapshot(id, GROUP);
                if (groupSession != null) {
                    user.addProperty(
                        "group_session_node",
                        groupSession.getRuntimeSnapshot()
                            .getCurrentNodeId());
                    user.addProperty("group_session_transport", groupSession.getTransportId());
                }
            }
            array.add(user);
        }
        state.add("players", array);
        JsonArray leases = new JsonArray();
        for (LoadedStoryPackage pack : DarkGreyRpg.getStoryPackageLoader()
            .getPackages()
            .values()) {
            Object generation = Stability0402Reflect.read(pack, "generation", "generation");
            if (generation != null) {
                JsonObject lease = new JsonObject();
                lease.addProperty("story", pack.getStoryId());
                lease.addProperty(
                    "leases",
                    (Integer) Stability0402Reflect.call(generation, "getLeaseCount", "getLeaseCount"));
                leases.add(lease);
            }
        }
        state.add("source_leases", leases);
        write("authored-state.json", state.toString());
        JsonObject ledger = new JsonObject();
        for (Map.Entry<UUID, Expected> entry : users.entrySet()) {
            JsonObject expected = new JsonObject();
            expected.addProperty("initial_xp", entry.getValue().initialXp);
            expected.addProperty("initial_receipts", entry.getValue().initialReceipts);
            expected.addProperty("settled_run", entry.getValue().settledRun);
            ledger.add(
                entry.getKey()
                    .toString(),
                expected);
        }
        write(ledgerName(), ledger.toString());
    }

    private JsonObject identityState(Entity entity) throws Exception {
        JsonObject result = new JsonObject();
        result.addProperty(
            "entity_id",
            ((Number) Stability0402Reflect.call(entity, "getEntityId", "func_145782_y")).intValue());
        result.addProperty("x", ((Number) Stability0402Reflect.read(entity, "posX", "field_70165_t")).doubleValue());
        result.addProperty("z", ((Number) Stability0402Reflect.read(entity, "posZ", "field_70161_v")).doubleValue());
        result.addProperty(
            "uuid",
            ((UUID) Stability0402Reflect.call(entity, "getUniqueID", "func_110124_au")).toString());
        result.addProperty(
            "class",
            entity.getClass()
                .getName());
        JsonArray actors = new JsonArray();
        for (String actor : EntityDgrIdentityResolver.resolveActorIds(entity))
            actors.add(new com.google.gson.JsonPrimitive(actor));
        result.add("resolved_actors", actors);
        return result;
    }

    private List<EntityPlayerMP> players() throws Exception {
        Object manager = Stability0402Reflect.call(server, "getConfigurationManager", "func_71203_ab");
        List<?> registered = (List<?>) Stability0402Reflect.read(manager, "playerEntityList", "field_72404_b");
        List<EntityPlayerMP> result = new ArrayList<>();
        for (Object player : new ArrayList<>(registered))
            if (player instanceof EntityPlayerMP && name((EntityPlayerMP) player).startsWith("DGR402Client"))
                result.add((EntityPlayerMP) player);
        return result;
    }

    private static String name(EntityPlayerMP player) throws Exception {
        Object profile = Stability0402Reflect.call(player, "getGameProfile", "func_146103_bH");
        return (String) profile.getClass()
            .getMethod("getName")
            .invoke(profile);
    }

    private int inventory(EntityPlayerMP player) throws Exception {
        Object inv = Stability0402Reflect.read(player, "inventory", "field_71071_by");
        int count = 0;
        for (ItemStack item : (ItemStack[]) Stability0402Reflect.read(inv, "mainInventory", "field_70462_a"))
            if (item != null && ItemIdentitySavedData.get(storage)
                .matchesItem(STORY + "~item~sample", item))
                count += (Integer) Stability0402Reflect.read(item, "stackSize", "field_77994_a");
        return count;
    }

    private static void teleport(EntityPlayerMP player, double x, double y, double z) throws Exception {
        teleportAt(player, x, y, z, (float) Math.toDegrees(Math.atan2(x - (groupMode() ? 8 : 3), -z)));
    }

    private static void teleportAt(EntityPlayerMP player, double x, double y, double z, float yaw) throws Exception {
        teleportAt(player, x, y, z, yaw, 0F);
    }

    private static void teleportAt(EntityPlayerMP player, double x, double y, double z, float yaw, float pitch)
        throws Exception {
        Object connection = Stability0402Reflect.read(player, "playerNetServerHandler", "field_71135_a");
        Stability0402Reflect
            .method(
                connection.getClass(),
                "setPlayerLocation",
                "func_147364_a",
                double.class,
                double.class,
                double.class,
                float.class,
                float.class)
            .invoke(connection, x, y, z, yaw, pitch);
    }

    private static File root() {
        return new File(System.getProperty("dgr0402.root"));
    }

    private static int receipts(EntityPlayerMP player) throws Exception {
        Object data = Stability0402Reflect.call(player, "getEntityData", "getEntityData");
        Object persisted = Stability0402Reflect.method(data.getClass(), "getCompoundTag", "func_74775_l", String.class)
            .invoke(data, "PlayerPersisted");
        Object receipts = Stability0402Reflect
            .method(persisted.getClass(), "getCompoundTag", "func_74775_l", String.class)
            .invoke(persisted, "DGRTaskReceipts");
        return ((java.util.Set<?>) Stability0402Reflect.call(receipts, "func_150296_c", "func_150296_c")).size();
    }

    private static boolean mixed() {
        return "MixedAuthored".equals(System.getProperty("dgr0402.scenario"));
    }

    private static boolean groupMode() {
        return Boolean.getBoolean("dgr0402.authoredGroup");
    }

    private static String ledgerName() {
        return groupMode() ? "authored-group-independent-ledger.json" : "authored-independent-ledger.json";
    }

    private static void write(String file, String text) throws Exception {
        Stability0402TestFiles.write(root(), file, text);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new IllegalStateException(message);
    }

    private static long[] queueMetrics() {
        return Boolean.getBoolean("dgr0402.baseline") ? new long[10] : MainThreadScheduler.serverMetrics();
    }

    private static final class Expected {

        int initialXp;
        long ignoredSettledRunForReplay;
        long settledRun;
        int initialReceipts;
        int entity, connections;

        Expected(int xp) {
            initialXp = xp;
        }
    }
}
