package darkgrey.rpg.media;

import java.nio.file.Path;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.config.RpgRuntimeDirectories;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.network.message.canonical.StoryMediaPlan;
import darkgrey.rpg.project.packages.LoadedStoryPackage;

/** Nonblocking cache/transfer controller; visible state changes are confined to the client thread. */
public final class CanonicalMediaClient {

    private static final ThreadPoolExecutor IO = new ThreadPoolExecutor(
        1,
        1,
        0L,
        TimeUnit.MILLISECONDS,
        new ArrayBlockingQueue<Runnable>(32),
        new ThreadFactory() {

            @Override
            public Thread newThread(Runnable task) {
                Thread thread = new Thread(task, "DGR-media-io");
                thread.setDaemon(true);
                return thread;
            }
        });
    private static final java.util.concurrent.ConcurrentLinkedQueue<State> RETIRED = new java.util.concurrent.ConcurrentLinkedQueue<State>();
    private static final Map<String, State> ACTIVE = new HashMap<String, State>();
    private static VerifiedMediaCache cache;
    private static StoryMediaCacheIndex packages;
    private static boolean loadingIndex;
    private static boolean journalDirty;
    private static boolean packagesDirty = true;
    private static boolean journalWorking;
    private static long nextMaintenance;
    private static Object connection;
    private static final Map<String, StoryMediaPlan.Descriptor> KNOWN = new java.util.LinkedHashMap<String, StoryMediaPlan.Descriptor>();
    private static final List<StoryMediaPlan.Descriptor> PLAN_DESCRIPTORS = new java.util.ArrayList<StoryMediaPlan.Descriptor>();
    private static final Set<String> PLAN_RUNNING = new HashSet<String>();
    private static int planNext;
    private static int planCount;
    private static long planRevision = -1;
    private static long appliedRevision = -1;
    private static String scope = "";
    private static final Set<String> VISIBLE = new HashSet<String>();
    private static final Set<String> RUNNING = new HashSet<String>();
    private static final Set<String> PRELOAD = new HashSet<String>();
    private static final Set<String> PACKAGE_PINS = new HashSet<String>();
    private static final Set<String> DELETE_PENDING = new HashSet<String>();
    private static final java.util.ArrayDeque<Runnable> PLAN_PENDING = new java.util.ArrayDeque<Runnable>();
    private static long sequence;
    private static final AtomicLong CACHE_HITS = new AtomicLong();
    private static final AtomicLong LOCAL_PACKAGE_HITS = new AtomicLong();
    private static final AtomicLong NETWORK_REQUESTS = new AtomicLong();
    /** A choice frame has no media fields, but remains in the line's portrait context. */
    private static long portraitTransportId = -1;
    private static String portraitStoryId;
    private static String portraitSessionResourceId;
    private static String portraitRef;

    private CanonicalMediaClient() {}

    private static VerifiedMediaCache cache() {
        if (cache == null) cache = new VerifiedMediaCache(
            RpgRuntimeDirectories.prepare(Minecraft.getMinecraft().mcDataDir)
                .cacheDirectory()
                .toPath());
        return cache;
    }

    private static void synchronizeConnection() {
        Minecraft mc = Minecraft.getMinecraft();
        Object next = mc.getNetHandler();
        if (connection == next) return;
        connection = next;
        scope = mc.isSingleplayer() && mc.getIntegratedServer() != null ? "local:" + mc.getIntegratedServer()
            .getFolderName() + "|"
            : "server:" + (mc.func_147104_D() == null ? "unknown" : mc.func_147104_D().serverIP) + "|";
        PLAN_PENDING.clear();
        KNOWN.clear();
        PLAN_DESCRIPTORS.clear();
        PLAN_RUNNING.clear();
        planNext = 0;
        planCount = 0;
        planRevision = -1;
        appliedRevision = -1;
        PRELOAD.clear();
        RUNNING.clear();
        VISIBLE.clear();
        if (packages != null) packages.suspend();
        Iterator<State> states = ACTIVE.values()
            .iterator();
        while (states.hasNext()) {
            State state = states.next();
            if (state.ready == null) {
                states.remove();
                retire(state);
            }
        }
        portraitRef = null;
        CanonicalMediaTextures.clear();
        journalDirty = true;
        packagesDirty = true;
    }

