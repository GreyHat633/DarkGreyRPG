package darkgrey.rpg.project.packages;

import java.io.StringReader;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader;
import darkgrey.rpg.identity.ResourceAddressJson;
import darkgrey.rpg.project.ProjectLoadException;

/** Validates every member before publishing a flat Group candidate. No Group runtime identity. */
public final class StoryGroupPackageReader {

    public static final String CONNECTIONS = "resources/group-connections.json";

    private StoryGroupPackageReader() {}

    public static Result read(DgrsArchiveReader archive) throws ProjectLoadException {
        try {
            JsonObject root = StrictPackageJson.read(new StringReader(archive.readUtf8("manifest.json")))
                .getAsJsonObject();
            Set<String> fields = new HashSet<String>(
                Arrays.asList("format", "format_version", "identity_format", "display_name", "connections", "members"));
            for (Map.Entry<String, JsonElement> field : root.entrySet())
                if (!fields.remove(field.getKey())) throw new ProjectLoadException("Unknown Group manifest field");
            if (!fields.isEmpty() || !"dgrs.g".equals(text(root, "format"))
                || !"story-uid-v1".equals(text(root, "identity_format"))
                || !CONNECTIONS.equals(text(root, "connections"))
                || !root.get("format_version")
                    .isJsonPrimitive()
                || !root.get("format_version")
                    .getAsJsonPrimitive()
                    .isNumber()
                || root.get("format_version")
                    .getAsBigDecimal()
                    .intValueExact() != 2)
                throw new ProjectLoadException("Unsupported Group identity/version");
            String displayName = text(root, "display_name");
            if (displayName.trim()
                .isEmpty() || displayName.length() > 128) throw new ProjectLoadException("Invalid Group display name");
            JsonArray members = root.getAsJsonArray("members");
            if (members.size() < 2 || members.size() > 4096)
                throw new ProjectLoadException("Group requires 2..4096 members");
            Map<String, StoryPackageManifest> manifests = new LinkedHashMap<String, StoryPackageManifest>();
            Map<String, StoryPackageSnapshotReader.Result> snapshots = new LinkedHashMap<String, StoryPackageSnapshotReader.Result>();
            Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
            Map<String, byte[]> definitions = new LinkedHashMap<String, byte[]>();
            Set<String> declared = new HashSet<String>(Arrays.asList("manifest.json", "project.json", CONNECTIONS));
            for (JsonElement member : members) {
                StoryPackageManifest manifest = StoryPackageManifest.read(
                    member.toString()
                        .getBytes(StandardCharsets.UTF_8),
                    archive.getSourceIdentity());
                if (manifests.put(manifest.getStoryId(), manifest) != null)
                    throw new ProjectLoadException("Duplicate Story UID in Group");
                if (manifest.getRequiredResources()
                    .getStoryLogicGraph() != null)
                    throw new ProjectLoadException("Member connections must use the Group graph");
                StoryPackageSnapshotReader.Result snapshot = StoryPackageSnapshotReader.read(archive, manifest, false);
                snapshots.put(manifest.getStoryId(), snapshot);
                stories.putAll(
                    snapshot.getSnapshot()
                        .getCanonicalStories());
                declared.addAll(
                    snapshot.getDeclaredBytes()
                        .keySet());
                declared.addAll(
                    manifest.getRequiredResources()
                        .getMedia());
                for (Map.Entry<String, byte[]> definition : snapshot.getDeclaredBytes()
                    .entrySet()) {
                    if (definition.getKey()
                        .equals("project.json")) continue;
                    JsonObject json = StrictPackageJson
                        .read(new StringReader(new String(definition.getValue(), StandardCharsets.UTF_8)))
                        .getAsJsonObject();
                    JsonElement id = null;
                    for (String field : Arrays.asList("npc_id", "group_id", "item_id", "id")) if (json.has(field)) {
                        id = json.get(field);
                        break;
                    }
                    if (id == null || !id.isJsonObject()) continue; // Story and membership UIDs were checked above.
                    String key = ResourceAddressJson.parse(id.toString())
                        .toKey();
                    byte[] prior = definitions.put(key, definition.getValue());
                    if (prior != null && !Arrays.equals(prior, definition.getValue()))
                        throw new ProjectLoadException("Group shared resource bytes differ: " + key);
                }
            }
            if (!declared.equals(new HashSet<String>(archive.getEntryNames())))
                throw new ProjectLoadException("Group has undeclared or missing entries");
            CanonicalStoryLogicGraphLoader loader = new CanonicalStoryLogicGraphLoader();
            CanonicalStoryLogicGraph graph = loader
                .loadUnresolved(archive.readBytes(CONNECTIONS), archive.getSourceIdentity());
            loader.validate(graph, stories);
            Set<String> reachable = new HashSet<String>();
            reachable.add(
                manifests.keySet()
                    .iterator()
                    .next());
            boolean changed;
            do {
                changed = false;
                for (CanonicalStoryLogicConnection edge : graph.getConnections()) {
                    if (reachable.contains(edge.getSourceStoryId())) changed |= reachable.add(edge.getTargetStoryId());
                    if (reachable.contains(edge.getTargetStoryId())) changed |= reachable.add(edge.getSourceStoryId());
                }
            } while (changed);
            if (!reachable.equals(manifests.keySet()))
                throw new ProjectLoadException("Group members are not one connected component");
            return new Result(displayName, manifests, snapshots, graph);
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new ProjectLoadException("Invalid complete Story Group container", exception);
        }
    }

