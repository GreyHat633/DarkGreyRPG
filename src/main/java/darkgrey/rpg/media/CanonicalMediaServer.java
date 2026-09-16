package darkgrey.rpg.media;

import java.util.Map;
import java.util.WeakHashMap;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.packages.LoadedStoryPackage;

/** Main-thread authorization against the frame actually sent to this player. */
public final class CanonicalMediaServer {

    private static final Map<EntityPlayerMP, CanonicalSessionFrame> FRAMES = new WeakHashMap<EntityPlayerMP, CanonicalSessionFrame>();
    private static final Map<EntityPlayerMP, String> PORTRAITS = new WeakHashMap<EntityPlayerMP, String>();
    private static final Map<EntityPlayerMP, long[]> RATES = new WeakHashMap<EntityPlayerMP, long[]>();
    private static final int MEDIA_WORKER_THREADS = 2;
    private static final int MEDIA_WORKER_QUEUE = 64;
    private static final java.util.concurrent.atomic.AtomicInteger IN_FLIGHT = new java.util.concurrent.atomic.AtomicInteger();
    private static final ThreadPoolExecutor MEDIA_WORKER = new ThreadPoolExecutor(
        MEDIA_WORKER_THREADS,
        MEDIA_WORKER_THREADS,
        30L,
        TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(MEDIA_WORKER_QUEUE),
        new java.util.concurrent.ThreadFactory() {

            private final java.util.concurrent.atomic.AtomicInteger sequence = new java.util.concurrent.atomic.AtomicInteger();

            @Override
            public Thread newThread(Runnable task) {
                Thread thread = new Thread(task, "dgr-media-" + sequence.incrementAndGet());
                thread.setDaemon(true);
                return thread;
            }
        },
        new ThreadPoolExecutor.AbortPolicy());

    private CanonicalMediaServer() {}

    public static void present(EntityPlayerMP player, CanonicalSessionFrame frame) {
        StoryMediaServer.presentFrame(player, frame);
        String portrait = retainedPortrait(FRAMES.get(player), frame, PORTRAITS.get(player));
        if (portrait == null) PORTRAITS.remove(player);
        else PORTRAITS.put(player, portrait);
        FRAMES.put(player, frame);
    }

    static String retainedPortrait(CanonicalSessionFrame previous, CanonicalSessionFrame frame, String portrait) {
        if (frame.getKind() == CanonicalSessionFrame.Kind.LINE) return frame.getPortraitRef();
        if (frame.getKind() == CanonicalSessionFrame.Kind.CHOICE && previous != null
            && previous.getTransportId() == frame.getTransportId()
            && previous.getStoryId()
                .equals(frame.getStoryId())
            && previous.getSessionResourceId()
                .equals(frame.getSessionResourceId()))
            return portrait;
        return null;
    }

    public static void close(EntityPlayerMP player, long transportId) {
        CanonicalSessionFrame frame = FRAMES.get(player);
        if (frame != null && frame.getTransportId() == transportId) {
            FRAMES.remove(player);
            PORTRAITS.remove(player);
            StoryMediaServer.clearFrame(player, frame.getStoryId());
        }
    }

    public static void enqueue(final EntityPlayerMP player, final CanonicalMediaRequest request) {
        if (player == null) return;
        synchronized (RATES) {
            long now = System.nanoTime();
            long[] rate = RATES.get(player);
            if (rate == null || now - rate[0] > 1000000000L) {
                rate = new long[] { now, 0 };
                RATES.put(player, rate);
            }
            if (++rate[1] > 32) return;
        }
        if (!reserveRequest()) return;
        darkgrey.rpg.network.MainThreadScheduler.scheduleServer(new Runnable() {

            @Override
            public void run() {
                requestReserved(player, request);
            }
        });
    }

    public static void request(EntityPlayerMP player, CanonicalMediaRequest request) {
        if (reserveRequest()) requestReserved(player, request);
    }

