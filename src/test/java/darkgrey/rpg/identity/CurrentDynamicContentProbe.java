package darkgrey.rpg.identity;

import java.io.Reader;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.session.runtime.DynamicContentText;

public final class CurrentDynamicContentProbe {

    public static void main(String[] args) throws Exception {
        JsonArray vectors;
        try (Reader reader = Files
            .newBufferedReader(Paths.get("src/test/resources/dynamic-content-0334.json"), StandardCharsets.UTF_8)) {
            vectors = new JsonParser().parse(reader)
                .getAsJsonArray();
        }
        for (JsonElement entry : vectors) {
            JsonObject vector = entry.getAsJsonObject();
            boolean rejected = false;
            String actual = null;
            try {
                actual = DynamicContentText.resolve(
                    vector.get("input")
                        .getAsString(),
                    (type, id) -> "player_name".equals(type) ? "Alice" : "player_level".equals(type) ? "12" : "3");
            } catch (RuntimeException invalid) {
                rejected = true;
            }
            if (vector.has("invalid") ? !rejected
                : rejected || !vector.get("expected")
                    .getAsString()
                    .equals(actual)) {
                throw new AssertionError("Current dynamic-content vector failed: " + vector);
            }
        }
        System.out.println("CurrentDynamicContentProbe PASS: " + vectors.size() + " shared vectors");
    }
}
