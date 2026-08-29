using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Migration;

/// <summary>A fingerprint of a source file inspected by the project preview.</summary>
public sealed record CanonicalProjectMigrationSourceFingerprint(
    string RelativePath,
    string FullPath,
    string Sha256,
    long Length)
{
    public string Path => FullPath;
    public string Hash => Sha256;
}

/// <summary>A detached, deterministic proposed canonical file.</summary>
public sealed class CanonicalProjectMigrationWriteCandidate
{
    internal CanonicalProjectMigrationWriteCandidate(
        string relativePath, string fullPath, string contents, string sha256,
        GraphResourceKind? resourceKind, string resourceId, bool isMembership)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        Path = fullPath;
        Contents = contents;
        Json = contents;
        Sha256 = sha256;
        Hash = sha256;
        ResourceKind = resourceKind;
        ResourceId = resourceId;
        Id = resourceId;
        IsMembership = isMembership;
    }

    public string RelativePath { get; }
    public string FullPath { get; }
    public string Path { get; }
    public string Contents { get; }
    public string Content => Contents;
    public string Json { get; }
    public string Sha256 { get; }
    public string ContentSha256 => Sha256;
    public string Hash { get; }
    public string DestinationPath => FullPath;
    public GraphResourceKind? ResourceKind { get; }
    public string ResourceId { get; }
    public string Id { get; }
    public bool IsMembership { get; }
}

/// <summary>A detached canonical membership manifest and its proposed destination.</summary>
public sealed class CanonicalStoryMembershipManifestCandidate
{
    internal CanonicalStoryMembershipManifestCandidate(
        CanonicalStoryMembershipManifest manifest,
        string relativePath, string fullPath, string contents, string sha256)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        Path = fullPath;
        Contents = contents;
        Json = contents;
        Sha256 = sha256;
        Hash = sha256;
        StoryId = manifest.StoryId;
        Id = StoryId;
    }

    public CanonicalStoryMembershipManifest Manifest => CanonicalStoryMembershipManifest.FromJson(Contents);
    public CanonicalStoryMembershipManifest Membership => Manifest;
    public string StoryId { get; }
    public string Id { get; }
    public string RelativePath { get; }
    public string FullPath { get; }
    public string Path { get; }
    public string Contents { get; }
    public string Content => Contents;
    public string Json { get; }
    public string Sha256 { get; }
    public string ContentSha256 => Sha256;
    public string Hash { get; }
    public string DestinationPath => FullPath;
}

/// <summary>One legacy file and the detached conversion result for that file.</summary>
public sealed class CanonicalProjectMigrationResourcePreview
{
    internal CanonicalProjectMigrationResourcePreview(
        LegacyMigrationSourceKind kind, string relativePath, string fullPath,
        LegacyMigrationPreviewResult result)
    {
        Kind = kind;
        RelativePath = relativePath;
        FullPath = fullPath;
        Result = result;
    }

    public LegacyMigrationSourceKind Kind { get; }
    public string KindName => Kind switch
    {
        LegacyMigrationSourceKind.Dialogue => "dialogue",
        LegacyMigrationSourceKind.Quest => "quest",
        _ => "story",
    };
    public string RelativePath { get; }
    public string FullPath { get; }
    public LegacyMigrationPreviewResult Result { get; }
    public LegacyMigrationPreviewResult Preview => Result;
    public string Id => Result.SourceId;
    public bool CanApply => Result.CanApply;
    public bool HasErrors => Result.HasErrors;
}

