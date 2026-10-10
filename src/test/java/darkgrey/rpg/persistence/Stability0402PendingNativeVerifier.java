package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.EventPriority;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;

/** Actual native login/timer recovery. Cold phase never calls a Forge recovery method. */
public final class Stability0402PendingNativeVerifier {

    private static final String SOURCE = "ST-AAAA-BBBB-CCCC-DDDD", TARGET = "ST-2222-3333-4444-5555";
    private static final String TIMER_SOURCE = "ST-3333-4444-5555-6666", TIMER_TARGET = "ST-7777-8888-9999-AAAA";
    private static final UUID FIRST = offline("DGR402ClientF"), EXISTING = offline("DGR402ClientG");
    private static final UUID UNRELATED = new UUID(40209, 73);
    private File root;
    private Object server;
    private MapStorage storage;
    private CanonicalSessionSavedData data;
    private CanonicalStoryServerService service;
    private JsonObject expected;
    private long firstTargetRun, existingTargetRun, timerSourceRun, timerTargetRun;
    private int ticks, stable, phase, nativePeak;
    private String stableState;
    private boolean initialized, finished;

    @SubscribeEvent(priority = EventPriority.LOWEST)
    public void tick(TickEvent.ServerTickEvent event) throws Exception {
        if (event.phase != TickEvent.Phase.END || finished
            || !"PendingNative".equals(System.getProperty("dgr0402.scenario"))) return;
        try {
            if (!initialized) {
                initialize();
                initialized = true;
                if ("prepare".equals(System.getProperty("dgr0402.pendingMode"))) {
                    prepare();
                    finished = true;
                    shutdown();
                }
                return;
            }
            ticks++;
            if (ticks > 12000) throw new IllegalStateException("Native login/timer recovery timeout");
            List<?> players = players();
            nativePeak = Math.max(nativePeak, players.size());
            verifyUnrelated();
            if (phase == 0 && connected(players, FIRST) && connected(players, EXISTING)) {
                CanonicalStoryInstanceSnapshot first = data.getStorySnapshot(FIRST, TARGET);
                CanonicalStoryInstanceSnapshot existing = data.getStorySnapshot(EXISTING, TARGET);
                if (first != null && existing != null
                    && data.pendingStoryTerminalRouteStoryIds(FIRST)
                        .isEmpty()
                    && data.pendingStoryTerminalRouteStoryIds(EXISTING)
                        .isEmpty()) {
                    firstTargetRun = first.getActivationTime();
                    existingTargetRun = existing.getActivationTime();
                    require(
                        existingTargetRun == expected.get("existing_target_run")
                            .getAsLong(),
                        "Existing target run preserved on real login");
                    checkpoint("native-login-recovered.json");
                    stableState = image();
                    clearDirty();
                    phase = 1;
                    stable = 0;
                }
            } else if (phase == 1) {
                verifyLoginRuns();
                require(
                    image().equals(stableState) && !dirty(),
                    "Repeated native login/timer polling is a serialization/dirty no-op");
                if (++stable >= 220) {
                    timerSourceRun = System.currentTimeMillis();
                    require(
                        service.startByEntry(FIRST, TIMER_SOURCE, timerSourceRun) != null,
                        "Legal terminal timer source prepared");
                    require(
                        data.claimStoryTerminalRoute(FIRST, TIMER_SOURCE, timerSourceRun, "out"),
                        "Pending installed while native player is already online");
                    require(
                        data.getStorySnapshot(FIRST, TIMER_TARGET) == null,
                        "Timer target absent before production periodic entry");
                    phase = 2;
                }
            } else if (phase == 2) {
                verifyLoginRuns();
                CanonicalStoryInstanceSnapshot timer = data.getStorySnapshot(FIRST, TIMER_TARGET);
                if (timer != null && data.pendingStoryTerminalRouteStoryIds(FIRST)
                    .isEmpty()) {
                    timerTargetRun = timer.getActivationTime();
                    checkpoint("native-timer-recovered.json");
                    stableState = image();
                    clearDirty();
                    phase = 3;
                    stable = 0;
                }
            } else if (phase == 3) {
                verifyLoginRuns();
                require(
                    data.getStorySnapshot(FIRST, TIMER_TARGET)
                        .getActivationTime() == timerTargetRun,
                    "Periodic target starts once");
                require(
                    image().equals(stableState) && !dirty(),
                    "Recovered periodic route remains a serialization/dirty no-op");
                if (++stable >= 220) {
                    require(nativePeak >= 2, "Two real registered native players");
                    Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
                    checkpoint("pending-native-result.json");
                    System.out.println(
                        "DGR0402_PENDING_NATIVE=PASS login-null-first existing-target timer idempotent unrelated");
                    finished = true;
                    shutdown();
                }
            }
            if (ticks % 10 == 0) {
                Files.write(
                    new File(root, "heartbeat.txt").toPath(),
                    (ticks + " " + phase).getBytes(StandardCharsets.UTF_8));
            }
        } catch (Exception failure) {
            finished = true;
            failure.printStackTrace();
            if (root != null) Files.write(
                new File(root, "pending-native-FAIL.txt").toPath(),
                failure.toString()
                    .getBytes(StandardCharsets.UTF_8));
            System.out.println("DGR0402_PENDING_NATIVE=FAIL");
            if (server != null) shutdown();
        }
    }

