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
        Pattern registration = Pattern.compile(
            "registerMessage\\s*\\([^;]*?,\\s*(\\w+)\\s*,\\s*(?:cpw\\.mods\\.fml\\.relauncher\\.)?Side\\.(CLIENT|SERVER)\\s*\\)");
        for (String source : sources) {
            Matcher m = registration.matcher(source);
            while (m.find()) {
                Integer id = m.group(1)
                    .matches("\\d+") ? Integer.valueOf(m.group(1)) : constants.get(m.group(1));
                if (id == null || ids.put(id, m.group(2)) != null)
                    throw new AssertionError("Unknown or duplicate discriminator: " + m.group());
            }
        }
        if (ids.size() != 23) throw new AssertionError("Expected 23 registrations, found " + ids);
        for (int i = 3; i <= 25; i++) if (!ids.containsKey(i)) throw new AssertionError("Missing discriminator " + i);
        if (!"CLIENT".equals(ids.get(17)) || !"SERVER".equals(ids.get(18)))
            throw new AssertionError("Creator packet side mismatch");
        if (!"CLIENT".equals(ids.get(24)) || !"SERVER".equals(ids.get(25)))
            throw new AssertionError("Title packet side mismatch");
        if (!"SERVER".equals(ids.get(21))) throw new AssertionError("Task submit packet side mismatch");
        if (!"SERVER".equals(ids.get(22)) || !"CLIENT".equals(ids.get(23)))
            throw new AssertionError("Media packet side mismatch");
        System.out.println("CREATOR_NETWORK_DISCRIMINATOR_PROBE=PASS");
    }
}
