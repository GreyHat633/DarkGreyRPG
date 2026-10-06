package darkgrey.rpg.identity;

import java.io.IOException;
import java.io.StringReader;
import java.io.StringWriter;
import java.util.HashSet;
import java.util.Set;

import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;
import com.google.gson.stream.JsonWriter;

/** Strict structured address codec. Never accepts old Namespace IDs or omitted owners. */
public final class ResourceAddressJson {

    private ResourceAddressJson() {}

    public static ResourceAddress parse(String json) throws IOException {
        if (json == null) throw new IOException("A resource address object is required.");
        try (JsonReader reader = new JsonReader(new StringReader(json))) {
            reader.setLenient(false);
            ResourceAddress result = read(reader);
            if (reader.peek() != JsonToken.END_DOCUMENT) throw new IOException("Trailing resource address content.");
            return result;
        }
    }

    public static ResourceAddress read(JsonReader reader) throws IOException {
        if (reader.peek() != JsonToken.BEGIN_OBJECT) throw new IOException("A resource address object is required.");
        reader.beginObject();
        String uid = null, kind = null, local = null;
        Set<String> seen = new HashSet<String>();
        while (reader.hasNext()) {
            String name = reader.nextName();
            if (!seen.add(name)) throw new IOException("Duplicate resource address field.");
            if (reader.peek() != JsonToken.STRING) throw new IOException("Address fields must be strings.");
            String value = reader.nextString();
            if ("story_uid".equals(name)) uid = value;
            else if ("kind".equals(name)) kind = value;
            else if ("local_id".equals(name)) local = value;
            else throw new IOException("Unknown resource address field.");
        }
        reader.endObject();
        if (uid == null || kind == null || local == null) throw new IOException("Incomplete resource address.");
        try {
            return new ResourceAddress(StoryUid.parse(uid), ResourceAddress.Kind.parse(kind), local);
        } catch (IllegalArgumentException exception) {
            throw new IOException("Invalid resource address.", exception);
        }
    }

    public static String serialize(ResourceAddress address) throws IOException {
        StringWriter text = new StringWriter();
        try (JsonWriter writer = new JsonWriter(text)) {
            write(writer, address);
        }
        return text.toString();
    }

    public static void write(JsonWriter writer, ResourceAddress address) throws IOException {
        if (address == null) throw new IOException("A resource address cannot be null.");
        writer.beginObject();
        writer.name("story_uid")
            .value(
                address.getStoryUid()
                    .getValue());
        writer.name("kind")
            .value(
                address.getKind()
                    .getToken());
        writer.name("local_id")
            .value(address.getLocalId());
        writer.endObject();
    }
}
