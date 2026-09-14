package darkgrey.rpg.task.journal;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;

/** Immutable, detached journal projection of one canonical TaskInstance. */
public final class CanonicalTaskJournalEntry {

    private final UUID playerUuid;
    private final String storyInstanceId;
    private final String taskNodePlacementId;
    private final String taskResourceId;
    private final String title;
    private final String description;
    private final boolean pendingRewards;
    private final CanonicalTaskInstanceStatus status;
    private final long activationTime;
    private final Long settlementTime;
    private final String settledResultSlot;
    private final Map<String, Boolean> publicLogicState;
    private final List<CanonicalTaskJournalObjectiveRow> objectiveRows;

    public CanonicalTaskJournalEntry(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, String title, CanonicalTaskInstanceStatus status, long activationTime,
        Long settlementTime, String settledResultSlot, Map<String, Boolean> publicLogicState,
        List<CanonicalTaskJournalObjectiveRow> objectiveRows) {
        this(
            playerUuid,
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            title,
            status,
            activationTime,
            settlementTime,
            settledResultSlot,
            publicLogicState,
            objectiveRows,
            "");
    }

    public CanonicalTaskJournalEntry(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, String title, CanonicalTaskInstanceStatus status, long activationTime,
        Long settlementTime, String settledResultSlot, Map<String, Boolean> publicLogicState,
        List<CanonicalTaskJournalObjectiveRow> objectiveRows, String description) {
        this(
            playerUuid,
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            title,
            status,
            activationTime,
            settlementTime,
            settledResultSlot,
            publicLogicState,
            objectiveRows,
            description,
            false);
    }

    public CanonicalTaskJournalEntry(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, String title, CanonicalTaskInstanceStatus status, long activationTime,
        Long settlementTime, String settledResultSlot, Map<String, Boolean> publicLogicState,
        List<CanonicalTaskJournalObjectiveRow> objectiveRows, String description, boolean pendingRewards) {
        this.pendingRewards = pendingRewards;
        if (description == null) throw new IllegalArgumentException("Task description is required.");
        this.description = description;
        if (playerUuid == null || blank(storyInstanceId)
            || blank(taskNodePlacementId)
            || blank(taskResourceId)
            || blank(title)
            || status == null
            || activationTime <= 0
            || (settlementTime != null
                && (settlementTime.longValue() <= 0 || settlementTime.longValue() < activationTime))
            || (status == CanonicalTaskInstanceStatus.SETTLED) != (settlementTime != null)
            || (status == CanonicalTaskInstanceStatus.SETTLED && blank(settledResultSlot))
            || (status != CanonicalTaskInstanceStatus.SETTLED && settledResultSlot != null)
            || publicLogicState == null
            || objectiveRows == null) throw new IllegalArgumentException("Invalid canonical Task Journal entry.");
        for (Map.Entry<String, Boolean> logic : publicLogicState.entrySet())
            if (blank(logic.getKey()) || logic.getValue() == null)
                throw new IllegalArgumentException("Invalid canonical Task Journal public Logic state.");
        for (CanonicalTaskJournalObjectiveRow row : objectiveRows)
            if (row == null) throw new IllegalArgumentException("Invalid canonical Task Journal objective row.");
        this.playerUuid = playerUuid;
        this.storyInstanceId = storyInstanceId;
        this.taskNodePlacementId = taskNodePlacementId;
        this.taskResourceId = taskResourceId;
        this.title = title;
        this.status = status;
        this.activationTime = activationTime;
        this.settlementTime = settlementTime;
        this.settledResultSlot = settledResultSlot;
        this.publicLogicState = Collections.unmodifiableMap(new LinkedHashMap<String, Boolean>(publicLogicState));
        this.objectiveRows = Collections
            .unmodifiableList(new ArrayList<CanonicalTaskJournalObjectiveRow>(objectiveRows));
    }

    public boolean hasPendingRewards() {
        return pendingRewards;
    }

    public UUID getPlayerUuid() {
        return playerUuid;
    }

    public UUID getPlayerId() {
        return playerUuid;
    }

    public String getStoryInstanceId() {
        return storyInstanceId;
    }

    public String getStoryId() {
        return storyInstanceId;
    }

