package darkgrey.rpg.client;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Detached presentation only. Contains no objective execution or transition logic. */
public final class CanonicalTaskClientStore {

    private static Object world;
    private static long revision = -1;
    private static NBTTagCompound snapshot = empty();

    private CanonicalTaskClientStore() {}

    public static synchronized void synchronizeWorld(Object currentWorld) {
        if (world != currentWorld) {
            world = currentWorld;
            revision = -1;
            snapshot = empty();
        }
    }

    public static void accept(NBTTagCompound data) {
        net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
        synchronizeWorld(mc.theWorld);
        if (mc.theWorld == null || mc.thePlayer == null
            || data == null
            || !data.hasKey("dimension", 3)
            || data.getInteger("dimension") != mc.thePlayer.dimension) return;
        replace(data);
    }

    public static synchronized boolean replace(NBTTagCompound data) {
        if (data == null || !data.hasKey("revision", 4)
            || !data.hasKey("tasks", 9)
            || data.getLong("revision") < 0
            || data.getLong("revision") <= revision) return false;
        NBTTagList tasks = (NBTTagList) data.getTag("tasks");
        if (tasks.tagCount() > 0 && tasks.func_150303_d() != 10) return false;
        if (tasks.tagCount() > 4096) return false;
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            if (!task.hasKey("id", 8) || task.getString("id")
                .isEmpty() || !task.hasKey("title", 8) || !task.hasKey("objectives", 9)) return false;
            NBTTagList objectives = (NBTTagList) task.getTag("objectives");
            if (objectives.tagCount() > 0 && objectives.func_150303_d() != 10) return false;
            if (objectives.tagCount() > 4096) return false;
            for (int j = 0; j < objectives.tagCount(); j++) {
                NBTTagCompound row = objectives.getCompoundTagAt(j);
                if (!row.hasKey("text", 8) || !row.hasKey("current", 3)
                    || !row.hasKey("required", 3)
                    || row.getInteger("current") < 0
                    || row.getInteger("required") < 0) return false;
            }
        }
        snapshot = (NBTTagCompound) data.copy();
        revision = data.getLong("revision");
        return true;
    }

    public static synchronized long getRevision() {
        return revision;
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
