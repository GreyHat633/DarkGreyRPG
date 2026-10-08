package darkgrey.rpg.session.persistence;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.persistence.StorageProbeSupport;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerServiceProbe;

/** Real MapStorage failure cache, forced-dirty preservation and current pending/restart behavior. */
public final class SessionStorage0401Probe {

    private static final String STORY = "ST-2345-6789-ABCD-EFGH";
    private static final UUID PLAYER = new UUID(0, 1);
    private static final UUID OTHER = new UUID(0, 2);
    private static final ProjectSnapshot PROJECT = CanonicalStoryServerServiceProbe.project();

    private SessionStorage0401Probe() {}

    public static void main(String[] args) throws Exception {
        Path root = StorageProbeSupport.root("sessions-");
        Path healthy = root.resolve("healthy");
        MapStorage storage = StorageProbeSupport.storage(healthy);
        CanonicalSessionSavedData fresh = CanonicalSessionSavedData.get(storage);
        CanonicalStoryServerService stories = new CanonicalStoryServerService(PROJECT, fresh);
        stories.startByEntry(PLAYER, STORY, 100L);
        stories.startByEntry(OTHER, STORY, 101L);
        CanonicalSessionServerService service = new CanonicalSessionServerService(PROJECT, fresh);
        darkgrey.rpg.session.server.CanonicalSessionDispatch first = service.start(PLAYER, STORY, "session_place");
        service.start(OTHER, STORY, "session_place");
        fresh.rememberLineContext(fresh.getSnapshot(PLAYER, STORY), first.getFrame());
        storage.saveAllData();
        NBTTagCompound valid = state(fresh);
        CanonicalSessionSavedData pending = CanonicalSessionSavedData.get(StorageProbeSupport.storage(healthy));
        require(
            !pending.isBound() && pending.hasPendingData() && valid.equals(state(pending)),
            "healthy pending exact preservation");
        pending.markDirty();
        MapStorage pendingStorage = StorageProbeSupport.storage(root.resolve("pending"));
        pendingStorage.setData(CanonicalSessionSavedData.DATA_NAME, pending);
        pendingStorage.saveAllData();
        new CanonicalStoryServerService(PROJECT, pending);
        new CanonicalSessionServerService(PROJECT, pending);
        require(
            pending.size() == 2 && pending.getSnapshot(PLAYER, STORY)
                .getTransportId()
                != pending.getSnapshot(OTHER, STORY)
                    .getTransportId(),
            "two players restart without transport crossover");
        require(
            pending.storySnapshots()
                .size() == 2 && pending.lineContext(pending.getSnapshot(PLAYER, STORY)) != null,
            "two current Story cursors and silent context restart");
        Map<String, NBTTagCompound> invalid = new LinkedHashMap<String, NBTTagCompound>();
        NBTTagCompound schema = copy(valid);
        schema.setInteger("schema_version", -1);
        invalid.put("schema", schema);
        NBTTagCompound type = copy(valid);
        type.setString("sessions", "wrong-type");
        invalid.put("field-type", type);
        NBTTagCompound missing = copy(valid);
        missing.removeTag("stories");
        invalid.put("missing-root", missing);
        NBTTagCompound session = copy(valid);
        session.getCompoundTag("sessions")
            .getTagList("instances", 10)
            .getCompoundTagAt(0)
            .setString("player_uuid", "bad");
        invalid.put("instance", session);
        NBTTagCompound context = copy(valid);
        NBTTagCompound contexts = new NBTTagCompound();
        contexts.setByteArray("1", new byte[] { 1, 2, 3 });
        context.setTag("line_contexts", contexts);
        invalid.put("line-context", context);
        NBTTagCompound mismatched = copy(valid);
        byte[] lineBytes = mismatched.getCompoundTag("line_contexts")
            .getByteArray("1");
        mismatched.getCompoundTag("line_contexts")
            .removeTag("1");
        mismatched.getCompoundTag("line_contexts")
            .setByteArray("2", lineBytes);
        invalid.put("context-transport", mismatched);
        NBTTagCompound foreignSession = copy(valid);
        foreignSession.getCompoundTag("sessions")
            .getTagList("instances", 10)
            .getCompoundTagAt(0)
            .setString("story_id", "ST-JKLM-NPQR-STUV-WXYZ");
        invalid.put("context-story", foreignSession);
        NBTTagCompound story = copy(valid);
        story.getCompoundTag("stories")
            .setTag("instances", new NBTTagList());
        story.getCompoundTag("stories")
            .setString("schema_version", "bad");
        invalid.put("story-instance", story);
        for (Map.Entry<String, NBTTagCompound> vector : invalid.entrySet()) {
            Path directory = root.resolve(vector.getKey());
            StorageProbeSupport.storage(directory);
            StorageProbeSupport.write(file(directory), vector.getValue());
            protect(directory, vector.getKey());
        }
        Path gzip = root.resolve("gzip");
        StorageProbeSupport.storage(gzip);
        Files.write(file(gzip), new byte[] { 1, 2, 3, 4 });
        protect(gzip, "gzip-before-reader");
        CanonicalSessionSavedData awaiting = new CanonicalSessionSavedData("awaiting");
        reject(awaiting::requireReadable);
        reject(() -> awaiting.writeToNBT(new NBTTagCompound()));
        reject(awaiting::markDirty);
        Field storeField = CanonicalSessionSavedData.class.getDeclaredField("store");
        storeField.setAccessible(true);
        Object previousStore = storeField.get(pending);
        NBTTagCompound previousContexts = copy(valid);
        try {
            pending.readFromNBT(context);
            throw new AssertionError("bad reread accepted");
        } catch (IllegalArgumentException expected) {}
        require(
            storeField.get(pending) == previousStore && previousContexts.equals(valid),
            "failed reread leaves live memory and input intact");
        reject(() -> pending.writeToNBT(new NBTTagCompound()));
        reject(
            () -> pending.bind(
                id -> PROJECT.getCanonicalSessions()
                    .get(id)));
        pending.readFromNBT(valid);
        new CanonicalSessionServerService(PROJECT, pending);
        require(pending.size() == 2, "explicit known-valid restore in isolated test");
        System.out.println(
            "SESSION_STORAGE_0401=PASS vectors=" + (invalid.size() + 1)
                + " pending new-world two-players reread forced-dirty sentinel no-empty-success");
        System.out.println("SESSION_FIXTURES=" + root);
    }

