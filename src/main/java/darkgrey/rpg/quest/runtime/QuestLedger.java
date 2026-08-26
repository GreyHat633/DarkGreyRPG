package darkgrey.rpg.quest.runtime;

import java.util.Collection;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

public final class QuestLedger {

    private final Map<String, Map<String, QuestProgressRecord>> players = new LinkedHashMap<String, Map<String, QuestProgressRecord>>();

    public QuestProgressRecord getQuest(String playerId, String questId) {
        return playerRecords(playerId, false).get(questId);
    }

    public Collection<QuestProgressRecord> getQuests(String playerId) {
        return Collections.unmodifiableCollection(playerRecords(playerId, false).values());
    }

    public QuestProgressRecord startQuest(String playerId, String questId) {
        Map<String, QuestProgressRecord> records = playerRecords(playerId, true);
        QuestProgressRecord record = records.get(questId);
        if (record == null) {
            record = new QuestProgressRecord(questId);
            records.put(questId, record);
        }
        return record;
    }

    public boolean resetQuest(String playerId, String questId) {
        Map<String, QuestProgressRecord> records = players.get(playerId);
        return records != null && records.remove(questId) != null;
    }

    public NBTTagCompound snapshotPlayer(String playerId) {
        NBTTagCompound snapshot = new NBTTagCompound();
        NBTTagList questTags = new NBTTagList();
        for (QuestProgressRecord record : playerRecords(playerId, false).values()) {
            questTags.appendTag(record.writeToNbt());
        }
        snapshot.setTag("quests", questTags);
        return snapshot;
    }

    public void restorePlayer(String playerId, NBTTagCompound snapshot) {
        Map<String, QuestProgressRecord> records = new LinkedHashMap<String, QuestProgressRecord>();
        NBTTagList questTags = snapshot.getTagList("quests", 10);
        for (int index = 0; index < questTags.tagCount(); index++) {
            QuestProgressRecord record = QuestProgressRecord.readFromNbt(questTags.getCompoundTagAt(index));
            if (!record.getQuestId()
                .isEmpty()) {
                records.put(record.getQuestId(), record);
            }
        }
        players.put(playerId, records);
    }

    public void readFromNbt(NBTTagCompound root) {
        players.clear();
        NBTTagList playerTags = root.getTagList("players", 10);
        for (int playerIndex = 0; playerIndex < playerTags.tagCount(); playerIndex++) {
            NBTTagCompound playerTag = playerTags.getCompoundTagAt(playerIndex);
            Map<String, QuestProgressRecord> records = new LinkedHashMap<String, QuestProgressRecord>();
            NBTTagList questTags = playerTag.getTagList("quests", 10);
            for (int questIndex = 0; questIndex < questTags.tagCount(); questIndex++) {
                QuestProgressRecord record = QuestProgressRecord.readFromNbt(questTags.getCompoundTagAt(questIndex));
                if (!record.getQuestId()
                    .isEmpty()) {
                    records.put(record.getQuestId(), record);
                }
            }
            players.put(playerTag.getString("playerUuid"), records);
        }
    }

    public void writeToNbt(NBTTagCompound root) {
        NBTTagList playerTags = new NBTTagList();
        for (Map.Entry<String, Map<String, QuestProgressRecord>> playerEntry : players.entrySet()) {
            NBTTagCompound playerTag = new NBTTagCompound();
            playerTag.setString("playerUuid", playerEntry.getKey());
            NBTTagList questTags = new NBTTagList();
            for (QuestProgressRecord record : playerEntry.getValue()
                .values()) {
                questTags.appendTag(record.writeToNbt());
            }
            playerTag.setTag("quests", questTags);
            playerTags.appendTag(playerTag);
        }
        root.setTag("players", playerTags);
    }

    private Map<String, QuestProgressRecord> playerRecords(String playerId, boolean create) {
        Map<String, QuestProgressRecord> records = players.get(playerId);
        if (records == null) {
            if (!create) {
                return Collections.emptyMap();
            }
            records = new LinkedHashMap<String, QuestProgressRecord>();
            players.put(playerId, records);
        }
        return records;
    }
}
