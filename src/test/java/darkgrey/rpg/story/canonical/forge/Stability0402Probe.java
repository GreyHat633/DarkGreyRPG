package darkgrey.rpg.story.canonical.forge;

import java.io.File;
import java.lang.management.ManagementFactory;
import java.lang.reflect.Field;
import java.util.Collections;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerServiceProbe;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;

/** Actual production polls, with isolated SavedData and no network-client claim. */
public final class Stability0402Probe {

    private Stability0402Probe() {}

    public static void main(String[] args) throws Exception {
        boolean baseline = args.length > 0 && "baseline".equals(args[0]);
        ProjectRepository repository = new ProjectRepository(new File("."));
        ProjectSnapshot project = CanonicalStoryServerServiceProbe.project();
        repository.installSnapshot(project);
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        String story = project.getCanonicalStories()
            .keySet()
            .iterator()
            .next();
        service.startByEntry(new UUID(402, 1), story, 100L);
        ProbePlayer player = player(new UUID(402, 2));
        CanonicalStoryForgeManager manager = new CanonicalStoryForgeManager(
            repository,
            new CanonicalSessionForgeManager(repository),
            new CanonicalTaskForgeManager(repository),
            ignored -> data,
            (ignored, action) -> true);
        Field store = CanonicalSessionSavedData.class.getDeclaredField("store");
        store.setAccessible(true);
        int offline = Integer.getInteger("dgr0402.offline", 0);
        Field storyStore = CanonicalSessionSavedData.class.getDeclaredField("storyStore");
        storyStore.setAccessible(true);
        darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore instances = (darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceStore) storyStore
            .get(data);
        for (int i = 0; i < offline; i++) instances.start(
            new UUID(40201, i),
            project.getCanonicalStory(story),
            "entry",
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy.ONCE,
            100L);
        int rebuilds = 0;
        long[] elapsed = new long[200];
        NBTTagCompound before = new NBTTagCompound();
        data.writeToNBT(before);
        data.setDirty(false);
        com.sun.management.ThreadMXBean bean = (com.sun.management.ThreadMXBean) ManagementFactory.getThreadMXBean();
        long thread = Thread.currentThread()
            .getId();
        long allocations = bean.getThreadAllocatedBytes(thread);
        for (int i = 0; i < 200; i++) {
            long start = System.nanoTime();
            Object previous = store.get(data);
            manager.synchronizeMedia(player);
            rebuilds += previous != store.get(data) ? 1 : 0;
            previous = store.get(data);
            manager.recoverPendingRoutes(player);
            rebuilds += previous != store.get(data) ? 1 : 0;
            previous = store.get(data);
            manager.matchingRegionTriggers(player, 0, 0, 64, 0);
            rebuilds += previous != store.get(data) ? 1 : 0;
            previous = store.get(data);
            manager.handleRegionPosition(player, 0, 0, 64, 0, Collections.emptyList());
            rebuilds += previous != store.get(data) ? 1 : 0;
            elapsed[i] = System.nanoTime() - start;
        }
        allocations = bean.getThreadAllocatedBytes(thread) - allocations;
        NBTTagCompound after = new NBTTagCompound();
        data.writeToNBT(after);
        require(before.equals(after) && !data.isDirty(), "Stable polls changed persisted state");
        require(baseline ? rebuilds == 800 : rebuilds == 0, "Unexpected rebuild count: " + rebuilds);
        java.util.Arrays.sort(elapsed);
        System.out.println(
            "POLL_METRICS baseline=" + baseline
                + " offline="
                + offline
                + " calls=800 rebuilds="
                + rebuilds
                + " allocated_bytes="
                + allocations
                + " p50_ns="
                + elapsed[99]
                + " p95_ns="
                + elapsed[189]
                + " p99_ns="
                + elapsed[197]
                + " max_ns="
                + elapsed[199]);
        if (baseline) {
            pendingBaseline(repository, data, manager, player);
            queueBaseline();
        }
        System.out.println("STABILITY_0402_" + (baseline ? "BASELINE_OBSERVED" : "STABLE_POLLS") + "=PASS");
    }

    private static void pendingBaseline(ProjectRepository repository, CanonicalSessionSavedData ignored,
        CanonicalStoryForgeManager unused, ProbePlayer player) throws Exception {
        ProjectSnapshot project = Stability0402Fixtures.flow();
        repository.installSnapshot(project);
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        service.startByEntry(player.getUniqueID(), Stability0402Fixtures.SOURCE, 402L);
        data.claimStoryTerminalRoute(player.getUniqueID(), Stability0402Fixtures.SOURCE, 402L, "out");
        CanonicalStoryForgeManager manager = new CanonicalStoryForgeManager(
            repository,
            new CanonicalSessionForgeManager(repository),
            new CanonicalTaskForgeManager(repository),
            p -> data,
            (p, action) -> true);
        require(!manager.recoverPendingRoutes(player), "Baseline unexpectedly recovered pending route");
        require(
            data.pendingStoryTerminalRouteStoryIds(player.getUniqueID())
                .contains(Stability0402Fixtures.SOURCE),
            "Baseline pending route disappeared");
        require(
            data.getStorySnapshot(player.getUniqueID(), Stability0402Fixtures.TARGET) == null,
            "Baseline target already exists");
        System.out.println("NULL_FIRST_PRODUCTION_RECOVERY baseline=true pending_retained=true target_created=false");
    }

    private static void queueBaseline() {
        int[] count = { 0 };
        for (int i = 0; i < 4096; i++) MainThreadScheduler.scheduleServer(() -> count[0]++);
        long start = System.nanoTime();
        new MainThreadScheduler().onServerTick(new TickEvent.ServerTickEvent(TickEvent.Phase.START));
        require(count[0] == 4096, "Baseline queue did not drain all accepted tasks");
        System.out.println(
            "QUEUE_BASELINE accepted=4096 executed_in_one_tick=" + count[0]
                + " elapsed_ns="
                + (System.nanoTime() - start)
                + " producer=noop no_lost_business_observed=true");
    }

    private static ProbePlayer player(UUID uuid) throws Exception {
        Field field = sun.misc.Unsafe.class.getDeclaredField("theUnsafe");
        field.setAccessible(true);
        ProbePlayer player = (ProbePlayer) ((sun.misc.Unsafe) field.get(null)).allocateInstance(ProbePlayer.class);
        player.uuid = uuid;
        return player;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static final class ProbePlayer extends EntityPlayerMP {

        private UUID uuid;

        private ProbePlayer() {
            super(null, null, null, null);
        }

        @Override
        public UUID getUniqueID() {
            return uuid;
        }
    }
}
