package darkgrey.rpg.story.canonical.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Detached immutable state of the pure canonical Story cursor. */
public final class CanonicalStorySnapshot {

    private final String resourceId;
    private final String resourceFingerprint;
    private final CanonicalStoryStatus status;
    private final CanonicalStoryRepeatPolicy repeatPolicy;
    private final String triggerPortId;
    private final String currentNodeId;
    private final String currentInputPortId;
    private final CanonicalStoryWaitKind waitKind;
    private final String waitResourceId;
    private final Integer waitDimension;
    private final Double waitX;
    private final Double waitY;
    private final Double waitZ;
    private final Double waitRadius;
    private final Map<String, Boolean> logicValues;
    private final String targetStoryId;
    private final Map<String, Boolean> externalLogicInputs;
    private final Boolean waitingConditionValue;
    private final List<String> executedFlowJudgmentNodeIds;

    public CanonicalStorySnapshot(String resourceId, String resourceFingerprint, CanonicalStoryStatus status,
        CanonicalStoryRepeatPolicy repeatPolicy, String triggerPortId, String currentNodeId, String currentInputPortId,
        CanonicalStoryWaitKind waitKind, String waitResourceId, Map<String, Boolean> logicValues,
        String targetStoryId) {
        this(
            resourceId,
            resourceFingerprint,
            status,
            repeatPolicy,
            triggerPortId,
            currentNodeId,
            currentInputPortId,
            waitKind,
            waitResourceId,
            null,
            null,
            null,
            null,
            null,
            logicValues,
            targetStoryId,
            Collections.<String, Boolean>emptyMap(),
            null,
            Collections.<String>emptyList());
    }

    public CanonicalStorySnapshot(String resourceId, String resourceFingerprint, CanonicalStoryStatus status,
        CanonicalStoryRepeatPolicy repeatPolicy, String triggerPortId, String currentNodeId, String currentInputPortId,
        CanonicalStoryWaitKind waitKind, String waitResourceId, Integer waitDimension, Double waitX, Double waitY,
        Double waitZ, Double waitRadius, Map<String, Boolean> logicValues, String targetStoryId) {
        this(
            resourceId,
            resourceFingerprint,
            status,
            repeatPolicy,
            triggerPortId,
            currentNodeId,
            currentInputPortId,
            waitKind,
            waitResourceId,
            waitDimension,
            waitX,
            waitY,
            waitZ,
            waitRadius,
            logicValues,
            targetStoryId,
            Collections.<String, Boolean>emptyMap(),
            null,
            Collections.<String>emptyList());
    }

