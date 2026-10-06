using System.Text.Json;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Editor-only names; this file cannot declare executable membership.</summary>
public sealed class StoryGroupNameStore
{
    private readonly IAtomicFileWriter _writer;
    public StoryGroupNameStore(string projectDirectory, IAtomicFileWriter? writer = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetFullPath(projectDirectory), "resources", "editor", "story-group-names.json");
        _writer = writer ?? new AtomicFileWriter();
    }
    public string Path { get; }
    public StoryGroupCatalog Load() => File.Exists(Path) ? Parse(File.ReadAllText(Path)) : StoryGroupCatalog.Empty;
    public void Save(StoryGroupCatalog catalog)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer.Write(Path, Serialize(catalog), staged => Parse(File.ReadAllText(staged)));
    }
    public static string Serialize(StoryGroupCatalog catalog) => JsonSerializer.Serialize(new
    {
        schema_version = 1,
        next_ordinal = catalog.NextOrdinal,
        names = catalog.Groups.Select(group => new { previous_members = group.Members, display_name = group.DisplayName })
    }, new JsonSerializerOptions { WriteIndented = true });

    public static StoryGroupCatalog Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireKeys(root, "schema_version", "next_ordinal", "names");
            if (root.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("故事组合名称格式不支持。");
            var groups = new List<StoryGroup>();
            foreach (var item in root.GetProperty("names").EnumerateArray())
            {
                RequireKeys(item, "previous_members", "display_name");
                groups.Add(new StoryGroup(item.GetProperty("previous_members").EnumerateArray().Select(member => member.GetString()!),
                    item.GetProperty("display_name").GetString()!));
            }
            return StoryGroupCatalog.Restore(groups, root.GetProperty("next_ordinal").GetInt32());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new InvalidDataException("故事组合名称快照无效。", exception);
        }
    }
    private static void RequireKeys(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("故事组合名称快照无效。");
        var keys = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (keys.Length != expected.Length || !keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidDataException("故事组合名称字段无效或重复。");
    }
}