    private static void initializeIndex() {
        if (packages != null || loadingIndex) return;
        final VerifiedMediaCache store = cache();
        loadingIndex = true;
        try {
            IO.execute(new Runnable() {

                @Override
                public void run() {
                    StoryMediaCacheIndex restored;
                    try {
                        restored = StoryMediaCacheIndex.load(
                            store.root()
                                .resolve("story-packages.json"));
                    } catch (Exception failure) {
                        DarkGreyRpg.LOG.warn("Could not restore media cache journal; rebuilding", failure);
                        restored = new StoryMediaCacheIndex();
                    }
                    final StoryMediaCacheIndex result = restored;
                    MainThreadScheduler.scheduleClient(new Runnable() {

                        @Override
                        public void run() {
                            packages = result;
                            packages.suspend();
                            loadingIndex = false;
                            while (!PLAN_PENDING.isEmpty()) PLAN_PENDING.remove()
                                .run();
                            pumpPackages();
                            collectLegacyOrphans();
                            journalDirty = true;
                            packagesDirty = true;
                        }
                    });
                }
            });
        } catch (RejectedExecutionException failure) {
            loadingIndex = false;
        }
    }

    private static void collectLegacyOrphans() {
        final VerifiedMediaCache store = cache();
        try {
            IO.execute(new Runnable() {

                @Override
                public void run() {
                    final Set<String> orphans = new HashSet<String>();
                    Path folder = store.root()
                        .resolve("media");
                    if (!java.nio.file.Files.isDirectory(folder) || java.nio.file.Files.isSymbolicLink(folder)) return;
                    try (java.nio.file.DirectoryStream<Path> files = java.nio.file.Files.newDirectoryStream(folder)) {
                        for (Path path : files) {
                            String ref = "media/" + path.getFileName()
                                .toString();
                            if (darkgrey.rpg.graph.canonical.CanonicalMediaReference.isValid(ref)) orphans.add(ref);
                        }
                    } catch (Exception failure) {
                        DarkGreyRpg.LOG.warn("Could not inspect legacy media cache", failure);
                    }
                    MainThreadScheduler.scheduleClient(new Runnable() {

                        @Override
                        public void run() {
                            orphans.removeAll(packages.ownedRefs());
                            DELETE_PENDING.addAll(orphans);
                        }
                    });
                }
            });
        } catch (RejectedExecutionException ignored) {}
    }

    public static void acceptPlan(StoryMediaPlan plan) {
        synchronizeConnection();
        if (plan.getRevision() <= appliedRevision || plan.getRevision() < planRevision) return;
        if (plan.getChunkIndex() == 0) {
            planRevision = plan.getRevision();
            PLAN_DESCRIPTORS.clear();
            PLAN_RUNNING.clear();
            planNext = 0;
            planCount = plan.getChunkCount();
        }
        if (plan.getRevision() != planRevision || plan.getChunkIndex() != planNext || plan.getChunkCount() != planCount)
            return;
        PLAN_DESCRIPTORS.addAll(plan.getDescriptors());
        PLAN_RUNNING.addAll(plan.getRunningPackageIds());
        if (++planNext != planCount) return;
        appliedRevision = planRevision;
        Map<String, StoryMediaPlan.Descriptor> next = new java.util.LinkedHashMap<String, StoryMediaPlan.Descriptor>();
        Set<String> running = new HashSet<String>();
        for (StoryMediaPlan.Descriptor descriptor : PLAN_DESCRIPTORS) {
            next.put(descriptor.getPackageId(), descriptor);
            if (PLAN_RUNNING.contains(descriptor.getPackageId())) running.add(descriptor.getStoryId());
        }
        Set<String> previouslyRunning = new HashSet<String>(RUNNING);
        runningPackages(running);
        for (StoryMediaPlan.Descriptor descriptor : next.values()) {
            StoryMediaPlan.Descriptor previous = KNOWN.get(descriptor.getPackageId());
            if (previous == null || !previous.getFingerprint()
                .equals(descriptor.getFingerprint()) || previous.isPreload() != descriptor.isPreload()) {
                offerPackage(
                    descriptor.getStoryId(),
                    descriptor.getFingerprint(),
                    descriptor.getRefs(),
                    descriptor.isPreload());
            } else if (running.contains(descriptor.getStoryId())
                && !previouslyRunning.contains(scope + descriptor.getStoryId())) {
                    offerPackage(
                        descriptor.getStoryId(),
                        descriptor.getFingerprint(),
                        descriptor.getRefs(),
                        descriptor.isPreload());
                }
        }
        for (StoryMediaPlan.Descriptor previous : KNOWN.values()) {
            if (!next.containsKey(previous.getPackageId())) removePackage(previous.getStoryId());
        }
        KNOWN.clear();
        KNOWN.putAll(next);
        PLAN_DESCRIPTORS.clear();
        PLAN_RUNNING.clear();
    }

