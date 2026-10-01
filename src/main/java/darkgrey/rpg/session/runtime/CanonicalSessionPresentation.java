package darkgrey.rpg.session.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalMediaReference;

/** Immutable, bounded Session presentation snapshot, independent of player and resource identity. */
public final class CanonicalSessionPresentation {

    public static final int MAX_JSON_CHARS = 32768;
    public static final CanonicalSessionPresentation EMPTY = new CanonicalSessionPresentation(
        0,
        0,
        null,
        false,
        0,
        0,
        1,
        Collections.<Layer>emptyList());
    private final long revision;
    private final long musicRevision;
    private final String musicRef;
    private final boolean loop;
    private final double fadeIn;
    private final double fadeOut;
    private final double musicVolume;
    private final List<Layer> layers;
    private long screenRevision;
    private Transition transition = Transition.NONE;

    private CanonicalSessionPresentation(long revision, long musicRevision, String musicRef, boolean loop,
        double fadeIn, double fadeOut, double musicVolume, List<Layer> layers) {
        if (revision < 0 || musicRevision < 0 || musicRevision > revision) throw invalid("revision");
        this.revision = revision;
        this.musicRevision = musicRevision;
        this.musicRef = musicRef;
        this.loop = loop;
        this.fadeIn = fadeIn;
        this.fadeOut = fadeOut;
        this.musicVolume = musicVolume;
        this.layers = Collections.unmodifiableList(new ArrayList<Layer>(layers));
    }

    public long getRevision() {
        return revision;
    }

    public long getMusicRevision() {
        return musicRevision;
    }

    public long getScreenRevision() {
        return screenRevision;
    }

    public Transition getTransition() {
        return transition;
    }

    private CanonicalSessionPresentation screenState(long id, Transition next) {
        screenRevision = id;
        transition = next;
        return this;
    }

    public String getMusicRef() {
        return musicRef;
    }

    public boolean isLoop() {
        return loop;
    }

    public double getFadeIn() {
        return fadeIn;
    }

    public double getFadeOut() {
        return fadeOut;
    }

    public double getMusicVolume() {
        return musicVolume;
    }

    public List<Layer> getLayers() {
        return layers;
    }

    public boolean contains(String ref) {
        if (ref.equals(musicRef)) return true;
        for (Layer layer : layers) if (ref.equals(layer.mediaRef)) return true;
        return false;
    }

    public CanonicalSessionPresentation apply(CanonicalGraphNode node) {
        if (revision == Long.MAX_VALUE) throw invalid("revision exhausted");
        JsonObject properties = new JsonObject();
        for (Map.Entry<String, JsonElement> entry : node.getProperties()
            .entrySet()) properties.add(entry.getKey(), entry.getValue());
        if ("music".equals(node.getType())) {
            keysAllowOptional(properties, "volume", "operation", "media_ref", "loop", "fade_in", "fade_out");
            String operation = text(properties, "operation");
            if (!"play".equals(operation) && !"stop".equals(operation)) throw invalid("music operation");
            JsonElement media = properties.get("media_ref");
            String ref = media.isJsonNull() ? null : text(properties, "media_ref");
            if ("play".equals(operation) ? !CanonicalMediaReference.isAudio(ref) : ref != null)
                throw invalid("music reference");
            JsonElement repeat = properties.get("loop");
            if (!repeat.isJsonPrimitive() || !repeat.getAsJsonPrimitive()
                .isBoolean()) throw invalid("loop");
            return new CanonicalSessionPresentation(
                revision + 1,
                revision + 1,
                ref,
                repeat.getAsBoolean(),
                number(properties, "fade_in", 0, 60),
                number(properties, "fade_out", 0, 60),
                numberOptional(properties, "volume", 0, 1, 1),
                layers).screenState(screenRevision, transition);
        }
        if ("screen".equals(node.getType())) {
            keysAllowOptional(properties, "transition", "layers");
            return new CanonicalSessionPresentation(
                revision + 1,
                musicRevision,
                musicRef,
                loop,
                fadeIn,
                fadeOut,
                musicVolume,
                parseLayers(properties.get("layers")))
                    .screenState(screenRevision + 1, Transition.parse(properties.get("transition")));
        }
        throw invalid("node type");
    }

