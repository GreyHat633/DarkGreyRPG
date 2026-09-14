package darkgrey.rpg.media;

import java.nio.file.Path;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

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
    private static MediaCacheRetention retention;
    private static long sequence;

    private CanonicalMediaClient() {}

    private static VerifiedMediaCache cache() {
        if (cache == null) cache = new VerifiedMediaCache(
            Minecraft.getMinecraft().mcDataDir.toPath()
                .resolve("darkgrey_rpg_media_cache"));
        if (retention == null) retention = new MediaCacheRetention(cache);
        return cache;
    }

    public static void present(CanonicalSessionFrame frame) {
        Set<String> refs = new HashSet<String>();
        if (frame.getPresentation()
            .getMusicRef() != null)
            refs.add(
                frame.getPresentation()
                    .getMusicRef());
        for (darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer layer : frame.getPresentation()
            .getLayers()) refs.add(layer.mediaRef);
        if (frame.getPortraitRef() != null) refs.add(frame.getPortraitRef());
        if (frame.getVoiceRef() != null) refs.add(frame.getVoiceRef());
        int imageCount = 0;
        for (String ref : refs) if (darkgrey.rpg.graph.canonical.CanonicalMediaReference.isImage(ref)) imageCount++;
        CanonicalMediaTextures.configureImageCount(imageCount);
        Iterator<Map.Entry<String, State>> iterator = ACTIVE.entrySet()
            .iterator();
        while (iterator.hasNext()) {
            State state = iterator.next()
                .getValue();
            if (!refs.contains(state.ref)) {
                iterator.remove();
                retire(state);
            }
        }
        for (String ref : refs) if (!ACTIVE.containsKey(ref)) {
            State state = new State(++sequence, ref);
            ACTIVE.put(ref, state);
            state.music = ref.equals(
                frame.getPresentation()
                    .getMusicRef());
            check(state);
        }
        for (State state : ACTIVE.values()) state.music = state.ref.equals(
            frame.getPresentation()
                .getMusicRef());
    }

    public static void clear() {
        for (State state : ACTIVE.values()) retire(state);
        ACTIVE.clear();
        CanonicalMediaTextures.clear();
    }

    public static boolean pin(String ref) {
        return cache().pin(ref);
    }

    public static void unpin(String ref) {
        cache().unpin(ref);
    }

    public static Path ready(String ref) {
        State state = ACTIVE.get(ref);
        return state == null ? null : state.ready;
    }

    private static boolean current(State state) {
        return ACTIVE.get(state.ref) == state;
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
                    if (found) touch(state);
                } catch (Exception ignored) {}
                final boolean available = found;
                MainThreadScheduler.scheduleClient(new Runnable() {

                    @Override
                    public void run() {
                        if (!current(state)) return;
                        state.working = false;
                        if (available) state.ready = store.path(state.ref);
                        else request(state);
                    }
                });
            }
        });
    }

    public static void tick() {
        CanonicalMediaTextures.releaseInactive();
        long now = System.nanoTime();
        for (State state : ACTIVE.values()) {
            if (state.ready != null || state.working) continue;
            if (state.waiting && now - state.sentAt > 2000000000L) request(state);
            else if (!state.waiting && now >= state.retryAt) check(state);
        }
    }

    private static void request(State state) {
        if (!current(state) || Minecraft.getMinecraft().theWorld == null) return;
        state.waiting = true;
        state.sentAt = System.nanoTime();
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalMediaRequest(state.id, state.ref, state.offset));
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
                    if (completed) touch(state);
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
                        if (done) state.ready = cache.path(state.ref);
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

    private static void touch(State state) {
        try {
            retention.touch(
                state.ref,
                state.music ? MediaCacheRetention.Kind.MUSIC
                    : state.ref.endsWith(".ogg") ? MediaCacheRetention.Kind.VOICE : MediaCacheRetention.Kind.IMAGE);
            retention.sweep();
        } catch (Exception ignored) {}
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
        }
    }
}
