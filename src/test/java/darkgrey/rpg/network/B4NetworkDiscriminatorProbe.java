package darkgrey.rpg.network;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Verifies current channel registrations, reserved holes, and per-side uniqueness. */
public final class B4NetworkDiscriminatorProbe {

    private static final Pattern REGISTRATION = Pattern.compile(
        "registerMessage\\s*\\(\\s*[A-Za-z0-9_.]+\\.class\\s*,\\s*"
            + "([A-Za-z0-9_.]+)\\.class\\s*,\\s*([A-Za-z0-9_]+)\\s*,\\s*Side\\.([A-Z]+)\\s*\\)");
    private static final Pattern CONSTANT = Pattern
        .compile("public\\s+static\\s+final\\s+int\\s+([A-Z0-9_]+)\\s*=\\s*(\\d+)");

    private B4NetworkDiscriminatorProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = args.length == 0 ? Paths.get(".") : Paths.get(args[0]);
        Path sourceRoot = root.resolve("src/main/java");
        String dialogue = read(sourceRoot.resolve("darkgrey/rpg/network/DialogueNetwork.java"));
        Map<String, Integer> constants = constants(dialogue);
        Map<String, Registration> registry = new LinkedHashMap<String, Registration>();
        scan(registry, "DialogueNetwork", dialogue, constants);
        scan(
            registry,
            "NominatorNetwork",
            read(sourceRoot.resolve("darkgrey/rpg/network/NominatorNetwork.java")),
            constants);
        scan(
            registry,
            "EntityToolsNetwork",
            read(sourceRoot.resolve("darkgrey/rpg/network/EntityToolsNetwork.java")),
            constants);
        validate(registry);
        System.out.println("B4_NETWORK_DISCRIMINATOR_PROBE=PASS");
    }

    private static void scan(Map<String, Registration> registry, String module, String source,
        Map<String, Integer> constants) {
        Matcher matcher = REGISTRATION.matcher(source);
        int found = 0;
        while (matcher.find()) {
            int id = resolve(matcher.group(2), constants);
            String qualified = matcher.group(1);
            Registration registration = new Registration(
                module,
                qualified.substring(qualified.lastIndexOf('.') + 1),
                matcher.group(3));
            Registration previous = registry.put(id + ":" + registration.side, registration);
            require(previous == null, "discriminator " + id + " is registered by " + previous + " and " + registration);
            found++;
        }
        require(found > 0, "no registrations found in " + module);
    }

    private static void validate(Map<String, Registration> registry) {
        require(registry.size() == 29, "expected 29 current side registrations, found " + registry.size());
        for (int retired : new int[] { 0, 1, 2, 3, 4, 8, 9, 17, 18 }) require(
            !registry.containsKey(retired + ":CLIENT") && !registry.containsKey(retired + ":SERVER"),
            "retired discriminator " + retired + " reused");
        expected(registry, 5, "CanonicalSessionAction", "SERVER");
        expected(registry, 6, "CanonicalSessionFrame", "CLIENT");
        expected(registry, 7, "CanonicalSessionClose", "CLIENT");
        expected(registry, 10, "C2SNominatorEntityOpen", "SERVER");
        expected(registry, 11, "S2CNominatorEntityOpen", "CLIENT");
        expected(registry, 12, "C2SNominatorInventoryOpen", "SERVER");
        expected(registry, 13, "S2CNominatorInventoryOpen", "CLIENT");
        expected(registry, 14, "C2SCopierTemplateAction", "SERVER");
        expected(registry, 15, "CanonicalStoryChooserFrame", "CLIENT");
        expected(registry, 16, "CanonicalStoryChooserSelection", "SERVER");
        expected(registry, 19, "C2SNominatorAction", "SERVER");
        expected(registry, 20, "S2CNominatorActionResult", "CLIENT");
        expected(registry, 21, "CanonicalTaskSubmit", "SERVER");
        expected(registry, 22, "CanonicalMediaRequest", "SERVER");
        expected(registry, 23, "CanonicalMediaChunk", "CLIENT");
        expected(registry, 24, "CanonicalTitleFrame", "CLIENT");
        expected(registry, 25, "CanonicalTitleComplete", "SERVER");
        expected(registry, 26, "CanonicalTaskSubmitChoiceFrame", "CLIENT");
        expected(registry, 27, "CanonicalTaskSubmitChoiceSelection", "SERVER");
        expected(registry, 28, "StoryMediaPlan", "CLIENT");
        expected(registry, 29, "CanonicalChoiceReceipt", "CLIENT");
        expected(registry, 30, "PlayerStatePacket", "SERVER");
        expected(registry, 30, "PlayerStatePacket", "CLIENT");
        expected(registry, 31, "CanonicalSessionNotice", "CLIENT");
        expected(registry, 32, "TaskPresentationPage", "SERVER");
        expected(registry, 32, "TaskPresentationPage", "CLIENT");
        expected(registry, 33, "StoryPackageManagerPacket", "SERVER");
        expected(registry, 33, "StoryPackageManagerPacket", "CLIENT");
        expected(registry, 34, "CanonicalTaskViewOpen", "CLIENT");
    }

    private static void expected(Map<String, Registration> registry, int id, String type, String side) {
        Registration actual = registry.get(id + ":" + side);
        require(actual != null, "missing discriminator " + id);
        require(type.equals(actual.messageType), "discriminator " + id + " maps to " + actual.messageType);
        require(side.equals(actual.side), "discriminator " + id + " has side " + actual.side);
    }

    private static Map<String, Integer> constants(String source) {
        Map<String, Integer> values = new LinkedHashMap<String, Integer>();
        Matcher matcher = CONSTANT.matcher(source);
        while (matcher.find()) values.put(matcher.group(1), Integer.valueOf(matcher.group(2)));
        return values;
    }

    private static int resolve(String token, Map<String, Integer> constants) {
        try {
            return Integer.parseInt(token);
        } catch (NumberFormatException numeric) {
            Integer value = constants.get(token);
            require(value != null, "unknown discriminator token " + token);
            return value.intValue();
        }
    }

    private static String read(Path path) throws IOException {
        return new String(Files.readAllBytes(path), StandardCharsets.UTF_8);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static final class Registration {

        private final String module;
        private final String messageType;
        private final String side;

        private Registration(String module, String messageType, String side) {
            this.module = module;
            this.messageType = messageType;
            this.side = side;
        }

        @Override
        public String toString() {
            return module + "." + messageType + "(" + side + ")";
        }
    }
}
