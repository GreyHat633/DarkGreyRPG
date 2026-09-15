using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Shared selection and editor-only names. Runtime layer objects remain unchanged.</summary>
internal sealed class ScreenLayerEditorState
{
    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<string, ScreenLayerEditorState>> Hosts = new();
    private Dictionary<string, string[]> _snapshots = new();
    private string? _path;
    public int Selection { get; private set; }
    public event EventHandler? Changed;

    public static ScreenLayerEditorState For(CanonicalNodeInspectorViewModel inspector, string? root)
    {
        var nodes = Hosts.GetOrCreateValue(inspector.Host);
        if (!nodes.TryGetValue(inspector.NodeId, out var state)) nodes[inspector.NodeId] = state = new();
        if (root is not null && state._path is null)
        {
            var key = Hash(inspector.Host.AuthoringResourceKey + "\n" + inspector.NodeId);
            state._path = Path.Combine(root, "resources", "editor", "screen-layers", key + ".json");
            try
            {
                if (File.Exists(state._path))
                    state._snapshots = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(state._path)) ?? new();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        }
        return state;
    }

    public void Select(int index)
    {
        if (Selection == index) return;
        Selection = index; Changed?.Invoke(this, EventArgs.Empty);
    }

    public string[] Names(JsonArray layers, string? root)
    {
        if (_snapshots.TryGetValue(Key(layers), out var names) && names.Length == layers.Count) return names.ToArray();
        return layers.Select((layer, index) => DefaultName(layer, root, index)).ToArray();
    }

    public void Store(JsonArray layers, string[] names, bool notify = false)
    {
        _snapshots[Key(layers)] = names.ToArray();
        if (_path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            new AtomicFileWriter().Write(_path, JsonSerializer.Serialize(_snapshots));
        }
        if (notify) Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Key(JsonArray layers) => Hash(layers.ToJsonString());
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string DefaultName(JsonNode? layer, string? root, int index)
    {
        if (root is not null && layer?["media_ref"]?.GetValue<string>() is { } media)
        {
            var path = Path.Combine(root, "resources", "media_metadata", Path.GetFileNameWithoutExtension(media) + ".json");
            try
            {
                if (File.Exists(path))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    if (document.RootElement.TryGetProperty("OriginalName", out var name) && name.GetString() is { Length: > 0 } text)
                        return Path.GetFileNameWithoutExtension(text);
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        }
        return $"图片 {index + 1}";
    }
}
