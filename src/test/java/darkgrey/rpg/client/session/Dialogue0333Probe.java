package darkgrey.rpg.client.session;

import java.util.Collections;

import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Deterministic display-only transitions and source-range integrity. */
public final class Dialogue0333Probe {

    private static final DialogueDisplayPages.Metrics METRICS = new DialogueDisplayPages.Metrics() {

        @Override
        public double advance(String cluster, boolean bold) {
            return cluster.equals("W") ? 3 : 1;
        }
    };

    public static void main(String[] args) throws Exception {
        com.google.gson.JsonObject profile;
        try (java.io.Reader reader = java.nio.file.Files.newBufferedReader(
            java.nio.file.Paths.get("schema/dialogue-capacity-profile.json"),
            java.nio.charset.StandardCharsets.UTF_8)) {
            profile = new com.google.gson.JsonParser().parse(reader)
                .getAsJsonObject();
        }
        CanonicalDialogueLayout baseline = new CanonicalDialogueLayout(320, 240);
        int quote = (int) Math.ceil(
            profile.getAsJsonArray("normal")
                .get('「')
                .getAsInt() * 1.5);
        check(
            profile.get("wrap_width")
                .getAsInt() == (int) ((baseline.textWidth - 2 * quote) / 1.5),
            "shared profile geometry including rounded quote gutters");
        for (String mode : new String[] { "normal", "unicode" }) {
            final com.google.gson.JsonArray advances = profile.getAsJsonArray(mode);
            for (com.google.gson.JsonElement value : profile.getAsJsonArray("vectors")) {
                com.google.gson.JsonObject vector = value.getAsJsonObject();
                String text = vector.get("text")
                    .getAsString();
                DialogueDisplayPages measured = DialogueDisplayPages.measure(
                    text,
                    profile.get("wrap_width")
                        .getAsInt(),
                    profile.get("rows")
                        .getAsInt(),
                    (cluster, bold) -> {
                        int width = 0;
                        for (int i = 0; i < cluster.length(); i++) {
                            int advance = advances.get(cluster.charAt(i))
                                .getAsInt();
                            width += advance + (bold && advance > 0 ? 1 : 0);
                        }
                        return width;
                    });
                check(
                    measured.lines.size() == vector.getAsJsonObject(mode + "_result")
                        .get("lines")
                        .getAsInt(),
                    "shared profile line breaks: " + mode + " " + text);
            }
        }
        String[] samples = { "", "a W  b", "中😀e\u0301末", "\u00a7lWide\u00a7r normal", "a\r\nb\nc", "WWWWWW" };
        for (String text : samples) {
            DialogueDisplayPages pages = DialogueDisplayPages.measure(text, 2, 1, METRICS);
            StringBuilder joined = new StringBuilder();
            for (int i = 0; i < pages.pageCount(); i++) joined.append(text.substring(pages.start(i), pages.end(i)));
            check(text.equals(joined.toString()), "exact source coverage " + text);
            for (DialogueDisplayPages.Line line : pages.lines) {
                check(
                    line.end == text.length() || !Character.isLowSurrogate(text.charAt(line.end)),
                    "surrogate boundary");
                check(line.end == 0 || text.charAt(line.end - 1) != '\u00a7', "format boundary");
            }
        }
        CanonicalSessionClientModel model = new CanonicalSessionClientModel();
        CanonicalSessionFrame frame = new CanonicalSessionFrame(
            1,
            "story",
            "session",
            "line",
            CanonicalSessionFrame.Kind.LINE,
            "speaker",
            "abcdef",
            Collections.<CanonicalSessionChoiceOption>emptyList()).withTextSpeed(0);
        check(model.acceptFrame(frame), "frame accepted");
        model.layout("small", 2, 1, METRICS);
        check(
            model.getVisibleText()
                .equals("ab"),
            "first local page");
        check(
            model.advanceDisplayPage() && model.getVisibleText()
                .equals("cd"),
            "second local page, no send");
        check(model.acceptFrame(frame), "same frame resync");
        check(
            model.getVisibleText()
                .equals("cd"),
            "same frame keeps page");
        model.layout("wide", 3, 1, METRICS);
        check(
            model.getVisibleText()
                .equals("abc"),
            "reflow repeats safely from source anchor");
        check(
            model.advanceDisplayPage() && model.getVisibleText()
                .equals("def"),
            "no unread suffix skipped");
        check(!model.advanceDisplayPage(), "one authoritative advance");
        check(model.advanceDisplayPage() && model.awaitingServer(), "double click cannot send again");
        model.acceptFrame(frame);
        check(model.awaitingServer(), "same frame preserves waiting fence");
        model.clear();
        model.acceptFrame(frame);
        model.layout("small", 2, 1, METRICS);
        model.toggleAutomatic();
        check(!model.autoDue(true, 100), "auto starts one clock");
        check(!model.autoDue(false, 4000000100L), "background cancels clock");
        check(!model.autoDue(true, 5000000100L), "foreground gets full delay");
        check(model.autoDue(true, 8000000100L), "auto due");
        check(model.advanceDisplayPage(), "auto uses local advance");
        check(!model.autoDue(true, 8000000101L), "new page gets full delay");
        model.clear();
        check(model.automatic(), "new session retains global automatic preference");
        check(new CanonicalSessionClientModel().automatic(), "new model shares automatic preference");
        model.toggleAutomatic();
        model.clear();
        check(!model.automatic(), "manual disable persists across sessions");
        for (int i = 0; i <= 120; i++) {
            DialoguePreferences.setSliderPosition(i);
            check(DialoguePreferences.speed() == (i == 120 ? 0 : i + 1), "monotonic UI speed");
            check(DialoguePreferences.sliderPosition() == i, "speed round trip");
        }
        model.clear();
        model.acceptFrame(frame.withTextSpeed(1));
        model.layout("quote-reveal", 20, 2, METRICS);
        check(!model.displayTextComplete(), "closing quote waits for actual sentence reveal");
        model.finishVisibleText();
        check(model.displayTextComplete(), "closing quote can follow the finished short sentence");
        DialoguePreferences.setSpeed(30);
        layoutMeasurementReuse();
        System.out.println("DIALOGUE_0333=PASS");
    }

