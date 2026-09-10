package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.identity.DgrResourceId;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.story.StoryDefinition;

/** Builds one server-authoritative resource snapshot from installed Story Packages. */
public final class StoryPackageSnapshotMerger {

    private StoryPackageSnapshotMerger() {}

    /**
     * Reports namespace claims made by primary Stories and explicitly owned
     * membership resources. Referenced IDs are deliberately excluded. The
     * method is pure: it does not mutate packages or reject a merge.
     */
    public static List<String> findNamespaceOriginWarnings(Map<String, LoadedStoryPackage> packages) {
        if (packages == null || packages.isEmpty()) return Collections.emptyList();
        Map<String, Set<String>> originsByNamespace = new LinkedHashMap<String, Set<String>>();
        List<LoadedStoryPackage> ordered = new ArrayList<LoadedStoryPackage>(packages.values());
        Collections.sort(
            ordered,
            (left, right) -> left.getPackageId()
                .compareTo(right.getPackageId()));
        for (LoadedStoryPackage value : ordered) {
            ProjectSnapshot snapshot = value.getSnapshot();
            String origin = snapshot.getProject()
                .getProjectOriginCode();
            for (String id : claimedResourceIds(value)) {
                if (!DgrResourceId.isFullId(id)) continue;
                String namespace = DgrResourceId.namespace(id);
                Set<String> origins = originsByNamespace.get(namespace);
                if (origins == null) {
                    origins = new LinkedHashSet<String>();
                    originsByNamespace.put(namespace, origins);
                }
                origins.add(origin);
            }
        }
        List<String> warnings = new ArrayList<String>();
        for (Map.Entry<String, Set<String>> entry : originsByNamespace.entrySet()) {
            if (entry.getValue()
                .size() > 1)
                warnings.add(
                    "Namespace '" + entry.getKey()
                        + "' is claimed by different Project Origins: "
                        + String.join(", ", entry.getValue())
                        + ".");
        }
        return Collections.unmodifiableList(warnings);
    }

    /** Short alias for callers that already use the warning noun in diagnostics. */
    public static List<String> namespaceOriginWarnings(Map<String, LoadedStoryPackage> packages) {
        return findNamespaceOriginWarnings(packages);
    }

