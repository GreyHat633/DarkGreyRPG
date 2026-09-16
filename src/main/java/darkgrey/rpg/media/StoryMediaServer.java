package darkgrey.rpg.media;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Queue;
import java.util.Set;
import java.util.WeakHashMap;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.StoryMediaPlan;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageManifest;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition;

/**
 * Server-side Story media ownership boundary. It observes real Story starts
 * and emits metadata; it never executes a Story or infers starts from nearby
 * entities or candidate queries.
 */
public final class StoryMediaServer {

    private static final Map<EntityPlayerMP, State> STATES = new WeakHashMap<EntityPlayerMP, State>();
    private static volatile StoryPackageLoader loader;

    private StoryMediaServer() {}

    public static void bind(StoryPackageLoader value) {
        loader = value;
    }

    /** Called after the aggregate gateway has successfully started a NEW/RESTART Story. */
    public static void storyStarted(EntityPlayerMP player, String storyId) {
        storyStartedInternal(player, storyId);
    }

    /** Restores ACTIVE Story roots after reconnect without creating a new Story run. */
    public static synchronized void restoreRunning(EntityPlayerMP player, Set<String> storyIds) {
        if (player == null) return;
        State state = state(player);
        LinkedHashSet<String> next = new LinkedHashSet<String>();
        if (storyIds != null)
            for (String storyId : storyIds) if (!blank(storyId) && packageForStory(storyId) != null) next.add(storyId);
        if (state.runningStories.equals(next)) return;
        state.runningStories.clear();
        state.runningStories.addAll(next);
        publish(player, state);
    }

    public static void storyStarted(EntityPlayerMP player, String storyId, CanonicalStoryStartDisposition disposition) {
        if (disposition != null && !disposition.isEligible()) return;
        storyStartedInternal(player, storyId);
    }

    private static synchronized void storyStartedInternal(EntityPlayerMP player, String storyId) {
        if (player == null || blank(storyId)) return;
        LoadedStoryPackage current = packageForStory(storyId);
        if (current == null) return;
        State state = state(player);
        // A fresh real Start authorizes the current installed generations again,
        // including downstream packages replaced since the previous run.
        StoryPackageLoader source = loader != null ? loader : DarkGreyRpg.getStoryPackageLoader();
        List<CanonicalStoryLogicConnection> links = new ArrayList<CanonicalStoryLogicConnection>();
        for (LoadedStoryPackage value : source.getPackages()
            .values())
            links.addAll(
                value.getStoryLogicGraph()
                    .getConnections());
        state.retiredStories.removeAll(downstreamStoryIds(storyId, links));
        state.runningStories.add(storyId);
        state.frameStories.remove(storyId);
        publish(player, state);
    }

    /**
     * Standalone Session authoring/debug path. It owns the package manifest and
     * visible refs but deliberately does not walk the cross-Story graph.
     */
    public static synchronized void presentFrame(EntityPlayerMP player,
        darkgrey.rpg.network.message.canonical.CanonicalSessionFrame frame) {
        if (player == null || frame == null || blank(frame.getStoryId())) return;
        LoadedStoryPackage current = packageForStory(frame.getStoryId());
        if (current == null) return;
        State state = state(player);
        state.frameStories.add(frame.getStoryId());
        publish(player, state);
    }

    public static synchronized void storyEnded(EntityPlayerMP player, String storyId) {
        State state = STATES.get(player);
        if (state == null) return;
        state.runningStories.remove(storyId);
        state.frameStories.remove(storyId);
        publish(player, state);
    }

    public static synchronized void reset(EntityPlayerMP player, String storyId) {
        storyEnded(player, storyId);
    }

    public static synchronized void clearFrame(EntityPlayerMP player, String storyId) {
        State state = STATES.get(player);
        if (state == null) return;
        state.frameStories.remove(storyId);
        publish(player, state);
    }

    /** Reconnect/unload revoke all old package capabilities for this connection. */
    public static synchronized void reconnect(EntityPlayerMP player) {
        revoke(player);
    }

    public static synchronized void unload(EntityPlayerMP player) {
        revoke(player);
    }

    public static synchronized void revoke(EntityPlayerMP player) {
        if (player == null) return;
        STATES.remove(player);
    }

