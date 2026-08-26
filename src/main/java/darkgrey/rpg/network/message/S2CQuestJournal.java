package darkgrey.rpg.network.message;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.QuestClientController;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.quest.runtime.QuestStatus;
import io.netty.buffer.ByteBuf;

public final class S2CQuestJournal implements IMessage {

    private List<QuestJournalEntry> entries = Collections.emptyList();

    public S2CQuestJournal() {}

    public S2CQuestJournal(List<QuestJournalEntry> entries) {
        this.entries = Collections.unmodifiableList(new ArrayList<QuestJournalEntry>(entries));
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        int count = buffer.readUnsignedShort();
        if (count > 1024) {
            throw new IllegalArgumentException("Too many Quest journal entries");
        }
        List<QuestJournalEntry> decoded = new ArrayList<QuestJournalEntry>();
        for (int index = 0; index < count; index++) {
            String questId = readString(buffer, 96);
            String title = readString(buffer, 512);
            String description = readString(buffer, 8192);
            int ordinal = buffer.readUnsignedByte();
            if (ordinal >= QuestStatus.values().length) {
                throw new IllegalArgumentException("Invalid Quest status");
            }
            int objectiveCount = buffer.readUnsignedShort();
            if (objectiveCount > 1024) {
                throw new IllegalArgumentException("Too many Quest objectives");
            }
            List<String> lines = new ArrayList<String>();
            for (int objectiveIndex = 0; objectiveIndex < objectiveCount; objectiveIndex++) {
                lines.add(readString(buffer, 2048));
            }
            decoded.add(new QuestJournalEntry(questId, title, description, QuestStatus.values()[ordinal], lines));
        }
        entries = Collections.unmodifiableList(decoded);
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (entries.size() > 1024) {
            throw new IllegalArgumentException("Too many Quest journal entries");
        }
        buffer.writeShort(entries.size());
        for (QuestJournalEntry entry : entries) {
            writeString(buffer, entry.getQuestId(), 96);
            writeString(buffer, entry.getTitle(), 512);
            writeString(buffer, entry.getDescription(), 8192);
            buffer.writeByte(
                entry.getStatus()
                    .ordinal());
            if (entry.getObjectiveLines()
                .size() > 1024) {
                throw new IllegalArgumentException("Too many Quest objectives");
            }
            buffer.writeShort(
                entry.getObjectiveLines()
                    .size());
            for (String line : entry.getObjectiveLines()) {
                writeString(buffer, line, 2048);
            }
        }
    }

    private static String readString(ByteBuf buffer, int maximumLength) {
        String value = ByteBufUtils.readUTF8String(buffer);
        if (value.length() > maximumLength) {
            throw new IllegalArgumentException("Quest journal string exceeds limit");
        }
        return value;
    }

    private static void writeString(ByteBuf buffer, String value, int maximumLength) {
        if (value.length() > maximumLength) {
            throw new IllegalArgumentException("Quest journal string exceeds limit");
        }
        ByteBufUtils.writeUTF8String(buffer, value);
    }

    public List<QuestJournalEntry> getEntries() {
        return entries;
    }

    public static final class Handler implements IMessageHandler<S2CQuestJournal, IMessage> {

        @Override
        public IMessage onMessage(final S2CQuestJournal message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    QuestClientController.openJournal(message.getEntries());
                }
            });
            return null;
        }
    }
}
