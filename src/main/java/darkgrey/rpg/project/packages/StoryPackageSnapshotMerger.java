package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
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

    public static ProjectSnapshot merge(Map<String, LoadedStoryPackage> packages) throws ProjectLoadException {
        if (packages == null || packages.isEmpty())
            throw new ProjectLoadException("At least one validated Story Package is required.");
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
            requireIds(
                snapshot.getStories()
                    .keySet(),
                Collections.singleton(value.getStoryId()),
                "Story",
                owner);
            requireIds(
                snapshot.getStories()
                    .keySet(),
                ids(Collections.singletonList(required.getStory())),
                "declared Story",
                owner);
            requireIds(
                snapshot.getCanonicalStories()
                    .keySet(),
                ids(required.getCanonicalStories()),
                "canonical Story",
                owner);
            requireIds(
                snapshot.getCanonicalStoryMemberships()
                    .keySet(),
                ids(required.getCanonicalMemberships()),
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
        Map<String, File> files = declaredFiles(value, declaredPaths, type);
        requireIds(source.keySet(), files.keySet(), type, value.getPackageId());
        for (Map.Entry<String, T> entry : source.entrySet()) {
            byte[] bytes = read(files.get(entry.getKey()), type, entry.getKey(), value.getPackageId());
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

    private static Map<String, File> declaredFiles(LoadedStoryPackage value, List<String> paths, String type)
        throws ProjectLoadException {
        Map<String, File> result = new LinkedHashMap<String, File>();
        for (String path : paths) {
            String id = id(path);
            File file = new File(value.getDirectory(), path.replace('/', File.separatorChar));
            if (result.put(id, file) != null) throw new ProjectLoadException(
                "Duplicate declared " + type + " ID '" + id + "' in package '" + value.getPackageId() + "'.");
        }
        return result;
    }

    private static Set<String> ids(List<String> paths) throws ProjectLoadException {
        Set<String> result = new HashSet<String>();
        for (String path : paths) {
            String id = id(path);
            if (!result.add(id)) throw new ProjectLoadException("Duplicate declared resource ID '" + id + "'.");
        }
        return result;
    }

    private static String id(String path) throws ProjectLoadException {
        int slash = path.lastIndexOf('/');
        String file = slash < 0 ? path : path.substring(slash + 1);
        if (!file.endsWith(".json") || file.length() <= 5)
            throw new ProjectLoadException("Declared resource path must end in a named .json file: " + path);
        return file.substring(0, file.length() - 5);
    }

    private static void requireIds(Set<String> actual, Set<String> declared, String type, String packageId)
        throws ProjectLoadException {
        if (!new HashSet<String>(actual).equals(new HashSet<String>(declared))) throw new ProjectLoadException(
            "Manifest " + type + " IDs do not match package contents for '" + packageId + "'.");
    }

    private static byte[] read(File file, String type, String id, String packageId) throws ProjectLoadException {
        try {
            return Files.readAllBytes(file.toPath());
        } catch (IOException exception) {
            throw new ProjectLoadException(
                "Cannot read declared " + type + " ID '" + id + "' from package '" + packageId + "'.",
                exception);
        }
    }
}
