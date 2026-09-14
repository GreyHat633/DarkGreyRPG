package darkgrey.rpg.media;

import java.util.Map;
import java.util.WeakHashMap;

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
    private static final Map<EntityPlayerMP, long[]> RATES = new WeakHashMap<EntityPlayerMP, long[]>();

    private CanonicalMediaServer() {}

    public static void present(EntityPlayerMP player, CanonicalSessionFrame frame) {
        FRAMES.put(player, frame);
    }

    public static void close(EntityPlayerMP player, long transportId) {
        CanonicalSessionFrame frame = FRAMES.get(player);
        if (frame != null && frame.getTransportId() == transportId) FRAMES.remove(player);
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
        darkgrey.rpg.network.MainThreadScheduler.scheduleServer(new Runnable() {

            @Override
            public void run() {
                request(player, request);
            }
        });
    }

    public static void request(EntityPlayerMP player, CanonicalMediaRequest request) {
        if (player == null || player.playerNetServerHandler == null) return;
        CanonicalSessionFrame frame = FRAMES.get(player);
        String ref = request.getMediaRef();
        CanonicalMediaChunk chunk = null;
        if (frame != null
            && (ref.equals(frame.getPortraitRef()) || ref.equals(frame.getVoiceRef())
                || frame.getPresentation()
                    .contains(ref))
            && DarkGreyRpg.getStoryPackageLoader() != null) {
            for (LoadedStoryPackage story : DarkGreyRpg.getStoryPackageLoader()
                .getPackages()
                .values()) {
                chunk = story.readMediaChunk(request.getRequestId(), ref, request.getOffset());
                if (chunk != null) break;
            }
        }
        if (chunk == null) chunk = new CanonicalMediaChunk(request.getRequestId(), ref, 0, 0, new byte[0]);
        DialogueNetwork.CHANNEL.sendTo(chunk, player);
    }
}
