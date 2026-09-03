package darkgrey.rpg.project.packages;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import net.minecraft.world.storage.MapStorage;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;

/** Shared startup/reload reconciliation for accepted package generations and persisted runtime state. */
public final class StoryPackageGenerationLifecycle {

    private static final Logger LOG = LogManager.getLogger(StoryPackageGenerationLifecycle.class);

    private StoryPackageGenerationLifecycle() {}

    public static Result reconcile(MapStorage storage, Map<String, LoadedStoryPackage> packages) {
        if (storage == null || packages == null)
            throw new IllegalArgumentException("Generation lifecycle inputs are required.");
        StoryPackageGenerationSavedData registry = StoryPackageGenerationSavedData.get(storage);
        Map<String, PackageGenerationKey> current = generationKeys(packages);
        List<StoryPackageGenerationDelta.Entry> deltas = StoryPackageGenerationDelta
            .betweenKeys(registry.snapshot(), current);
        boolean bootstrap = !registry.isInitialized();
        Set<String> affectedStories = new LinkedHashSet<String>();
        for (StoryPackageGenerationDelta.Entry delta : deltas) if (delta.retiresRuntime()) affectedStories.add(
            delta.getPrevious()
                .getStoryId());
        // A B2 world has runtime but no generation registry. Retire only Stories
        // owned by currently installed packages once, instead of falsely adopting
        // those cursors as belonging to an unknown B3 generation.
        if (bootstrap)
            for (PackageGenerationKey generation : current.values()) affectedStories.add(generation.getStoryId());

        CanonicalSessionSavedData.DiscardResult sessions = CanonicalSessionSavedData.get(storage)
            .discardByStoryIds(affectedStories);
        int tasks = CanonicalTaskSavedData.get(storage)
            .discardByStoryIds(affectedStories);
        registry.replace(current);
        Result result = new Result(deltas, affectedStories, sessions, tasks, bootstrap);
        log(result);
        return result;
    }

    public static Map<String, PackageGenerationKey> generationKeys(Map<String, LoadedStoryPackage> packages) {
        if (packages == null) throw new IllegalArgumentException("Story Packages are required.");
        Map<String, PackageGenerationKey> result = new java.util.TreeMap<String, PackageGenerationKey>();
        for (Map.Entry<String, LoadedStoryPackage> entry : packages.entrySet()) {
            PackageGenerationKey value = PackageGenerationKey.from(entry.getValue());
            if (!entry.getKey()
                .equals(value.getPackageId()))
                throw new IllegalArgumentException("Story Package map key does not match package_id.");
            result.put(entry.getKey(), value);
        }
        return Collections.unmodifiableMap(new LinkedHashMap<String, PackageGenerationKey>(result));
    }

    private static void log(Result result) {
        if (result.isBootstrap()) LOG.info(
            "Story Package generation registry initialized; unknown pre-B3 runtime for installed package Stories was retired.");
        for (StoryPackageGenerationDelta.Entry delta : result.getDeltas()) {
            PackageGenerationKey previous = delta.getPrevious();
            PackageGenerationKey current = delta.getCurrent();
            if (delta.getKind() == StoryPackageGenerationDelta.Kind.UPDATED) LOG.info(
                "Story Package {} UPDATED generation {} -> {}",
                delta.getPackageId(),
                previous.shortFingerprint(),
                current.shortFingerprint());
            else if (delta.getKind() == StoryPackageGenerationDelta.Kind.REPLACED) LOG.info(
                "Story Package {} identity replacement {}:{} -> {}:{}",
                delta.getPackageId(),
                previous.getPackageId(),
                previous.getStoryId(),
                current.getPackageId(),
                current.getStoryId());
            else if (delta.getKind() == StoryPackageGenerationDelta.Kind.ADDED)
                LOG.info("Story Package {} ADDED generation {}", delta.getPackageId(), current.shortFingerprint());
            else if (delta.getKind() == StoryPackageGenerationDelta.Kind.REMOVED)
                LOG.info("Story Package {} REMOVED generation {}", delta.getPackageId(), previous.shortFingerprint());
            else LOG.info("Story Package {} UNCHANGED generation {}", delta.getPackageId(), current.shortFingerprint());
        }
        if (!result.getAffectedStoryIds()
            .isEmpty())
            LOG.info(
                "Story Package generation retirement stories={} story_instances={} session_instances={} continuations={} task_instances={}",
                result.getAffectedStoryIds(),
                Integer.valueOf(result.getStoryInstancesRetired()),
                Integer.valueOf(result.getSessionInstancesRetired()),
                Integer.valueOf(result.getContinuationsRetired()),
                Integer.valueOf(result.getTaskInstancesRetired()));
    }

    public static final class Result {

        private final List<StoryPackageGenerationDelta.Entry> deltas;
        private final Set<String> affectedStoryIds;
        private final CanonicalSessionSavedData.DiscardResult sessions;
        private final int taskInstances;
        private final boolean bootstrap;

        Result(List<StoryPackageGenerationDelta.Entry> deltas, Set<String> affectedStoryIds,
            CanonicalSessionSavedData.DiscardResult sessions, int taskInstances, boolean bootstrap) {
            this.deltas = deltas;
            this.affectedStoryIds = Collections.unmodifiableSet(new LinkedHashSet<String>(affectedStoryIds));
            this.sessions = sessions;
            this.taskInstances = taskInstances;
            this.bootstrap = bootstrap;
        }

        public List<StoryPackageGenerationDelta.Entry> getDeltas() {
            return deltas;
        }

        public Set<String> getAffectedStoryIds() {
            return affectedStoryIds;
        }

        public int getStoryInstancesRetired() {
            return sessions.getStoryInstances();
        }

        public int getSessionInstancesRetired() {
            return sessions.getSessionInstances();
        }

        public int getContinuationsRetired() {
            return sessions.getContinuations();
        }

        public int getTaskInstancesRetired() {
            return taskInstances;
        }

        public int getRuntimeStatesRetired() {
            return sessions.total() + taskInstances;
        }

        public boolean isBootstrap() {
            return bootstrap;
        }
    }
}
