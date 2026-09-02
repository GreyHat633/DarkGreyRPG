package darkgrey.rpg.graph.canonical;

import java.io.File;
import java.io.IOException;
import java.io.StringReader;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;
import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

/** Strict, read-only loader for schema-version-1/2/3 Story membership manifests. */
public final class CanonicalStoryMembershipLoader {

    private static final Pattern ID_PATTERN = Pattern.compile("^[a-z0-9][a-z0-9_-]*$");
    private static final Set<String> LEGACY_ROOT = set(
        "schema_version",
        "story_id",
        "owned_resources",
        "referenced_resources");
    private static final Set<String> CURRENT_ROOT = set(
        "schema_version",
        "story_id",
        "owned_resources",
        "referenced_resources",
        "display_order");
    private static final Set<String> LEGACY_MEMBERSHIP = set("actors", "sessions", "tasks");
    private static final Set<String> CURRENT_MEMBERSHIP = set("actors", "items", "item_groups", "sessions", "tasks");
    private static final Set<String> DISPLAY_ORDER = set("actors", "items", "sessions", "tasks");

    public CanonicalStoryMembership load(File file) throws CanonicalStoryMembershipException {
        return load(file == null ? null : file.toPath());
    }

    public CanonicalStoryMembership load(Path file) throws CanonicalStoryMembershipException {
        if (file == null) throw CanonicalStoryMembershipException
            .failure("story.membership.file.required", "Canonical Story membership file is required.");
        if (!Files.isRegularFile(file)) throw CanonicalStoryMembershipException
            .failure("story.membership.file.missing", "Canonical Story membership file does not exist: " + file);
        try {
            return parse(
                Files.readAllBytes(file),
                file.getFileName()
                    .toString());
        } catch (IOException exception) {
            throw new CanonicalStoryMembershipException(
                "story.membership.file.read",
                "Could not read canonical Story membership file '" + file + "'.",
                exception);
        }
    }

    /** Parses one detached package entry through the same strict membership parser as file-backed projects. */
    public CanonicalStoryMembership load(byte[] bytes, String fileName) throws CanonicalStoryMembershipException {
        if (bytes == null) throw CanonicalStoryMembershipException
            .failure("story.membership.bytes.required", "Canonical Story membership bytes are required.");
        if (fileName == null || fileName.trim()
            .isEmpty())
            throw CanonicalStoryMembershipException
                .failure("story.membership.file.required", "Canonical Story membership file name is required.");
        return parse(bytes, fileName);
    }

    public List<CanonicalStoryMembership> loadDirectory(File directory) throws CanonicalStoryMembershipException {
        return loadDirectory(directory == null ? null : directory.toPath());
    }

    public List<CanonicalStoryMembership> loadDirectory(Path directory) throws CanonicalStoryMembershipException {
        if (directory == null) throw CanonicalStoryMembershipException
            .failure("story.membership.directory.required", "Canonical Story membership directory is required.");
        if (!Files.isDirectory(directory)) throw CanonicalStoryMembershipException.failure(
            "story.membership.directory.missing",
            "Canonical Story membership directory does not exist: " + directory);
        List<Path> files = new ArrayList<Path>();
        try (DirectoryStream<Path> stream = Files.newDirectoryStream(directory, "*.json")) {
            for (Path path : stream) if (Files.isRegularFile(path)) files.add(path);
        } catch (IOException exception) {
            throw new CanonicalStoryMembershipException(
                "story.membership.directory.read",
                "Could not list canonical Story membership directory '" + directory + "'.",
                exception);
        }
        Collections.sort(files, new Comparator<Path>() {

            @Override
            public int compare(Path left, Path right) {
                return left.getFileName()
                    .toString()
                    .compareTo(
                        right.getFileName()
                            .toString());
            }
        });
        List<CanonicalStoryMembership> result = new ArrayList<CanonicalStoryMembership>();
        Map<String, CanonicalStoryMembership> byId = new LinkedHashMap<String, CanonicalStoryMembership>();
        for (Path file : files) {
            CanonicalStoryMembership membership = load(file);
            if (byId.put(membership.getStoryId(), membership) != null) throw CanonicalStoryMembershipException.failure(
                "story.membership.story_id.duplicate",
                "Duplicate canonical Story membership story_id '" + membership.getStoryId() + "'.");
            result.add(membership);
        }
        return Collections.unmodifiableList(new ArrayList<CanonicalStoryMembership>(result));
    }