    public String toJson() {
        JsonObject json = new JsonObject();
        json.addProperty("revision", revision);
        json.addProperty("music_revision", musicRevision);
        json.addProperty("media_ref", musicRef);
        json.addProperty("loop", loop);
        json.addProperty("fade_in", fadeIn);
        json.addProperty("fade_out", fadeOut);
        json.addProperty("volume", musicVolume);
        JsonArray array = new JsonArray();
        for (Layer layer : layers) array.add(layer.toJson());
        json.add("layers", array);
        json.addProperty("screen_revision", screenRevision);
        json.add("transition", transition.toJson());
        return json.toString();
    }

    public static CanonicalSessionPresentation fromJson(String value) {
        if (value == null || value.length() > MAX_JSON_CHARS) throw invalid("snapshot size");
        JsonElement parsed = new JsonParser().parse(value);
        if (!parsed.isJsonObject()) throw invalid("snapshot object");
        JsonObject json = parsed.getAsJsonObject();
        for (Map.Entry<String, JsonElement> field : json.entrySet()) if (!java.util.Arrays
            .asList(
                "revision",
                "music_revision",
                "media_ref",
                "loop",
                "fade_in",
                "fade_out",
                "volume",
                "layers",
                "screen_revision",
                "transition")
            .contains(field.getKey())) throw invalid("unexpected properties");
        JsonObject legacy = new JsonParser().parse(value)
            .getAsJsonObject();
        legacy.remove("screen_revision");
        legacy.remove("transition");
        keysAllowOptional(
            legacy,
            "volume",
            "revision",
            "music_revision",
            "media_ref",
            "loop",
            "fade_in",
            "fade_out",
            "layers");
        long revision = integer(json, "revision");
        long musicRevision = integer(json, "music_revision");
        if (json.has("screen_revision") && integer(json, "screen_revision") > revision)
            throw invalid("screen revision");
        String ref = json.get("media_ref")
            .isJsonNull() ? null : text(json, "media_ref");
        if (ref != null && !CanonicalMediaReference.isAudio(ref)) throw invalid("music reference");
        JsonElement loop = json.get("loop");
        if (!loop.isJsonPrimitive() || !loop.getAsJsonPrimitive()
            .isBoolean()) throw invalid("loop");
        return new CanonicalSessionPresentation(
            revision,
            musicRevision,
            ref,
            loop.getAsBoolean(),
            number(json, "fade_in", 0, 60),
            number(json, "fade_out", 0, 60),
            numberOptional(json, "volume", 0, 1, 1),
            parseLayers(json.get("layers"))).screenState(
                json.has("screen_revision") ? integer(json, "screen_revision") : revision,
                Transition.parse(json.get("transition")));
    }

