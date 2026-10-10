package darkgrey.rpg.task.instance;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Random;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicInteger;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.canonical.forge.Stability0402Fixtures;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Property isolation and independent Task index/state ledger, separate from multiplayer testing. */
public final class TaskHotPath0402Probe {

    private TaskHotPath0402Probe() {}

    public static void main(String[] args) throws Exception {
        properties();
        ProjectSnapshot project = Stability0402Fixtures.load(2, 20);
        for (int seed = 40201; seed <= 40205; seed++) model(seed, project);
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.bind(project::getCanonicalTask);
        UUID user = new UUID(40201, 1);
        data.start(user, Stability0402Fixtures.loadStory(0), "work", task(project, 0), 1L);
        NBTTagCompound before = image(data);
        data.setDirty(false);
        require(
            data.dispatch(user, CanonicalTaskEvent.killEntity("unrelated"), 2L)
                .getChangedCount() == 0,
            "Unrelated event is a no-op");
        require(before.equals(image(data)) && !data.isDirty(), "No-op dirty and serialization");
        require(
            data.dispatch(user, kill(), 2L)
                .getChangedCount() == 1 && data.isDirty(),
            "Progress marks dirty");
        require(!before.equals(image(data)), "Progress persists");
        java.lang.reflect.Field storeField = CanonicalTaskSavedData.class.getDeclaredField("store");
        storeField.setAccessible(true);
        CanonicalTaskInstanceStore owned = (CanonicalTaskInstanceStore) storeField.get(data);
        Object unrelated = owned.get(user, Stability0402Fixtures.loadStory(0), "work");
        try (
            darkgrey.rpg.persistence.Scoped0402TraversalGuard ignored = darkgrey.rpg.persistence.Scoped0402TraversalGuard
                .install(owned)) {
            UUID other = new UUID(40201, 2);
            data.setDirty(false);
            data.start(other, Stability0402Fixtures.loadStory(1), "work", task(project, 1), 3L);
            require(data.isDirty(), "New instance marks dirty without a world traversal");
            data.setDirty(false);
            data.start(other, Stability0402Fixtures.loadStory(1), "work", task(project, 1), 4L);
            require(!data.isDirty(), "Repeated start is a scoped no-op");
            require(
                owned.get(user, Stability0402Fixtures.loadStory(0), "work") == unrelated,
                "Unrelated instance was not restored");
        }
        System.out.println(
            "TASK_HOT_PATH_0402=PASS property isolation exact JSON spelling, no-op dirty, index ledger seeds=5 steps=10000 layer=A");
    }

    private static void properties() {
        JsonObject source = new JsonParser().parse("{\"array\":[null,{\"value\":\"text\"}],\"number\":1e+09}")
            .getAsJsonObject();
        AtomicInteger mutable = new AtomicInteger(7);
        Map<String, JsonElement> input = new LinkedHashMap<>();
        input.put("nested", source);
        input.put("mutableNumber", new JsonPrimitive(mutable));
        CanonicalGraphNode node = new CanonicalGraphNode("copy", "objective", "copy", Collections.emptyList(), input);
        String expected = node.getProperties()
            .toString();
        require(expected.contains("1e+09"), "Numeric token spelling");
        mutable.set(99);
        source.getAsJsonArray("array")
            .add(new JsonPrimitive("changed"));
        Map<String, JsonElement> first = node.getProperties();
        first.get("nested")
            .getAsJsonObject()
            .getAsJsonArray("array")
            .get(1)
            .getAsJsonObject()
            .addProperty("value", "changed");
        require(
            expected.equals(
                node.getProperties()
                    .toString()),
            "Constructor and getter deep isolation");
        try {
            first.put("bad", new JsonPrimitive(true));
            throw new AssertionError("Mutable property map");
        } catch (UnsupportedOperationException expectedFailure) {}
    }

