package darkgrey.rpg.story.runtime;

import java.util.ArrayDeque;
import java.util.Queue;

import net.minecraft.entity.player.EntityPlayerMP;

public final class StoryEventBus {

    private final StoryRuntimeService runtime;
    private final Queue<PendingEvent> pending = new ArrayDeque<PendingEvent>();
    private boolean dispatching;

    public StoryEventBus(StoryRuntimeService runtime) {
        this.runtime = runtime;
    }

    public void post(EntityPlayerMP player, StoryEvent event) {
        pending.add(new PendingEvent(player, event));
        if (dispatching) {
            return;
        }
        dispatching = true;
        try {
            PendingEvent next;
            while ((next = pending.poll()) != null) {
                runtime.handle(next.player, next.event);
            }
        } finally {
            dispatching = false;
        }
    }

    private static final class PendingEvent {

        private final EntityPlayerMP player;
        private final StoryEvent event;

        private PendingEvent(EntityPlayerMP player, StoryEvent event) {
            this.player = player;
            this.event = event;
        }
    }
}
