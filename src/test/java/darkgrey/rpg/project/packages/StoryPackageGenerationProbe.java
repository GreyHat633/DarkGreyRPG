package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

/** Pure executable proof for B3 package fingerprint, generation delta, and registry persistence. */
public final class StoryPackageGenerationProbe {

    private StoryPackageGenerationProbe() {}

    public static void main(String[] args) throws Exception {
        fingerprintContract();
        generationDelta();
        registryPersistence();
        System.out.println("FINGERPRINT_SAME_CONTENT_STABLE=PASS");
        System.out.println("FINGERPRINT_RESOURCE_CHANGE_DETECTED=PASS");
        System.out.println("FINGERPRINT_ROLE_CHANGE_DETECTED=PASS");
        System.out.println("FINGERPRINT_ENTRY_ORDER_INDEPENDENT=PASS");
        System.out.println("FINGERPRINT_REPEAT_POLICY_CHANGE_DETECTED=PASS");
        System.out.println("PACKAGE_GENERATION_DELTA_CLASSIFICATION=PASS");
        System.out.println("PACKAGE_GENERATION_REGISTRY_RESTART=PASS");
    }

    private static void fingerprintContract() throws Exception {
        StoryPackageManifest manifest = manifest(
            "pkg",
            "story",
            "producer-a",
            "package-a",
            Arrays.asList("actors/b.json", "actors/a.json"),
            Collections.singletonList("items/item.json"));
        Map<String, byte[]> bytes = bytes("once");
        String first = StoryPackageContentFingerprint.compute(manifest, bytes);
        Map<String, byte[]> reordered = new LinkedHashMap<String, byte[]>();
        java.util.List<String> keys = new java.util.ArrayList<String>(bytes.keySet());
        Collections.reverse(keys);
        for (String key : keys) reordered.put(key, bytes.get(key));
        require(
            first.equals(StoryPackageContentFingerprint.compute(manifest, reordered)),
            "map entry order changed fingerprint");

        StoryPackageManifest reorderedManifest = manifest(
            "pkg",
            "story",
            "producer-b",
            "package-b",
            Arrays.asList("actors/a.json", "actors/b.json"),
            Collections.singletonList("items/item.json"));
        require(
            first.equals(StoryPackageContentFingerprint.compute(reorderedManifest, bytes)),
            "manifest order or excluded version changed fingerprint");

        StoryPackageManifest otherIdentity = manifest(
            "other_pkg",
            "other_story",
            "producer-a",
            "package-a",
            Arrays.asList("actors/b.json", "actors/a.json"),
            Collections.singletonList("items/item.json"));
        require(
            first.equals(StoryPackageContentFingerprint.compute(otherIdentity, bytes)),
            "InstallIdentity leaked into content fingerprint");

        Map<String, byte[]> changed = bytes("once");
        changed.put("tasks/task.json", utf8("{\"changed\":true}"));
        require(!first.equals(StoryPackageContentFingerprint.compute(manifest, changed)), "resource change missed");

        Map<String, byte[]> repeatable = bytes("repeatable");
        require(
            !first.equals(StoryPackageContentFingerprint.compute(manifest, repeatable)),
            "repeat policy change missed");

        StoryPackageManifest roleChanged = manifest(
            "pkg",
            "story",
            "producer-a",
            "package-a",
            Collections.singletonList("items/item.json"),
            Arrays.asList("actors/a.json", "actors/b.json"));
        require(!first.equals(StoryPackageContentFingerprint.compute(roleChanged, bytes)), "record role change missed");
        require(first.matches("[0-9a-f]{64}"), "fingerprint format");
    }

