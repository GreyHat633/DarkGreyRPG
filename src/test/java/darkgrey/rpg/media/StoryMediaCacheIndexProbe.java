package darkgrey.rpg.media;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** Small pure-Java policy probe for the ten-package/three-download contract. */
public final class StoryMediaCacheIndexProbe {

    private StoryMediaCacheIndexProbe() {}

    public static void main(String[] args) throws Exception {
        slotsAndConcurrentLimit();
        runningPackagesStallAndRelease();
        packageCompletesOnlyWhenAllReferencesReady();
        sharedReferenceIsEvictedOnlyAfterLastOwner();
        lruAndVersionReplacement();
        saveReloadDropsReadinessAndActiveState();
        System.out.println("STORY_MEDIA_CACHE_INDEX_POLICY=PASS");
    }

    private static void slotsAndConcurrentLimit() {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        for (int number = 0; number < 20; number++)
            index.offer("slot-" + number, "v1", Collections.singletonList(ref(number, "png")), false);
        index.pump();
        require(
            index.entries()
                .size() == 3,
            "first batch admits only three incomplete packages");
        require(index.queuedCount() == 17, "remaining offers stay queued behind active downloads");
        require(
            index.downloading()
                .size() == 3,
            "only three packages admitted as downloading");
        for (int number = 0; number < 3; number++) index.markReady(ref(number, "png"));
        index.pump();
        require(
            index.entries()
                .size() == 6 && index.queuedCount() == 14,
            "next FIFO batch admits after completion");
        require(
            index.downloading()
                .get(0).id.equals("slot-3"),
            "FIFO next batch starts with fourth offer");
    }

    private static void runningPackagesStallAndRelease() {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        Set<String> running = new HashSet<String>();
        for (int number = 0; number < 11; number++) {
            String id = "running-" + number;
            index.offer(id, "v1", Collections.singletonList(ref(100 + number, "png")), false);
            if (number < 10) running.add(id);
        }
        index.setRunning(running);
        index.pump();
        require(
            index.entries()
                .size() == 3 && index.queuedCount() == 8,
            "running packages start in protected batches");
        for (int number = 0; number < 10; number += 3) {
            int limit = Math.min(number + 3, 10);
            for (int current = number; current < limit; current++) index.markReady(ref(100 + current, "png"));
            index.pump();
        }
        index.pump();
        require(
            index.entries()
                .size() == 10 && index.queuedCount() == 1,
            "protected ten running packages stall admission");
        index.setRunning(Collections.<String>emptySet());
        index.pump();
        require(
            index.queuedCount() == 0 && index.entries()
                .size() == 10,
            "released package admits after protection ends");
    }

    private static void packageCompletesOnlyWhenAllReferencesReady() {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        String first = ref(200, "ogg");
        String second = ref(201, "png");
        index.offer("all-refs", "v1", Arrays.asList(first, second), false);
        index.pump();
        index.markReady(first);
        require(
            !index.entries()
                .get(0)
                .isReady(),
            "partial package stays unready");
        index.markReady(second);
        require(
            index.entries()
                .get(0)
                .isReady(),
            "package completes after every reference is ready");
    }

    private static void sharedReferenceIsEvictedOnlyAfterLastOwner() {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        String shared = ref(300, "png");
        String onlyA = ref(301, "png");
        String onlyB = ref(302, "png");
        index.offer("shared-a", "v1", Arrays.asList(shared, onlyA), false);
        index.offer("shared-b", "v1", Arrays.asList(shared, onlyB), false);
        index.offer("shared-c", "v1", Collections.singletonList(ref(303, "png")), false);
        index.markReady(shared);
        index.markReady(onlyA);
        index.markReady(onlyB);
        index.markReady(ref(303, "png"));
        index.pump();
        index.touch("shared-b");
        for (int number = 304; number < 312; number++) {
            String id = "fill-" + number;
            String media = ref(number, "png");
            index.offer(id, "v1", Collections.singletonList(media), false);
            index.markReady(media);
        }
        index.pump();
        List<String> firstDrain = index.drainEvictedRefs();
        require(firstDrain.contains(onlyA) && !firstDrain.contains(shared), "shared ref retained by second owner");
        for (StoryMediaCacheIndex.Entry entry : index.entries()) {
            if (!entry.id.equals("shared-b")) index.touch(entry.id);
        }
        index.offer("shared-d", "v1", Collections.singletonList(ref(313, "png")), false);
        index.markReady(ref(313, "png"));
        index.pump();
        List<String> secondDrain = index.drainEvictedRefs();
        require(secondDrain.contains(shared), "shared ref orphaned after last owner eviction");
    }

