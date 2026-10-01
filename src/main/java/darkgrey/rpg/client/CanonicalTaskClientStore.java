package darkgrey.rpg.client;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Detached presentation only. Contains no objective execution or transition logic. */
public final class CanonicalTaskClientStore {

    private static Object world;
    private static long revision = -1;
    private static NBTTagCompound snapshot = empty();
    private static byte[][] assembling;
    private static long assemblingRevision = -1;
    private static int assemblingBytes, assemblingTotal;
    private static long assemblingStarted;
    private static final java.util.Map<String, Long> pendingSince = new java.util.HashMap<String, Long>();

    private CanonicalTaskClientStore() {}

    public static synchronized void synchronizeWorld(Object currentWorld) {
        if (world != currentWorld) {
            world = currentWorld;
            revision = -1;
            snapshot = empty();
            pendingSince.clear();
            assembling = null;
            darkgrey.rpg.client.TaskPresentationPages.clear();
            TaskNotificationCards.clear();
        }
    }

    public static void accept(NBTTagCompound data) {
        net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
        synchronizeWorld(mc.theWorld);
        if (mc.theWorld == null || mc.thePlayer == null
            || data == null
            || !data.hasKey("dimension", 3)
            || data.getInteger("dimension") != mc.thePlayer.dimension) return;
        if (data.hasKey("payload", 7)) {
            data = assemble(data);
            if (data == null) return;
        }
        long before = getRevision();
        if (replace(data)) {
            TaskTrackerClient.accept(data, before < 0);
            if (before >= 0) TaskNotificationCards.accept(data.getTagList("notifications", 10), System.nanoTime());
        }
    }

    private static synchronized NBTTagCompound assemble(NBTTagCompound part) {
        long incoming = part.getLong("revision");
        int count = part.getInteger("parts"), index = part.getInteger("part"), total = part.getInteger("total");
        byte[] bytes = part.getByteArray("payload");
        int chunk = darkgrey.rpg.creator.TaskSnapshotTransport.CHUNK;
        if (incoming <= revision || count < 1
            || total < 1
            || total > darkgrey.rpg.creator.TaskSnapshotTransport.MAX_BYTES
            || count != (total + chunk - 1) / chunk
            || index < 0
            || index >= count
            || bytes.length != Math.min(chunk, total - index * chunk)) return null;
        if (assembling != null && System.nanoTime() - assemblingStarted > 10000000000L) assembling = null;
        if (assembling == null || incoming > assemblingRevision) {
            assembling = new byte[count][];
            assemblingRevision = incoming;
            assemblingTotal = total;
            assemblingBytes = 0;
            assemblingStarted = System.nanoTime();
        }
        if (incoming != assemblingRevision || count != assembling.length || total != assemblingTotal) return null;
        if (assembling[index] == null) {
            assembling[index] = bytes;
            assemblingBytes += bytes.length;
        }
        if (assemblingBytes != total) return null;
        byte[] raw = new byte[total];
        int offset = 0;
        for (byte[] value : assembling) {
            System.arraycopy(value, 0, raw, offset, value.length);
            offset += value.length;
        }
        assembling = null;
        try {
            java.io.DataInputStream input = new java.io.DataInputStream(new java.io.ByteArrayInputStream(raw));
            NBTTagCompound result = net.minecraft.nbt.CompressedStreamTools.func_152456_a(
                input,
                new net.minecraft.nbt.NBTSizeTracker(2L * darkgrey.rpg.creator.TaskSnapshotTransport.MAX_BYTES));
            if (input.available() != 0 || result.getLong("revision") != incoming
                || result.getInteger("dimension") != part.getInteger("dimension")) return null;
            return result;
        } catch (java.io.IOException invalid) {
            return null;
        }
    }

    public static synchronized boolean replace(NBTTagCompound data) {
        if (data == null || !data.hasKey("revision", 4)
            || !data.hasKey("tasks", 9)
            || data.getLong("revision") < 0
            || data.getLong("revision") <= revision) return false;
        NBTTagList tasks = (NBTTagList) data.getTag("tasks");
        if (tasks.tagCount() > 0 && tasks.func_150303_d() != 10) return false;
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            if (!task.hasKey("id", 8) || task.getString("id")
                .isEmpty() || !task.hasKey("title", 8) || !task.hasKey("objectives", 9)) return false;
            NBTTagList objectives = (NBTTagList) task.getTag("objectives");
            if (objectives.tagCount() > 0 && objectives.func_150303_d() != 10) return false;
            for (int j = 0; j < objectives.tagCount(); j++) {
                NBTTagCompound row = objectives.getCompoundTagAt(j);
                if (!row.hasKey("text", 8) || !row.hasKey("current", 3)
                    || !row.hasKey("required", 3)
                    || row.getInteger("current") < 0
                    || row.getInteger("required") < 0) return false;
            }
        }
        snapshot = (NBTTagCompound) data.copy();
        java.util.Set<String> pending = new java.util.HashSet<String>();
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            if (task.getBoolean("pending_rewards")) {
                String key = task.getString("tracking_id");
                pending.add(key);
                if (!pendingSince.containsKey(key)) pendingSince.put(key, System.nanoTime());
            }
        }
        pendingSince.keySet()
            .retainAll(pending);
        revision = data.getLong("revision");
        return true;
    }

    public static synchronized long getRevision() {
        return revision;
    }

    public static synchronized boolean delayedReward(String identity) {
        Long since = pendingSince.get(identity);
        return since != null && System.nanoTime() - since > 3000000000L;
    }

    public static synchronized NBTTagCompound getSnapshot() {
        return (NBTTagCompound) snapshot.copy();
    }

    private static NBTTagCompound empty() {
        NBTTagCompound result = new NBTTagCompound();
        result.setTag("tasks", new NBTTagList());
        return result;
    }
}
