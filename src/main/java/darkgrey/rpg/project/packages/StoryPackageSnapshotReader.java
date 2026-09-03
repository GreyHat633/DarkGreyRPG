package darkgrey.rpg.project.packages;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoader;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentException;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipException;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipLoader;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryLoader;

/** Builds one validated ProjectSnapshot directly from detached DGRS entries. */
final class StoryPackageSnapshotReader {

    private StoryPackageSnapshotReader() {}

    static Result read(DgrsArchiveReader archive, StoryPackageManifest manifest) throws ProjectLoadException {
        if (archive == null || manifest == null)
            throw new IllegalArgumentException("archive and manifest are required");
        StoryPackageManifest.RequiredResources required = manifest.getRequiredResources();
        Map<String, byte[]> declaredBytes = requiredBytes(archive, required);
        declaredBytes.put("project.json", archive.readBytes("project.json"));
        ProjectDefinition project = ProjectRepository
            .readPackagedProject(archive.readBytes("project.json"), source(archive, "project.json"));

        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        for (String path : required.getActors()) {
            ActorDefinition value = ProjectRepository.readPackagedActor(archive.readBytes(path), source(archive, path));
            put(actors, value.getId(), value, "Actor", path);
        }
        Map<String, ItemResourceDefinition> items = readItems(
            archive,
            required.getItems(),
            ItemResourceDefinition.TYPE_INDIVIDUAL,
            "Item");
        Map<String, ItemResourceDefinition> itemGroups = readItems(
            archive,
            required.getItemGroups(),
            ItemResourceDefinition.TYPE_COLLECTIVE,
            "Item Group");
        Map<String, DialogueDefinition> dialogues = new LinkedHashMap<String, DialogueDefinition>();
        for (String path : required.getDialogues()) {
            DialogueDefinition value = ProjectRepository
                .readPackagedDialogue(archive.readBytes(path), source(archive, path), actors);
            put(dialogues, value.getId(), value, "Dialogue", path);
        }
        Map<String, QuestDefinition> quests = new LinkedHashMap<String, QuestDefinition>();
        for (String path : required.getQuests()) {
            QuestDefinition value = ProjectRepository.readPackagedQuest(archive.readBytes(path), source(archive, path));
            put(quests, value.getId(), value, "Quest", path);
        }

        Map<String, StoryDefinition> stories = new LinkedHashMap<String, StoryDefinition>();
        if (required.getStory()
            .startsWith("stories/")) {
            StoryDefinition story = StoryLoader.loadPackagedStory(
                archive.readBytes(required.getStory()),
                source(archive, required.getStory()),
                actors,
                dialogues,
                quests);
            put(stories, story.getId(), story, "Story", required.getStory());
            StoryLoader.validatePackagedStories(stories);
        }

        Map<String, CanonicalGraphResource> canonicalStories = readCanonical(
            archive,
            required.getCanonicalStories(),
            CanonicalGraphResourceKind.STORY,
            "canonical Story");
        Map<String, CanonicalGraphResource> sessions = readCanonical(
            archive,
            required.getSessions(),
            CanonicalGraphResourceKind.SESSION,
            "Session");
        Map<String, CanonicalGraphResource> tasks = readCanonical(
            archive,
            required.getTasks(),
            CanonicalGraphResourceKind.TASK,
            "Task");
        Map<String, CanonicalStoryMembership> memberships = readMemberships(
            archive,
            required.getCanonicalMemberships());
        CanonicalProjectContent canonical;
        try {
            canonical = new CanonicalProjectContentLoader().loadPackageContent(
                canonicalStories,
                sessions,
                tasks,
                memberships,
                actors.keySet(),
                items.keySet(),
                itemGroups.keySet());
        } catch (CanonicalProjectContentException exception) {
            throw new ProjectLoadException(
                "Could not load canonical DGRS content: " + exception.getMessage(),
                exception);
        }
        CanonicalStoryLogicGraph logicGraph = readLogicGraph(archive, required.getStoryLogicGraph());
        for (CanonicalStoryLogicConnection connection : logicGraph.getConnections()) if (!manifest.getStoryId()
            .equals(connection.getSourceStoryId()))
            throw new ProjectLoadException(
                "Story Package may only own public Logic connections sourced by its story_id.");
        ProjectSnapshot snapshot = new ProjectSnapshot(
            project,
            actors,
            items,
            itemGroups,
            dialogues,
            quests,
            stories,
            canonical);
        return new Result(snapshot, logicGraph, declaredBytes);
    }

    private static Map<String, ItemResourceDefinition> readItems(DgrsArchiveReader archive, List<String> paths,
        String expectedType, String type) throws ProjectLoadException {
        Map<String, ItemResourceDefinition> values = new LinkedHashMap<String, ItemResourceDefinition>();
        for (String path : paths) {
            ItemResourceDefinition value = ProjectRepository
                .readPackagedItem(archive.readBytes(path), source(archive, path), expectedType);
            put(values, value.getId(), value, type, path);
        }
        return values;
    }

