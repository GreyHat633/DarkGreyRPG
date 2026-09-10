using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Packaging;

public sealed record OfflinePackageIdentity(string PackageId, string PackageVersion)
{
    public override string ToString() => $"{PackageId}@{PackageVersion}";
}

/// <summary>An immutable provider definition retained in a read-only package catalog.</summary>
public sealed record OfflineProviderResource(
    DgrResourceKind Kind,
    string Id,
    string DisplayName,
    string DefinitionJson,
    OfflinePackageIdentity PackageIdentity,
    string ProjectOriginCode,
    string Fingerprint,
    bool IsReadOnly = true)
{
    public string SourcePath { get; init; } = string.Empty;
    public string StoryId { get; init; } = string.Empty;
    public string PackagePath { get; init; } = string.Empty;
    public string Namespace => DgrResourceId.IsFullId(Id) ? DgrResourceId.Namespace(Id) : string.Empty;
    public string FullId => Id;
    public string ProviderPackageId => PackageIdentity.PackageId;
    public byte[] DefinitionBytes => Encoding.UTF8.GetBytes(DefinitionJson);
    public GraphResourceEnvelope? ReadGraphDefinition()
        => Kind is DgrResourceKind.Story or DgrResourceKind.Session or DgrResourceKind.Task
            ? GraphResourceEnvelopeSerializer.Deserialize(DefinitionJson)
            : null;
    public ActorResource? ReadActorDefinition()
        => Kind == DgrResourceKind.Actor ? ActorSerializer.Deserialize(DefinitionJson) : null;
    public ItemResource? ReadItemDefinition()
        => Kind is DgrResourceKind.Item or DgrResourceKind.ItemGroup ? ItemSerializer.Deserialize(DefinitionJson) : null;
}

public sealed record OfflineProviderDiagnostic(string Code, string Message, string PackagePath);

public sealed record OfflineDgrsPackage(
    string PackagePath,
    StoryPackageManifest Manifest,
    OfflinePackageIdentity Identity,
    string ProjectOriginCode,
    string Fingerprint,
    IReadOnlyDictionary<string, byte[]> Entries,
    IReadOnlyList<OfflineProviderResource> Resources)
{
    internal byte[] ArchiveBytes { get; init; } = [];
    public byte[] GetEntry(string relativePath)
        => Entries.TryGetValue(relativePath, out var bytes)
            ? bytes.ToArray()
            : throw new StoryPackageException($"DGRS entry '{relativePath}' is missing.");
}

/// <summary>Reads and validates a DGRS archive without extracting it.</summary>
public static class OfflineDgrsPackageReader
{
    public static OfflineDgrsPackage Read(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        using var lockedArchive = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var validation = DgrsPackageValidator.Validate(packagePath);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var stream = File.OpenRead(validation.PackagePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                using var input = entry.Open();
                using var output = new MemoryStream();
                input.CopyTo(output);
                entries[path] = output.ToArray();
            }
        }

