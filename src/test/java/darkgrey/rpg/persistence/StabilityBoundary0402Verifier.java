package darkgrey.rpg.persistence;

import java.io.File;
import java.lang.reflect.Field;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Map;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.EventPriority;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.diagnostics.ReadOnlyStateSource;
import darkgrey.rpg.live.LiveBridgeController;
import darkgrey.rpg.live.LivePickService;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageManagerPacket;
import darkgrey.rpg.project.packages.StoryPackageManagerService;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;

/** Real isolated Forge endpoints. Faults touch only a detached Bridge repository and read-only projections. */
public final class StabilityBoundary0402Verifier {

    private Object server;
    private WorldServer world;
    private MapStorage storage;
    private CanonicalSessionSavedData sessions;
    private NBTTagCompound initialSessions, initialTasks;
    private boolean initialized, diagnosticFault, packageFault, finished, bridgeRecorded;
    private int diagnosticReads, packageFailures, ticks;
    private JsonArray bridgeReplies;
    private Object previousManager, previousLoader;
    private boolean managerInstalled;

    @SubscribeEvent(priority = EventPriority.HIGHEST)
    public void beforeQueue(TickEvent.ServerTickEvent event) throws Exception {
        if (finished) return;
        if (event.phase == TickEvent.Phase.START) {
            if (initialized && diagnosticFault)
                ReadOnlyStateSource.observe(storage, CanonicalSessionSavedData.DATA_NAME, new FaultProjection());
            return;
        }
        if (!initialized) {
            File root = root();
            Class<?> type = Class.forName("net.minecraft.server.MinecraftServer");
            server = Stability0402Reflect.method(type, "getServer", "func_71276_C")
                .invoke(null);
            world = (WorldServer) Stability0402Reflect
                .method(type, "worldServerForDimension", "func_71218_a", int.class)
                .invoke(server, 0);
            storage = (MapStorage) Stability0402Reflect.read(world, "mapStorage", "field_72988_C");
            sessions = CanonicalSessionSavedData.get(storage);
            initialSessions = state(sessions);
            initialTasks = state(CanonicalTaskSavedData.get(storage));
            initialized = true;
            write("boundary-ready.json", "{\"status\":\"READY\",\"root_guard\":true}");
        }
        ticks++;
        File requestFile = new File(root(), "boundary-command.json");
        if (requestFile.isFile()) {
            JsonObject request = new JsonParser()
                .parse(new String(Files.readAllBytes(requestFile.toPath()), StandardCharsets.UTF_8))
                .getAsJsonObject();
            Files.delete(requestFile.toPath());
            String action = request.get("action")
                .getAsString();
            if (action.equals("bridge")) {
                ProjectRepository detached = new ProjectRepository(new File(root(), "DetachedBridge"));
                Field snapshot = ProjectRepository.class.getDeclaredField("snapshot");
                snapshot.setAccessible(true);
                snapshot.set(detached, null);
                LiveBridgeController controller = new LiveBridgeController(
                    detached,
                    new LivePickService(),
                    DarkGreyRpg.getStoryPackageLoader());
                bridgeReplies = new JsonArray();
                for (int i = 0; i < 32; i++) {
                    JsonObject message = new JsonObject();
                    message.addProperty("type", "state.request");
                    message.addProperty("request_id", "boundary-" + i);
                    controller.onMessage(message, bridgeReplies::add);
                }
            } else if (action.equals("diagnostic-on")) diagnosticFault = true;
            else if (action.equals("diagnostic-off")) diagnosticFault = false;
            else if (action.equals("package-install")) {
                require(!managerInstalled, "Single owned package manager injection");
                Field current = field(StoryPackageManagerPacket.Server.class, "current");
                Field manager = field(StoryPackageManagerPacket.Server.class, "manager");
                previousLoader = current.get(null);
                previousManager = manager.get(null);
                StoryPackageLoader loader = DarkGreyRpg.getStoryPackageLoader();
                manager
                    .set(null, new StoryPackageManagerService(loader, new StoryPackageManagerService.RuntimeAccess() {

                        public void reload() {
                            if (packageFault) {
                                packageFailures++;
                                throw new IllegalStateException("DGR0402_TEST_ONLY_PACKAGE_RELOAD_FAILURE");
                            }
                        }

                        public void retire(Map<String, LoadedStoryPackage> retained) {
                            throw new IllegalStateException("Deletion is outside this read-only endpoint fixture");
                        }

                        public int activeCount(Set<String> stories) {
                            return 0;
                        }
                    }));
                current.set(null, loader);
                managerInstalled = true;
            } else if (action.equals("package-fail")) packageFault = true;
            else if (action.equals("package-recover")) packageFault = false;
            else if (action.equals("finish")) {
                require(diagnosticReads > 0 && packageFailures > 0, "Both real request faults exercised");
                require(bridgeReplies != null && bridgeReplies.size() == 32, "All Bridge requests answered");
                require(
                    initialSessions.equals(state(sessions))
                        && initialTasks.equals(state(CanonicalTaskSavedData.get(storage))),
                    "Business NBT unchanged");
                diagnosticFault = false;
                packageFault = false;
                if (managerInstalled) {
                    field(StoryPackageManagerPacket.Server.class, "manager").set(null, previousManager);
                    field(StoryPackageManagerPacket.Server.class, "current").set(null, previousLoader);
                }
                JsonObject result = new JsonObject();
                result.addProperty("status", "PASS");
                result.addProperty("layer", "S; native GUI correlation is recorded separately");
                result.addProperty("bridge_replies", bridgeReplies.size());
                result.addProperty("diagnostic_fault_reads", diagnosticReads);
                result.addProperty("package_reload_faults", packageFailures);
                result.addProperty("business_nbt_unchanged", true);
                result.addProperty("production_classes_redefined", false);
                write("boundary-result.json", result.toString());
                finished = true;
                Stability0402Reflect.call(server, "initiateShutdown", "func_71263_m");
            } else throw new IllegalArgumentException("Unknown boundary action " + action);
        }
        if (ticks % 10 == 0) write("heartbeat.txt", ticks + " " + System.nanoTime());
    }