    private static CanonicalStoryMembership parse(byte[] bytes, String fileName)
        throws CanonicalStoryMembershipException {
        JsonObject root;
        try {
            JsonReader reader = new JsonReader(new StringReader(new String(bytes, StandardCharsets.UTF_8)));
            reader.setLenient(false);
            JsonElement element = readTree(reader, "story.membership.root");
            if (reader.peek() != JsonToken.END_DOCUMENT) throw CanonicalStoryMembershipException
                .failure("story.membership.invalid", "Trailing JSON content is not allowed.");
            if (!element.isJsonObject()) throw CanonicalStoryMembershipException
                .failure("story.membership.root.invalid", "Story membership root must be an object.");
            root = element.getAsJsonObject();
        } catch (CanonicalStoryMembershipException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new CanonicalStoryMembershipException(
                "story.membership.invalid",
                "Story membership JSON is invalid or contains unsupported fields.",
                exception);
        }
        int version = integer(root, "schema_version", "story.membership.root");
        if (version != CanonicalStoryMembership.LEGACY_SCHEMA_VERSION
            && version != CanonicalStoryMembership.ITEM_MEMBERSHIP_SCHEMA_VERSION
            && version != CanonicalStoryMembership.CURRENT_SCHEMA_VERSION)
            throw CanonicalStoryMembershipException.failure(
                "story.membership.schema_version.unsupported",
                "Unsupported Story membership schema_version " + version + ".");
        exact(
            root,
            version == CanonicalStoryMembership.CURRENT_SCHEMA_VERSION ? CURRENT_ROOT : LEGACY_ROOT,
            "story.membership.root");
        String storyId = string(root, "story_id", "story.membership.root");
        CanonicalStoryMembershipSet owned = membershipSet(
            required(root, "owned_resources", "story.membership.root"),
            "owned_resources",
            version);
        CanonicalStoryMembershipSet referenced = membershipSet(
            required(root, "referenced_resources", "story.membership.root"),
            "referenced_resources",
            version);
        if (version == CanonicalStoryMembership.CURRENT_SCHEMA_VERSION)
            displayOrder(required(root, "display_order", "story.membership.root"));
        CanonicalStoryMembership result = new CanonicalStoryMembership(version, storyId, owned, referenced);
        validate(result);
        String expectedFile = storyId + ".json";
        if (!fileName.equals(expectedFile)) throw CanonicalStoryMembershipException.failure(
            "story.membership.filename.mismatch",
            "Canonical Story membership filename must equal story_id + '.json': " + fileName);
        return result;
    }

    private static CanonicalStoryMembershipSet membershipSet(JsonElement element, String path, int version)
        throws CanonicalStoryMembershipException {
        if (!element.isJsonObject()) throw CanonicalStoryMembershipException
            .failure("story.membership.set.invalid", "'" + path + "' must be an object.");
        JsonObject object = element.getAsJsonObject();
        boolean current = version >= CanonicalStoryMembership.ITEM_MEMBERSHIP_SCHEMA_VERSION;
        exact(object, current ? CURRENT_MEMBERSHIP : LEGACY_MEMBERSHIP, "story.membership.set");
        return new CanonicalStoryMembershipSet(
            ids(required(object, "actors", "story.membership.set"), path + ".actors"),
            current ? ids(required(object, "items", "story.membership.set"), path + ".items")
                : Collections.<String>emptyList(),
            current ? ids(required(object, "item_groups", "story.membership.set"), path + ".item_groups")
                : Collections.<String>emptyList(),
            ids(required(object, "sessions", "story.membership.set"), path + ".sessions"),
            ids(required(object, "tasks", "story.membership.set"), path + ".tasks"));
    }

    private static void displayOrder(JsonElement element) throws CanonicalStoryMembershipException {
        if (!element.isJsonObject()) throw CanonicalStoryMembershipException
            .failure("story.membership.display_order.invalid", "'display_order' must be an object.");
        JsonObject object = element.getAsJsonObject();
        exact(object, DISPLAY_ORDER, "story.membership.display_order");
        orderHandles(required(object, "actors", "story.membership.display_order"), "actor", false);
        orderHandles(required(object, "items", "story.membership.display_order"), "item", true);
        orderHandles(required(object, "sessions", "story.membership.display_order"), "session", false);
        orderHandles(required(object, "tasks", "story.membership.display_order"), "task", false);
    }

