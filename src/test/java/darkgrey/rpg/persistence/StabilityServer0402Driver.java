package darkgrey.rpg.persistence;

import java.io.File;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import cpw.mods.fml.common.FMLCommonHandler;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.event.FMLInitializationEvent;
import cpw.mods.fml.common.eventhandler.EventPriority;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.story.canonical.forge.Stability0402Fixtures;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;

/** Separate isolated driver; production JAR never contains this class or fixtures. */
@Mod(
    modid = "dgr0402stabilitytest",
    name = "DGR 0402 isolated stability",
    version = "1",
    dependencies = "required-after:darkgrey_rpg")
public final class StabilityServer0402Driver {

    @Mod.EventHandler
    public void stopped(cpw.mods.fml.common.event.FMLServerStoppedEvent event) throws Exception {
        if (Boolean.getBoolean("dgr0402.baseline")) return;
        String location = System.getProperty("dgr0402.root", System.getProperty("dgr0402.clientRoot"));
        if (location == null) return;
        File root = new File(location).getCanonicalFile();
        require(
            root.getPath()
                .contains("\\.tooling\\0402\\Worlds\\"),
            "Owned cache-stop observer only");
        Class<?> transactions = darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions.class;
        int journals = 0, recovered = 0;
        for (String name : new String[] { "JOURNALS", "RECOVERED" }) {
            Field field = transactions.getDeclaredField(name);
            field.setAccessible(true);
            int count = ((java.util.Map<?, ?>) field.get(null)).size();
            if (name.equals("JOURNALS")) journals = count;
            else recovered = count;
        }
        String result = "{\"status\":\"" + (journals == 0 && recovered == 0 ? "PASS" : "FAIL")
            + "\",\"journal_contexts\":"
            + journals
            + ",\"recovered_contexts\":"
            + recovered
            + "}";
        Files.write(new File(root, "journal-cache-stop.json").toPath(), result.getBytes(StandardCharsets.UTF_8));
        System.out.println("DGR0402_CACHE_STOP=" + (journals == 0 && recovered == 0 ? "PASS" : "FAIL"));
        require(journals == 0 && recovered == 0, "Player journal caches clear at server stop");
    }

    private Object server, storage, player;
    private final StabilityWorld0402Verifier nativeWorlds = new StabilityWorld0402Verifier();
    private CanonicalSessionSavedData data;
    private CanonicalStoryForgeManager manager;
    private Field storeField;
    private int ticks, binds;
    private boolean started, failed, finished;
    private long tickStarted;
    private final List<Long> samples = new ArrayList<>();
    private final List<Long> completeTickSamples = new ArrayList<>();
    private final List<Object> logicalPlayers = new ArrayList<>();
    private final int activeTasks = Integer.getInteger("dgr0402.active", 0);
    private final int logicalUsers = Integer.getInteger("dgr0402.users", 1);
    private final int nodes = Integer.getInteger("dgr0402.nodes", 20);
    private final int offline = Integer.getInteger("dgr0402.offline", 0);
    private final int warmupTicks = Integer.getInteger("dgr0402.warmupTicks", 0);
    private final int expectedNetworkPlayers = Integer.getInteger("dgr0402.network", 0);
    private final java.util.Map<UUID, long[]> networkLedger = new java.util.LinkedHashMap<>();
    private int networkPeak;
    private long[][] events;
    private ProjectSnapshot project;
    private int initializedTasks;
    private long firstCompletedTickIndex;
    private long startedAt;
    private final boolean idle = "Idle".equals(System.getProperty("dgr0402.scenario"));
    private final boolean staticPlayers = "StaticPlayers".equals(System.getProperty("dgr0402.scenario"));
    private net.minecraft.nbt.NBTTagCompound idleState;
    private final StringBuilder memorySamples = new StringBuilder(
        "tick,heap_used_bytes,gc_count,gc_ms,queue_pending,queue_peak,queue_oldest_ns\n");
    private final StringBuilder operationSamples = new StringBuilder(
        "tick,user,world_logic_ns,objectives_ns,dispatch_ns\n");
    private final UUID uuid = new UUID(40201, 1);

