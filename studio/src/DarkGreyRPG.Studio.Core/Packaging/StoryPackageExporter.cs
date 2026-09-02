using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Builds a deterministic, server-ready directory package for one Story.</summary>
public sealed class StoryPackageExporter
{
    private readonly string _projectDirectory;
    public StoryPackageExporter(string projectDirectory) => _projectDirectory = Path.GetFullPath(projectDirectory);

    public StoryPackageBuildResult Build(string storyId, string outputDirectory, string packageVersion = "1.0.0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (string.IsNullOrWhiteSpace(packageVersion)) throw new StoryPackageException("packageVersion is required.");

        var stories = new StoryRepository(_projectDirectory);
        var legacyPath = Path.Combine(stories.StoriesDirectory, storyId + ".json");
        var legacyStory = File.Exists(legacyPath) ? stories.LoadStory(storyId) : null;
        var canonicalStore = new CanonicalProjectGraphStore(_projectDirectory);
        var canonicalPath = Path.Combine(canonicalStore.StoriesDirectory, storyId + ".json");
        var canonicalStory = File.Exists(canonicalPath) ? canonicalStore.Stories.Load(storyId) : null;
        if (legacyStory is null && canonicalStory is null)
            throw new StoryPackageException($"Story '{storyId}' was not found in either the legacy or canonical Story repository.");

        EnsurePackageableStory(storyId, legacyStory, canonicalStory);
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        // The output directory is the package boundary. Remove only files and
        // roots owned by this exporter so stale resources cannot survive a
        // rebuild and alter the package contents.
        foreach (var directory in new[] { "actors", "dialogues", "quests", "stories", "items", "item_groups", "resources" })
        {
            var path = Path.Combine(root, directory);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        foreach (var file in new[] { "project.json", "manifest.json" })
        {
            var path = Path.Combine(root, file);
            if (File.Exists(path)) File.Delete(path);
        }
        // ProjectRepository's server contract requires these roots even when
        // this Story has no resources of that kind.
        foreach (var directory in new[] { "actors", "dialogues", "quests", "stories" })
            Directory.CreateDirectory(Path.Combine(root, directory));

        var required = new StoryPackageRequiredResources
        {
            Story = legacyStory is not null
                ? $"stories/{storyId}.json"
                : $"resources/canonical/stories/{storyId}.json",
        };
        if (legacyStory is not null)
        {
            Copy(root, "stories", legacyStory.Id + ".json");
            var membership = legacyStory.OwnedResources.Clone();
            membership.Actors.AddRange(legacyStory.ReferencedResources.Actors);
            membership.Dialogues.AddRange(legacyStory.ReferencedResources.Dialogues);
            membership.Quests.AddRange(legacyStory.ReferencedResources.Quests);
            AddLegacyResources(root, required, membership);
        }
        AddCanonicalResources(root, required, storyId);
        required = AddStoryLogicGraph(root, required, storyId);
        var projectPath = Path.Combine(_projectDirectory, "project.json");
        if (File.Exists(projectPath)) CopyFile(projectPath, Path.Combine(root, "project.json"));
        else
        {
            var fallbackProject = new Dictionary<string, object?>
            {
                ["schema_version"] = 2,
                ["id"] = storyId,
                ["display_name"] = canonicalStory?.DisplayName ?? legacyStory!.DisplayName,
            };
            File.WriteAllText(
                Path.Combine(root, "project.json"),
                JsonSerializer.Serialize(fallbackProject, new JsonSerializerOptions { WriteIndented = true })
                    .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        }

        var manifest = new StoryPackageManifest
        {
            ProducerVersion = packageVersion,
            PackageId = storyId,
            PackageVersion = packageVersion,
            StoryId = storyId,
            StorySchemaVersion = canonicalStory?.SchemaVersion ?? legacyStory!.SchemaVersion,
            RequiredResources = required,
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToJson());
        return new StoryPackageBuildResult(root, manifest);
    }

    public StoryPackageBuildResult Export(string storyId, string outputDirectory, string packageVersion = "1.0.0")
        => Build(storyId, outputDirectory, packageVersion);

    private static void EnsurePackageableStory(
        string storyId,
        StoryResource? legacyStory,
        GraphResourceEnvelope? canonicalStory)
    {
        var source = legacyStory is not null && ContainsCompatibilityEnterStory(legacyStory.Nodes)
            ? "legacy EnterStory"
            : null;
        if (canonicalStory is not null && ContainsCompatibilityEnterStory(canonicalStory.Graph?.Nodes))
            source = source is null ? "canonical enter_story" : "legacy EnterStory and canonical enter_story";

        if (source is not null)
        {
            throw new StoryPackageException(
                $"Selected Story '{storyId}' contains compatibility-only {source} transition data and cannot be exported as a server-ready single-Story package. Project-level cross-Story migration is required before export.");
        }
    }

    private static bool ContainsCompatibilityEnterStory(IEnumerable<StoryNodeResource>? nodes)
        => (nodes ?? [])
            .Where(node => node is not null)
            .Any(node => node.Type is "EnterStory" or "enter_story");

    private static bool ContainsCompatibilityEnterStory(IEnumerable<GraphNode>? nodes)
        => (nodes ?? [])
            .Where(node => node is not null)
            .Any(node => string.Equals(node.Type, "enter_story", StringComparison.Ordinal));

    private void AddLegacyResources(string root, StoryPackageRequiredResources required, StoryMembership membership)
    {
        foreach (var id in membership.Actors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "actors", id + ".json"); AddUnique(required.Actors, $"actors/{id}.json"); }
        foreach (var id in membership.Dialogues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "dialogues", id + ".json"); AddUnique(required.Dialogues, $"dialogues/{id}.json"); }
        foreach (var id in membership.Quests.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "quests", id + ".json"); AddUnique(required.Quests, $"quests/{id}.json"); }
    }

    private void AddCanonicalResources(string root, StoryPackageRequiredResources required, string storyId)
    {
        var store = new CanonicalProjectGraphStore(_projectDirectory);
        var membershipPath = Path.Combine(store.MembershipsDirectory, storyId + ".json");
        if (!File.Exists(membershipPath)) return;
        // A selected membership is the canonical package boundary. Read it
        // before creating any package canonical roots so unrelated canonical
        // project data cannot produce partial output roots.
        var manifest = store.Memberships.Load(storyId);
        var storyPath = Path.Combine(store.StoriesDirectory, storyId + ".json");
        if (!File.Exists(storyPath))
            throw new StoryPackageException($"Required canonical resource is missing: {storyPath}");

        foreach (var directory in new[] { "stories", "memberships", "sessions", "tasks" })
            Directory.CreateDirectory(Path.Combine(root, "resources", "canonical", directory));

        CopyCanonical(root, store.StoriesDirectory, storyId, "resources/canonical/stories", required.CanonicalStories);
        CopyCanonical(root, store.MembershipsDirectory, storyId, "resources/canonical/memberships", required.CanonicalMemberships);
        foreach (var id in manifest.OwnedResources.Actors.Concat(manifest.ReferencedResources.Actors).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "actors", id + ".json"); AddUnique(required.Actors, $"actors/{id}.json"); }
        foreach (var id in manifest.OwnedResources.Sessions.Concat(manifest.ReferencedResources.Sessions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) CopyCanonical(root, store.SessionsDirectory, id, "resources/canonical/sessions", required.Sessions);
        foreach (var id in manifest.OwnedResources.Tasks.Concat(manifest.ReferencedResources.Tasks).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) CopyCanonical(root, store.TasksDirectory, id, "resources/canonical/tasks", required.Tasks);
        foreach (var id in manifest.OwnedResources.Items.Concat(manifest.ReferencedResources.Items).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "items", id + ".json"); AddUnique(required.Items, $"items/{id}.json"); }
        foreach (var id in manifest.OwnedResources.ItemGroups.Concat(manifest.ReferencedResources.ItemGroups).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) { Copy(root, "item_groups", id + ".json"); AddUnique(required.ItemGroups, $"item_groups/{id}.json"); }
    }

    private StoryPackageRequiredResources AddStoryLogicGraph(
        string root,
        StoryPackageRequiredResources required,
        string storyId)
    {
        var store = new CanonicalProjectGraphStore(_projectDirectory);
        var outgoing = store.StoryLogicGraph.Load().Connections
            .Where(connection => string.Equals(connection.SourceStoryId, storyId, StringComparison.Ordinal))
            .OrderBy(connection => connection.SourcePortId, StringComparer.Ordinal)
            .ThenBy(connection => connection.TargetStoryId, StringComparer.Ordinal)
            .ThenBy(connection => connection.TargetPortId, StringComparer.Ordinal)
            .ToArray();
        if (outgoing.Length == 0) return required;
        const string relative = "resources/story_logic_graph.json";
        var graph = new CanonicalStoryLogicGraph(1, outgoing);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target,
            JsonSerializer.Serialize(graph, options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        return new StoryPackageRequiredResources
        {
            Story = required.Story,
            Actors = required.Actors,
            Items = required.Items,
            ItemGroups = required.ItemGroups,
            Dialogues = required.Dialogues,
            Quests = required.Quests,
            CanonicalStories = required.CanonicalStories,
            CanonicalMemberships = required.CanonicalMemberships,
            Sessions = required.Sessions,
            Tasks = required.Tasks,
            StoryLogicGraph = relative,
        };
    }

    private void CopyCanonical(string root, string sourceDirectory, string id, string packageDirectory, List<string> paths)
    {
        var source = Path.Combine(sourceDirectory, id + ".json");
        if (!File.Exists(source)) throw new StoryPackageException($"Required canonical resource is missing: {source}");
        var relative = packageDirectory + "/" + id + ".json";
        CopyFile(source, Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        AddUnique(paths, relative);
    }

    private void Copy(string root, string directory, string fileName)
    {
        var source = Path.Combine(_projectDirectory, directory, fileName);
        if (!File.Exists(source)) throw new StoryPackageException($"Required resource is missing: {source}");
        CopyFile(source, Path.Combine(root, directory, fileName));
    }

    private static void CopyFile(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, true);
    }

    private static void AddUnique(List<string> paths, string path)
    {
        if (!paths.Contains(path, StringComparer.Ordinal)) paths.Add(path);
    }
}

public sealed record StoryPackageBuildResult(string PackageDirectory, StoryPackageManifest Manifest);
