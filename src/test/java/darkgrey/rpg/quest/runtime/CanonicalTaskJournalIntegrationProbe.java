package darkgrey.rpg.quest.runtime;

import java.util.Arrays;
import java.util.Collections;
import java.util.UUID;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;

/** Focused offline proof of the Stage 4 legacy Journal bridge and merge seam. */
public final class CanonicalTaskJournalIntegrationProbe {

    private static final UUID PLAYER = UUID.fromString("11111111-1111-1111-1111-111111111111");

    private CanonicalTaskJournalIntegrationProbe() {}

    public static void main(String[] args) {
        CanonicalTaskJournalEntry active = entry("story-a", "placement-a", CanonicalTaskInstanceStatus.ACTIVE);
        CanonicalTaskJournalEntry settled = entry("story-a", "placement-b", CanonicalTaskInstanceStatus.SETTLED);
        QuestJournalEntry activeLegacy = CanonicalTaskLegacyJournalAdapter.adapt(active);
        QuestJournalEntry settledLegacy = CanonicalTaskLegacyJournalAdapter.adapt(settled);
        require(activeLegacy.getStatus() == QuestStatus.ACTIVE, "active mapping");
        require(settledLegacy.getStatus() == QuestStatus.COMPLETED, "settled mapping");
        require(
            !activeLegacy.getQuestId()
                .equals(settledLegacy.getQuestId()),
            "placement-distinguishing ID");
        require(
            activeLegacy.getQuestId()
                .length() <= 96,
            "bounded ID");
        require(
            !activeLegacy.getDescription()
                .contains("\u0000"),
            "transport-safe description");
        require(
            !activeLegacy.getDescription()
                .contains("identity="),
            "concise public identity context");
        require(
            settledLegacy.getDescription()
                .isEmpty(),
            "missing author description stays empty without exposing internal context");
        require(
            activeLegacy.getObjectiveLines()
                .equals(Arrays.asList("objective")),
            "objective preservation");

        CanonicalTaskJournalEntry mixedObjectives = new CanonicalTaskJournalEntry(
            PLAYER,
            "story-a",
            "placement-mixed",
            "resource-a",
            "Canonical title",
            CanonicalTaskInstanceStatus.ACTIVE,
            1L,
            null,
            null,
            Collections.<String, Boolean>emptyMap(),
            Arrays.asList(
                row("inactive", darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.INACTIVE, 0, 1),
                row("active", darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE, 0, 1),
                row("completed", darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.COMPLETED, 1, 1)));
        require(
            CanonicalTaskLegacyJournalAdapter.adapt(mixedObjectives)
                .getObjectiveLines()
                .equals(Collections.singletonList("active")),
            "only active objectives reach the player Journal");

        CanonicalTaskJournalEntry unsafeText = new CanonicalTaskJournalEntry(
            PLAYER,
            "story-a",
            "placement-control",
            "resource-a",
            "Title\u0000Control",
            CanonicalTaskInstanceStatus.ACTIVE,
            1L,
            null,
            null,
            Collections.<String, Boolean>emptyMap(),
            Collections.singletonList(
                new darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow(
                    "objective",
                    "objective",
                    "kill_entity",
                    darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE,
                    0,
                    1,
                    true,
                    "objective\u0000control")));
        QuestJournalEntry safeText = CanonicalTaskLegacyJournalAdapter.adapt(unsafeText);
        require(
            !safeText.getTitle()
                .contains("\u0000"),
            "transport-safe title");
        require(
            !safeText.getObjectiveLines()
                .get(0)
                .contains("\u0000"),
            "transport-safe objective");

        CanonicalTaskJournalEntry failed = entry(
            "story-a",
            "placement-c",
            CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION);
        require(
            CanonicalTaskLegacyJournalAdapter.adapt(failed)
                .getStatus() == QuestStatus.FAILED,
            "cancelled mapping");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskLegacyJournalAdapter
                    .adapt(entry("story-a", "placement-d", CanonicalTaskInstanceStatus.NOT_STARTED));
            }
        }, "not-started rejection");

        QuestJournalEntry legacy = new QuestJournalEntry(
            "legacy",
            "Legacy",
            "legacy description",
            QuestStatus.ACTIVE,
            Collections.singletonList("legacy objective"));
        java.util.List<QuestJournalEntry> merged = QuestRuntimeService
            .mergeLegacyAndCanonical(Collections.singletonList(legacy), Arrays.asList(active, settled, failed));
        require(merged.size() == 4, "combined entry count");
        require(
            legacy.getQuestId()
                .equals("legacy"),
            "source not mutated");

        System.out.println("CANONICAL_TASK_LEGACY_ADAPTER=PASS");
        System.out.println("CANONICAL_TASK_JOURNAL_MERGE=PASS");
    }

    private static CanonicalTaskJournalEntry entry(String story, String placement, CanonicalTaskInstanceStatus status) {
        boolean settled = status == CanonicalTaskInstanceStatus.SETTLED;
        return new CanonicalTaskJournalEntry(
            PLAYER,
            story,
            placement,
            "resource-a",
            "Canonical title",
            status,
            1L,
            settled ? Long.valueOf(2L) : null,
            settled ? "result" : null,
            Collections.<String, Boolean>emptyMap(),
            Collections.singletonList(
                new darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow(
                    "objective",
                    "objective",
                    "kill_entity",
                    darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE,
                    0,
                    1,
                    true,
                    "objective")));
    }

    private static darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow row(String label,
        darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus status, int current, int required) {
        return new darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow(
            label,
            label,
            "kill_entity",
            status,
            current,
            required,
            true,
            label);
    }

    private static void expectFailure(Runnable action, String label) {
        try {
            action.run();
            throw new IllegalStateException("Expected failure: " + label);
        } catch (IllegalArgumentException expected) {}
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