    private static List<Layer> parseLayers(JsonElement element) {
        if (element == null || !element.isJsonArray()
            || element.getAsJsonArray()
                .size() > 32)
            throw invalid("layers");
        if (element.toString()
            .getBytes(java.nio.charset.StandardCharsets.UTF_8).length > 30000)
            throw invalid("layers/animations exceed transmission capacity (30000 UTF-8 bytes)");
        List<Layer> layers = new ArrayList<Layer>();
        java.util.Set<String> morphKeys = new java.util.HashSet<String>();
        for (JsonElement item : element.getAsJsonArray()) {
            if (!item.isJsonObject()) throw invalid("layer object");
            JsonObject json = item.getAsJsonObject();
            JsonObject geometry = new JsonParser().parse(json.toString())
                .getAsJsonObject();
            geometry.remove("enter");
            geometry.remove("exit");
            geometry.remove("animations");
            geometry.remove("morph_duration");
            keysAllowOptional(
                geometry,
                "morph_key",
                "media_ref",
                "x",
                "y",
                "width",
                "height",
                "anchor_x",
                "anchor_y",
                "z");
            Transition enter = json.has("enter") ? Transition.parse(json.get("enter")) : null;
            Transition exit = json.has("exit") ? Transition.parse(json.get("exit")) : null;
            if (exit != null && "morph".equals(exit.type)) throw invalid("exit morph");
            String key = json.has("morph_key") ? text(json, "morph_key") : null;
            if (key != null && (key.trim()
                .isEmpty() || key.length() > 96
                || !morphKeys.add(key))) throw invalid("morph_key");
            String ref = text(json, "media_ref");
            if (!CanonicalMediaReference.isImage(ref)) throw invalid("layer reference");
            double width = number(json, "width", 0, 4), height = number(json, "height", 0, 4);
            long z = integer(json, "z");
            if (width <= 0 || height <= 0 || z < -32768 || z > 32767) throw invalid("layer size/z");
            layers.add(
                new Layer(
                    ref,
                    number(json, "x", -2, 3),
                    number(json, "y", -2, 3),
                    width,
                    height,
                    number(json, "anchor_x", 0, 1),
                    number(json, "anchor_y", 0, 1),
                    (int) z,
                    key,
                    enter,
                    exit,
                    parseAnimations(json),
                    numberOptional(json, "morph_duration", 0, 60, 0)));
        }
        return layers;
    }

    private static void keys(JsonObject object, String... names) {
        if (object.entrySet()
            .size() != names.length) throw invalid("unexpected properties");
        for (String name : names) if (!object.has(name)) throw invalid("missing " + name);
    }

    private static void keysAllowOptional(JsonObject object, String optional, String... names) {
        for (Map.Entry<String, JsonElement> field : object.entrySet()) if (!field.getKey()
            .equals(optional)
            && !java.util.Arrays.asList(names)
                .contains(field.getKey()))
            throw invalid("unexpected property " + field.getKey());
        if (object.entrySet()
            .size() != names.length
            && object.entrySet()
                .size() != names.length + 1)
            throw invalid("unexpected properties");
        for (String name : names) if (!object.has(name)) throw invalid("missing " + name);
        if (object.has(optional) && object.get(optional)
            .isJsonNull()) throw invalid(optional);
    }

    private static double numberOptional(JsonObject object, String name, double min, double max, double fallback) {
        return object.has(name) ? number(object, name, min, max) : fallback;
    }

    private static String text(JsonObject object, String name) {
        JsonElement element = object.get(name);
        if (element == null || !element.isJsonPrimitive()
            || !element.getAsJsonPrimitive()
                .isString())
            throw invalid(name);
        return element.getAsString();
    }

    private static double number(JsonObject object, String name, double min, double max) {
        JsonElement element = object.get(name);
        if (element == null || !element.isJsonPrimitive()
            || !element.getAsJsonPrimitive()
                .isNumber())
            throw invalid(name);
        double value = element.getAsDouble();
        if (Double.isNaN(value) || Double.isInfinite(value) || value < min || value > max) throw invalid(name);
        return value;
    }

    private static long integer(JsonObject object, String name) {
        JsonElement value = object.get(name);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw invalid(name);
        try {
            return value.getAsBigDecimal()
                .longValueExact();
        } catch (ArithmeticException exception) {
            throw invalid(name);
        }
    }

    private static IllegalArgumentException invalid(String field) {
        return new IllegalArgumentException("Invalid Session presentation: " + field);
    }

    public static final class Layer {

        public final String mediaRef;
        public final double x, y, width, height, anchorX, anchorY;
        public final int z;
        public final String morphKey;
        public final Transition enter, exit;
        public final List<AnimationStep> animations;
        public final double morphDuration;

        private Layer(String ref, double x, double y, double width, double height, double anchorX, double anchorY,
            int z, String morphKey, Transition enter, Transition exit, List<AnimationStep> animations,
            double morphDuration) {
            this.mediaRef = ref;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.anchorX = anchorX;
            this.anchorY = anchorY;
            this.z = z;
            this.morphKey = morphKey;
            this.enter = enter;
            this.exit = exit;
            this.animations = animations;
            this.morphDuration = morphDuration;
        }

