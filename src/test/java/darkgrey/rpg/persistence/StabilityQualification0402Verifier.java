package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.media.CanonicalMediaServer;
import darkgrey.rpg.project.packages.StoryPackageGenerationLifecycle;
import darkgrey.rpg.project.packages.StoryPackageInventoryEntry;
import darkgrey.rpg.project.packages.StoryPackageRuntimeReloader;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceNbtCodec;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Actual loader/generation boundaries. Generated current-schema containers and genuine native players. */
public final class StabilityQualification0402Verifier {

    private static final String PLACEMENT = "node_ca2c21edb75241728a39c266b2259ddf";
    private Object server;
    private MapStorage storage;
    private EntityPlayerMP first, second;
    private final List<String> stories = new ArrayList<>();
    private final List<String> steps = new ArrayList<>();
    private NBTTagCompound unrelated;
    private CountDownLatch mediaRelease;
    private long previousRun;
    private int ticks;
    private boolean initialized, finished;

    @SubscribeEvent
    public void tick(TickEvent.ServerTickEvent event) throws Exception {
        if (event.phase != TickEvent.Phase.END || finished) return;
        if (!initialized) {
            Class<?> type = Class.forName("net.minecraft.server.MinecraftServer");
            server = Stability0402Reflect.method(type, "getServer", "func_71276_C")
                .invoke(null);
            Object world = Stability0402Reflect.method(type, "worldServerForDimension", "func_71218_a", int.class)
                .invoke(server, 0);
            storage = (MapStorage) Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
            for (File file : new File(root(), "Inputs-v1").listFiles()) if (file.getName()
                .endsWith(".dgrs"))
                stories.add(
                    file.getName()
                        .replace(".dgrs", ""));
            Collections.sort(stories);
            require(stories.size() == 3, "Three generated qualification containers");
            initialized = true;
            write("qualification-ready.json", "{\"status\":\"READY\"}");
        }
        ticks++;
        File command = new File(root(), "qualification-command.json");
        if (command.isFile()) {
            JsonObject request = new JsonParser()
                .parse(new String(Files.readAllBytes(command.toPath()), StandardCharsets.UTF_8))
                .getAsJsonObject();
            Files.delete(command.toPath());
            String action = request.get("action")
                .getAsString();
            String uid = stories.get(0);
            File source = owned("DarkGreyRPG/StoryPackages/" + uid + ".dgrs");
            if (action.equals("prime")) {
                Object configuration = Stability0402Reflect.call(server, "getConfigurationManager", "func_71203_ab");
                for (Object value : (List<?>) Stability0402Reflect
                    .read(configuration, "playerEntityList", "field_72404_b")) {
                    EntityPlayerMP player = (EntityPlayerMP) value;
                    String name = (String) Stability0402Reflect.call(player, "getCommandSenderName", "func_70005_c_");
                    if (name.equals("DGR402ClientF")) first = player;
                    if (name.equals("DGR402ClientG")) second = player;
                }
                require(first != null && second != null, "Two genuine native profiles connected");
                java.lang.reflect.Field field = CanonicalMediaServer.class.getDeclaredField("MEDIA_WORKER");
                field.setAccessible(true);
                ThreadPoolExecutor workers = (ThreadPoolExecutor) field.get(null);
                mediaRelease = new CountDownLatch(1);
                CountDownLatch entered = new CountDownLatch(2);
                for (int i = 0; i < 2; i++) workers.execute(() -> {
                    entered.countDown();
                    try {
                        require(mediaRelease.await(90, TimeUnit.SECONDS), "Finite controlled media hold");
                    } catch (InterruptedException failure) {
                        Thread.currentThread()
                            .interrupt();
                        throw new AssertionError(failure);
                    }
                });
                require(entered.await(2, TimeUnit.SECONDS), "Real workers held");
                start(first, 0);
                start(first, 1);
                start(second, 2);
                previousRun = story(first, 0).getActivationTime();
            } else if (action.equals("unready-task")) {
                CanonicalTaskInstanceSnapshot task = DarkGreyRpg.getCanonicalTaskManager()
                    .snapshot(first, uid, PLACEMENT);
                require(
                    task != null && CanonicalMediaServer.getMediaInFlightCount() > 0 && mediaRelease.getCount() == 1,
                    "Native Choice reached Task while media remained held/in-flight");
                write(
                    "unready-task-result.json",
                    "{\"status\":\"PASS\",\"task_run\":" + task.getActivationTime()
                        + ",\"in_flight\":"
                        + CanonicalMediaServer.getMediaInFlightCount()
                        + "}");
                unrelated = unrelated();
            } else if (action.equals("release")) {
                mediaRelease.countDown();
            } else if (action.equals("disable") || action.equals("enable")) {
                DarkGreyRpg.getStoryPackageLoader()
                    .setEnabled(
                        source.getName(),
                        action.equals("enable"),
                        DarkGreyRpg.getStoryPackageLoader()
                            .getInventoryRevision());
                reload();
                require(
                    story(first, 0).getActivationTime() == previousRun && DarkGreyRpg.getCanonicalTaskManager()
                        .snapshot(first, uid, PLACEMENT) != null,
                    "Disabled retains the existing Story and Task");
                if (action.equals("disable")) require(
                    !DarkGreyRpg.getCanonicalStoryManager()
                        .startByEntry(second, uid) && story(second, 0) == null,
                    "Disabled rejects a new native player run");
                else start(second, 0);
                preserved();
            } else if (action.equals("error")) {
                Files.write(source.toPath(), "DGR0402_TEST_ONLY_INVALID_PACKAGE".getBytes(StandardCharsets.UTF_8));
                reload();
                state(source.getName(), StoryPackageInventoryEntry.State.ERROR);
                retired();
            } else if (action.equals("restore")) {
                Files.copy(
                    owned("Inputs-v1/" + source.getName()).toPath(),
                    source.toPath(),
                    StandardCopyOption.REPLACE_EXISTING);
                reload();
                restartRetired();
                previousRun = story(first, 0).getActivationTime();
                preserved();
            } else if (action.equals("conflict")) {
                File duplicate = owned("DarkGreyRPG/StoryPackages/Duplicate.dgrs");
                Files.copy(source.toPath(), duplicate.toPath());
                reload();
                state(source.getName(), StoryPackageInventoryEntry.State.CONFLICT);
                state(duplicate.getName(), StoryPackageInventoryEntry.State.CONFLICT);
                retired();
            } else if (action.equals("resolve-conflict")) {
                Files.move(
                    owned("DarkGreyRPG/StoryPackages/Duplicate.dgrs").toPath(),
                    owned("Backups/Duplicate.dgrs").toPath());
                reload();
                restartRetired();
                previousRun = story(first, 0).getActivationTime();
                preserved();
            } else if (action.equals("unload")) {
                Files.move(source.toPath(), owned("Backups/Unloaded.dgrs").toPath());
                reload();
                retired();
            } else if (action.equals("change")) {
                Files.copy(
                    owned("Inputs-v2/" + source.getName()).toPath(),
                    source.toPath(),
                    StandardCopyOption.REPLACE_EXISTING);
                reload();
                retired();
                restartRetired();
                require(
                    story(first, 0).getActivationTime() != previousRun,
                    "New content starts a fresh generation run");
                preserved();
            } else if (action.equals("no-content")) {
                long run = story(first, 0).getActivationTime();
                reload();
                require(story(first, 0).getActivationTime() == run, "No-content reload retains run");
                preserved();
            } else if (action.equals("finish")) {
                require(
                    steps.contains("error") && steps.contains("conflict")
                        && steps.contains("unload")
                        && steps.contains("change")
                        && steps.contains("unready-task"),
                    "Full controlled sequence completed");
                preserved();
                Stability0402Reflect.call(storage, "saveAllData", "func_75744_a");
                write(
                    "qualification-result.json",
                    "{\"status\":\"PASS\",\"native_players\":2,\"generated_not_studio_export\":true,\"retired_history_blocks_automatic_restart\":true,\"explicit_admin_reset_for_new_run\":true,\"steps\":"
                        + new com.google.gson.Gson().toJson(steps)
                        + "}");
                finished = true;
                Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
            } else throw new IllegalArgumentException("Unknown qualification action " + action);
            steps.add(action);
            write(
                "qualification-state.json",
                "{\"steps\":" + new com.google.gson.Gson().toJson(steps) + ",\"tick\":" + ticks + "}");
        }
        if (ticks % 10 == 0) write("heartbeat.txt", ticks + " " + System.nanoTime());
    }

