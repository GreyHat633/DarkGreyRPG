using System.Text.Json;
using S = DarkGreyRPG.Studio.Core.Graphs.Definitions.ScreenTransitionPreview.Sprite;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public sealed record ScreenLayerEffect(string Type, string Direction, double Duration)
{
    public static readonly ScreenLayerEffect None = new("none", "left", 0);
    public static ScreenLayerEffect Read(JsonElement value) => new(value.GetProperty("type").GetString()!, value.GetProperty("direction").GetString()!, value.GetProperty("duration").GetDouble());
}

/// <summary>Appearance/disappearance belongs to the image; matching images persist.</summary>
public static class ScreenLayerAnimation
{
    private static S[] Matches(IReadOnlyList<S> source, S target) => target.Key is null ? [] : source.Where(s => s.Key == target.Key && s.Alpha > 0 && s.ClipWidth > 0 && s.ClipHeight > 0).ToArray();
    public static double Duration(IReadOnlyList<S> source, IReadOnlyList<S> target, ScreenLayerEffect fallback)
        => target.Select(s => (Matches(source, s).Length > 0 ? ScreenAnimationSequence.Morph(s, fallback) : 0) + ScreenAnimationSequence.Steps(s, fallback).Sum(a => a.Delay + a.Duration)).DefaultIfEmpty(0).Max();

    public static IReadOnlyList<S> Sample(IReadOnlyList<S> source, IReadOnlyList<S> target, ScreenLayerEffect fallback, double seconds, int seed = 1)
    {
        var result = new List<S>();
        foreach (var b in target)
        {
            var same = Matches(source, b);
            double morph = same.Length > 0 ? ScreenAnimationSequence.Morph(b, fallback) : 0;
            if (seconds < morph) { result.AddRange(ScreenTransitionPreview.Sample(same, [b], "morph", "left", seconds / morph, seed)); continue; }
            double elapsed = Math.Max(0, seconds - morph);
            var steps = ScreenAnimationSequence.Steps(b, fallback);
            bool visible = steps.Count == 0 || steps[0].Kind != "enter";
            bool sampling = false;
            foreach (var step in steps)
            {
                if (elapsed < step.Delay) break;
                elapsed -= step.Delay;
                if (elapsed < step.Duration)
                {
                    // Repeated exits cannot resurrect an already hidden image.
                    if (step.Kind == "enter" || visible) Add(result, b, step.Effect, elapsed, step.Kind == "enter", seed);
                    sampling = true; break;
                }
                elapsed -= step.Duration; visible = step.Kind == "enter";
            }
            if (!sampling && visible) result.Add(b);
        }
        return result.OrderBy(s => s.Z).ToArray();
    }

    private static void Add(List<S> result, S s, ScreenLayerEffect effect, double seconds, bool entering, int seed)
    {
        double p = effect.Duration <= 0 ? 1 : Math.Clamp(seconds / effect.Duration, 0, 1), t = p * p * (3 - 2 * p);
        if (effect.Type == "none" || p >= 1) { if (entering) result.Add(s); return; }
        double visible = entering ? t : 1 - t;
        if (effect.Type == "slide")
        {
            double x = effect.Direction == "left" ? -s.Width : effect.Direction == "right" ? 1 : s.X;
            double y = effect.Direction == "up" ? -s.Height : effect.Direction == "down" ? 1 : s.Y;
            result.Add(s with { X = s.X + (x - s.X) * (1 - visible), Y = s.Y + (y - s.Y) * (1 - visible) });
        }
        else if (effect.Type == "wipe")
        {
            bool horizontal = effect.Direction is "left" or "right";
            result.Add(Clip(s, s.X + (effect.Direction == "right" ? s.Width * (1 - visible) : 0), s.Y + (effect.Direction == "down" ? s.Height * (1 - visible) : 0), s.Width * (horizontal ? visible : 1), s.Height * (horizontal ? 1 : visible)));
        }
        else if (effect.Type == "random_lines")
        {
            for (int strip = 0; strip < 32; strip++)
                if (visible * 32 >= ((strip * 13 + (seed & 31)) & 31) + 1)
                    result.Add(effect.Direction == "vertical" ? Clip(s, s.X + s.Width * strip / 32, s.Y, s.Width / 32, s.Height) : Clip(s, s.X, s.Y + s.Height * strip / 32, s.Width, s.Height / 32));
        }
        else result.Add(s with { Alpha = s.Alpha * visible });
    }

    private static S Clip(S s, double x, double y, double w, double h)
    {
        double left = Math.Max(s.ClipX, x), top = Math.Max(s.ClipY, y);
        return s with { ClipX = left, ClipY = top, ClipWidth = Math.Max(0, Math.Min(s.ClipX + s.ClipWidth, x + w) - left), ClipHeight = Math.Max(0, Math.Min(s.ClipY + s.ClipHeight, y + h) - top) };
    }

    public static ScreenLayerEffect LegacyEntry(ScreenLayerEffect effect) => effect.Type is "slide" or "wipe"
        ? effect with { Direction = effect.Direction switch { "left" => "right", "right" => "left", "up" => "down", "down" => "up", _ => effect.Direction } } : effect;
}
