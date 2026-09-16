package darkgrey.rpg.session.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Detached, immutable persistence shape for one canonical Session instance. */
public final class CanonicalSessionSnapshot {

    private final CanonicalSessionPresentation presentation;
    private final long lineEpoch;
    private final int linePageIndex;

    public int getLinePageIndex() {
        return linePageIndex;
    }

    public CanonicalSessionPresentation getPresentation() {
        return presentation;
    }

    public long getLineEpoch() {
        return lineEpoch;
    }

    private final String sessionResourceId;
    private final String currentNodeId;
    private final CanonicalSessionStatus status;
    private final List<String> selectedOptionIds;
    private final Map<String, Boolean> internalLogicValues;
    private final String finalEndPortId;
    private final Map<String, Boolean> publicLogicOutputs;
    private final boolean activationLogic;
    private final Map<String, String> latestChoiceSelections;
    private final List<String> selectedChoiceNodeIds;
    private final Map<String, Boolean> externalLogicInputs;
    private final boolean waitingCondition;
    private final Boolean waitingConditionValue;
    private final List<String> executedFlowJudgmentNodeIds;

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            false,
            Collections.<String, String>emptyMap(),
            Collections.<String>emptyList(),
            Collections.<String, Boolean>emptyMap(),
            false,
            null,
            Collections.<String>emptyList());
    }

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            Collections.<String, String>emptyMap(),
            Collections.<String>emptyList(),
            Collections.<String, Boolean>emptyMap(),
            false,
            null,
            Collections.<String>emptyList());
    }

    /** Full state constructor retaining the old constructors for callers. */
    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic, Map<String, String> latestChoiceSelections,
        List<String> selectedChoiceNodeIds) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            latestChoiceSelections,
            selectedChoiceNodeIds,
            Collections.<String, Boolean>emptyMap(),
            false,
            null,
            Collections.<String>emptyList());
    }

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic, Map<String, String> latestChoiceSelections,
        List<String> selectedChoiceNodeIds, Map<String, Boolean> externalLogicInputs, boolean waitingCondition,
        Boolean waitingConditionValue) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            latestChoiceSelections,
            selectedChoiceNodeIds,
            externalLogicInputs,
            waitingCondition,
            waitingConditionValue,
            Collections.<String>emptyList());
    }

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic, Map<String, String> latestChoiceSelections,
        List<String> selectedChoiceNodeIds, Map<String, Boolean> externalLogicInputs, boolean waitingCondition,
        Boolean waitingConditionValue, List<String> executedFlowJudgmentNodeIds) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            latestChoiceSelections,
            selectedChoiceNodeIds,
            externalLogicInputs,
            waitingCondition,
            waitingConditionValue,
            executedFlowJudgmentNodeIds,
            CanonicalSessionPresentation.EMPTY,
            0);
    }

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic, Map<String, String> latestChoiceSelections,
        List<String> selectedChoiceNodeIds, Map<String, Boolean> externalLogicInputs, boolean waitingCondition,
        Boolean waitingConditionValue, List<String> executedFlowJudgmentNodeIds,
        CanonicalSessionPresentation presentation, long lineEpoch) {
        this(
            sessionResourceId,
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            latestChoiceSelections,
            selectedChoiceNodeIds,
            externalLogicInputs,
            waitingCondition,
            waitingConditionValue,
            executedFlowJudgmentNodeIds,
            presentation,
            lineEpoch,
            0);
    }

    public CanonicalSessionSnapshot(String sessionResourceId, String currentNodeId, CanonicalSessionStatus status,
        List<String> selectedOptionIds, Map<String, Boolean> internalLogicValues, String finalEndPortId,
        Map<String, Boolean> publicLogicOutputs, boolean activationLogic, Map<String, String> latestChoiceSelections,
        List<String> selectedChoiceNodeIds, Map<String, Boolean> externalLogicInputs, boolean waitingCondition,
        Boolean waitingConditionValue, List<String> executedFlowJudgmentNodeIds,
        CanonicalSessionPresentation presentation, long lineEpoch, int linePageIndex) {
        if (presentation == null || lineEpoch < 0 || linePageIndex < 0)
            throw new IllegalArgumentException("Invalid Session presentation state");
        this.linePageIndex = linePageIndex;
        this.presentation = presentation;
        this.lineEpoch = lineEpoch;
        this.sessionResourceId = sessionResourceId;
        this.currentNodeId = currentNodeId;
        this.status = status;
        this.selectedOptionIds = immutableList(selectedOptionIds);
        this.internalLogicValues = immutableMap(internalLogicValues);
        this.finalEndPortId = finalEndPortId;
        this.publicLogicOutputs = immutableMap(publicLogicOutputs);
        this.activationLogic = activationLogic;
        this.latestChoiceSelections = immutableStringMap(latestChoiceSelections);
        this.selectedChoiceNodeIds = immutableList(selectedChoiceNodeIds);
        this.externalLogicInputs = immutableMap(externalLogicInputs);
        this.waitingCondition = waitingCondition;
        this.waitingConditionValue = waitingConditionValue;
        this.executedFlowJudgmentNodeIds = immutableList(executedFlowJudgmentNodeIds);
        if (waitingCondition != (waitingConditionValue != null))
            throw new IllegalArgumentException("Session Condition wait state and value must be supplied together.");
    }

    public String getSessionResourceId() {
        return sessionResourceId;
    }

    public String getCurrentNodeId() {
        return currentNodeId;
    }

    public CanonicalSessionStatus getStatus() {
        return status;
    }

    public List<String> getSelectedOptionIds() {
        return selectedOptionIds;
    }

    public Map<String, Boolean> getInternalLogicValues() {
        return internalLogicValues;
    }

    public Map<String, Boolean> getInternalLogicMap() {
        return internalLogicValues;
    }

    public String getFinalEndPortId() {
        return finalEndPortId;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogicOutputs;
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return publicLogicOutputs;
    }

    public boolean isActivationLogic() {
        return activationLogic;
    }

    public boolean getActivationLogic() {
        return activationLogic;
    }

    public boolean isActivation() {
        return activationLogic;
    }

    public Map<String, String> getLatestChoiceSelections() {
        return latestChoiceSelections;
    }

    public Map<String, String> getSelectedChoiceOptions() {
        return latestChoiceSelections;
    }

    public List<String> getSelectedChoiceNodeIds() {
        return selectedChoiceNodeIds;
    }

    public Map<String, Boolean> getExternalLogicInputs() {
        return externalLogicInputs;
    }

    public Map<String, Boolean> getLogicInputs() {
        return externalLogicInputs;
    }

    public boolean isWaitingCondition() {
        return waitingCondition;
    }

    public Boolean getWaitingConditionValue() {
        return waitingConditionValue;
    }

    public List<String> getExecutedFlowJudgmentNodeIds() {
        return executedFlowJudgmentNodeIds;
    }

    private static List<String> immutableList(List<String> values) {
        return Collections
            .unmodifiableList(new ArrayList<String>(values == null ? Collections.<String>emptyList() : values));
    }

    private static Map<String, Boolean> immutableMap(Map<String, Boolean> values) {
        return Collections.unmodifiableMap(
            new LinkedHashMap<String, Boolean>(values == null ? Collections.<String, Boolean>emptyMap() : values));
    }

    private static Map<String, String> immutableStringMap(Map<String, String> values) {
        return Collections.unmodifiableMap(
            new LinkedHashMap<String, String>(values == null ? Collections.<String, String>emptyMap() : values));
    }
}
