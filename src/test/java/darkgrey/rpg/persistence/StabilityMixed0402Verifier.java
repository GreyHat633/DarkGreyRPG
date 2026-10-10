package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.identity.NpcHostIdentity;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.task.forge.CanonicalTaskInventory;
import darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** S layer: actual Forge storage, inventory journal and physical vanilla host; not native UI/CNPC/network evidence. */
public final class StabilityMixed0402Verifier {

    private Object server, world, save;
    private MapStorage storage;
    private Entity host;
    private ItemStack prototype;
    private final List<EntityPlayerMP> players = new ArrayList<>();
    private final List<Long> samples = new ArrayList<>();
    private final StringBuilder operations = new StringBuilder("tick,stage,user,nanos\n");
    private final StringBuilder resources = new StringBuilder(
        "tick,heap_used_bytes,gc_count,gc_ms,queue_pending,queue_peak,queue_oldest_ns\n");
    private final long[][] runs = new long[4][3];
    private final int[][] progress = new int[4][3];
    private final int[] inventory = new int[4], xp = new int[4], receipts = new int[4];
    private final boolean[] cancelled = new boolean[4], rewarded = new boolean[4];
    private boolean initialized, finished;
    private final long constructedAt = System.currentTimeMillis();
    private int ticks, queueOffered, queueExecuted, queueCancelled;
    private long burstAt;
    private long firstCompletedTickIndex;
    private Throwable queuedBusinessFailure;

