package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardOpenOption;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

/** Read-only observation with an explicit isolated packet fixture. Desktop actions use native Win32 input. */
public final class StabilityClient0402Observer {

    private int ticks;
    private boolean failed;
    private int blockedPublications;
    private final StabilityClient0402PacketFixture packets = new StabilityClient0402PacketFixture();

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END || System.getProperty("dgr0402.clientRoot") == null || failed) return;
        try {
            File root = new File(System.getProperty("dgr0402.clientRoot")).getCanonicalFile();
            if (!root.equals(new File(".").getCanonicalFile()) || !root.getPath()
                .contains(File.separator + "0402" + File.separator + "Worlds" + File.separator))
                throw new IllegalStateException("Isolated client root required");
            Class<?> type = Class.forName("net.minecraft.client.Minecraft");
            Object mc = Stability0402Reflect.method(type, "getMinecraft", "func_71410_x")
                .invoke(null);
            Object world = Stability0402Reflect.read(mc, "theWorld", "field_71441_e");
            Object gui = Stability0402Reflect.read(mc, "currentScreen", "field_71462_r");
            Object connection = Stability0402Reflect.call(mc, "getNetHandler", "func_147114_u");
            if (!Boolean.getBoolean("dgr0402.baseline")) packets.tick(root, world, connection);
            boolean active = org.lwjgl.opengl.Display.isActive();
            Stability0402ClientStats.enabled = active && world != null
                && (gui == null || gui.getClass()
                    .getName()
                    .startsWith("darkgrey.rpg.client.gui."));
            if (++ticks % 10 != 0) return;
            String interactions = Stability0402InteractionStats.drain();
            if (!interactions.isEmpty()) Files.write(
                new File(root, "native-client-interactions.csv").toPath(),
                interactions.getBytes(StandardCharsets.UTF_8),
                StandardOpenOption.CREATE,
                StandardOpenOption.APPEND);
            long[] queue = Boolean.getBoolean("dgr0402.baseline") ? new long[10]
                : darkgrey.rpg.network.MainThreadScheduler.clientMetrics();
            String state = "{\"status\":\"OBSERVED\",\"tick\":" + ticks
                + ",\"world_loaded\":"
                + (world != null)
                + ",\"world_identity\":"
                + System.identityHashCode(world)
                + ",\"connection_identity\":"
                + System.identityHashCode(connection)
                + ",\"gui\":\""
                + (gui == null ? ""
                    : gui.getClass()
                        .getSimpleName())
                + "\",\"active\":"
                + active
                + ",\"queue_pending\":"
                + queue[0]
                + ",\"queue_accepted\":"
                + queue[2]
                + ",\"queue_completed\":"
                + queue[3]
                + ",\"queue_cancelled\":"
                + queue[5]
                + ",\"queue_failed\":"
                + queue[6]
                + ",\"frame_overflow\":"
                + Stability0402ClientStats.overflow()
                + (Boolean.getBoolean("dgr0402.baseline") ? ""
                    : ",\"session_awaiting_server\":"
                        + darkgrey.rpg.client.session.CanonicalSessionClientController.presentationModel()
                            .awaitingServer())
                + playerState(mc)
                + targetState(mc)
                + guiState(gui)
                + mediaState()
                + "}";
            java.nio.file.Path temporary = new File(root, "native-client-state.tmp").toPath();
            Files.write(temporary, state.getBytes(StandardCharsets.UTF_8));
            try {
                Files.move(
                    temporary,
                    new File(root, "native-client-state.json").toPath(),
                    java.nio.file.StandardCopyOption.ATOMIC_MOVE,
                    java.nio.file.StandardCopyOption.REPLACE_EXISTING);
                blockedPublications = 0;
            } catch (java.nio.file.FileSystemException sharing) {
                Files.deleteIfExists(temporary);
                if (++blockedPublications >= 20) throw sharing;
                // Native evidence readers can briefly hold the previous complete snapshot open on Windows.
            }
            StringBuilder frames = new StringBuilder();
            long now = System.nanoTime();
            for (long interval : Stability0402ClientStats.drain()) frames.append(ticks)
                .append(',')
                .append(now)
                .append(',')
                .append(System.identityHashCode(world))
                .append(',')
                .append(interval)
                .append('\n');
            if (frames.length() > 0) Files.write(
                new File(root, "native-client-frames.csv").toPath(),
                frames.toString()
                    .getBytes(StandardCharsets.UTF_8),
                StandardOpenOption.CREATE,
                StandardOpenOption.APPEND);
        } catch (Exception failure) {
            failed = true;
            if (failure.getCause() instanceof Error) throw (Error) failure.getCause();
            failure.printStackTrace();
            try {
                Files.write(
                    new File(System.getProperty("dgr0402.clientRoot"), "native-client-FAIL.txt").toPath(),
                    failure.toString()
                        .getBytes(StandardCharsets.UTF_8));
            } catch (Exception output) {
                output.printStackTrace();
            }
        }
    }

    private static String targetState(Object mc) throws Exception {
        Object ray = Stability0402Reflect.read(mc, "objectMouseOver", "field_71476_x");
        Object entity = ray == null ? null : Stability0402Reflect.read(ray, "entityHit", "field_72308_g");
        return entity == null ? ""
            : ",\"target_entity_uuid\":\"" + Stability0402Reflect.call(entity, "getUniqueID", "func_110124_au")
                + "\",\"target_entity_id\":"
                + Stability0402Reflect.call(entity, "getEntityId", "func_145782_y")
                + ",\"target_entity_class\":\""
                + entity.getClass()
                    .getName()
                + "\"";
    }

    private static String mediaState() throws Exception {
        Class<?> media = Class.forName("darkgrey.rpg.media.CanonicalMediaClient");
        Object cache = staticField(media, "cache");
        int pins = 0;
        if (cache != null)
            for (Object value : ((java.util.Map<?, ?>) Stability0402Reflect.read(cache, "pins", "pins")).values())
                pins += Math.max(0, ((java.util.concurrent.atomic.AtomicInteger) value).get());
        Class<?> textures = Class.forName("darkgrey.rpg.media.CanonicalMediaTextures");
        long pixels = 0;
        for (Object value : ((java.util.Map<?, ?>) staticField(textures, "PIXELS")).values())
            pixels += ((Long) value).longValue();
        Object packages = staticField(media, "packages");
        int runningBundles = 0, readyBundles = 0;
        if (packages != null)
            for (darkgrey.rpg.media.StoryMediaCacheIndex.Entry entry : ((darkgrey.rpg.media.StoryMediaCacheIndex) packages)
                .entries()) {
                    if (entry.isRunning()) runningBundles++;
                    if (entry.isReady()) readyBundles++;
                }
        return ",\"media_running_bundles\":" + runningBundles
            + ",\"media_ready_bundles\":"
            + readyBundles
            + ",\"media_active\":"
            + ((java.util.Map<?, ?>) staticField(media, "ACTIVE")).size()
            + ",\"media_retired\":"
            + ((java.util.Collection<?>) staticField(media, "RETIRED")).size()
            + ",\"media_pins\":"
            + pins
            + ",\"media_bundles\":"
            + darkgrey.rpg.media.CanonicalMediaClient.getCachedPackageCount()
            + ",\"media_downloading_bundles\":"
            + darkgrey.rpg.media.CanonicalMediaClient.getPreloadingPackageCount()
            + ",\"media_queued_bundles\":"
            + darkgrey.rpg.media.CanonicalMediaClient.getQueuedPackageCount()
            + ",\"media_network_requests\":"
            + darkgrey.rpg.media.CanonicalMediaClient.getNetworkRequestCount()
            + ",\"media_cache_hits\":"
            + darkgrey.rpg.media.CanonicalMediaClient.getCacheHitCount()
            + ",\"texture_count\":"
            + ((java.util.Map<?, ?>) staticField(textures, "READY")).size()
            + ",\"texture_pending\":"
            + ((java.util.Collection<?>) staticField(textures, "PENDING")).size()
            + ",\"texture_uploads\":"
            + ((java.util.Collection<?>) staticField(textures, "UPLOADS")).size()
            + ",\"texture_pixels\":"
            + pixels;
    }

    private static String playerState(Object mc) throws Exception {
        Object player = Stability0402Reflect.read(mc, "thePlayer", "field_71439_g");
        if (player == null) return "";
        com.google.gson.JsonObject state = new com.google.gson.JsonObject();
        String[][] fields = { { "x", "posX", "field_70165_t" }, { "y", "posY", "field_70163_u" },
            { "z", "posZ", "field_70161_v" }, { "yaw", "rotationYaw", "field_70177_z" },
            { "pitch", "rotationPitch", "field_70125_A" } };
        for (String[] field : fields)
            state.addProperty(field[0], (Number) Stability0402Reflect.read(player, field[1], field[2]));
        return ",\"player_transform\":" + state;
    }

    private static String guiState(Object gui) throws Exception {
        if (gui == null) return "";
        String kind = gui.getClass()
            .getSimpleName();
        if (kind.equals("GuiCanonicalSessionScreen")) {
            Object frame = Stability0402Reflect.read(gui, "frame", "frame");
            return ",\"session_frame_kind\":"
                + new com.google.gson.Gson().toJson(
                    Stability0402Reflect.call(frame, "getKind", "getKind")
                        .toString())
                + ",\"session_choices\":"
                + new com.google.gson.Gson().toJson(Stability0402Reflect.call(frame, "getChoices", "getChoices"))
                + ",\"session_node\":"
                + new com.google.gson.Gson()
                    .toJson(Stability0402Reflect.call(frame, "getCurrentNodeId", "getCurrentNodeId"));
        }
        if (!kind.equals("GuiPlayerStateInspection") && !kind.equals("GuiStoryPackageManager")) return "";
        Object loading = Stability0402Reflect.read(gui, "loading", "loading");
        Object request = Stability0402Reflect.read(gui, "request", "request");
        Object message = Stability0402Reflect.read(gui, "message", "message");
        return ",\"gui_loading\":" + loading
            + ",\"gui_request\":"
            + request
            + ",\"gui_message\":"
            + new com.google.gson.Gson().toJson(message);
    }

    private static Object staticField(Class<?> type, String name) throws Exception {
        java.lang.reflect.Field field = type.getDeclaredField(name);
        field.setAccessible(true);
        return field.get(null);
    }
}
