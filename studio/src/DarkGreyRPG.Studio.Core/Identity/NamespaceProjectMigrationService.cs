using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Identity;

public sealed class NamespaceMigrationPreview
{
    internal NamespaceMigrationPreview(string root, NamespaceMigrationPlan plan,
        Dictionary<string, byte[]> sources, List<NamespaceFileChange> changes)
    {
        ProjectDirectory = root;
        FinalProject = plan.Result;
        Sources = sources;
        Changes = changes;
        Renames = new ReadOnlyDictionary<DgrResourceKey, string>(plan.Renames.Entries.ToDictionary(pair => pair.Key, pair => pair.Value));
    }
    public string ProjectDirectory { get; }
    public string GlobalNamespace => FinalProject.Policy!.GlobalNamespace;
    public IReadOnlyDictionary<DgrResourceKey, string> Renames { get; }
    public int ChangedFileCount => Changes.Count;
    public string Resolve(DgrResourceKind kind, string id) => Renames.GetValueOrDefault(new(kind, id), id);
    internal NamespaceProjectSnapshot FinalProject { get; }
    internal Dictionary<string, byte[]> Sources { get; }
    internal List<NamespaceFileChange> Changes { get; }
}

/// <summary>Explicit preview/apply boundary. Opening and previewing a project never migrate it implicitly.</summary>
public sealed class NamespaceProjectMigrationService
{
    private readonly NamespaceFileTransaction _transaction;
    public NamespaceProjectMigrationService(NamespaceFileTransaction? transaction = null) => _transaction = transaction ?? new();

    public NamespaceMigrationPreview PreviewGlobal(string projectDirectory, string newNamespace)
        => Preview(projectDirectory, source => NamespaceMigrationPlanner.ChangeGlobal(source, newNamespace));
    public NamespaceMigrationPreview PreviewCustom(string projectDirectory, string storyId, string customNamespace)
        => Preview(projectDirectory, source => NamespaceMigrationPlanner.ChangeStory(source, storyId, customNamespace));
    public NamespaceMigrationPreview PreviewReturnToGlobal(string projectDirectory, string storyId)
        => Preview(projectDirectory, source => NamespaceMigrationPlanner.ReturnToGlobal(source, storyId));

    public NamespaceMigrationPreview PreviewResourceRename(string projectDirectory, DgrResourceKind kind, string id, string newId, string? displayName = null, IReadOnlyList<string>? tags = null)
        => Preview(projectDirectory, source =>
        {
            if (!DgrResourceId.IsFullId(newId)) throw new ArgumentException("资源 ID 格式无效。");
            var inventory = NamespaceMigrationPlanner.Inventory(source).ToArray();
            if (!inventory.Contains(new DgrResourceKey(kind, id))) throw new InvalidOperationException("只能重命名本项目的原生资源。");
            if (Packaging.OfflineProviderCatalog.Load(projectDirectory).Resources.Any(resource => resource.Kind == kind && resource.Id == newId))
                throw new InvalidOperationException("目标 ID 已被引用故事包中的资源使用。");
            var map = new ResourceRenameMap();
            map.Add(kind, id, newId);
            map.ValidateCollisions(inventory);
            var result = new NamespaceProjectSnapshot(
                source.Graphs.Select(map.Rewrite).ToArray(), source.Memberships.Select(map.Rewrite).ToArray(),
                source.Actors.Select(map.Rewrite).ToArray(), source.Items.Select(map.Rewrite).ToArray(),
                map.Rewrite(source.Logic), source.Policy);
            if (displayName is not null)
            {
                var name = displayName.Trim();
                if (name.Length == 0) throw new ArgumentException("显示名称不能为空。");
                if (kind == DgrResourceKind.Actor)
                {
                    var actor = result.Actors.Single(actor => actor.Id == newId);
                    actor.DisplayName = name;
                    if (tags is not null) actor.Tags = tags.ToList();
                }
                if (kind is DgrResourceKind.Item or DgrResourceKind.ItemGroup)
                    result = result with { Items = result.Items.Select(item =>
                    {
                        var itemKind = item is IndividualItemResource ? DgrResourceKind.Item : DgrResourceKind.ItemGroup;
                        if (itemKind != kind || item.Id != newId) return item;
                        var json = JsonNode.Parse(ItemSerializer.Serialize(item))!.AsObject();
                        json["display_name"] = name;
                        if (tags is not null) json["tags"] = System.Text.Json.JsonSerializer.SerializeToNode(tags);
                        return ItemSerializer.Deserialize(json.ToJsonString());
                    }).ToArray() };
                if (kind is DgrResourceKind.Session or DgrResourceKind.Task)
                    result = result with { Graphs = result.Graphs.Select(graph =>
                    {
                        if (ResourceRenameMap.Kind(graph.ResourceKind) != kind || graph.Id != newId) return graph;
                        var json = JsonNode.Parse(graph.ToJson())!.AsObject();
                        json["display_name"] = name;
                        if (tags is not null) json["tags"] = System.Text.Json.JsonSerializer.SerializeToNode(tags);
                        return GraphResourceEnvelopeSerializer.Deserialize(json.ToJsonString());
                    }).ToArray() };
            }
            return new NamespaceMigrationPlan(map, result);
        });