    @SubscribeEvent
    public void tick(TickEvent.ServerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || finished
            || !("MixedLifecycle".equals(System.getProperty("dgr0402.scenario"))
                || "MixedAuthored".equals(System.getProperty("dgr0402.scenario"))))
            return;
        long callbackStarted = System.nanoTime();
        try {
            if (queuedBusinessFailure != null)
                throw new IllegalStateException("Queued business request failed", queuedBusinessFailure);
            if (!initialized) {
                long before = System.nanoTime();
                initialize();
                Stability0402TickStats.stage(5, System.nanoTime() - before);
                operations.append("0,initialize,-1,")
                    .append(System.nanoTime() - before)
                    .append('\n');
                write("mixed-operations.csv", operations.toString());
                initialized = true;
                return;
            }
            ticks++;
            if (Stability0402TickStats.completed > 0) {
                if (samples.isEmpty()) firstCompletedTickIndex = Stability0402TickStats.completed;
                samples.add(Stability0402TickStats.lastNanos);
            }
            int warmupTicks = Integer.getInteger("dgr0402.warmupTicks", 0);
            if (warmupTicks > 0 && ticks == warmupTicks) {
                // Keep cold/preheat spikes and all business assertions. Only the
                // measurement window changes; warm observations remain reviewable.
                writeObservations();
                for (String name : new String[] { "complete-tick-samples.csv", "driver-tick-profile.csv",
                    "driver-tick-profile-inputs.json" })
                    if (name.equals("complete-tick-samples.csv") || Boolean.getBoolean("dgr0402.profileDrivers"))
                        Files.move(
                            new File(root(), name).toPath(),
                            new File(root(), "warmup-" + name).toPath(),
                            java.nio.file.StandardCopyOption.REPLACE_EXISTING);
                samples.clear();
            }
            if (ticks % 200 == 0) {
                long before = System.nanoTime();
                cycle();
                Stability0402TickStats.stage(4, System.nanoTime() - before);
                operations.append(ticks)
                    .append(",new_runs,-1,")
                    .append(System.nanoTime() - before)
                    .append('\n');
            }
            for (int u = 0; u < players.size(); u++) {
                long before = System.nanoTime();
                EntityPlayerMP player = players.get(u);
                int phase = ticks % 200;
                position(player, 0, 5, 0);
                position(host, 1, 4, 0);
                if (phase == 20 || phase == 40 || phase == 60) {
                    DarkGreyRpg.getCanonicalTaskManager()
                        .dispatch(player, CanonicalTaskEvent.killEntity(StabilityMixed0402Fixtures.ACTOR));
                    progress[u][0]++;
                }
                if (phase == 70) {
                    DarkGreyRpg.getCanonicalTaskManager()
                        .synchronizeObjectives(player);
                    progress[u][1] = 3;
                }
                if (phase == 80) {
                    putInventory(player, 0);
                    inventory[u] = 0;
                    require(
                        !DarkGreyRpg.getCanonicalTaskManager()
                            .handleEntityInteraction(player, host),
                        "Shortage rejects actual host submission");
                    putInventory(player, 2);
                    inventory[u] = 2;
                }
                if (phase == 100 + u * Integer.getInteger("dgr0402.submissionSpacingTicks", 0)) {
                    if (Boolean.getBoolean("dgr0402.directSubmissions")) submit(u);
                    else queueSubmit(u);
                }
                if (phase == 120) {
                    DarkGreyRpg.getCanonicalTaskManager()
                        .cancelByStory(player, StabilityMixed0402Fixtures.story(1));
                    cancelled[u] = true;
                }
                if (phase == 140) DarkGreyRpg.getCanonicalTaskManager()
                    .synchronizeWorldLogic(player);
                if (phase == 160) checkpoint(player);
                if (phase == 180) {
                    checkpoint(player);
                    EntityPlayerMP replacement = createPlayer(u);
                    CanonicalTaskPlayerTransactions.recover(replacement);
                    players.set(u, replacement);
                    require(replacement != player, "Actual player entity replacement");
                }
                long elapsed = System.nanoTime() - before;
                Stability0402TickStats.stage(4, elapsed);
                if (phase % 20 == 0 || phase == 70 || elapsed >= 20000000L) operations.append(ticks)
                    .append(",phase_")
                    .append(phase)
                    .append(',')
                    .append(u)
                    .append(',')
                    .append(elapsed)
                    .append('\n');
            }
            if (ticks % 400 == 10) burst(128);
            if (ticks % 400 == 210) burst(256);
            if (queueOffered != queueExecuted + queueCancelled && System.nanoTime() - burstAt > 5000000000L)
                throw new AssertionError("Burst did not drain within 5 seconds");
            long validationStarted = System.nanoTime();
            for (int u = 0; u < players.size(); u++) validate(u);
            Stability0402TickStats.stage(2, System.nanoTime() - validationStarted);
            if (ticks % 20 == 0) write("heartbeat.txt", ticks + " " + System.nanoTime());
            if (ticks % 200 == 0) {
                long fixtureIoStarted = System.nanoTime();
                // These logical entities are not in the server's registered player list.
                // Save the fixture's newly prepared inventory before publishing its cold-read ledger.
                for (EntityPlayerMP player : players) checkpoint(player);
                saveStorage();
                ledger();
                long collections = 0, pauses = 0;
                for (java.lang.management.GarbageCollectorMXBean collector : java.lang.management.ManagementFactory
                    .getGarbageCollectorMXBeans()) {
                    collections += collector.getCollectionCount();
                    pauses += collector.getCollectionTime();
                }
                long[] queue = MainThreadScheduler.serverMetrics();
                resources.append(ticks)
                    .append(',')
                    .append(
                        java.lang.management.ManagementFactory.getMemoryMXBean()
                            .getHeapMemoryUsage()
                            .getUsed())
                    .append(',')
                    .append(collections)
                    .append(',')
                    .append(pauses)
                    .append(',')
                    .append(queue[0])
                    .append(',')
                    .append(queue[1])
                    .append(',')
                    .append(queue[7])
                    .append('\n');
                write("memory-queue.csv", resources.toString());
                write("mixed-operations.csv", operations.toString());
                Stability0402TickStats.stage(3, System.nanoTime() - fixtureIoStarted);
            }
            if (ticks >= Integer.getInteger("dgr0402.ticks", 1200)) finish();
        } catch (Exception | AssertionError failure) {
            finished = true;
            failure.printStackTrace();
            System.out.println("DGR0402_REAL_FORGE=FAIL");
            try {
                write("failure.txt", failure.toString());
                writeObservations();
                shutdown();
            } catch (Exception cleanup) {
                cleanup.printStackTrace();
            }
        } finally {
            Stability0402TickStats.stage(0, System.nanoTime() - callbackStarted);
        }
    }

    private void initialize() throws Exception {
        File root = root();
        Class<?> serverType = Class.forName("net.minecraft.server.MinecraftServer");
        server = Stability0402Reflect.method(serverType, "getServer", "func_71276_C")
            .invoke(null);
        world = Stability0402Reflect.method(serverType, "worldServerForDimension", "func_71218_a", int.class)
            .invoke(server, 0);
        storage = (MapStorage) Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
        save = Stability0402Reflect.call(world, "getSaveHandler", "func_72860_G");
        if (!"MixedAuthored".equals(System.getProperty("dgr0402.scenario"))) DarkGreyRpg.getProjectRepository()
            .installSnapshot(StabilityMixed0402Fixtures.project());
        Class<?> items = Class.forName("net.minecraft.init.Items");
        Item apple;
        try {
            apple = (Item) items.getField("apple")
                .get(null);
        } catch (NoSuchFieldException missing) {
            apple = (Item) items.getField("field_151034_e")
                .get(null);
        }
        prototype = new ItemStack(apple, 1, 0);
        ItemIdentitySavedData.get(storage)
            .bindItem(StabilityMixed0402Fixtures.ITEM, ItemStackDefinition.capture(prototype));
        Stability0402Reflect.method(world.getClass(), "getChunkFromBlockCoords", "func_72938_d", int.class, int.class)
            .invoke(world, 0, 0);
        host = (Entity) Class.forName("net.minecraft.entity.passive.EntityVillager")
            .getConstructor(Class.forName("net.minecraft.world.World"))
            .newInstance(world);
        position(host, 1, 5, 0);
        require(
            (Boolean) Stability0402Reflect.method(world.getClass(), "spawnEntityInWorld", "func_72838_d", Entity.class)
                .invoke(world, host),
            "Actual host enters loaded world");
        UUID hostId = (UUID) Stability0402Reflect.call(host, "getUniqueID", "func_110124_au");
        NpcIdentitySavedData npc = NpcIdentitySavedData.get(storage);
        NpcHostIdentity identity = new NpcHostIdentity(hostId, "Villager", 0);
        if (npc.getHost(StabilityMixed0402Fixtures.ACTOR) == null) npc.bind(StabilityMixed0402Fixtures.ACTOR, identity);
        else npc.transfer(StabilityMixed0402Fixtures.ACTOR, identity);
        for (int u = 0; u < 4; u++) players.add(createPlayer(u));
        File ledger = new File(root, "mixed-ledger.csv");
        if (Boolean.getBoolean("dgr0402.prepareFromPlayerImages")) {
            require(!ledger.isFile(), "New workload preparation must not bypass an existing cold-read ledger");
            StringBuilder baseline = new StringBuilder("user,initial_xp,initial_receipts\n");
            for (int u = 0; u < players.size(); u++) {
                EntityPlayerMP player = players.get(u);
                xp[u] = ((Number) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb")).intValue();
                NBTTagCompound data = player.getEntityData();
                receipts[u] = compound(compound(data, "PlayerPersisted"), "DGRTaskReceipts").func_150296_c()
                    .size();
                baseline.append(u)
                    .append(',')
                    .append(xp[u])
                    .append(',')
                    .append(receipts[u])
                    .append('\n');
            }
            write("prepared-player-baseline.csv", baseline.toString());
            write(
                "mixed-preparation.json",
                "{\"status\":\"NEW_WORKLOAD_FROM_RETAINED_PLAYER_IMAGES\",\"cold_restore_asserted\":false,\"receipts_erased\":false}");
        }
        if (ledger.isFile()) {
            for (String row : Files.readAllLines(ledger.toPath(), StandardCharsets.UTF_8)) {
                String[] values = row.split(",");
                int u = Integer.parseInt(values[0]);
                inventory[u] = Integer.parseInt(values[1]);
                xp[u] = Integer.parseInt(values[2]);
                receipts[u] = Integer.parseInt(values[3]);
                cancelled[u] = Boolean.parseBoolean(values[4]);
                rewarded[u] = Boolean.parseBoolean(values[5]);
                for (int kind = 0; kind < 3; kind++) {
                    runs[u][kind] = Long.parseLong(values[6 + kind * 2]);
                    progress[u][kind] = Integer.parseInt(values[7 + kind * 2]);
                }
            }
            for (int u = 0; u < 4; u++) validate(u);
            write(
                "mixed-cold-restore.json",
                "{\"status\":\"PASS\",\"assertions\":\"initial persisted runs inventory XP cumulative receipts progress status before new operations\"}");
        } else cycle();
        write("heartbeat.txt", "initialized " + System.nanoTime());
    }

    private EntityPlayerMP createPlayer(int u) throws Exception {
        Class<?> serverType = Class.forName("net.minecraft.server.MinecraftServer");
        Object interaction = Class.forName("net.minecraft.server.management.ItemInWorldManager")
            .getConstructor(Class.forName("net.minecraft.world.World"))
            .newInstance(world);
        Object profile = Class.forName("com.mojang.authlib.GameProfile")
            .getConstructor(UUID.class, String.class)
            .newInstance(new UUID(40203, u + 1), "DGR402Mixed" + u);
        EntityPlayerMP player = (EntityPlayerMP) Class.forName("net.minecraft.entity.player.EntityPlayerMP")
            .getConstructor(
                serverType,
                Class.forName("net.minecraft.world.WorldServer"),
                profile.getClass(),
                interaction.getClass())
            .newInstance(server, world, profile, interaction);
        Stability0402Reflect
            .method(
                save.getClass(),
                "readPlayerData",
                "func_75752_b",
                Class.forName("net.minecraft.entity.player.EntityPlayer"))
            .invoke(save, player);
        return player;
    }

    private String hostDescription(EntityPlayerMP player) throws Exception {
        return "dead=" + Stability0402Reflect.read(host, "isDead", "field_70128_L")
            + " health="
            + Stability0402Reflect.call(host, "getHealth", "func_110143_aJ")
            + " actors="
            + darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds(host)
            + " visible="
            + Stability0402Reflect.method(player.getClass(), "canEntityBeSeen", "func_70685_l", Entity.class)
                .invoke(player, host);
    }

    private void cycle() throws Exception {
        for (int u = 0; u < players.size(); u++) {
            EntityPlayerMP player = players.get(u);
            for (int kind = 0; kind < 3; kind++) {
                DarkGreyRpg.getCanonicalStoryManager()
                    .reset(player, StabilityMixed0402Fixtures.story(kind));
                require(
                    DarkGreyRpg.getCanonicalStoryManager()
                        .startByEntry(player, StabilityMixed0402Fixtures.story(kind)),
                    "Actual Story owns Task start");
                CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                    .snapshot(player, StabilityMixed0402Fixtures.story(kind), "work");
                runs[u][kind] = task.getActivationTime();
                progress[u][kind] = 0;
            }
            cancelled[u] = false;
            rewarded[u] = false;
            putInventory(player, 3);
            inventory[u] = 3;
        }
    }

    private void validate(int u) throws Exception {
        EntityPlayerMP player = players.get(u);
        require(countInventory(player) == inventory[u], "Independent inventory ledger user=" + u);
        require(
            ((Number) Stability0402Reflect.read(player, "experienceTotal", "field_71067_cb")).intValue() == xp[u],
            "Independent XP ledger user=" + u);
        NBTTagCompound data = (NBTTagCompound) Stability0402Reflect.call(player, "getEntityData", "getEntityData");
        require(
            compound(compound(data, "PlayerPersisted"), "DGRTaskReceipts").func_150296_c()
                .size() == receipts[u],
            "Cumulative receipt ledger user=" + u);
        for (int kind = 0; kind < 3; kind++) {
            CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                .snapshot(player, StabilityMixed0402Fixtures.story(kind), "work");
            require(
                task != null && task.getActivationTime() == runs[u][kind],
                "Preserved run user=" + u + " kind=" + kind);
            require(
                task.getRuntimeSnapshot()
                    .getProgress()
                    .get("objective") == progress[u][kind],
                "Independent progress user=" + u + " kind=" + kind);
            boolean settled = kind == 0 && progress[u][0] == 3 || kind == 2 && rewarded[u];
            CanonicalTaskInstanceStatus expected = settled ? CanonicalTaskInstanceStatus.SETTLED
                : cancelled[u] ? CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
                    : CanonicalTaskInstanceStatus.ACTIVE;
            require(task.getStatus() == expected, "Independent status user=" + u + " kind=" + kind);
        }
    }

    private void submit(int u) throws Exception {
        EntityPlayerMP player = players.get(u);
        if (!DarkGreyRpg.getCanonicalTaskManager()
            .handleEntityInteraction(player, host))
            throw new AssertionError("Actual host commits submit " + hostDescription(player));
        inventory[u] -= 2;
        progress[u][2] = 2;
        receipts[u]++;
        require(
            !DarkGreyRpg.getCanonicalTaskManager()
                .handleEntityInteraction(player, host),
            "Duplicate physical submit has no delta");
        DarkGreyRpg.getCanonicalTaskManager()
            .synchronizeRewards(player);
        xp[u] += 7;
        receipts[u]++;
        rewarded[u] = true;
        DarkGreyRpg.getCanonicalTaskManager()
            .synchronizeRewards(player);
    }

    private void queueSubmit(final int u) {
        final int id = ++queueOffered;
        burstAt = System.nanoTime();
        operations.append(ticks)
            .append(",submit_offered,")
            .append(u)
            .append(",0\n");
        require(MainThreadScheduler.scheduleServer(MainThreadScheduler.serverScope(), () -> {
            require(id == queueExecuted + 1, "Business and burst jobs share accepted FIFO");
            queueExecuted++;
            long before = System.nanoTime();
            try {
                submit(u);
            } catch (Exception | AssertionError failure) {
                queuedBusinessFailure = failure;
                throw new IllegalStateException("Queued actual submit failed", failure);
            } finally {
                long elapsed = System.nanoTime() - before;
                Stability0402TickStats.stage(4, elapsed);
                operations.append(ticks + 1)
                    .append(",submit_executed,")
                    .append(u)
                    .append(',')
                    .append(elapsed)
                    .append('\n');
            }
        }, () -> queueCancelled++), "Actual submit admitted by production queue");
    }

    private void burst(int count) {
        burstAt = System.nanoTime();
        for (int i = 0; i < count; i++) {
            final int id = ++queueOffered;
            require(MainThreadScheduler.scheduleServer(MainThreadScheduler.serverScope(), () -> {
                require(id == queueExecuted + 1, "Accepted FIFO");
                queueExecuted++;
            }, () -> queueCancelled++), "Normal burst admitted");
        }
    }

    private ItemStack[] stacks(EntityPlayerMP player) throws Exception {
        return (ItemStack[]) Stability0402Reflect
            .read(Stability0402Reflect.read(player, "inventory", "field_71071_by"), "mainInventory", "field_70462_a");
    }

    private static NBTTagCompound compound(NBTTagCompound data, String key) throws Exception {
        return (NBTTagCompound) Stability0402Reflect
            .method(NBTTagCompound.class, "getCompoundTag", "func_74775_l", String.class)
            .invoke(data, key);
    }

    private int countInventory(EntityPlayerMP player) throws Exception {
        return CanonicalTaskInventory.count(
            stacks(player),
            darkgrey.rpg.task.forge.MixedBusiness0402Probe.fixtureResource(1)
                .getGraph()
                .getNodes()
                .get(0),
            ItemIdentitySavedData.get(storage));
    }

    private void putInventory(EntityPlayerMP player, int count) throws Exception {
        ItemStack[] slots = stacks(player);
        java.util.Arrays.fill(slots, null);
        if (count > 0) slots[0] = ItemIdentitySavedData.get(storage)
            .getItem(StabilityMixed0402Fixtures.ITEM)
            .createStack(count);
    }

    private static void position(Object entity, double x, double y, double z) throws Exception {
        Stability0402Reflect
            .method(entity.getClass(), "setPosition", "func_70107_b", double.class, double.class, double.class)
            .invoke(entity, x, y, z);
    }

    private void checkpoint(EntityPlayerMP player) throws Exception {
        Stability0402Reflect
            .method(
                save.getClass(),
                "writePlayerData",
                "func_75753_a",
                Class.forName("net.minecraft.entity.player.EntityPlayer"))
            .invoke(save, player);
    }

    private void saveStorage() throws Exception {
        Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
    }

    private void ledger() throws Exception {
        StringBuilder csv = new StringBuilder();
        for (int u = 0; u < players.size(); u++) {
            csv.append(u)
                .append(',')
                .append(inventory[u])
                .append(',')
                .append(xp[u])
                .append(',')
                .append(receipts[u])
                .append(',')
                .append(cancelled[u])
                .append(',')
                .append(rewarded[u]);
            for (int kind = 0; kind < 3; kind++) csv.append(',')
                .append(runs[u][kind])
                .append(',')
                .append(progress[u][kind]);
            csv.append('\n');
        }
        write("mixed-ledger.csv", csv.toString());
    }

    private void finish() throws Exception {
        // Preserve observations even when a final acceptance gate rejects the run.
        writeObservations();
        require(queueOffered == queueExecuted && queueCancelled == 0, "Queue drained without silent cancellation");
        if ("MixedAuthored".equals(System.getProperty("dgr0402.scenario"))) {
            File nativeResult = new File(root(), "authored-result.json");
            require(
                nativeResult.isFile() && nativeResult.lastModified() >= constructedAt
                    && new String(Files.readAllBytes(nativeResult.toPath()), StandardCharsets.UTF_8)
                        .contains("\"PASS\""),
                "Fresh native authored assertions must complete during this mixed run");
        }
        for (EntityPlayerMP player : players) checkpoint(player);
        saveStorage();
        ledger();
        Collections.sort(samples);
        write(
            "complete-tick-result.json",
            "{\"samples\":" + samples.size()
                + ",\"p95_ns\":"
                + samples.get((int) Math.ceil(samples.size() * .95) - 1)
                + ",\"p99_ns\":"
                + samples.get((int) Math.ceil(samples.size() * .99) - 1)
                + ",\"max_ns\":"
                + samples.get(samples.size() - 1)
                + ",\"scope\":\"full MC tick entry through return\"}");
        write(
            "mixed-result.json",
            "{\"status\":\"PASS\",\"layer\":\"S\",\"ticks\":" + ticks
                + ",\"logical_users\":4,\"native_network_players\":"
                + ("MixedAuthored".equals(System.getProperty("dgr0402.scenario")) ? 2 : 0)
                + ",\"submission_ingress\":\""
                + (Boolean.getBoolean("dgr0402.directSubmissions") ? "direct_fixture_pressure"
                    : "production_bounded_server_queue")
                + "\",\"queue_offered\":"
                + queueOffered
                + ",\"queue_executed\":"
                + queueExecuted
                + ",\"assertions\":\"per-tick run inventory XP receipts progress status, shortage duplicate submit, zero settlement, player replacement, normal saves\"}");
        finished = true;
        System.out.println("DGR0402_REAL_FORGE=PASS mixed ticks=" + ticks);
        shutdown();
    }

    private void writeObservations() throws Exception {
        StringBuilder raw = new StringBuilder("sample,complete_tick_ns\n");
        for (int i = 0; i < samples.size(); i++) raw.append(i + 1)
            .append(',')
            .append(samples.get(i))
            .append('\n');
        write("complete-tick-samples.csv", raw.toString());
        Stability0402TickStats.writeProfile(root(), firstCompletedTickIndex, samples.size());
        write("mixed-operations.csv", operations.toString());
    }

    private void shutdown() throws Exception {
        Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
    }

    private File root() throws Exception {
        File root = new File(System.getProperty("dgr0402.root")).getCanonicalFile();
        require(
            root.equals(new File(".").getCanonicalFile()) && root.getPath()
                .contains(File.separator + "0402" + File.separator + "Worlds" + File.separator),
            "Isolated root");
        return root;
    }

    private void write(String name, String text) throws Exception {
        Stability0402TestFiles.write(root(), name, text);
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
