package darkgrey.rpg.network;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.network.NetHandlerPlayServer;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

public final class MainThreadScheduler {

    public static final class Scope {

        private final MainThreadQueue.Scope value = new MainThreadQueue.Scope();

        private Scope() {}
    }

    private static final org.apache.logging.log4j.Logger LOG = org.apache.logging.log4j.LogManager
        .getLogger(MainThreadScheduler.class);
    private static final java.util.Map<String, FailureCount> FAILURES = new java.util.LinkedHashMap<>();
    private static final MainThreadQueue SERVER = new MainThreadQueue(
        1024,
        64,
        2000000L,
        System::nanoTime,
        MainThreadScheduler::failed);
    private static final MainThreadQueue CLIENT = new MainThreadQueue(
        1024,
        32,
        2000000L,
        System::nanoTime,
        MainThreadScheduler::failed);
    private static volatile Scope server = new Scope(), client = new Scope();

    public static Scope serverScope() {
        return server;
    }

    public static Scope clientScope() {
        return client;
    }

    public static synchronized void beginServer() {
        SERVER.retire(server.value);
        server = new Scope();
    }

    public static synchronized void stopServer() {
        SERVER.retire(server.value);
    }

    public static synchronized void beginClient() {
        CLIENT.retire(client.value);
        client = new Scope();
    }

    public static synchronized void stopClient() {
        CLIENT.retire(client.value);
    }

    public static boolean scheduleServer(Scope scope, Runnable task, Runnable cancelled) {
        return SERVER.offer(scope.value, () -> true, task, reason -> cancelled.run());
    }

    public static boolean scheduleClient(Scope scope, Runnable task, Runnable cancelled) {
        return CLIENT.offer(scope.value, () -> true, task, reason -> cancelled.run());
    }

    public static boolean completeServer(Scope scope, Runnable task, Runnable cancelled) {
        return SERVER.offerCompletion(scope.value, () -> true, task, reason -> cancelled.run());
    }

    public static boolean completeClient(Scope scope, Runnable task, Runnable cancelled) {
        return CLIENT.offerCompletion(scope.value, () -> true, task, reason -> cancelled.run());
    }

    public static boolean scheduleServer(EntityPlayerMP player, Runnable task, Runnable cancelled) {
        final NetHandlerPlayServer connection = player == null ? null : player.playerNetServerHandler;
        return SERVER.offer(
            server.value,
            () -> player != null && connection != null
                && player.playerNetServerHandler == connection
                && connection.playerEntity == player
                && connection.netManager.isChannelOpen(),
            task,
            reason -> cancelled.run());
    }

    public static boolean scheduleClient(Object connection, Runnable task, Runnable cancelled) {
        return CLIENT.offer(
            client.value,
            () -> darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection),
            task,
            reason -> cancelled.run());
    }

    public static void scheduleServer(Runnable task) {
        scheduleServer(server, task, MainThreadScheduler::rejected);
    }

    public static void scheduleClient(Runnable task) {
        scheduleClient(client, task, MainThreadScheduler::rejected);
    }

    private static void rejected() {
        throw new java.util.concurrent.RejectedExecutionException("DGR main-thread task not executed");
    }

    private static void failed(RuntimeException failure) {
        reportFailure("main-thread", failure);
    }

    /** Bounded diagnostic categories; request IDs and player/world objects are never retained. */
    public static synchronized void reportFailure(String source, RuntimeException failure) {
        String category = source + ":"
            + failure.getClass()
                .getName();
        if (!FAILURES.containsKey(category) && FAILURES.size() >= 64) category = "other";
        FailureCount count = FAILURES.computeIfAbsent(category, ignored -> new FailureCount());
        count.total++;
        long now = System.nanoTime();
        if (count.total == 1 || now - count.last >= 60000000000L) {
            LOG.error("DGR request failed: " + category + "; cumulative failures=" + count.total, failure);
            count.last = now;
        }
    }

    private static final class FailureCount {

        long total, last;
    }

    public static long[] serverMetrics() {
        return SERVER.metrics();
    }

    public static long[] clientMetrics() {
        return CLIENT.metrics();
    }

    public static String slowestServerTask() {
        return SERVER.slowestTask();
    }

    public static String slowestClientTask() {
        return CLIENT.slowestTask();
    }

    public static void rejectClient(Object connection) {
        darkgrey.rpg.DarkGreyRpg.proxy.rejectClientQueue(connection);
    }

    public static void rejectServer(EntityPlayerMP player) {
        if (player != null && player.playerNetServerHandler != null
            && player.playerNetServerHandler.netManager.isChannelOpen())
            player.playerNetServerHandler.sendPacket(
                new net.minecraft.network.play.server.S02PacketChat(
                    new net.minecraft.util.ChatComponentText("[DarkGrey RPG] 本次请求未执行；服务器繁忙或连接状态已变化，请稍后重试。")));
    }

    @SubscribeEvent
    public void connected(cpw.mods.fml.common.network.FMLNetworkEvent.ClientConnectedToServerEvent event) {
        beginClient();
    }

    @SubscribeEvent
    public void disconnected(cpw.mods.fml.common.network.FMLNetworkEvent.ClientDisconnectionFromServerEvent event) {
        stopClient();
    }

    @SubscribeEvent
    public void onServerTick(TickEvent.ServerTickEvent event) {
        if (event.phase == TickEvent.Phase.START) {
            SERVER.drain();
        }
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.START) {
            CLIENT.drain();
        }
    }

}