    public void Apply(NamespaceMigrationPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _transaction.Apply(preview.ProjectDirectory, preview.Changes, () =>
        {
            var current = Read(preview.ProjectDirectory).Files;
            if (current.Count != preview.Sources.Count || preview.Sources.Any(pair =>
                    !current.TryGetValue(pair.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(pair.Value)))
                throw new InvalidOperationException("Project changed after the Namespace preview; preview again.");
            NamespaceProjectValidator.Validate(preview.FinalProject);
        });
    }

    /// <summary>One-unit undo, accepted only while every migrated source still has its committed bytes.</summary>
    public void Undo(NamespaceMigrationPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var expected = preview.Sources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var change in preview.Changes)
            if (change.DesiredBytes is null) expected.Remove(change.RelativePath);
            else expected[change.RelativePath] = change.DesiredBytes;
        var inverse = preview.Changes.Select(change => new NamespaceFileChange(change.RelativePath, change.DesiredBytes, change.ExpectedBytes)).ToArray();
        _transaction.Apply(preview.ProjectDirectory, inverse, () =>
        {
            var current = Read(preview.ProjectDirectory).Files;
            if (current.Count != expected.Count || expected.Any(pair => !current.TryGetValue(pair.Key, out var bytes)
                    || !bytes.AsSpan().SequenceEqual(pair.Value)))
                throw new InvalidOperationException("Project changed after migration; Namespace undo would overwrite newer work.");
        });
    }

    public static NamespaceProjectSnapshot ReadProject(string projectDirectory) => Read(Path.GetFullPath(projectDirectory)).Project;

