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

/**
 * Source-level validation of the complete shared FML discriminator registry.
 * The old chooser IDs 8/9 intentionally collide with NominatorNetwork and fail
 * this probe before any Minecraft runtime or LaunchClassLoader is required.
 */
public final class B4NetworkDiscriminatorProbe {

    private static final Pattern REGISTRATION = Pattern.compile(
        "registerMessage\\s*\\(\\s*([A-Za-z0-9_]+)(?:\\.Handler)?\\.class\\s*,\\s*"
            + "([A-Za-z0-9_]+)\\.class\\s*,\\s*([A-Za-z0-9_]+)\\s*,\\s*Side\\.([A-Z]+)\\s*\\)");
    private static final Pattern CONSTANT = Pattern
        .compile("public\\s+static\\s+final\\s+int\\s+([A-Z0-9_]+)\\s*=\\s*(\\d+)");

    private B4NetworkDiscriminatorProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = args.length == 0 ? Paths.get(".") : Paths.get(args[0]);
        Path sourceRoot = root.resolve("src/main/java");
        String dialogue = read(sourceRoot.resolve("darkgrey/rpg/network/DialogueNetwork.java"));
        Map<String, Integer> constants = constants(dialogue);
        Map<Integer, Registration> registry = new LinkedHashMap<Integer, Registration>();
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

    private static void scan(Map<Integer, Registration> registry, String module, String source,
        Map<String, Integer> constants) {
        Matcher matcher = REGISTRATION.matcher(source);
        int found = 0;
        while (matcher.find()) {
            int id = resolve(matcher.group(3), constants);
            Registration registration = new Registration(module, matcher.group(2), matcher.group(4));
            Registration previous = registry.put(id, registration);
            require(previous == null, "discriminator " + id + " is registered by " + previous + " and " + registration);
            found++;
        }
        require(found > 0, "no registrations found in " + module);
    }

    private static void validate(Map<Integer, Registration> registry) {
        require(registry.size() == 14, "expected 14 shared registrations, found " + registry.size());
        expected(registry, 3, "S2CQuestJournal", "CLIENT");
        expected(registry, 4, "C2SQuestJournalRequest", "SERVER");
        expected(registry, 5, "CanonicalSessionAction", "SERVER");
        expected(registry, 6, "CanonicalSessionFrame", "CLIENT");
        expected(registry, 7, "CanonicalSessionClose", "CLIENT");
        expected(registry, 8, "C2SNominatorEntityBind", "SERVER");
        expected(registry, 9, "C2SNominatorInventoryBind", "SERVER");
        expected(registry, 10, "C2SNominatorEntityOpen", "SERVER");
        expected(registry, 11, "S2CNominatorEntityOpen", "CLIENT");
        expected(registry, 12, "C2SNominatorInventoryOpen", "SERVER");
        expected(registry, 13, "S2CNominatorInventoryOpen", "CLIENT");
        expected(registry, 14, "C2SCopierTemplateAction", "SERVER");
        expected(registry, 15, "CanonicalStoryChooserFrame", "CLIENT");
        expected(registry, 16, "CanonicalStoryChooserSelection", "SERVER");
    }

    private static void expected(Map<Integer, Registration> registry, int id, String type, String side) {
        Registration actual = registry.get(id);
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
