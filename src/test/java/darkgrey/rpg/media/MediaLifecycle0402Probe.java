package darkgrey.rpg.media;

import java.lang.reflect.Field;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.network.NetHandlerPlayServer;
import net.minecraft.network.NetworkManager;

import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import io.netty.channel.ChannelInboundHandlerAdapter;
import io.netty.channel.embedded.EmbeddedChannel;
import sun.misc.Unsafe;

/** Actual producer reservation and delayed worker cancellation; no network/world claim. */
public final class MediaLifecycle0402Probe {

    private MediaLifecycle0402Probe() {}

    public static void main(String[] args) throws Exception {
        Field unsafeField = Unsafe.class.getDeclaredField("theUnsafe");
        unsafeField.setAccessible(true);
        Unsafe unsafe = (Unsafe) unsafeField.get(null);
        EntityPlayerMP player = (EntityPlayerMP) unsafe.allocateInstance(EntityPlayerMP.class);
        set(Entity.class, player, "entityUniqueID", new UUID(40201, 1));
        CanonicalMediaRequest request = new CanonicalMediaRequest(
            1L,
            "media/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png",
            0);
        MainThreadScheduler scheduler = new MainThreadScheduler();
        MainThreadScheduler.beginServer();
        MainThreadScheduler.Scope epoch = MainThreadScheduler.serverScope();
        for (int i = 0; i < 1024; i++)
            require(MainThreadScheduler.scheduleServer(epoch, () -> {}, () -> {}), "Fill admission");
        CanonicalMediaServer.enqueue(player, request);
        require(CanonicalMediaServer.getMediaInFlightCount() == 0, "Full admission releases reservation");
        MainThreadScheduler.stopServer();
        MainThreadScheduler.beginServer();
        for (int i = 0; i < 66; i++) CanonicalMediaServer.enqueue(player, request);
        require(CanonicalMediaServer.getMediaInFlightCount() == 66, "Producer's bounded in-flight admission");
        CanonicalMediaServer.enqueue(player, request);
        require(CanonicalMediaServer.getMediaInFlightCount() == 66, "In-flight cap retained");
        MainThreadScheduler.stopServer();
        require(CanonicalMediaServer.getMediaInFlightCount() == 0, "Stop releases queued producers");
        MainThreadScheduler.beginServer();
        CanonicalMediaServer.enqueue(player, request);
        scheduler.onServerTick(new TickEvent.ServerTickEvent(TickEvent.Phase.START));
        require(CanonicalMediaServer.getMediaInFlightCount() == 0, "Invalid connection guard releases producer");

        EmbeddedChannel channel = new EmbeddedChannel(new ChannelInboundHandlerAdapter());
        NetworkManager network = new NetworkManager(false);
        set(NetworkManager.class, network, "channel", channel);
        NetHandlerPlayServer handler = (NetHandlerPlayServer) unsafe.allocateInstance(NetHandlerPlayServer.class);
        set(NetHandlerPlayServer.class, handler, "netManager", network);
        handler.playerEntity = player;
        player.playerNetServerHandler = handler;
        Field workerField = CanonicalMediaServer.class.getDeclaredField("MEDIA_WORKER");
        workerField.setAccessible(true);
        ThreadPoolExecutor workers = (ThreadPoolExecutor) workerField.get(null);
        long completedBefore = workers.getCompletedTaskCount();
        CountDownLatch entered = new CountDownLatch(2), release = new CountDownLatch(1);
        Runnable blocked = () -> {
            entered.countDown();
            try {
                if (!release.await(5, TimeUnit.SECONDS)) throw new AssertionError("Controlled worker deadline");
            } catch (InterruptedException interrupted) {
                Thread.currentThread()
                    .interrupt();
                throw new AssertionError(interrupted);
            }
        };
        workers.execute(blocked);
        workers.execute(blocked);
        require(entered.await(2, TimeUnit.SECONDS), "Both workers held");
        try {
            CanonicalMediaServer.request(player, request);
            require(CanonicalMediaServer.getMediaInFlightCount() == 1, "Actual IO producer queued");
            MainThreadScheduler.stopServer();
            MainThreadScheduler.beginServer();
        } finally {
            release.countDown();
        }
        awaitCompleted(workers, completedBefore + 3);
        require(
            CanonicalMediaServer.getMediaInFlightCount() == 0 && MainThreadScheduler.serverMetrics()[0] == 0,
            "Late result belongs to retired epoch and releases in-flight: in_flight="
                + CanonicalMediaServer.getMediaInFlightCount()
                + " pending="
                + MainThreadScheduler.serverMetrics()[0]);
        completedBefore = workers.getCompletedTaskCount();
        CanonicalMediaServer.request(player, request);
        awaitCompleted(workers, completedBefore + 1);
        require(MainThreadScheduler.serverMetrics()[0] == 1, "New epoch admits completion");
        scheduler.onServerTick(new TickEvent.ServerTickEvent(TickEvent.Phase.START));
        require(CanonicalMediaServer.getMediaInFlightCount() == 0, "New completion releases reservation");
        MainThreadScheduler.stopServer();
        channel.close()
            .syncUninterruptibly();
        System.out.println(
            "MEDIA_LIFECYCLE_0402=PASS actual producer full/retired/invalid/late-worker/new-epoch reservation layer=A no real-download claim");
    }

    private static void awaitCompleted(ThreadPoolExecutor workers, long expected) throws Exception {
        long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(3);
        while (workers.getCompletedTaskCount() < expected) {
            if (System.nanoTime() >= deadline) throw new AssertionError("Worker did not finish");
            Thread.sleep(10L);
        }
    }

    private static void set(Class<?> owner, Object target, String name, Object value) throws Exception {
        Field field = owner.getDeclaredField(name);
        field.setAccessible(true);
        field.set(target, value);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