    @Mod.EventHandler
    public void init(FMLInitializationEvent event) throws Exception {
        if (event.getSide()
            .isClient() && Boolean.getBoolean("dgr0402.hudFixture"))
            FMLCommonHandler.instance()
                .bus()
                .register(
                    Class.forName("darkgrey.rpg.persistence.StabilityClient0402HudFixture")
                        .newInstance());
        FMLCommonHandler.instance()
            .bus()
            .register(this);
        FMLCommonHandler.instance()
            .bus()
            .register(nativeWorlds);
        FMLCommonHandler.instance()
            .bus()
            .register(new StabilityMixed0402Verifier());
        FMLCommonHandler.instance()
            .bus()
            .register(new StabilityAuthored0402Verifier());
        if (Boolean.getBoolean("dgr0402.crashStop")) FMLCommonHandler.instance()
            .bus()
            .register(new Stability0402CrashStopVerifier());
        if ("BoundaryFault".equals(System.getProperty("dgr0402.scenario"))) FMLCommonHandler.instance()
            .bus()
            .register(new StabilityBoundary0402Verifier());
        if ("Qualification".equals(System.getProperty("dgr0402.scenario"))) FMLCommonHandler.instance()
            .bus()
            .register(new StabilityQualification0402Verifier());
        if ("PendingNative".equals(System.getProperty("dgr0402.scenario"))) FMLCommonHandler.instance()
            .bus()
            .register(new Stability0402PendingNativeVerifier());
        if (event.getSide()
            .isClient() && System.getProperty("dgr0402.clientRoot") != null)
            FMLCommonHandler.instance()
                .bus()
                .register(
                    Class.forName("darkgrey.rpg.persistence.StabilityClient0402Observer")
                        .newInstance());
    }

    @Mod.EventHandler
    public void serverStarting(cpw.mods.fml.common.event.FMLServerStartingEvent event) throws Exception {
        nativeWorlds.prepare();
    }

    @SubscribeEvent(priority = EventPriority.HIGHEST)
    public void start(TickEvent.ServerTickEvent event) {
        if (event.phase == TickEvent.Phase.START) tickStarted = System.nanoTime();
    }

