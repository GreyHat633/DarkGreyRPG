using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Identity;

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

        _ = StoryUid.Parse(storyId);
        var canonicalStore = new CanonicalProjectGraphStore(_projectDirectory);
        var canonicalStory = canonicalStore.Stories.Load(storyId);
        EnsurePackageableStory(storyId, canonicalStory);
        EnsurePackageableSessionText(canonicalStore, storyId);
        EnsureDynamicReferences(canonicalStore, storyId);
        var providers = OfflineProviderCatalog.Load(_projectDirectory);
        if (providers.Diagnostics.Count != 0) throw new StoryPackageException(string.Join("; ", providers.Diagnostics.Select(issue => issue.Message)));
        EnsurePackageableBoundaries(canonicalStory);
        if (File.Exists(canonicalStore.Memberships.GetPath(storyId)))
        {
            var membership = canonicalStore.Memberships.Load(storyId);
            foreach (var id in membership.OwnedResources.Sessions.Concat(membership.ReferencedResources.Sessions)
                         .Concat(membership.OwnedResources.Tasks).Concat(membership.ReferencedResources.Tasks).Distinct(StringComparer.Ordinal))
            {
                var kind = ResourceAddress.FromKey(id).Kind == ResourceKind.Session ? GraphResourceKind.Session : GraphResourceKind.Task;
                var repository = kind == GraphResourceKind.Session ? canonicalStore.Sessions : canonicalStore.Tasks;
                if (File.Exists(repository.GetPath(id))) EnsurePackageableBoundaries(repository.Load(id));
                else if (providers.Resolve(kind == GraphResourceKind.Session ? DgrResourceKind.Session : DgrResourceKind.Task, id) is { } provider)
                    EnsurePackageableBoundaries(GraphResourceEnvelopeSerializer.Deserialize(System.Text.Encoding.UTF8.GetString(provider.DefinitionBytes)));
            }
        }
        var usedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = Path.GetFullPath(outputDirectory);
        if (string.Equals(root, _projectDirectory, StringComparison.OrdinalIgnoreCase)
            || _projectDirectory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new StoryPackageException("Package output cannot contain the source project.");
        Directory.CreateDirectory(root);
        // The output directory is the package boundary. Remove only files and
        // roots owned by this exporter so stale resources cannot survive a
        // rebuild and alter the package contents.
        foreach (var directory in new[] { "actors", "dialogues", "quests", "stories", "items", "item_groups", "resources", "media" })
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
        foreach (var directory in new[] { "actors", "resources" })
            Directory.CreateDirectory(Path.Combine(root, directory));

        var required = new StoryPackageRequiredResources
        {
            Story = $"resources/canonical/stories/{storyId}.json",
        };
        AddCanonicalResources(root, required, storyId, providers, usedProviders);
        SessionPortraitPackageValidation.Validate(root, required);
        required = AddStoryLogicGraph(root, required, storyId);
        StoryPackageMedia.CopyReachable(_projectDirectory, root, required, mediaRef =>
        {
            var candidates = providers.Providers.Where(provider => usedProviders.Contains(provider.PackagePath) && provider.Entries.ContainsKey(mediaRef))
                .Select(provider => provider.GetEntry(mediaRef)).ToArray();
            if (candidates.Length == 0) return null;
            if (candidates.Any(bytes => !bytes.AsSpan().SequenceEqual(candidates[0]))) throw new StoryPackageException("Referenced media content differs: " + mediaRef);
            return candidates[0];
        });
        var projectPath = Path.Combine(_projectDirectory, "project.json");
        if (File.Exists(projectPath))
        {
            var project = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
            // Older projects use their persisted project id as the compatibility origin.
            // Imported provider project.json is never installed in authoring storage.
            if (string.IsNullOrWhiteSpace(project["project_origin_code"]?.GetValue<string>()))
                project["project_origin_code"] = project["id"]!.GetValue<string>();
            File.WriteAllText(Path.Combine(root, "project.json"), project.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            var fallbackProject = new Dictionary<string, object?>
            {
                ["schema_version"] = 3,
                ["identity_format"] = "story-uid-v1",
                ["id"] = storyId,
                ["display_name"] = canonicalStory.DisplayName,
                ["project_origin_code"] = storyId,
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
            StorySchemaVersion = canonicalStory.SchemaVersion,
            RequiredResources = required,
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToJson());
        return new StoryPackageBuildResult(root, manifest);
    }

    public StoryPackageBuildResult Export(string storyId, string outputDirectory, string packageVersion = "1.0.0")
        => Build(storyId, outputDirectory, packageVersion);

    private static void EnsureDynamicReferences(CanonicalProjectGraphStore store, string storyId)
    {
        if (!File.Exists(store.Memberships.GetPath(storyId))) return;
        var membership = store.Memberships.Load(storyId);
        var actors = membership.OwnedResources.Actors.Concat(membership.ReferencedResources.Actors).ToHashSet(StringComparer.Ordinal);
        var items = membership.OwnedResources.Items.Concat(membership.ReferencedResources.Items)
            .Concat(membership.OwnedResources.ItemGroups).Concat(membership.ReferencedResources.ItemGroups).ToHashSet(StringComparer.Ordinal);
        var files = membership.OwnedResources.Sessions.Concat(membership.ReferencedResources.Sessions).Select(store.Sessions.GetPath)
            .Concat(membership.OwnedResources.Tasks.Concat(membership.ReferencedResources.Tasks).Select(store.Tasks.GetPath))
            .Append(store.Stories.GetPath(storyId));
        foreach (var path in files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var id in DynamicContentText.ItemReferences(json.RootElement))
                if (!items.Contains(id)) throw new StoryPackageException($"动态内容引用了未声明物品 '{id}'：{path}");
            foreach (var id in DynamicContentText.ActorReferences(json.RootElement))
                if (!actors.Contains(id)) throw new StoryPackageException($"动态内容引用了未声明角色 '{id}'：{path}");
        }
    }

    // Empty drafts remain editable and saveable, but the runtime rejects them.
    // Check before touching output so a failed export preserves the previous package.
    private static void EnsurePackageableSessionText(CanonicalProjectGraphStore store, string storyId)
    {
        if (!File.Exists(store.Memberships.GetPath(storyId))) return;
        var membership = store.Memberships.Load(storyId);
        foreach (var id in membership.OwnedResources.Sessions.Concat(membership.ReferencedResources.Sessions)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(store.Sessions.GetPath(id))) continue;
            var session = store.Sessions.Load(id);
            var graph = session.Graph ?? throw new StoryPackageException($"会话 '{id}' 缺少节点图，不能导出。");
            foreach (var node in graph.Nodes.Where(node => node.Type == "line"))
            {
                var pages = CanonicalSessionLineSchema.ReadPages(node);
                if (pages.Count == 0)
                    throw new StoryPackageException($"会话 '{id}' 的台词节点 '{node.Id}' 至少需要一句正文，不能导出空台词。");
                for (var index = 0; index < pages.Count; index++)
                    if (!pages[index].TryGetValue("text", out var text)
                        || text.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(text.GetString()))
                        throw new StoryPackageException($"会话 '{id}' 的台词节点 '{node.Id}' 第 {index + 1} 句正文不能为空，请填写后再导出。");
            }
        }
    }

    private static void EnsurePackageableStory(string storyId, GraphResourceEnvelope story)
    {
        var issues = GraphScopePolicy.Validate(story.Graph!, GraphScope.StoryFlow)
            .Where(issue => issue.Severity == DarkGreyRPG.Studio.Core.Validation.ValidationSeverity.Error).ToArray();
        if (issues.Length != 0)
            throw new StoryPackageException($"Story '{storyId}' cannot run: " + string.Join("; ", issues.Select(issue => issue.Message)));
        if (story.Graph!.Nodes.Any(node => node.Type == "enter_story"))
            throw new StoryPackageException($"Story '{storyId}' contains retired enter_story transitions.");
    }

    private static void EnsurePackageableBoundaries(GraphResourceEnvelope resource)
    {
        foreach (var node in resource.Graph!.Nodes.Where(node => node.Type is "logic_input" or "logic_output" or "terminate"
                     || resource.ResourceKind == GraphResourceKind.Session && node.Type == "end"))
            foreach (var field in new[] { "port_id", "display_name" })
                if (!node.Properties.TryGetValue(field, out var value) || value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(value.GetString()))
                    throw new StoryPackageException($"Resource '{resource.Id}' boundary '{node.Id}' requires a nonblank {field}.");
    }

    private void AddCanonicalResources(string root, StoryPackageRequiredResources required, string storyId, OfflineProviderCatalog providers, ISet<string> usedProviders)
    {
        var store = new CanonicalProjectGraphStore(_projectDirectory);
        var membershipPath = store.Memberships.GetPath(storyId);
        if (!File.Exists(membershipPath)) return;
        // A selected membership is the canonical package boundary. Read it
        // before creating any package canonical roots so unrelated canonical
        // project data cannot produce partial output roots.
        var manifest = store.Memberships.Load(storyId);
        var storyPath = store.Stories.GetPath(storyId);
        if (!File.Exists(storyPath))
            throw new StoryPackageException($"Required canonical resource is missing: {storyPath}");

        foreach (var directory in new[] { "stories", "memberships", "sessions", "tasks" })
            Directory.CreateDirectory(Path.Combine(root, "resources", "canonical", directory));

        CopyCanonical(root, storyPath, storyId, "resources/canonical/stories", required.CanonicalStories);
        CopyCanonical(root, membershipPath, storyId, "resources/canonical/memberships", required.CanonicalMemberships);
        var actors = new ActorRepository(_projectDirectory).ListActors().ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        var items = new ItemRepository(_projectDirectory);
        foreach (var id in manifest.OwnedResources.Actors.Concat(manifest.ReferencedResources.Actors).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!actors.TryGetValue(id, out var actor))
            {
                if (!manifest.OwnedResources.Actors.Contains(id, StringComparer.Ordinal))
                {
                    CopyReference(DgrResourceKind.Actor, id, "actors", required.Actors);
                    continue;
                }
                throw new StoryPackageException($"Required Actor is missing: {id}");
            }
            CheckProviderBytes(DgrResourceKind.Actor, id, actor.SourcePath);
            CopyCanonical(root, actor.SourcePath, id, "actors", required.Actors);
        }
        CopyMembers(manifest.OwnedResources.Sessions, manifest.ReferencedResources.Sessions, store.Sessions.GetPath, "resources/canonical/sessions", required.Sessions);
        CopyMembers(manifest.OwnedResources.Tasks, manifest.ReferencedResources.Tasks, store.Tasks.GetPath, "resources/canonical/tasks", required.Tasks);
        CopyMembers(manifest.OwnedResources.Items, manifest.ReferencedResources.Items, items.GetItemPath, "items", required.Items);
        CopyMembers(manifest.OwnedResources.ItemGroups, manifest.ReferencedResources.ItemGroups, items.GetGroupPath, "item_groups", required.ItemGroups);

        void CheckProviderBytes(DgrResourceKind kind, string id, string source)
        {
            var provider = providers.Resolve(kind, id);
            if (provider is null) return;
            if (!File.ReadAllBytes(source).SequenceEqual(provider.DefinitionBytes))
                throw new StoryPackageException($"Native {kind} '{id}' conflicts with referenced package '{Path.GetFileName(provider.PackagePath)}': definition bytes differ.");
            usedProviders.Add(provider.PackagePath);
        }

        void CopyReference(DgrResourceKind kind, string id, string destination, List<string> paths)
        {
            var resource = providers.Resolve(kind, id) ?? throw new StoryPackageException($"Referenced {kind} definition is missing or ambiguous: {id}");
            var relative = destination + "/" + ResourceAddress.FromKey(id).RelativeDefinitionPath;
            var target = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, resource.DefinitionBytes);
            usedProviders.Add(resource.PackagePath);
            AddUnique(paths, relative);
        }

        void CopyMembers(IEnumerable<string> owned, IEnumerable<string> referenced, Func<string, string> sourcePath, string destination, List<string> paths)
        {
            var requiredIds = owned.ToHashSet(StringComparer.Ordinal);
            foreach (var id in requiredIds.Concat(referenced).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var source = sourcePath(id);
                if (!File.Exists(source) && !requiredIds.Contains(id))
                {
                    var kind = Enum.Parse<DgrResourceKind>(ResourceAddress.FromKey(id).Kind.ToString());
                    CopyReference(kind, id, destination, paths);
                    continue;
                }
                if (File.Exists(source)) CheckProviderBytes(Enum.Parse<DgrResourceKind>(ResourceAddress.FromKey(id).Kind.ToString()), id, source);
                CopyCanonical(root, source, id, destination, paths);
            }
        }
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
        var graph = new CanonicalStoryLogicGraph(2, outgoing);
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
            CanonicalStories = required.CanonicalStories,
            CanonicalMemberships = required.CanonicalMemberships,
            Sessions = required.Sessions,
            Tasks = required.Tasks,
            StoryLogicGraph = relative,
        };
    }

    private void CopyCanonical(string root, string source, string id, string packageDirectory, List<string> paths)
    {
        if (!File.Exists(source)) throw new StoryPackageException($"Required canonical resource is missing: {source}");
        var relative = packageDirectory + "/" + (StoryUid.IsValid(id) ? id + ".json" : ResourceAddress.FromKey(id).RelativeDefinitionPath);
        var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (packageDirectory is "resources/canonical/stories" or "resources/canonical/sessions")
        {
            // Export the same upgraded boundaries and line pages shown by Studio, even before a legacy file is saved.
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, GraphResourceEnvelopeSerializer.Serialize(
                GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(source))));
        }
        else CopyFile(source, target);
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
