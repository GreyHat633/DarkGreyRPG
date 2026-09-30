package darkgrey.rpg.session.runtime;

import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

/** Paired with Studio DynamicContentText. Unmarked historical strings are always literal. */
public final class DynamicContentText {

    public static final String PREFIX = "\u001eDGR1\u001f";

    public interface Resolver {

        String resolve(String type, String itemId);
    }

    private DynamicContentText() {}

    public static String resolve(String value, Resolver resolver) {
        if (value == null || !value.startsWith(PREFIX)) return value == null ? "" : value;
        if (value.length() > 1048576) throw new IllegalArgumentException("Dynamic envelope is too long.");
        validateJson(value.substring(PREFIX.length()));
        JsonElement root = new JsonParser().parse(value.substring(PREFIX.length()));
        if (!root.isJsonArray() || root.getAsJsonArray()
            .size() > 4096) throw new IllegalArgumentException("Invalid dynamic content envelope.");
        StringBuilder result = new StringBuilder();
        for (JsonElement element : root.getAsJsonArray()) {
            if (element.isJsonPrimitive() && element.getAsJsonPrimitive()
                .isString()) {
                result.append(element.getAsString());
                if (result.length() > 131072) throw new IllegalArgumentException("Dynamic text is too long.");
                continue;
            }
            JsonObject object = element.getAsJsonObject();
            String type = string(object, "type");
            boolean item = "item_count".equals(type) || "item_name".equals(type);
            boolean actor = "actor_name".equals(type);
            if (!"player_name".equals(type) && !"player_level".equals(type) && !item && !actor)
                throw new IllegalArgumentException("Unknown dynamic content.");
            for (Map.Entry<String, JsonElement> field : object.entrySet())
                if (!"type".equals(field.getKey()) && !(item && "item_id".equals(field.getKey()))
                    && !(actor && "actor_id".equals(field.getKey())))
                    throw new IllegalArgumentException("Unknown dynamic content field.");
            String id = item ? string(object, "item_id") : actor ? string(object, "actor_id") : null;
            if (id != null && id.trim()
                .isEmpty()) throw new IllegalArgumentException("Missing item identity.");
            String resolved = resolver.resolve(type, id);
            result.append(resolved == null ? "数据不可用" : resolved);
            if (result.length() > 131072) throw new IllegalArgumentException("Dynamic text is too long.");
        }
        return result.toString();
    }

    private static void validateJson(String json) {
        try {
            com.google.gson.stream.JsonReader reader = new com.google.gson.stream.JsonReader(
                new java.io.StringReader(json));
            reader.setLenient(false);
            reader.beginArray();
            int count = 0;
            while (reader.hasNext()) {
                if (++count > 4096) throw new IllegalArgumentException("Too many dynamic parts.");
                if (reader.peek() == com.google.gson.stream.JsonToken.STRING) reader.nextString();
                else {
                    reader.beginObject();
                    java.util.Set<String> fields = new java.util.HashSet<String>();
                    while (reader.hasNext()) {
                        if (!fields.add(reader.nextName()))
                            throw new IllegalArgumentException("Duplicate dynamic field.");
                        if (reader.peek() != com.google.gson.stream.JsonToken.STRING)
                            throw new IllegalArgumentException("Expected dynamic string.");
                        reader.nextString();
                    }
                    reader.endObject();
                }
            }
            reader.endArray();
            if (reader.peek() != com.google.gson.stream.JsonToken.END_DOCUMENT)
                throw new IllegalArgumentException("Trailing dynamic data.");
            reader.close();
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Invalid dynamic JSON.", invalid);
        }
    }

    private static String string(JsonObject object, String key) {
        JsonElement value = object.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw new IllegalArgumentException("Expected string: " + key);
        return value.getAsString();
    }
}
