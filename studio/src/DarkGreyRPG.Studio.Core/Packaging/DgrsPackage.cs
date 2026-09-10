using System.IO.Compression;
using System.Text;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Writes one validated DGRS v1 archive and commits it only after reopen validation.</summary>
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

    private static void WriteArchive(string sourceDirectory, string archivePath)
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
            ValidateCanonicalPayload(entries, manifest);
            return new DgrsValidationResult(
                path,
                manifest,
                entries.Keys.Order(StringComparer.Ordinal).ToArray());
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or UnauthorizedAccessException or NotSupportedException)
        {
            throw new StoryPackageException($"Could not open DGRS package '{path}'.", exception);
        }
    }

    private static Dictionary<string, ZipArchiveEntry> IndexEntries(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
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

    private static IEnumerable<string> RequiredPaths(StoryPackageRequiredResources required)
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
            if (!DgrResourceId.IsFullId(membership.StoryId) && !string.Equals(Path.GetFileNameWithoutExtension(path), membership.StoryId, StringComparison.Ordinal))
                throw new StoryPackageException($"Canonical membership identity does not match DGRS path '{path}'.");
        }

        ValidateGraphs(entries, manifest.RequiredResources.CanonicalStories, GraphResourceKind.Story);
        ValidateGraphs(entries, manifest.RequiredResources.Sessions, GraphResourceKind.Session);
        ValidateGraphs(entries, manifest.RequiredResources.Tasks, GraphResourceKind.Task);

        var selectedCanonical = manifest.RequiredResources.CanonicalStories
            .Any(path => string.Equals(GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[path])).Id, manifest.StoryId, StringComparison.Ordinal));
        if (!selectedCanonical && manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal))
            throw new StoryPackageException("Manifest story_id is not present in canonical_stories.");
        if (manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal)
            && (!membershipIds.Contains(manifest.StoryId)
                || GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[manifest.RequiredResources.Story])).Id != manifest.StoryId))
            throw new StoryPackageException("Primary canonical Story and membership must match manifest story_id.");

        if (manifest.RequiredResources.Story.StartsWith("stories/", StringComparison.Ordinal))
        {
            var story = StorySerializer.Deserialize(ReadText(entries[manifest.RequiredResources.Story]));
            if (!string.Equals(story.Id, manifest.StoryId, StringComparison.Ordinal))
                throw new StoryPackageException("Legacy Story identity does not match manifest story_id.");
            var errors = StoryValidator.Validate(story)
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .ToArray();
            if (errors.Length != 0)
                throw new StoryPackageException($"Legacy Story payload failed validation: {errors[0].Message}");
        }
    }

    private static void ValidateGraphs(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IEnumerable<string> paths,
        GraphResourceKind expectedKind)
    {
        var scope = GraphResourceScopeAdapter.GetScope(expectedKind);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var envelope = GraphResourceEnvelopeSerializer.Deserialize(ReadText(entries[path]));
            if (envelope.ResourceKind != expectedKind
                || !DgrResourceId.IsFullId(envelope.Id) && !string.Equals(Path.GetFileNameWithoutExtension(path), envelope.Id, StringComparison.Ordinal))
                throw new StoryPackageException($"Canonical resource identity does not match DGRS path '{path}'.");
            if (!identities.Add(envelope.Id)) throw new StoryPackageException($"Duplicate canonical resource ID '{envelope.Id}'.");
            var graph = GraphResourceScopeAdapter.Open(envelope, scope);
            var issues = GraphScopePolicy.Validate(graph, scope)
                .Concat(ValidatePackageNodeShapes(graph, scope))
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .ToArray();
            if (issues.Length != 0)
                throw new StoryPackageException($"Canonical resource '{path}' failed validation: {issues[0].Code}: {issues[0].Message}");
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

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
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
