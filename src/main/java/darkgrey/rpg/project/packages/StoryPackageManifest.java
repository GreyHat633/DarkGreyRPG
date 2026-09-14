package darkgrey.rpg.project.packages;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.project.ProjectLoadException;

/** Strict server-side manifest for a Story Package. */
public final class StoryPackageManifest {

    public static final int CURRENT_SCHEMA_VERSION = 1;
    public static final String CURRENT_FORMAT = "dgrs";
    public static final int CURRENT_FORMAT_VERSION = 1;
    public static final String CURRENT_PRODUCER = "DarkGreyRPGStudio";
    private static final Set<String> ROOT_FIELDS = set(
        "format",
        "format_version",
        "producer",
        "producer_version",
        "schema_version",
        "package_id",
        "package_version",
        "story_id",
        "story_schema_version",
        "required_resources");
    private static final Set<String> RESOURCE_FIELDS = set(
        "story",
        "actors",
        "items",
        "item_groups",
        "dialogues",
        "quests",
        "canonical_stories",
        "canonical_memberships",
        "sessions",
        "tasks",
        "story_logic_graph",
        "media");

    private final int schemaVersion;
    private final String format;
    private final Integer formatVersion;
    private final String producer;
    private final String producerVersion;
    private final String packageId;
    private final String packageVersion;
    private final String storyId;
    private final int storySchemaVersion;
    private final RequiredResources requiredResources;

    private StoryPackageManifest(String format, Integer formatVersion, String producer, String producerVersion,
        int schemaVersion, String packageId, String packageVersion, String storyId, int storySchemaVersion,
        RequiredResources requiredResources) {
        this.format = format;
        this.formatVersion = formatVersion;
        this.producer = producer;
        this.producerVersion = producerVersion;
        this.schemaVersion = schemaVersion;
        this.packageId = packageId;
        this.packageVersion = packageVersion;
        this.storyId = storyId;
        this.storySchemaVersion = storySchemaVersion;
        this.requiredResources = requiredResources;
    }

    public static StoryPackageManifest read(File file) throws ProjectLoadException {
        try {
            InputStreamReader reader = new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8);
            try {
                return parse(reader, file);
            } finally {
                reader.close();
            }
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (IOException | RuntimeException exception) {
            throw new ProjectLoadException("Invalid Story Package manifest " + file, exception);
        }
    }

