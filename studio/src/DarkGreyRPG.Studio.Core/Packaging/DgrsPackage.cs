using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Writes one validated current DGRS archive and commits it only after reopen validation.</summary>
public sealed class DgrsStoryPackageExporter
{
    private static readonly DateTimeOffset StableEntryTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _projectDirectory;
    private readonly Action<string, string> _archiveWriter;

    public DgrsStoryPackageExporter(
        string projectDirectory,
        Action<string, string>? archiveWriter = null)
    {
        _projectDirectory = Path.GetFullPath(projectDirectory);
        _archiveWriter = archiveWriter ?? WriteArchive;
    }

    public DgrsPackageBuildResult Build(
        string storyId,
        string outputFile,
        string producerVersion = "1.0.0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerVersion);

        if (new DgrsGroupPackageExporter(_projectDirectory).Groups().ByStory.ContainsKey(storyId))
            throw new StoryPackageException("联动故事必须以完整 .dgrs.g 故事组导出。");

        var target = Path.GetFullPath(outputFile);
        if (!string.Equals(Path.GetExtension(target), ".dgrs", StringComparison.OrdinalIgnoreCase))
            throw new StoryPackageException("DGRS output must use the .dgrs extension.");
        var parent = Path.GetDirectoryName(target)
            ?? throw new StoryPackageException("DGRS output must have a parent directory.");
        Directory.CreateDirectory(parent);

        var transactionId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(parent, $".{Path.GetFileNameWithoutExtension(target)}.{transactionId}.staging");
        var temporary = Path.Combine(parent, $".{Path.GetFileName(target)}.{transactionId}.tmp.dgrs");

        try
        {
            var staged = new StoryPackageExporter(_projectDirectory)
                .Build(storyId, staging, producerVersion);
            _archiveWriter(staging, temporary);
            var validation = DgrsPackageValidator.Validate(temporary);
            Commit(temporary, target);
            return new DgrsPackageBuildResult(target, staged.Manifest, validation);
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or NotSupportedException)
        {
            throw new StoryPackageException($"Could not build DGRS package '{target}'.", exception);
        }
        finally
        {
            TryDeleteFile(temporary);
            TryDeleteDirectory(staging);
        }
    }

    public DgrsPackageBuildResult Export(
        string storyId,
        string outputFile,
        string producerVersion = "1.0.0")
        => Build(storyId, outputFile, producerVersion);

