package darkgrey.rpg.quest.runtime;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

public final class QuestProgressRecord {

    private final String questId;
    private QuestStatus status;
    private final Map<String, Integer> objectiveProgress = new LinkedHashMap<String, Integer>();

    public QuestProgressRecord(String questId) {
        this(questId, QuestStatus.ACTIVE);
    }

    public QuestProgressRecord(String questId, QuestStatus status) {
        this.questId = questId;
        this.status = status;
    }

    public String getQuestId() {
        return questId;
    }

    public QuestStatus getStatus() {
        return status;
    }

    public void setStatus(QuestStatus status) {
        this.status = status;
    }

    public int getProgress(String objectiveId) {
        Integer progress = objectiveProgress.get(objectiveId);
        return progress == null ? 0 : progress.intValue();
    }

    public void setProgress(String objectiveId, int progress) {
        objectiveProgress.put(objectiveId, Integer.valueOf(Math.max(0, progress)));
    }

    public Map<String, Integer> getObjectiveProgress() {
        return Collections.unmodifiableMap(objectiveProgress);
    }

    public NBTTagCompound writeToNbt() {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString("questId", questId);
        tag.setString("status", status.name());
        NBTTagList progressTags = new NBTTagList();
        for (Map.Entry<String, Integer> entry : objectiveProgress.entrySet()) {
            NBTTagCompound progressTag = new NBTTagCompound();
            progressTag.setString("objectiveId", entry.getKey());
            progressTag.setInteger(
                "amount",
                entry.getValue()
                    .intValue());
            progressTags.appendTag(progressTag);
        }
        tag.setTag("progress", progressTags);
        return tag;
    }

    public static QuestProgressRecord readFromNbt(NBTTagCompound tag) {
        String questId = tag.getString("questId");
        QuestStatus status;
        try {
            status = QuestStatus.valueOf(tag.getString("status"));
        } catch (IllegalArgumentException exception) {
            status = QuestStatus.ACTIVE;
        }
        QuestProgressRecord record = new QuestProgressRecord(questId, status);
        NBTTagList progressTags = tag.getTagList("progress", 10);
        for (int index = 0; index < progressTags.tagCount(); index++) {
            NBTTagCompound progressTag = progressTags.getCompoundTagAt(index);
            record.setProgress(progressTag.getString("objectiveId"), progressTag.getInteger("amount"));
        }
        return record;
    }
}
