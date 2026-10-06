using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>A flat container of complete Story members. It has no persistent Group identity.</summary>
public sealed class DgrsGroupManifest
{
    public const string GraphPath = "resources/group-connections.json";
    [JsonPropertyName("format")] public string Format { get; init; } = "dgrs.g";
    [JsonPropertyName("format_version")] public int FormatVersion { get; init; } = 2;
    [JsonPropertyName("identity_format")] public string IdentityFormat { get; init; } = "story-uid-v1";
    [JsonPropertyName("display_name")] public string DisplayName { get; init; } = string.Empty;
    [JsonPropertyName("connections")] public string Connections { get; init; } = GraphPath;
    [JsonPropertyName("members")] public IReadOnlyList<StoryPackageManifest> Members { get; init; } = [];

    public void Validate()
    {
        if (Format != "dgrs.g" || FormatVersion != 2 || IdentityFormat != "story-uid-v1"
            || string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 128 || Connections != GraphPath
            || Members is null || Members.Count is < 2 or > 4096 || Members.Any(member => member is null)
            || Members.Select(member => member.StoryId).Distinct(StringComparer.Ordinal).Count() != Members.Count)
            throw new StoryPackageException("Invalid current Story Group container manifest.");
        foreach (var member in Members)
        {
            StoryPackageManifest.Validate(member);
            if (member.RequiredResources.StoryLogicGraph is not null)
                throw new StoryPackageException("Group connections must be declared once at the container root.");
        }
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static DgrsGroupManifest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var fields = new[] { "format", "format_version", "identity_format", "display_name", "connections", "members" };
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != fields.Length
                || !fields.ToHashSet(StringComparer.Ordinal).SetEquals(root.EnumerateObject().Select(field => field.Name)))
                throw new StoryPackageException("Group manifest fields are missing, duplicate or unknown.");
            // Parse every member with the strict single-member parser; defaults
            // must not accept missing version/identity fields inside a Group.
            var members = root.GetProperty("members").EnumerateArray().Select(member => StoryPackageManifest.Parse(member.GetRawText())).ToArray();
            var result = new DgrsGroupManifest
            {
                Format = root.GetProperty("format").GetString()!, FormatVersion = root.GetProperty("format_version").GetInt32(),
                IdentityFormat = root.GetProperty("identity_format").GetString()!, DisplayName = root.GetProperty("display_name").GetString()!,
                Connections = root.GetProperty("connections").GetString()!, Members = members,
            };
            result.Validate();
            return result;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException)
        { throw new StoryPackageException("Invalid Group manifest JSON.", exception); }
    }
}

public sealed record DgrsGroupValidationResult(string PackagePath, DgrsGroupManifest Manifest,
    CanonicalStoryLogicGraph Connections, IReadOnlyList<string> Entries);