    public CanonicalStorySnapshot(String resourceId, String resourceFingerprint, CanonicalStoryStatus status,
        CanonicalStoryRepeatPolicy repeatPolicy, String triggerPortId, String currentNodeId, String currentInputPortId,
        CanonicalStoryWaitKind waitKind, String waitResourceId, Integer waitDimension, Double waitX, Double waitY,
        Double waitZ, Double waitRadius, Map<String, Boolean> logicValues, String targetStoryId,
        Map<String, Boolean> externalLogicInputs, Boolean waitingConditionValue,
        List<String> executedFlowJudgmentNodeIds) {
        if (blank(resourceId) || blank(resourceFingerprint)
            || status == null
            || repeatPolicy == null
            || blank(triggerPortId)
            || waitKind == null
            || logicValues == null) throw new IllegalArgumentException("Invalid canonical Story snapshot.");
        if (status == CanonicalStoryStatus.ACTIVE && (blank(currentNodeId) || blank(currentInputPortId)))
            throw new IllegalArgumentException("Active canonical Story requires one cursor.");
        if (status != CanonicalStoryStatus.ACTIVE && waitKind != CanonicalStoryWaitKind.NONE)
            throw new IllegalArgumentException("Terminal canonical Story cannot wait on a child boundary.");
        if ((waitKind == CanonicalStoryWaitKind.CONDITION) != (waitingConditionValue != null))
            throw new IllegalArgumentException("Story Condition wait state and value must be supplied together.");
        if (waitKind == CanonicalStoryWaitKind.SESSION || waitKind == CanonicalStoryWaitKind.TASK
            || waitKind.isActorInteraction()) {
            if (blank(waitResourceId)) throw new IllegalArgumentException("Aggregate wait requires a resource ID.");
            if (waitDimension != null || waitX != null || waitY != null || waitZ != null || waitRadius != null)
                throw new IllegalArgumentException("Actor wait cannot retain a region descriptor.");
        } else if (waitKind == CanonicalStoryWaitKind.ENTER_REGION) {
            if (waitDimension == null || waitX == null
                || waitY == null
                || waitZ == null
                || waitRadius == null
                || waitRadius.doubleValue() <= 0D
                || !finite(waitX)
                || !finite(waitY)
                || !finite(waitZ)
                || !finite(waitRadius)) throw new IllegalArgumentException("Region wait requires a finite sphere.");
            if (waitResourceId != null) throw new IllegalArgumentException("Region wait cannot retain a resource ID.");
        } else if (waitResourceId != null || waitDimension != null
            || waitX != null
            || waitY != null
            || waitZ != null
            || waitRadius != null) throw new IllegalArgumentException("Only aggregate waits retain a resource ID.");
        if ((status == CanonicalStoryStatus.TRANSFERRED) != !blank(targetStoryId))
            throw new IllegalArgumentException("Only transferred canonical Stories retain a target Story ID.");
        LinkedHashMap<String, Boolean> detached = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : logicValues.entrySet()) {
            if (blank(entry.getKey()) || entry.getValue() == null)
                throw new IllegalArgumentException("Invalid canonical Story Logic snapshot.");
            detached.put(entry.getKey(), entry.getValue());
        }
        if (externalLogicInputs == null)
            throw new IllegalArgumentException("Story external Logic snapshot is required.");
        LinkedHashMap<String, Boolean> external = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : externalLogicInputs.entrySet()) {
            if (blank(entry.getKey()) || entry.getValue() == null)
                throw new IllegalArgumentException("Invalid Story external Logic snapshot.");
            external.put(entry.getKey(), entry.getValue());
        }
        if (executedFlowJudgmentNodeIds == null)
            throw new IllegalArgumentException("Story Flow Judgment snapshot is required.");
        ArrayList<String> executed = new ArrayList<String>();
        for (String nodeId : executedFlowJudgmentNodeIds) {
            if (blank(nodeId) || !executed.add(nodeId))
                throw new IllegalArgumentException("Invalid Story Flow Judgment snapshot.");
        }
        this.resourceId = resourceId;
        this.resourceFingerprint = resourceFingerprint;
        this.status = status;
        this.repeatPolicy = repeatPolicy;
        this.triggerPortId = triggerPortId;
        this.currentNodeId = currentNodeId;
        this.currentInputPortId = currentInputPortId;
        this.waitKind = waitKind;
        this.waitResourceId = waitResourceId;
        this.waitDimension = waitDimension;
        this.waitX = waitX;
        this.waitY = waitY;
        this.waitZ = waitZ;
        this.waitRadius = waitRadius;
        this.logicValues = Collections.unmodifiableMap(detached);
        this.targetStoryId = targetStoryId;
        this.externalLogicInputs = Collections.unmodifiableMap(external);
        this.waitingConditionValue = waitingConditionValue;
        this.executedFlowJudgmentNodeIds = Collections.unmodifiableList(executed);
    }

    public CanonicalStorySnapshot(String resourceId, String resourceFingerprint, CanonicalStoryStatus status,
        CanonicalStoryRepeatPolicy repeatPolicy, String triggerPortId, String currentNodeId, String currentInputPortId,
        CanonicalStoryWaitKind waitKind, String waitResourceId, Integer waitDimension, Double waitX, Double waitY,
        Double waitZ, Double waitRadius, Map<String, Boolean> logicValues, String targetStoryId,
        Map<String, Boolean> externalLogicInputs, Boolean waitingConditionValue) {
        this(
            resourceId,
            resourceFingerprint,
            status,
            repeatPolicy,
            triggerPortId,
            currentNodeId,
            currentInputPortId,
            waitKind,
            waitResourceId,
            waitDimension,
            waitX,
            waitY,
            waitZ,
            waitRadius,
            logicValues,
            targetStoryId,
            externalLogicInputs,
            waitingConditionValue,
            Collections.<String>emptyList());
    }

    public String getResourceId() {
        return resourceId;
    }

    public String getStoryId() {
        return resourceId;
    }

    public String getResourceFingerprint() {
        return resourceFingerprint;
    }

    public CanonicalStoryStatus getStatus() {
        return status;
    }

    public CanonicalStoryRepeatPolicy getRepeatPolicy() {
        return repeatPolicy;
    }

    public String getTriggerPortId() {
        return triggerPortId;
    }

    public String getCurrentNodeId() {
        return currentNodeId;
    }

    public String getCurrentInputPortId() {
        return currentInputPortId;
    }

    public CanonicalStoryWaitKind getWaitKind() {
        return waitKind;
    }

    public String getWaitResourceId() {
        return waitResourceId;
    }

    public String getWaitActorId() {
        return waitKind.isActorInteraction() ? waitResourceId : null;
    }

    public String getWaitInteractActorId() {
        return getWaitActorId();
    }

    public Integer getWaitDimension() {
        return waitDimension;
    }

    public Integer getWaitRegionDimension() {
        return waitDimension;
    }

    public Double getWaitX() {
        return waitX;
    }

    public Double getWaitRegionX() {
        return waitX;
    }

    public Double getWaitY() {
        return waitY;
    }

    public Double getWaitRegionY() {
        return waitY;
    }

    public Double getWaitZ() {
        return waitZ;
    }

    public Double getWaitRegionZ() {
        return waitZ;
    }

    public Double getWaitRadius() {
        return waitRadius;
    }

    public Double getWaitRegionRadius() {
        return waitRadius;
    }

    public Map<String, Boolean> getLogicValues() {
        return logicValues;
    }

    public Map<String, Boolean> getStoryLogicState() {
        return logicValues;
    }

    public String getTargetStoryId() {
        return targetStoryId;
    }

    public Map<String, Boolean> getExternalLogicInputs() {
        return externalLogicInputs;
    }

    public Map<String, Boolean> getLogicInputs() {
        return externalLogicInputs;
    }

    public Boolean getWaitingConditionValue() {
        return waitingConditionValue;
    }

    public List<String> getExecutedFlowJudgmentNodeIds() {
        return executedFlowJudgmentNodeIds;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static boolean finite(Double value) {
        return value != null && !Double.isNaN(value.doubleValue()) && !Double.isInfinite(value.doubleValue());
    }
}