    static Map<String, StoryPackageManifest> readManifests(DgrsArchiveReader archive) throws ProjectLoadException {
        return readManifests(archive.readBytes("manifest.json"), archive.getSourceIdentity());
    }

    static Map<String, StoryPackageManifest> readManifests(byte[] bytes, String source) throws ProjectLoadException {
        try {
            String json = StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(java.nio.charset.CodingErrorAction.REPORT)
                .onUnmappableCharacter(java.nio.charset.CodingErrorAction.REPORT)
                .decode(java.nio.ByteBuffer.wrap(bytes))
                .toString();
            JsonObject root = StrictPackageJson.read(new StringReader(json))
                .getAsJsonObject();
            Set<String> fields = new HashSet<String>(
                Arrays.asList("format", "format_version", "identity_format", "display_name", "connections", "members"));
            for (Map.Entry<String, JsonElement> field : root.entrySet())
                if (!fields.remove(field.getKey())) throw new ProjectLoadException("Unknown Group manifest field");
            if (!fields.isEmpty() || !"dgrs.g".equals(text(root, "format"))
                || !"story-uid-v1".equals(text(root, "identity_format"))
                || !CONNECTIONS.equals(text(root, "connections"))
                || !root.get("format_version")
                    .isJsonPrimitive()
                || !root.get("format_version")
                    .getAsJsonPrimitive()
                    .isNumber()
                || root.get("format_version")
                    .getAsBigDecimal()
                    .intValueExact() != 2)
                throw new ProjectLoadException("Unsupported Group identity/version");
            String displayName = text(root, "display_name");
            if (displayName.trim()
                .isEmpty() || displayName.length() > 128) throw new ProjectLoadException("Invalid Group display name");
            JsonArray members = root.getAsJsonArray("members");
            if (members.size() < 2 || members.size() > 4096)
                throw new ProjectLoadException("Group requires 2..4096 members");
            Map<String, StoryPackageManifest> manifests = new LinkedHashMap<String, StoryPackageManifest>();
            for (JsonElement member : members) {
                StoryPackageManifest manifest = StoryPackageManifest.read(
                    member.toString()
                        .getBytes(StandardCharsets.UTF_8),
                    source);
                if (manifests.put(manifest.getStoryId(), manifest) != null)
                    throw new ProjectLoadException("Duplicate Story UID in Group");
                if (manifest.getRequiredResources()
                    .getStoryLogicGraph() != null)
                    throw new ProjectLoadException("Member connections must use the Group graph");
            }
            return Collections.unmodifiableMap(manifests);
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (java.nio.charset.CharacterCodingException | RuntimeException exception) {
            throw new ProjectLoadException("Invalid Group member manifest", exception);
        }
    }

    private static String text(JsonObject object, String name) throws ProjectLoadException {
        JsonElement value = object.get(name);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw new ProjectLoadException("Group string required: " + name);
        return value.getAsString();
    }

    public static final class Result {

        private final String displayName;
        private final Map<String, StoryPackageManifest> manifests;
        private final Map<String, StoryPackageSnapshotReader.Result> snapshots;
        private final CanonicalStoryLogicGraph connections;

        Result(String displayName, Map<String, StoryPackageManifest> manifests,
            Map<String, StoryPackageSnapshotReader.Result> snapshots, CanonicalStoryLogicGraph connections) {
            this.displayName = displayName;
            this.manifests = Collections.unmodifiableMap(manifests);
            this.snapshots = Collections.unmodifiableMap(snapshots);
            this.connections = connections;
        }

        public String getDisplayName() {
            return displayName;
        }

        public Map<String, StoryPackageManifest> getManifests() {
            return manifests;
        }

        Map<String, StoryPackageSnapshotReader.Result> getSnapshots() {
            return snapshots;
        }

        public CanonicalStoryLogicGraph getConnections() {
            return connections;
        }
    }
}
