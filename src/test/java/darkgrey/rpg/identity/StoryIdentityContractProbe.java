package darkgrey.rpg.identity;

import java.io.BufferedReader;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.HashSet;
import java.util.Set;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

/** Executes the same vectors as StoryIdentityContractTests with the Minecraft Gson version. */
public final class StoryIdentityContractProbe {

    private StoryIdentityContractProbe() {}

    public static void main(String[] args) throws Exception {
        if (args.length != 1) throw new IllegalArgumentException("Expected the shared identity vector file.");
        int count = 0;
        try (BufferedReader input = Files.newBufferedReader(Paths.get(args[0]), StandardCharsets.UTF_8)) {
            String line;
            while ((line = input.readLine()) != null) {
                JsonObject vector = new JsonParser().parse(line)
                    .getAsJsonObject();
                String value = vector.get("input")
                    .getAsString();
                boolean valid = vector.get("valid")
                    .getAsBoolean();
                count++;
                if ("uid".equals(
                    vector.get("type")
                        .getAsString())) {
                    require(StoryUid.isValid(value) == valid, "UID vector " + count);
                    if (valid) require(
                        value.equals(
                            StoryUid.parse(value)
                                .getValue()),
                        "UID parse " + count);
                    else {
                        try {
                            StoryUid.parse(value);
                            throw new AssertionError("Accepted invalid UID " + count);
                        } catch (IllegalArgumentException expected) { /* rejected */ }
                    }
                } else if (valid) {
                    ResourceAddress address = ResourceAddressJson.parse(value);
                    String encoded = ResourceAddressJson.serialize(address);
                    require(
                        vector.get("canonical")
                            .getAsString()
                            .equals(encoded),
                        "JSON vector " + count);
                    require(
                        vector.get("path")
                            .getAsString()
                            .equals(address.relativeDefinitionPath()),
                        "Path vector " + count);
                    require(address.equals(ResourceAddressJson.parse(encoded)), "Round trip " + count);
                    require(
                        address.equals(ResourceAddress.fromKey(address.toKey())),
                        "Internal key round trip " + count);
                    require(ResourceAddress.isKey(address.toKey()), "Current key validation " + count);
                } else {
                    try {
                        ResourceAddressJson.parse(value);
                        throw new AssertionError("Accepted invalid address " + count);
                    } catch (IOException expected) { /* rejected */ }
                }
            }
        }
        require(count >= 50, "Incomplete vector set");
        StoryUid a = StoryUid.parse("ST-2345-6789-ABCD-EFGH");
        StoryUid b = StoryUid.parse("ST-JKLM-NPQR-STUV-WXYZ");
        Set<ResourceAddress> addresses = new HashSet<ResourceAddress>();
        addresses.add(new ResourceAddress(a, ResourceAddress.Kind.ACTOR, "r17"));
        addresses.add(new ResourceAddress(b, ResourceAddress.Kind.ACTOR, "r17"));
        addresses.add(new ResourceAddress(a, ResourceAddress.Kind.ITEM, "r17"));
        require(addresses.size() == 3, "Owner/kind isolation");
        require(
            addresses.contains(new ResourceAddress(StoryUid.parse(a.getValue()), ResourceAddress.Kind.ACTOR, "r17")),
            "Value equality");
        Set<StoryUid> identities = new HashSet<StoryUid>();
        for (int i = 0; i < 2048; i++) {
            StoryUid uid = StoryUid.create(identities);
            require(StoryUid.isValid(uid.getValue()) && identities.add(uid), "UID allocation");
            ResourceAddress address = ResourceAddress.create(uid, ResourceAddress.Kind.ACTOR, addresses);
            require(
                address.getLocalId()
                    .length() == 33 && addresses.add(address),
                "Address allocation");
        }
        require(!StoryUid.isValid(null) && !ResourceAddress.isValidLocalId(null), "Null validation");
        require(
            !ResourceAddress.isKey("Author:actor") && !ResourceAddress.isKey("r17") && !ResourceAddress.isKey(null),
            "No legacy key fallback");
        System.out.println("STORY_IDENTITY_CONTRACT_PROBE=PASS vectors=" + count + " allocations=2048");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
