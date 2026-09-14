package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;
import darkgrey.rpg.project.ProjectLoadException;

/** Validates media independently of Actor identity and canonical execution state. */
final class StoryPackageMediaValidation {

    private StoryPackageMediaValidation() {}

    static void validate(StoryPackageManifest manifest, Map<String, byte[]> bytes) throws ProjectLoadException {
        Set<String> reachable = new HashSet<String>();
        for (String path : manifest.getRequiredResources()
            .getActors()) {
            JsonObject actor = object(bytes, path);
            add(reachable, actor.get("default_portrait_ref"));
            JsonElement variants = actor.get("portrait_variants");
            if (variants != null) for (JsonElement variant : variants.getAsJsonArray()) add(
                reachable,
                variant.getAsJsonObject()
                    .get("media_ref"));
        }
        for (String path : manifest.getRequiredResources()
            .getSessions()) {
            JsonObject session = object(bytes, path);
            JsonElement graph = session.get("graph");
            if (graph == null || graph.isJsonNull()) continue;
            for (JsonElement value : graph.getAsJsonObject()
                .getAsJsonArray("nodes")) {
                JsonObject node = value.getAsJsonObject();
                String type = node.get("type")
                    .getAsString();
                JsonObject properties = node.getAsJsonObject("properties");
                if (properties == null) continue;
                if ("line".equals(type)) add(reachable, properties.get("voice_ref"));
                if ("music".equals(type)) add(reachable, properties.get("media_ref"));
                if ("screen".equals(type) && properties.has("layers"))
                    for (JsonElement layer : properties.getAsJsonArray("layers")) add(
                        reachable,
                        layer.getAsJsonObject()
                            .get("media_ref"));
            }
        }
        if (!reachable.equals(
            new HashSet<String>(
                manifest.getRequiredResources()
                    .getMedia())))
            throw new ProjectLoadException("Declared media must match reachable resource references.");
        for (String ref : reachable) {
            byte[] content = bytes.get(ref);
            if (content == null || content.length == 0 || content.length > 64L * 1024L * 1024L)
                throw new ProjectLoadException("Missing or oversized media: " + ref);
            String hash;
            try {
                StringBuilder result = new StringBuilder();
                for (byte value : MessageDigest.getInstance("SHA-256")
                    .digest(content)) {
                    result.append(Character.forDigit((value & 255) >> 4, 16));
                    result.append(Character.forDigit(value & 15, 16));
                }
                hash = result.toString();
            } catch (NoSuchAlgorithmException exception) {
                throw new IllegalStateException(exception);
            }
            if (!ref.substring(6, 70)
                .equals(hash)) throw new ProjectLoadException("Media fingerprint mismatch: " + ref);
            boolean valid = ref.endsWith(".ogg") ? starts(content, new int[] { 79, 103, 103, 83 })
                : ref.endsWith(".png") ? starts(content, new int[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                    : starts(content, new int[] { 255, 216, 255 });
            if (!valid) throw new ProjectLoadException("Media format mismatch: " + ref);
            try {
                darkgrey.rpg.media.MediaPayloadValidation.validate(ref, content);
            } catch (IllegalArgumentException exception) {
                throw new ProjectLoadException("Corrupt media payload: " + ref, exception);
            }
        }
    }

    private static boolean starts(byte[] content, int[] signature) {
        if (content.length < signature.length) return false;
        for (int i = 0; i < signature.length; i++) if ((content[i] & 255) != signature[i]) return false;
        return true;
    }

    private static JsonObject object(Map<String, byte[]> bytes, String path) throws ProjectLoadException {
        byte[] content = bytes.get(path);
        if (content == null) throw new ProjectLoadException("Missing declared resource: " + path);
        return new JsonParser().parse(new String(content, StandardCharsets.UTF_8))
            .getAsJsonObject();
    }

    private static void add(Set<String> result, JsonElement value) throws ProjectLoadException {
        if (value == null || value.isJsonNull()) return;
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString() || !CanonicalMediaReference.isValid(value.getAsString()))
            throw new ProjectLoadException("Invalid project media reference.");
        result.add(value.getAsString());
    }
}
