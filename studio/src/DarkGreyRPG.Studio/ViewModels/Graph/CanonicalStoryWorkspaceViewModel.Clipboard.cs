using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalStoryWorkspaceViewModel
{
    public Func<IEnumerable<CanonicalStoryWorkspaceViewModel>>? OpenProjectWorkspaces { get; set; }
    private static void AddScreenMetadataChanges(List<NamespaceFileChange> changes, string root, string resource,
        IReadOnlyDictionary<string, byte[]?>? metadata, IReadOnlyDictionary<string, string> ids)
    {
        foreach (var pair in metadata ?? new Dictionary<string, byte[]?>())
        {
            if (!ids.TryGetValue(pair.Key, out var id)) continue;
            var path = ScreenLayerEditorState.MetadataPath(root, resource, id);
            changes.Add(new(Path.GetRelativePath(root, path), File.Exists(path) ? File.ReadAllBytes(path) : null, pair.Value));
        }
    }

    public (Action Undo, Action Redo) PrepareScreenMetadata(GraphEditorHostViewModel host,
        IReadOnlyDictionary<string, string> ids, bool parameters)
    {
        if (MediaProjectDirectory is not { } root) return (() => { }, () => { });
        var changes = new List<NamespaceFileChange>();
        AddScreenMetadataChanges(changes, root, host.AuthoringResourceKey,
            parameters ? Clipboard.ParameterScreenMetadata : Clipboard.NodeScreenMetadata, ids);
        var inverse = changes.Select(c => new NamespaceFileChange(c.RelativePath, c.DesiredBytes, c.ExpectedBytes)).ToArray();
        void Apply(IEnumerable<NamespaceFileChange> edits) { new NamespaceFileTransaction().Apply(root, edits.ToArray(), () => { }); ScreenLayerEditorState.Reload(host); }
        return (() => Apply(inverse), () => Apply(changes));
    }
    public (Action Undo, Action Redo) PrepareCopiedResources(GraphDocument pasted, IReadOnlyDictionary<string, string> nodeIds)
    {
        var aggregates = pasted.Nodes.Where(n => n.Type is "session" or "task").ToArray();
        if (aggregates.Length == 0) return (() => { }, () => { });
        var root = MediaProjectDirectory ?? throw new InvalidOperationException("项目未打开。");
        var store = new CanonicalProjectGraphStore(root);
        var changes = new List<NamespaceFileChange>();
        var created = new List<CanonicalGraphClipboard.Resource>();
        var membership = store.Memberships.Load(StoryEditor.Id);
        var owned = membership.OwnedResources; var order = membership.DisplayOrder;
        // Persist the visible baseline first; an initially absent order must
        // not put newly pasted resources ahead of existing resources on reopen.
        foreach (var item in SessionItems)
            if (!order.Sessions.Contains(item.Id)) order.Sessions.Add(item.Id);
        foreach (var item in TaskItems)
            if (!order.Tasks.Contains(item.Id)) order.Tasks.Add(item.Id);
        var original = Clipboard.Nodes!.Read();
        var cloned = new Dictionary<(GraphResourceKind, string), (GraphResourceEnvelope Envelope, Dictionary<string, string> Ports)>();
        var reserved = store.Sessions.List().Select(r => r.Id).Concat(store.Tasks.List().Select(r => r.Id)).ToHashSet(StringComparer.Ordinal);
        var layoutPath = new CanonicalGraphLayoutStore(root).LayoutPath;
        var layoutBefore = File.Exists(layoutPath) ? File.ReadAllBytes(layoutPath) : null;
        var layoutJson = layoutBefore is null ? new JsonObject { ["schema_version"] = 1, ["graphs"] = new JsonObject() } : JsonNode.Parse(layoutBefore)!.AsObject();
        var layoutGraphs = layoutJson["graphs"]?.AsObject() ?? throw new InvalidOperationException("布局文件无效。");
        foreach (var aggregate in aggregates)
        {
            var kind = aggregate.Type == "session" ? GraphResourceKind.Session : GraphResourceKind.Task;
            var oldId = aggregate.Properties["resource_id"].GetString()!;
            if (!cloned.TryGetValue((kind, oldId), out var clone))
            {
                var source = Clipboard.NodeResources[(kind, oldId)];
                var envelope = GraphResourceEnvelope.FromJson(source.Envelope.ToJson());
                var sourceGraph = envelope.Graph!;
                var scope = GraphResourceScopeAdapter.GetScope(kind);
                var graph = new GraphClipboardSnapshot(scope, sourceGraph, sourceGraph.Nodes.Select(n => n.Id)).CloneForPaste(scope, out var internalIds, true);
                string id = oldId + "_copy"; int suffix = 0;
                while (!reserved.Add(id)) id = oldId + "_copy" + ++suffix;
                envelope.Id = id; envelope.Graph = graph;
                AddScreenMetadataChanges(changes, root, CanonicalGraphLayoutStore.BuildGraphKey(kind, id), source.ScreenMetadata, internalIds);
                var ports = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var oldNode in sourceGraph.Nodes)
                {
                    var newNode = graph.Nodes.Single(n => n.Id == internalIds[oldNode.Id]);
                    for (int i = 0; i < oldNode.Ports.Count; i++) ports[oldNode.Ports[i].Id] = newNode.Ports[i].Id;
                    if (oldNode.Properties.TryGetValue("port_id", out var boundary) && boundary.ValueKind == JsonValueKind.String)
                        ports[boundary.GetString()!] = newNode.Properties["port_id"].GetString()!;
                }
                clone = (envelope, ports); cloned[(kind, oldId)] = clone;
                var positions = source.Layout.ToDictionary(pair => internalIds[pair.Key], pair => pair.Value);
                created.Add(new(envelope, positions));
                var repository = kind == GraphResourceKind.Session ? store.Sessions : store.Tasks;
                changes.Add(new(Path.GetRelativePath(root, repository.GetPath(id)), null, Encoding.UTF8.GetBytes(envelope.ToJson())));
                if (kind == GraphResourceKind.Session) { owned.Sessions.Add(id); order.Sessions.Add(id); }
                else { owned.Tasks.Add(id); order.Tasks.Add(id); }
                var nodes = new JsonObject();
                foreach (var position in positions) nodes[position.Key] = new JsonObject { ["x"] = position.Value.X, ["y"] = position.Value.Y };
                layoutGraphs[CanonicalGraphLayoutStore.BuildGraphKey(kind, id)] = nodes;
            }
            var oldNodeId = nodeIds.Single(pair => pair.Value == aggregate.Id).Key;
            var oldAggregate = original.Nodes.Single(n => n.Id == oldNodeId);
            for (int i = 0; i < aggregate.Ports.Count; i++)
            {
                var port = aggregate.Ports[i]; var oldPort = oldAggregate.Ports[i].Id;
                string mapped = clone.Ports.GetValueOrDefault(oldPort, oldPort), temporary = port.Id;
                foreach (var edge in pasted.Connections)
                {
                    if (edge.FromNodeId == aggregate.Id && edge.FromPortId == temporary) edge.FromPortId = mapped;
                    if (edge.ToNodeId == aggregate.Id && edge.ToPortId == temporary) edge.ToPortId = mapped;
                }
                port.Id = mapped;
            }
            aggregate.Properties["resource_id"] = JsonSerializer.SerializeToElement(clone.Envelope.Id);
        }
        membership.OwnedResources = owned; membership.DisplayOrder = order;
        var membershipPath = store.Memberships.GetPath(StoryEditor.Id);
        changes.Add(new(Path.GetRelativePath(root, membershipPath), File.ReadAllBytes(membershipPath), Encoding.UTF8.GetBytes(membership.ToJson())));
        var inverse = changes.Select(c => new NamespaceFileChange(c.RelativePath, c.DesiredBytes, c.ExpectedBytes)).ToArray();
        void ApplyFiles(bool forward)
        {
            // Saving another graph may rewrite this shared sidecar between paste
            // and undo. Merge only the copied resource keys into its current version.
            var currentBytes = File.Exists(layoutPath) ? File.ReadAllBytes(layoutPath) : null;
            var current = currentBytes is null ? new JsonObject { ["schema_version"] = 1, ["graphs"] = new JsonObject() } : JsonNode.Parse(currentBytes)!.AsObject();
            var currentGraphs = current["graphs"]!.AsObject();
            var originalGraphs = layoutBefore is null ? new JsonObject() : JsonNode.Parse(layoutBefore)!["graphs"]!.AsObject();
            foreach (var resource in created)
            {
                var key = CanonicalGraphLayoutStore.BuildGraphKey(resource.Envelope.ResourceKind, resource.Envelope.Id);
                var value = forward ? layoutGraphs[key] : originalGraphs[key];
                if (value is null) currentGraphs.Remove(key); else currentGraphs[key] = value.DeepClone();
            }
            var layoutChange = new NamespaceFileChange(Path.GetRelativePath(root, layoutPath), currentBytes, Encoding.UTF8.GetBytes(current.ToJsonString()));
            new NamespaceFileTransaction().Apply(root, (forward ? changes : inverse.AsEnumerable()).Append(layoutChange).ToArray(), () => { });
        }
        void Redo()
        {
            ApplyFiles(true);
            try { foreach (var resource in created) AddClipboardResource(resource); }
            catch { foreach (var resource in created) RemoveClipboardResource(resource.Envelope.ResourceKind, resource.Envelope.Id); ApplyFiles(false); throw; }
        }
        void Undo()
        {
            ApplyFiles(false);
            foreach (var resource in created) RemoveClipboardResource(resource.Envelope.ResourceKind, resource.Envelope.Id);
        }
        return (Undo, Redo);
    }

    public bool PasteAggregateParameters(string nodeId, Func<string, bool> confirm)
    {
        var placement = StoryEditor.Host.Graph.Nodes.Single(n => n.Id == nodeId);
        var sourceNode = Clipboard.Parameters!.Read().Nodes.Single();
        var kind = placement.Type == "session" ? GraphResourceKind.Session : GraphResourceKind.Task;
        string targetId = placement.Properties["resource_id"].GetString()!, sourceId = sourceNode.Properties["resource_id"].GetString()!;
        var target = Editors.Single(e => e.Id == targetId && e.ResourceKind == kind);
        if (!IsWritableEditor(target)) throw new InvalidOperationException("引用包中的资源不能修改。");
        var source = Clipboard.ParameterResources[(kind, sourceId)];
        var before = target.CreateSnapshot(); var after = target.CreateSnapshot();
        var scope = target.Host.Scope;
        var sourceGraph = source.Envelope.Graph!;
        after.Graph = new GraphClipboardSnapshot(scope, sourceGraph, sourceGraph.Nodes.Select(n => n.Id)).CloneForPaste(scope, out var ids, true);
        var beforeLayout = target.CreateLayoutSnapshot();
        var afterLayout = source.Layout.ToDictionary(p => ids[p.Key], p => p.Value);
        var root = MediaProjectDirectory!; var store = new CanonicalProjectGraphStore(root);
        var open = (OpenProjectWorkspaces?.Invoke() ?? [this]).Append(this).Distinct().ToArray();
        var retainedChanges = new List<(GraphEditorHostViewModel Host, GraphDocument Before, GraphDocument After, IReadOnlyDictionary<string, GraphEditorNodePosition> Layout)>();
        var resourceHosts = open.SelectMany(w => w.Editors).Where(e => e.ResourceKind == kind && e.Id == targetId)
            .Distinct().Select(e => (Host: e.Host, Before: e.CreateSnapshot().Graph!, Layout: e.CreateLayoutSnapshot())).ToArray();
        var changes = new List<NamespaceFileChange>(); var references = new List<string>(); var removed = new List<string>();
        AddScreenMetadataChanges(changes, root, target.Host.AuthoringResourceKey, source.ScreenMetadata, ids);
        GraphDocument? nextStory = null;
        foreach (var info in store.Stories.List())
        {
            var retained = open.FirstOrDefault(w => w.StoryEditor.Id == info.Id)?.StoryEditor;
            var story = retained?.CreateSnapshot() ?? store.Stories.Load(info.Id);
            var previousGraph = GraphDocument.FromJson(story.Graph!.ToJson());
            var graph = story.Graph!; bool changed = false;
            foreach (var node in graph.Nodes.Where(n => n.Type == placement.Type && n.Properties.TryGetValue("resource_id", out var id) && id.GetString() == targetId).ToArray())
            {
                references.Add($"{story.DisplayName} / {node.DisplayName} ({node.Id})");
                var projected = CanonicalAggregateNodeFactory.Create(after, node.Id);
                if (!projected.IsSuccess || projected.Candidate is null) throw new InvalidOperationException(string.Join("\n", projected.Issues.Select(i => i.Message)));
                projected.Candidate.DisplayName = node.DisplayName;
                graph = GraphClipboardSnapshot.PasteParameters(GraphScope.StoryFlow, graph, projected.Candidate, node.Id, out var lost, true);
                removed.AddRange(lost.Select(c => $"{story.DisplayName}: {c.FromNodeId}/{c.FromPortId} → {c.ToNodeId}/{c.ToPortId}"));
                changed = true;
            }
            if (!changed) continue;
            if (info.Id == StoryEditor.Id) nextStory = graph;
            else
            {
                if (retained is not null) retainedChanges.Add((retained.Host, previousGraph, graph, retained.CreateLayoutSnapshot()));
                story.Graph = graph;
                string path = store.Stories.GetPath(info.Id);
                changes.Add(new(Path.GetRelativePath(root, path), File.ReadAllBytes(path), Encoding.UTF8.GetBytes(story.ToJson())));
            }
        }
        if (references.Count > 1 || removed.Count > 0)
        {
            var message = references.Count > 1 ? "此资源被以下节点共享，粘贴将同时影响这些引用：\n" + string.Join("\n", references) : "";
            if (removed.Count > 0) message += "\n以下连线无法匹配新端口，将被移除：\n" + string.Join("\n", removed);
            if (!confirm(message)) return false;
        }
        string resourcePath = (kind == GraphResourceKind.Session ? store.Sessions : store.Tasks).GetPath(targetId);
        changes.Add(new(Path.GetRelativePath(root, resourcePath), File.ReadAllBytes(resourcePath), Encoding.UTF8.GetBytes(after.ToJson())));
        var inverse = changes.Select(c => new NamespaceFileChange(c.RelativePath, c.DesiredBytes, c.ExpectedBytes)).ToArray();
        void Redo()
        {
            new NamespaceFileTransaction().Apply(root, changes, () => { });
            foreach (var item in resourceHosts) item.Host.RestoreClipboardSnapshot(after.Graph!, afterLayout);
            foreach (var item in retainedChanges) item.Host.RestoreClipboardSnapshot(item.After, item.Layout);
        }
        void Undo()
        {
            new NamespaceFileTransaction().Apply(root, inverse, () => { });
            foreach (var item in resourceHosts) item.Host.RestoreClipboardSnapshot(item.Before, item.Layout);
            foreach (var item in retainedChanges) item.Host.RestoreClipboardSnapshot(item.Before, item.Layout);
        }
        StoryEditor.Host.CommitClipboardSnapshot(nextStory ?? StoryEditor.Host.Graph, StoryEditor.Host.Layout, Undo, Redo);
        return true;
    }

    private void AddClipboardResource(CanonicalGraphClipboard.Resource resource)
    {
        var editor = new CanonicalGraphResourceEditorViewModel(resource.Envelope, resource.Layout);
        editor.PropertyChanged += OnEditorPropertyChanged;
        var item = new CanonicalStoryGraphItem(editor);
        if (editor.ResourceKind == GraphResourceKind.Session) { SessionItems = [.. SessionItems, item]; SessionEditors = [.. SessionEditors, editor]; }
        else { TaskItems = [.. TaskItems, item]; TaskEditors = [.. TaskEditors, editor]; }
        NotifyClipboardLibrary();
    }

    private void RemoveClipboardResource(GraphResourceKind kind, string id)
    {
        var item = SessionItems.Concat(TaskItems).SingleOrDefault(i => i.ResourceKind == kind && i.Id == id);
        if (item is null) return;
        if (ReferenceEquals(ActiveEditor, item.Editor)) ReturnToStory();
        SessionItems = SessionItems.Where(i => !ReferenceEquals(i, item)).ToArray();
        TaskItems = TaskItems.Where(i => !ReferenceEquals(i, item)).ToArray();
        SessionEditors = SessionItems.Select(i => i.Editor).ToArray(); TaskEditors = TaskItems.Select(i => i.Editor).ToArray();
        item.Editor.PropertyChanged -= OnEditorPropertyChanged; item.Editor.Dispose();
        NotifyClipboardLibrary();
    }

    private void NotifyClipboardLibrary()
    {
        Folders.Single(f => f.Kind == CanonicalStoryFolderKind.Sessions).SynchronizeItems(SessionItems);
        Folders.Single(f => f.Kind == CanonicalStoryFolderKind.Tasks).SynchronizeItems(TaskItems);
        foreach (var name in new[] { nameof(SessionItems), nameof(TaskItems), nameof(SessionEditors), nameof(TaskEditors), nameof(Editors), nameof(HasDirtyEditors) }) OnPropertyChanged(name);
    }
}