    private void initialize() throws Exception {
        root = new File(System.getProperty("dgr0402.root")).getCanonicalFile();
        require(
            root.equals(new File(".").getCanonicalFile()) && root.getPath()
                .contains("\\.tooling\\0402\\Worlds\\"),
            "Owned isolated world only");
        Class<?> type = Class.forName("net.minecraft.server.MinecraftServer");
        server = Stability0402Reflect.method(type, "getServer", "func_71276_C")
            .invoke(null);
        Object world = Stability0402Reflect.method(type, "worldServerForDimension", "func_71218_a", int.class)
            .invoke(server, 0);
        storage = (MapStorage) Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
        ProjectSnapshot project = DarkGreyRpg.getProjectRepository()
            .getSnapshot();
        require(
            project.getCanonicalStories()
                .size() == 4,
            "Four generated current-format stories loaded by real package reader");
        for (String id : new String[] { SOURCE, TARGET, TIMER_SOURCE, TIMER_TARGET }) require(
            DarkGreyRpg.getStoryPackageLoader()
                .getPackageForStory(id) != null,
            "Real installed package admission " + id);
        data = CanonicalSessionSavedData.get(storage);
        service = new CanonicalStoryServerService(project, data);
        File ledger = new File(root, "pending-native-prepared.json");
        if (!"prepare".equals(System.getProperty("dgr0402.pendingMode"))) {
            expected = new JsonParser().parse(new String(Files.readAllBytes(ledger.toPath()), StandardCharsets.UTF_8))
                .getAsJsonObject();
            require(
                data.getStorySnapshot(FIRST, TARGET) == null && data.pendingStoryTerminalRouteStoryIds(FIRST)
                    .contains(SOURCE),
                "Cold save retains first legal pending before any real login");
            require(
                data.getStorySnapshot(EXISTING, TARGET)
                    .getActivationTime()
                    == expected.get("existing_target_run")
                        .getAsLong(),
                "Cold save retains existing target");
            existingTargetRun = expected.get("existing_target_run")
                .getAsLong();
            verifyUnrelated();
            checkpoint("pending-native-cold-ready.json");
        }
    }

