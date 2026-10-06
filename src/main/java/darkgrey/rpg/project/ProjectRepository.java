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

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentException;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.ResourceAddressJson;
import darkgrey.rpg.identity.StoryUid;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.story.StoryDefinition;

public final class ProjectRepository {

    private static final Logger LOG = LogManager.getLogger(ProjectRepository.class);
    private static final Pattern NODE_ID = Pattern.compile("[a-z0-9][a-z0-9_.-]*");
    private static final Set<String> PROJECT_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList("schema_version", "identity_format", "id", "display_name", "project_origin_code")));
    private static final Set<String> LEGACY_ACTOR_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(Arrays.asList("schema_version", "id", "display_name", "notes", "tags", "home_story_id")));
    private static final Set<String> ACTOR_CURRENT_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList(
                "schema_version",
                "identity_format",
                "type",
                "npc_id",
                "group_id",
                "display_name",
                "tags",
                "home_story_id",
                "default_portrait_ref",
                "portrait_variants")));
    private static final Set<String> ITEM_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList("schema_version", "identity_format", "type", "item_id", "display_name", "tags")));
    private static final Set<String> ITEM_GROUP_FIELDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList("schema_version", "identity_format", "type", "group_id", "display_name", "tags")));
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
        validateProjectIdentityFormat(file, json, schema);
        String projectId = requiredProjectId(file, json);
        String projectOriginCode = optionalNonEmptyString(file, json, "project_origin_code");
        return new ProjectDefinition(
            schema,
            projectId,
            requiredString(file, json, "display_name"),
            projectOriginCode == null ? projectId : projectOriginCode);
    }

    private static void validateProjectIdentityFormat(File file, JsonObject json, int schema)
        throws ProjectLoadException {
        validateSchema(file, schema, 3);
        if (!"story-uid-v1".equals(requiredString(file, json, "identity_format"))) {
            throw new ProjectLoadException("Project requires identity_format story-uid-v1: " + file);
        }
    }

    private static void requireIdentityFormat(File file, JsonObject json) throws ProjectLoadException {
        if (!"story-uid-v1".equals(requiredString(file, json, "identity_format")))
            throw new ProjectLoadException("Current identity format is required: " + file);
    }

    private static String requiredAddress(File file, JsonObject json, String field, ResourceAddress.Kind kind)
        throws ProjectLoadException {
        try {
            ResourceAddress address = ResourceAddressJson.parse(
                json.has(field) ? json.get(field)
                    .toString() : null);
            if (address.getKind() != kind) throw new IllegalArgumentException("Resource kind mismatch");
            return address.toKey();
        } catch (IOException | IllegalArgumentException exception) {
            throw new ProjectLoadException("Invalid resource address " + field + " in " + file, exception);
        }
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
        validateProjectIdentityFormat(projectFile, projectJson, projectSchema);
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
        for (String retired : new String[] { "stories", "dialogues", "quests" }) {
            File directory = new File(projectDirectory, retired);
            if (directory.isDirectory() && !jsonFiles(directory, retired).isEmpty())
                throw new ProjectLoadException("Legacy resource directory is unsupported: " + directory);
        }
        Map<String, DialogueDefinition> dialogues = Collections.emptyMap();
        Map<String, QuestDefinition> quests = Collections.emptyMap();
        Map<String, StoryDefinition> stories = Collections.emptyMap();
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
        validateSchema(file, version, 2);
        requireIdentityFormat(file, json);
        String type = requiredString(file, json, "type");
        if (!expectedType.equals(type))
            throw new ProjectLoadException("Item resource type must be '" + expectedType + "' in " + file);
        String id = requiredAddress(
            file,
            json,
            individual ? "item_id" : "group_id",
            individual ? ResourceAddress.Kind.ITEM : ResourceAddress.Kind.ITEM_GROUP);
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
        throw new ProjectLoadException("Legacy Quest format is unsupported; use a current canonical Task: " + file);
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
        throw new ProjectLoadException(
            "Legacy Dialogue format is unsupported; use a current canonical Session: " + file);
    }

    private static ActorDefinition loadActor(File actorFile) throws ProjectLoadException {
        return loadActor(actorFile, readObject(actorFile));
    }

    private static ActorDefinition loadActor(File actorFile, JsonObject json) throws ProjectLoadException {
        int schemaVersion = requiredInt(actorFile, json, "schema_version");
        validateSchema(actorFile, schemaVersion, 5);
        requireIdentityFormat(actorFile, json);

        rejectUnknownFields(actorFile, json, ACTOR_CURRENT_FIELDS);
        String type = requiredString(actorFile, json, "type");
        String id;
        if (ActorDefinition.TYPE_INDIVIDUAL.equals(type)) {
            id = requiredAddress(actorFile, json, "npc_id", ResourceAddress.Kind.ACTOR);
            if (json.has("group_id")) {
                throw new ProjectLoadException(
                    "Individual Actor cannot contain group_id in " + actorFile.getAbsolutePath());
            }
        } else if (ActorDefinition.TYPE_COLLECTIVE.equals(type)) {
            id = requiredAddress(actorFile, json, "group_id", ResourceAddress.Kind.ACTOR);
            if (json.has("npc_id")) {
                throw new ProjectLoadException(
                    "Collective Actor cannot contain npc_id in " + actorFile.getAbsolutePath());
            }
        } else {
            throw new ProjectLoadException(
                "Actor type must be individual or collective in " + actorFile.getAbsolutePath());
        }
        String displayName = requiredString(actorFile, json, "display_name");
        List<String> tags = optionalStringList(actorFile, json, "tags");
        String homeStoryId = requiredString(actorFile, json, "home_story_id");
        if (!StoryUid.isValid(homeStoryId) || !ResourceAddress.fromKey(id)
            .getStoryUid()
            .getValue()
            .equals(homeStoryId))
            throw new ProjectLoadException("Actor owner does not match home Story UID: " + actorFile);
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
        if (json.get(field)
            .isJsonNull()) throw new ProjectLoadException("Field '" + field + "' cannot be null in " + file);
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
