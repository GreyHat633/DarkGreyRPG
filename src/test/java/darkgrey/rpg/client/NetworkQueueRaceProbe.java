package darkgrey.rpg.client;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

import net.minecraft.network.EnumConnectionState;
import net.minecraft.network.NetworkManager;
import net.minecraft.network.handshake.client.C00Handshake;

import io.netty.channel.embedded.EmbeddedChannel;

/** Isolated diagnostic of the upstream outbound queue race; no production patch or game launch. */
public final class NetworkQueueRaceProbe {

    public static void main(String[] args) throws Exception {
        final NetworkManager manager = new NetworkManager(true);
        final CountDownLatch checked = new CountDownLatch(2);
        ConcurrentLinkedQueue<Object> queue = new ConcurrentLinkedQueue<Object>() {

            @Override
            public boolean isEmpty() {
                boolean empty = super.isEmpty();
                if (!empty) {
                    checked.countDown();
                    try {
                        if (!checked.await(5, TimeUnit.SECONDS)) throw new AssertionError("missing competing consumer");
                    } catch (InterruptedException e) {
                        throw new AssertionError(e);
                    }
                }
                return empty;
            }
        };
        Field outbound = NetworkManager.class.getDeclaredField("outboundPacketsQueue");
        outbound.setAccessible(true);
        outbound.set(manager, queue);
        manager.scheduleOutboundPacket(new C00Handshake(5, "localhost", 25565, EnumConnectionState.LOGIN));
        EmbeddedChannel channel = new EmbeddedChannel(new io.netty.channel.ChannelDuplexHandler());
        channel.attr(NetworkManager.attrKeyConnectionState)
            .set(EnumConnectionState.HANDSHAKING);
        Field active = NetworkManager.class.getDeclaredField("channel");
        active.setAccessible(true);
        active.set(manager, channel);
        final Method flush = NetworkManager.class.getDeclaredMethod("flushOutboundQueue");
        flush.setAccessible(true);
        final AtomicInteger failures = new AtomicInteger();
        final AtomicInteger completed = new AtomicInteger();
        final Throwable[] errors = new Throwable[2];
        Thread[] consumers = new Thread[2];
        for (int i = 0; i < 2; i++) {
            final int index = i;
            consumers[i] = new Thread(() -> {
                try {
                    flush.invoke(manager);
                    completed.incrementAndGet();
                } catch (InvocationTargetException e) {
                    errors[index] = e.getCause();
                    if (errors[index] instanceof NullPointerException) failures.incrementAndGet();
                } catch (Exception e) {
                    errors[index] = e;
                }
            }, "isolated-flush-" + i);
            consumers[i].setDaemon(true);
            consumers[i].start();
        }
        for (Thread consumer : consumers) {
            consumer.join(10000);
            if (consumer.isAlive()) throw new AssertionError("diagnostic timed out");
        }
        for (Throwable error : errors) if (error != null) error.printStackTrace(System.out);
        channel.finish();
        if (failures.get() != 1 || completed.get() != 1)
            throw new AssertionError("expected one upstream NPE and one successful consumer");
        System.out.println("UPSTREAM_OUTBOUND_QUEUE_RACE_REPRODUCED=YES; live incident cause remains inferred");
    }
}
