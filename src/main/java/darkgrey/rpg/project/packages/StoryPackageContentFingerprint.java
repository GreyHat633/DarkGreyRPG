package darkgrey.rpg.project.packages;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.project.ProjectLoadException;

/** Deterministic DGR Package Content Fingerprint v1 over validated authoritative bytes. */
public final class StoryPackageContentFingerprint {

    private static final byte[] HEADER = "DGR-PACKAGE-CONTENT-FINGERPRINT-V1\0".getBytes(StandardCharsets.US_ASCII);

    private StoryPackageContentFingerprint() {}

    public static String compute(StoryPackageManifest manifest, Map<String, byte[]> resourceBytes)
        throws ProjectLoadException {
        if (manifest == null || resourceBytes == null)
            throw new IllegalArgumentException("Story Package fingerprint inputs are required.");
        MessageDigest digest = sha256();
        digest.update(HEADER);
        updateText(digest, nullable(manifest.getFormat()));
        updateText(
            digest,
            manifest.getFormatVersion() == null ? ""
                : manifest.getFormatVersion()
                    .toString());
        updateText(digest, Integer.toString(manifest.getSchemaVersion()));
        updateText(digest, Integer.toString(manifest.getStorySchemaVersion()));

        List<Record> records = records(manifest, resourceBytes);
        Collections.sort(records, new Comparator<Record>() {

            @Override
            public int compare(Record left, Record right) {
                int result = left.role.compareTo(right.role);
                return result == 0 ? left.path.compareTo(right.path) : result;
            }
        });
        for (Record record : records) {
            updateBytes(digest, record.role.getBytes(StandardCharsets.UTF_8));
            updateBytes(digest, record.path.getBytes(StandardCharsets.UTF_8));
            digest.update(
                ByteBuffer.allocate(8)
                    .putLong(record.content.length)
                    .array());
            digest.update(record.content);
        }
        return hex(digest.digest());
    }

    private static List<Record> records(StoryPackageManifest manifest, Map<String, byte[]> bytes)
        throws ProjectLoadException {
        List<Record> result = new ArrayList<Record>();
        add(result, "project", "project.json", bytes);
        StoryPackageManifest.RequiredResources required = manifest.getRequiredResources();
        add(result, "story", required.getStory(), bytes);
        addAll(result, "actor", required.getActors(), bytes);
        addAll(result, "item", required.getItems(), bytes);
        addAll(result, "item_group", required.getItemGroups(), bytes);
        addAll(result, "dialogue", required.getDialogues(), bytes);
        addAll(result, "quest", required.getQuests(), bytes);
        addAll(result, "canonical_story", required.getCanonicalStories(), bytes);
        addAll(result, "canonical_membership", required.getCanonicalMemberships(), bytes);
        addAll(result, "session", required.getSessions(), bytes);
        addAll(result, "task", required.getTasks(), bytes);
        addAll(result, "media", required.getMedia(), bytes);
        if (required.getStoryLogicGraph() != null)
            add(result, "story_logic_graph", required.getStoryLogicGraph(), bytes);
        return result;
    }

    private static void addAll(List<Record> target, String role, List<String> paths, Map<String, byte[]> bytes)
        throws ProjectLoadException {
        for (String path : paths) add(target, role, path, bytes);
    }

    private static void add(List<Record> target, String role, String path, Map<String, byte[]> bytes)
        throws ProjectLoadException {
        byte[] content = bytes.get(path);
        if (content == null) throw new ProjectLoadException(
            "Missing authoritative bytes for Story Package fingerprint entry '" + role + "' at '" + path + "'");
        target.add(new Record(role, path, content));
    }

    private static void updateText(MessageDigest digest, String value) {
        updateBytes(digest, value.getBytes(StandardCharsets.UTF_8));
    }

    private static void updateBytes(MessageDigest digest, byte[] value) {
        digest.update(
            ByteBuffer.allocate(4)
                .putInt(value.length)
                .array());
        digest.update(value);
    }

    private static MessageDigest sha256() {
        try {
            return MessageDigest.getInstance("SHA-256");
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException("SHA-256 is unavailable.", exception);
        }
    }

    private static String nullable(String value) {
        return value == null ? "" : value;
    }

    private static String hex(byte[] bytes) {
        StringBuilder result = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) result.append(String.format(java.util.Locale.ROOT, "%02x", value & 0xff));
        return result.toString();
    }

    private static final class Record {

        private final String role;
        private final String path;
        private final byte[] content;

        Record(String role, String path, byte[] content) {
            this.role = role;
            this.path = path;
            this.content = content;
        }
    }
}
