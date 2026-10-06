using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed record StoryContentCopyPlan(string ProjectDirectory, string SourceStoryId, string TargetStoryId,
    StoryResourceCopyMap ResourceMap, IReadOnlyDictionary<string, string> StoryNodeIds,
    IReadOnlyList<ProjectFileChange> Changes)
{
    internal string SourceStamp { get; init; } = string.Empty;
}

/// <summary>Append detached owned content into an existing Story without changing either Story UID or project edges.</summary>
public sealed class StoryContentCopyService
{
    private readonly ProjectFileTransaction _transaction;
    public StoryContentCopyService(ProjectFileTransaction? transaction = null) => _transaction = transaction ?? new();

    public StoryContentCopyPlan Prepare(string projectDirectory, string sourceStoryId, string targetStoryId, IReadOnlyDictionary<string, ProjectGraphNodeLayout>? targetLayout = null)
    {
        var root = Path.GetFullPath(projectDirectory);
        var stamp = OfflineProjectStateStamp.Capture(root);
        var sourceUid = StoryUid.Parse(sourceStoryId);
        var targetUid = StoryUid.Parse(targetStoryId);
        if (sourceUid == targetUid) throw new InvalidOperationException("不能把故事复制到自身。");
        var store = new CanonicalProjectGraphStore(root);
        var source = store.Stories.Load(sourceStoryId);
        var target = store.Stories.Load(targetStoryId);
        var targetGraph = target.Graph!;
        var sourceMembers = store.Memberships.Load(sourceStoryId);
        var targetMembers = store.Memberships.Load(targetStoryId);
        var sourceOwned = CurrentProjectInventory.Members(sourceMembers.OwnedResources).ToArray();
        var targetOwned = CurrentProjectInventory.Members(targetMembers.OwnedResources).ToArray();
        var map = StoryResourceCopyMap.Create(sourceUid, targetUid,
            sourceOwned.Select(key => ResourceAddress.FromKey(key.Id)), targetOwned.Select(key => ResourceAddress.FromKey(key.Id)));
        var remap = new ResourceCopyRemapper();
        remap.Add(DgrResourceKind.Story, sourceStoryId, targetStoryId);
        foreach (var pair in map.Copies) remap.Add(Enum.Parse<DgrResourceKind>(pair.Key.Kind.ToString()), pair.Key.ToKey(), pair.Value.ToKey());
        var changes = new List<ProjectFileChange>();
        var layoutPath = new CanonicalGraphLayoutStore(root).LayoutPath;
        var layoutOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
        var layouts = File.Exists(layoutPath)
            ? JsonSerializer.Deserialize<CanonicalGraphLayoutDocument>(File.ReadAllText(layoutPath), layoutOptions) ?? throw new InvalidDataException("Invalid graph layout.")
            : new CanonicalGraphLayoutDocument();
        if (layouts.SchemaVersion != 1 || layouts.Graphs is null || layouts.Frames is null) throw new InvalidDataException("Invalid graph layout.");
        var publicPorts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var actors = new ActorRepository(root);
        var items = new ItemRepository(root);
        foreach (var owned in sourceOwned)
        {
            var id = remap.Resolve(owned.Kind, owned.Id);
            switch (owned.Kind)
            {
                case DgrResourceKind.Actor:
                    Add(actors.GetActorPath(id), ActorSerializer.Serialize(remap.Rewrite(actors.LoadActor(owned.Id).ToResource()), ActorIdPolicy.ExistingResource), fresh: true);
                    break;
                case DgrResourceKind.Item:
                    Add(items.GetItemPath(id), ItemSerializer.Serialize(remap.Rewrite(items.LoadItem(owned.Id))), fresh: true);
                    break;
                case DgrResourceKind.ItemGroup:
                    Add(items.GetGroupPath(id), ItemSerializer.Serialize(remap.Rewrite(items.LoadGroup(owned.Id))), fresh: true);
                    break;
                case DgrResourceKind.Session:
                case DgrResourceKind.Task:
                    var repository = owned.Kind == DgrResourceKind.Session ? store.Sessions : store.Tasks;
                    var envelope = remap.Rewrite(repository.Load(owned.Id));
                    envelope.Graph = Clone(envelope, out var internalIds, out var ports);
                    CopyLayout(envelope.ResourceKind, owned.Id, id, internalIds, append: false);
                    publicPorts.Add(owned.Id, ports);
                    Add(repository.GetPath(id), envelope.ToJson(), fresh: true);
                    break;
            }
        }
        var mediaSources = new StoryPackageRequiredResources
        {
            Actors = sourceOwned.Where(key => key.Kind == DgrResourceKind.Actor).Select(key => actors.GetActorPath(key.Id)).ToList(),
            Sessions = sourceOwned.Where(key => key.Kind == DgrResourceKind.Session).Select(key => store.Sessions.GetPath(key.Id)).ToList(),
        };
        foreach (var mediaRef in StoryPackageMedia.Collect(mediaSources, File.ReadAllText))
        {
            // Project media is content addressed and survives source Story deletion; both copies retain its own bytes.
            using var media = File.OpenRead(Path.Combine(root, "resources", mediaRef));
            StoryPackageMedia.Validate(mediaRef, media);
        }
        var sourceGraph = source.Graph!;
        var copy = Clone(remap.Rewrite(source), out var nodeIds, out _);
        foreach (var old in sourceGraph.Nodes.Where(node => node.Type is "session" or "task"))
        {
            var node = copy.Nodes.Single(node => node.Id == nodeIds[old.Id]);
            var oldId = old.Properties["resource_id"].GetString()!;
            publicPorts.TryGetValue(oldId, out var ports);
            for (var index = 0; index < node.Ports.Count; index++)
            {
                var port = node.Ports[index];
                var prior = port.Id;
                var final = ports?.GetValueOrDefault(old.Ports[index].Id, old.Ports[index].Id) ?? old.Ports[index].Id;
                foreach (var edge in copy.Connections)
                {
                    if (edge.FromNodeId == node.Id && edge.FromPortId == prior) edge.FromPortId = final;
                    if (edge.ToNodeId == node.Id && edge.ToPortId == prior) edge.ToPortId = final;
                }
                port.Id = final;
            }
        }
        foreach (var kind in new[] { GraphInterfaceKind.Flow, GraphInterfaceKind.Logic })
        {
            var next = targetGraph.Nodes.Where(n => PublicOutputSchema.IsOutput(n) && PublicOutputSchema.Kind(n) == kind)
                .Select(PublicOutputSchema.Order).DefaultIfEmpty(-1).Max() + 1;
            foreach (var output in copy.Nodes.Where(n => PublicOutputSchema.IsOutput(n) && PublicOutputSchema.Kind(n) == kind).OrderBy(PublicOutputSchema.Order))
                output.Properties["display_order"] = JsonSerializer.SerializeToElement(next++);
        }
        target.Graph = new GraphDocument(targetGraph.Nodes.Concat(copy.Nodes), targetGraph.Connections.Concat(copy.Connections));
        Add(store.Stories.GetPath(targetStoryId), target.ToJson());
        CopyLayout(GraphResourceKind.Story, sourceStoryId, targetStoryId, nodeIds, append: true);
        Add(layoutPath, JsonSerializer.Serialize(layouts, layoutOptions));
        var incoming = remap.Rewrite(sourceMembers);
        targetMembers.OwnedResources = Merge(targetMembers.OwnedResources, incoming.OwnedResources);
        targetMembers.ReferencedResources = Merge(targetMembers.ReferencedResources, incoming.ReferencedResources);
        // References to content already owned by the target collapse back to its existing definition.
        var refs = targetMembers.ReferencedResources;
        var allOwned = targetMembers.OwnedResources;
        refs.Actors.RemoveAll(allOwned.Actors.Contains); refs.Items.RemoveAll(allOwned.Items.Contains);
        refs.ItemGroups.RemoveAll(allOwned.ItemGroups.Contains); refs.Sessions.RemoveAll(allOwned.Sessions.Contains); refs.Tasks.RemoveAll(allOwned.Tasks.Contains);
        targetMembers.ReferencedResources = refs;
        Add(store.Memberships.GetPath(targetStoryId), targetMembers.ToJson());
        if (stamp != OfflineProjectStateStamp.Capture(root)) throw new IOException("Project changed while preparing Story content copy.");
        return new(root, sourceStoryId, targetStoryId, map, nodeIds, changes.AsReadOnly()) { SourceStamp = stamp };

        void CopyLayout(GraphResourceKind kind, string oldId, string newId, IReadOnlyDictionary<string, string> ids, bool append)
        {
            var oldKey = CanonicalGraphLayoutStore.BuildGraphKey(kind, oldId);
            var newKey = CanonicalGraphLayoutStore.BuildGraphKey(kind, newId);
            var sourcePositions = layouts.Graphs.GetValueOrDefault(oldKey) ?? new();
            var targetPositions = append ? layouts.Graphs.GetValueOrDefault(newKey) ?? new() : new Dictionary<string, ProjectGraphNodeLayout>();
            if (append && targetLayout is not null)
                foreach (var pair in targetLayout)
                    if (targetGraph.Nodes.Any(node => node.Id == pair.Key)) targetPositions[pair.Key] = new ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y };
            var offset = append ? targetPositions.Values.Select(position => position.X).DefaultIfEmpty(0).Max() + 420
                - sourcePositions.Values.Select(position => position.X).DefaultIfEmpty(0).Min() : 0;
            var index = 0;
            foreach (var pair in ids)
            {
                var position = sourcePositions.GetValueOrDefault(pair.Key) ?? new ProjectGraphNodeLayout { X = index % 4 * 260, Y = index / 4 * 180 };
                targetPositions.Add(pair.Value, new ProjectGraphNodeLayout { X = position.X + offset, Y = position.Y });
                index++;
                string ScreenPath(string resource, string node) => Path.Combine(root, "resources", "editor", "screen-layers",
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resource + "\n" + node))) + ".json");
                var sourceMetadata = ScreenPath(oldKey, pair.Key);
                if (File.Exists(sourceMetadata)) Add(ScreenPath(newKey, pair.Value), File.ReadAllText(sourceMetadata), fresh: true);
            }
            layouts.Graphs[newKey] = targetPositions;
            var frames = layouts.Frames.GetValueOrDefault(oldKey) ?? [];
            var frameIds = frames.ToDictionary(frame => frame.Id, _ => "group_" + Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
            var copiedFrames = frames.Select(frame => frame with
            {
                Id = frameIds[frame.Id], X = frame.X + offset,
                Members = frame.Members.Where(ids.ContainsKey).Select(id => ids[id]).ToArray(),
                Groups = frame.Groups.Where(frameIds.ContainsKey).Select(id => frameIds[id]).ToArray(),
            });
            layouts.Frames[newKey] = (append ? layouts.Frames.GetValueOrDefault(newKey) ?? [] : new List<GraphCommentFrame>()).Concat(copiedFrames).ToList();
        }

        void Add(string path, string json, bool fresh = false)
        {
            var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (fresh && before is not null) throw new IOException("Copied resource destination is already occupied: " + path);
            changes.Add(new(Path.GetRelativePath(root, path), before, Encoding.UTF8.GetBytes(json)));
        }
    }

    public void Apply(StoryContentCopyPlan plan) => _transaction.Apply(plan.ProjectDirectory, plan.Changes, () =>
    {
        if (plan.SourceStamp != OfflineProjectStateStamp.Capture(plan.ProjectDirectory)) throw new IOException("Project changed after Story copy preview.");
    });
    public void Undo(StoryContentCopyPlan plan) => _transaction.Apply(plan.ProjectDirectory,
        plan.Changes.Select(change => new ProjectFileChange(change.RelativePath, change.DesiredBytes, change.ExpectedBytes)).ToArray(), () => { });
    public void Redo(StoryContentCopyPlan plan) => _transaction.Apply(plan.ProjectDirectory, plan.Changes, () => { });

    private static CanonicalStoryMembershipSet Merge(CanonicalStoryMembershipSet first, CanonicalStoryMembershipSet second) => new()
    {
        Actors = first.Actors.Union(second.Actors, StringComparer.Ordinal).ToList(),
        Items = first.Items.Union(second.Items, StringComparer.Ordinal).ToList(),
        ItemGroups = first.ItemGroups.Union(second.ItemGroups, StringComparer.Ordinal).ToList(),
        Sessions = first.Sessions.Union(second.Sessions, StringComparer.Ordinal).ToList(),
        Tasks = first.Tasks.Union(second.Tasks, StringComparer.Ordinal).ToList(),
    };

    private static GraphDocument Clone(GraphResourceEnvelope envelope, out IReadOnlyDictionary<string, string> nodeIds, out Dictionary<string, string> ports)
    {
        var graph = envelope.Graph!;
        var scope = GraphResourceScopeAdapter.GetScope(envelope.ResourceKind);
        var clone = new GraphClipboardSnapshot(scope, graph, graph.Nodes.Select(node => node.Id)).CloneForPaste(scope, out nodeIds, includeFixed: true);
        ports = new(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            var newId = nodeIds[node.Id];
            var copied = clone.Nodes.Single(value => value.Id == newId);
            if (node.Properties.TryGetValue("port_id", out var boundary) && boundary.ValueKind == JsonValueKind.String)
                ports[boundary.GetString()!] = copied.Properties["port_id"].GetString()!;
        }
        return clone;
    }
}