    private static void protect(Path directory, String name) throws Exception {
        String before = StorageProbeSupport.hash(file(directory));
        MapStorage storage = StorageProbeSupport.storage(directory);
        reject(() -> CanonicalSessionSavedData.get(storage));
        reject(() -> CanonicalSessionSavedData.get(storage));
        reject(
            () -> darkgrey.rpg.project.packages.StoryPackageGenerationLifecycle
                .reconcile(storage, java.util.Collections.emptyMap()));
        require(
            storage.loadData(
                darkgrey.rpg.project.packages.StoryPackageGenerationSavedData.class,
                darkgrey.rpg.project.packages.StoryPackageGenerationSavedData.DATA_NAME) == null,
            "quarantine precedes creation of adjacent generation registry");
        CanonicalSessionSavedData failed = (CanonicalSessionSavedData) storage
            .loadData(CanonicalSessionSavedData.class, CanonicalSessionSavedData.DATA_NAME);
        require(failed != null, "real MapStorage caches failed reflected object");
        reject(failed::size);
        reject(failed::snapshots);
        reject(failed::storySnapshots);
        reject(failed::getPendingRaw);
        reject(failed::isBound);
        reject(() -> failed.getStorySnapshot(PLAYER, STORY));
        reject(() -> failed.completedStorySummary(PLAYER, STORY));
        reject(() -> failed.claimStoryTerminalRoute(PLAYER, STORY, 1, "done"));
        reject(() -> failed.markStoryTerminalRouteApplied(PLAYER, STORY, 1, "done"));
        reject(() -> failed.releaseStoryTerminalRoute(PLAYER, STORY, 1, "done"));
        reject(
            () -> failed.bind(
                id -> PROJECT.getCanonicalSessions()
                    .get(id)));
        reject(
            () -> failed.bindAvailable(
                id -> PROJECT.getCanonicalSessions()
                    .get(id),
                id -> PROJECT.getCanonicalStories()
                    .get(id)));
        reject(() -> new CanonicalSessionServerService(PROJECT, failed).start(PLAYER, STORY, "session_place"));
        reject(() -> new CanonicalSessionServerService(PROJECT, failed).resume(PLAYER, STORY));
        reject(() -> failed.acceptAndConsume(null, null));
        reject(failed::markDirty);
        NBTTagCompound sentinel = new NBTTagCompound();
        sentinel.setString("sentinel", "preserved");
        NBTTagCompound unchanged = copy(sentinel);
        reject(() -> failed.writeToNBT(sentinel));
        require(unchanged.equals(sentinel), "rejected writer leaves output unchanged");
        storage.saveAllData();
        StorageProbeSupport.forceDirty(failed);
        storage.saveAllData();
        require(before.equals(StorageProbeSupport.hash(file(directory))), "forced dirty must preserve original file");
        require(
            storage.loadData(CanonicalSessionSavedData.class, CanonicalSessionSavedData.DATA_NAME) == failed,
            "failed instance is not replaced");
        require(
            !Files.exists(
                directory
                    .resolve(darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory.DATA_NAME + ".dat")),
            "failure does not create dependent history");
        System.out.println(
            "SESSION_QUARANTINE vector=" + name
                + " before_sha256="
                + before
                + " after_sha256="
                + StorageProbeSupport.hash(file(directory)));
    }

    private static Path file(Path directory) {
        return directory.resolve(CanonicalSessionSavedData.DATA_NAME + ".dat");
    }

    private static NBTTagCompound state(CanonicalSessionSavedData data) {
        NBTTagCompound value = new NBTTagCompound();
        data.writeToNBT(value);
        return value;
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return (NBTTagCompound) value.copy();
    }

    private static void reject(Runnable operation) {
        try {
            operation.run();
        } catch (CanonicalSessionDataUnavailableException expected) {
            return;
        }
        throw new AssertionError("Expected file-level data unavailability");
    }

    private static void require(boolean condition, String message) {
        StorageProbeSupport.require(condition, message);
    }
}
