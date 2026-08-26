package darkgrey.rpg.story.runtime;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Deque;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.story.StoryDefinition;

public final class StoryInstance {

    private final StoryDefinition definition;
    private final Deque<String> pendingSequenceNodes = new ArrayDeque<String>();
    private final List<String> previousNodes = new ArrayList<String>();
    private String currentNodeId;
    private StoryState state = StoryState.IDLE;
    private String error = "";
    private String lastNodeId = "";
    private String lastResult = "";
    private String lastExplanation = "";
    private String lastDialogueResult = "";
    private final Map<String, String> nodeResults = new LinkedHashMap<String, String>();
    private final Map<String, String> nodeExplanations = new LinkedHashMap<String, String>();

    public StoryInstance(StoryDefinition definition) {
        this.definition = definition;
        this.currentNodeId = definition.getEntry();
    }

    public StoryDefinition getDefinition() {
        return definition;
    }

    public String getCurrentNodeId() {
        return currentNodeId;
    }

    public void moveTo(String nodeId) {
        previousNodes.add(currentNodeId);
        if (previousNodes.size() > 64) {
            previousNodes.remove(0);
        }
        currentNodeId = nodeId;
        state = StoryState.RUNNING;
    }

    public StoryState getState() {
        return state;
    }

    public void setState(StoryState state) {
        this.state = state;
    }

    public List<String> getPreviousNodes() {
        return Collections.unmodifiableList(previousNodes);
    }

    public void pushPending(String nodeId) {
        pendingSequenceNodes.push(nodeId);
    }

    public String popPending() {
        return pendingSequenceNodes.isEmpty() ? null : pendingSequenceNodes.pop();
    }

    public void reset() {
        currentNodeId = definition.getEntry();
        pendingSequenceNodes.clear();
        state = StoryState.IDLE;
        error = "";
        lastNodeId = "";
        lastResult = "";
        lastExplanation = "";
        lastDialogueResult = "";
    }

    public String getError() {
        return error;
    }

    public void fail(String message) {
        error = message;
        state = StoryState.ERROR;
    }

    public void startAt(String nodeId) {
        currentNodeId = nodeId;
        pendingSequenceNodes.clear();
        previousNodes.clear();
        state = StoryState.RUNNING;
        error = "";
        lastNodeId = "";
        lastResult = "";
        lastExplanation = "";
        lastDialogueResult = "";
        nodeResults.clear();
        nodeExplanations.clear();
    }

    public void recordDebug(String nodeId, String result, String explanation) {
        lastNodeId = nodeId;
        lastResult = result;
        lastExplanation = explanation;
        nodeResults.put(nodeId, result);
        nodeExplanations.put(nodeId, explanation);
    }

    public String getLastNodeId() {
        return lastNodeId;
    }

    public String getLastResult() {
        return lastResult;
    }

    public String getLastExplanation() {
        return lastExplanation;
    }

    public void recordDialogueResult(String result) {
        lastDialogueResult = result == null ? "" : result;
    }

    public String getLastDialogueResult() {
        return lastDialogueResult;
    }

    public Map<String, String> getNodeResults() {
        return Collections.unmodifiableMap(nodeResults);
    }

    public Map<String, String> getNodeExplanations() {
        return Collections.unmodifiableMap(nodeExplanations);
    }

    public Snapshot snapshot() {
        return new Snapshot(
            currentNodeId,
            state,
            new ArrayList<String>(previousNodes),
            new ArrayList<String>(pendingSequenceNodes),
            error,
            lastNodeId,
            lastResult,
            lastExplanation,
            lastDialogueResult,
            new LinkedHashMap<String, String>(nodeResults),
            new LinkedHashMap<String, String>(nodeExplanations));
    }

    public void restore(Snapshot snapshot) {
        currentNodeId = snapshot.currentNodeId;
        state = snapshot.state;
        previousNodes.clear();
        previousNodes.addAll(snapshot.previousNodes);
        pendingSequenceNodes.clear();
        pendingSequenceNodes.addAll(snapshot.pendingSequenceNodes);
        error = snapshot.error;
        lastNodeId = snapshot.lastNodeId;
        lastResult = snapshot.lastResult;
        lastExplanation = snapshot.lastExplanation;
        lastDialogueResult = snapshot.lastDialogueResult;
        nodeResults.clear();
        nodeResults.putAll(snapshot.nodeResults);
        nodeExplanations.clear();
        nodeExplanations.putAll(snapshot.nodeExplanations);
    }

    public static final class Snapshot {

        private final String currentNodeId;
        private final StoryState state;
        private final List<String> previousNodes;
        private final List<String> pendingSequenceNodes;
        private final String error;
        private final String lastNodeId;
        private final String lastResult;
        private final String lastExplanation;
        private final String lastDialogueResult;
        private final Map<String, String> nodeResults;
        private final Map<String, String> nodeExplanations;

        private Snapshot(String currentNodeId, StoryState state, List<String> previousNodes,
            List<String> pendingSequenceNodes, String error, String lastNodeId, String lastResult,
            String lastExplanation, String lastDialogueResult, Map<String, String> nodeResults,
            Map<String, String> nodeExplanations) {
            this.currentNodeId = currentNodeId;
            this.state = state;
            this.previousNodes = previousNodes;
            this.pendingSequenceNodes = pendingSequenceNodes;
            this.error = error;
            this.lastNodeId = lastNodeId;
            this.lastResult = lastResult;
            this.lastExplanation = lastExplanation;
            this.lastDialogueResult = lastDialogueResult;
            this.nodeResults = nodeResults;
            this.nodeExplanations = nodeExplanations;
        }
    }
}