    /** Transport descriptors arrive before presentation. No installed-package scanning triggers preload. */
    public static void offerPackage(final String storyId, final String version, final List<String> refs,
        final boolean preload) {
        synchronizeConnection();
        initializeIndex();
        final String id = scope + storyId;
        Runnable apply = new Runnable() {

            @Override
            public void run() {
                if (preload) PRELOAD.add(id);
                packages.offer(id, version, refs, RUNNING.contains(id));
                // Reuse individually verified hash-addressed files across package versions/owners.
                for (String ref : refs) {
                    State state = ACTIVE.get(ref);
                    if (state != null && state.ready != null) packages.markReady(ref);
                }
                journalDirty = true;
                packagesDirty = true;
            }
        };
        if (packages == null) PLAN_PENDING.add(apply);
        else apply.run();
    }

    public static void runningPackages(final Set<String> storyIds) {
        synchronizeConnection();
        initializeIndex();
        final Set<String> ids = new HashSet<String>();
        for (String id : storyIds) ids.add(scope + id);
        Runnable apply = new Runnable() {

            @Override
            public void run() {
                for (String previous : RUNNING)
                    if (!ids.contains(previous) && !PRELOAD.contains(previous)) packages.pause(previous);
                RUNNING.clear();
                RUNNING.addAll(ids);
                packages.setRunning(ids);
                journalDirty = true;
                packagesDirty = true;
            }
        };
        if (packages == null) PLAN_PENDING.add(apply);
        else apply.run();
    }

    public static void removePackage(final String storyId) {
        synchronizeConnection();
        final String id = scope + storyId;
        Runnable apply = new Runnable() {

            @Override
            public void run() {
                PRELOAD.remove(id);
                packages.remove(id);
                journalDirty = true;
                packagesDirty = true;
            }
        };
        if (packages == null) PLAN_PENDING.add(apply);
        else apply.run();
    }

    private static void completed(State state, Path path) {
        state.ready = path;
        state.waiting = false;
        if (packages != null) packages.markReady(state.ref);
        journalDirty = true;
        packagesDirty = true;
    }

    private static void pumpPackages() {
        if (packages == null || !packagesDirty) return;
        packagesDirty = false;
        packages.pump();
        Set<String> owned = packages.ownedRefs();
        List<StoryMediaCacheIndex.Entry> downloading = packages.downloading();
        Set<String> transferring = new HashSet<String>();
        for (StoryMediaCacheIndex.Entry entry : downloading) transferring.addAll(entry.refs);
        DELETE_PENDING.addAll(packages.drainEvictedRefs());
        Iterator<String> pins = PACKAGE_PINS.iterator();
        while (pins.hasNext()) {
            String ref = pins.next();
            if (!owned.contains(ref)) {
                cache().unpin(ref);
                pins.remove();
            }
        }
        for (String ref : owned) if (!PACKAGE_PINS.contains(ref) && cache().pin(ref)) PACKAGE_PINS.add(ref);
        Iterator<State> states = ACTIVE.values()
            .iterator();
        while (states.hasNext()) {
            State state = states.next();
            if (!owned.contains(state.ref) || state.ready == null && !transferring.contains(state.ref)) {
                states.remove();
                retire(state);
            }
        }
        if (connection == null) return;
        for (StoryMediaCacheIndex.Entry entry : downloading) {
            boolean busy = false;
            for (String ref : entry.refs) {
                State state = ACTIVE.get(ref);
                if (state != null && state.ready == null) {
                    busy = true;
                    break;
                }
            }
            if (busy) continue;
            String next = null;
            for (String ref : entry.refs) {
                State state = ACTIVE.get(ref);
                if (state != null && state.ready != null) continue;
                if (VISIBLE.contains(ref)) {
                    next = ref;
                    break;
                }
                if (next == null && PRELOAD.contains(entry.id)) next = ref;
            }
            if (next != null) activate(next, null);
        }
    }