    @SubscribeEvent(priority = EventPriority.LOWEST)
    public void tick(TickEvent.ServerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || finished
            || System.getProperty("dgr0402.root") == null
            || "MixedLifecycle".equals(System.getProperty("dgr0402.scenario"))
            || "MixedAuthored".equals(System.getProperty("dgr0402.scenario"))
            || "BoundaryFault".equals(System.getProperty("dgr0402.scenario"))
            || "Qualification".equals(System.getProperty("dgr0402.scenario"))
            || "PendingNative".equals(System.getProperty("dgr0402.scenario"))
            || "NormalAuthored".equals(System.getProperty("dgr0402.scenario"))) return;
        try {
            if (!started) {
                long initializationStarted = System.nanoTime();
                initialize();
                Stability0402TickStats.stage(5, System.nanoTime() - initializationStarted);
                started = true;
                return;
            }
            if (initializedTasks < activeTasks) {
                int i = initializedTasks++;
                write("heartbeat.txt", "initializing " + i + " " + System.nanoTime());
                net.minecraft.entity.player.EntityPlayerMP owner = (net.minecraft.entity.player.EntityPlayerMP) logicalPlayers
                    .get(i % logicalUsers);
                if (DarkGreyRpg.getCanonicalTaskManager()
                    .snapshot(owner, Stability0402Fixtures.loadStory(i / logicalUsers), "work") == null)
                    require(
                        manager.startByEntry(
                            (net.minecraft.entity.player.EntityPlayerMP) logicalPlayers.get(i % logicalUsers),
                            Stability0402Fixtures.loadStory(i / logicalUsers)),
                        "Actual Story/Task start " + i);
                return;
            }
            Object previous = storeField.get(data);
            net.minecraft.entity.player.EntityPlayerMP actual = (net.minecraft.entity.player.EntityPlayerMP) player;
            if (!idle) manager.synchronizeMedia(actual);
            binds += previous != storeField.get(data) ? 1 : 0;
            previous = storeField.get(data);
            if (!idle) manager.recoverPendingRoutes(actual);
            binds += previous != storeField.get(data) ? 1 : 0;
            previous = storeField.get(data);
            if (!idle) manager.matchingRegionTriggers(actual, 0, 0, 64, 0);
            binds += previous != storeField.get(data) ? 1 : 0;
            previous = storeField.get(data);
            if (!idle) manager.handleRegionPosition(actual, 0, 0, 64, 0, java.util.Collections.emptyList());
            binds += previous != storeField.get(data) ? 1 : 0;
            ticks++;
            if (expectedNetworkPlayers > 0 && ticks % 20 == 0) networkStep();
            if (activeTasks > 0 && ticks % 20 == 0) {
                int target = (ticks / 20 - 1) % 10;
                for (int u = 0; u < logicalUsers; u++) {
                    net.minecraft.entity.player.EntityPlayerMP user = (net.minecraft.entity.player.EntityPlayerMP) logicalPlayers
                        .get(u);
                    long beforeLogic = System.nanoTime();
                    DarkGreyRpg.getCanonicalTaskManager()
                        .synchronizeWorldLogic(user);
                    long beforeObjectives = System.nanoTime();
                    DarkGreyRpg.getCanonicalTaskManager()
                        .synchronizeObjectives(user);
                    long beforeDispatch = System.nanoTime();
                    if (!staticPlayers) DarkGreyRpg.getCanonicalTaskManager()
                        .dispatch(
                            user,
                            darkgrey.rpg.task.runtime.CanonicalTaskEvent
                                .killEntity(Stability0402Fixtures.SOURCE + "~actor~load" + target));
                    if (!staticPlayers) events[u][target]++;
                    operationSamples.append(ticks)
                        .append(',')
                        .append(u)
                        .append(',')
                        .append(beforeObjectives - beforeLogic)
                        .append(',')
                        .append(beforeDispatch - beforeObjectives)
                        .append(',')
                        .append(System.nanoTime() - beforeDispatch)
                        .append('\n');
                }
                if (ticks % 100 == 0) write("operation-samples.csv", operationSamples.toString());
            }
            if (staticPlayers && ticks == Math.max(1, warmupTicks - 100)) {
                method(storage.getClass(), "saveAllData", "func_75744_a").invoke(storage);
                idleState = stateText();
            }
            if (ticks > warmupTicks) {
                samples.add(System.nanoTime() - tickStarted);
                require(Stability0402TickStats.completed > 0, "Complete tick instrumentation missing");
                if (completeTickSamples.isEmpty()) firstCompletedTickIndex = Stability0402TickStats.completed;
                completeTickSamples.add(Stability0402TickStats.lastNanos);
            }
            if (ticks % 100 == 0) {
                write("heartbeat.txt", ticks + " " + System.nanoTime());
                long collections = 0, pauses = 0;
                for (java.lang.management.GarbageCollectorMXBean collector : java.lang.management.ManagementFactory
                    .getGarbageCollectorMXBeans()) {
                    collections += collector.getCollectionCount();
                    pauses += collector.getCollectionTime();
                }
                long[] queue = Boolean.getBoolean("dgr0402.baseline") ? new long[10]
                    : darkgrey.rpg.network.MainThreadScheduler.serverMetrics();
                memorySamples.append(ticks)
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
            }
            if (ticks >= Integer.getInteger("dgr0402.ticks", 200)) finish();
        } catch (Exception | AssertionError failure) {
            failure.printStackTrace();
            failed = true;
            finished = true;
            System.out.println("DGR0402_REAL_FORGE=FAIL");
            try {
                write("failure.txt", failure.toString());
                shutdown();
            } catch (Exception cleanup) {
                cleanup.printStackTrace();
            }
        }
    }

    private void initialize() throws Exception {
        File root = new File(System.getProperty("dgr0402.root")).getCanonicalFile();
        startedAt = System.nanoTime();
        require(
            root.equals(new File(".").getCanonicalFile()) && root.getPath()
                .contains(File.separator + "0402" + File.separator + "Worlds" + File.separator),
            "Isolated root required");
        Class<?> serverType = Class.forName("net.minecraft.server.MinecraftServer");
        server = method(serverType, "getServer", "func_71276_C").invoke(null);
        Object world = method(serverType, "worldServerForDimension", "func_71218_a", int.class).invoke(server, 0);
        storage = field(world.getClass(), "mapStorage", "field_72988_C").get(world);
        data = CanonicalSessionSavedData.get((net.minecraft.world.storage.MapStorage) storage);
        require(
            data == CanonicalSessionSavedData.get((net.minecraft.world.storage.MapStorage) storage),
            "Production MapStorage identity");
        Object interaction = Class.forName("net.minecraft.server.management.ItemInWorldManager")
            .getConstructor(Class.forName("net.minecraft.world.World"))
            .newInstance(world);
        Object profile = Class.forName("com.mojang.authlib.GameProfile")
            .getConstructor(UUID.class, String.class)
            .newInstance(uuid, "DGR402Logical");
        player = Class.forName("net.minecraft.entity.player.EntityPlayerMP")
            .getConstructor(
                serverType,
                Class.forName("net.minecraft.world.WorldServer"),
                profile.getClass(),
                interaction.getClass())
            .newInstance(server, world, profile, interaction);
        project = activeTasks == 0 ? Stability0402Fixtures.flow()
            : Stability0402Fixtures.load((activeTasks + logicalUsers - 1) / logicalUsers, nodes);
        DarkGreyRpg.getProjectRepository()
            .installSnapshot(project);
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        if (!idle && data.getStorySnapshot(uuid, Stability0402Fixtures.SOURCE) == null) {
            service.startByEntry(uuid, Stability0402Fixtures.SOURCE, 402L);
            data.claimStoryTerminalRoute(uuid, Stability0402Fixtures.SOURCE, 402L, "out");
        }
        manager = DarkGreyRpg.getCanonicalStoryManager();
        boolean recovered = !idle && manager.recoverPendingRoutes((net.minecraft.entity.player.EntityPlayerMP) player);
        boolean baseline = Boolean.getBoolean("dgr0402.baseline");
        require(
            idle || baseline ? !recovered
                : recovered || data.pendingStoryTerminalRouteStoryIds(uuid)
                    .isEmpty(),
            "Production null-first recovery");
        require(
            idle || baseline ? data.getStorySnapshot(uuid, Stability0402Fixtures.TARGET) == null
                : data.getStorySnapshot(uuid, Stability0402Fixtures.TARGET) != null,
            "Target run");
        storeField = CanonicalSessionSavedData.class.getDeclaredField("store");
        storeField.setAccessible(true);
        logicalPlayers.add(player);
        for (int u = 1; u < logicalUsers; u++) {
            Object otherProfile = profile.getClass()
                .getConstructor(UUID.class, String.class)
                .newInstance(new UUID(40201, u + 1), "DGR402Logic" + u);
            logicalPlayers.add(
                player.getClass()
                    .getConstructor(serverType, world.getClass(), profile.getClass(), interaction.getClass())
                    .newInstance(server, world, otherProfile, interaction));
        }
        events = new long[logicalUsers][10];
        File expectedLedger = new File(root, "event-ledger.csv");
        if (expectedLedger.isFile())
            for (String line : Files.readAllLines(expectedLedger.toPath(), StandardCharsets.UTF_8)) {
                String[] fields = line.split(",");
                events[Integer.parseInt(fields[0])][Integer.parseInt(fields[1])] = Long.parseLong(fields[2]);
            }
        if (offline > 0) {
            Field storyStore = CanonicalSessionSavedData.class.getDeclaredField("storyStore");
            storyStore.setAccessible(true);
            darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore instances = (darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore) storyStore
                .get(data);
            for (int i = 0; i < offline; i++) instances.start(
                new UUID(40202, i),
                project.getCanonicalStory(Stability0402Fixtures.SOURCE),
                "out",
                darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy.ONCE,
                402L);
            method(data.getClass(), "markDirty", "func_76185_a").invoke(data);
        }
        if (idle) {
            method(storage.getClass(), "saveAllData", "func_75744_a").invoke(storage);
            idleState = stateText();
        }
        System.out.println(
            "DGR0402_INITIALIZED baseline=" + baseline
                + " logical_users="
                + logicalUsers
                + " active_tasks="
                + activeTasks
                + " offline="
                + offline
                + " network_players=0 pending_recovered="
                + recovered);
    }

    private void finish() throws Exception {
        finished = true;
        boolean baseline = Boolean.getBoolean("dgr0402.baseline");
        require(binds == (baseline && !idle ? ticks * 4 : 0), "Poll binding count " + binds);
        if (idle) {
            require(
                data.storySnapshots()
                    .size() == offline,
                "Only offline fixture stories exist");
            require(
                data.getStorySnapshot(uuid, Stability0402Fixtures.SOURCE) == null,
                "Idle never starts an online story");
            require(idleState.equals(stateText()), "Idle preserves every serialized story and route");
            require(!(Boolean) Stability0402Reflect.call(data, "isDirty", "func_76188_b"), "Idle remains clean");
        }
        if (staticPlayers) {
            require(
                idleState != null && idleState.equals(stateText()),
                "Static players preserve all offline world state");
            require(
                !(Boolean) Stability0402Reflect.call(data, "isDirty", "func_76188_b"),
                "Static world remains clean");
        }
        require(networkPeak >= expectedNetworkPlayers, "Actual network peak " + networkPeak);
        darkgrey.rpg.task.persistence.CanonicalTaskSavedData taskData = darkgrey.rpg.task.persistence.CanonicalTaskSavedData
            .get((net.minecraft.world.storage.MapStorage) storage);
        require(taskData.size() == activeTasks + networkLedger.size(), "Actual task count " + taskData.size());
        for (int i = 0; i < activeTasks; i++) {
            int u = i % logicalUsers;
            darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot task = taskData
                .getSnapshot(new UUID(40201, u + 1), Stability0402Fixtures.loadStory(i / logicalUsers), "work");
            require(
                task != null && task.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
                "Actual ACTIVE placement " + i);
            for (int n = 0; n < nodes; n++) require(
                task.getRuntimeSnapshot()
                    .getProgress()
                    .get("objective" + n) == events[u][n % 10],
                "Independent event ledger " + i + "/" + n);
        }
        method(storage.getClass(), "saveAllData", "func_75744_a").invoke(storage);
        StringBuilder ledger = new StringBuilder();
        for (int u = 0; u < logicalUsers; u++) for (int target = 0; target < 10; target++) ledger.append(u)
            .append(',')
            .append(target)
            .append(',')
            .append(events[u][target])
            .append('\n');
        write("event-ledger.csv", ledger.toString());
        write("memory-queue.csv", memorySamples.toString());
        write("operation-samples.csv", operationSamples.toString());
        write(
            "load-profile.json",
            "{\"active_tasks\":" + activeTasks
                + ",\"logical_users\":"
                + logicalUsers
                + ",\"network_players\":"
                + networkPeak
                + ",\"task_nodes\":"
                + nodes
                + ",\"offline_instances\":"
                + offline
                + ",\"warmup_ticks\":"
                + warmupTicks
                + ",\"duration_ns\":"
                + (System.nanoTime() - startedAt)
                + ",\"operation\":\"indexed kill event, each logical user once per 20 ticks; zero settlement, legal ACTIVE waits\"}");
        StringBuilder csv = new StringBuilder("tick,forge_tick_span_ns\n");
        for (int i = 0; i < samples.size(); i++) csv.append(i + 1)
            .append(',')
            .append(samples.get(i))
            .append('\n');
        write("tick-samples.csv", csv.toString());
        csv = new StringBuilder("sample,complete_tick_ns\n");
        for (int i = 0; i < completeTickSamples.size(); i++) csv.append(i + 1)
            .append(',')
            .append(completeTickSamples.get(i))
            .append('\n');
        write("complete-tick-samples.csv", csv.toString());
        Stability0402TickStats.writeProfile(
            new File(System.getProperty("dgr0402.root")),
            firstCompletedTickIndex,
            completeTickSamples.size());
        java.util.Collections.sort(completeTickSamples);
        write(
            "complete-tick-result.json",
            "{\"samples\":" + completeTickSamples.size()
                + ",\"p95_ns\":"
                + completeTickSamples.get((int) Math.ceil(completeTickSamples.size() * .95) - 1)
                + ",\"p99_ns\":"
                + completeTickSamples.get((int) Math.ceil(completeTickSamples.size() * .99) - 1)
                + ",\"max_ns\":"
                + completeTickSamples.get(completeTickSamples.size() - 1)
                + ",\"scope\":\"MinecraftServer.tick entry through return, test-only ASM\"}");
        java.util.Collections.sort(samples);
        String result = "{\"baseline\":" + baseline
            + ",\"ticks\":"
            + ticks
            + ",\"calls\":"
            + (idle ? 0 : ticks * 4)
            + ",\"rebuilds\":"
            + binds
            + ",\"logical_users\":"
            + logicalUsers
            + ",\"network_players\":"
            + networkPeak
            + ",\"p95_ns\":"
            + samples.get((int) Math.ceil(samples.size() * 0.95) - 1)
            + ",\"p99_ns\":"
            + samples.get((int) Math.ceil(samples.size() * 0.99) - 1)
            + ",\"max_ns\":"
            + samples.get(samples.size() - 1)
            + ",\"metric_scope\":\"HIGHEST START to LOWEST END, includes DGR, omits event dispatch edges\"}";
        write("result.json", result);
        System.out.println("DGR0402_REAL_FORGE=PASS " + result);
        shutdown();
    }

    private net.minecraft.nbt.NBTTagCompound stateText() throws Exception {
        net.minecraft.nbt.NBTTagCompound state = new net.minecraft.nbt.NBTTagCompound();
        method(data.getClass(), "writeToNBT", "func_76187_b", net.minecraft.nbt.NBTTagCompound.class)
            .invoke(data, state);
        return state;
    }

    private void write(String name, String value) throws Exception {
        Stability0402TestFiles.write(new File(System.getProperty("dgr0402.root")), name, value);
    }

    private void networkStep() throws Exception {
        Object configuration = method(server.getClass(), "getConfigurationManager", "func_71203_ab").invoke(server);
        java.util.List<?> players = (java.util.List<?>) field(
            configuration.getClass(),
            "playerEntityList",
            "field_72404_b").get(configuration);
        int active = 0;
        for (Object connected : new java.util.ArrayList<Object>(players)) {
            Object profile = method(connected.getClass(), "getGameProfile", "func_146103_bH").invoke(connected);
            String name = (String) profile.getClass()
                .getMethod("getName")
                .invoke(profile);
            if (!name.startsWith("DGR402Client")) continue;
            UUID id = (UUID) profile.getClass()
                .getMethod("getId")
                .invoke(profile);
            active++;
            net.minecraft.entity.player.EntityPlayerMP actual = (net.minecraft.entity.player.EntityPlayerMP) connected;
            long[] counts = networkLedger.get(id);
            if (counts == null) {
                require(
                    manager.startByEntry(actual, Stability0402Fixtures.loadStory(0)),
                    "Network Story start " + name);
                counts = new long[10];
                networkLedger.put(id, counts);
                System.out.println("DGR0402_NETWORK_JOIN " + name + " " + id);
            }
            int target = (ticks / 20 - 1 + (name.endsWith("B") ? 3 : 0)) % 10;
            DarkGreyRpg.getCanonicalTaskManager()
                .dispatch(
                    actual,
                    darkgrey.rpg.task.runtime.CanonicalTaskEvent
                        .killEntity(Stability0402Fixtures.SOURCE + "~actor~load" + target));
            counts[target]++;
            darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                .snapshot(actual, Stability0402Fixtures.loadStory(0), "work");
            for (int n = 0; n < nodes; n++) require(
                task.getRuntimeSnapshot()
                    .getProgress()
                    .get("objective" + n) == counts[n % 10],
                "Network isolated ledger " + name + "/" + n);
        }
        networkPeak = Math.max(networkPeak, active);
        write(
            "network-ledger.json",
            "{\"peak_network_players\":" + networkPeak
                + ",\"distinct_network_players\":"
                + networkLedger.size()
                + ",\"progress_assertions\":\"each connected player, per objective after every event\"}");
    }

    private void shutdown() throws Exception {
        method(server.getClass(), "initiateShutdown", "func_71263_m").invoke(server);
    }

    private static Field field(Class<?> type, String... names) throws Exception {
        for (Class<?> current = type; current != null; current = current.getSuperclass())
            for (String name : names) try {
                Field field = current.getDeclaredField(name);
                field.setAccessible(true);
                return field;
            } catch (NoSuchFieldException ignored) {}
        throw new NoSuchFieldException(java.util.Arrays.toString(names));
    }

    private static Method method(Class<?> type, String mcp, String srg, Class<?>... parameters) throws Exception {
        for (Class<?> current = type; current != null; current = current.getSuperclass())
            for (String name : new String[] { mcp, srg }) try {
                Method method = current.getDeclaredMethod(name, parameters);
                method.setAccessible(true);
                return method;
            } catch (NoSuchMethodException ignored) {}
        throw new NoSuchMethodException(mcp);
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