/// <summary>
/// Read-only project-level aggregation of the Studio 2.1.3 legacy migration.
/// Construction and preview never create directories or write files.
/// </summary>
public sealed class CanonicalProjectMigrationPreviewResult
{
    internal CanonicalProjectMigrationPreviewResult(
        string projectDirectory,
        IReadOnlyList<CanonicalProjectMigrationSourceFingerprint> fingerprints,
        IReadOnlyList<CanonicalProjectMigrationResourcePreview> resources,
        IReadOnlyList<CanonicalStoryMembershipManifestCandidate> memberships,
        IReadOnlyList<CanonicalProjectMigrationWriteCandidate> writes,
        IReadOnlyList<ValidationIssue> issues)
    {
        ProjectDirectory = projectDirectory;
        ProjectPath = projectDirectory;
        FullProjectPath = projectDirectory;
        SourceFingerprints = Array.AsReadOnly(fingerprints.ToArray());
        ResourcePreviews = Array.AsReadOnly(resources.ToArray());
        MembershipCandidates = Array.AsReadOnly(memberships.ToArray());
        ProposedWrites = Array.AsReadOnly(writes.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public string ProjectDirectory { get; }
    public string ProjectPath { get; }
    public string FullProjectPath { get; }
    public string ProjectFilePath => Path.Combine(ProjectDirectory, "project.json");
    public IReadOnlyList<CanonicalProjectMigrationSourceFingerprint> SourceFingerprints { get; }
    public IReadOnlyList<CanonicalProjectMigrationSourceFingerprint> Fingerprints => SourceFingerprints;
    public IReadOnlyList<CanonicalProjectMigrationResourcePreview> ResourcePreviews { get; }
    public IReadOnlyList<CanonicalProjectMigrationResourcePreview> PerResourcePreviews => ResourcePreviews;
    public IReadOnlyList<CanonicalStoryMembershipManifestCandidate> MembershipCandidates { get; }
    public IReadOnlyList<CanonicalStoryMembershipManifestCandidate> Memberships => MembershipCandidates;
    public IReadOnlyList<CanonicalStoryMembershipManifestCandidate> CanonicalMembershipCandidates => MembershipCandidates;
    public IReadOnlyList<CanonicalProjectMigrationWriteCandidate> ProposedWrites { get; }
    public IReadOnlyList<CanonicalProjectMigrationWriteCandidate> Writes => ProposedWrites;
    public IReadOnlyList<CanonicalProjectMigrationWriteCandidate> ProposedFiles => ProposedWrites;
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error)
        || ResourcePreviews.Any(p => p.HasErrors);
    public bool CanApply => !HasErrors;

    public IReadOnlyList<LegacyMigrationPreviewResult> Dialogues => ResourcePreviews
        .Where(p => p.Kind == LegacyMigrationSourceKind.Dialogue).Select(p => p.Result).ToArray();
    public IReadOnlyList<LegacyMigrationPreviewResult> DialoguePreviews => Dialogues;
    public IReadOnlyList<LegacyMigrationPreviewResult> Quests => ResourcePreviews
        .Where(p => p.Kind == LegacyMigrationSourceKind.Quest).Select(p => p.Result).ToArray();
    public IReadOnlyList<LegacyMigrationPreviewResult> QuestPreviews => Quests;
    public IReadOnlyList<LegacyMigrationPreviewResult> Stories => ResourcePreviews
        .Where(p => p.Kind == LegacyMigrationSourceKind.Story).Select(p => p.Result).ToArray();
    public IReadOnlyList<LegacyMigrationPreviewResult> StoryPreviews => Stories;
}

/// <summary>Project-level, read-only migration preview entry point.</summary>
public static class CanonicalProjectMigrationPreview
{
    private static readonly Regex IdPattern = new(
        "^[a-z0-9][a-z0-9_-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions ProjectOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private const string CanonicalPrefix = "resources/canonical/";

    public static CanonicalProjectMigrationPreviewResult Preview(string projectDirectory)
        => PreviewProject(projectDirectory);

    public static CanonicalProjectMigrationPreviewResult PreviewProject(string projectDirectory)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(projectDirectory))
            return Empty(projectDirectory ?? string.Empty,
                Issue("migration.project.path.invalid", "Project directory is required.", "project_path"));
        string root;
        try { root = Path.GetFullPath(projectDirectory ?? string.Empty); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Empty(projectDirectory ?? string.Empty,
                Issue("migration.project.path.invalid", exception.Message, "project_path"));
        }
        if (!Directory.Exists(root))
            issues.Add(Issue("migration.project.path.invalid", $"Project directory '{projectDirectory}' does not exist.", "project_path"));

        var fingerprints = new List<CanonicalProjectMigrationSourceFingerprint>();
        var resources = new List<CanonicalProjectMigrationResourcePreview>();
        var memberships = new List<CanonicalStoryMembershipManifestCandidate>();
        var writes = new List<CanonicalProjectMigrationWriteCandidate>();

        var projectPath = Path.Combine(root, "project.json");
        byte[]? projectBytes = null;
        if (File.Exists(projectPath)) projectBytes = ReadAndFingerprint(root, projectPath, fingerprints, issues);
        else issues.Add(Issue("migration.project.project.missing", "Project file 'project.json' is missing.", "project_path"));
        if (projectBytes is not null) ReadProject(projectBytes, issues);