    public String getTaskNodePlacementId() {
        return taskNodePlacementId;
    }

    public String getTaskNodePlacement() {
        return taskNodePlacementId;
    }

    public String getTaskResourceId() {
        return taskResourceId;
    }

    public String getResourceId() {
        return taskResourceId;
    }

    public String getTitle() {
        return title;
    }

    public String getDescription() {
        return description;
    }

    public String getDisplayName() {
        return title;
    }

    public CanonicalTaskInstanceStatus getStatus() {
        return status;
    }

    public CanonicalTaskInstanceStatus getInstanceStatus() {
        return status;
    }

    public long getActivationTime() {
        return activationTime;
    }

    public long getActivationTimestamp() {
        return activationTime;
    }

    public Long getSettlementTime() {
        return settlementTime;
    }

    public Long getSettlementTimestamp() {
        return settlementTime;
    }

    public String getSettledResultSlot() {
        return settledResultSlot;
    }

    public String getSettledResultPortId() {
        return settledResultSlot;
    }

    public String getResultPortId() {
        return settledResultSlot;
    }

    public String getResult() {
        return settledResultSlot;
    }

    public Map<String, Boolean> getPublicLogicState() {
        return publicLogicState;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogicState;
    }

    public Map<String, Boolean> getPublicLogic() {
        return publicLogicState;
    }

    public List<CanonicalTaskJournalObjectiveRow> getObjectiveRows() {
        return objectiveRows;
    }

    public List<CanonicalTaskJournalObjectiveRow> getObjectives() {
        return objectiveRows;
    }

    public List<String> getObjectiveLines() {
        List<String> lines = new ArrayList<String>();
        for (CanonicalTaskJournalObjectiveRow row : objectiveRows) lines.add(row.getDisplayLine());
        return Collections.unmodifiableList(lines);
    }

    /** Canonical TaskInstance identity, matching the persistence key convention. */
    public String getIdentity() {
        return identity(playerUuid, storyInstanceId, taskNodePlacementId);
    }

    public String getCanonicalIdentity() {
        return getIdentity();
    }

    public String getTaskInstanceIdentity() {
        return getIdentity();
    }

    public String getInstanceIdentity() {
        return getIdentity();
    }

    public String getSettledResultSlotId() {
        return settledResultSlot;
    }

    public String getSettlementResult() {
        return settledResultSlot;
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof CanonicalTaskJournalEntry)) return false;
        CanonicalTaskJournalEntry that = (CanonicalTaskJournalEntry) other;
        return playerUuid.equals(that.playerUuid) && storyInstanceId.equals(that.storyInstanceId)
            && taskNodePlacementId.equals(that.taskNodePlacementId)
            && taskResourceId.equals(that.taskResourceId)
            && title.equals(that.title)
            && description.equals(that.description)
            && pendingRewards == that.pendingRewards
            && status == that.status
            && activationTime == that.activationTime
            && (settlementTime == null ? that.settlementTime == null : settlementTime.equals(that.settlementTime))
            && (settledResultSlot == null ? that.settledResultSlot == null
                : settledResultSlot.equals(that.settledResultSlot))
            && publicLogicState.equals(that.publicLogicState)
            && objectiveRows.equals(that.objectiveRows);
    }

    @Override
    public int hashCode() {
        int result = playerUuid.hashCode();
        result = 31 * result + storyInstanceId.hashCode();
        result = 31 * result + taskNodePlacementId.hashCode();
        result = 31 * result + taskResourceId.hashCode();
        result = 31 * result + title.hashCode();
        result = 31 * result + description.hashCode();
        result = 31 * result + (pendingRewards ? 1 : 0);
        result = 31 * result + status.hashCode();
        result = 31 * result + (int) (activationTime ^ (activationTime >>> 32));
        result = 31 * result + (settlementTime == null ? 0 : settlementTime.hashCode());
        result = 31 * result + (settledResultSlot == null ? 0 : settledResultSlot.hashCode());
        result = 31 * result + publicLogicState.hashCode();
        return 31 * result + objectiveRows.hashCode();
    }

    static String identity(UUID playerUuid, String storyInstanceId, String taskNodePlacementId) {
        return playerUuid.toString() + "\u0000" + storyInstanceId + "\u0000" + taskNodePlacementId;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
