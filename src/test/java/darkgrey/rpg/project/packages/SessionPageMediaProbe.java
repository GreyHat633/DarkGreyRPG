package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

import darkgrey.rpg.project.ProjectLoadException;

/** All pages contribute to the same package-owned media set used by both archive readers. */
public final class SessionPageMediaProbe {

    private SessionPageMediaProbe() {}

    public static void main(String[] args) throws Exception {
        run();
    }

    static void run() throws Exception {
        String first = ref('a', ".ogg");
        String later = ref('b', ".ogg");
        String portrait = ref('c', ".png");
        String variant = ref('d', ".png");
        String obsolete = ref('e', ".ogg");
        StoryPackageManifest manifest = StoryPackageManifest.read(
            bytes(
                "{\"schema_version\":1,\"package_id\":\"probe\",\"package_version\":\"1\","
                    + "\"story_id\":\"story\",\"story_schema_version\":1,\"required_resources\":{"
                    + "\"story\":\"stories/story.json\",\"actors\":[\"actors/npc.json\"],"
                    + "\"sessions\":[\"sessions/test.json\"],\"media\":[]}}"),
            "page-probe");
        Map<String, byte[]> resources = new HashMap<String, byte[]>();
        resources.put(
            "actors/npc.json",
            bytes(
                "{\"default_portrait_ref\":\"" + portrait
                    + "\",\"portrait_variants\":[{\"name\":\"later\",\"media_ref\":\""
                    + variant
                    + "\"}]}"));
        resources.put(
            "sessions/test.json",
            session(
                "{\"voice_ref\":\"" + obsolete
                    + "\",\"pages\":["
                    + "{\"page_id\":\"1\",\"text\":\"first\",\"voice_ref\":\""
                    + first
                    + "\"},"
                    + "{\"page_id\":\"2\",\"text\":\"silent\",\"voice_ref\":null},"
                    + "{\"page_id\":\"3\",\"text\":\"later\",\"portrait_variant\":\"later\",\"voice_ref\":\""
                    + later
                    + "\"},"
                    + "{\"page_id\":\"4\",\"text\":\"shared\",\"voice_ref\":\""
                    + first
                    + "\"}]}"));
        Set<String> expected = new HashSet<String>(Arrays.asList(first, later, portrait, variant));
        require(
            StoryPackageMediaValidation.reachable(manifest, resources)
                .equals(expected),
            "Later page media missing, shared references duplicated, or stale legacy voice retained");
        try {
            StoryPackageMediaValidation.validate(manifest, resources);
            throw new AssertionError("Missing media declarations accepted");
        } catch (ProjectLoadException expectedFailure) {
            require(
                expectedFailure.getMessage()
                    .contains("Declared media"),
                "Wrong declaration failure");
        }
        resources.put("sessions/test.json", session("{\"text\":\"legacy\",\"voice_ref\":\"" + first + "\"}"));
        require(
            StoryPackageMediaValidation.reachable(manifest, resources)
                .equals(new HashSet<String>(Arrays.asList(first, portrait, variant))),
            "Legacy voice no longer reachable");
        for (String invalid : new String[] { "null", "{}", "[]", "[null]", "[1]", "[{\"voice_ref\":\"bad\"}]" }) {
            resources.put("sessions/test.json", session("{\"pages\":" + invalid + "}"));
            try {
                StoryPackageMediaValidation.reachable(manifest, resources);
                throw new AssertionError("Malformed pages accepted: " + invalid);
            } catch (ProjectLoadException expectedFailure) {}
        }
        System.out.println("SESSION_PAGE_MEDIA=PASS");
    }

    private static byte[] session(String properties) {
        return bytes("{\"graph\":{\"nodes\":[{\"type\":\"line\",\"properties\":" + properties + "}]}}");
    }

    private static byte[] bytes(String text) {
        return text.getBytes(StandardCharsets.UTF_8);
    }

    private static String ref(char digit, String suffix) {
        char[] hash = new char[64];
        Arrays.fill(hash, digit);
        return "media/" + new String(hash) + suffix;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