    private static void maintainJournal() {
        if (packages == null || journalWorking || System.nanoTime() < nextMaintenance) return;
        if (!journalDirty && DELETE_PENDING.isEmpty()) return;
        final Set<String> deleting = new HashSet<String>(DELETE_PENDING);
        deleting.removeAll(packages.ownedRefs());
        final StoryMediaCacheIndex index = packages;
        final VerifiedMediaCache store = cache();
        journalWorking = true;
        journalDirty = false;
        nextMaintenance = System.nanoTime() + 1000000000L;
        try {
            IO.execute(new Runnable() {

                @Override
                public void run() {
                    final Set<String> deleted = new HashSet<String>();
                    boolean saved = false;
                    try {
                        index.save(
                            store.root()
                                .resolve("story-packages.json"));
                        saved = true;
                        for (String ref : deleting) {
                            if (store.deleteIfUnpinned(ref) || !java.nio.file.Files.exists(store.path(ref)))
                                deleted.add(ref);
                        }
                    } catch (Exception failure) {
                        DarkGreyRpg.LOG.warn("Could not maintain story media cache", failure);
                    }
                    final boolean success = saved;
                    MainThreadScheduler.scheduleClient(new Runnable() {

                        @Override
                        public void run() {
                            DELETE_PENDING.removeAll(deleted);
                            for (String ref : deleted) packages.invalidate(ref);
                            journalWorking = false;
                            if (!success) journalDirty = true;
                            packagesDirty = true;
                        }
                    });
                }
            });
        } catch (RejectedExecutionException failure) {
            journalWorking = false;
            journalDirty = true;
            packagesDirty = true;
        }
    }

    public static int getCachedPackageCount() {
        return packages == null ? 0
            : packages.entries()
                .size();
    }

    public static int getPreloadingPackageCount() {
        return packages == null ? 0
            : packages.downloading()
                .size();
    }

    public static int getQueuedPackageCount() {
        return packages == null ? 0 : packages.queuedCount();
    }

    public static void present(CanonicalSessionFrame frame) {
        synchronizeConnection();
        updatePortraitContext(frame);
        Set<String> refs = new HashSet<String>();
        if (frame.getPresentation()
            .getMusicRef() != null)
            refs.add(
                frame.getPresentation()
                    .getMusicRef());
        for (darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer layer : frame.getPresentation()
            .getLayers()) refs.add(layer.mediaRef);
        String effectivePortrait = portraitRef(frame);
        if (effectivePortrait != null) refs.add(effectivePortrait);
        if (frame.getVoiceRef() != null) refs.add(frame.getVoiceRef());
        int imageCount = 0;
        for (String ref : refs) if (darkgrey.rpg.graph.canonical.CanonicalMediaReference.isImage(ref)) imageCount++;
        CanonicalMediaTextures.configureImageCount(imageCount);
        VISIBLE.clear();
        VISIBLE.addAll(refs);
        packagesDirty = true;
        if (packages != null) packages.touch(scope + frame.getStoryId());
        // Admission and package concurrency also apply to visible media. The server
        // sends the manifest before the frame; do not bypass a full ten-package cache.
        pumpPackages();
    }

    public static void clear() {
        VISIBLE.clear();
        portraitTransportId = -1;
        portraitStoryId = null;
        portraitSessionResourceId = null;
        portraitRef = null;
        // Verified files and recently uploaded textures are deliberately retained across
        // Session close/reopen. Texture retention is bounded by CanonicalMediaTextures.
        CanonicalMediaTextures.releaseInactive();
    }

    /** Returns the current line portrait, including the context retained for a choice frame. */
    public static String portraitRef(CanonicalSessionFrame frame) {
        if (frame == null) return null;
        if (frame.getPortraitRef() != null) return frame.getPortraitRef();
        return frame.getKind() == CanonicalSessionFrame.Kind.CHOICE && samePortraitContext(frame) ? portraitRef : null;
    }

    private static void updatePortraitContext(CanonicalSessionFrame frame) {
        if (frame.getKind() == CanonicalSessionFrame.Kind.LINE) {
            portraitTransportId = frame.getTransportId();
            portraitStoryId = frame.getStoryId();
            portraitSessionResourceId = frame.getSessionResourceId();
            portraitRef = frame.getPortraitRef();
        } else if (frame.getKind() != CanonicalSessionFrame.Kind.CHOICE || !samePortraitContext(frame)) {
            portraitTransportId = frame.getTransportId();
            portraitStoryId = frame.getStoryId();
            portraitSessionResourceId = frame.getSessionResourceId();
            portraitRef = null;
        }
    }

