package darkgrey.rpg.story.runtime;

import java.util.Map;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

public final class PlayerStoryData extends WorldSavedData {

    private static final String DATA_NAME = "darkgrey_rpg_player_stories";
    private final StoryVariableLedger ledger = new StoryVariableLedger();

    public PlayerStoryData() {
        super(DATA_NAME);
    }

    public PlayerStoryData(String name) {
        super(name);
    }

    public static PlayerStoryData get(EntityPlayer player) {
        WorldServer overworld = MinecraftServer.getServer()
            .worldServerForDimension(0);
        MapStorage storage = overworld.mapStorage;
        PlayerStoryData data = (PlayerStoryData) storage.loadData(PlayerStoryData.class, DATA_NAME);
        if (data == null) {
            data = new PlayerStoryData();
            storage.setData(DATA_NAME, data);
        }
        return data;
    }

    public String getVariable(EntityPlayer player, String storyId, String variable) {
        return ledger.get(
            player.getUniqueID()
                .toString(),
            storyId,
            variable);
    }

    public void setVariable(EntityPlayer player, String storyId, String variable, String value) {
        ledger.set(
            player.getUniqueID()
                .toString(),
            storyId,
            variable,
            value);
        markDirty();
    }

    public Map<String, Map<String, String>> getVariables(EntityPlayer player) {
        return ledger.getStories(
            player.getUniqueID()
                .toString());
    }

    public void clearStory(EntityPlayer player, String storyId) {
        ledger.clearStory(
            player.getUniqueID()
                .toString(),
            storyId);
        markDirty();
    }

    public NBTTagCompound snapshotPlayer(EntityPlayer player) {
        return ledger.snapshotPlayer(
            player.getUniqueID()
                .toString());
    }

    public void restorePlayer(EntityPlayer player, NBTTagCompound snapshot) {
        ledger.restorePlayer(
            player.getUniqueID()
                .toString(),
            snapshot);
        markDirty();
    }

    @Override
    public void readFromNBT(NBTTagCompound root) {
        ledger.readFromNbt(root);
    }

    @Override
    public void writeToNBT(NBTTagCompound root) {
        ledger.writeToNbt(root);
    }
}
