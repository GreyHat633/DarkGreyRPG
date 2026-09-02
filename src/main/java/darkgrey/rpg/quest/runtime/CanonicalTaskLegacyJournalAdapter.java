package darkgrey.rpg.quest.runtime;

import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/**
 * Pure compatibility adapter from the canonical Task Journal projection to
 * the existing Quest Journal transport DTO. The wire schema deliberately
 * stays unchanged while canonical identity is retained in the description.
 */
public final class CanonicalTaskLegacyJournalAdapter {

    private static final Charset UTF_8 = Charset.forName("UTF-8");
    public static final int MAX_QUEST_ID_LENGTH = 96;

    private CanonicalTaskLegacyJournalAdapter() {}

    public static QuestJournalEntry adapt(CanonicalTaskJournalEntry entry) {
        if (entry == null) throw new IllegalArgumentException("Canonical Task Journal entry is required.");
        CanonicalTaskInstanceStatus canonicalStatus = entry.getStatus();
        QuestStatus status;
        if (canonicalStatus == CanonicalTaskInstanceStatus.ACTIVE) status = QuestStatus.ACTIVE;
        else if (canonicalStatus == CanonicalTaskInstanceStatus.SETTLED) status = QuestStatus.COMPLETED;
        else if (canonicalStatus == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
            || canonicalStatus == CanonicalTaskInstanceStatus.ERROR) status = QuestStatus.FAILED;
        else throw new IllegalArgumentException(
            "Canonical Task status cannot enter the Quest Journal: " + canonicalStatus);

        String identity = requireSafeText(entry.getIdentity(), "Canonical Task identity");
        String questId = transportQuestId(identity);
        String description = description(entry, status);
        List<String> objectives = new ArrayList<String>();
        for (CanonicalTaskJournalObjectiveRow row : entry.getObjectiveRows()) {
            if (row.getRuntimeStatus() != CanonicalTaskObjectiveStatus.ACTIVE) continue;
            objectives.add(safe(requireSafeText(row.getDisplayLine(), "Canonical Task objective")));
        }
        return new QuestJournalEntry(
            questId,
            safe(requireSafeText(entry.getTitle(), "Canonical Task title")),
            description,
            status,
            objectives);
    }

    public static QuestJournalEntry toLegacy(CanonicalTaskJournalEntry entry) {
        return adapt(entry);
    }

    public static QuestJournalEntry adaptEntry(CanonicalTaskJournalEntry entry) {
        return adapt(entry);
    }

    public static QuestJournalEntry toQuestJournalEntry(CanonicalTaskJournalEntry entry) {
        return adapt(entry);
    }

    public static QuestJournalEntry toLegacyEntry(CanonicalTaskJournalEntry entry) {
        return adapt(entry);
    }

    public static List<QuestJournalEntry> adapt(List<CanonicalTaskJournalEntry> entries) {
        if (entries == null) throw new IllegalArgumentException("Canonical Task Journal entries are required.");
        List<QuestJournalEntry> result = new ArrayList<QuestJournalEntry>();
        for (CanonicalTaskJournalEntry entry : entries) result.add(adapt(entry));
        return Collections.unmodifiableList(result);
    }

    public static List<QuestJournalEntry> adaptEntries(List<CanonicalTaskJournalEntry> entries) {
        return adapt(entries);
    }

    public static List<QuestJournalEntry> toLegacyEntries(List<CanonicalTaskJournalEntry> entries) {
        return adapt(entries);
    }

    /** Stable, bounded ID suitable for the existing UTF-8 Quest packet field. */
    public static String transportQuestId(CanonicalTaskJournalEntry entry) {
        if (entry == null) throw new IllegalArgumentException("Canonical Task Journal entry is required.");
        return transportQuestId(requireSafeText(entry.getIdentity(), "Canonical Task identity"));
    }

    public static String canonicalQuestId(CanonicalTaskJournalEntry entry) {
        return transportQuestId(entry);
    }

    public static String transportQuestId(java.util.UUID playerUuid, String storyInstanceId,
        String taskNodePlacementId) {
        if (playerUuid == null || storyInstanceId == null
            || storyInstanceId.trim()
                .isEmpty()
            || taskNodePlacementId == null
            || taskNodePlacementId.trim()
                .isEmpty())
            throw new IllegalArgumentException("Canonical Task identity is required.");
        return transportQuestId(playerUuid.toString() + "\u0000" + storyInstanceId + "\u0000" + taskNodePlacementId);
    }

    private static String transportQuestId(String identity) {
        // A digest avoids control separators (the canonical identity uses NUL)
        // and remains bounded even for user-authored story/placement IDs.
        String digest;
        try {
            byte[] bytes = MessageDigest.getInstance("SHA-256")
                .digest(identity.getBytes(UTF_8));
            StringBuilder hex = new StringBuilder(bytes.length * 2);
            for (byte value : bytes) hex.append(String.format("%02x", Integer.valueOf(value & 0xff)));
            digest = hex.toString();
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException("SHA-256 is unavailable.", impossible);
        }
        String result = "task-" + digest;
        if (result.length() > MAX_QUEST_ID_LENGTH) return result.substring(0, MAX_QUEST_ID_LENGTH);
        return result;
    }

    private static String description(CanonicalTaskJournalEntry entry, QuestStatus status) {
        StringBuilder result = new StringBuilder();
        result.append("规范任务 [")
            .append(statusLabel(status))
            .append("]");
        result.append(" 故事实例=")
            .append(safe(entry.getStoryInstanceId()));
        result.append(" 任务节点=")
            .append(safe(entry.getTaskNodePlacementId()));
        result.append(" 任务资源=")
            .append(safe(entry.getTaskResourceId()));
        result.append(" 结算出口=")
            .append(entry.getSettledResultSlot() == null ? "无" : safe(entry.getSettledResultSlot()));
        return result.toString();
    }

    private static String statusLabel(QuestStatus status) {
        if (status == QuestStatus.ACTIVE) return "进行中";
        if (status == QuestStatus.COMPLETED) return "已完成";
        return "失败";
    }

    private static String requireSafeText(String value, String label) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(label + " is required.");
        return value;
    }

    private static String safe(String value) {
        if (value == null) return "";
        StringBuilder result = new StringBuilder(value.length());
        for (int i = 0; i < value.length(); i++) {
            char character = value.charAt(i);
            result.append(character < 0x20 || character == 0x7f ? '?' : character);
        }
        return result.toString();
    }
}