    private static void model(int seed, ProjectSnapshot project) {
        Random random = new Random(seed);
        CanonicalTaskInstanceStore store = new CanonicalTaskInstanceStore();
        Map<String, Expected> ledger = new LinkedHashMap<>();
        for (int step = 0; step < 2000; step++) {
            UUID player = new UUID(seed, random.nextInt(4));
            int storyIndex = random.nextInt(2);
            String story = Stability0402Fixtures.loadStory(storyIndex), placement = "work" + random.nextInt(3);
            String key = key(player, story, placement);
            int operation = random.nextInt(8);
            if (operation == 0) {
                store.start(player, story, placement, task(project, storyIndex), step + 1L);
                ledger.putIfAbsent(key, new Expected(player, story, placement, step + 1L));
            } else if (operation == 1 && ledger.containsKey(key)) {
                Expected expected = ledger.get(key);
                require(
                    store.acceptEvent(player, story, placement, kill(), 20001L) == expected.active,
                    "Event acceptance matches independent lifecycle");
                if (expected.active) expected.progress++;
                require(
                    expected.active == store.get(player, story, placement)
                        .isActive(),
                    "Active event ledger");
            } else if (operation == 2 && ledger.containsKey(key)) {
                CanonicalTaskInstance old = store.get(player, story, placement);
                store.replaceExisting(CanonicalTaskInstance.restore(old.snapshot(), task(project, storyIndex)));
            } else if (operation == 3) {
                store.discardByPlayerStory(player, story);
                ledger.values()
                    .removeIf(e -> e.player.equals(player) && e.story.equals(story));
            } else if (operation == 4) {
                store.discardByStoryIds(Collections.singleton(story));
                ledger.values()
                    .removeIf(e -> e.story.equals(story));
            } else if (operation == 5) {
                store.cancelByStory(player, story);
                for (Expected expected : ledger.values())
                    if (expected.player.equals(player) && expected.story.equals(story)) expected.active = false;
            } else if (operation == 6) {
                NBTTagCompound payload = store.writeToNbt();
                CanonicalTaskInstanceStore next = new CanonicalTaskInstanceStore();
                next.readFromNbt(payload, project::getCanonicalTask);
                require(payload.equals(next.writeToNbt()), "Restore encoding seed=" + seed + " step=" + step);
                store = next;
            } else if (operation == 7) {
                NBTTagCompound before = store.writeToNbt();
                try {
                    store.readFromNbt(new NBTTagCompound(), project::getCanonicalTask);
                    throw new AssertionError("Bad data accepted");
                } catch (IllegalArgumentException expected) {}
                require(before.equals(store.writeToNbt()), "Failed restore changes no state");
            }
            require(store.size() == ledger.size(), "Global identity count seed=" + seed + " step=" + step);
            NBTTagCompound beforeQuery = store.writeToNbt();
            for (int u = 0; u < 4; u++) {
                UUID owner = new UUID(seed, u);
                int count = 0;
                String last = "";
                for (Expected expected : ledger.values()) if (expected.player.equals(owner)) count++;
                require(
                    store.snapshots(owner)
                        .size() == count,
                    "Player index count");
                for (CanonicalTaskInstanceSnapshot snapshot : store.snapshots(owner)) {
                    String actualKey = key(
                        snapshot.getPlayerUuid(),
                        snapshot.getStoryInstanceId(),
                        snapshot.getTaskNodePlacementId());
                    require(actualKey.compareTo(last) > 0, "Detached order");
                    last = actualKey;
                    Expected expected = ledger.get(actualKey);
                    require(
                        expected != null && expected.player.equals(owner)
                            && snapshot.getActivationTime() == expected.run,
                        "Player and run identity");
                    require(
                        (snapshot.getStatus() == CanonicalTaskInstanceStatus.ACTIVE) == expected.active,
                        "Lifecycle ledger");
                    for (int n = 0; n < 20; n++) require(
                        snapshot.getRuntimeSnapshot()
                            .getProgress()
                            .get("objective" + n) == (n % 10 == 0 ? expected.progress : 0),
                        "Independent objective ledger");
                }
            }
            require(beforeQuery.equals(store.writeToNbt()), "Queries preserve serialization");
        }
    }

    private static CanonicalGraphResource task(ProjectSnapshot project, int story) {
        return project.getCanonicalTask(Stability0402Fixtures.loadStory(story) + "~task~work");
    }

    private static CanonicalTaskEvent kill() {
        return CanonicalTaskEvent.killEntity(Stability0402Fixtures.SOURCE + "~actor~load0");
    }

    private static String key(UUID user, String story, String placement) {
        return user + "/" + story + "/" + placement;
    }

    private static NBTTagCompound image(CanonicalTaskSavedData data) {
        NBTTagCompound result = new NBTTagCompound();
        data.writeToNBT(result);
        return result;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static final class Expected {

        final UUID player;
        final String story, placement;
        final long run;
        int progress;
        boolean active = true;

        Expected(UUID player, String story, String placement, long run) {
            this.player = player;
            this.story = story;
            this.placement = placement;
            this.run = run;
        }
    }
}
