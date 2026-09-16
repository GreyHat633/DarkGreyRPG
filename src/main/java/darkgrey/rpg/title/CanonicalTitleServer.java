package darkgrey.rpg.title;

import java.util.ArrayDeque;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalTitleFrame;
import darkgrey.rpg.story.canonical.runtime.CanonicalTitleConfiguration;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatch;

/** Server-owned FIFO, transient clocks and one-shot completion receipts. No client-selected Story IDs. */
public final class CanonicalTitleServer {

    private static final Map<UUID, Queue> QUEUES = new HashMap<UUID, Queue>();
    private static long sequence;

    private CanonicalTitleServer() {}

    public static synchronized boolean enqueue(EntityPlayerMP player, CanonicalStoryDispatch dispatch,
        Runnable completed) {
        if (player == null || player.playerNetServerHandler == null) return false;
        UUID id = player.getUniqueID();
        Queue queue = QUEUES.get(id);
        if (queue == null || queue.player != player) {
            queue = new Queue(player);
            QUEUES.put(id, queue);
        }
        String story = dispatch.getSnapshot()
            .getStoryId();
        for (Entry entry : queue.entries) if (entry.story.equals(story) && entry.node.equals(dispatch.getPlacementId())
            && entry.activation == dispatch.getSnapshot()
                .getActivationTime())
            return true;
        if (queue.entries.size() >= 16) return false;
        queue.entries.add(
            new Entry(
                story,
                dispatch.getPlacementId(),
                dispatch.getSnapshot()
                    .getActivationTime(),
                CanonicalTitleConfiguration.parse(dispatch.getActionProperties()),
                completed));
        tick(player);
        return true;
    }

    public static synchronized void tick(EntityPlayerMP player) {
        Queue queue = QUEUES.get(player.getUniqueID());
        if (queue == null || queue.player != player || queue.entries.isEmpty()) return;
        Entry entry = queue.entries.peek();
        if (player.isDead || player.getHealth() <= 0 || player.playerNetServerHandler == null) {
            if (entry.token != 0 && player.playerNetServerHandler != null)
                DialogueNetwork.CHANNEL.sendTo(new CanonicalTitleFrame(entry.token, null), player);
            entry.token = 0;
            entry.pending = false;
            return;
        }
        if (entry.token == 0) {
            if (sequence == Long.MAX_VALUE) throw new IllegalStateException("Title token exhausted");
            entry.token = ++sequence;
            entry.started = System.nanoTime();
            DialogueNetwork.CHANNEL.sendTo(new CanonicalTitleFrame(entry.token, entry.title), player);
        }
    }

    /** Called on Netty: validate/rate-limit before allocating a main-thread job. */
    public static synchronized void acknowledge(final EntityPlayerMP player, final long token) {
        Queue queue = QUEUES.get(player.getUniqueID());
        if (queue == null || queue.player != player || queue.entries.isEmpty()) return;
        Entry entry = queue.entries.peek();
        if (entry.token != token || entry.pending
            || System.nanoTime() - entry.started < entry.title.duration() * 1000000000.0) return;
        entry.pending = true;
        MainThreadScheduler.scheduleServer(new Runnable() {

            @Override
            public void run() {
                complete(player, token);
            }
        });
    }

    private static synchronized void complete(EntityPlayerMP player, long token) {
        Queue queue = QUEUES.get(player.getUniqueID());
        if (queue == null || queue.player != player || queue.entries.isEmpty()) return;
        Entry entry = queue.entries.peek();
        if (entry.token != token || player.isDead || player.getHealth() <= 0) {
            entry.pending = false;
            return;
        }
        queue.entries.remove();
        if (queue.entries.isEmpty()) QUEUES.remove(player.getUniqueID());
        if (entry.title.waitForCompletion) entry.completed.run();
        tick(player);
    }

    public static synchronized void clear(UUID playerId) {
        QUEUES.remove(playerId);
    }

    public static synchronized void retireStories(java.util.Set<String> storyIds) {
        java.util.List<EntityPlayerMP> players = new java.util.ArrayList<EntityPlayerMP>();
        for (Queue queue : QUEUES.values()) players.add(queue.player);
        for (EntityPlayerMP player : players) for (String storyId : storyIds) clearStory(player, storyId);
    }

    public static synchronized void clearStory(EntityPlayerMP player, String story) {
        clearStory(player, story, false);
    }

    public static synchronized void clearWaitingStory(EntityPlayerMP player, String story) {
        clearStory(player, story, true);
    }

    private static void clearStory(EntityPlayerMP player, String story, boolean preserveDetached) {
        Queue queue = QUEUES.get(player.getUniqueID());
        if (queue == null) return;
        Iterator<Entry> entries = queue.entries.iterator();
        while (entries.hasNext()) {
            Entry entry = entries.next();
            if (entry.story.equals(story) && (!preserveDetached || entry.title.waitForCompletion)) {
                if (entry.token > 0 && player.playerNetServerHandler != null)
                    DialogueNetwork.CHANNEL.sendTo(new CanonicalTitleFrame(entry.token, null), player);
                entries.remove();
            }
        }
        if (queue.entries.isEmpty()) QUEUES.remove(player.getUniqueID());
        else tick(player);
    }

    private static final class Queue {

        final EntityPlayerMP player;
        final ArrayDeque<Entry> entries = new ArrayDeque<Entry>();

        Queue(EntityPlayerMP player) {
            this.player = player;
        }
    }

    private static final class Entry {

        final String story, node;
        final long activation;
        final CanonicalTitleConfiguration title;
        final Runnable completed;
        long token, started;
        boolean pending;

        Entry(String story, String node, long activation, CanonicalTitleConfiguration title, Runnable completed) {
            this.story = story;
            this.node = node;
            this.activation = activation;
            this.title = title;
            this.completed = completed;
        }
    }
}
