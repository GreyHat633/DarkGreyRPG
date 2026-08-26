using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Stories;

/// <summary>Persists editor-only Project Graph positions outside Runtime Story resources.</summary>
public sealed class ProjectGraphLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public ProjectGraphLayoutStore(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        LayoutPath = Path.Combine(ProjectDirectory, "resources", "editor", "story-graph-layout.json");
    }

    public string ProjectDirectory { get; }
    public string LayoutPath { get; }

    public ProjectGraphLayout Load()
    {
        if (!File.Exists(LayoutPath)) return new();
        try
        {
            var layout = JsonSerializer.Deserialize<ProjectGraphLayout>(File.ReadAllText(LayoutPath), JsonOptions) ?? new();
            if (layout.SchemaVersion != ProjectGraphLayout.CurrentSchemaVersion) return new();
            layout.Nodes = layout.Nodes
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && double.IsFinite(pair.Value.X) && double.IsFinite(pair.Value.Y))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return layout;
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save(IReadOnlyDictionary<string, ProjectGraphNodeLayout> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var directory = Path.GetDirectoryName(LayoutPath)!;
        Directory.CreateDirectory(directory);
        var resource = new ProjectGraphLayout
        {
            Nodes = positions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        };
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(LayoutPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(resource, JsonOptions));
            File.Move(temporaryPath, LayoutPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

public sealed class ProjectGraphLayout
{
    public const int CurrentSchemaVersion = 1;
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonPropertyName("nodes")]
    public Dictionary<string, ProjectGraphNodeLayout> Nodes { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ProjectGraphNodeLayout
{
    [JsonPropertyName("x")]
    public double X { get; init; }
    [JsonPropertyName("y")]
    public double Y { get; init; }
}