    private void start(EntityPlayerMP player, int index) {
        require(
            DarkGreyRpg.getCanonicalStoryManager()
                .startByEntry(player, stories.get(index)),
            "True current Story Start");
    }

    private void restartRetired() {
        require(
            !DarkGreyRpg.getCanonicalStoryManager()
                .startByEntry(first, stories.get(0)) && story(first, 0) == null,
            "Retained interrupted history cannot silently become a new run");
        require(
            DarkGreyRpg.getCanonicalStoryManager()
                .reset(first, stories.get(0)),
            "Explicit owned administrator reset");
        start(first, 0);
    }

    private CanonicalStoryInstanceSnapshot story(EntityPlayerMP player, int index) {
        return DarkGreyRpg.getCanonicalStoryManager()
            .snapshot(player, stories.get(index));
    }

    private void reload() {
        StoryPackageRuntimeReloader.Result result = StoryPackageRuntimeReloader
            .reload(DarkGreyRpg.getProjectRepository(), DarkGreyRpg.getStoryPackageLoader());
        require(result.isPackageSetCommitted(), "Committed actual accepted package set");
        StoryPackageGenerationLifecycle.reconcile(
            storage,
            DarkGreyRpg.getStoryPackageLoader()
                .getPackages());
    }

    private void state(String name, StoryPackageInventoryEntry.State expected) {
        for (StoryPackageInventoryEntry entry : DarkGreyRpg.getStoryPackageLoader()
            .getInventory())
            if (entry.getSourceName()
                .equals(name)) {
                    require(entry.getState() == expected, "Actual inventory state " + expected);
                    return;
                }
        throw new AssertionError("Inventory entry missing " + name);
    }

