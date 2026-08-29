package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.project.ProjectLoadException;

/** Strict server-side manifest for a Story Package. */
public final class StoryPackageManifest {

    public static final int CURRENT_SCHEMA_VERSION = 1;
    private static final Set<String> ROOT_FIELDS = set(
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
        "tasks");

    private final int schemaVersion;
    private final String packageId;
    private final String packageVersion;
    private final String storyId;
    private final int storySchemaVersion;
    private final RequiredResources requiredResources;

    private StoryPackageManifest(int schemaVersion, String packageId, String packageVersion, String storyId,
        int storySchemaVersion, RequiredResources requiredResources) {
        this.schemaVersion = schemaVersion;
        this.packageId = packageId;
        this.packageVersion = packageVersion;
        this.storyId = storyId;
        this.storySchemaVersion = storySchemaVersion;
        this.requiredResources = requiredResources;
    }

    public static StoryPackageManifest read(File file) throws ProjectLoadException {
        try {
            FileReader reader = new FileReader(file);
            try {
                JsonElement root = new JsonParser().parse(reader);
                if (!root.isJsonObject()) throw failure(file, "Manifest root must be an object");
                JsonObject json = root.getAsJsonObject();
                rejectUnknown(file, json, ROOT_FIELDS);
                int schema = requiredInt(file, json, "schema_version");
                if (schema != CURRENT_SCHEMA_VERSION)
                    throw failure(file, "Unsupported package schema_version " + schema);
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
                    paths(file, resources, "tasks"));
                return new StoryPackageManifest(schema, packageId, packageVersion, storyId, storySchema, required);
            } finally {
                reader.close();
            }
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (IOException | RuntimeException exception) {
            throw new ProjectLoadException("Invalid Story Package manifest " + file, exception);
        }
    }

    public int getSchemaVersion() {
        return schemaVersion;
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
            sessions, tasks;

        private RequiredResources(String story, List<String> actors, List<String> items, List<String> itemGroups,
            List<String> dialogues, List<String> quests, List<String> canonicalStories,
            List<String> canonicalMemberships, List<String> sessions, List<String> tasks) {
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
            if (path.trim()
                .isEmpty() || path.indexOf('\\') >= 0
                || path.startsWith("/")
                || path.contains("..")) throw failure(file, "Unsafe resource path: " + path);
            if (!seen.add(path)) throw failure(file, "Duplicate resource path: " + path);
            values.add(path);
        }
        return values;
    }

    private static String requiredPath(File file, JsonObject json, String field) throws ProjectLoadException {
        String value = requiredString(file, json, field);
        if (value.indexOf('\\') >= 0 || value.startsWith("/") || value.contains(".."))
            throw failure(file, "Unsafe resource path: " + value);
        return value;
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
        if (!value.matches("[a-z0-9][a-z0-9_.-]*")) throw failure(file, "Invalid " + field + " '" + value + "'");
        return value;
    }

    private static ProjectLoadException failure(File file, String message) {
        return new ProjectLoadException(message + " in " + file.getAbsolutePath());
    }

    private static Set<String> set(String... values) {
        return Collections.unmodifiableSet(new HashSet<String>(Arrays.asList(values)));
    }
}
