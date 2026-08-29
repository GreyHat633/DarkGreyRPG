package darkgrey.rpg.task.event;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Detached outcome of one indexed Task event dispatch. */
public final class CanonicalTaskDispatchResult {

    private final int candidateCount;
    private final int changedInstanceCount;
    private final List<CanonicalTaskInstanceSnapshot> settledInstances;
    private final Map<CanonicalTaskInstanceIdentity, String> settledResults;
    private final List<CanonicalTaskInstanceIdentity> erroredInstances;

    public CanonicalTaskDispatchResult(int candidateCount, int changedInstanceCount,
        List<CanonicalTaskInstanceSnapshot> settledInstances, Map<CanonicalTaskInstanceIdentity, String> settledResults,
        List<CanonicalTaskInstanceIdentity> erroredInstances) {
        this.candidateCount = candidateCount;
        this.changedInstanceCount = changedInstanceCount;
        this.settledInstances = immutableSnapshots(settledInstances);
        this.settledResults = Collections
            .unmodifiableMap(new LinkedHashMap<CanonicalTaskInstanceIdentity, String>(settledResults));
        this.erroredInstances = immutableIdentities(erroredInstances);
    }

    public int getCandidateCount() {
        return candidateCount;
    }

    public int getCandidates() {
        return candidateCount;
    }

    public int getChangedInstanceCount() {
        return changedInstanceCount;
    }

    public int getChangedCount() {
        return changedInstanceCount;
    }

    public List<CanonicalTaskInstanceSnapshot> getSettledInstances() {
        return settledInstances;
    }

    public List<CanonicalTaskInstanceSnapshot> getSettledSnapshots() {
        return settledInstances;
    }

    public Map<CanonicalTaskInstanceIdentity, String> getSettledResults() {
        return settledResults;
    }

    public List<CanonicalTaskInstanceIdentity> getErroredInstances() {
        return erroredInstances;
    }

    public List<CanonicalTaskInstanceIdentity> getErroredInstanceIdentities() {
        return erroredInstances;
    }

    private static List<CanonicalTaskInstanceSnapshot> immutableSnapshots(List<CanonicalTaskInstanceSnapshot> value) {
        return Collections.unmodifiableList(
            new ArrayList<CanonicalTaskInstanceSnapshot>(
                value == null ? Collections.<CanonicalTaskInstanceSnapshot>emptyList() : value));
    }

    private static List<CanonicalTaskInstanceIdentity> immutableIdentities(List<CanonicalTaskInstanceIdentity> value) {
        List<CanonicalTaskInstanceIdentity> result = new ArrayList<CanonicalTaskInstanceIdentity>(
            value == null ? Collections.<CanonicalTaskInstanceIdentity>emptyList() : value);
        Collections.sort(result);
        return Collections.unmodifiableList(result);
    }
}