        private JsonObject toJson() {
            JsonObject json = new JsonObject();
            json.addProperty("media_ref", mediaRef);
            json.addProperty("x", x);
            json.addProperty("y", y);
            json.addProperty("width", width);
            json.addProperty("height", height);
            json.addProperty("anchor_x", anchorX);
            json.addProperty("anchor_y", anchorY);
            json.addProperty("z", z);
            if (morphKey != null) json.addProperty("morph_key", morphKey);
            if (enter != null) json.add("enter", enter.toJson());
            if (exit != null) json.add("exit", exit.toJson());
            if (animations != null) {
                JsonArray steps = new JsonArray();
                for (AnimationStep step : animations) steps.add(step.toJson());
                json.add("animations", steps);
                json.addProperty("morph_duration", morphDuration);
            }
            return json;
        }
    }

    public static final class AnimationStep {

        public final String kind;
        public final Transition effect;
        public final double delay;

        public AnimationStep(String kind, Transition effect, double delay) {
            this.kind = kind;
            this.effect = effect;
            this.delay = delay;
        }

        JsonObject toJson() {
            JsonObject json = effect.toJson();
            json.addProperty("kind", kind);
            json.addProperty("delay", delay);
            return json;
        }
    }

    private static List<AnimationStep> parseAnimations(JsonObject layer) {
        if (!layer.has("animations")) return null;
        if (layer.has("enter") || layer.has("exit")
            || !layer.get("animations")
                .isJsonArray())
            throw invalid("animations");
        List<AnimationStep> result = new ArrayList<AnimationStep>();
        for (JsonElement value : layer.getAsJsonArray("animations")) {
            if (!value.isJsonObject()) throw invalid("animation step");
            JsonObject step = value.getAsJsonObject();
            keys(step, "kind", "type", "direction", "duration", "delay");
            String kind = text(step, "kind");
            if (!"enter".equals(kind) && !"exit".equals(kind)) throw invalid("animation kind");
            JsonObject effect = new JsonParser().parse(step.toString())
                .getAsJsonObject();
            effect.remove("kind");
            effect.remove("delay");
            Transition transition = Transition.parse(effect);
            if ("none".equals(transition.type) || "morph".equals(transition.type)) throw invalid("animation type");
            result.add(new AnimationStep(kind, transition, number(step, "delay", 0, 60)));
        }
        return Collections.unmodifiableList(result);
    }

    public static final class Transition {

        public static final Transition NONE = new Transition("none", "left", 0);
        public final String type, direction;
        public final double duration;

        public Transition(String type, String direction, double duration) {
            this.type = type;
            this.direction = direction;
            this.duration = duration;
        }

        public static Transition parse(JsonElement value) {
            if (value == null) return NONE;
            if (!value.isJsonObject()) throw invalid("transition");
            JsonObject json = value.getAsJsonObject();
            keys(json, "type", "direction", "duration");
            String type = text(json, "type"), direction = text(json, "direction");
            if (!java.util.Arrays.asList("none", "fade", "slide", "wipe", "random_lines", "morph")
                .contains(type)
                || !java.util.Arrays.asList("left", "right", "up", "down", "horizontal", "vertical")
                    .contains(direction))
                throw invalid("transition mode/direction");
            if (("slide".equals(type) || "wipe".equals(type)) && !java.util.Arrays.asList("left", "right", "up", "down")
                .contains(direction)
                || "random_lines".equals(type) && !java.util.Arrays.asList("horizontal", "vertical")
                    .contains(direction))
                throw invalid("transition direction for mode");
            return new Transition(type, direction, number(json, "duration", 0, 60));
        }

        public JsonObject toJson() {
            JsonObject json = new JsonObject();
            json.addProperty("type", type);
            json.addProperty("direction", direction);
            json.addProperty("duration", duration);
            return json;
        }
    }
}
