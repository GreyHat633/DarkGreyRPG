using System.IO;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Views.Graph;

// Opt-in acceptance trace; disk writes run off the dispatcher and never scan resources.
internal static class SelectionDiagnostics
{
    private static readonly string? Path = Environment.GetEnvironmentVariable("DARKGREYRPG_STUDIO_SELECTION_TRACE");
    private static readonly object Gate = new();
    private static int _count;
    internal static void Record(bool marquee, string title, int before, int after, double milliseconds)
    {
        if (string.IsNullOrWhiteSpace(Path) || Interlocked.Increment(ref _count) > 2000) return;
        var line = JsonSerializer.Serialize(new { utc = DateTime.UtcNow, marquee, title, created = after - before, totalCreated = after, milliseconds });
        _ = Task.Run(() => { try { lock (Gate) File.AppendAllText(Path, line + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
    }
}