        CheckCanonicalStore(root, issues);

        var actors = ReadActors(root, fingerprints, issues);
        var dialogues = ReadDialogues(root, fingerprints, issues);
        var quests = ReadQuests(root, fingerprints, issues);
        var stories = ReadStories(root, fingerprints, issues);
        ValidateActorReferences(actors, dialogues, quests, stories, issues);

        // Dialogue and Quest conversions intentionally precede Story conversion.
        var dialogueById = new Dictionary<string, LegacyMigrationPreviewResult>(StringComparer.Ordinal);
        foreach (var item in dialogues)
        {
            var result = PreviewDialogue(item.Resource, item.RelativePath, item.FullPath, issues);
            resources.Add(result);
            issues.AddRange(result.Result.Issues);
            if (item.Resource is not null && result.Result.CanApply) dialogueById[item.Resource.Id] = result.Result;
        }
        var questById = new Dictionary<string, LegacyMigrationPreviewResult>(StringComparer.Ordinal);
        foreach (var item in quests)
        {
            var result = PreviewQuest(item.Resource, item.RelativePath, item.FullPath, issues);
            resources.Add(result);
            issues.AddRange(result.Result.Issues);
            if (item.Resource is not null && result.Result.CanApply) questById[item.Resource.Id] = result.Result;
        }

        var childEnvelopes = dialogueById.Values.Concat(questById.Values)
            .Where(r => r.Envelope is not null).Select(r => r.Envelope!).ToArray();
        foreach (var item in stories)
        {
            var result = PreviewStory(item.Resource, item.RelativePath, item.FullPath, childEnvelopes, issues);
            resources.Add(result);
            issues.AddRange(result.Result.Issues);
        }

        foreach (var item in stories.Where(x => x.Resource is not null))
        {
            var story = item.Resource!;
            try
            {
                var membership = BuildMembership(root, story, actors, dialogueById, questById, issues);
                if (membership is not null) memberships.Add(membership);
            }
            catch (Exception exception)
            {
                issues.Add(Issue("migration.project.membership.preview.failed", exception.Message, item.RelativePath));
            }
        }

        // Writes are ordered by canonical destination, independent of source directory order.
        foreach (var preview in resources.Where(p => p.Result.CanApply).OrderBy(p => p.KindName, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            var envelope = preview.Result.Envelope!;
            AddWrite(root, CanonicalDirectoryName(envelope.ResourceKind), envelope.Id,
                GraphResourceEnvelopeSerializer.Serialize(envelope), envelope.ResourceKind, false, writes, issues);
        }
        foreach (var membership in memberships.OrderBy(m => m.StoryId, StringComparer.Ordinal))
            AddWrite(root, "memberships", membership.StoryId, membership.Contents, null, true, writes, issues);

        fingerprints.Sort((a, b) => StringComparer.Ordinal.Compare(a.RelativePath, b.RelativePath));
        resources.Sort((a, b) => StringComparer.Ordinal.Compare(a.RelativePath, b.RelativePath));
        memberships.Sort((a, b) => StringComparer.Ordinal.Compare(a.StoryId, b.StoryId));
        writes.Sort((a, b) => StringComparer.Ordinal.Compare(a.RelativePath, b.RelativePath));
        return new CanonicalProjectMigrationPreviewResult(root, fingerprints, resources, memberships, writes, issues);
    }

    public static CanonicalProjectMigrationPreviewResult Inspect(string projectDirectory)
        => PreviewProject(projectDirectory);

    private static CanonicalProjectMigrationPreviewResult Empty(string root, params ValidationIssue[] issues)
        => new(root, [], [], [], [], issues);

    private sealed record Parsed<T>(string RelativePath, string FullPath, T? Resource);

    private static List<Parsed<ActorResource>> ReadActors(string root, List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues)
        => ReadKind(root, "actors", "actor", fingerprints, issues,
            (json, _) => ActorSerializer.Deserialize(json));

    private static List<Parsed<DialogueResource>> ReadDialogues(string root, List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues)
        => ReadKind(root, "dialogues", "dialogue", fingerprints, issues,
            (json, _) => DialogueSerializer.Deserialize(json));