    private void prepare() throws Exception {
        long run = System.currentTimeMillis();
        expected = new JsonObject();
        require(service.startByEntry(FIRST, SOURCE, run) != null, "First source starts");
        require(data.claimStoryTerminalRoute(FIRST, SOURCE, run, "out"), "First pending is durable");
        require(service.startByEntry(EXISTING, SOURCE, run + 1) != null, "Second source starts");
        require(
            service.startByFlow(EXISTING, SOURCE, "out", TARGET, "in", run + 2) != null,
            "Second target already exists");
        data.recordStoryTerminalRouteTarget(EXISTING, SOURCE, run + 1, "out", TARGET, run + 2);
        require(
            data.claimStoryTerminalRoute(EXISTING, SOURCE, run + 1, "out"),
            "Existing target source remains pending");
        require(service.startByEntry(UNRELATED, SOURCE, run + 3) != null, "Unrelated offline source starts");
        expected.addProperty("first_source_run", run);
        expected.addProperty("existing_source_run", run + 1);
        expected.addProperty("existing_target_run", run + 2);
        expected.addProperty("unrelated", unrelatedImage());
        require(data.getStorySnapshot(FIRST, TARGET) == null, "Target is absent until actual native login");
        Files.write(
            new File(root, "pending-native-prepared.json").toPath(),
            expected.toString()
                .getBytes(StandardCharsets.UTF_8));
        Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
        System.out.println("DGR0402_PENDING_NATIVE_PREPARED=PASS");
    }

    private void verifyLoginRuns() {
        require(
            data.getStorySnapshot(FIRST, TARGET)
                .getActivationTime() == firstTargetRun,
            "First target does not restart");
        require(
            data.getStorySnapshot(EXISTING, TARGET)
                .getActivationTime() == existingTargetRun,
            "Existing target does not restart");
        require(
            data.getStorySnapshot(FIRST, SOURCE)
                .getActivationTime()
                == expected.get("first_source_run")
                    .getAsLong(),
            "Source run remains stable");
    }

    private void verifyUnrelated() {
        require(
            unrelatedImage().equals(
                expected.get("unrelated")
                    .getAsString()),
            "Unrelated offline player is byte-equivalent");
        require(data.getStorySnapshot(UNRELATED, TARGET) == null, "Unrelated offline target was not executed");
    }

    private String unrelatedImage() {
        return CanonicalStoryInstanceNbtCodec
            .encode(Collections.singletonList(data.getStorySnapshot(UNRELATED, SOURCE)))
            .toString();
    }

    private String image() throws Exception {
        NBTTagCompound state = new NBTTagCompound();
        Stability0402Reflect.method(data.getClass(), "writeToNBT", "func_76187_b", NBTTagCompound.class)
            .invoke(data, state);
        return state.toString();
    }

    private void clearDirty() throws Exception {
        Stability0402Reflect.method(data.getClass(), "setDirty", "func_76186_a", boolean.class)
            .invoke(data, false);
    }

    private boolean dirty() throws Exception {
        return (Boolean) Stability0402Reflect.call(data, "isDirty", "func_76188_b");
    }

    private void checkpoint(String name) throws Exception {
        JsonObject record = new JsonObject();
        record.addProperty("status", "pending-native-result.json".equals(name) ? "PASS" : "OBSERVED");
        record.addProperty("tick", ticks);
        record.addProperty("phase", phase);
        record.addProperty("network_peak", nativePeak);
        record.addProperty("first_target_run", firstTargetRun);
        record.addProperty("existing_target_run", existingTargetRun);
        record.addProperty("timer_target_run", timerTargetRun);
        record.addProperty("forge_recover_called_by_test", false);
        record.addProperty("unrelated_unchanged", true);
        Files.write(
            new File(root, name).toPath(),
            record.toString()
                .getBytes(StandardCharsets.UTF_8));
    }

    private List<?> players() throws Exception {
        Object configuration = Stability0402Reflect.call(server, "getConfigurationManager", "func_71203_ab");
        return (List<?>) Stability0402Reflect.read(configuration, "playerEntityList", "field_72404_b");
    }

    private boolean connected(List<?> players, UUID id) throws Exception {
        for (Object player : players)
            if (id.equals(Stability0402Reflect.call(player, "getUniqueID", "func_110124_au"))) return true;
        return false;
    }

    private void shutdown() throws Exception {
        Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
    }

    private static UUID offline(String name) {
        return UUID.nameUUIDFromBytes(("OfflinePlayer:" + name).getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new IllegalStateException(message);
    }
}
