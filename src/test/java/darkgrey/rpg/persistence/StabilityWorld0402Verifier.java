package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.forge.Stability0402Fixtures;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Native GUI switches actual integrated worlds; this server-thread verifier maintains an independent ledger. */
public final class StabilityWorld0402Verifier {

    private final Map<String, Expected> worlds = new LinkedHashMap<>();
    private final ProjectSnapshot project = Stability0402Fixtures.load(2, 20);
    private int currentServer, ticks, generations;
    private boolean failed;

    public void prepare() throws Exception {
        if (!Boolean.getBoolean("dgr0402.worldProbe")) return;
        File root = root();
        if (worlds.isEmpty()) {
            File ledger = new File(root, "native-world-ledger.csv");
            if (ledger.isFile()) for (String line : Files.readAllLines(ledger.toPath(), StandardCharsets.UTF_8)) {
                String[] fields = line.split(",");
                Expected expected = new Expected();
                expected.run = Long.parseLong(fields[1]);
                expected.sourceRun = Long.parseLong(fields[2]);
                for (int i = 0; i < 10; i++) expected.progress[i] = Long.parseLong(fields[i + 3]);
                worlds.put(fields[0], expected);
            }
        }
        DarkGreyRpg.getProjectRepository()
            .installSnapshot(project);
        ticks = 0;
    }