    public static StoryPackageManifest read(byte[] bytes, String source) throws ProjectLoadException {
        if (bytes == null) throw new ProjectLoadException("Story Package manifest bytes are required");
        File context = new File(source == null ? "manifest.json" : source);
        InputStreamReader reader = new InputStreamReader(new ByteArrayInputStream(bytes), StandardCharsets.UTF_8);
        try {
            return parse(reader, context);
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (RuntimeException exception) {
            throw new ProjectLoadException("Invalid Story Package manifest " + context, exception);
        } finally {
            try {
                reader.close();
            } catch (IOException ignored) {}
        }
    }

    private static StoryPackageManifest parse(InputStreamReader reader, File file) throws ProjectLoadException {
        JsonElement root = new JsonParser().parse(reader);
        if (!root.isJsonObject()) throw failure(file, "Manifest root must be an object");
        JsonObject json = root.getAsJsonObject();
        rejectUnknown(file, json, ROOT_FIELDS);
        String format = optionalString(file, json, "format");
        Integer formatVersion = optionalInt(file, json, "format_version");
        String producer = optionalString(file, json, "producer");
        String producerVersion = optionalString(file, json, "producer_version");
        boolean hasDgrsIdentity = format != null || formatVersion != null
            || producer != null
            || producerVersion != null;
        if (hasDgrsIdentity && (format == null || formatVersion == null || producer == null || producerVersion == null))
            throw failure(file, "DGRS identity fields must be present together");
        if (hasDgrsIdentity) {
            if (!CURRENT_FORMAT.equals(format)) throw failure(file, "Unsupported package format '" + format + "'");
            if (formatVersion.intValue() != CURRENT_FORMAT_VERSION)
                throw failure(file, "Unsupported DGRS format_version " + formatVersion);
            if (!CURRENT_PRODUCER.equals(producer)) throw failure(file, "Unsupported DGRS producer '" + producer + "'");
        }
        int schema = requiredInt(file, json, "schema_version");
        if (schema != CURRENT_SCHEMA_VERSION) throw failure(file, "Unsupported package schema_version " + schema);
        String packageId = requiredId(file, json, "package_id");
        String packageVersion = requiredString(file, json, "package_version");
        String storyId = requiredId(file, json, "story_id");
        int storySchema = requiredInt(file, json, "story_schema_version");
        if (storySchema <= 0) throw failure(file, "story_schema_version must be positive");
        JsonObject resources = requiredObject(file, json, "required_resources");
        rejectUnknown(file, resources, RESOURCE_FIELDS);
        RequiredResources required = new RequiredResources(
            requiredPath(file, resources, "story"),
            paths(file, resources, "actors"),
            paths(file, resources, "items"),
            paths(file, resources, "item_groups"),
            paths(file, resources, "dialogues"),
            paths(file, resources, "quests"),
            paths(file, resources, "canonical_stories"),
            paths(file, resources, "canonical_memberships"),
            paths(file, resources, "sessions"),
            paths(file, resources, "tasks"),
            optionalPath(file, resources, "story_logic_graph"),
            paths(file, resources, "media"));
        return new StoryPackageManifest(
            format,
            formatVersion,
            producer,
            producerVersion,
            schema,
            packageId,
            packageVersion,
            storyId,
            storySchema,
            required);
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public boolean isDgrsV1() {
        return CURRENT_FORMAT.equals(format) && formatVersion != null
            && formatVersion.intValue() == CURRENT_FORMAT_VERSION
            && CURRENT_PRODUCER.equals(producer)
            && producerVersion != null
            && !producerVersion.trim()
                .isEmpty();
    }

    public String getFormat() {
        return format;
    }

    public Integer getFormatVersion() {
        return formatVersion;
    }

    public String getProducer() {
        return producer;
    }

    public String getProducerVersion() {
        return producerVersion;
    }

    public String getPackageId() {
        return packageId;
    }

    public String getPackageVersion() {
        return packageVersion;
    }

    public String getStoryId() {
        return storyId;
    }

    public int getStorySchemaVersion() {
        return storySchemaVersion;
    }

    public RequiredResources getRequiredResources() {
        return requiredResources;
    }

    private static RequiredResources immutable(RequiredResources source) {
        return source;
    }

    public static final class RequiredResources {

        private final String story;
        private final List<String> actors, items, itemGroups, dialogues, quests, canonicalStories, canonicalMemberships,
            sessions, tasks, media;
        private final String storyLogicGraph;

        private RequiredResources(String story, List<String> actors, List<String> items, List<String> itemGroups,
            List<String> dialogues, List<String> quests, List<String> canonicalStories,
            List<String> canonicalMemberships, List<String> sessions, List<String> tasks, String storyLogicGraph,
            List<String> media) {
            this.media = freeze(media);
            for (String path : media) if (!darkgrey.rpg.graph.canonical.CanonicalMediaReference.isValid(path))
                throw new IllegalArgumentException("Invalid media reference: " + path);
            this.story = story;
            this.actors = freeze(actors);
            this.items = freeze(items);
            this.itemGroups = freeze(itemGroups);
            this.dialogues = freeze(dialogues);
            this.quests = freeze(quests);
            this.canonicalStories = freeze(canonicalStories);
            this.canonicalMemberships = freeze(canonicalMemberships);
            this.sessions = freeze(sessions);
            this.tasks = freeze(tasks);
            this.storyLogicGraph = storyLogicGraph;
        }

        public List<String> getMedia() {
            return media;
        }

        public String getStory() {
            return story;
        }

        public List<String> getActors() {
            return actors;
        }

        public List<String> getItems() {
            return items;
        }

        public List<String> getItemGroups() {
            return itemGroups;
        }

        public List<String> getDialogues() {
            return dialogues;
        }

        public List<String> getQuests() {
            return quests;
        }

        public List<String> getCanonicalStories() {
            return canonicalStories;
        }

        public List<String> getCanonicalMemberships() {
            return canonicalMemberships;
        }

        public List<String> getSessions() {
            return sessions;
        }

        public List<String> getTasks() {
            return tasks;
        }

        public String getStoryLogicGraph() {
            return storyLogicGraph;
        }

        private static List<String> freeze(List<String> values) {
            return Collections.unmodifiableList(new ArrayList<String>(values));
        }
    }

    private static List<String> paths(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement element = json.get(field);
        if (element == null) return Collections.emptyList();
        if (!element.isJsonArray()) throw failure(file, "required_resources." + field + " must be an array");
        List<String> values = new ArrayList<String>();
        Set<String> seen = new HashSet<String>();
        for (JsonElement value : element.getAsJsonArray()) {
            if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
                .isString()) throw failure(file, "Resource paths must be strings");
            String path = value.getAsString();
            validatePath(file, path);
            if (!seen.add(path.toLowerCase(Locale.ROOT)))
                throw failure(file, "Duplicate normalized resource path: " + path);
            values.add(path);
        }
        return values;
    }

    private static String requiredPath(File file, JsonObject json, String field) throws ProjectLoadException {
        requiredString(file, json, field);
        String value = json.get(field)
            .getAsString();
        validatePath(file, value);
        return value;
    }

    private static void validatePath(File file, String path) throws ProjectLoadException {
        if (path == null || path.trim()
            .isEmpty() || path.indexOf('\\') >= 0 || path.startsWith("/") || path.indexOf(':') >= 0)
            throw failure(file, "Unsafe resource path: " + path);
        String[] segments = path.split("/", -1);
        for (String segment : segments) if (segment.length() == 0 || ".".equals(segment) || "..".equals(segment))
            throw failure(file, "Unsafe resource path: " + path);
    }

    private static String optionalPath(File file, JsonObject json, String field) throws ProjectLoadException {
        if (!json.has(field)) return null;
        return requiredPath(file, json, field);
    }

    private static JsonObject requiredObject(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement e = json.get(field);
        if (e == null || !e.isJsonObject()) throw failure(file, field + " must be an object");
        return e.getAsJsonObject();
    }

    private static void rejectUnknown(File file, JsonObject json, Set<String> allowed) throws ProjectLoadException {
        for (java.util.Map.Entry<String, JsonElement> entry : json.entrySet())
            if (!allowed.contains(entry.getKey())) throw failure(file, "Unsupported field '" + entry.getKey() + "'");
    }

    private static int requiredInt(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement e = json.get(field);
        if (e == null || !e.isJsonPrimitive()
            || !e.getAsJsonPrimitive()
                .isNumber())
            throw failure(file, field + " must be an integer");
        return e.getAsInt();
    }

    private static Integer optionalInt(File file, JsonObject json, String field) throws ProjectLoadException {
        if (!json.has(field)) return null;
        return Integer.valueOf(requiredInt(file, json, field));
    }

    private static String optionalString(File file, JsonObject json, String field) throws ProjectLoadException {
        if (!json.has(field)) return null;
        return requiredString(file, json, field);
    }

    private static String requiredString(File file, JsonObject json, String field) throws ProjectLoadException {
        JsonElement e = json.get(field);
        if (e == null || !e.isJsonPrimitive()
            || !e.getAsJsonPrimitive()
                .isString()
            || e.getAsString()
                .trim()
                .isEmpty())
            throw failure(file, field + " must be a non-empty string");
        return e.getAsString()
            .trim();
    }

    private static String requiredId(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = requiredString(file, json, field);
        if (!darkgrey.rpg.identity.DgrResourceId.isCompatibleId(value))
            throw failure(file, "Invalid " + field + " '" + value + "'");
        return value;
    }

    private static ProjectLoadException failure(File file, String message) {
        return new ProjectLoadException(message + " in " + file.getAbsolutePath());
    }

    private static Set<String> set(String... values) {
        return Collections.unmodifiableSet(new HashSet<String>(Arrays.asList(values)));
    }
}