    private static boolean samePortraitContext(CanonicalSessionFrame frame) {
        return portraitTransportId == frame.getTransportId() && portraitStoryId != null
            && portraitStoryId.equals(frame.getStoryId())
            && portraitSessionResourceId != null
            && portraitSessionResourceId.equals(frame.getSessionResourceId());
    }

    public static boolean pin(String ref) {
        return cache().pin(ref);
    }

    public static void unpin(String ref) {
        cache().unpin(ref);
    }

    public static Path ready(String ref) {
        State state = ACTIVE.get(ref);
        return !VISIBLE.contains(ref) || state == null ? null : state.ready;
    }

    private static boolean current(State state) {
        return ACTIVE.get(state.ref) == state;
    }

    private static void activate(String ref, String musicRef) {
        if (ref == null || ACTIVE.containsKey(ref)) return;
        State state = new State(++sequence, ref);
        ACTIVE.put(ref, state);
        state.music = ref.equals(musicRef);
        check(state);
    }

    private static void check(final State state) {
        state.retired = false;
        final VerifiedMediaCache store = cache();
        if (!state.pinned) {
            state.pinned = store.pin(state.ref);
            if (!state.pinned) {
                state.retryAt = System.nanoTime() + 100000000L;
                return;
            }
        }
        state.working = true;
        work(state, new Runnable() {

            @Override
            public void run() {
                boolean found = false;
                try {
                    found = store.available(state.ref);
                    if (found) {
                        CACHE_HITS.incrementAndGet();

                    } else found = importFromLocalPackage(state, store);
                } catch (Exception ignored) {}
                final boolean available = found;
                MainThreadScheduler.scheduleClient(new Runnable() {

                    @Override
                    public void run() {
                        if (!current(state)) return;
                        state.working = false;
                        if (available) completed(state, store.path(state.ref));
                        else request(state);
                    }
                });
            }
        });
    }

    public static void tick() {
        synchronizeConnection();
        initializeIndex();
        pumpPackages();
        maintainJournal();
        CanonicalMediaTextures.releaseInactive();
        long now = System.nanoTime();
        for (State state : ACTIVE.values()) {
            if (state.ready != null) {
                // Start image decode as soon as the verified file is available, rather than
                // waiting for the renderer's first lookup. This keeps the panel and portrait
                // ready on the same render tick without blocking the client thread.
                if (VISIBLE.contains(state.ref)
                    && darkgrey.rpg.graph.canonical.CanonicalMediaReference.isImage(state.ref))
                    CanonicalMediaTextures.get(state.ref);
                continue;
            }
            if (state.working) continue;
            if (state.waiting && now - state.sentAt > 2000000000L) request(state);
            else if (!state.waiting && now >= state.retryAt) check(state);
        }
    }