    private static void orderHandles(JsonElement element, String kind, boolean itemHandle)
        throws CanonicalStoryMembershipException {
        String path = "display_order." + kind + "s";
        List<String> handles = ids(element, path);
        Set<String> seen = new HashSet<String>();
        for (String handle : handles) {
            boolean valid = itemHandle ? validItemOrderHandle(handle)
                : ID_PATTERN.matcher(handle)
                    .matches();
            if (!valid) throw CanonicalStoryMembershipException.failure(
                "story.membership." + kind + ".order.invalid",
                "Display-order handle '" + handle + "' is invalid.");
            if (!seen.add(handle)) throw CanonicalStoryMembershipException.failure(
                "story.membership." + kind + ".order.duplicate",
                "'" + path + "' contains duplicate handle '" + handle + "'.");
        }
    }

    private static boolean validItemOrderHandle(String handle) {
        int separator = handle == null ? -1 : handle.indexOf(':');
        if (separator <= 0 || separator == handle.length() - 1 || separator != handle.lastIndexOf(':')) return false;
        String prefix = handle.substring(0, separator);
        return ("item".equals(prefix) || "item_group".equals(prefix))
            && ID_PATTERN.matcher(handle.substring(separator + 1))
                .matches();
    }

    private static List<String> ids(JsonElement element, String path) throws CanonicalStoryMembershipException {
        if (!element.isJsonArray()) throw CanonicalStoryMembershipException
            .failure("story.membership.list.invalid", "'" + path + "' must be an array.");
        List<String> result = new ArrayList<String>();
        for (JsonElement value : element.getAsJsonArray()) {
            if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
                .isString())
                throw CanonicalStoryMembershipException
                    .failure("story.membership.id.type", "'" + path + "' entries must be strings.");
            result.add(value.getAsString());
        }
        return result;
    }

    private static void validate(CanonicalStoryMembership membership) throws CanonicalStoryMembershipException {
        if (membership.getSchemaVersion() != CanonicalStoryMembership.LEGACY_SCHEMA_VERSION
            && membership.getSchemaVersion() != CanonicalStoryMembership.ITEM_MEMBERSHIP_SCHEMA_VERSION
            && membership.getSchemaVersion() != CanonicalStoryMembership.CURRENT_SCHEMA_VERSION)
            throw CanonicalStoryMembershipException.failure(
                "story.membership.schema_version.unsupported",
                "Unsupported Story membership schema_version " + membership.getSchemaVersion() + ".");
        validateId(membership.getStoryId(), "story.membership.story_id.invalid", "Story ID");
        validateList(
            membership.getOwnedResources()
                .getActors(),
            "actor",
            "owned_resources.actors");
        validateList(
            membership.getOwnedResources()
                .getSessions(),
            "session",
            "owned_resources.sessions");
        validateList(
            membership.getOwnedResources()
                .getTasks(),
            "task",
            "owned_resources.tasks");
        validateList(
            membership.getOwnedResources()
                .getItems(),
            "item",
            "owned_resources.items");
        validateList(
            membership.getOwnedResources()
                .getItemGroups(),
            "item_group",
            "owned_resources.item_groups");
        validateList(
            membership.getReferencedResources()
                .getActors(),
            "actor",
            "referenced_resources.actors");
        validateList(
            membership.getReferencedResources()
                .getSessions(),
            "session",
            "referenced_resources.sessions");
        validateList(
            membership.getReferencedResources()
                .getTasks(),
            "task",
            "referenced_resources.tasks");
        validateList(
            membership.getReferencedResources()
                .getItems(),
            "item",
            "referenced_resources.items");
        validateList(
            membership.getReferencedResources()
                .getItemGroups(),
            "item_group",
            "referenced_resources.item_groups");
        overlap(
            membership.getOwnedResources()
                .getActors(),
            membership.getReferencedResources()
                .getActors(),
            "actor");
        overlap(
            membership.getOwnedResources()
                .getSessions(),
            membership.getReferencedResources()
                .getSessions(),
            "session");
        overlap(
            membership.getOwnedResources()
                .getTasks(),
            membership.getReferencedResources()
                .getTasks(),
            "task");
        overlap(
            membership.getOwnedResources()
                .getItems(),
            membership.getReferencedResources()
                .getItems(),
            "item");
        overlap(
            membership.getOwnedResources()
                .getItemGroups(),
            membership.getReferencedResources()
                .getItemGroups(),
            "item_group");
    }

    private static void validateList(List<String> ids, String kind, String path)
        throws CanonicalStoryMembershipException {
        if (ids == null) throw CanonicalStoryMembershipException
            .failure("story.membership." + kind + ".list.required", "'" + path + "' cannot be null.");
        Set<String> seen = new HashSet<String>();
        for (String id : ids) {
            validateId(id, "story.membership." + kind + ".id.invalid", kind + " ID");
            if (!seen.add(id)) throw CanonicalStoryMembershipException.failure(
                "story.membership." + kind + ".id.duplicate",
                "'" + path + "' contains duplicate ID '" + id + "'.");
        }
    }

    private static void overlap(List<String> owned, List<String> referenced, String kind)
        throws CanonicalStoryMembershipException {
        Set<String> ownedSet = new HashSet<String>(owned);
        for (String id : referenced) if (ownedSet.contains(id)) throw CanonicalStoryMembershipException.failure(
            "story.membership." + kind + ".ownership.overlap",
            kind + " ID '" + id + "' cannot be both owned and referenced.");
    }

    private static void validateId(String id, String code, String label) throws CanonicalStoryMembershipException {
        if (id == null || !ID_PATTERN.matcher(id)
            .matches())
            throw CanonicalStoryMembershipException
                .failure(code, label + " '" + id + "' must match [a-z0-9][a-z0-9_-]*.");
    }

    private static JsonElement readTree(JsonReader reader, String context) throws IOException {
        JsonToken token = reader.peek();
        if (token == JsonToken.BEGIN_OBJECT) {
            JsonObject result = new JsonObject();
            reader.beginObject();
            while (reader.hasNext()) {
                String name = reader.nextName();
                if (result.has(name)) throw CanonicalStoryMembershipException
                    .failure(context + ".member.duplicate", "Duplicate JSON member '" + name + "'.");
                result.add(name, readTree(reader, childContext(context, name)));
            }
            reader.endObject();
            return result;
        }
        if (token == JsonToken.BEGIN_ARRAY) {
            JsonArray result = new JsonArray();
            reader.beginArray();
            while (reader.hasNext()) result.add(readTree(reader, context));
            reader.endArray();
            return result;
        }
        if (token == JsonToken.STRING) return new JsonPrimitive(reader.nextString());
        if (token == JsonToken.BOOLEAN) return new JsonPrimitive(reader.nextBoolean());
        if (token == JsonToken.NULL) {
            reader.nextNull();
            return JsonNull.INSTANCE;
        }
        if (token == JsonToken.NUMBER) return new JsonParser().parse(reader.nextString());
        throw CanonicalStoryMembershipException
            .failure("story.membership.invalid", "Unexpected JSON token '" + token + "'.");
    }

    private static void exact(JsonObject object, Set<String> expected, String prefix)
        throws CanonicalStoryMembershipException {
        for (Map.Entry<String, JsonElement> entry : object.entrySet())
            if (!expected.contains(entry.getKey())) throw CanonicalStoryMembershipException
                .failure(prefix + ".member.unsupported", "Unsupported field '" + entry.getKey() + "'.");
        for (String name : expected) if (!object.has(name)) throw CanonicalStoryMembershipException
            .failure(prefix + ".member.required", "Required field '" + name + "' is missing.");
    }

    private static String childContext(String context, String name) {
        if ("story.membership.root".equals(context)
            && ("owned_resources".equals(name) || "referenced_resources".equals(name))) return "story.membership.set";
        return context;
    }

    private static JsonElement required(JsonObject object, String name, String prefix)
        throws CanonicalStoryMembershipException {
        if (!object.has(name)) throw CanonicalStoryMembershipException
            .failure(prefix + ".member.required", "Required field '" + name + "' is missing.");
        return object.get(name);
    }

    private static String string(JsonObject object, String name, String prefix)
        throws CanonicalStoryMembershipException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString())
            throw CanonicalStoryMembershipException
                .failure(prefix + ".member.type", "Field '" + name + "' must be a string.");
        return value.getAsString();
    }

    private static int integer(JsonObject object, String name, String prefix) throws CanonicalStoryMembershipException {
        JsonElement value = required(object, name, prefix);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isNumber())
            throw CanonicalStoryMembershipException
                .failure(prefix + ".member.type", "Field '" + name + "' must be an integer.");
        try {
            return new BigDecimal(value.getAsString()).intValueExact();
        } catch (ArithmeticException | NumberFormatException exception) {
            throw new CanonicalStoryMembershipException(
                prefix + ".member.type",
                "Field '" + name + "' must be an integer.",
                exception);
        }
    }

    private static Set<String> set(String... names) {
        Set<String> result = new HashSet<String>();
        Collections.addAll(result, names);
        return Collections.unmodifiableSet(result);
    }
}
