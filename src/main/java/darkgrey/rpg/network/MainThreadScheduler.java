package darkgrey.rpg.network;

import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

public final class MainThreadScheduler {

    private static final Queue<Runnable> SERVER_TASKS = new ConcurrentLinkedQueue<Runnable>();
    private static final Queue<Runnable> CLIENT_TASKS = new ConcurrentLinkedQueue<Runnable>();

    public static void scheduleServer(Runnable task) {
        SERVER_TASKS.add(task);
    }

    public static void scheduleClient(Runnable task) {
        CLIENT_TASKS.add(task);
    }

    @SubscribeEvent
    public void onServerTick(TickEvent.ServerTickEvent event) {
        if (event.phase == TickEvent.Phase.START) {
            drain(SERVER_TASKS);
        }
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.START) {
            drain(CLIENT_TASKS);
        }
    }

    private static void drain(Queue<Runnable> tasks) {
        Runnable task;
        while ((task = tasks.poll()) != null) {
            task.run();
        }
    }
}