public static class DgrsGroupPackageValidator
{
    public static DgrsGroupValidationResult Validate(string packagePath)
    {
        try
        {
            using var stream = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entries = DgrsPackageValidator.IndexEntries(archive);
            if (!entries.ContainsKey("manifest.json") || !entries.ContainsKey("project.json") || !entries.ContainsKey(DgrsGroupManifest.GraphPath))
                throw new StoryPackageException("Group container root entries are missing.");
            var manifest = DgrsGroupManifest.Parse(DgrsPackageValidator.ReadText(entries["manifest.json"]));
            var declared = manifest.Members.SelectMany(member => DgrsPackageValidator.RequiredPaths(member.RequiredResources))
                .Concat(new[] { "manifest.json", "project.json", manifest.Connections }).ToHashSet(StringComparer.Ordinal);
            if (!declared.SetEquals(entries.Keys)) throw new StoryPackageException("Group contains missing or undeclared entries.");
            using var project = JsonDocument.Parse(DgrsPackageValidator.ReadText(entries["project.json"]));
            var root = project.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema_version", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 3
                || !root.TryGetProperty("identity_format", out var identity) || identity.ValueKind != JsonValueKind.String || identity.GetString() != "story-uid-v1")
                throw new StoryPackageException("Group project requires the current identity format.");
            var definitions = new Dictionary<(DgrResourceKind, string), byte[]>();
            var graphIssues = new List<StoryPackageGraphIssue>();
            foreach (var member in manifest.Members)
            {
                var subset = DgrsPackageValidator.RequiredPaths(member.RequiredResources).Distinct(StringComparer.Ordinal)
                    .ToDictionary(path => path, path => entries[path], StringComparer.Ordinal);
                try { DgrsPackageValidator.ValidateMember(subset, member); }
                catch (StoryPackageException exception) when (exception.GraphIssues.Count != 0)
                {
                    graphIssues.AddRange(exception.GraphIssues);
                    continue;
                }
                var required = member.RequiredResources;
                foreach (var (kind, paths) in new[]
                {
                    (DgrResourceKind.Story, required.CanonicalStories), (DgrResourceKind.Actor, required.Actors),
                    (DgrResourceKind.Item, required.Items), (DgrResourceKind.ItemGroup, required.ItemGroups),
                    (DgrResourceKind.Session, required.Sessions), (DgrResourceKind.Task, required.Tasks),
                })
                foreach (var path in paths)
                {
                    var identityKey = (kind, OfflineDgrsPackageReader.Identity(kind, Encoding.UTF8.GetBytes(DgrsPackageValidator.ReadText(entries[path])), path).Id);
                    using var input = entries[path].Open();
                    var fingerprint = SHA256.HashData(input);
                    if (definitions.TryGetValue(identityKey, out var existing) && !existing.AsSpan().SequenceEqual(fingerprint))
                        throw new StoryPackageException("Group members disagree on shared resource bytes: " + identityKey);
                    definitions[identityKey] = fingerprint;
                }
            }
            if (graphIssues.Count != 0) throw new StoryPackageException(graphIssues);
            using var graphJson = JsonDocument.Parse(DgrsPackageValidator.ReadText(entries[manifest.Connections]));
            var graph = CanonicalStoryLogicGraphRepository.Parse(graphJson.RootElement);
            var stories = manifest.Members.ToDictionary(member => member.StoryId,
                member => GraphResourceEnvelopeSerializer.Deserialize(DgrsPackageValidator.ReadText(entries[member.RequiredResources.Story])), StringComparer.Ordinal);
            CanonicalStoryLogicGraphRepository.ValidateDetached(graph, stories);
            var groups = StoryGroupCatalog.Derive(stories.Keys, graph);
            if (groups.Groups.Count != 1 || groups.Singles.Count != 0)
                throw new StoryPackageException("Group members must form one complete connected component.");
            return new(Path.GetFullPath(packagePath), manifest, graph, entries.Keys.Order(StringComparer.Ordinal).ToArray());
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or ArgumentException or InvalidOperationException or CanonicalStoryLogicGraphRepositoryException)
        { throw new StoryPackageException("Could not validate complete Story Group container.", exception); }
    }
}

public sealed class DgrsGroupPackageExporter(string projectDirectory)
{
    public StoryGroupCatalog Groups() => Compose(Path.GetFullPath(projectDirectory)).Groups;

    private static (StoryGroupCatalog Groups, CanonicalStoryLogicGraph Graph, IReadOnlyDictionary<string, OfflineDgrsPackage> Providers) Compose(string root)
    {
        var store = new CanonicalProjectGraphStore(root);
        var catalog = OfflineProviderCatalog.Load(root);
        if (catalog.Diagnostics.Count != 0) throw new StoryPackageException(string.Join("; ", catalog.Diagnostics.Select(issue => issue.Message)));
        var providers = catalog.Providers.ToDictionary(package => package.Manifest.StoryId, StringComparer.Ordinal);
        var ids = store.Stories.List().Select(story => story.Id).Concat(providers.Keys).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new StoryPackageException("Native and referenced Story identities overlap.");
        var graph = new CanonicalStoryLogicGraph(2, store.StoryLogicGraph.Load().Connections
            .Concat(catalog.Providers.SelectMany(provider => provider.ContainerConnections.Connections)).Distinct().ToArray());
        var groups = StoryGroupCatalog.Derive(ids, graph, new StoryGroupNameStore(root).Load());
        foreach (var source in catalog.Providers.Where(provider => provider.GroupDisplayName is not null).DistinctBy(provider => provider.PackagePath))
            if (groups.ByStory.TryGetValue(source.Manifest.StoryId, out var group)
                && group.Members.ToHashSet(StringComparer.Ordinal).SetEquals(source.ContainerMembers))
                groups = groups.Rename(group.Key, source.GroupDisplayName!);
        return (groups, graph, providers);
    }

