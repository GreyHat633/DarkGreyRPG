package darkgrey.rpg.task.runtime;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/** Detached immutable state of a canonical Task runtime. */
public final class CanonicalTaskSnapshot {

    private final String resourceId;
    private final String resourceFingerprint;
    private final CanonicalTaskStatus status;
    private final Map<String, Integer> progress;
    private final Map<String, CanonicalTaskObjectiveStatus> objectiveStatuses;
    private final Map<String, Boolean> logicValues;
    private final Map<String, Boolean> publicLogicOutputs;
    private final boolean activationLogic;
    private final String resultPortId;

    public CanonicalTaskSnapshot(String resourceId, String resourceFingerprint, CanonicalTaskStatus status,
        Map<String, Integer> progress, Map<String, CanonicalTaskObjectiveStatus> objectiveStatuses,
        Map<String, Boolean> logicValues, Map<String, Boolean> publicLogicOutputs, boolean activationLogic,
        String resultPortId) {
        this.resourceId = resourceId;
        this.resourceFingerprint = resourceFingerprint;
        this.status = status;
        this.progress = immutable(progress);
        this.objectiveStatuses = immutable(objectiveStatuses);
        this.logicValues = immutable(logicValues);
        this.publicLogicOutputs = immutable(publicLogicOutputs);
        this.activationLogic = activationLogic;
        this.resultPortId = resultPortId;
    }

    public String getResourceId() {
        return resourceId;
    }

    public String getResourceFingerprint() {
        return resourceFingerprint;
    }

    public CanonicalTaskStatus getStatus() {
        return status;
    }

    public Map<String, Integer> getProgress() {
        return progress;
    }

    public Map<String, CanonicalTaskObjectiveStatus> getObjectiveStatuses() {
        return objectiveStatuses;
    }

    public Map<String, CanonicalTaskObjectiveStatus> getObjectiveStatus() {
        return objectiveStatuses;
    }

    public Map<String, Boolean> getObjectiveActiveStates() {
        Map<String, Boolean> result = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, CanonicalTaskObjectiveStatus> entry : objectiveStatuses.entrySet())
            result.put(entry.getKey(), Boolean.valueOf(entry.getValue() == CanonicalTaskObjectiveStatus.ACTIVE));
        return Collections.unmodifiableMap(result);
    }

    public Map<String, Boolean> getLogicValues() {
        return logicValues;
    }

    public Map<String, Boolean> getInternalLogicValues() {
        return logicValues;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogicOutputs;
    }

    public boolean getActivationLogic() {
        return activationLogic;
    }

    public String getResultPortId() {
        return resultPortId;
    }

    public String getResult() {
        return resultPortId;
    }

    private static <T> Map<String, T> immutable(Map<String, T> source) {
        return Collections
            .unmodifiableMap(new LinkedHashMap<String, T>(source == null ? Collections.<String, T>emptyMap() : source));
    }
}