    private void retired() {
        require(
            story(first, 0) == null && story(second, 0) == null
                && DarkGreyRpg.getCanonicalTaskManager()
                    .snapshot(first, stories.get(0), PLACEMENT) == null,
            "Affected runtime retired without old content fallback");
        preserved();
    }

    private void preserved() {
        require(unrelated.equals(unrelated()), "Other native player/Story/Session NBT unchanged");
    }

    private NBTTagCompound unrelated() {
        List<CanonicalStoryInstanceSnapshot> rows = new ArrayList<>();
        rows.add(story(first, 1));
        rows.add(story(second, 2));
        NBTTagCompound storiesNbt = CanonicalStoryInstanceNbtCodec.encode(rows);
        List<CanonicalSessionInstanceSnapshot> sessions = new ArrayList<>();
        for (EntityPlayerMP player : new EntityPlayerMP[] { first, second }) {
            UUID id = (UUID) call(player, "getUniqueID", "func_110124_au");
            CanonicalSessionInstanceSnapshot session = CanonicalSessionSavedData.get(storage)
                .getSnapshot(id, this.stories.get(player == first ? 1 : 2));
            require(session != null, "Unrelated native Session remains");
            sessions.add(session);
        }
        NBTTagCompound sessionsNbt = CanonicalSessionInstanceNbtCodec.encode(sessions);
        try {
            Stability0402Reflect
                .method(
                    NBTTagCompound.class,
                    "setTag",
                    "func_74782_a",
                    String.class,
                    Class.forName("net.minecraft.nbt.NBTBase"))
                .invoke(storiesNbt, "test_sessions", sessionsNbt);
        } catch (Exception failure) {
            throw new IllegalStateException(failure);
        }
        return storiesNbt;
    }

    private static Object call(Object value, String mcp, String srg) {
        try {
            return Stability0402Reflect.call(value, mcp, srg);
        } catch (Exception failure) {
            throw new IllegalStateException(failure);
        }
    }

    private static File root() throws Exception {
        File root = new File(System.getProperty("dgr0402.root")).getCanonicalFile();
        require(
            root.getPath()
                .contains("\\.tooling\\0402\\Worlds\\"),
            "Owned test world required");
        return root;
    }

    private static File owned(String path) throws Exception {
        File file = new File(root(), path).getCanonicalFile();
        require(
            file.getPath()
                .startsWith(root().getPath() + File.separator),
            "Fixture path remains inside owned root");
        return file;
    }

    private static void write(String name, String value) throws Exception {
        Files.write(owned(name).toPath(), value.getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