    private static List<Parsed<QuestResource>> ReadQuests(string root, List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues)
        => ReadKind(root, "quests", "quest", fingerprints, issues,
            (json, _) => QuestSerializer.Deserialize(json));

    private static List<Parsed<StoryResource>> ReadStories(string root, List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues)
        => ReadKind(root, "stories", "story", fingerprints, issues,
            (json, _) => StorySerializer.Deserialize(json));

    private static List<Parsed<T>> ReadKind<T>(string root, string directory, string kind,
        List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues,
        Func<string, string, T> parser)
    {
        var result = new List<Parsed<T>>();
        var path = Path.Combine(root, directory);
        if (!Directory.Exists(path)) return result;
        string[] paths;
        try { paths = Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(p => Path.GetRelativePath(root, p), StringComparer.Ordinal).ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(Issue($"migration.project.{kind}.enumerate", exception.Message, directory));
            return result;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in paths)
        {
            var relative = NormalizeRelative(root, file);
            var bytes = ReadAndFingerprint(root, file, fingerprints, issues);
            if (bytes is null)
            {
                result.Add(new Parsed<T>(relative, Path.GetFullPath(file), default));
                continue;
            }
            try
            {
                var json = DecodeText(bytes);
                var resource = parser(json, file);
                var id = ResourceId(resource);
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!string.Equals(stem, id, StringComparison.Ordinal))
                    issues.Add(Issue($"migration.project.{kind}.filename.mismatch",
                        $"{kind} file stem '{stem}' must equal resource ID '{id}'.", relative));
                if (!ids.Add(id))
                    issues.Add(Issue($"migration.project.{kind}.id.duplicate",
                        $"Duplicate {kind} ID '{id}'.", relative));
                result.Add(new Parsed<T>(relative, Path.GetFullPath(file), resource));
            }
            catch (Exception exception)
            {
                issues.Add(Issue($"migration.project.{kind}.parse",
                    $"Could not parse {kind} file '{relative}': {exception.Message}", relative));
                result.Add(new Parsed<T>(relative, Path.GetFullPath(file), default));
            }
        }
        return result;
    }

    private static CanonicalProjectMigrationResourcePreview PreviewDialogue(
        DialogueResource? source, string relative, string full, List<ValidationIssue> issues)
    {
        if (source is null) return Failed(LegacyMigrationSourceKind.Dialogue, relative, full, "dialogue");
        try { return new(LegacyMigrationSourceKind.Dialogue, relative, full, CanonicalLegacyMigrationPreview.PreviewDialogue(source)); }
        catch (Exception exception) { issues.Add(Issue("migration.project.dialogue.preview.failed", exception.Message, relative)); return Failed(LegacyMigrationSourceKind.Dialogue, relative, full, source.Id); }
    }
    private static CanonicalProjectMigrationResourcePreview PreviewQuest(
        QuestResource? source, string relative, string full, List<ValidationIssue> issues)
    {
        if (source is null) return Failed(LegacyMigrationSourceKind.Quest, relative, full, "quest");
        try { return new(LegacyMigrationSourceKind.Quest, relative, full, CanonicalLegacyMigrationPreview.PreviewQuest(source)); }
        catch (Exception exception) { issues.Add(Issue("migration.project.quest.preview.failed", exception.Message, relative)); return Failed(LegacyMigrationSourceKind.Quest, relative, full, source.Id); }
    }
    private static CanonicalProjectMigrationResourcePreview PreviewStory(
        StoryResource? source, string relative, string full, IReadOnlyList<GraphResourceEnvelope> children, List<ValidationIssue> issues)
    {
        if (source is null) return Failed(LegacyMigrationSourceKind.Story, relative, full, "story");
        try { return new(LegacyMigrationSourceKind.Story, relative, full, CanonicalLegacyMigrationPreview.PreviewStory(source, children)); }
        catch (Exception exception) { issues.Add(Issue("migration.project.story.preview.failed", exception.Message, relative)); return Failed(LegacyMigrationSourceKind.Story, relative, full, source.Id); }
    }

    private static CanonicalProjectMigrationResourcePreview Failed(LegacyMigrationSourceKind kind, string relative, string full, string id)
        => new(kind, relative, full, new LegacyMigrationPreviewResult(kind, id, null,
            [Issue($"migration.project.{KindText(kind)}.parse", "Source resource could not be converted.", relative)]));