    /** Explicitly retires a package after the loader replaces or unloads it. */
    public static synchronized void retirePackage(EntityPlayerMP player, String packageId) {
        State state = STATES.get(player);
        if (state == null || blank(packageId)) return;
        state.descriptors.remove(packageId);
        publish(player, state);
    }

    /** Retires Story metadata for every connection during an install-set swap. */
    public static synchronized void retireStories(Set<String> storyIds) {
        if (storyIds == null || storyIds.isEmpty()) return;
        for (Map.Entry<EntityPlayerMP, State> entry : STATES.entrySet()) {
            State state = entry.getValue();
            state.retiredStories.addAll(storyIds);
            state.runningStories.removeAll(storyIds);
            state.frameStories.removeAll(storyIds);
            java.util.Iterator<Map.Entry<String, StoryMediaPlan.Descriptor>> descriptors = state.descriptors.entrySet()
                .iterator();
            while (descriptors.hasNext()) if (storyIds.contains(
                descriptors.next()
                    .getValue()
                    .getStoryId()))
                descriptors.remove();
            publish(entry.getKey(), state);
        }
    }

    /** Authorizes server media requests against metadata emitted for this connection. */
    public static synchronized boolean authorizes(EntityPlayerMP player, String ref) {
        State state = STATES.get(player);
        return player != null && player.playerNetServerHandler != null
            && state != null
            && state.authorizedRefs.contains(ref);
    }

    /** Useful for lifecycle probes and integration diagnostics. */
    public static synchronized Set<String> runningPackageIds(EntityPlayerMP player) {
        State state = STATES.get(player);
        return state == null ? Collections.<String>emptySet()
            : Collections.unmodifiableSet(new LinkedHashSet<String>(state.runningPackageIds));
    }

    /** Pure directed BFS used by the server and probe; both Flow and Logic edges are package links. */
    public static List<String> downstreamStoryIds(String startStoryId, List<CanonicalStoryLogicConnection> edges) {
        if (blank(startStoryId)) return Collections.emptyList();
        Map<String, List<String>> outgoing = new HashMap<String, List<String>>();
        if (edges != null) for (CanonicalStoryLogicConnection edge : edges) {
            if (edge == null) continue;
            List<String> targets = outgoing.get(edge.getSourceStoryId());
            if (targets == null) {
                targets = new ArrayList<String>();
                outgoing.put(edge.getSourceStoryId(), targets);
            }
            targets.add(edge.getTargetStoryId());
        }
        List<String> result = new ArrayList<String>();
        Set<String> seen = new LinkedHashSet<String>();
        Queue<String> queue = new ArrayDeque<String>();
        seen.add(startStoryId);
        queue.add(startStoryId);
        while (!queue.isEmpty()) {
            String story = queue.remove();
            result.add(story);
            List<String> targets = outgoing.get(story);
            if (targets == null) continue;
            for (String target : targets) if (!blank(target) && seen.add(target)) queue.add(target);
        }
        return Collections.unmodifiableList(result);
    }