    public DgrsGroupValidationResult Build(string memberStoryId, string outputFile, string producerVersion)
    {
        _ = StoryUid.Parse(memberStoryId);
        var root = Path.GetFullPath(projectDirectory);
        var sourceStamp = OfflineProjectStateStamp.Capture(root);
        var (groups, graph, providers) = Compose(root);
        if (!groups.ByStory.TryGetValue(memberStoryId, out var group)) throw new StoryPackageException("Selected Story does not belong to a Group.");
        var target = Path.GetFullPath(outputFile);
        if (!target.EndsWith(".dgrs.g", StringComparison.OrdinalIgnoreCase)) throw new StoryPackageException("Group output must use .dgrs.g.");
        var parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".group-" + Guid.NewGuid().ToString("N"));
        var flat = Path.Combine(staging, "flat");
        var temporary = Path.Combine(staging, "candidate.dgrs.g");
        Directory.CreateDirectory(flat);
        try
        {
            var members = new List<StoryPackageManifest>();
            byte[]? projectBytes = File.Exists(Path.Combine(root, "project.json")) ? File.ReadAllBytes(Path.Combine(root, "project.json")) : null;
            foreach (var uid in group.Members)
            {
                var part = Path.Combine(staging, uid);
                StoryPackageManifest sourceManifest;
                Func<string, byte[]> read;
                if (providers.TryGetValue(uid, out var provider))
                {
                    sourceManifest = provider.Manifest; read = provider.GetEntry;
                }
                else
                {
                    sourceManifest = new StoryPackageExporter(root).Build(uid, part, producerVersion).Manifest;
                    read = path => File.ReadAllBytes(Path.Combine(part, path));
                }
                projectBytes ??= read("project.json");
                var json = JsonNode.Parse(sourceManifest.ToJson())!;
                json["required_resources"]!.AsObject().Remove("story_logic_graph");
                var member = StoryPackageManifest.Parse(json.ToJsonString());
                members.Add(member);
                foreach (var path in DgrsPackageValidator.RequiredPaths(member.RequiredResources).Distinct(StringComparer.Ordinal))
                {
                    var bytes = read(path);
                    var destination = Path.Combine(flat, path);
                    if (File.Exists(destination))
                    {
                        if (!bytes.AsSpan().SequenceEqual(File.ReadAllBytes(destination))) throw new StoryPackageException("Shared Group resource bytes differ: " + path);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.WriteAllBytes(destination, bytes);
                }
            }
            File.WriteAllBytes(Path.Combine(flat, "project.json"), projectBytes!);
            var manifest = new DgrsGroupManifest { DisplayName = group.DisplayName, Members = members };
            File.WriteAllText(Path.Combine(flat, "manifest.json"), manifest.ToJson());
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(flat, manifest.Connections))!);
            File.WriteAllText(Path.Combine(flat, manifest.Connections), JsonSerializer.Serialize(new CanonicalStoryLogicGraph(2,
                graph.Connections.Where(edge => group.Members.Contains(edge.SourceStoryId)).ToArray())));
            DgrsStoryPackageExporter.WriteArchive(flat, temporary);
            var result = DgrsGroupPackageValidator.Validate(temporary);
            if (OfflineProjectStateStamp.Capture(root) != sourceStamp)
                throw new StoryPackageException("Project changed during Group export; previous output was preserved.");
            if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            return result with { PackagePath = target };
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }
}