    private static CanonicalStoryMembershipManifestCandidate? BuildMembership(
        string root, StoryResource story, IReadOnlyList<Parsed<ActorResource>> actors,
        IReadOnlyDictionary<string, LegacyMigrationPreviewResult> dialogues,
        IReadOnlyDictionary<string, LegacyMigrationPreviewResult> quests,
        List<ValidationIssue> issues)
    {
        var actorIds = actors.Where(a => a.Resource is not null).Select(a => a.Resource!.Id).ToHashSet(StringComparer.Ordinal);
        var owned = MapMembership(story.OwnedResources, story.Id, "owned_resources", actorIds, dialogues, quests, issues);
        var referenced = MapMembership(story.ReferencedResources, story.Id, "referenced_resources", actorIds, dialogues, quests, issues);
        var manifest = new CanonicalStoryMembershipManifest(story.Id, owned, referenced);
        try { CanonicalStoryMembershipSerializer.Validate(manifest); }
        catch (CanonicalStoryMembershipException exception)
        {
            issues.Add(Issue("migration.project.membership.invalid", exception.Message, story.Id));
            return null;
        }
        var json = manifest.ToJson();
        var relative = CanonicalPrefix + "memberships/" + story.Id + ".json";
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return new CanonicalStoryMembershipManifestCandidate(manifest, relative, full, json, Hash(json));
    }

