package darkgrey.rpg.project.packages;

import java.io.IOException;
import java.io.Reader;
import java.math.BigDecimal;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import com.google.gson.JsonPrimitive;
import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

import darkgrey.rpg.project.ProjectLoadException;

/** Bounded strict JSON tree reader; Gson's default parser silently replaces duplicate fields. */
final class StrictPackageJson {

    private StrictPackageJson() {}

    static JsonElement read(Reader source) throws ProjectLoadException {
        try {
            JsonReader reader = new JsonReader(source);
            reader.setLenient(false);
            JsonElement result = value(reader, 0);
            if (reader.peek() != JsonToken.END_DOCUMENT) throw new IOException("Trailing package JSON");
            return result;
        } catch (IOException | RuntimeException exception) {
            throw new ProjectLoadException("Invalid strict package JSON", exception);
        }
    }

    private static JsonElement value(JsonReader reader, int depth) throws IOException {
        if (depth > 64) throw new IOException("Package JSON depth exceeded");
        switch (reader.peek()) {
            case BEGIN_OBJECT:
                JsonObject object = new JsonObject();
                reader.beginObject();
                while (reader.hasNext()) {
                    String name = reader.nextName();
                    if (object.has(name)) throw new IOException("Duplicate package JSON field: " + name);
                    object.add(name, value(reader, depth + 1));
                }
                reader.endObject();
                return object;
            case BEGIN_ARRAY:
                JsonArray array = new JsonArray();
                reader.beginArray();
                while (reader.hasNext()) array.add(value(reader, depth + 1));
                reader.endArray();
                return array;
            case STRING:
                return new JsonPrimitive(reader.nextString());
            case NUMBER:
                return new JsonPrimitive(new BigDecimal(reader.nextString()));
            case BOOLEAN:
                return new JsonPrimitive(reader.nextBoolean());
            case NULL:
                reader.nextNull();
                return JsonNull.INSTANCE;
            default:
                throw new IOException("Unexpected package JSON token");
        }
    }
}