    private static void generationDelta() {
        PackageGenerationKey oldA = key("a", "story_a", 'a');
        PackageGenerationKey oldB = key("b", "story_b", 'b');
        PackageGenerationKey oldC = key("c", "story_c", 'c');
        PackageGenerationKey oldD = key("d", "story_d", 'd');
        Map<String, PackageGenerationKey> previous = map(oldA, oldB, oldC, oldD);
        PackageGenerationKey sameA = key("a", "story_a", 'a');
        PackageGenerationKey updatedB = key("b", "story_b", 'e');
        PackageGenerationKey replacedC = key("c", "story_replaced", 'f');
        PackageGenerationKey addedE = key("e", "story_e", '1');
        List<StoryPackageGenerationDelta.Entry> deltas = StoryPackageGenerationDelta
            .betweenKeys(previous, map(sameA, updatedB, replacedC, addedE));
        require(deltas.size() == 5, "delta count");
        require(
            deltas.get(0)
                .getKind() == StoryPackageGenerationDelta.Kind.UNCHANGED,
            "unchanged delta");
        require(
            deltas.get(1)
                .getKind() == StoryPackageGenerationDelta.Kind.UPDATED,
            "updated delta");
        require(
            deltas.get(2)
                .getKind() == StoryPackageGenerationDelta.Kind.REPLACED,
            "replaced delta");
        require(
            deltas.get(3)
                .getKind() == StoryPackageGenerationDelta.Kind.REMOVED,
            "removed delta");
        require(
            deltas.get(4)
                .getKind() == StoryPackageGenerationDelta.Kind.ADDED,
            "added delta");
        require(
            !deltas.get(0)
                .retiresRuntime() && deltas.get(1)
                    .retiresRuntime()
                && deltas.get(2)
                    .retiresRuntime()
                && deltas.get(3)
                    .retiresRuntime()
                && !deltas.get(4)
                    .retiresRuntime(),
            "retirement classification");
    }

    private static void registryPersistence() {
        MapStorage storage = new MapStorage(null);
        StoryPackageGenerationSavedData data = StoryPackageGenerationSavedData.get(storage);
        require(!data.isInitialized(), "fresh registry initialized");
        Map<String, PackageGenerationKey> values = map(key("b", "story_b", 'b'), key("a", "story_a", 'a'));
        data.replace(values);
        require(data.isInitialized() && data.isDirty(), "registry replace");
        NBTTagCompound checkpoint = new NBTTagCompound();
        data.writeToNBT(checkpoint);
        StoryPackageGenerationSavedData restarted = new StoryPackageGenerationSavedData();
        restarted.readFromNBT(checkpoint);
        require(
            restarted.isInitialized() && restarted.snapshot()
                .equals(values),
            "registry restart");
        NBTTagCompound roundTrip = new NBTTagCompound();
        restarted.writeToNBT(roundTrip);
        require(checkpoint.equals(roundTrip), "registry deterministic NBT");
    }

    private static StoryPackageManifest manifest(String packageId, String storyId, String producerVersion,
        String packageVersion, List<String> actors, List<String> items) throws Exception {
        String json = "{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\","
            + "\"producer_version\":\""
            + producerVersion
            + "\",\"schema_version\":1,\"package_id\":\""
            + packageId
            + "\",\"package_version\":\""
            + packageVersion
            + "\",\"story_id\":\""
            + storyId
            + "\",\"story_schema_version\":3,\"required_resources\":{\"story\":\"stories/story.json\","
            + "\"actors\":"
            + array(actors)
            + ",\"items\":"
            + array(items)
            + ",\"item_groups\":[],\"dialogues\":[],\"quests\":[],\"canonical_stories\":[],"
            + "\"canonical_memberships\":[],\"sessions\":[],\"tasks\":[\"tasks/task.json\"]}}";
        return StoryPackageManifest.read(utf8(json), "probe!/manifest.json");
    }

    private static Map<String, byte[]> bytes(String repeatPolicy) {
        Map<String, byte[]> result = new LinkedHashMap<String, byte[]>();
        result.put("project.json", utf8("{\"schema_version\":3}"));
        result.put("stories/story.json", utf8("{\"repeat_policy\":\"" + repeatPolicy + "\"}"));
        result.put("actors/a.json", utf8("{\"id\":\"a\"}"));
        result.put("actors/b.json", utf8("{\"id\":\"b\"}"));
        result.put("items/item.json", utf8("{\"id\":\"item\"}"));
        result.put("tasks/task.json", utf8("{\"id\":\"task\"}"));
        return result;
    }

    private static String array(List<String> values) {
        StringBuilder result = new StringBuilder("[");
        for (String value : values) {
            if (result.length() > 1) result.append(',');
            result.append('\"')
                .append(value)
                .append('\"');
        }
        return result.append(']')
            .toString();
    }

    private static Map<String, PackageGenerationKey> map(PackageGenerationKey... values) {
        Map<String, PackageGenerationKey> result = new LinkedHashMap<String, PackageGenerationKey>();
        for (PackageGenerationKey value : values) result.put(value.getPackageId(), value);
        return result;
    }

    private static PackageGenerationKey key(String packageId, String storyId, char fingerprint) {
        char[] value = new char[64];
        Arrays.fill(value, fingerprint);
        return new PackageGenerationKey(packageId, storyId, new String(value));
    }

    private static byte[] utf8(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new IllegalStateException(message);
    }
}