    private static void ValidateActorReferences(
        IReadOnlyList<Parsed<ActorResource>> actors,
        IReadOnlyList<Parsed<DialogueResource>> dialogues,
        IReadOnlyList<Parsed<QuestResource>> quests,
        IReadOnlyList<Parsed<StoryResource>> stories,
        List<ValidationIssue> issues)
    {
        var actorIds = actors.Where(x => x.Resource is not null).Select(x => x.Resource!.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in dialogues.Where(x => x.Resource is not null))
        {
            foreach (var id in item.Resource!.Nodes?.Select(x => x.Speaker).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal) ?? [])
                if (!actorIds.Contains(id!))
                    issues.Add(Issue("migration.project.dialogue.actor.missing", $"Dialogue actor '{id}' is missing.", item.RelativePath));
        }
        foreach (var item in quests.Where(x => x.Resource is not null))
        {
            foreach (var id in item.Resource!.Objectives?.Select(x => x.ActorId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal) ?? [])
                if (!actorIds.Contains(id!))
                    issues.Add(Issue("migration.project.quest.actor.missing", $"Quest actor '{id}' is missing.", item.RelativePath));
        }
        foreach (var item in stories.Where(x => x.Resource is not null))
        {
            foreach (var id in item.Resource!.Nodes?.Select(x => ActorProperty(x)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal) ?? [])
                if (!actorIds.Contains(id!))
                    issues.Add(Issue("migration.project.story.actor.missing", $"Story actor '{id}' is missing.", item.RelativePath));
        }
    }

    private static string? ActorProperty(StoryNodeResource node)
    {
        if (node.Properties is null) return null;
        foreach (var name in new[] { "actor_id", "target_actor_id" })
            if (node.Properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static CanonicalStoryMembershipSet MapMembership(
        StoryMembership source, string storyId, string field, HashSet<string> actorIds,
        IReadOnlyDictionary<string, LegacyMigrationPreviewResult> dialogues,
        IReadOnlyDictionary<string, LegacyMigrationPreviewResult> quests, List<ValidationIssue> issues)
    {
        var result = new CanonicalStoryMembershipSet();
        foreach (var id in source.Actors ?? [])
        {
            if (!actorIds.Contains(id)) issues.Add(Issue("migration.project.membership.actor.missing", $"Actor '{id}' in story '{storyId}' is missing.", field));
            result.Actors.Add(id);
        }
        foreach (var id in source.Dialogues ?? [])
        {
            if (!dialogues.TryGetValue(id, out var preview) || !preview.CanApply)
                issues.Add(Issue("migration.project.membership.session.missing", $"Dialogue/Session '{id}' in story '{storyId}' is missing or failed preview.", field));
            result.Sessions.Add(id);
        }
        foreach (var id in source.Quests ?? [])
        {
            if (!quests.TryGetValue(id, out var preview) || !preview.CanApply)
                issues.Add(Issue("migration.project.membership.task.missing", $"Quest/Task '{id}' in story '{storyId}' is missing or failed preview.", field));
            result.Tasks.Add(id);
        }
        return result;
    }

    private static void AddWrite(string root, string directory, string id, string contents,
        GraphResourceKind? resourceKind, bool membership,
        List<CanonicalProjectMigrationWriteCandidate> writes, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern.IsMatch(id))
        {
            issues.Add(Issue("migration.project.write.id.invalid", $"Destination ID '{id}' is not safe.", directory));
            return;
        }
        var relative = CanonicalPrefix + directory + "/" + id + ".json";
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(root, full))
        {
            issues.Add(Issue("migration.project.write.path.escape", $"Destination '{relative}' escapes project root.", relative));
            return;
        }
        if (writes.Any(candidate => string.Equals(candidate.FullPath, full, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Issue("migration.project.write.path.duplicate",
                $"More than one candidate targets '{relative}'.", relative));
            return;
        }
        writes.Add(new CanonicalProjectMigrationWriteCandidate(relative, full, contents, Hash(contents), resourceKind, id, membership));
    }

    private static void CheckCanonicalStore(string root, List<ValidationIssue> issues)
    {
        var canonical = Path.Combine(root, "resources", "canonical");
        if (!Directory.Exists(canonical)) return;
        try
        {
            if (Directory.EnumerateFiles(canonical, "*.json", SearchOption.AllDirectories).Any())
                issues.Add(Issue("migration.project.canonical.nonempty", "Canonical project store already contains JSON data.", NormalizeRelative(root, canonical)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { issues.Add(Issue("migration.project.canonical.read", exception.Message, NormalizeRelative(root, canonical))); }
    }

    private static void ReadProject(byte[] bytes, List<ValidationIssue> issues)
    {
        try
        {
            var project = JsonSerializer.Deserialize<ProjectResource>(DecodeText(bytes), ProjectOptions)
                ?? throw new JsonException("Project JSON root cannot be null.");
            if (project.SchemaVersion is not (ProjectResource.LegacySchemaVersion or ProjectResource.CurrentSchemaVersion))
                throw new JsonException($"Unsupported project schema_version {project.SchemaVersion}.");
        }
        catch (Exception exception) { issues.Add(Issue("migration.project.project.parse", exception.Message, "project.json")); }
    }

    private static byte[]? ReadAndFingerprint(string root, string path,
        List<CanonicalProjectMigrationSourceFingerprint> fingerprints, List<ValidationIssue> issues)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            fingerprints.Add(new(NormalizeRelative(root, path), Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.LongLength));
            return bytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(Issue("migration.project.source.read", exception.Message, NormalizeRelative(root, path)));
            return null;
        }
    }

    private static string ResourceId<T>(T resource) => resource switch
    {
        ActorResource actor => actor.Id,
        DialogueResource dialogue => dialogue.Id,
        QuestResource quest => quest.Id,
        StoryResource story => story.Id,
        _ => throw new InvalidOperationException("Unsupported legacy resource type."),
    };
    private static string KindText(LegacyMigrationSourceKind kind) => kind switch
    {
        LegacyMigrationSourceKind.Dialogue => "dialogue",
        LegacyMigrationSourceKind.Quest => "quest",
        _ => "story",
    };
    private static string CanonicalDirectoryName(GraphResourceKind kind) => kind switch
    {
        GraphResourceKind.Story => CanonicalProjectGraphStore.StoriesDirectoryName,
        GraphResourceKind.Session => CanonicalProjectGraphStore.SessionsDirectoryName,
        GraphResourceKind.Task => CanonicalProjectGraphStore.TasksDirectoryName,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported canonical resource kind."),
    };
    private static string DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
    private static string NormalizeRelative(string root, string path)
        => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static bool IsWithin(string root, string path)
    {
        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static ValidationIssue Issue(string code, string message, string? field = null)
        => new(code, message, field);
}

/// <summary>Instance facade for dependency injection and future transaction seams.</summary>
public sealed class CanonicalProjectMigrationPreviewService
{
    public CanonicalProjectMigrationPreviewResult Preview(string projectDirectory)
        => CanonicalProjectMigrationPreview.PreviewProject(projectDirectory);
    public CanonicalProjectMigrationPreviewResult PreviewProject(string projectDirectory)
        => CanonicalProjectMigrationPreview.PreviewProject(projectDirectory);
}
