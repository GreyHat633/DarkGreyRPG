package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.AnimationStep;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Transition;

/** Pure normalized sampling; one bounded actual source and latest complete target. */
public final class ScreenTransition {

    public static final int MAX_SOURCE_SPRITES = 96;

    public static final class Sprite {

        public final String ref, key;
        public final double x, y, w, h, alpha, cx, cy, cw, ch;
        public final int z;
        public final Transition enter, exit;
        public List<AnimationStep> animations;
        public double morphDuration;

        public Sprite(String ref, String key, double x, double y, double w, double h, double alpha, int z) {
            this(ref, key, x, y, w, h, alpha, z, 0, 0, 1, 1);
        }

        private Sprite(String ref, String key, double x, double y, double w, double h, double alpha, int z, double cx,
            double cy, double cw, double ch) {
            this(ref, key, x, y, w, h, alpha, z, cx, cy, cw, ch, null, null);
        }

        private Sprite(String ref, String key, double x, double y, double w, double h, double alpha, int z, double cx,
            double cy, double cw, double ch, Transition enter, Transition exit) {
            this.ref = ref;
            this.key = key;
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
            this.alpha = alpha;
            this.z = z;
            this.cx = cx;
            this.cy = cy;
            this.cw = cw;
            this.ch = ch;
            this.enter = enter;
            this.exit = exit;
        }

        Sprite effects(Transition enter, Transition exit) {
            return new Sprite(ref, key, x, y, w, h, alpha, z, cx, cy, cw, ch, enter, exit);
        }

        Sprite sequence(List<AnimationStep> steps, double morph) {
            animations = steps;
            morphDuration = morph;
            return this;
        }

        Sprite move(double dx, double dy, double opacity) {
            return new Sprite(ref, key, x + dx, y + dy, w, h, alpha * opacity, z, cx, cy, cw, ch, enter, exit);
        }

        Sprite clip(double x, double y, double w, double h) {
            double l = Math.max(x, cx), t = Math.max(y, cy), r = Math.min(x + w, cx + cw), b = Math.min(y + h, cy + ch);
            return new Sprite(
                ref,
                key,
                this.x,
                this.y,
                this.w,
                this.h,
                alpha,
                z,
                l,
                t,
                Math.max(0, r - l),
                Math.max(0, b - t),
                enter,
                exit);
        }
    }

    private List<Sprite> source = Collections.emptyList(), target = Collections.emptyList();
    private Transition mode = Transition.NONE;
    private long begun, id = -1;
    private boolean converged;
    private boolean perLayer;
    private double duration;

    public boolean retarget(long revision, List<Layer> layers, Transition next, long now, boolean animate) {
        if (revision == id) return false;
        List<Sprite> actual = sample(now);
        target = layers(layers);
        source = new ArrayList<Sprite>(actual);
        mode = next;
        perLayer = true;
        source.removeIf(
            s -> s.alpha <= 0 || target.stream()
                .noneMatch(t -> t.key != null && t.key.equals(s.key) && ScreenLayerAnimation.morph(t, next) > 0));
        duration = perLayer ? ScreenLayerAnimation.duration(source, target, next) : next.duration;
        begun = now;
        id = revision;
        converged = source.size() > MAX_SOURCE_SPRITES;
        if (!animate || converged || duration <= 0 || !perLayer && "none".equals(next.type)) {
            target = ScreenLayerAnimation.sample(source, target, next, duration, id);
            source = Collections.emptyList();
            perLayer = false;
            mode = Transition.NONE;
            duration = 0;
        }
        return true;
    }

    public void prepare(List<Layer> layers, Transition next, long now) {
        List<Sprite> actual = new ArrayList<Sprite>(sample(now));
        List<Sprite> upcoming = layers(layers);
        actual.removeIf(
            s -> s.alpha <= 0 || upcoming.stream()
                .noneMatch(t -> t.key != null && t.key.equals(s.key) && ScreenLayerAnimation.morph(t, next) > 0));
        target = actual;
        source = Collections.emptyList();
        perLayer = false;
        mode = Transition.NONE;
        duration = 0;
    }

    public boolean converged() {
        return converged;
    }

    public boolean active(long now) {
        return (perLayer || !"none".equals(mode.type)) && now - begun < duration * 1e9;
    }

    public void clear() {
        source = target = Collections.emptyList();
        mode = Transition.NONE;
        id = -1;
        duration = 0;
        perLayer = false;
    }

    public Set<String> refs(long now) {
        Set<String> refs = new HashSet<String>();
        for (Sprite s : target) refs.add(s.ref);
        if (active(now)) for (Sprite s : source) refs.add(s.ref);
        return refs;
    }

