package darkgrey.rpg.creator;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Stream;

/** Checks every actual main-source registration, including bootstrap registrations. */
public final class CreatorNetworkDiscriminatorProbe {

    private CreatorNetworkDiscriminatorProbe() {}

    public static void main(String[] args) throws Exception {
        List<String> sources = new ArrayList<String>();
        try (Stream<Path> paths = Files.walk(Paths.get("src/main/java"))) {
            for (Path path : (Iterable<Path>) paths.filter(
                p -> p.toString()
                    .endsWith(".java"))::iterator)
                sources.add(new String(Files.readAllBytes(path), StandardCharsets.UTF_8));
        }
        Map<String, Integer> constants = new HashMap<String, Integer>();
        Pattern declaration = Pattern.compile("static\\s+final\\s+int\\s+(\\w+)\\s*=\\s*(\\d+)");
        for (String source : sources) {
            Matcher m = declaration.matcher(source);
            while (m.find()) constants.put(m.group(1), Integer.valueOf(m.group(2)));
        }
        Map<Integer, String> ids = new HashMap<Integer, String>();
        Map<Integer, String> packetTypes = new HashMap<Integer, String>();
        java.util.Set<String> handlers = new java.util.HashSet<String>();
        Pattern registration = Pattern.compile(
            "registerMessage\\s*\\([^;]*?,\\s*([\\w.]+)\\.class\\s*,\\s*(\\w+)\\s*,\\s*(?:cpw\\.mods\\.fml\\.relauncher\\.)?Side\\.(CLIENT|SERVER)\\s*\\)");
        for (String source : sources) {
            // Gramophone has its own channel and discriminator namespace.
            if (source.contains("class GramophoneNetwork")) continue;
            Matcher m = registration.matcher(source);
            while (m.find()) {
                Integer id = m.group(2)
                    .matches("\\d+") ? Integer.valueOf(m.group(2)) : constants.get(m.group(2));
                String oldType = packetTypes.put(id, m.group(1));
                if (id == null || !handlers.add(id + ":" + m.group(3))
                    || (oldType != null && !oldType.equals(m.group(1))))
                    throw new AssertionError("Unknown or duplicate discriminator: " + m.group());
                ids.put(id, ids.containsKey(id) ? ids.get(id) + "," + m.group(3) : m.group(3));
            }
        }
        if (ids.size() != 28) throw new AssertionError("Expected 28 current packet identities, found " + ids);
        for (int retired : new int[] { 3, 4, 8, 9 })
            if (ids.containsKey(retired)) throw new AssertionError("Retired discriminator reused: " + retired);
        for (int i = 5; i <= 34; i++)
            if (i != 8 && i != 9 && !ids.containsKey(i)) throw new AssertionError("Missing discriminator " + i);
        if (!"CLIENT".equals(ids.get(34))) throw new AssertionError("Current Task window open side mismatch");
        if (!"CLIENT".equals(ids.get(17)) || !"SERVER".equals(ids.get(18)))
            throw new AssertionError("Creator packet side mismatch");
        if (!"CLIENT".equals(ids.get(24)) || !"SERVER".equals(ids.get(25)))
            throw new AssertionError("Title packet side mismatch");
        if (!"SERVER".equals(ids.get(21))) throw new AssertionError("Task submit packet side mismatch");
        if (!"SERVER".equals(ids.get(22)) || !"CLIENT".equals(ids.get(23)))
            throw new AssertionError("Media packet side mismatch");
        for (int id : new int[] { 30, 32, 33 })
            if (!handlers.contains(id + ":CLIENT") || !handlers.contains(id + ":SERVER"))
                throw new AssertionError("Bidirectional packet is missing a handler: " + id);
        System.out.println("CREATOR_NETWORK_DISCRIMINATOR_PROBE=PASS");
    }
}
