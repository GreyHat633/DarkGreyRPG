package darkgrey.rpg.project;

import java.io.BufferedReader;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.nio.file.FileVisitResult;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.SimpleFileVisitor;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.dialogue.ChoiceNode;
import darkgrey.rpg.dialogue.ChoiceOption;
import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.DialogueNode;
import darkgrey.rpg.dialogue.EndNode;
import darkgrey.rpg.dialogue.JumpNode;
import darkgrey.rpg.dialogue.LineNode;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentException;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader;
import darkgrey.rpg.identity.DgrResourceId;
import darkgrey.rpg.quest.CollectItemObjective;
import darkgrey.rpg.quest.InteractActorObjective;
import darkgrey.rpg.quest.KillEntityObjective;
import darkgrey.rpg.quest.ObjectiveGroup;
import darkgrey.rpg.quest.ObjectiveGroupMode;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;
import darkgrey.rpg.quest.ReachLocationObjective;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryLoader;

public final class ProjectRepository {

    private static final Logger LOG = LogManager.getLogger(ProjectRepository.class);
    private static final Pattern NODE_ID = Pattern.compile("[a-z0-9][a-z0-9_.-]*");
    private static final Set<String> PROJECT_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("schema_version", "id", "display_name", "project_origin_code")));
    private static final Set<String> LEGACY_ACTOR_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("schema_version", "id", "display_name", "notes", "tags", "home_story_id")));
    private static final Set<String> ACTOR_CURRENT_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList(
                "schema_version",
                "type",
                "npc_id",
                "group_id",
                "display_name",
                "tags",
                "home_story_id",
                "default_portrait_ref",
                "portrait_variants")));
    private static final Set<String> ITEM_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("schema_version", "type", "item_id", "display_name", "tags")));
    private static final Set<String> ITEM_GROUP_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("schema_version", "type", "group_id", "display_name", "tags")));
    private static final Set<String> DIALOGUE_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList(
                "schema_version",
                "id",
                "title",
                "display_name",
                "home_story_id",
                "speakers",
                "entry",
                "nodes",
                "metadata")));
    private static final Set<String> LINE_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "speaker", "text", "next")));
    private static final Set<String> CHOICE_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "prompt", "choices")));
    private static final Set<String> CHOICE_OPTION_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("text", "next")));
    private static final Set<String> JUMP_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "target")));
    private static final Set<String> END_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "result")));
    private static final Set<String> METADATA_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("notes", "tags")));
    private static final Set<String> QUEST_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList(
                "schema_version",
                "id",
                "title",
                "display_name",
                "description",
                "home_story_id",
                "objectives",
                "objective_groups",
                "metadata")));
    private static final Set<String> KILL_OBJECTIVE_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "description", "entity", "required")));
    private static final Set<String> COLLECT_OBJECTIVE_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("id", "type", "description", "item", "metadata", "required")));
    private static final Set<String> REACH_OBJECTIVE_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("id", "type", "description", "dimension", "x", "y", "z", "radius")));
    private static final Set<String> INTERACT_OBJECTIVE_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "type", "description", "actor_id", "required")));
    private static final Set<String> OBJECTIVE_GROUP_FIELDS = Collections
        .unmodifiableSet(new HashSet<String>(Arrays.asList("id", "mode", "objectives")));

    private final File projectDirectory;
    private volatile ProjectSnapshot snapshot = ProjectSnapshot.empty();
    private volatile ReloadResult lastReload = ReloadResult.failure("Project has not been loaded");
    private volatile long snapshotRevision;

    public ProjectRepository(File projectDirectory) {
        this.projectDirectory = projectDirectory;
    }

    /** Reuses the strict project parser for one detached DGRS entry. */
    public static ProjectDefinition readPackagedProject(byte[] bytes, String source) throws ProjectLoadException {
        File file = packageContext(source);
        JsonObject json = readObject(bytes, file);
        rejectUnknownFields(file, json, PROJECT_FIELDS);
        int schema = requiredInt(file, json, "schema_version");
        validateSchema(file, schema, 1, 2);
        String projectId = requiredProjectId(file, json);
        String projectOriginCode = optionalNonEmptyString(file, json, "project_origin_code");
        return new ProjectDefinition(
            schema,
            projectId,
            requiredString(file, json, "display_name"),
            projectOriginCode == null ? projectId : projectOriginCode);
    }

    /** Reuses the strict Actor parser for one detached DGRS entry. */
    public static ActorDefinition readPackagedActor(byte[] bytes, String source) throws ProjectLoadException {
        File file = packageContext(source);
        return loadActor(file, readObject(bytes, file));
    }

    /** Reuses the strict Item/Item Group parser for one detached DGRS entry. */
    public static ItemResourceDefinition readPackagedItem(byte[] bytes, String source, String expectedType)
        throws ProjectLoadException {
        File file = packageContext(source);
        return loadItem(file, expectedType, readObject(bytes, file));
    }

    /** Reuses the strict Dialogue parser for one detached DGRS entry. */
    public static DialogueDefinition readPackagedDialogue(byte[] bytes, String source,
        Map<String, ActorDefinition> actors) throws ProjectLoadException {
        File file = packageContext(source);
        return loadDialogue(file, actors, readObject(bytes, file));
    }

    /** Reuses the strict Quest parser for one detached DGRS entry. */
    public static QuestDefinition readPackagedQuest(byte[] bytes, String source) throws ProjectLoadException {
        File file = packageContext(source);
        return loadQuest(file, readObject(bytes, file));
    }

    private static File packageContext(String source) throws ProjectLoadException {
        if (source == null || source.trim()
            .isEmpty()) throw new ProjectLoadException("DGRS entry source is required");
        return new File(source);
    }

    public synchronized ReloadResult reload() {
        try {
            ProjectSnapshot loaded = loadSnapshot();
            snapshot = loaded;
            snapshotRevision++;
            lastReload = ReloadResult.success(loaded);
        } catch (ProjectLoadException exception) {
            lastReload = ReloadResult.failure(exception.getMessage());
            LOG.error("Could not reload project from " + projectDirectory.getAbsolutePath(), exception);
        }
        return lastReload;
    }

    public ProjectSnapshot getSnapshot() {
        return snapshot;
    }

    /** Monotonic server-side fence for catalogs derived from the published snapshot. */
    public long getSnapshotRevision() {
        return snapshotRevision;
    }

    /**
     * Installs an already parsed and validated immutable snapshot. Story Package
     * integration uses this as the single atomic publication boundary; callers
     * must build the complete candidate before invoking it.
     */
    public synchronized ReloadResult installSnapshot(ProjectSnapshot loaded) {
        if (loaded == null) throw new IllegalArgumentException("Project snapshot is required.");
        snapshot = loaded;
        snapshotRevision++;
        lastReload = ReloadResult.success(loaded);
        return lastReload;
    }

    /**
     * Publishes an empty authoritative snapshot while retaining the failure that
     * made the previous snapshot unavailable. Package deletion uses this path so
     * status reporting cannot turn a failed base reload into a false success.
     */
    public synchronized ReloadResult installUnavailableSnapshot(String summary) {
        snapshot = ProjectSnapshot.empty();
        snapshotRevision++;
        lastReload = ReloadResult.failure(summary);
        return lastReload;
    }

    public ReloadResult getLastReload() {
        return lastReload;
    }

    public File getProjectDirectory() {
        return projectDirectory;
    }

    private ProjectSnapshot loadSnapshot() throws ProjectLoadException {
        if (!projectDirectory.isDirectory()) {
            throw new ProjectLoadException("Project directory does not exist: " + projectDirectory.getAbsolutePath());
        }

        File projectFile = new File(projectDirectory, "project.json");
        JsonObject projectJson = readObject(projectFile);
        rejectUnknownFields(projectFile, projectJson, PROJECT_FIELDS);

        int projectSchema = requiredInt(projectFile, projectJson, "schema_version");
        validateSchema(projectFile, projectSchema, 1, 2);
        String projectId = requiredProjectId(projectFile, projectJson);
        String projectName = requiredString(projectFile, projectJson, "display_name");
        String projectOriginCode = optionalNonEmptyString(projectFile, projectJson, "project_origin_code");
        ProjectDefinition project = new ProjectDefinition(
            projectSchema,
            projectId,
            projectName,
            projectOriginCode == null ? projectId : projectOriginCode);

        File actorsDirectory = new File(projectDirectory, "actors");
        if (!actorsDirectory.isDirectory() || unsafePath(actorsDirectory.toPath())) {
            throw new ProjectLoadException("Missing actors directory: " + actorsDirectory.getAbsolutePath());
        }

        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        for (File actorFile : jsonFiles(actorsDirectory, "actors")) {
            ActorDefinition actor = loadActor(actorFile);
            if (actors.put(actor.getId(), actor) != null) {
                throw new ProjectLoadException("Duplicate actor id '" + actor.getId() + "'");
            }
        }
        Map<String, ItemResourceDefinition> items = loadItems("items", ItemResourceDefinition.TYPE_INDIVIDUAL);
        Map<String, ItemResourceDefinition> itemGroups = loadItems(
            "item_groups",
            ItemResourceDefinition.TYPE_COLLECTIVE);
        Map<String, DialogueDefinition> dialogues = loadDialogues(actors);
        Map<String, QuestDefinition> quests = loadQuests();
        Map<String, StoryDefinition> stories = StoryLoader.load(projectDirectory, actors, dialogues, quests);
        CanonicalProjectContent canonicalContent;
        try {
            canonicalContent = new CanonicalProjectContentLoader()
                .load(projectDirectory, actors.keySet(), items.keySet(), itemGroups.keySet());
        } catch (CanonicalProjectContentException exception) {
            throw new ProjectLoadException(
                "Could not load canonical project content: " + exception.getMessage(),
                exception);
        }
        return new ProjectSnapshot(project, actors, items, itemGroups, dialogues, quests, stories, canonicalContent);
    }

    private Map<String, ItemResourceDefinition> loadItems(String directoryName, String expectedType)
        throws ProjectLoadException {
        File directory = new File(projectDirectory, directoryName);
        if (!directory.exists()) return Collections.emptyMap();
        if (!directory.isDirectory() || unsafePath(directory.toPath()))
            throw new ProjectLoadException("Item resource path is not a directory: " + directory.getAbsolutePath());
        Map<String, ItemResourceDefinition> result = new LinkedHashMap<String, ItemResourceDefinition>();
        for (File file : jsonFiles(directory, directoryName)) {
            ItemResourceDefinition definition = loadItem(file, expectedType);
            if (result.put(definition.getId(), definition) != null)
                throw new ProjectLoadException("Duplicate item resource id '" + definition.getId() + "'");
        }
        return result;
    }

    private static ItemResourceDefinition loadItem(File file, String expectedType) throws ProjectLoadException {
        return loadItem(file, expectedType, readObject(file));
    }

    private static ItemResourceDefinition loadItem(File file, String expectedType, JsonObject json)
        throws ProjectLoadException {
        boolean individual = ItemResourceDefinition.TYPE_INDIVIDUAL.equals(expectedType);
        rejectUnknownFields(file, json, individual ? ITEM_FIELDS : ITEM_GROUP_FIELDS);
        int version = requiredInt(file, json, "schema_version");
        validateSchema(file, version, 1);
        String type = requiredString(file, json, "type").toLowerCase();
        if (!expectedType.equals(type))
            throw new ProjectLoadException("Item resource type must be '" + expectedType + "' in " + file);
        String id = requiredResourceId(file, json, individual ? "item_id" : "group_id");
        String expectedName = id + ".json";
        if (!DgrResourceId.isFullId(id) && !expectedName.equals(file.getName()))
            throw new ProjectLoadException("Item resource filename must be '" + expectedName + "': " + file);
        return new ItemResourceDefinition(
            version,
            type,
            id,
            requiredString(file, json, "display_name"),
            optionalStringList(file, json, "tags"));
    }

    private Map<String, QuestDefinition> loadQuests() throws ProjectLoadException {
        File questsDirectory = new File(projectDirectory, "quests");
        if (!questsDirectory.isDirectory()) {
            throw new ProjectLoadException("Missing quests directory: " + questsDirectory.getAbsolutePath());
        }
        File[] questFiles = questsDirectory.listFiles();
        if (questFiles == null) {
            throw new ProjectLoadException("Cannot list quests directory: " + questsDirectory.getAbsolutePath());
        }
        Arrays.sort(questFiles, new Comparator<File>() {

            @Override
            public int compare(File left, File right) {
                return left.getName()
                    .compareToIgnoreCase(right.getName());
            }
        });
        Map<String, QuestDefinition> quests = new LinkedHashMap<String, QuestDefinition>();
        for (File questFile : questFiles) {
            if (!questFile.isFile() || !questFile.getName()
                .toLowerCase()
                .endsWith(".json")) {
                continue;
            }
            QuestDefinition quest = loadQuest(questFile);
            if (quests.put(quest.getId(), quest) != null) {
                throw new ProjectLoadException("Duplicate quest id '" + quest.getId() + "'");
            }
        }
        return quests;
    }

    private static QuestDefinition loadQuest(File file) throws ProjectLoadException {
        return loadQuest(file, readObject(file));
    }

    private static QuestDefinition loadQuest(File file, JsonObject json) throws ProjectLoadException {
        rejectUnknownFields(file, json, QUEST_FIELDS);
        int schemaVersion = requiredInt(file, json, "schema_version");
        validateSchema(file, schemaVersion, 1, 2);
        String id = requiredResourceId(file, json, "id");
        if (!DgrResourceId.isFullId(id) && !file.getName()
            .equals(id + ".json")) {
            throw new ProjectLoadException("Quest file name must match its id: " + file.getAbsolutePath());
        }
        String title = requiredString(file, json, "title");
        validateStudioResourceFields(file, json, schemaVersion);
        String description = requiredString(file, json, "description");
        List<QuestObjective> objectives = loadObjectives(file, json.get("objectives"));
        Map<String, QuestObjective> objectivesById = new LinkedHashMap<String, QuestObjective>();
        for (QuestObjective objective : objectives) {
            if (objectivesById.put(objective.getId(), objective) != null) {
                throw new ProjectLoadException(
                    "Duplicate objective id '" + objective.getId() + "' in " + file.getAbsolutePath());
            }
        }
        List<ObjectiveGroup> groups = loadObjectiveGroups(file, json.get("objective_groups"), objectivesById);
        JsonObject metadata = optionalObject(file, json, "metadata");
        rejectUnknownFields(file, metadata, METADATA_FIELDS);
        return new QuestDefinition(
            schemaVersion,
            id,
            title,
            description,
            objectives,
            groups,
            optionalString(file, metadata, "notes", ""),
            optionalStringList(file, metadata, "tags"));
    }

    private static List<QuestObjective> loadObjectives(File file, JsonElement value) throws ProjectLoadException {
        if (value == null || !value.isJsonArray()
            || value.getAsJsonArray()
                .size() == 0) {
            throw new ProjectLoadException("Quest objectives must be a non-empty array in " + file.getAbsolutePath());
        }
        List<QuestObjective> objectives = new ArrayList<QuestObjective>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonObject()) {
                throw new ProjectLoadException("Quest objectives must be objects in " + file.getAbsolutePath());
            }
            JsonObject objective = element.getAsJsonObject();
            String id = requiredResourceId(file, objective, "id");
            String type = requiredString(file, objective, "type").toLowerCase();
            String description = requiredString(file, objective, "description");
            if ("kill_entity".equals(type)) {
                rejectUnknownFields(file, objective, KILL_OBJECTIVE_FIELDS);
                objectives.add(
                    new KillEntityObjective(
                        id,
                        description,
                        requiredString(file, objective, "entity"),
                        requiredPositiveInt(file, objective, "required")));
            } else if ("collect_item".equals(type)) {
                rejectUnknownFields(file, objective, COLLECT_OBJECTIVE_FIELDS);
                objectives.add(
                    new CollectItemObjective(
                        id,
                        description,
                        requiredString(file, objective, "item"),
                        optionalInt(file, objective, "metadata", -1),
                        requiredPositiveInt(file, objective, "required")));
            } else if ("reach_location".equals(type)) {
                rejectUnknownFields(file, objective, REACH_OBJECTIVE_FIELDS);
                double radius = requiredDouble(file, objective, "radius");
                if (radius <= 0.0D) {
                    throw new ProjectLoadException("ReachLocation radius must be positive in " + file);
                }
                objectives.add(
                    new ReachLocationObjective(
                        id,
                        description,
                        requiredInt(file, objective, "dimension"),
                        requiredDouble(file, objective, "x"),
                        requiredDouble(file, objective, "y"),
                        requiredDouble(file, objective, "z"),
                        radius));
            } else if ("interact_actor".equals(type)) {
                rejectUnknownFields(file, objective, INTERACT_OBJECTIVE_FIELDS);
                objectives.add(
                    new InteractActorObjective(
                        id,
                        description,
                        requiredResourceId(file, objective, "actor_id"),
                        requiredPositiveInt(file, objective, "required")));
            } else {
                throw new ProjectLoadException(
                    "Unsupported Quest objective type '" + type + "' in " + file.getAbsolutePath());
            }
        }
        return objectives;
    }

    private static List<ObjectiveGroup> loadObjectiveGroups(File file, JsonElement value,
        Map<String, QuestObjective> objectivesById) throws ProjectLoadException {
        if (value == null || !value.isJsonArray()
            || value.getAsJsonArray()
                .size() == 0) {
            throw new ProjectLoadException(
                "Quest objective_groups must be a non-empty array in " + file.getAbsolutePath());
        }
        List<ObjectiveGroup> groups = new ArrayList<ObjectiveGroup>();
        Set<String> groupIds = new HashSet<String>();
        Set<String> assignedObjectives = new HashSet<String>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonObject()) {
                throw new ProjectLoadException("Objective groups must be objects in " + file.getAbsolutePath());
            }
            JsonObject group = element.getAsJsonObject();
            rejectUnknownFields(file, group, OBJECTIVE_GROUP_FIELDS);
            String groupId = requiredResourceId(file, group, "id");
            if (!groupIds.add(groupId)) {
                throw new ProjectLoadException("Duplicate objective group id '" + groupId + "' in " + file);
            }
            ObjectiveGroupMode mode;
            try {
                mode = ObjectiveGroupMode.valueOf(requiredString(file, group, "mode").toUpperCase());
            } catch (IllegalArgumentException exception) {
                throw new ProjectLoadException("Objective group mode must be ALL, ANY, or SEQUENCE in " + file);
            }
            List<String> objectiveIds = requiredResourceIdList(file, group, "objectives");
            for (String objectiveId : objectiveIds) {
                if (!objectivesById.containsKey(objectiveId)) {
                    throw new ProjectLoadException(
                        "Objective group '" + groupId + "' references missing objective '" + objectiveId + "'");
                }
                if (!assignedObjectives.add(objectiveId)) {
                    throw new ProjectLoadException(
                        "Objective '" + objectiveId + "' belongs to more than one group in " + file);
                }
            }
            groups.add(new ObjectiveGroup(groupId, mode, objectiveIds));
        }
        if (assignedObjectives.size() != objectivesById.size()) {
            throw new ProjectLoadException(
                "Every Quest objective must belong to exactly one objective group in " + file);
        }
        return groups;
    }

    private Map<String, DialogueDefinition> loadDialogues(Map<String, ActorDefinition> actors)
        throws ProjectLoadException {
        File dialoguesDirectory = new File(projectDirectory, "dialogues");
        if (!dialoguesDirectory.isDirectory()) {
            throw new ProjectLoadException("Missing dialogues directory: " + dialoguesDirectory.getAbsolutePath());
        }
        File[] dialogueFiles = dialoguesDirectory.listFiles();
        if (dialogueFiles == null) {
            throw new ProjectLoadException("Cannot list dialogues directory: " + dialoguesDirectory.getAbsolutePath());
        }
        Arrays.sort(dialogueFiles, new Comparator<File>() {

            @Override
            public int compare(File left, File right) {
                return left.getName()
                    .compareToIgnoreCase(right.getName());
            }
        });

        Map<String, DialogueDefinition> dialogues = new LinkedHashMap<String, DialogueDefinition>();
        for (File dialogueFile : dialogueFiles) {
            if (!dialogueFile.isFile() || !dialogueFile.getName()
                .toLowerCase()
                .endsWith(".json")) {
                continue;
            }
            DialogueDefinition dialogue = loadDialogue(dialogueFile, actors);
            if (dialogues.put(dialogue.getId(), dialogue) != null) {
                throw new ProjectLoadException("Duplicate dialogue id '" + dialogue.getId() + "'");
            }
        }
        return dialogues;
    }

    private static DialogueDefinition loadDialogue(File file, Map<String, ActorDefinition> actors)
        throws ProjectLoadException {
        return loadDialogue(file, actors, readObject(file));
    }

    private static DialogueDefinition loadDialogue(File file, Map<String, ActorDefinition> actors, JsonObject json)
        throws ProjectLoadException {
        rejectUnknownFields(file, json, DIALOGUE_FIELDS);
        int schemaVersion = requiredInt(file, json, "schema_version");
        validateSchema(file, schemaVersion, 1, 2);
        String id = requiredResourceId(file, json, "id");
        if (!DgrResourceId.isFullId(id) && !file.getName()
            .equals(id + ".json")) {
            throw new ProjectLoadException("Dialogue file name must match its id: " + file.getAbsolutePath());
        }
        String title = requiredString(file, json, "title");
        validateStudioResourceFields(file, json, schemaVersion);
        // An End-only Dialogue is a valid Studio draft and has no speakers yet.
        // Line nodes below still require their speaker to be declared here.
        List<String> speakers = optionalResourceIdList(file, json, "speakers");
        for (String speaker : speakers) {
            if (!actors.containsKey(speaker)) {
                throw new ProjectLoadException(
                    "Missing Actor '" + speaker + "' referenced by " + file.getAbsolutePath());
            }
        }
        String entry = requiredNodeId(file, json, "entry");
        List<DialogueNode> nodes = loadNodes(file, json.get("nodes"), speakers);
        Map<String, DialogueNode> nodesById = new LinkedHashMap<String, DialogueNode>();
        for (DialogueNode node : nodes) {
            if (nodesById.put(node.getId(), node) != null) {
                throw new ProjectLoadException("Duplicate node id '" + node.getId() + "' in " + file.getAbsolutePath());
            }
        }
        if (!nodesById.containsKey(entry)) {
            throw new ProjectLoadException(
                "Dialogue entry node does not exist: " + entry + " in " + file.getAbsolutePath());
        }
        validateConnections(file, nodes, nodesById);

        JsonObject metadata = optionalObject(file, json, "metadata");
        rejectUnknownFields(file, metadata, METADATA_FIELDS);
        String notes = optionalString(file, metadata, "notes", "");
        List<String> tags = optionalStringList(file, metadata, "tags");
        return new DialogueDefinition(schemaVersion, id, title, speakers, entry, nodes, notes, tags);
    }

    private static List<DialogueNode> loadNodes(File file, JsonElement value, List<String> speakers)
        throws ProjectLoadException {
        if (value == null || !value.isJsonArray()
            || value.getAsJsonArray()
                .size() == 0) {
            throw new ProjectLoadException("Dialogue nodes must be a non-empty array in " + file.getAbsolutePath());
        }
        List<DialogueNode> nodes = new ArrayList<DialogueNode>();
        for (JsonElement element : value.getAsJsonArray()) {
            if (!element.isJsonObject()) {
                throw new ProjectLoadException("Dialogue nodes must be objects in " + file.getAbsolutePath());
            }
            JsonObject node = element.getAsJsonObject();
            String type = requiredString(file, node, "type").toLowerCase();
            String nodeId = requiredNodeId(file, node, "id");
            if ("line".equals(type)) {
                rejectUnknownFields(file, node, LINE_FIELDS);
                String speaker = requiredResourceId(file, node, "speaker");
                if (!speakers.contains(speaker)) {
                    throw new ProjectLoadException(
                        "Line node '" + nodeId + "' uses undeclared speaker '" + speaker + "'");
                }
                nodes.add(
                    new LineNode(
                        nodeId,
                        speaker,
                        requiredString(file, node, "text"),
                        requiredNodeId(file, node, "next")));
            } else if ("choice".equals(type)) {
                rejectUnknownFields(file, node, CHOICE_FIELDS);
                JsonElement choicesValue = node.get("choices");
                if (choicesValue == null || !choicesValue.isJsonArray()
                    || choicesValue.getAsJsonArray()
                        .size() == 0) {
                    throw new ProjectLoadException("Choice node '" + nodeId + "' must contain choices");
                }
                List<ChoiceOption> choices = new ArrayList<ChoiceOption>();
                for (JsonElement choiceElement : choicesValue.getAsJsonArray()) {
                    if (!choiceElement.isJsonObject()) {
                        throw new ProjectLoadException("Choice options must be objects in " + file.getAbsolutePath());
                    }
                    JsonObject choice = choiceElement.getAsJsonObject();
                    rejectUnknownFields(file, choice, CHOICE_OPTION_FIELDS);
                    choices.add(
                        new ChoiceOption(requiredString(file, choice, "text"), requiredNodeId(file, choice, "next")));
                }
                nodes.add(new ChoiceNode(nodeId, optionalString(file, node, "prompt", ""), choices));
            } else if ("jump".equals(type)) {
                rejectUnknownFields(file, node, JUMP_FIELDS);
                nodes.add(new JumpNode(nodeId, requiredNodeId(file, node, "target")));
            } else if ("end".equals(type)) {
                rejectUnknownFields(file, node, END_FIELDS);
                nodes.add(new EndNode(nodeId, requiredNodeId(file, node, "result")));
            } else {
                throw new ProjectLoadException(
                    "Unsupported Dialogue node type '" + type + "' in " + file.getAbsolutePath());
            }
        }
        return nodes;
    }

    private static void validateConnections(File file, List<DialogueNode> nodes, Map<String, DialogueNode> nodesById)
        throws ProjectLoadException {
        for (DialogueNode node : nodes) {
            List<String> targets = new ArrayList<String>();
            if (node instanceof LineNode) {
                targets.add(((LineNode) node).getNext());
            } else if (node instanceof ChoiceNode) {
                for (ChoiceOption option : ((ChoiceNode) node).getChoices()) {
                    targets.add(option.getNext());
                }
            } else if (node instanceof JumpNode) {
                targets.add(((JumpNode) node).getTarget());
            }
            for (String target : targets) {
                if (!nodesById.containsKey(target)) {
                    throw new ProjectLoadException(
                        "Node '" + node
                            .getId() + "' references missing node '" + target + "' in " + file.getAbsolutePath());
                }
            }
        }
    }

    private static ActorDefinition loadActor(File actorFile) throws ProjectLoadException {
        return loadActor(actorFile, readObject(actorFile));
    }

    private static ActorDefinition loadActor(File actorFile, JsonObject json) throws ProjectLoadException {
        int schemaVersion = requiredInt(actorFile, json, "schema_version");
        validateSchema(actorFile, schemaVersion, 1, 2, 4);
        if (schemaVersion < 3) {
            rejectUnknownFields(actorFile, json, LEGACY_ACTOR_FIELDS);
            return loadLegacyActor(actorFile, json, schemaVersion);
        }

        rejectUnknownFields(actorFile, json, ACTOR_CURRENT_FIELDS);
        String type = requiredString(actorFile, json, "type").toLowerCase();
        String id;
        if (ActorDefinition.TYPE_INDIVIDUAL.equals(type)) {
            id = requiredResourceId(actorFile, json, "npc_id");
            if (json.has("group_id")) {
                throw new ProjectLoadException(
                    "Individual Actor cannot contain group_id in " + actorFile.getAbsolutePath());
            }
        } else if (ActorDefinition.TYPE_COLLECTIVE.equals(type)) {
            id = requiredResourceId(actorFile, json, "group_id");
            if (json.has("npc_id")) {
                throw new ProjectLoadException(
                    "Collective Actor cannot contain npc_id in " + actorFile.getAbsolutePath());
            }
        } else {
            throw new ProjectLoadException(
                "Actor type must be individual or collective in " + actorFile.getAbsolutePath());
        }
        validateActorFileName(actorFile, id);
        String displayName = requiredString(actorFile, json, "display_name");
        List<String> tags = optionalStringList(actorFile, json, "tags");
        String homeStoryId = requiredResourceId(actorFile, json, "home_story_id");
        try {
            return new ActorDefinition(
                schemaVersion,
                type,
                id,
                displayName,
                "",
                tags,
                homeStoryId,
                ActorPortraits.parse(json));
        } catch (IllegalArgumentException exception) {
            throw new ProjectLoadException("Invalid Actor portraits in " + actorFile.getAbsolutePath(), exception);
        }
    }

    private static ActorDefinition loadLegacyActor(File actorFile, JsonObject json, int schemaVersion)
        throws ProjectLoadException {
        String id = requiredResourceId(actorFile, json, "id");
        validateActorFileName(actorFile, id);
        String displayName = requiredString(actorFile, json, "display_name");
        String notes = optionalString(actorFile, json, "notes", "");
        List<String> tags = optionalStringList(actorFile, json, "tags");
        String homeStoryId = optionalString(actorFile, json, "home_story_id", null);
        if (schemaVersion >= 2 && (homeStoryId == null || !DgrResourceId.isCompatibleId(homeStoryId))) {
            throw new ProjectLoadException(
                "Actor schema_version 2 requires a valid home_story_id in " + actorFile.getAbsolutePath());
        }
        return new ActorDefinition(schemaVersion, id, displayName, notes, tags, homeStoryId);
    }

    private static void validateActorFileName(File actorFile, String id) throws ProjectLoadException {
        if (DgrResourceId.isFullId(id)) return;
        String expectedFileName = id + ".json";
        if (!actorFile.getName()
            .equals(expectedFileName)) {
            throw new ProjectLoadException(
                "Actor file name must match its id: expected " + expectedFileName + ", got " + actorFile.getName());
        }
    }

    /** Lists JSON resources recursively without following links or reparse points. */
    private static List<File> jsonFiles(File directory, final String label) throws ProjectLoadException {
        final Path root = directory.toPath();
        final List<Path> paths = new ArrayList<Path>();
        try {
            Files.walkFileTree(root, new SimpleFileVisitor<Path>() {

                @Override
                public FileVisitResult preVisitDirectory(Path path, BasicFileAttributes attributes) {
                    return unsafePath(path) || attributes.isOther() ? FileVisitResult.SKIP_SUBTREE
                        : FileVisitResult.CONTINUE;
                }

                @Override
                public FileVisitResult visitFile(Path path, BasicFileAttributes attributes) {
                    if (!unsafePath(path) && attributes.isRegularFile()
                        && path.getFileName()
                            .toString()
                            .toLowerCase()
                            .endsWith(".json"))
                        paths.add(path);
                    return FileVisitResult.CONTINUE;
                }
            });
        } catch (IOException exception) {
            throw new ProjectLoadException("Cannot recursively list " + label + " directory: " + directory, exception);
        }
        Collections.sort(paths, new Comparator<Path>() {

            @Override
            public int compare(Path left, Path right) {
                return root.relativize(left)
                    .toString()
                    .compareTo(
                        root.relativize(right)
                            .toString());
            }
        });
        List<File> result = new ArrayList<File>();
        for (Path path : paths) result.add(path.toFile());
        return result;
    }

    private static boolean unsafePath(Path path) {
        if (Files.isSymbolicLink(path)) return true;
        try {
            Object reparse = Files.getAttribute(path, "dos:reparsePoint", LinkOption.NOFOLLOW_LINKS);
            return Boolean.TRUE.equals(reparse);
        } catch (IOException | UnsupportedOperationException | IllegalArgumentException ignored) {
            return false;
        }
    }

    private static JsonObject readObject(File file) throws ProjectLoadException {
        if (!file.isFile()) {
            throw new ProjectLoadException("Missing JSON file: " + file.getAbsolutePath());
        }
        try {
            BufferedReader reader = new BufferedReader(
                new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8));
            try {
                return readObject(reader, file);
            } finally {
                reader.close();
            }
        } catch (IOException exception) {
            throw new ProjectLoadException("Cannot read " + file.getAbsolutePath(), exception);
        } catch (RuntimeException exception) {
            throw new ProjectLoadException(
                "Invalid JSON in " + file.getAbsolutePath() + ": " + exception.getMessage(),
                exception);
        }
    }

    private static JsonObject readObject(byte[] bytes, File file) throws ProjectLoadException {
        if (bytes == null) throw new ProjectLoadException("Missing JSON bytes: " + file.getAbsolutePath());
        BufferedReader reader = new BufferedReader(
            new InputStreamReader(new ByteArrayInputStream(bytes), StandardCharsets.UTF_8));
        try {
            return readObject(reader, file);
        } catch (RuntimeException exception) {
            throw new ProjectLoadException(
                "Invalid JSON in " + file.getAbsolutePath() + ": " + exception.getMessage(),
                exception);
        } finally {
            try {
                reader.close();
            } catch (IOException ignored) {}
        }
    }

    private static JsonObject readObject(BufferedReader reader, File file) throws ProjectLoadException {
        JsonElement root = new JsonParser().parse(reader);
        if (!root.isJsonObject()) {
            throw new ProjectLoadException("JSON root must be an object: " + file.getAbsolutePath());
        }
        return root.getAsJsonObject();
    }

    private static void rejectUnknownFields(File file, JsonObject json, Set<String> allowed)
        throws ProjectLoadException {
        for (Map.Entry<String, JsonElement> entry : json.entrySet()) {
            if (!allowed.contains(entry.getKey())) {
                throw new ProjectLoadException(
                    "Unsupported field '" + entry.getKey() + "' in " + file.getAbsolutePath());
            }
        }
    }

    private static void validateSchema(File file, int schemaVersion, int... supportedVersions)
        throws ProjectLoadException {
        for (int supportedVersion : supportedVersions) {
            if (schemaVersion == supportedVersion) {
                return;
            }
        }
        throw new ProjectLoadException("Unsupported schema_version " + schemaVersion + " in " + file.getAbsolutePath());
    }

    private static int requiredInt(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber()) {
            throw new ProjectLoadException("Field '" + field + "' must be an integer in " + file.getAbsolutePath());
        }
        try {
            return value.getAsInt();
        } catch (RuntimeException exception) {
            throw new ProjectLoadException("Field '" + field + "' must be an integer in " + file.getAbsolutePath());
        }
    }

    private static int requiredPositiveInt(File file, JsonObject json, String field) throws ProjectLoadException {
        int value = requiredInt(file, json, field);
        if (value <= 0) {
            throw new ProjectLoadException("Field '" + field + "' must be positive in " + file.getAbsolutePath());
        }
        return value;
    }

    private static int optionalInt(File file, JsonObject json, String field, int fallback) throws ProjectLoadException {
        if (!json.has(field) || json.get(field)
            .isJsonNull()) {
            return fallback;
        }
        return requiredInt(file, json, field);
    }

    private static double requiredDouble(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber()) {
            throw new ProjectLoadException("Field '" + field + "' must be a number in " + file.getAbsolutePath());
        }
        try {
            return value.getAsDouble();
        } catch (RuntimeException exception) {
            throw new ProjectLoadException("Field '" + field + "' must be a number in " + file.getAbsolutePath());
        }
    }

    private static String requiredProjectId(File file, JsonObject json) throws ProjectLoadException {
        String value = requiredString(file, json, "id");
        if (!value.matches("[A-Za-z0-9][A-Za-z0-9_.-]*"))
            throw new ProjectLoadException("Invalid project id '" + value + "' in " + file);
        return value;
    }

    private static String requiredResourceId(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = optionalString(file, json, field, null);
        if (value == null || value.isEmpty() || !DgrResourceId.isCompatibleId(value)) {
            throw new ProjectLoadException(
                "Field '" + field + "' has invalid resource id '" + value + "' in " + file.getAbsolutePath());
        }
        return value;
    }

    private static String requiredNodeId(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = requiredString(file, json, field);
        if (!NODE_ID.matcher(value)
            .matches()) {
            throw new ProjectLoadException(
                "Field '" + field + "' has invalid node id '" + value + "' in " + file.getAbsolutePath());
        }
        return value;
    }

    private static String requiredString(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = optionalString(file, json, field, null);
        if (value == null || value.trim()
            .isEmpty()) {
            throw new ProjectLoadException(
                "Field '" + field + "' must be a non-empty string in " + file.getAbsolutePath());
        }
        return value.trim();
    }

    /** Optional field with the same strict non-empty string contract as requiredString. */
    private static String optionalNonEmptyString(File file, JsonObject json, String field) throws ProjectLoadException {
        if (!json.has(field)) return null;
        requiredString(file, json, field);
        return json.get(field)
            .getAsString();
    }

    private static String optionalString(File file, JsonObject json, String field, String fallback)
        throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return fallback;
        }
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString()) {
            throw new ProjectLoadException("Field '" + field + "' must be a string in " + file.getAbsolutePath());
        }
        return value.getAsString();
    }

    /**
     * Studio 2.1 schema 2 adds editor identity and ownership fields while the
     * Runtime continues to execute the stable title/node/objective semantics.
     */
    private static void validateStudioResourceFields(File file, JsonObject json, int schemaVersion)
        throws ProjectLoadException {
        if (schemaVersion < 2) {
            return;
        }
        requiredString(file, json, "display_name");
        requiredResourceId(file, json, "home_story_id");
    }

    private static List<String> optionalStringList(File file, JsonObject json, String field)
        throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return Collections.emptyList();
        }
        if (!value.isJsonArray()) {
            throw new ProjectLoadException("Field '" + field + "' must be an array in " + file.getAbsolutePath());
        }
        JsonArray array = value.getAsJsonArray();
        List<String> result = new ArrayList<String>();
        for (JsonElement entry : array) {
            if (!entry.isJsonPrimitive() || !entry.getAsJsonPrimitive()
                .isString()) {
                throw new ProjectLoadException(
                    "Field '" + field + "' must contain only strings in " + file.getAbsolutePath());
            }
            String tag = entry.getAsString()
                .trim();
            if (!tag.isEmpty() && !result.contains(tag)) {
                result.add(tag);
            }
        }
        return result;
    }

    private static List<String> requiredResourceIdList(File file, JsonObject json, String field)
        throws ProjectLoadException {
        List<String> values = optionalResourceIdList(file, json, field);
        if (values.isEmpty()) {
            throw new ProjectLoadException("Field '" + field + "' must contain at least one resource ID in " + file);
        }
        return values;
    }

    private static List<String> optionalResourceIdList(File file, JsonObject json, String field)
        throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) return Collections.emptyList();
        if (!value.isJsonArray()) {
            throw new ProjectLoadException("Field '" + field + "' must be an array in " + file.getAbsolutePath());
        }
        List<String> values = new ArrayList<String>();
        for (JsonElement entry : value.getAsJsonArray()) {
            if (!entry.isJsonPrimitive() || !entry.getAsJsonPrimitive()
                .isString()) {
                throw new ProjectLoadException(
                    "Field '" + field + "' must contain only strings in " + file.getAbsolutePath());
            }
            String id = entry.getAsString();
            if (!DgrResourceId.isCompatibleId(id)) throw new ProjectLoadException(
                "Field '" + field + "' contains invalid resource ID '" + id + "' in " + file);
            values.add(id);
        }
        return values;
    }

    private static JsonObject optionalObject(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement value = json.get(field);
        if (value == null || value.isJsonNull()) {
            return new JsonObject();
        }
        if (!value.isJsonObject()) {
            throw new ProjectLoadException("Field '" + field + "' must be an object in " + file.getAbsolutePath());
        }
        return value.getAsJsonObject();
    }

    public static final class ReloadResult {

        private final boolean successful;
        private final String projectDisplayName;
        private final int actorCount;
        private final int dialogueCount;
        private final int questCount;
        private final int storyCount;
        private final int itemCount;
        private final int itemGroupCount;
        private final int sessionCount;
        private final int taskCount;
        private final String summary;

        private ReloadResult(boolean successful, String projectDisplayName, int actorCount, int dialogueCount,
            int questCount, int storyCount, int itemCount, int itemGroupCount, int sessionCount, int taskCount,
            String summary) {
            this.successful = successful;
            this.projectDisplayName = projectDisplayName;
            this.actorCount = actorCount;
            this.dialogueCount = dialogueCount;
            this.questCount = questCount;
            this.storyCount = storyCount;
            this.itemCount = itemCount;
            this.itemGroupCount = itemGroupCount;
            this.sessionCount = sessionCount;
            this.taskCount = taskCount;
            this.summary = summary;
        }

        public static ReloadResult success(ProjectSnapshot snapshot) {
            if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
            Set<String> storyIds = new HashSet<String>(
                snapshot.getStories()
                    .keySet());
            storyIds.addAll(
                snapshot.getCanonicalStories()
                    .keySet());
            return success(
                snapshot.getProject()
                    .getDisplayName(),
                snapshot.getActors()
                    .size(),
                snapshot.getDialogues()
                    .size(),
                snapshot.getQuests()
                    .size(),
                storyIds.size(),
                snapshot.getItems()
                    .size(),
                snapshot.getItemGroups()
                    .size(),
                snapshot.getCanonicalSessions()
                    .size(),
                snapshot.getCanonicalTasks()
                    .size());
        }

        public static ReloadResult success(String projectDisplayName, int actorCount, int dialogueCount, int questCount,
            int storyCount) {
            return success(projectDisplayName, actorCount, dialogueCount, questCount, storyCount, 0, 0, 0, 0);
        }

        public static ReloadResult success(String projectDisplayName, int actorCount, int dialogueCount, int questCount,
            int storyCount, int itemCount, int itemGroupCount, int sessionCount, int taskCount) {
            return new ReloadResult(
                true,
                projectDisplayName,
                actorCount,
                dialogueCount,
                questCount,
                storyCount,
                itemCount,
                itemGroupCount,
                sessionCount,
                taskCount,
                "已加载项目“" + projectDisplayName
                    + "”：故事 "
                    + storyCount
                    + "，角色 "
                    + actorCount
                    + "，物品 "
                    + itemCount
                    + "，物品组 "
                    + itemGroupCount
                    + "，会话 "
                    + sessionCount
                    + "，任务 "
                    + taskCount
                    + "。");
        }

        public static ReloadResult failure(String summary) {
            return new ReloadResult(false, "", 0, 0, 0, 0, 0, 0, 0, 0, summary);
        }

        public boolean isSuccessful() {
            return successful;
        }

        public String getProjectDisplayName() {
            return projectDisplayName;
        }

        public int getActorCount() {
            return actorCount;
        }

        public int getDialogueCount() {
            return dialogueCount;
        }

        public int getQuestCount() {
            return questCount;
        }

        public int getStoryCount() {
            return storyCount;
        }

        public int getItemCount() {
            return itemCount;
        }

        public int getItemGroupCount() {
            return itemGroupCount;
        }

        public int getSessionCount() {
            return sessionCount;
        }

        public int getTaskCount() {
            return taskCount;
        }

        public String getSummary() {
            return summary;
        }
    }
}