    private static Map<String, CanonicalGraphResource> readCanonical(DgrsArchiveReader archive, List<String> paths,
        CanonicalGraphResourceKind kind, String type) throws ProjectLoadException {
        Map<String, CanonicalGraphResource> values = new LinkedHashMap<String, CanonicalGraphResource>();
        CanonicalGraphResourceLoader loader = new CanonicalGraphResourceLoader();
        for (String path : paths) try {
            CanonicalGraphResource value = loader.load(archive.readBytes(path), fileName(path), kind);
            put(values, value.getId(), value, type, path);
        } catch (CanonicalGraphResourceException exception) {
            throw new ProjectLoadException(
                "Invalid " + type + " entry '" + path + "': " + exception.getMessage(),
                exception);
        }
        return values;
    }

    private static Map<String, CanonicalStoryMembership> readMemberships(DgrsArchiveReader archive, List<String> paths)
        throws ProjectLoadException {
        Map<String, CanonicalStoryMembership> values = new LinkedHashMap<String, CanonicalStoryMembership>();
        CanonicalStoryMembershipLoader loader = new CanonicalStoryMembershipLoader();
        for (String path : paths) try {
            CanonicalStoryMembership value = loader.load(archive.readBytes(path), fileName(path));
            put(values, value.getStoryId(), value, "Story membership", path);
        } catch (CanonicalStoryMembershipException exception) {
            throw new ProjectLoadException(
                "Invalid Story membership entry '" + path + "': " + exception.getMessage(),
                exception);
        }
        return values;
    }

    private static CanonicalStoryLogicGraph readLogicGraph(DgrsArchiveReader archive, String path)
        throws ProjectLoadException {
        if (path == null) return CanonicalStoryLogicGraph.empty();
        try {
            return new CanonicalStoryLogicGraphLoader().loadUnresolved(archive.readBytes(path), source(archive, path));
        } catch (CanonicalGraphResourceException exception) {
            throw new ProjectLoadException(
                "Invalid Story Package public Logic graph: " + exception.getMessage(),
                exception);
        }
    }

    private static Map<String, byte[]> requiredBytes(DgrsArchiveReader archive,
        StoryPackageManifest.RequiredResources required) throws ProjectLoadException {
        Set<String> paths = new LinkedHashSet<String>();
        add(paths, required.getStory());
        add(paths, required.getActors());
        add(paths, required.getItems());
        add(paths, required.getItemGroups());
        add(paths, required.getDialogues());
        add(paths, required.getQuests());
        add(paths, required.getCanonicalStories());
        add(paths, required.getCanonicalMemberships());
        add(paths, required.getSessions());
        add(paths, required.getTasks());
        if (required.getStoryLogicGraph() != null) add(paths, required.getStoryLogicGraph());
        Map<String, byte[]> result = new LinkedHashMap<String, byte[]>();
        for (String path : paths) {
            archive.readUtf8(path);
            result.put(path, archive.readBytes(path));
        }
        return result;
    }

    private static void add(Set<String> target, List<String> paths) {
        target.addAll(paths);
    }

    private static void add(Set<String> target, String path) {
        target.add(path);
    }

    private static <T> void put(Map<String, T> target, String id, T value, String type, String path)
        throws ProjectLoadException {
        if (target.put(id, value) != null)
            throw new ProjectLoadException("Duplicate " + type + " ID '" + id + "' in DGRS entry '" + path + "'.");
    }

    private static String source(DgrsArchiveReader archive, String path) {
        return archive.getSourceIdentity() + "!/" + path;
    }

    private static String fileName(String path) {
        int slash = path.lastIndexOf('/');
        return slash < 0 ? path : path.substring(slash + 1);
    }

    static final class Result {

        private final ProjectSnapshot snapshot;
        private final CanonicalStoryLogicGraph storyLogicGraph;
        private final Map<String, byte[]> declaredBytes;

        Result(ProjectSnapshot snapshot, CanonicalStoryLogicGraph storyLogicGraph, Map<String, byte[]> declaredBytes) {
            this.snapshot = snapshot;
            this.storyLogicGraph = storyLogicGraph;
            this.declaredBytes = Collections.unmodifiableMap(new LinkedHashMap<String, byte[]>(declaredBytes));
        }

        ProjectSnapshot getSnapshot() {
            return snapshot;
        }

        CanonicalStoryLogicGraph getStoryLogicGraph() {
            return storyLogicGraph;
        }

        Map<String, byte[]> getDeclaredBytes() {
            return declaredBytes;
        }
    }
}
