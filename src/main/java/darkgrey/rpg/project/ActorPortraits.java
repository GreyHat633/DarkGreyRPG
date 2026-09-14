package darkgrey.rpg.project;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;

/** Presentation data only; never participates in Actor identity. */
public final class ActorPortraits {

    private final String defaultRef;
    private final Map<String, String> variants;

    public ActorPortraits(String defaultRef, Map<String, String> variants) {
        if (defaultRef != null && !CanonicalMediaReference.isImage(defaultRef))
            throw new IllegalArgumentException("Invalid default portrait reference.");
        Map<String, String> copy = new LinkedHashMap<String, String>();
        for (Map.Entry<String, String> entry : variants.entrySet()) {
            if (entry.getKey() == null || entry.getKey()
                .trim()
                .isEmpty() || !CanonicalMediaReference.isImage(entry.getValue()))
                throw new IllegalArgumentException("Invalid portrait variant.");
            copy.put(entry.getKey(), entry.getValue());
        }
        this.defaultRef = defaultRef;
        this.variants = Collections.unmodifiableMap(copy);
    }

    public static ActorPortraits empty() {
        return new ActorPortraits(null, Collections.<String, String>emptyMap());
    }

    public static ActorPortraits parse(JsonObject object) {
        JsonElement defaultValue = object.get("default_portrait_ref");
        String defaultRef = defaultValue == null || defaultValue.isJsonNull() ? null : string(defaultValue);
        Map<String, String> variants = new LinkedHashMap<String, String>();
        JsonElement values = object.get("portrait_variants");
        if (values != null) {
            if (!values.isJsonArray()) throw new IllegalArgumentException("portrait_variants must be an array.");
            for (JsonElement value : values.getAsJsonArray()) {
                if (!value.isJsonObject()) throw new IllegalArgumentException("Invalid portrait variant.");
                JsonObject variant = value.getAsJsonObject();
                if (variant.entrySet()
                    .size() != 2 || !variant.has("name")
                    || !variant.has("media_ref"))
                    throw new IllegalArgumentException("Invalid portrait variant fields.");
                String name = string(variant.get("name"));
                if (variants.put(name, string(variant.get("media_ref"))) != null)
                    throw new IllegalArgumentException("Duplicate portrait variant: " + name);
            }
        }
        return new ActorPortraits(defaultRef, variants);
    }

    private static String string(JsonElement value) {
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw new IllegalArgumentException("Portrait field must be a string.");
        return value.getAsString();
    }

    public String getDefaultRef() {
        return defaultRef;
    }

    public Map<String, String> getVariants() {
        return variants;
    }

    public String resolve(String name) {
        if (name == null) return defaultRef;
        if (!variants.containsKey(name)) throw new IllegalArgumentException("Missing Actor portrait variant: " + name);
        return variants.get(name);
    }
}