    private static void request(State state) {
        if (!current(state) || Minecraft.getMinecraft().theWorld == null) return;
        state.waiting = true;
        state.sentAt = System.nanoTime();
        NETWORK_REQUESTS.incrementAndGet();
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalMediaRequest(state.id, state.ref, state.offset));
    }

    /**
     * Singleplayer only: an installed Story Package is already server-validated and
     * immutable. Read it through its generation lease on the media worker, then pass
     * every chunk through VerifiedMediaCache.Download so the content hash remains the
     * authorization boundary. Remote servers always use the authenticated transport.
     */
    private static boolean importFromLocalPackage(State state, VerifiedMediaCache store) {
        LoadedStoryPackage source = state.localPackage;
        if (source == null || state.retired) return false;
        boolean imported = importFromLocalPackage(source, state.ref, state.id, store);
        if (imported) {
            LOCAL_PACKAGE_HITS.incrementAndGet();
        }
        return imported;
    }

    private static boolean importFromLocalPackage(LoadedStoryPackage source, String ref, long id,
        VerifiedMediaCache store) {
        if (source == null) return false;
        try {
            LoadedStoryPackage.MediaReader reader = source.openMediaReader(ref);
            if (reader == null) return false;
            try (LoadedStoryPackage.MediaReader opened = reader) {
                VerifiedMediaCache.Download download = null;
                try {
                    int offset = 0;
                    int total = -1;
                    while (true) {
                        CanonicalMediaChunk chunk = opened.readChunk(id, offset);
                        if (chunk == null) return false;
                        if (total < 0) {
                            total = chunk.getTotal();
                            download = store.begin(id, ref, total);
                        } else if (chunk.getTotal() != total) return false;
                        if (chunk.getOffset() != offset || download == null) return false;
                        if (download.accept(chunk)) {
                            return true;
                        }
                        int expected = Math.min(CanonicalMediaChunk.CHUNK_BYTES, total - offset);
                        if (chunk.getData().length != expected) return false;
                        offset += CanonicalMediaChunk.CHUNK_BYTES;
                    }
                } finally {
                    if (download != null) download.close();
                }
            }
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean isWarmPortrait(String ref) {
        return false;
    }

    public static long getCacheHitCount() {
        return CACHE_HITS.get();
    }

    public static long getLocalPackageHitCount() {
        return LOCAL_PACKAGE_HITS.get();
    }

    public static long getNetworkRequestCount() {
        return NETWORK_REQUESTS.get();
    }

    public static void accept(final CanonicalMediaChunk chunk) {
        final State state = ACTIVE.get(chunk.getMediaRef());
        if (state == null || state.id != chunk.getRequestId() || state.ready != null || state.working) return;
        if (chunk.getTotal() == 0) {
            fail(state);
            return;
        }
        if (chunk.getOffset() != state.offset) return;
        state.waiting = false;
        state.working = true;
        work(state, new Runnable() {

            @Override
            public void run() {
                boolean completed = false;
                Exception failure = null;
                try {
                    if (state.download == null) state.download = cache.begin(state.id, state.ref, chunk.getTotal());
                    completed = state.download.accept(chunk);
                } catch (Exception exception) {
                    failure = exception;
                    close(state);
                }
                final boolean done = completed;
                final boolean failed = failure != null;
                MainThreadScheduler.scheduleClient(new Runnable() {

                    @Override
                    public void run() {
                        if (!current(state)) return;
                        state.working = false;
                        if (failed) {
                            fail(state);
                            return;
                        }
                        if (done) completed(state, cache.path(state.ref));
                        else {
                            state.offset += CanonicalMediaChunk.CHUNK_BYTES;
                            request(state);
                        }
                    }
                });
            }
        });
    }

    private static void fail(State state) {
        state.waiting = false;
        state.working = false;
        state.offset = 0;
        state.retryAt = System.nanoTime() + 5000000000L;
        retire(state);
    }

    private static void work(final State state, final Runnable task) {
        try {
            IO.execute(new Runnable() {

                @Override
                public void run() {
                    drainRetired();
                    try {
                        if (!state.retired) task.run();
                    } finally {
                        if (state.retired) close(state);
                        drainRetired();
                    }
                }
            });
        } catch (RejectedExecutionException exception) {
            state.working = false;
            state.waiting = false;
            state.retryAt = System.nanoTime() + 5000000000L;
        }
    }

    private static void retire(final State state) {
        state.retired = true;
        RETIRED.add(state);
        if (state.pinned) {
            cache.unpin(state.ref);
            state.pinned = false;
        }
        try {
            IO.execute(new Runnable() {

                @Override
                public void run() {
                    drainRetired();
                }
            });
        } catch (RejectedExecutionException ignored) { /* queued workers drain retired handles before and after work */ }
    }

    private static void drainRetired() {
        State state;
        while ((state = RETIRED.poll()) != null) close(state);
    }

    private static void close(State state) {
        try {
            if (state.download != null) state.download.close();
        } catch (Exception ignored) {}
        state.download = null;
    }

    private static final class State {

        volatile boolean retired;
        volatile boolean music;
        final long id;
        final String ref;
        final LoadedStoryPackage localPackage;
        int offset;
        long sentAt;
        long retryAt;
        boolean waiting;
        boolean working;
        boolean pinned;
        Path ready;
        VerifiedMediaCache.Download download;

        State(long id, String ref) {
            this.id = id;
            this.ref = ref;
            this.localPackage = localPackage(ref);
        }
    }

    private static LoadedStoryPackage localPackage(String ref) {
        Minecraft minecraft = Minecraft.getMinecraft();
        if (!minecraft.isSingleplayer()) return null;
        darkgrey.rpg.project.packages.StoryPackageLoader loader = DarkGreyRpg.getStoryPackageLoader();
        if (loader == null) return null;
        for (LoadedStoryPackage packageValue : loader.getInstalledPackages()
            .values())
            if (packageValue.getManifest()
                .getRequiredResources()
                .getMedia()
                .contains(ref)) return packageValue;
        return null;
    }
}
