package darkgrey.rpg.story.canonical.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

/** Per-player edge detector so enter_region fires once on outside-to-inside transitions. */
public final class CanonicalStoryRegionEntryTracker {

    private final Map<UUID, Set<CanonicalStoryTriggerIndex.Match>> previousByPlayer = new HashMap<UUID, Set<CanonicalStoryTriggerIndex.Match>>();

    public synchronized List<CanonicalStoryTriggerIndex.Match> update(UUID playerUuid,
        List<CanonicalStoryTriggerIndex.Match> currentMatches) {
        if (playerUuid == null || currentMatches == null)
            throw new IllegalArgumentException("Region entry tracker inputs are required.");
        Set<CanonicalStoryTriggerIndex.Match> current = new HashSet<CanonicalStoryTriggerIndex.Match>();
        for (CanonicalStoryTriggerIndex.Match match : currentMatches) {
            if (match == null) throw new IllegalArgumentException("Region trigger matches cannot contain null.");
            current.add(match);
        }
        Set<CanonicalStoryTriggerIndex.Match> previous = previousByPlayer
            .put(playerUuid, Collections.unmodifiableSet(current));
        if (previous == null) previous = Collections.emptySet();
        List<CanonicalStoryTriggerIndex.Match> entered = new ArrayList<CanonicalStoryTriggerIndex.Match>();
        for (CanonicalStoryTriggerIndex.Match match : currentMatches)
            if (!previous.contains(match) && !entered.contains(match)) entered.add(match);
        return Collections.unmodifiableList(entered);
    }

    public synchronized void forget(UUID playerUuid) {
        if (playerUuid != null) previousByPlayer.remove(playerUuid);
    }
}
