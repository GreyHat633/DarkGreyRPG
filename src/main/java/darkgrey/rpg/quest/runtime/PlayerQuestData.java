package darkgrey.rpg.quest.runtime;

import java.util.Collection;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

public final class PlayerQuestData extends WorldSavedData {

    private static final String DATA_NAME = "darkgrey_rpg_player_quests";

    private final QuestLedger ledger = new QuestLedger();

    public PlayerQuestData() {
        super(DATA_NAME);
    }

    public PlayerQuestData(String name) {
        super(name);
    }

    public static PlayerQuestData get(EntityPlayer player) {
        WorldServer overworld = MinecraftServer.getServer()
            .worldServerForDimension(0);
        MapStorage storage = overworld.mapStorage;
        PlayerQuestData data = (PlayerQuestData) storage.loadData(PlayerQuestData.class, DATA_NAME);
        if (data == null) {
            data = new PlayerQuestData();
            storage.setData(DATA_NAME, data);
        }
        return data;
    }

    public QuestProgressRecord getQuest(EntityPlayer player, String questId) {
        return ledger.getQuest(playerId(player), questId);
    }

    public Collection<QuestProgressRecord> getQuests(EntityPlayer player) {
        return ledger.getQuests(playerId(player));
    }

    public QuestProgressRecord startQuest(EntityPlayer player, String questId) {
        QuestProgressRecord record = ledger.getQuest(playerId(player), questId);
        if (record != null) {
            return record;
        }
        record = ledger.startQuest(playerId(player), questId);
        markDirty();
        return record;
    }

    public boolean resetQuest(EntityPlayer player, String questId) {
        boolean removed = ledger.resetQuest(playerId(player), questId);
        if (removed) {
            markDirty();
        }
        return removed;
    }

    public NBTTagCompound snapshotPlayer(EntityPlayer player) {
        return ledger.snapshotPlayer(playerId(player));
    }

    public void restorePlayer(EntityPlayer player, NBTTagCompound snapshot) {
        ledger.restorePlayer(playerId(player), snapshot);
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

    private static String playerId(EntityPlayer player) {
        return player.getUniqueID()
            .toString();
    }
}