    public List<Sprite> sample(long now) {
        if (!active(now)) {
            if (perLayer) target = ScreenLayerAnimation.sample(source, target, mode, duration, id);
            source = Collections.emptyList();
            perLayer = false;
            mode = Transition.NONE;
            return target;
        }
        if (perLayer) return ScreenLayerAnimation.sample(source, target, mode, Math.max(0, (now - begun) / 1e9), id);
        return sampleLegacy(source, target, mode, (now - begun) / (mode.duration * 1e9), id);
    }

    static List<Sprite> sampleLegacy(List<Sprite> source, List<Sprite> target, Transition mode, double progress,
        long id) {
        if (progress >= 1 || "none".equals(mode.type)) return target;
        double p = Math.max(0, Math.min(1, progress)), t = p * p * (3 - 2 * p);
        List<Sprite> out = new ArrayList<Sprite>();
        if ("morph".equals(mode.type)) {
            Set<Sprite> matched = new HashSet<Sprite>();
            for (Sprite b : target) {
                List<Sprite> same = new ArrayList<Sprite>();
                if (b.key != null) for (Sprite a : source) if (b.key.equals(a.key)) same.add(a);
                if (same.isEmpty()) {
                    out.add(b.move(0, 0, t));
                    continue;
                }
                for (Sprite a : same) {
                    matched.add(a);
                    double x = mix(a.x, b.x, t), y = mix(a.y, b.y, t), w = mix(a.w, b.w, t), h = mix(a.h, b.h, t);
                    if (a.ref.equals(b.ref) && same.size() == 1)
                        out.add(new Sprite(b.ref, b.key, x, y, w, h, mix(a.alpha, 1, t), b.z));
                    else out.add(new Sprite(a.ref, a.key, x, y, w, h, a.alpha * (1 - t), b.z, a.cx, a.cy, a.cw, a.ch));
                }
                if (same.size() != 1 || !same.get(0).ref.equals(b.ref)) {
                    Sprite a = same.get(0);
                    out.add(
                        new Sprite(
                            b.ref,
                            b.key,
                            mix(a.x, b.x, t),
                            mix(a.y, b.y, t),
                            mix(a.w, b.w, t),
                            mix(a.h, b.h, t),
                            t,
                            b.z));
                }
            }
            for (Sprite a : source) if (!matched.contains(a)) out.add(a.move(0, 0, 1 - t));
            Collections.sort(out, (a, b) -> Integer.compare(a.z, b.z));
        } else if ("slide".equals(mode.type)) {
            double dx = "left".equals(mode.direction) ? -1 : "right".equals(mode.direction) ? 1 : 0,
                dy = "up".equals(mode.direction) ? -1 : "down".equals(mode.direction) ? 1 : 0;
            for (Sprite a : source) out.add(a.move(dx * t, dy * t, 1));
            for (Sprite b : target) out.add(b.move(-dx * (1 - t), -dy * (1 - t), 1));
        } else if ("wipe".equals(mode.type)) {
            double x = 0, y = 0, w = 1, h = 1;
            if ("left".equals(mode.direction)) {
                x = 1 - t;
                w = t;
            } else if ("right".equals(mode.direction)) w = t;
            else if ("up".equals(mode.direction)) {
                y = 1 - t;
                h = t;
            } else h = t;
            for (Sprite a : source)
                out.add(w < 1 ? a.clip(x > 0 ? 0 : t, 0, 1 - t, 1) : a.clip(0, y > 0 ? 0 : t, 1, 1 - t));
            for (Sprite b : target) out.add(b.clip(x, y, w, h));
        } else if ("random_lines".equals(mode.type)) {
            boolean horizontal = !"vertical".equals(mode.direction);
            for (int strip = 0; strip < 32; strip++) {
                int order = (strip * 13 + (int) (id & 31)) & 31;
                for (Sprite s : t * 32 >= order + 1 ? target : source)
                    out.add(horizontal ? s.clip(0, strip / 32.0, 1, 1 / 32.0) : s.clip(strip / 32.0, 0, 1 / 32.0, 1));
            }
        } else {
            for (Sprite a : source) out.add(a.move(0, 0, 1 - t));
            for (Sprite b : target) out.add(b.move(0, 0, t));
        }
        return out;
    }

    private static double mix(double a, double b, double t) {
        return a + (b - a) * t;
    }

    private static List<Sprite> layers(List<Layer> layers) {
        List<Sprite> result = new ArrayList<Sprite>();
        for (Layer l : layers) result.add(
            new Sprite(
                l.mediaRef,
                l.morphKey,
                l.x - l.anchorX * l.width,
                l.y - l.anchorY * l.height,
                l.width,
                l.height,
                1,
                l.z).effects(l.enter, l.exit)
                    .sequence(l.animations, l.morphDuration));
        Collections.sort(result, (a, b) -> Integer.compare(a.z, b.z));
        return result;
    }
}