    internal static void WriteArchive(string sourceDirectory, string archivePath)
    {
        using var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);
        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(sourceDirectory, path), StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(sourceDirectory, source)
                .Replace(Path.DirectorySeparatorChar, '/');
            var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
            entry.LastWriteTime = StableEntryTimestamp;
            using var input = File.OpenRead(source);
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    private static void Commit(string temporary, string target)
    {
        if (File.Exists(target)) File.Replace(temporary, target, destinationBackupFileName: null);
        else File.Move(temporary, target);
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Reopens and validates the DGRS identity, path table, required entries, and canonical graph payload.</summary>
public static class DgrsPackageValidator
{
    public static DgrsValidationResult Validate(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var path = Path.GetFullPath(packagePath);
        if (!File.Exists(path)) throw new StoryPackageException($"DGRS package was not found: {path}");

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);
            var entries = IndexEntries(archive);
            if (!entries.TryGetValue("manifest.json", out var manifestEntry))
                throw new StoryPackageException("DGRS manifest.json is missing.");
            var manifest = StoryPackageManifest.Parse(ReadText(manifestEntry), $"{path}!/manifest.json");
            RequireEntry(entries, "project.json");
            foreach (var required in RequiredPaths(manifest.RequiredResources)) RequireEntry(entries, required);
            var declared = RequiredPaths(manifest.RequiredResources).Append("project.json").Append("manifest.json").ToHashSet(StringComparer.Ordinal);
            if (!declared.SetEquals(entries.Keys)) throw new StoryPackageException("DGRS contains undeclared entries.");
            using (var project = JsonDocument.Parse(ReadText(entries["project.json"])))
            {
                var root = project.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 3
                    || !root.TryGetProperty("identity_format", out var identity) || identity.ValueKind != JsonValueKind.String || identity.GetString() != "story-uid-v1")
                    throw new StoryPackageException("DGRS project requires the current identity format.");
            }
            ValidateMember(entries, manifest);
            return new DgrsValidationResult(
                path,
                manifest,
                entries.Keys.Order(StringComparer.Ordinal).ToArray());
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or UnauthorizedAccessException or NotSupportedException or JsonException or ArgumentException
            or InvalidOperationException or ActorRepositoryException)
        {
            throw new StoryPackageException($"Could not open DGRS package '{path}'.", exception);
        }
    }

    internal static void ValidateMember(IReadOnlyDictionary<string, ZipArchiveEntry> entries, StoryPackageManifest manifest)
    {
        ValidateCanonicalPayload(entries, manifest);
        var reachable = StoryPackageMedia.Collect(manifest.RequiredResources, name => ReadText(entries[name]));
        if (!reachable.SetEquals(manifest.RequiredResources.Media)) throw new StoryPackageException("媒体清单必须与资源实际引用一致。");
        if (entries.Keys.Any(name => name.StartsWith("media/", StringComparison.Ordinal) && !reachable.Contains(name))) throw new StoryPackageException("包中包含未声明或不可达的媒体。");
        foreach (var mediaRef in manifest.RequiredResources.Media)
        {
            using var input = entries[mediaRef].Open();
            using var bytes = new MemoryStream();
            if (entries[mediaRef].Length > 64L * 1024 * 1024) throw new StoryPackageException("运行时媒体过大。");
            byte[] buffer = new byte[81920]; int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) != 0) {
                if (bytes.Length + count > 64L * 1024 * 1024) throw new StoryPackageException("运行时媒体解压后过大。");
                bytes.Write(buffer, 0, count);
            }
            bytes.Position = 0;
            StoryPackageMedia.Validate(mediaRef, bytes);
        }
    }

    internal static Dictionary<string, ZipArchiveEntry> IndexEntries(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        if (archive.Entries.Count > 4096) throw new StoryPackageException("DGRS entry count exceeds 4096.");
        long total = 0;
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > 64L * 1024 * 1024 || (total += entry.Length) > 256L * 1024 * 1024)
                throw new StoryPackageException("DGRS uncompressed size exceeds its limit.");
            var path = NormalizeEntryPath(entry.FullName);
            if (!normalized.Add(path))
                throw new StoryPackageException($"DGRS contains duplicate normalized entry path '{path}'.");
            entries.Add(path, entry);
        }
        return entries;
    }

    private static string NormalizeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\')
            || path.StartsWith('/')
            || path.Contains(':'))
            throw new StoryPackageException($"DGRS contains unsafe entry path '{path}'.");
        var segments = path.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new StoryPackageException($"DGRS contains unsafe entry path '{path}'.");
        return string.Join('/', segments);
    }

    private static void RequireEntry(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string path)
    {
        if (!entries.ContainsKey(path))
            throw new StoryPackageException($"Required DGRS entry is missing: {path}");
    }

    internal static IEnumerable<string> RequiredPaths(StoryPackageRequiredResources required)
    {
        yield return required.Story;
        foreach (var path in required.Actors) yield return path;
        foreach (var path in required.Items) yield return path;
        foreach (var path in required.ItemGroups) yield return path;
        foreach (var path in required.Dialogues) yield return path;
        foreach (var path in required.Quests) yield return path;
        foreach (var path in required.CanonicalStories) yield return path;
        foreach (var path in required.CanonicalMemberships) yield return path;
        foreach (var path in required.Sessions) yield return path;
        foreach (var path in required.Tasks) yield return path;
        foreach (var path in required.Media) yield return path;
        if (required.StoryLogicGraph is not null) yield return required.StoryLogicGraph;
    }

    private static void ValidateCanonicalPayload(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        StoryPackageManifest manifest)
    {
        var membershipIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in manifest.RequiredResources.CanonicalMemberships)
        {
            var membership = CanonicalStoryMembershipSerializer.Deserialize(ReadText(entries[path]));
            if (!membershipIds.Add(membership.StoryId)) throw new StoryPackageException($"Duplicate Story membership ID '{membership.StoryId}'.");
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), membership.StoryId, StringComparison.Ordinal))
                throw new StoryPackageException($"Canonical membership identity does not match DGRS path '{path}'.");
        }

        var graphIssues = new List<StoryPackageGraphIssue>();
        ValidateGraphs(entries, manifest.RequiredResources.CanonicalStories, GraphResourceKind.Story, manifest, graphIssues);
        ValidateGraphs(entries, manifest.RequiredResources.Sessions, GraphResourceKind.Session, manifest, graphIssues);
        ValidateGraphs(entries, manifest.RequiredResources.Tasks, GraphResourceKind.Task, manifest, graphIssues);
        if (graphIssues.Count != 0) throw new StoryPackageException(graphIssues);

        var graphs = manifest.RequiredResources.CanonicalStories.Concat(manifest.RequiredResources.Sessions).Concat(manifest.RequiredResources.Tasks)
            .Select(path => GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[path]))).ToArray();
        var memberships = manifest.RequiredResources.CanonicalMemberships.Select(path => CanonicalStoryMembershipSerializer.Deserialize(ReadText(entries[path]))).ToArray();
        var actors = manifest.RequiredResources.Actors.Select(path => ActorSerializer.Deserialize(ReadText(entries[path]))).ToArray();
        var items = manifest.RequiredResources.Items.Concat(manifest.RequiredResources.ItemGroups).Select(path => ItemSerializer.Deserialize(ReadText(entries[path]))).ToArray();
        CurrentProjectValidator.Validate(new(graphs, memberships, actors, items, CanonicalStoryLogicGraph.Empty));
        var inventory = CurrentProjectInventory.Resources(new(graphs, memberships, actors, items, CanonicalStoryLogicGraph.Empty)).ToHashSet();
        var declaredResources = CurrentProjectInventory.Members(memberships.Single().OwnedResources)
            .Concat(CurrentProjectInventory.Members(memberships.Single().ReferencedResources)).Append(new(DgrResourceKind.Story, manifest.StoryId)).ToHashSet();
        if (!inventory.SetEquals(declaredResources)) throw new StoryPackageException("DGRS must contain exactly its owned and referenced resource closure.");

        var selectedCanonical = manifest.RequiredResources.CanonicalStories
            .Any(path => string.Equals(GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[path])).Id, manifest.StoryId, StringComparison.Ordinal));
        if (!selectedCanonical && manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal))
            throw new StoryPackageException("Manifest story_id is not present in canonical_stories.");
        if (manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal)
            && (!membershipIds.Contains(manifest.StoryId)
                || GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[manifest.RequiredResources.Story])).Id != manifest.StoryId))
            throw new StoryPackageException("Primary canonical Story and membership must match manifest story_id.");

    }

    private static void ValidateGraphs(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IEnumerable<string> paths,
        GraphResourceKind expectedKind,
        StoryPackageManifest manifest,
        ICollection<StoryPackageGraphIssue> graphIssues)
    {
        var scope = GraphResourceScopeAdapter.GetScope(expectedKind);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var envelope = GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[path]));
            if (envelope.ResourceKind != expectedKind
                || !path.EndsWith("/" + (expectedKind == GraphResourceKind.Story ? envelope.Id + ".json" : ResourceAddress.FromKey(envelope.Id).RelativeDefinitionPath), StringComparison.Ordinal))
                throw new StoryPackageException($"Canonical resource identity does not match DGRS path '{path}'.");
            if (!identities.Add(envelope.Id)) throw new StoryPackageException($"Duplicate canonical resource ID '{envelope.Id}'.");
            var graph = GraphResourceScopeAdapter.Open(envelope, scope);
            var issues = GraphScopePolicy.Validate(graph, scope)
                .Concat(ValidatePackageNodeShapes(graph, scope))
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .ToArray();
            if (issues.Length == 0) continue;
            var storyName = GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[manifest.RequiredResources.Story])).DisplayName;
            foreach (var issue in issues)
            {
                var node = graph.Nodes.FirstOrDefault(candidate => candidate.Id == issue.NodeId);
                graphIssues.Add(new(manifest.StoryId, storyName, path, expectedKind, envelope.Id,
                    envelope.DisplayName, node?.DisplayName, issue));
            }
        }
    }

    private static IEnumerable<ValidationIssue> ValidatePackageNodeShapes(
        GraphDocument graph,
        GraphScope scope)
    {
        var issues = GraphNodeShapeValidator.Validate(graph, scope);
        if (scope != GraphScope.Task) return issues;

        var dormant = (graph.Nodes ?? [])
            .Where(node => node is not null
                && string.Equals(node.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal)
                && CanonicalTaskObjectiveSchema.IsDormantUnselectedTarget(graph, node))
            .Select(node => node!.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (dormant.Count == 0) return issues;

        return issues.Where(issue => issue.Code != "graph.objective.target.invalid"
            || issue.NodeId is null
            || !dormant.Contains(issue.NodeId));
    }

    internal static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            if (bytes.Length + count > 64L * 1024 * 1024) throw new StoryPackageException("DGRS entry exceeds its decoded size limit.");
            bytes.Write(buffer, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
    }
}

public sealed record DgrsPackageBuildResult(
    string PackagePath,
    StoryPackageManifest Manifest,
    DgrsValidationResult Validation);

public sealed record DgrsValidationResult(
    string PackagePath,
    StoryPackageManifest Manifest,
    IReadOnlyList<string> Entries);
