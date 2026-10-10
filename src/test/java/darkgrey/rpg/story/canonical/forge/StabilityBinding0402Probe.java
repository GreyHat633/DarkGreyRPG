package darkgrey.rpg.story.canonical.forge;

import java.lang.reflect.Field;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Random;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;

/** Detached binding and index model; never counted as network or gameplay exploration. */
public final class StabilityBinding0402Probe {

    private StabilityBinding0402Probe() {}

    public static void main(String[] args) throws Exception {
        ProjectSnapshot project = Stability0402Fixtures.flow();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        data.bindProject(project, false);
        UUID player = new UUID(40201, 1);
        data.startStory(
            player,
            project.getCanonicalStory(Stability0402Fixtures.SOURCE),
            "out",
            CanonicalStoryRepeatPolicy.ONCE,
            402L);
        data.claimStoryTerminalRoute(player, Stability0402Fixtures.SOURCE, 402L, "out");
        Field storyField = CanonicalSessionSavedData.class.getDeclaredField("storyStore");
        storyField.setAccessible(true);
        CanonicalStoryInstanceStore stories = (CanonicalStoryInstanceStore) storyField.get(data);
        Object unrelated = stories.get(player, Stability0402Fixtures.SOURCE);
        try (
            darkgrey.rpg.persistence.Scoped0402TraversalGuard ignored = darkgrey.rpg.persistence.Scoped0402TraversalGuard
                .install(stories)) {
            UUID other = new UUID(40201, 2);
            data.startStory(
                other,
                project.getCanonicalStory(Stability0402Fixtures.TARGET),
                "in",
                CanonicalStoryRepeatPolicy.ONCE,
                403L);
            require(data.isDirty(), "Scoped new Story persists");
            data.setDirty(false);
            data.startStory(
                other,
                project.getCanonicalStory(Stability0402Fixtures.TARGET),
                "in",
                CanonicalStoryRepeatPolicy.ONCE,
                404L);
            require(!data.isDirty(), "Scoped repeated Story start is a no-op");
            try {
                data.startStory(
                    new UUID(40201, 3),
                    project.getCanonicalStory(Stability0402Fixtures.TARGET),
                    "missing",
                    CanonicalStoryRepeatPolicy.ONCE,
                    405L);
                throw new AssertionError("Invalid trigger accepted");
            } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException expected) {}
            require(
                !data.isDirty() && stories.get(player, Stability0402Fixtures.SOURCE) == unrelated,
                "Failed start preserves unrelated identity and dirty");
        }
        NBTTagCompound before = nbt(data);
        data.setDirty(false);
        Field store = CanonicalSessionSavedData.class.getDeclaredField("store");
        store.setAccessible(true);
        Object initial = store.get(data);
        for (int i = 0; i < 200; i++) data.bindProject(project, i % 2 == 0);
        require(initial == store.get(data) && before.equals(nbt(data)) && !data.isDirty(), "Stable bind fence");
        ProjectSnapshot reload = Stability0402Fixtures.flow();
        data.bindProject(reload, false);
        require(
            initial != store.get(data) && before.equals(nbt(data)) && !data.isDirty(),
            "No-content reload must restore once without state loss");
        initial = store.get(data);
        data.bindProject(reload, true);
        require(initial == store.get(data), "Reload settles");
        data.readFromNBT(before);
        data.bindProject(reload, false);
        require(initial != store.get(data) && before.equals(nbt(data)), "Reread invalidates same snapshot fence");
        CanonicalSessionSavedData worldB = new CanonicalSessionSavedData();
        worldB.bindProject(reload, false);
        require(worldB.getStorySnapshot(player, Stability0402Fixtures.SOURCE) == null, "World storage ownership");
        try {
            data.readFromNBT(new NBTTagCompound());
            throw new AssertionError("Malformed accepted");
        } catch (IllegalArgumentException expected) {}
        try {
            data.bindProject(reload, true);
            throw new AssertionError("Cache bypassed quarantine");
        } catch (darkgrey.rpg.session.persistence.CanonicalSessionDataUnavailableException expected) {}
        for (int seed = 40201; seed <= 40205; seed++) indexModel(seed, project);
        System.out.println(
            "STABILITY_BINDING_0402=PASS stable reload reread quarantine world-ownership index seeds=5 operations=10000 layer=A-index-model");
    }

    private static void indexModel(int seed, ProjectSnapshot project) {
        Random random = new Random(seed);
        CanonicalStoryInstanceStore store = new CanonicalStoryInstanceStore();
        Map<String, Long> ledger = new LinkedHashMap<>();
        for (int step = 0; step < 2000; step++) {
            UUID player = new UUID(seed, random.nextInt(4));
            String story = random.nextBoolean() ? Stability0402Fixtures.SOURCE : Stability0402Fixtures.TARGET;
            String key = player + "/" + story;
            int operation = random.nextInt(5);
            if (operation == 0 || operation == 1) {
                long run = step + 1;
                store.start(
                    player,
                    project.getCanonicalStory(story),
                    story.equals(Stability0402Fixtures.SOURCE) ? "out" : "in",
                    CanonicalStoryRepeatPolicy.ONCE,
                    run);
                ledger.putIfAbsent(key, run);
            } else if (operation == 2) {
                store.discardByPlayerStory(player, story);
                ledger.remove(key);
            } else if (operation == 3) {
                store.discardByStoryIds(Collections.singleton(story));
                ledger.keySet()
                    .removeIf(value -> value.endsWith("/" + story));
            } else {
                NBTTagCompound payload = store.writeToNbt();
                CanonicalStoryInstanceStore restored = new CanonicalStoryInstanceStore();
                restored.readFromNbt(payload, project::getCanonicalStory);
                require(payload.equals(restored.writeToNbt()), "Round trip seed=" + seed + " step=" + step);
                store = restored;
            }
            require(store.size() == ledger.size(), "Size seed=" + seed + " step=" + step);
            for (int u = 0; u < 4; u++) {
                UUID owner = new UUID(seed, u);
                int expected = 0;
                for (String value : ledger.keySet()) if (value.startsWith(owner + "/")) expected++;
                require(
                    store.snapshots(owner)
                        .size() == expected,
                    "Index size seed=" + seed + " step=" + step);
                String previous = "";
                for (CanonicalStoryInstanceSnapshot snapshot : store.snapshots(owner)) {
                    require(owner.equals(snapshot.getPlayerUuid()), "Owner");
                    require(
                        snapshot.getStoryId()
                            .compareTo(previous) > 0,
                        "Stable sort");
                    previous = snapshot.getStoryId();
                    require(
                        snapshot.getActivationTime() == ledger.get(owner + "/" + snapshot.getStoryId()),
                        "Run identity");
                }
            }
        }
    }

    private static NBTTagCompound nbt(CanonicalSessionSavedData data) {
        NBTTagCompound result = new NBTTagCompound();
        data.writeToNBT(result);
        return result;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
