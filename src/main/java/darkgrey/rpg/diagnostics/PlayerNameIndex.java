package darkgrey.rpg.diagnostics;

import java.util.Locale;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

/** Minimal verified name-to-identity index; writes only on a real login. */
public final class PlayerNameIndex extends WorldSavedData {

    public static final String NAME = "darkgrey_rpg_player_names";
    private NBTTagCompound names = new NBTTagCompound();

    public PlayerNameIndex(String name) {
        super(name);
    }

    public static void record(EntityPlayerMP player) {
        MapStorage storage = MinecraftServer.getServer()
            .worldServerForDimension(0).mapStorage;
        PlayerNameIndex index = (PlayerNameIndex) storage.loadData(PlayerNameIndex.class, NAME);
        if (index == null) {
            index = new PlayerNameIndex(NAME);
            storage.setData(NAME, index);
        }
        String key = player.getCommandSenderName()
            .toLowerCase(Locale.ROOT);
        String id = player.getUniqueID()
            .toString();
        if (!id.equals(index.names.getString(key))) {
            index.names.setString(key, id);
            index.markDirty();
        }
    }

    public static UUID lookup(String name) {
        MinecraftServer server = MinecraftServer.getServer();
        for (Object value : server.getConfigurationManager().playerEntityList) {
            EntityPlayerMP player = (EntityPlayerMP) value;
            if (name.equalsIgnoreCase(player.getCommandSenderName())) return player.getUniqueID();
        }
        PlayerNameIndex index = (PlayerNameIndex) server.worldServerForDimension(0).mapStorage
            .loadData(PlayerNameIndex.class, NAME);
        if (index != null && index.names.hasKey(name.toLowerCase(Locale.ROOT), 8))
            return UUID.fromString(index.names.getString(name.toLowerCase(Locale.ROOT)));
        // Read local historical profiles only. Never invoke profile lookup/authentication or guess offline UUIDs.
        java.io.File file = server.getFile("usercache.json");
        if (!file.isFile()) return null;
        try (java.io.Reader reader = new java.io.InputStreamReader(
            new java.io.FileInputStream(file),
            java.nio.charset.StandardCharsets.UTF_8)) {
            UUID found = null;
            for (com.google.gson.JsonElement element : new com.google.gson.JsonParser().parse(reader)
                .getAsJsonArray()) {
                com.google.gson.JsonObject row = element.getAsJsonObject();
                if (!name.equalsIgnoreCase(
                    row.get("name")
                        .getAsString()))
                    continue;
                UUID id = UUID.fromString(
                    row.get("uuid")
                        .getAsString());
                if (found != null && !found.equals(id)) throw new IllegalStateException("本服名称记录存在歧义。");
                found = id;
            }
            return found;
        } catch (java.io.IOException invalid) {
            throw new IllegalStateException("无法读取本服名称缓存。", invalid);
        }
    }

    @Override
    public void readFromNBT(NBTTagCompound root) {
        names = (NBTTagCompound) root.getCompoundTag("names")
            .copy();
    }

    @Override
    public void writeToNBT(NBTTagCompound root) {
        root.setTag("names", names.copy());
    }
}