    public static ProjectSnapshot merge(Map<String, LoadedStoryPackage> packages) throws ProjectLoadException {
        if (packages == null || packages.isEmpty())
            throw new ProjectLoadException("At least one validated Story Package is required.");
        for (String warning : findNamespaceOriginWarnings(packages)) {
            org.apache.logging.log4j.LogManager.getLogger(StoryPackageSnapshotMerger.class)
                .warn(warning);
        }
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        Map<String, ItemResourceDefinition> items = new LinkedHashMap<String, ItemResourceDefinition>();
        Map<String, ItemResourceDefinition> itemGroups = new LinkedHashMap<String, ItemResourceDefinition>();
        Map<String, DialogueDefinition> dialogues = new LinkedHashMap<String, DialogueDefinition>();
        Map<String, QuestDefinition> quests = new LinkedHashMap<String, QuestDefinition>();
        Map<String, StoryDefinition> stories = new LinkedHashMap<String, StoryDefinition>();
        Map<String, CanonicalGraphResource> canonicalStories = new LinkedHashMap<String, CanonicalGraphResource>();
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<String, CanonicalGraphResource>();
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        List<CanonicalStoryLogicConnection> storyLogicConnections = new java.util.ArrayList<CanonicalStoryLogicConnection>();
        Map<String, byte[]> actorOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> itemOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> itemGroupOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> dialogueOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> questOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> sessionOrigins = new LinkedHashMap<String, byte[]>();
        Map<String, byte[]> taskOrigins = new LinkedHashMap<String, byte[]>();
        for (LoadedStoryPackage value : packages.values()) {
            String owner = value.getPackageId();
            ProjectSnapshot snapshot = value.getSnapshot();
            StoryPackageManifest.RequiredResources required = value.getManifest()
                .getRequiredResources();
            Map<String, ?> primaryStories = value.getManifest()
                .isDgrsV1() ? snapshot.getCanonicalStories() : snapshot.getStories();
            String primaryStoryType = value.getManifest()
                .isDgrsV1() ? "canonical Story" : "Story";
            requireIds(primaryStories.keySet(), Collections.singleton(value.getStoryId()), primaryStoryType, owner);
            requireIds(
                primaryStories.keySet(),
                ids(value, Collections.singletonList(required.getStory()), primaryStoryType),
                "declared " + primaryStoryType,
                owner);
            requireIds(
                snapshot.getCanonicalStories()
                    .keySet(),
                ids(value, required.getCanonicalStories(), "canonical Story"),
                "canonical Story",
                owner);
            requireIds(
                snapshot.getCanonicalStoryMemberships()
                    .keySet(),
                ids(value, required.getCanonicalMemberships(), "Story membership"),
                "Story membership",
                owner);
            putAllShared(actors, actorOrigins, snapshot.getActors(), required.getActors(), "Actor", value);
            putAllShared(items, itemOrigins, snapshot.getItems(), required.getItems(), "Item", value);
            putAllShared(
                itemGroups,
                itemGroupOrigins,
                snapshot.getItemGroups(),
                required.getItemGroups(),
                "Item Group",
                value);
            putAllShared(
                dialogues,
                dialogueOrigins,
                snapshot.getDialogues(),
                required.getDialogues(),
                "Dialogue",
                value);
            putAllShared(quests, questOrigins, snapshot.getQuests(), required.getQuests(), "Quest", value);
            putAllExclusive(stories, snapshot.getStories(), "Story", owner);
            putAllExclusive(canonicalStories, snapshot.getCanonicalStories(), "canonical Story", owner);
            putAllShared(
                sessions,
                sessionOrigins,
                snapshot.getCanonicalSessions(),
                required.getSessions(),
                "Session",
                value);
            putAllShared(tasks, taskOrigins, snapshot.getCanonicalTasks(), required.getTasks(), "Task", value);
            putAllExclusive(memberships, snapshot.getCanonicalStoryMemberships(), "Story membership", owner);
            storyLogicConnections.addAll(
                value.getStoryLogicGraph()
                    .getConnections());
        }
        try {
            // Package-local loading may defer full-ID external references. The installed
            // set is the resolution boundary and must be validated atomically before it
            // can become a candidate snapshot.
            new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader().loadPackageContent(
                canonicalStories,
                sessions,
                tasks,
                memberships,
                actors.keySet(),
                items.keySet(),
                itemGroups.keySet());
        } catch (darkgrey.rpg.graph.canonical.CanonicalProjectContentException exception) {
            throw new ProjectLoadException(
                "Invalid merged canonical Story Package content: " + exception.getMessage(),
                exception);
        }
        CanonicalStoryLogicGraph storyLogicGraph = new CanonicalStoryLogicGraph(storyLogicConnections);
        try {
            new CanonicalStoryLogicGraphLoader().validate(storyLogicGraph, canonicalStories);
        } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException exception) {
            throw new ProjectLoadException(
                "Invalid merged Story public Logic graph: " + exception.getMessage(),
                exception);
        }
        CanonicalProjectContent canonical = new CanonicalProjectContent(
            canonicalStories,
            sessions,
            tasks,
            memberships,
            storyLogicGraph);
        return new ProjectSnapshot(
            new ProjectDefinition(2, "installed_story_packages", "Installed Story Packages"),
            actors,
            items,
            itemGroups,
            dialogues,
            quests,
            stories,
            canonical);
    }

    private static <T> void putAllExclusive(Map<String, T> target, Map<String, T> source, String type, String packageId)
        throws ProjectLoadException {
        for (Map.Entry<String, T> entry : source.entrySet())
            if (target.put(entry.getKey(), entry.getValue()) != null) throw new ProjectLoadException(
                "Duplicate " + type + " ID '" + entry.getKey() + "' while merging package '" + packageId + "'.");
    }

    /** Referenced resources may appear in more than one package, but only as byte-identical definitions. */
    private static <T> void putAllShared(Map<String, T> target, Map<String, byte[]> origins, Map<String, T> source,
        List<String> declaredPaths, String type, LoadedStoryPackage value) throws ProjectLoadException {
        Map<String, String> paths = declaredPaths(value, declaredPaths, type);
        requireIds(source.keySet(), paths.keySet(), type, value.getPackageId());
        for (Map.Entry<String, T> entry : source.entrySet()) {
            byte[] bytes = read(value, paths.get(entry.getKey()), type, entry.getKey());
            if (!target.containsKey(entry.getKey())) {
                target.put(entry.getKey(), entry.getValue());
                origins.put(entry.getKey(), bytes);
            } else if (!Arrays.equals(origins.get(entry.getKey()), bytes)) {
                throw new ProjectLoadException(
                    "Conflicting shared " + type
                        + " ID '"
                        + entry.getKey()
                        + "' while merging package '"
                        + value.getPackageId()
                        + "'.");
            }
        }
    }

    private static Map<String, String> declaredPaths(LoadedStoryPackage value, List<String> paths, String type)
        throws ProjectLoadException {
        Map<String, String> result = new LinkedHashMap<String, String>();
        for (String path : paths) {
            String id = id(value, path, type);
            if (result.put(id, path) != null) throw new ProjectLoadException(
                "Duplicate declared " + type + " ID '" + id + "' in package '" + value.getPackageId() + "'.");
        }
        return result;
    }

    private static Set<String> ids(LoadedStoryPackage value, List<String> paths, String type)
        throws ProjectLoadException {
        Set<String> result = new HashSet<String>();
        for (String path : paths) {
            String id = id(value, path, type);
            if (!result.add(id)) throw new ProjectLoadException("Duplicate declared resource ID '" + id + "'.");
        }
        return result;
    }

    private static String id(LoadedStoryPackage value, String path, String type) throws ProjectLoadException {
        try {
            com.google.gson.JsonObject json = new com.google.gson.JsonParser()
                .parse(new String(read(value, path, type, path), java.nio.charset.StandardCharsets.UTF_8))
                .getAsJsonObject();
            String field = "Story membership".equals(type) ? "story_id" : "id";
            if ("Actor".equals(type) && json.has("type")) field = "individual".equals(
                json.get("type")
                    .getAsString()) ? "npc_id" : "group_id";
            if ("Item".equals(type)) field = "item_id";
            if ("Item Group".equals(type)) field = "group_id";
            String id = json.get(field)
                .getAsString();
            if (!darkgrey.rpg.identity.DgrResourceId.isCompatibleId(id))
                throw new IllegalArgumentException("Invalid resource ID.");
            return id;
        } catch (RuntimeException exception) {
            throw new ProjectLoadException(
                "Cannot resolve declared " + type + " content identity at '" + path + "'.",
                exception);
        }
    }

    private static void requireIds(Set<String> actual, Set<String> declared, String type, String packageId)
        throws ProjectLoadException {
        if (!new HashSet<String>(actual).equals(new HashSet<String>(declared))) throw new ProjectLoadException(
            "Manifest " + type + " IDs do not match package contents for '" + packageId + "'.");
    }

    private static byte[] read(LoadedStoryPackage value, String path, String type, String id)
        throws ProjectLoadException {
        byte[] bytes = value.getDeclaredResourceBytes(path);
        if (bytes == null) throw new ProjectLoadException(
            "Cannot read declared " + type + " ID '" + id + "' from package '" + value.getPackageId() + "'.");
        return bytes;
    }

    private static Set<String> claimedResourceIds(LoadedStoryPackage value) {
        Set<String> claimed = new LinkedHashSet<String>();
        claimed.add(value.getStoryId());
        CanonicalStoryMembership membership = value.getSnapshot()
            .getCanonicalStoryMembership(value.getStoryId());
        if (membership == null) return claimed;
        CanonicalStoryMembershipSet owned = membership.getOwnedResources();
        claimed.addAll(owned.getActors());
        claimed.addAll(owned.getItems());
        claimed.addAll(owned.getItemGroups());
        claimed.addAll(owned.getSessions());
        claimed.addAll(owned.getTasks());
        return claimed;
    }
}