    @SubscribeEvent
    public void tick(TickEvent.ServerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !Boolean.getBoolean("dgr0402.worldProbe") || failed) return;
        try {
            Class<?> serverClass = Class.forName("net.minecraft.server.MinecraftServer");
            Object server = Stability0402Reflect.method(serverClass, "getServer", "func_71276_C")
                .invoke(null);
            Object world = Stability0402Reflect
                .method(serverClass, "worldServerForDimension", "func_71218_a", int.class)
                .invoke(server, 0);
            String name = (String) Stability0402Reflect
                .call(Stability0402Reflect.call(world, "getWorldInfo", "func_72912_H"), "getWorldName", "func_76065_j");
            require(name.equals("WorldA0402") || name.equals("WorldB0402"), "Only isolated named worlds");
            Object configuration = Stability0402Reflect.call(server, "getConfigurationManager", "func_71203_ab");
            java.util.List<?> players = (java.util.List<?>) Stability0402Reflect
                .read(configuration, "playerEntityList", "field_72404_b");
            if (players.isEmpty()) return;
            Object player = players.get(0);
            String user = (String) Stability0402Reflect.call(player, "getGameProfile", "func_146103_bH")
                .getClass()
                .getMethod("getName")
                .invoke(Stability0402Reflect.call(player, "getGameProfile", "func_146103_bH"));
            require(user.startsWith("DGR402Client"), "Owned test profile only");
            UUID uuid = (UUID) Stability0402Reflect.call(player, "getUniqueID", "func_110124_au");
            Object storage = Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
            CanonicalSessionSavedData storyData = CanonicalSessionSavedData
                .get((net.minecraft.world.storage.MapStorage) storage);
            CanonicalTaskSavedData tasks = CanonicalTaskSavedData.get((net.minecraft.world.storage.MapStorage) storage);
            net.minecraft.entity.player.EntityPlayerMP actual = (net.minecraft.entity.player.EntityPlayerMP) player;
            Expected expected = worlds.get(name);
            boolean entered = currentServer != System.identityHashCode(server);
            if (entered) {
                currentServer = System.identityHashCode(server);
                generations++;
                ticks = 0;
                if (expected == null) {
                    require(
                        storyData.getStorySnapshot(uuid, Stability0402Fixtures.SOURCE) == null && tasks.size() == 0,
                        "New world owns fresh progress");
                    expected = new Expected();
                    require(
                        DarkGreyRpg.getCanonicalStoryManager()
                            .startByEntry(actual, Stability0402Fixtures.SOURCE),
                        "Actual source start");
                    require(
                        DarkGreyRpg.getCanonicalStoryManager()
                            .startByEntry(actual, Stability0402Fixtures.loadStory(0)),
                        "Actual Task start");
                    expected.sourceRun = storyData.getStorySnapshot(uuid, Stability0402Fixtures.SOURCE)
                        .getActivationTime();
                    expected.run = DarkGreyRpg.getCanonicalTaskManager()
                        .snapshot(actual, Stability0402Fixtures.loadStory(0), "work")
                        .getActivationTime();
                    worlds.put(name, expected);
                }
                require(
                    storyData.getStorySnapshot(uuid, Stability0402Fixtures.SOURCE)
                        .getActivationTime() == expected.sourceRun,
                    "Cold source run unchanged");
                require(
                    storyData.getStorySnapshot(uuid, Stability0402Fixtures.TARGET) != null,
                    "Automatic terminal target survives");
                require(MainThreadState.pending() == 0, "No old queued tasks survive server start");
            }
            ticks++;
            if (ticks % 20 == 0) {
                int target = (int) (sum(expected.progress) % 10);
                DarkGreyRpg.getCanonicalTaskManager()
                    .dispatch(
                        actual,
                        CanonicalTaskEvent.killEntity(Stability0402Fixtures.SOURCE + "~actor~load" + target));
                expected.progress[target]++;
            }
            CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                .snapshot(actual, Stability0402Fixtures.loadStory(0), "work");
            require(task != null && task.getActivationTime() == expected.run, "World Task run unchanged");
            for (int n = 0; n < 20; n++) require(
                task.getRuntimeSnapshot()
                    .getProgress()
                    .get("objective" + n) == expected.progress[n % 10],
                "World independent progress " + n);
            if (entered || ticks % 20 == 0) {
                writeLedger();
                String status = "{\"status\":\"PASS_CURRENT_WORLD_ASSERTIONS\",\"world\":\"" + name
                    + "\",\"generations\":"
                    + generations
                    + ",\"source_run\":"
                    + expected.sourceRun
                    + ",\"task_run\":"
                    + expected.run
                    + ",\"events\":"
                    + sum(expected.progress)
                    + ",\"tick\":"
                    + ticks
                    + "}";
                Files.write(
                    new File(root(), "native-world-state.json").toPath(),
                    status.getBytes(StandardCharsets.UTF_8));
            }
        } catch (Exception | AssertionError failure) {
            failed = true;
            failure.printStackTrace();
            try {
                Files.write(
                    new File(root(), "native-world-FAIL.txt").toPath(),
                    failure.toString()
                        .getBytes(StandardCharsets.UTF_8));
            } catch (Exception output) {
                output.printStackTrace();
            }
        }
    }

    private void writeLedger() throws Exception {
        StringBuilder result = new StringBuilder();
        for (Map.Entry<String, Expected> entry : worlds.entrySet()) {
            Expected value = entry.getValue();
            result.append(entry.getKey())
                .append(',')
                .append(value.run)
                .append(',')
                .append(value.sourceRun);
            for (long progress : value.progress) result.append(',')
                .append(progress);
            result.append('\n');
        }
        Files.write(
            new File(root(), "native-world-ledger.csv").toPath(),
            result.toString()
                .getBytes(StandardCharsets.UTF_8));
    }

    private static long sum(long[] counts) {
        long result = 0;
        for (long count : counts) result += count;
        return result;
    }

    private static File root() throws Exception {
        File root = new File(System.getProperty("dgr0402.clientRoot")).getCanonicalFile();
        require(
            root.equals(new File(".").getCanonicalFile()) && root.getPath()
                .contains(File.separator + "0402" + File.separator + "Worlds" + File.separator),
            "Isolated client root");
        return root;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static final class Expected {

        long run, sourceRun;
        final long[] progress = new long[10];
    }

    private static final class MainThreadState {

        static long pending() {
            return Boolean.getBoolean("dgr0402.baseline") ? 0
                : darkgrey.rpg.network.MainThreadScheduler.serverMetrics()[0];
        }
    }
}
