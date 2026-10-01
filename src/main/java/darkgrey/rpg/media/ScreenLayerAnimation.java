package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.media.ScreenTransition.Sprite;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.AnimationStep;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Transition;

/** Per-image entering, leaving and matched-image motion; no timing queue. */
final class ScreenLayerAnimation {

    private ScreenLayerAnimation() {}

    private static Transition legacyEntry(Transition effect) {
        if (!"slide".equals(effect.type) && !"wipe".equals(effect.type)) return effect;
        String direction = "left".equals(effect.direction) ? "right"
            : "right".equals(effect.direction) ? "left" : "up".equals(effect.direction) ? "down" : "up";
        return new Transition(effect.type, direction, effect.duration);
    }

    static double morph(Sprite s, Transition fallback) {
        if (s.animations != null) return s.morphDuration;
        Transition enter = s.enter == null ? fallback : s.enter;
        return "morph".equals(enter.type) ? enter.duration : 0;
    }

    private static List<AnimationStep> steps(Sprite s, Transition fallback) {
        if (s.animations != null) return s.animations;
        List<AnimationStep> result = new ArrayList<AnimationStep>();
        Transition enter = s.enter == null ? legacyEntry(fallback) : s.enter;
        if (!"none".equals(enter.type) && !"morph".equals(enter.type)) result.add(new AnimationStep("enter", enter, 0));
        if (s.exit != null && !"none".equals(s.exit.type)) result.add(new AnimationStep("exit", s.exit, 0));
        return result;
    }

    private static List<Sprite> matches(List<Sprite> source, Sprite target) {
        List<Sprite> result = new ArrayList<Sprite>();
        if (target.key != null)
            for (Sprite s : source) if (target.key.equals(s.key) && s.alpha > 0 && s.cw > 0 && s.ch > 0) result.add(s);
        return result;
    }

    static double duration(List<Sprite> source, List<Sprite> target, Transition fallback) {
        double duration = 0;
        for (Sprite s : target) {
            double total = matches(source, s).isEmpty() ? 0 : morph(s, fallback);
            for (AnimationStep step : steps(s, fallback)) total += step.delay + step.effect.duration;
            duration = Math.max(duration, total);
        }
        return duration;
    }

    static List<Sprite> sample(List<Sprite> source, List<Sprite> target, Transition fallback, double seconds,
        long seed) {
        List<Sprite> result = new ArrayList<Sprite>();
        for (Sprite b : target) {
            List<Sprite> same = matches(source, b);
            double motion = same.isEmpty() ? 0 : morph(b, fallback);
            if (seconds < motion) {
                result.addAll(
                    ScreenTransition.sampleLegacy(
                        same,
                        Collections.singletonList(b),
                        new Transition("morph", "left", motion),
                        seconds / motion,
                        seed));
                continue;
            }
            double elapsed = Math.max(0, seconds - motion);
            List<AnimationStep> sequence = steps(b, fallback);
            boolean visible = sequence.isEmpty() || !"enter".equals(sequence.get(0).kind), sampling = false;
            for (AnimationStep step : sequence) {
                if (elapsed < step.delay) break;
                elapsed -= step.delay;
                if (elapsed < step.effect.duration) {
                    if ("enter".equals(step.kind) || visible)
                        add(result, b, step.effect, elapsed, "enter".equals(step.kind), seed);
                    sampling = true;
                    break;
                }
                elapsed -= step.effect.duration;
                visible = "enter".equals(step.kind);
            }
            if (!sampling && visible) result.add(b);
        }
        Collections.sort(result, (a, b) -> Integer.compare(a.z, b.z));
        return result;
    }

    private static void add(List<Sprite> result, Sprite s, Transition effect, double seconds, boolean entering,
        long seed) {
        double p = effect.duration <= 0 ? 1 : Math.max(0, Math.min(1, seconds / effect.duration)),
            t = p * p * (3 - 2 * p);
        if ("none".equals(effect.type) || p >= 1) {
            if (entering) result.add(s);
            return;
        }
        double visible = entering ? t : 1 - t;
        if ("slide".equals(effect.type)) {
            double x = "left".equals(effect.direction) ? -s.w : "right".equals(effect.direction) ? 1 : s.x;
            double y = "up".equals(effect.direction) ? -s.h : "down".equals(effect.direction) ? 1 : s.y;
            result.add(s.move((x - s.x) * (1 - visible), (y - s.y) * (1 - visible), 1));
        } else if ("wipe".equals(effect.type)) {
            boolean horizontal = "left".equals(effect.direction) || "right".equals(effect.direction);
            result.add(
                s.clip(
                    s.x + ("right".equals(effect.direction) ? s.w * (1 - visible) : 0),
                    s.y + ("down".equals(effect.direction) ? s.h * (1 - visible) : 0),
                    s.w * (horizontal ? visible : 1),
                    s.h * (horizontal ? 1 : visible)));
        } else if ("random_lines".equals(effect.type)) {
            for (int strip = 0; strip < 32; strip++)
                if (visible * 32 >= ((strip * 13 + (int) (seed & 31)) & 31) + 1) result.add(
                    "vertical".equals(effect.direction) ? s.clip(s.x + s.w * strip / 32, s.y, s.w / 32, s.h)
                        : s.clip(s.x, s.y + s.h * strip / 32, s.w, s.h / 32));
        } else result.add(s.move(0, 0, visible));
    }
}
