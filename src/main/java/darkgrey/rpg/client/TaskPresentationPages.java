package darkgrey.rpg.client;

import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.TaskPresentationPage;

/** Connection-scoped page LRU: 16 pages / 4 MiB, four in-flight requests, no polling of hidden content. */
public final class TaskPresentationPages {

    private static final LinkedHashMap<String, Page> CACHE = new LinkedHashMap<String, Page>(16, .75f, true);
    private static final Map<String, Request> PENDING = new HashMap<String, Request>();
    private static final LinkedHashMap<String, Carousel> CAROUSELS = new LinkedHashMap<String, Carousel>(
        16,
        .75f,
        true);
    private static Object world, connection;
    private static long sequence;
    private static int bytes;

    private TaskPresentationPages() {}

    public static synchronized void clear() {
        CACHE.clear();
        PENDING.clear();
        CAROUSELS.clear();
        bytes = 0;
    }

    private static boolean current() {
        Minecraft mc = Minecraft.getMinecraft();
        Object active = mc.getNetHandler();
        if (world != mc.theWorld || connection != active) {
            clear();
            world = mc.theWorld;
            connection = active;
        }
        return world != null && mc.thePlayer != null;
    }

    public static String identity(NBTTagCompound context) {
        return context.getInteger("operation") + ":"
            + context.getInteger("dimension")
            + ":"
            + context.getString("story")
            + "\0"
            + context.getString("placement")
            + "\0"
            + context.getString("objective")
            + ":"
            + context.getLong("activation")
            + ":"
            + context.getLong("bindings")
            + ":"
            + context.getLong("package_revision")
            + ":"
            + context.getLong("generation")
            + ":"
            + context.getString("history");
    }

    public static synchronized NBTTagCompound page(NBTTagCompound context, int cursor) {
        if (!current()) return null;
        String key = identity(context) + ":" + cursor;
        long now = System.nanoTime();
        Page found = CACHE.get(key);
        if (found != null) {
            found.visible = now;
            return found.data;
        }
        Iterator<Request> requests = PENDING.values()
            .iterator();
        while (requests.hasNext()) if (now - requests.next().sent > 3000000000L) requests.remove();
        Request pending = PENDING.get(key);
        if (pending != null) {
            pending.visible = now;
            return null;
        }
        if (PENDING.size() >= 4) return null;
        NBTTagCompound request = (NBTTagCompound) context.copy();
        // Send context only, never copy the summary's stacks into a request.
        for (Object name : new HashSet<Object>(request.func_150296_c())) if (!Arrays
            .asList(
                "operation",
                "dimension",
                "story",
                "placement",
                "objective",
                "activation",
                "bindings",
                "package_revision",
                "generation",
                "history")
            .contains(name)) request.removeTag((String) name);
        request.setInteger("cursor", cursor);
        request.setLong("sequence", ++sequence);
        PENDING.put(key, new Request(sequence, now));
        DialogueNetwork.CHANNEL.sendToServer(new TaskPresentationPage(false, request));
        return null;
    }

    public static synchronized void accept(NBTTagCompound data) {
        if (!current() || data.getInteger("dimension") != Minecraft.getMinecraft().thePlayer.dimension) return;
        String key = identity(data) + ":" + data.getInteger("cursor");
        Request pending = PENDING.get(key);
        if (pending == null || pending.sequence != data.getLong("sequence")) return;
        PENDING.remove(key);
        long now = System.nanoTime();
        if (now - pending.visible > 500000000L) return;
        int weight = 2 * darkgrey.rpg.creator.TaskCandidateIndex.measured(data) + 1024;
        if (weight > 4 * 1024 * 1024) return;
        while (!CACHE.isEmpty() && (CACHE.size() >= 16 || bytes + weight > 4 * 1024 * 1024)) {
            String oldest = null;
            for (Map.Entry<String, Page> entry : CACHE.entrySet()) if (now - entry.getValue().visible > 250000000L) {
                oldest = entry.getKey();
                break;
            }
            if (oldest == null) return;
            bytes -= CACHE.remove(oldest).weight;
        }
        CACHE.put(key, new Page(data, weight, now));
        bytes += weight;
    }

    public static synchronized NBTTagCompound candidate(NBTTagCompound summary, boolean freeze) {
        if (!summary.getBoolean("group") || summary.getInteger("total") < 1) return null;
        NBTTagCompound context = (NBTTagCompound) summary.copy();
        context.setInteger("operation", summary.getInteger("operation") == 4 ? 4 : 1);
        String identity = identity(context);
        Carousel carousel = CAROUSELS.get(identity);
        long now = System.nanoTime();
        if (carousel == null) {
            if (CAROUSELS.size() >= 16) CAROUSELS.remove(
                CAROUSELS.keySet()
                    .iterator()
                    .next());
            carousel = new Carousel();

            CAROUSELS.put(identity, carousel);
        }
        int total = summary.getInteger("total");
        int desired = (int) ((now / 1500000000L) % total);
        NBTTagCompound page = page(context, carousel.cursor);
        if (page != null && page.hasKey("error")) return candidateError(page);
        if (page == null || desired < carousel.cursor || desired >= page.getInteger("next")) {
            int start = desired / 20 * 20;
            NBTTagCompound next = page(context, start);
            if (next == null) return carousel.last;
            if (next.hasKey("error")) return candidateError(next);
            if (desired >= next.getInteger("next")) {
                start = desired;
                next = page(context, start);
            }
            if (next == null) return carousel.last;
            if (next.hasKey("error")) return candidateError(next);
            carousel.cursor = start;
            page = next;
        }
        NBTTagList items = page.getTagList("items", 10);
        int offset = desired - carousel.cursor;
        if (offset < 0 || offset >= items.tagCount()) return carousel.last;
        if (offset >= items.tagCount() - 2)
            page(context, page.getInteger("next") < total ? page.getInteger("next") : 0);
        carousel.last = items.getCompoundTagAt(offset);
        return carousel.last;
    }

    private static NBTTagCompound candidateError(NBTTagCompound page) {
        NBTTagCompound result = new NBTTagCompound();
        result.setString("error", page.getString("error"));
        return result;
    }

    public static synchronized void retry(NBTTagCompound context, int cursor) {
        String key = identity(context) + ":" + cursor;
        Page old = CACHE.remove(key);
        if (old != null) bytes -= old.weight;
        PENDING.remove(key);
    }

    public static NBTTagCompound historyContext() {
        NBTTagCompound context = new NBTTagCompound();
        context.setInteger("operation", 2);
        Minecraft mc = Minecraft.getMinecraft();
        context.setInteger("dimension", mc.thePlayer == null ? 0 : mc.thePlayer.dimension);
        context.setLong(
            "generation",
            CanonicalTaskClientStore.getSnapshot()
                .getLong("generation"));
        return context;
    }

    private static final class Request {

        final long sequence, sent;
        long visible;

        Request(long sequence, long now) {
            this.sequence = sequence;
            sent = visible = now;
        }
    }

    private static final class Page {

        final NBTTagCompound data;
        final int weight;
        long visible;

        Page(NBTTagCompound data, int weight, long now) {
            this.data = data;
            this.weight = weight;
            visible = now;
        }
    }

    private static final class Carousel {

        int cursor;
        NBTTagCompound last;
    }
}