    private static void lruAndVersionReplacement() {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        String oldRef = ref(400, "png");
        String unchangedRef = ref(401, "png");
        String newRef = ref(402, "png");
        index.offer("versioned", "v1", Arrays.asList(oldRef, unchangedRef), false);
        index.markReady(oldRef);
        index.markReady(unchangedRef);
        index.pump();
        require(
            index.entries()
                .get(0)
                .isReady(),
            "initial version is ready");
        index.offer("versioned", "v2", Arrays.asList(unchangedRef, newRef), false);
        require(
            !index.entries()
                .get(0)
                .isReady(),
            "version replacement invalidates incomplete readiness");
        require(
            index.drainEvictedRefs()
                .contains(oldRef),
            "version replacement reports orphaned old reference");
        index.markReady(newRef);
        require(
            index.entries()
                .get(0)
                .isReady(),
            "unchanged verified ref is reused");

        for (int number = 402; number < 411; number++) {
            String id = "lru-" + number;
            String media = ref(number, "png");
            index.offer(id, "v1", Collections.singletonList(media), false);
            index.markReady(media);
        }
        index.pump();
        index.touch("versioned");
        String next = ref(411, "png");
        index.offer("lru-411", "v1", Collections.singletonList(next), false);
        index.markReady(next);
        index.pump();
        require(index.admits("versioned"), "touched package survives LRU eviction");
        require(!index.admits("lru-402"), "oldest untouched package is evicted");
    }

    private static void saveReloadDropsReadinessAndActiveState() throws Exception {
        StoryMediaCacheIndex index = new StoryMediaCacheIndex();
        String media = ref(500, "ogg");
        index.offer("persisted", "v7", Collections.singletonList(media), true);
        index.markReady(media);
        index.pump();
        Path root = Paths.get(".tooling/0.3.3.1/story-media");
        Files.createDirectories(root);
        Path directory = Files.createTempDirectory(root, "index-");
        Path journal = directory.resolve("cache.json");
        index.save(journal);
        StoryMediaCacheIndex restored = StoryMediaCacheIndex.load(journal);
        require(
            restored.entries()
                .size() == 1,
            "journal restores admitted metadata");
        require(
            !restored.entries()
                .get(0)
                .isReady(),
            "journal does not trust persisted ready state");
        require(
            !restored.entries()
                .get(0)
                .isRunning(),
            "journal does not restore active state");
        require(
            restored.downloading()
                .isEmpty(),
            "restored dormant package does not start downloading");
        restored.offer("persisted", "v7", Collections.singletonList(media), false);
        restored.pump();
        require(
            restored.downloading()
                .size() == 1,
            "actual reoffer reactivates package for file revalidation");

        Files.write(journal, "{\"entries\":[{\"id\":\"bad\",\"ready\":true}]}".getBytes(StandardCharsets.UTF_8));
        require(
            StoryMediaCacheIndex.load(journal)
                .entries()
                .isEmpty(),
            "malformed journal is safely ignored");
    }

    private static String ref(int number, String extension) {
        StringBuilder hash = new StringBuilder();
        String digits = Integer.toHexString(number);
        while (hash.length() < 64 - digits.length()) hash.append('a');
        hash.append(digits);
        return "media/" + hash + "." + extension;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