    @SubscribeEvent(priority = EventPriority.LOWEST)
    public void afterQueue(TickEvent.ServerTickEvent event) throws Exception {
        if (!initialized) return;
        if (event.phase == TickEvent.Phase.START) {
            ReadOnlyStateSource.observe(storage, CanonicalSessionSavedData.DATA_NAME, sessions);
            return;
        }
        if (!bridgeRecorded && bridgeReplies != null && bridgeReplies.size() == 32) {
            for (int i = 0; i < 32; i++) {
                JsonObject reply = bridgeReplies.get(i)
                    .getAsJsonObject();
                require(
                    reply.get("request_id")
                        .getAsString()
                        .equals("boundary-" + i)
                        && !reply.get("ok")
                            .getAsBoolean(),
                    "Correlated explicit Bridge failure");
            }
            write("bridge-correlation.json", bridgeReplies.toString());
            bridgeRecorded = true;
        }
    }

    private final class FaultProjection extends WorldSavedData {

        FaultProjection() {
            super("DGR0402_TEST_ONLY_READ_ONLY_PROJECTION");
        }

        public void readFromNBT(NBTTagCompound value) {
            throw new UnsupportedOperationException();
        }

        public void writeToNBT(NBTTagCompound value) {
            diagnosticReads++;
            throw new IllegalStateException("DGR0402_TEST_ONLY_DIAGNOSTIC_READ_FAILURE");
        }

        // The driver stays outside the reobfuscated Runtime JAR; formal Forge invokes these aliases.
        public void func_76184_a(NBTTagCompound value) {
            readFromNBT(value);
        }

        public void func_76187_b(NBTTagCompound value) {
            writeToNBT(value);
        }
    }

    private static NBTTagCompound state(Object data) throws Exception {
        NBTTagCompound output = new NBTTagCompound();
        Stability0402Reflect.method(data.getClass(), "writeToNBT", "func_76187_b", NBTTagCompound.class)
            .invoke(data, output);
        return output;
    }

    private static Field field(Class<?> type, String name) throws Exception {
        Field value = type.getDeclaredField(name);
        value.setAccessible(true);
        return value;
    }

    private static File root() throws Exception {
        File root = new File(System.getProperty("dgr0402.root")).getCanonicalFile();
        require(
            root.getPath()
                .contains("\\.tooling\\0402\\Worlds\\"),
            "Owned isolated world required");
        return root;
    }

    private static void write(String name, String value) throws Exception {
        Files.write(new File(root(), name).toPath(), value.getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean valid, String message) {
        if (!valid) throw new AssertionError(message);
    }
}