    /** Revoke presentation capabilities and stop client media when their package retires. */
    public static void retireStories(java.util.Set<String> storyIds) {
        StoryMediaServer.retireStories(storyIds);
        java.util.Iterator<Map.Entry<EntityPlayerMP, CanonicalSessionFrame>> entries = FRAMES.entrySet()
            .iterator();
        while (entries.hasNext()) {
            Map.Entry<EntityPlayerMP, CanonicalSessionFrame> entry = entries.next();
            if (!storyIds.contains(
                entry.getValue()
                    .getStoryId()))
                continue;
            EntityPlayerMP player = entry.getKey();
            CanonicalSessionFrame frame = entry.getValue();
            entries.remove();
            PORTRAITS.remove(player);
            if (player != null && player.playerNetServerHandler != null) DialogueNetwork.CHANNEL.sendTo(
                new darkgrey.rpg.network.message.canonical.CanonicalSessionClose(
                    frame.getTransportId(),
                    frame.getStoryId()),
                player);
        }
    }

    private static boolean reserveRequest() {
        while (true) {
            int active = IN_FLIGHT.get();
            if (active >= MEDIA_WORKER_QUEUE + MEDIA_WORKER_THREADS) return false;
            if (IN_FLIGHT.compareAndSet(active, active + 1)) return true;
        }
    }

    private static void requestReserved(EntityPlayerMP player, CanonicalMediaRequest request) {
        if (player == null || player.playerNetServerHandler == null) {
            IN_FLIGHT.decrementAndGet();
            return;
        }
        String ref = request.getMediaRef();
        LoadedStoryPackage packageSource = null;
        if (isAuthorized(player, ref) && DarkGreyRpg.getStoryPackageLoader() != null) {
            for (LoadedStoryPackage story : DarkGreyRpg.getStoryPackageLoader()
                .getPackages()
                .values())
                if (story.getManifest()
                    .getRequiredResources()
                    .getMedia()
                    .contains(ref)) {
                        packageSource = story;
                        break;
                    }
        }
        final LoadedStoryPackage source = packageSource;
        final boolean sourceLease = source != null && source.retainMediaRequest();
        try {
            MEDIA_WORKER.execute(new Runnable() {

                @Override
                public void run() {
                    try {
                        CanonicalMediaChunk chunk;
                        try {
                            chunk = source == null || !sourceLease ? unavailable(request)
                                : source
                                    .readMediaChunk(request.getRequestId(), request.getMediaRef(), request.getOffset());
                        } catch (RuntimeException failure) {
                            chunk = null;
                        }
                        final CanonicalMediaChunk response = chunk == null ? unavailable(request) : chunk;
                        darkgrey.rpg.network.MainThreadScheduler.scheduleServer(new Runnable() {

                            @Override
                            public void run() {
                                // Recheck presentation ownership after disk work. A closed/replaced
                                // session must never receive a chunk from its former generation.
                                try {
                                    if (isAuthorized(player, request.getMediaRef()))
                                        DialogueNetwork.CHANNEL.sendTo(response, player);
                                } finally {
                                    IN_FLIGHT.decrementAndGet();
                                }
                            }
                        });
                    } finally {
                        if (sourceLease) source.releaseMediaRequest();
                    }
                }
            });
        } catch (RejectedExecutionException rejected) {
            IN_FLIGHT.decrementAndGet();
            if (sourceLease) source.releaseMediaRequest();
            DialogueNetwork.CHANNEL.sendTo(unavailable(request), player);
        }
    }

    private static boolean isAuthorized(EntityPlayerMP player, String ref) {
        CanonicalSessionFrame frame = FRAMES.get(player);
        return player != null && player.playerNetServerHandler != null
            && (StoryMediaServer.authorizes(player, ref)
                || frame != null && (ref.equals(PORTRAITS.get(player)) || ref.equals(frame.getVoiceRef())
                    || frame.getPresentation()
                        .contains(ref)));
    }

    private static CanonicalMediaChunk unavailable(CanonicalMediaRequest request) {
        return new CanonicalMediaChunk(request.getRequestId(), request.getMediaRef(), 0, 0, new byte[0]);
    }

    /** Bounded worker diagnostics used by the WP-F probe and operational logs. */
    public static int getMediaWorkerQueueSize() {
        return MEDIA_WORKER.getQueue()
            .size();
    }

    public static int getMediaWorkerActiveCount() {
        return MEDIA_WORKER.getActiveCount();
    }

    public static int getMediaInFlightCount() {
        return IN_FLIGHT.get();
    }
}