        var manifest = validation.Manifest;
        var origin = ReadProjectOrigin(entries.TryGetValue("project.json", out var project) ? project : null, manifest.PackageId);
        var fingerprint = ComputeFingerprint(manifest, entries);
        var resources = ReadResources(manifest, entries, origin, fingerprint, validation.PackagePath);
        return new OfflineDgrsPackage(
            validation.PackagePath,
            manifest,
            new OfflinePackageIdentity(manifest.PackageId, manifest.PackageVersion),
            origin,
            fingerprint,
            new ReadOnlyDictionary<string, byte[]>(entries.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal)),
            new ReadOnlyCollection<OfflineProviderResource>(resources)) { ArchiveBytes = File.ReadAllBytes(packagePath) };
    }

    public static string ComputeFingerprint(StoryPackageManifest manifest, IReadOnlyDictionary<string, byte[]> entries)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(entries);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes("DGR-PACKAGE-CONTENT-FINGERPRINT-V1\0"));
        AddText(hash, manifest.Format ?? string.Empty);
        AddText(hash, manifest.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddText(hash, manifest.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddText(hash, manifest.StorySchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var records = RequiredRecords(manifest).OrderBy(value => value.Role, StringComparer.Ordinal).ThenBy(value => value.Path, StringComparer.Ordinal);
        Span<byte> recordLength = stackalloc byte[8];
        foreach (var record in records)
        {
            if (!entries.TryGetValue(record.Path, out var content))
                throw new StoryPackageException($"Missing authoritative bytes for Story Package fingerprint entry '{record.Role}' at '{record.Path}'.");
            AddBytes(hash, Encoding.UTF8.GetBytes(record.Role));
            AddBytes(hash, Encoding.UTF8.GetBytes(record.Path));
            BinaryPrimitives.WriteInt64BigEndian(recordLength, content.LongLength);
            hash.AppendData(recordLength);
            hash.AppendData(content);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ReadProjectOrigin(byte[]? bytes, string fallback)
    {
        if (bytes is null) throw new StoryPackageException("DGRS project.json is missing.");
        using var document = JsonDocument.Parse(bytes);
        foreach (var field in new[] { "project_origin_code", "origin_code", "id" })
        {
            if (!document.RootElement.TryGetProperty(field, out var value)) continue;
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new StoryPackageException($"Package project field '{field}' must be a nonempty string.");
            return value.GetString()!;
        }
        throw new StoryPackageException("Package Project identity is missing.");
    }

    private static List<OfflineProviderResource> ReadResources(
        StoryPackageManifest manifest,
        IReadOnlyDictionary<string, byte[]> entries,
        string origin,
        string fingerprint,
        string packagePath)
    {
        var result = new List<OfflineProviderResource>();
        Add(DgrResourceKind.Story, manifest.RequiredResources.Story, "story");
        AddAll(DgrResourceKind.Actor, manifest.RequiredResources.Actors, "actor");
        AddAll(DgrResourceKind.Item, manifest.RequiredResources.Items, "item");
        AddAll(DgrResourceKind.ItemGroup, manifest.RequiredResources.ItemGroups, "item_group");
        AddAll(DgrResourceKind.Story, manifest.RequiredResources.CanonicalStories, "canonical_story");
        AddAll(DgrResourceKind.Session, manifest.RequiredResources.Sessions, "session");
        AddAll(DgrResourceKind.Task, manifest.RequiredResources.Tasks, "task");
        foreach (var duplicate in result.GroupBy(resource => (resource.Kind, resource.Id)))
        {
            if (duplicate.Select(resource => resource.SourcePath).Distinct(StringComparer.Ordinal).Count() > 1)
                throw new StoryPackageException($"DGRS contains duplicate {duplicate.Key.Kind} resource ID '{duplicate.Key.Id}'.");
        }
        return result.GroupBy(resource => (resource.Kind, resource.Id)).Select(group => group.First()).ToList();

        void AddAll(DgrResourceKind kind, IEnumerable<string> paths, string role)
        { foreach (var path in paths) Add(kind, path, role); }

        void Add(DgrResourceKind kind, string path, string role)
        {
            if (!entries.TryGetValue(path, out var bytes)) throw new StoryPackageException($"DGRS entry '{path}' is missing.");
            var (id, display) = Identity(kind, bytes, path);
            result.Add(new OfflineProviderResource(kind, id, display, Encoding.UTF8.GetString(bytes),
                new OfflinePackageIdentity(manifest.PackageId, manifest.PackageVersion), origin, fingerprint)
            { SourcePath = path, PackagePath = packagePath, StoryId = manifest.StoryId });
        }
    }

    private static (string Id, string DisplayName) Identity(DgrResourceKind kind, byte[] bytes, string sourcePath)
    {
        var json = Encoding.UTF8.GetString(bytes);
        try
        {
            switch (kind)
            {
                case DgrResourceKind.Story when sourcePath.StartsWith("resources/canonical/", StringComparison.Ordinal):
                case DgrResourceKind.Session:
                case DgrResourceKind.Task:
                    var envelope = GraphResourceEnvelopeSerializer.Deserialize(json);
                    return (envelope.Id, envelope.DisplayName);
                case DgrResourceKind.Story:
                    var story = StorySerializer.Deserialize(json);
                    return (story.Id, story.DisplayName);
                case DgrResourceKind.Actor:
                    var actor = ActorSerializer.Deserialize(json);
                    return (actor.Id, actor.DisplayName);
                case DgrResourceKind.Item:
                case DgrResourceKind.ItemGroup:
                    var item = ItemSerializer.Deserialize(json);
                    if (kind == DgrResourceKind.Item && item is not IndividualItemResource
                        || kind == DgrResourceKind.ItemGroup && item is not CollectiveItemResource)
                        throw new StoryPackageException($"DGRS {kind} definition '{sourcePath}' has the wrong item type.");
                    return (item is IndividualItemResource individual ? individual.ItemId : ((CollectiveItemResource)item).GroupId, item.DisplayName);
            }
        }
        catch (Exception exception) when (exception is ActorValidationException or ActorDataException or ItemValidationException or ItemDataException or StoryValidationException or StoryDataException or GraphResourceEnvelopeException)
        { throw new StoryPackageException($"DGRS {kind} definition '{sourcePath}' is invalid.", exception); }
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var bareId) ? bareId.GetString() : null;
        var display = root.TryGetProperty("display_name", out var name) ? name.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(display)) throw new StoryPackageException($"DGRS {kind} definition '{sourcePath}' has no identity.");
        return (id!, display!);
    }

    private static IEnumerable<(string Role, string Path)> RequiredRecords(StoryPackageManifest manifest)
    {
        yield return ("project", "project.json");
        yield return ("story", manifest.RequiredResources.Story);
        foreach (var path in manifest.RequiredResources.Actors) yield return ("actor", path);
        foreach (var path in manifest.RequiredResources.Items) yield return ("item", path);
        foreach (var path in manifest.RequiredResources.ItemGroups) yield return ("item_group", path);
        foreach (var path in manifest.RequiredResources.Dialogues) yield return ("dialogue", path);
        foreach (var path in manifest.RequiredResources.Quests) yield return ("quest", path);
        foreach (var path in manifest.RequiredResources.CanonicalStories) yield return ("canonical_story", path);
        foreach (var path in manifest.RequiredResources.CanonicalMemberships) yield return ("canonical_membership", path);
        foreach (var path in manifest.RequiredResources.Sessions) yield return ("session", path);
        foreach (var path in manifest.RequiredResources.Tasks) yield return ("task", path);
        if (manifest.RequiredResources.StoryLogicGraph is not null) yield return ("story_logic_graph", manifest.RequiredResources.StoryLogicGraph);
    }

    private static void AddText(IncrementalHash hash, string value) => AddBytes(hash, Encoding.UTF8.GetBytes(value));
    private static void AddBytes(IncrementalHash hash, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}

/// <summary>Read-only catalog over project-local references/*.dgrs files.</summary>
public sealed class OfflineProviderCatalog
{
    private OfflineProviderCatalog(string projectDirectory, IReadOnlyList<OfflineDgrsPackage> providers, IReadOnlyList<OfflineProviderDiagnostic> diagnostics)
    {
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        Providers = providers;
        Diagnostics = diagnostics;
        Resources = new ReadOnlyCollection<OfflineProviderResource>(providers.SelectMany(provider => provider.Resources).ToList());
    }

    public string ProjectDirectory { get; }
    public IReadOnlyList<OfflineDgrsPackage> Providers { get; }
    public IReadOnlyList<OfflineProviderResource> Resources { get; }
    public IReadOnlyList<OfflineProviderDiagnostic> Diagnostics { get; }
    public static OfflineProviderCatalog Load(string projectDirectory)
    {
        var root = Path.GetFullPath(projectDirectory);
        var providers = new List<OfflineDgrsPackage>();
        var diagnostics = new List<OfflineProviderDiagnostic>();
        var references = Path.Combine(root, "references");
        if (Directory.Exists(references) && (File.GetAttributes(references) & FileAttributes.ReparsePoint) != 0)
        {
            diagnostics.Add(new("provider.path.unsafe", "references must be a physical Project directory.", references));
            return new OfflineProviderCatalog(root, providers, diagnostics);
        }
        if (Directory.Exists(references))
            foreach (var path in Directory.EnumerateFiles(references, "*.dgrs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new StoryPackageException("Referenced package must be a physical project-local file.");
                    providers.Add(OfflineDgrsPackageReader.Read(path));
                }
                catch (Exception exception) when (exception is StoryPackageException or IOException or UnauthorizedAccessException or InvalidDataException)
                { diagnostics.Add(new("provider.invalid", exception.Message, Path.GetFullPath(path))); }
            }
        var duplicate = providers.GroupBy(provider => provider.Identity.PackageId, StringComparer.Ordinal).Where(group => group.Count() > 1);
        foreach (var group in duplicate) diagnostics.Add(new("provider.identity.duplicate", $"Package identity '{group.Key}' is referenced more than once.", string.Join(";", group.Select(item => item.PackagePath))));
        foreach (var group in providers.SelectMany(provider => provider.Resources).GroupBy(resource => new DgrResourceKey(resource.Kind, resource.Id)).Where(group => group.Count() > 1))
            diagnostics.Add(new("provider.resource.ambiguous", $"Ambiguous {group.Key.Kind} full ID '{group.Key.Id}' in multiple referenced packages.", string.Join(";", group.Select(resource => resource.PackagePath))));
        return new OfflineProviderCatalog(root, providers, diagnostics);
    }

    public OfflineProviderResource? Resolve(DgrResourceKind kind, string fullId)
    {
        var candidates = Resources.Where(resource => resource.Kind == kind && string.Equals(resource.Id, fullId, StringComparison.Ordinal)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    public IReadOnlyList<OfflineProviderResource> Find(DgrResourceKind kind) => Resources.Where(resource => resource.Kind == kind).ToArray();
}

/// <summary>Enumerates project-owned native content for the Studio source picker.</summary>
public sealed record OfflineNativeResource(DgrResourceKind Kind, string Id, string DisplayName, string DefinitionJson)
{
    public string Namespace => DgrResourceId.IsFullId(Id) ? DgrResourceId.Namespace(Id) : string.Empty;
    public bool IsOwned => true;
    public bool IsReadOnly => false;
}

public static class OfflineNativeContentCatalog
{
    public static IReadOnlyList<OfflineNativeResource> Load(string projectDirectory)
    {
        var root = Path.GetFullPath(projectDirectory);
        var result = new List<OfflineNativeResource>();
        var canonical = new CanonicalProjectGraphStore(root);
        AddGraphs(canonical.Stories, DgrResourceKind.Story);
        AddGraphs(canonical.Sessions, DgrResourceKind.Session);
        AddGraphs(canonical.Tasks, DgrResourceKind.Task);
        var actors = new ActorRepository(root);
        foreach (var info in actors.ListActors())
            result.Add(new(DgrResourceKind.Actor, info.Id, info.DisplayName, ActorSerializer.Serialize(actors.LoadActor(info.Id).ToResource(), ActorIdPolicy.ExistingResource)));
        var items = new ItemRepository(root);
        foreach (var info in items.ListItems()) result.Add(new(DgrResourceKind.Item, info.Id, info.DisplayName, ItemSerializer.Serialize(items.LoadItem(info.Id))));
        foreach (var info in items.ListGroups()) result.Add(new(DgrResourceKind.ItemGroup, info.Id, info.DisplayName, ItemSerializer.Serialize(items.LoadGroup(info.Id))));
        return result.OrderBy(item => item.Kind).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();

        void AddGraphs(GraphResourceRepository repository, DgrResourceKind kind)
        {
            foreach (var info in repository.List())
            {
                var envelope = repository.Load(info.Id);
                result.Add(new(kind, envelope.Id, envelope.DisplayName, envelope.ToJson()));
            }
        }
    }
}
