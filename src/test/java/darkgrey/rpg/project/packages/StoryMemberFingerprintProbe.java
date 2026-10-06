package darkgrey.rpg.project.packages;

import java.io.BufferedReader;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.identity.StoryUid;

public final class StoryMemberFingerprintProbe {

    private StoryMemberFingerprintProbe() {}

    public static void main(String[] args) throws Exception {
        if (args.length != 1) throw new IllegalArgumentException("Expected fingerprint vectors path.");
        int count = 0;
        try (BufferedReader input = Files.newBufferedReader(Paths.get(args[0]), StandardCharsets.UTF_8)) {
            String line;
            while ((line = input.readLine()) != null) {
                JsonObject vector = new JsonParser().parse(line)
                    .getAsJsonObject();
                List<StoryMemberFingerprint.Content> records = new ArrayList<StoryMemberFingerprint.Content>();
                for (JsonElement element : vector.getAsJsonArray("records")) {
                    JsonObject record = element.getAsJsonObject();
                    records.add(
                        new StoryMemberFingerprint.Content(
                            record.get("role")
                                .getAsString(),
                            record.get("identity")
                                .getAsString(),
                            record.get("content")
                                .getAsString()));
                }
                if (vector.get("valid")
                    .getAsBoolean()) {
                    String result = StoryMemberFingerprint.compute(
                        StoryUid.parse(
                            vector.get("uid")
                                .getAsString()),
                        records);
                    if (!vector.get("hash")
                        .getAsString()
                        .equals(result)) throw new AssertionError("Fingerprint vector " + count + ": " + result);
                } else {
                    boolean rejected = false;
                    try {
                        StoryMemberFingerprint.compute(
                            StoryUid.parse(
                                vector.get("uid")
                                    .getAsString()),
                            records);
                    } catch (IOException | IllegalArgumentException expected) {
                        rejected = true;
                    }
                    if (!rejected) throw new AssertionError("Accepted invalid fingerprint vector " + count);
                }
                count++;
            }
        }
        if (count < 30) throw new AssertionError("Missing fingerprint vectors");
        System.out.println("STORY_MEMBER_FINGERPRINT_PROBE=PASS vectors=" + count);
    }
}
