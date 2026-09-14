package darkgrey.rpg.task.journal;

import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Immutable, UI-independent projection of one canonical Task objective node. */
public final class CanonicalTaskJournalObjectiveRow {

    private final String objectiveId;
    private final String description;
    private final String objectiveType;
    private final CanonicalTaskObjectiveStatus status;
    private final int currentProgress;
    private final int requiredProgress;
    private final boolean required;
    private final String displayLine;
    private final String submitActorId;
    private final Integer regionX;
    private final Integer regionY;
    private final Integer regionZ;

    public CanonicalTaskJournalObjectiveRow(String objectiveId, String description, String objectiveType,
        CanonicalTaskObjectiveStatus status, int currentProgress, int requiredProgress, boolean required,
        String displayLine) {
        this(
            objectiveId,
            description,
            objectiveType,
            status,
            currentProgress,
            requiredProgress,
            required,
            displayLine,
            null,
            null,
            null,
            null);
    }

    public CanonicalTaskJournalObjectiveRow(String objectiveId, String description, String objectiveType,
        CanonicalTaskObjectiveStatus status, int currentProgress, int requiredProgress, boolean required,
        String displayLine, String submitActorId, Integer regionX, Integer regionY, Integer regionZ) {
        if (blank(objectiveId) || blank(description)
            || blank(objectiveType)
            || status == null
            || currentProgress < 0
            || requiredProgress <= 0
            || currentProgress > requiredProgress
            || (status == CanonicalTaskObjectiveStatus.INACTIVE && currentProgress != 0)
            || (status == CanonicalTaskObjectiveStatus.ACTIVE && currentProgress >= requiredProgress)
            || (status == CanonicalTaskObjectiveStatus.COMPLETED && currentProgress != requiredProgress)
            || blank(displayLine)) throw new IllegalArgumentException("Invalid canonical Task Journal objective row.");
        this.objectiveId = objectiveId;
        this.description = description;
        this.objectiveType = objectiveType;
        this.status = status;
        this.currentProgress = currentProgress;
        this.requiredProgress = requiredProgress;
        this.required = required;
        this.displayLine = displayLine;
        this.submitActorId = submitActorId;
        this.regionX = regionX;
        this.regionY = regionY;
        this.regionZ = regionZ;
    }

    public String getObjectiveId() {
        return objectiveId;
    }

    public String getId() {
        return objectiveId;
    }

    public String getDescription() {
        return description;
    }

    public String getObjectiveType() {
        return objectiveType;
    }

    public String getType() {
        return objectiveType;
    }

    public CanonicalTaskObjectiveStatus getStatus() {
        return status;
    }

    public CanonicalTaskObjectiveStatus getRuntimeStatus() {
        return status;
    }

    public int getCurrentProgress() {
        return currentProgress;
    }

    public int getCurrent() {
        return currentProgress;
    }

    public int getRequiredProgress() {
        return requiredProgress;
    }

    public int getRequired() {
        return requiredProgress;
    }

    public int getRequiredAmount() {
        return requiredProgress;
    }

    public boolean isRequired() {
        return required;
    }

    public boolean getRequiredFlag() {
        return required;
    }

    public String getDisplayLine() {
        return displayLine;
    }

    public String getLine() {
        return displayLine;
    }

    public String getSubmitActorId() {
        return submitActorId;
    }

    public boolean hasRegionCoordinates() {
        return regionX != null && regionY != null && regionZ != null;
    }

    public Integer getRegionX() {
        return regionX;
    }

    public Integer getRegionY() {
        return regionY;
    }

    public Integer getRegionZ() {
        return regionZ;
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof CanonicalTaskJournalObjectiveRow)) return false;
        CanonicalTaskJournalObjectiveRow that = (CanonicalTaskJournalObjectiveRow) other;
        return objectiveId.equals(that.objectiveId) && description.equals(that.description)
            && objectiveType.equals(that.objectiveType)
            && status == that.status
            && currentProgress == that.currentProgress
            && requiredProgress == that.requiredProgress
            && required == that.required
            && displayLine.equals(that.displayLine)
            && equalsValue(submitActorId, that.submitActorId)
            && equalsValue(regionX, that.regionX)
            && equalsValue(regionY, that.regionY)
            && equalsValue(regionZ, that.regionZ);
    }

    @Override
    public int hashCode() {
        int result = objectiveId.hashCode();
        result = 31 * result + description.hashCode();
        result = 31 * result + objectiveType.hashCode();
        result = 31 * result + status.hashCode();
        result = 31 * result + currentProgress;
        result = 31 * result + requiredProgress;
        result = 31 * result + (required ? 1 : 0);
        result = 31 * result + displayLine.hashCode();
        result = 31 * result + (submitActorId == null ? 0 : submitActorId.hashCode());
        result = 31 * result + (regionX == null ? 0 : regionX.hashCode());
        result = 31 * result + (regionY == null ? 0 : regionY.hashCode());
        return 31 * result + (regionZ == null ? 0 : regionZ.hashCode());
    }

    private static boolean equalsValue(Object left, Object right) {
        return left == null ? right == null : left.equals(right);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
