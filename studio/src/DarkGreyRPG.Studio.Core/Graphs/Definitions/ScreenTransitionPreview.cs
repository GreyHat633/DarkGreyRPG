using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Transient normalized samples. No graph mutation or media side effects.</summary>
public static class ScreenTransitionPreview
{
    public sealed record Sprite(string Media, string? Key, double X, double Y, double Width, double Height, double Alpha, int Z,
        double ClipX = 0, double ClipY = 0, double ClipWidth = 1, double ClipHeight = 1, ScreenLayerEffect? Enter = null, ScreenLayerEffect? Exit = null, IReadOnlyList<ScreenAnimationStep>? Animations = null, double MorphDuration = 0);
    public static IReadOnlyList<Sprite> Layers(JsonElement layers) => layers.EnumerateArray().Select(l => new Sprite(
        l.GetProperty("media_ref").GetString()!, l.TryGetProperty("morph_key", out var key) ? key.GetString() : null,
        l.GetProperty("x").GetDouble() - l.GetProperty("anchor_x").GetDouble() * l.GetProperty("width").GetDouble(),
        l.GetProperty("y").GetDouble() - l.GetProperty("anchor_y").GetDouble() * l.GetProperty("height").GetDouble(),
        l.GetProperty("width").GetDouble(), l.GetProperty("height").GetDouble(), 1, l.GetProperty("z").GetInt32(),
        Enter: l.TryGetProperty("enter", out var enter) ? ScreenLayerEffect.Read(enter) : null,
        Exit: l.TryGetProperty("exit", out var exit) ? ScreenLayerEffect.Read(exit) : null,
        Animations: l.TryGetProperty("animations", out var animations) ? animations.EnumerateArray().Select(ScreenAnimationStep.Read).ToArray() : null,
        MorphDuration: l.TryGetProperty("morph_duration", out var morph) ? morph.GetDouble() : 0)).OrderBy(l => l.Z).ToArray();

    public static IReadOnlyList<Sprite> Sample(IReadOnlyList<Sprite> source, IReadOnlyList<Sprite> target, string type, string direction, double progress, int seed = 1)
    {
        if (type == "none" || progress >= 1) return target;
        double p = Math.Clamp(progress, 0, 1), t = p * p * (3 - 2 * p);
        var result = new List<Sprite>();
        double Mix(double a, double b) => a + (b - a) * t;
        Sprite Clip(Sprite s, double x, double y, double w, double h)
        {
            double left = Math.Max(x, s.ClipX), top = Math.Max(y, s.ClipY);
            return s with { ClipX = left, ClipY = top, ClipWidth = Math.Max(0, Math.Min(x + w, s.ClipX + s.ClipWidth) - left), ClipHeight = Math.Max(0, Math.Min(y + h, s.ClipY + s.ClipHeight) - top) };
        }
        if (type == "morph")
        {
            var matched = new HashSet<Sprite>();
            foreach (var b in target)
            {
                var same = b.Key is null ? [] : source.Where(a => a.Key == b.Key).ToArray();
                if (same.Length == 0) { result.Add(b with { Alpha = t }); continue; }
                foreach (var a in same)
                {
                    matched.Add(a);
                    var moved = a with { X = Mix(a.X, b.X), Y = Mix(a.Y, b.Y), Width = Mix(a.Width, b.Width), Height = Mix(a.Height, b.Height), Z = b.Z };
                    result.Add(moved with { Alpha = same.Length == 1 && a.Media == b.Media ? Mix(a.Alpha, 1) : a.Alpha * (1 - t) });
                }
                if (same.Length != 1 || same[0].Media != b.Media)
                    result.Add(b with { X = Mix(same[0].X, b.X), Y = Mix(same[0].Y, b.Y), Width = Mix(same[0].Width, b.Width), Height = Mix(same[0].Height, b.Height), Alpha = t });
            }
            result.AddRange(source.Where(a => !matched.Contains(a)).Select(a => a with { Alpha = a.Alpha * (1 - t) }));
            return result.OrderBy(s => s.Z).ToArray();
        }
        if (type == "slide")
        {
            double x = direction == "left" ? -1 : direction == "right" ? 1 : 0, y = direction == "up" ? -1 : direction == "down" ? 1 : 0;
            result.AddRange(source.Select(a => a with { X = a.X + x * t, Y = a.Y + y * t }));
            result.AddRange(target.Select(b => b with { X = b.X - x * (1 - t), Y = b.Y - y * (1 - t) }));
        }
        else if (type == "wipe")
        {
            double x = direction == "left" ? 1 - t : 0, y = direction == "up" ? 1 - t : 0;
            bool horizontal = direction is "left" or "right";
            result.AddRange(source.Select(a => horizontal ? Clip(a, x > 0 ? 0 : t, 0, 1 - t, 1) : Clip(a, 0, y > 0 ? 0 : t, 1, 1 - t)));
            result.AddRange(target.Select(b => Clip(b, x, y, horizontal ? t : 1, horizontal ? 1 : t)));
        }
        else if (type == "random_lines")
            for (int strip = 0; strip < 32; strip++)
            {
                int order = (strip * 13 + (seed & 31)) & 31;
                foreach (var s in t * 32 >= order + 1 ? target : source)
                    result.Add(direction == "vertical" ? Clip(s, strip / 32d, 0, 1 / 32d, 1) : Clip(s, 0, strip / 32d, 1, 1 / 32d));
            }
        else { result.AddRange(source.Select(a => a with { Alpha = a.Alpha * (1 - t) })); result.AddRange(target.Select(b => b with { Alpha = t })); }
        return result;
    }
}
