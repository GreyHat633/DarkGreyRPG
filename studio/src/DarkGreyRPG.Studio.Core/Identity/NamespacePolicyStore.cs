using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Project-side policy records Custom membership and the last applied global setting.</summary>
public static class NamespacePolicyStore
{
    public const string RelativePath = "resources/canonical/namespace_policy.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static NamespacePolicy? Load(string projectDirectory)
    {
        var path = Path.Combine(projectDirectory, RelativePath);
        return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
    }

    public static byte[] Encode(NamespacePolicy policy) => JsonSerializer.SerializeToUtf8Bytes(new PolicyDocument
    {
        GlobalNamespace = policy.GlobalNamespace,
        StoryOverrides = policy.StoryOverrides.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
    }, Options);

    public static NamespacePolicy Decode(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var fields = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length
            || !fields.ToHashSet(StringComparer.Ordinal).SetEquals(["schema_version", "global_namespace", "story_overrides"]))
            throw new InvalidDataException("Namespace policy requires exactly schema_version, global_namespace and story_overrides.");
        if (document.RootElement.TryGetProperty("story_overrides", out var overrides))
        {
            var ids = overrides.EnumerateObject().Select(property => property.Name).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidDataException("Duplicate Story Namespace overrides.");
        }
        var value = JsonSerializer.Deserialize<PolicyDocument>(bytes, Options)
            ?? throw new InvalidDataException("Namespace policy is missing.");
        if (value.SchemaVersion != 1) throw new InvalidDataException("Unsupported Namespace policy schema.");
        return new(value.GlobalNamespace, value.StoryOverrides);
    }

    private sealed class PolicyDocument
    {
        [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; } = 1;
        [JsonPropertyName("global_namespace")] public required string GlobalNamespace { get; init; }
        [JsonPropertyName("story_overrides")] public required Dictionary<string, string> StoryOverrides { get; init; }
    }
}