    private static NamespaceMigrationPreview Preview(string directory, Func<NamespaceProjectSnapshot, NamespaceMigrationPlan> plan)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var source = Read(root);
        var migration = plan(source.Project);
        NamespaceProjectValidator.Validate(migration.Result);
        var desired = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        void Output(string oldPath, string newPath, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            if (oldPath == newPath && source.Files.TryGetValue(oldPath, out var oldBytes)
                && SameJson(oldBytes, bytes))
                bytes = oldBytes;
            if (!desired.TryAdd(newPath, bytes)) throw new InvalidOperationException($"Namespace path collision at '{newPath}'.");
        }
        foreach (var before in source.Project.Graphs)
        {
            var kind = ResourceRenameMap.Kind(before.ResourceKind);
            var after = migration.Result.Graphs.Single(graph => graph.ResourceKind == before.ResourceKind && graph.Id == migration.Renames.Resolve(kind, before.Id));
            var oldPath = source.ResourcePaths[new(kind, before.Id)];
            var folder = before.ResourceKind switch { GraphResourceKind.Story => "stories", GraphResourceKind.Session => "sessions", _ => "tasks" };
            var path = after.Id == before.Id ? oldPath : $"resources/canonical/{folder}/{DgrResourceId.RelativeJsonPath(after.Id)}";
            Output(oldPath, path, after.ToJson());
        }
        foreach (var before in source.Project.Memberships)
        {
            var after = migration.Result.Memberships.Single(member => member.StoryId == migration.Renames.Resolve(DgrResourceKind.Story, before.StoryId));
            var oldPath = source.MembershipPaths[before.StoryId];
            Output(oldPath, after.StoryId == before.StoryId ? oldPath : $"resources/canonical/memberships/{DgrResourceId.RelativeJsonPath(after.StoryId)}", after.ToJson());
        }
        foreach (var before in source.Project.Actors)
        {
            var after = migration.Result.Actors.Single(actor => actor.Id == migration.Renames.Resolve(DgrResourceKind.Actor, before.Id));
            var oldPath = source.ResourcePaths[new(DgrResourceKind.Actor, before.Id)];
            Output(oldPath, after.Id == before.Id ? oldPath : $"actors/{DgrResourceId.RelativeJsonPath(after.Id)}", ActorSerializer.Serialize(after, ActorIdPolicy.ExistingResource));
        }
        foreach (var before in source.Project.Items)
        {
            var kind = before is IndividualItemResource ? DgrResourceKind.Item : DgrResourceKind.ItemGroup;
            var after = migration.Result.Items.Single(item => item.GetType() == before.GetType() && item.Id == migration.Renames.Resolve(kind, before.Id));
            var oldPath = source.ResourcePaths[new(kind, before.Id)];
            Output(oldPath, after.Id == before.Id ? oldPath : $"{(kind == DgrResourceKind.Item ? "items" : "item_groups")}/{DgrResourceId.RelativeJsonPath(after.Id)}", ItemSerializer.Serialize(after));
        }
        const string logicPath = "resources/canonical/story_logic_graph.json";
        if (source.Files.ContainsKey(logicPath)) Output(logicPath, logicPath, JsonSerializer.Serialize(migration.Result.Logic));
        foreach (var path in LayoutPaths)
            if (source.Files.TryGetValue(path, out var bytes)) Output(path, path, RewriteLayout(path, bytes, migration.Renames));
        desired[NamespacePolicyStore.RelativePath] = NamespacePolicyStore.Encode(migration.Result.Policy!);
        var changes = source.Files.Keys.Union(desired.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal).Select(path => new NamespaceFileChange(path, source.Files.GetValueOrDefault(path), desired.GetValueOrDefault(path)))
            .Where(change => change.ExpectedBytes is null || change.DesiredBytes is null || !change.ExpectedBytes.AsSpan().SequenceEqual(change.DesiredBytes))
            .ToList();
        return new(root, migration, source.Files, changes);
    }

    private static string RewriteLayout(string path, byte[] bytes, ResourceRenameMap map)
    {
        var root = JsonNode.Parse(bytes)?.AsObject() ?? throw new InvalidDataException("Editor layout is missing.");
        var field = path.EndsWith("studio_layout.json", StringComparison.Ordinal) ? "graphs" : "nodes";
        if (root[field] is not JsonObject old) return root.ToJsonString();
        var updated = new JsonObject();
        foreach (var pair in old)
        {
            var key = pair.Key;
            if (field == "nodes") key = map.Resolve(DgrResourceKind.Story, key);
            else
            {
                var split = key.IndexOf(':');
                if (split > 0 && key[..split] is "story" or "session" or "task")
                    key = key[..(split + 1)] + map.Resolve(ResourceRenameMap.Kind(GraphResourceEnvelopeSerializer.ParseResourceKind(key[..split])), key[(split + 1)..]);
            }
            if (updated.ContainsKey(key)) throw new InvalidDataException($"Editor layout identity collision at '{key}'.");
            updated.Add(key, pair.Value?.DeepClone());
        }
        root[field] = updated;
        return root.ToJsonString(new() { WriteIndented = true });
    }

    private static readonly string[] LayoutPaths = ["resources/editor/studio_layout.json", "resources/editor/story-graph-layout.json"];

    private static bool SameJson(byte[] left, byte[] right)
    {
        using var leftJson = JsonDocument.Parse(left);
        using var rightJson = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(leftJson.RootElement, rightJson.RootElement);
    }

    private static Source Read(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<DgrResourceKey, string>();
        var membershipPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        byte[] ReadFile(string path)
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new InvalidDataException("Resource escaped project.");
            var bytes = File.ReadAllBytes(path);
            if (!files.TryAdd(relative, bytes)) throw new InvalidDataException("Duplicate source resource path.");
            return bytes;
        }
        string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
        var store = new CanonicalProjectGraphStore(root);
        var graphs = new List<GraphResourceEnvelope>();
        foreach (var repository in new[] { store.Stories, store.Sessions, store.Tasks })
            foreach (var info in repository.List())
            {
                var graph = GraphResourceEnvelopeSerializer.Deserialize(Encoding.UTF8.GetString(ReadFile(info.SourcePath)));
                graphs.Add(graph);
                paths.Add(new(ResourceRenameMap.Kind(graph.ResourceKind), graph.Id), Relative(info.SourcePath));
            }
        var memberships = new List<CanonicalStoryMembershipManifest>();
        foreach (var info in store.Memberships.List())
        {
            memberships.Add(CanonicalStoryMembershipSerializer.Deserialize(Encoding.UTF8.GetString(ReadFile(info.SourcePath))));
            membershipPaths.Add(info.StoryId, Relative(info.SourcePath));
        }
        var actors = new List<ActorResource>();
        // Avoid legacy repository List side effects when optional directories are absent.
        if (Directory.Exists(Path.Combine(root, "actors"))) foreach (var info in new ActorRepository(root).ListActors())
        {
            var actor = ActorSerializer.Deserialize(Encoding.UTF8.GetString(ReadFile(info.SourcePath)));
            actors.Add(actor);
            paths.Add(new(DgrResourceKind.Actor, actor.Id), Relative(info.SourcePath));
        }
        var itemRepository = new ItemRepository(root);
        var items = new List<ItemResource>();
        foreach (var info in itemRepository.ListItems().Concat(itemRepository.ListGroups()))
        {
            var item = ItemSerializer.Deserialize(Encoding.UTF8.GetString(ReadFile(info.Path)));
            items.Add(item);
            paths.Add(new(item is IndividualItemResource ? DgrResourceKind.Item : DgrResourceKind.ItemGroup, item.Id), Relative(info.Path));
        }
        var logic = store.StoryLogicGraph.Load();
        if (File.Exists(store.StoryLogicGraph.Path)) ReadFile(store.StoryLogicGraph.Path);
        var policyPath = Path.Combine(root, NamespacePolicyStore.RelativePath);
        var policy = File.Exists(policyPath) ? NamespacePolicyStore.Decode(ReadFile(policyPath)) : null;
        foreach (var path in LayoutPaths)
            if (File.Exists(Path.Combine(root, path))) ReadFile(Path.Combine(root, path));
        return new(new(graphs, memberships, actors, items, logic, policy), files, paths, membershipPaths);
    }

    private sealed record Source(NamespaceProjectSnapshot Project, Dictionary<string, byte[]> Files,
        Dictionary<DgrResourceKey, string> ResourcePaths, Dictionary<string, string> MembershipPaths);
}