    private static void layoutMeasurementReuse() {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < 1000; i++) text.append("W中 i ");
        final int[] measurements = { 0 };
        DialogueDisplayPages.Metrics counting = (cluster, bold) -> {
            measurements[0]++;
            return METRICS.advance(cluster, bold);
        };
        CanonicalSessionClientModel model = new CanonicalSessionClientModel();
        model.acceptFrame(
            new CanonicalSessionFrame(
                1,
                "perf-story",
                "perf-session",
                "perf-line",
                CanonicalSessionFrame.Kind.LINE,
                "speaker",
                text.toString(),
                Collections.<CanonicalSessionChoiceOption>emptyList()).withTextSpeed(30));
        long started = System.nanoTime();
        model.layout("stable", 125, 3, counting);
        long firstNanos = System.nanoTime() - started;
        int initial = measurements[0];
        check(initial > 0, "first layout measures the real long sentence");
        started = System.nanoTime();
        for (int i = 0; i < 1000; i++) {
            model.layout("stable", 125, 3, counting);
            model.getVisibleText();
            model.getDisplayLines();
        }
        long cachedNanos = System.nanoTime() - started;
        check(measurements[0] == initial, "1000 reveal frames must not remeasure unchanged text");
        model.layout("resized", 110, 3, counting);
        int resized = measurements[0];
        check(resized > initial, "real resize must remeasure");
        model.layout("resized", 110, 3, counting);
        check(measurements[0] == resized, "resized layout also caches subsequent frames");
        System.out.println(
            "DIALOGUE_LAYOUT_REUSE initial_metrics=" + initial
                + " unchanged_frames=1000 extra_metrics=0"
                + " resize_metrics="
                + (resized - initial)
                + " initial_ns="
                + firstNanos
                + " cached_1000_frames_ns="
                + cachedNanos);
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
