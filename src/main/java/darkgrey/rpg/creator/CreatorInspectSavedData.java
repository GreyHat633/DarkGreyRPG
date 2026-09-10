package darkgrey.rpg.creator;

import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

/** Creator preferences belong to the save, independently of Story persistence. */
public final class CreatorInspectSavedData extends WorldSavedData {

    public static final String NAME = "darkgrey_rpg_creator_inspect";
    private final Set<UUID> enabled = new HashSet<UUID>();

    public CreatorInspectSavedData() {
        this(NAME);
    }

    public CreatorInspectSavedData(String name) {
        super(name);
    }

    public static CreatorInspectSavedData get() {
        MapStorage storage = MinecraftServer.getServer()
            .worldServerForDimension(0).mapStorage;
        CreatorInspectSavedData data = (CreatorInspectSavedData) storage.loadData(CreatorInspectSavedData.class, NAME);
        if (data == null) {
            data = new CreatorInspectSavedData();
            storage.setData(NAME, data);
        }
        return data;
    }

    public boolean enabled(UUID player) {
        return enabled.contains(player);
    }

    public boolean toggle(UUID player) {
        if (player == null) throw new IllegalArgumentException("Player UUID required");
        if (!enabled.remove(player)) enabled.add(player);
        markDirty();
        return enabled(player);
    }

    public static boolean effective(boolean persistent, boolean goggles) {
        return persistent || goggles;
    }

    @Override
    public void readFromNBT(NBTTagCompound root) {
        enabled.clear();
        NBTTagList players = root.getTagList("players", 10);
        for (int i = 0; i < players.tagCount(); i++) {
            NBTTagCompound tag = players.getCompoundTagAt(i);
            if (tag.hasKey("most", 4) && tag.hasKey("least", 4))
                enabled.add(new UUID(tag.getLong("most"), tag.getLong("least")));
        }
    }

    @Override
    public void writeToNBT(NBTTagCompound root) {
        NBTTagList players = new NBTTagList();
        for (UUID player : enabled) {
            NBTTagCompound tag = new NBTTagCompound();
            tag.setLong("most", player.getMostSignificantBits());
            tag.setLong("least", player.getLeastSignificantBits());
            players.appendTag(tag);
        }
        root.setTag("players", players);
    }
}