    private static void publish(EntityPlayerMP player, State state) {
        StoryPackageLoader source = loader != null ? loader : DarkGreyRpg.getStoryPackageLoader();
        if (source == null) return;
        Map<String, LoadedStoryPackage> packages = source.getPackages();
        List<CanonicalStoryLogicConnection> edges = new ArrayList<CanonicalStoryLogicConnection>();
        for (LoadedStoryPackage value : packages.values()) edges.addAll(
            value.getStoryLogicGraph()
                .getConnections());
        LinkedHashSet<String> roots = new LinkedHashSet<String>();
        roots.addAll(state.runningStories);
        LinkedHashSet<String> storyOrder = new LinkedHashSet<String>();
        for (String root : roots) storyOrder.addAll(downstreamStoryIds(root, edges));
        for (String storyId : roots) {
            LoadedStoryPackage value = source.getPackageForStory(storyId);
            if (value != null) storyOrder.add(storyId);
        }
        state.runningPackageIds.clear();
        for (String storyId : storyOrder) {
            if (state.retiredStories.contains(storyId)) continue;
            LoadedStoryPackage value = source.getPackageForStory(storyId);
            if (value == null) continue;
            StoryMediaPlan.Descriptor previous = state.descriptors.get(value.getPackageId());
            if (previous == null || !previous.isPreload()
                || !value.getContentFingerprint()
                    .equals(previous.getFingerprint()))
                state.descriptors.put(value.getPackageId(), descriptor(value, true));
        }
        for (String storyId : state.frameStories) {
            if (state.retiredStories.contains(storyId)) continue;
            LoadedStoryPackage value = source.getPackageForStory(storyId);
            if (value != null && !state.descriptors.containsKey(value.getPackageId()))
                state.descriptors.put(value.getPackageId(), descriptor(value, false));
        }
        for (String storyId : state.runningStories) {
            if (state.retiredStories.contains(storyId)) continue;
            LoadedStoryPackage value = source.getPackageForStory(storyId);
            if (value != null) state.runningPackageIds.add(value.getPackageId());
        }
        for (String packageId : state.runningPackageIds) {
            StoryMediaPlan.Descriptor current = state.descriptors.get(packageId);
            LoadedStoryPackage value = packages.get(packageId);
            if (value != null && (current == null || !current.isPreload()
                || !value.getContentFingerprint()
                    .equals(current.getFingerprint())))
                state.descriptors.put(packageId, descriptor(value, true));
        }
        for (String storyId : state.frameStories) {
            LoadedStoryPackage value = source.getPackageForStory(storyId);
            if (value != null) state.runningPackageIds.add(value.getPackageId());
        }
        state.authorizedRefs.clear();
        for (StoryMediaPlan.Descriptor descriptor : state.descriptors.values())
            state.authorizedRefs.addAll(descriptor.getRefs());
        sendPlans(
            player,
            new ArrayList<StoryMediaPlan.Descriptor>(state.descriptors.values()),
            state.runningPackageIds,
            ++state.revision);
    }

    private static void sendPlans(EntityPlayerMP player, List<StoryMediaPlan.Descriptor> descriptors,
        Set<String> running, long revision) {
        if (player == null || player.playerNetServerHandler == null) return;
        int count = Math.max(
            1,
            (descriptors.size() + StoryMediaPlan.MAX_DESCRIPTORS_PER_PACKET - 1)
                / StoryMediaPlan.MAX_DESCRIPTORS_PER_PACKET);
        if (count > StoryMediaPlan.MAX_CHUNKS)
            throw new IllegalStateException("Story Media plan exceeds wire chunk bound");
        for (int index = 0; index < count; index++) {
            int from = index * StoryMediaPlan.MAX_DESCRIPTORS_PER_PACKET;
            int to = Math.min(descriptors.size(), from + StoryMediaPlan.MAX_DESCRIPTORS_PER_PACKET);
            send(player, new StoryMediaPlan(revision, index, count, descriptors.subList(from, to), running));
        }
    }

    private static StoryMediaPlan.Descriptor descriptor(LoadedStoryPackage value, boolean preload) {
        StoryPackageManifest manifest = value.getManifest();
        return new StoryMediaPlan.Descriptor(
            value.getPackageId(),
            value.getStoryId(),
            manifest.getPackageVersion(),
            value.getContentFingerprint(),
            manifest.getRequiredResources()
                .getMedia(),
            preload);
    }

    private static void send(EntityPlayerMP player, StoryMediaPlan plan) {
        DialogueNetwork.CHANNEL.sendTo(plan, player);
    }

    private static LoadedStoryPackage packageForStory(String storyId) {
        StoryPackageLoader source = loader != null ? loader : DarkGreyRpg.getStoryPackageLoader();
        return source == null ? null : source.getPackageForStory(storyId);
    }

    private static State state(EntityPlayerMP player) {
        State state = STATES.get(player);
        if (state == null) {
            state = new State();
            STATES.put(player, state);
        }
        return state;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static final class State {

        final Set<String> runningStories = new LinkedHashSet<String>();
        final Set<String> frameStories = new LinkedHashSet<String>();
        final Set<String> runningPackageIds = new LinkedHashSet<String>();
        final Set<String> authorizedRefs = new HashSet<String>();
        final Set<String> retiredStories = new HashSet<String>();
        final LinkedHashMap<String, StoryMediaPlan.Descriptor> descriptors = new LinkedHashMap<String, StoryMediaPlan.Descriptor>();
        long revision;
    }
}
