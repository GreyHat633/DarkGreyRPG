using System.Text.Json;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Only presentation state. Group members are always derived from connections.</summary>
public sealed record StoryGraphPresentation(string[] NavigationOrder, Dictionary<string, StoryGroupAppearance> Groups)
{
    public static StoryGraphPresentation Empty => new([], new(StringComparer.Ordinal));
}
public sealed record StoryGroupAppearance(string Color, bool Collapsed);

public sealed class StoryGraphPresentationStore(string projectDirectory)
{
    public string Path { get; } = System.IO.Path.Combine(projectDirectory, "resources", "editor", "story-graph-presentation.json");
    public StoryGraphPresentation Load()
    {
        if (!File.Exists(Path)) return StoryGraphPresentation.Empty;
        return JsonSerializer.Deserialize<StoryGraphPresentation>(File.ReadAllText(Path))
            ?? throw new InvalidDataException("故事图谱显示设置无效。");
    }
    public void Save(StoryGraphPresentation state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        new AtomicFileWriter().Write(Path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }),
            staged => JsonSerializer.Deserialize<StoryGraphPresentation>(File.ReadAllText(staged)));
    }
}
