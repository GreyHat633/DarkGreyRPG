using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ClipboardPlanTests
{
    [TestMethod]
    public void CrossStoryAggregateCopyDeclaresTypedDependenciesAndUndoesThem()
    {
        const string owner = "ST-2345-6789-ABCD-EFGH", target = "ST-JKLM-NPQR-STUV-WXYZ";
        var root = Path.Combine(AppContext.BaseDirectory, "temp", "clipboard-dependencies-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var lifecycle = new CanonicalStoryLifecycleService(store);
            lifecycle.Create(owner, "源故事"); lifecycle.Create(target, "目标故事");
            string Key(ResourceKind kind) => new ResourceAddress(StoryUid.Parse(owner), kind, "shared").ToKey();
            var actor = Key(ResourceKind.Actor); var item = Key(ResourceKind.Item);
            new CanonicalStoryActorLifecycleService(store).CreateOwned(owner, CanonicalStoryActorKind.Individual, actor, "角色");
            new CanonicalStoryItemLifecycleService(store).CreateOwned(owner, CanonicalStoryItemKind.Individual, item, "物品");
            var session = new CanonicalStoryResourceLifecycleService(store).CreateOwned(owner, GraphResourceKind.Session, Key(ResourceKind.Session), "会话");
            var definition = store.Sessions.Load(Key(ResourceKind.Session));
            var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
            line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(actor);
            line.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { new { page_id = "page", text = DynamicContentText.Encode([new(Type: "item_name", ItemId: item)]) } });
            var sessionGraph = definition.Graph!; sessionGraph.Nodes.Add(line); definition.Graph = sessionGraph;
            store.Sessions.Replace(definition);
            var sourceStory = store.Stories.Load(owner);
            var sourceGraph = sourceStory.Graph!;
            sourceGraph.Nodes.Add(CanonicalAggregateNodeFactory.Create(definition, "placement").Candidate!);
            sourceStory.Graph = sourceGraph;
            store.Stories.Replace(sourceStory);
            using var source = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(owner)) { MediaProjectDirectory = root };
            using var destination = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(target)) { MediaProjectDirectory = root, Clipboard = source.Clipboard };
            source.Clipboard.CopyNodes(source.StoryEditor.Host, ["placement"]);
            source.Clipboard.CaptureResources(source, false);
            var copied = source.Clipboard.Nodes!.CloneForPaste(GraphScope.StoryFlow, out var ids);
            var change = destination.PrepareCopiedResources(copied, ids);
            var graph = GraphDocument.FromJson(destination.StoryEditor.Host.Graph.ToJson()); graph.Nodes.AddRange(copied.Nodes);
            destination.StoryEditor.Host.CommitClipboardSnapshot(graph, destination.StoryEditor.Host.Layout, change.Undo, change.Redo);
            CollectionAssert.AreEqual(new[] { actor }, store.Memberships.Load(target).ReferencedResources.Actors);
            CollectionAssert.AreEqual(new[] { item }, store.Memberships.Load(target).ReferencedResources.Items);
            Assert.AreEqual(actor, destination.ActorItems.Single().Id);
            Assert.AreEqual(item, destination.ItemItems.Single().Id);
            Assert.IsTrue(destination.StoryEditor.Host.Undo());
            Assert.HasCount(0, store.Memberships.Load(target).ReferencedResources.Actors);
            Assert.HasCount(0, destination.ActorItems);
            Assert.IsTrue(destination.StoryEditor.Host.Redo());
            Assert.AreEqual(actor, destination.ActorItems.Single().Id);
            Assert.AreEqual(owner, ResourceAddress.FromKey(store.Sessions.Load(Key(ResourceKind.Session)).Id).StoryUid.Value);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SharedAggregateParametersCancelAtomicallyAndUpdateRetainedWorkspaces()
    {
        const string aId = "ST-2345-6789-ABCD-EFGH", bId = "ST-JKLM-NPQR-STUV-WXYZ";
        var sourceId = new ResourceAddress(StoryUid.Parse(aId), ResourceKind.Session, "source").ToKey();
        var targetId = new ResourceAddress(StoryUid.Parse(aId), ResourceKind.Session, "target").ToKey();
        string root = Path.Combine(AppContext.BaseDirectory, "temp", "clipboard-shared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var source = new GraphResourceEnvelope(GraphResourceKind.Session, sourceId, "Source", new([GraphNodeFactory.Create(GraphScope.Session, "line", "source-line")]));
            var actorId = new ResourceAddress(StoryUid.Parse(aId), ResourceKind.Actor, "speaker").ToKey();
            var sourceGraph = source.Graph!;
            sourceGraph.Nodes.Single().Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(actorId);
            source.Graph = sourceGraph;
            var target = new GraphResourceEnvelope(GraphResourceKind.Session, targetId, "Keep name", new([GraphNodeFactory.Create(GraphScope.Session, "line", "target-line")])) { Tags = ["keep-tag"] };
            store.Sessions.Create(source); store.Sessions.Create(target);
            GraphNode Place(GraphResourceEnvelope resource, string id) => CanonicalAggregateNodeFactory.Create(resource, id).Candidate!;
            var a = new GraphResourceEnvelope(GraphResourceKind.Story, aId, "Story A", new([Place(source, "source-placement"), Place(target, "target-placement")]));
            var b = new GraphResourceEnvelope(GraphResourceKind.Story, bId, "Story B", new([Place(target, "shared-placement")]));
            store.Stories.Create(a); store.Stories.Create(b);
            store.Memberships.Create(new(aId, new() { Sessions = [sourceId, targetId] }));
            store.Memberships.Create(new(bId, new(), new() { Sessions = [targetId] }));
            new CanonicalStoryActorLifecycleService(store).CreateOwned(aId, CanonicalStoryActorKind.Individual, actorId, "Shared speaker");
            var unplaced = new CanonicalStoryLifecycleService(store).CreateNew("Unplaced consumer");
            new CanonicalStoryResourceLifecycleService(store).AddReference(unplaced.Id, GraphResourceKind.Session, targetId);
            using var first = new CanonicalStoryWorkspaceViewModel(a, sessions: [source, target]) { MediaProjectDirectory = root };
            using var second = new CanonicalStoryWorkspaceViewModel(store.Stories.Load(bId), sessions: [store.Sessions.Load(targetId)]) { MediaProjectDirectory = root };
            first.OpenProjectWorkspaces = () => [first, second];
            first.Clipboard.CopyParameters(first.StoryEditor.Host, "source-placement"); first.Clipboard.CaptureResources(first, true);
            var original = File.ReadAllText(store.Sessions.GetPath(targetId));
            string? prompt = null;
            Assert.IsFalse(first.PasteAggregateParameters("target-placement", message => { prompt = message; return false; }));
            StringAssert.Contains(prompt!, "Story A"); StringAssert.Contains(prompt!, "Story B");
            Assert.AreEqual(original, File.ReadAllText(store.Sessions.GetPath(targetId)));
            Assert.IsEmpty(store.Memberships.Load(bId).ReferencedResources.Actors);
            Assert.IsEmpty(store.Memberships.Load(unplaced.Id).ReferencedResources.Actors);
            Assert.AreEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.IsTrue(first.PasteAggregateParameters("target-placement", _ => true));
            var changed = store.Sessions.Load(targetId);
            Assert.AreEqual("Keep name", changed.DisplayName); CollectionAssert.AreEqual(new[] { "keep-tag" }, changed.Tags.ToArray());
            Assert.AreNotEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.AreEqual(actorId, second.ActorItems.Single().Id);
            CollectionAssert.AreEqual(new[] { actorId }, store.Memberships.Load(unplaced.Id).ReferencedResources.Actors);
            Assert.IsTrue(first.StoryEditor.Host.Undo());
            Assert.IsEmpty(second.ActorItems);
            Assert.IsEmpty(store.Memberships.Load(unplaced.Id).ReferencedResources.Actors);
            Assert.AreEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.AreEqual(original, File.ReadAllText(store.Sessions.GetPath(targetId)));
            Assert.IsTrue(first.StoryEditor.Host.Redo());
            Assert.AreEqual(actorId, second.ActorItems.Single().Id);
            Assert.AreNotEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AggregateCopyClonesOnceAndUndoesMembershipAndResourceFiles()
    {
        const string storyId = "ST-2345-6789-ABCD-EFGH";
        var sourceId = new ResourceAddress(StoryUid.Parse(storyId), ResourceKind.Session, "chat").ToKey();
        string root = Path.Combine(AppContext.BaseDirectory, "temp", "clipboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var source = new GraphResourceEnvelope(GraphResourceKind.Session, sourceId, "聊天", new([GraphNodeFactory.Create(GraphScope.Session, "line", "line")])) { Tags = ["tag"] };
            store.Sessions.Create(source);
            GraphNode Aggregate(string id) => new(id, "session", "会话「聊天」", [new("flow_in", "Flow In", true, GraphInterfaceKind.Flow)], new Dictionary<string, JsonElement> { ["resource_id"] = JsonSerializer.SerializeToElement(sourceId) });
            var story = new GraphResourceEnvelope(GraphResourceKind.Story, storyId, "Story", new([Aggregate("a"), Aggregate("b")]));
            store.Stories.Create(story);
            store.Memberships.Create(new(storyId, new() { Sessions = [sourceId] }));
            using var workspace = new CanonicalStoryWorkspaceViewModel(story, sessions: [source]) { MediaProjectDirectory = root };
            workspace.Clipboard.CopyNodes(workspace.StoryEditor.Host, ["a", "b"]);
            workspace.Clipboard.CaptureResources(workspace, false);
            var copied = workspace.Clipboard.Nodes!.CloneForPaste(GraphScope.StoryFlow, out var ids);
            var transaction = workspace.PrepareCopiedResources(copied, ids);
            var graph = GraphDocument.FromJson(story.Graph!.ToJson()); graph.Nodes.AddRange(copied.Nodes);
            workspace.StoryEditor.Host.CommitClipboardSnapshot(graph, workspace.StoryEditor.Host.Layout, transaction.Undo, transaction.Redo);
            Assert.HasCount(2, store.Sessions.List());
            Assert.HasCount(2, workspace.SessionItems);
            var copiedId = copied.Nodes[0].Properties["resource_id"].GetString()!;
            Assert.AreEqual(storyId, ResourceAddress.FromKey(copiedId).StoryUid.Value);
            Assert.AreNotEqual(sourceId, copiedId);
            Assert.IsTrue(copied.Nodes.All(n => n.Properties["resource_id"].GetString() == copiedId));
            Assert.AreEqual("聊天", store.Sessions.Load(copiedId).DisplayName);
            Assert.AreNotEqual("line", store.Sessions.Load(copiedId).Graph!.Nodes[0].Id);
            new CanonicalGraphLayoutStore(root).Save(GraphResourceKind.Story, storyId,
                new Dictionary<string, DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout> { ["a"] = new() { X = 123, Y = 456 } });
            Assert.IsTrue(workspace.StoryEditor.Host.Undo());
            Assert.AreEqual(123d, new CanonicalGraphLayoutStore(root).Load(GraphResourceKind.Story, storyId)["a"].X);
            Assert.HasCount(1, store.Sessions.List()); Assert.HasCount(1, workspace.SessionItems);
            Assert.IsTrue(workspace.StoryEditor.Host.Redo()); Assert.HasCount(2, store.Sessions.List());
            using var reopened = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(storyId));
            Assert.HasCount(2, reopened.SessionItems);
            Assert.AreEqual(copiedId, reopened.SessionItems.Last().Id);
            var repeated = workspace.Clipboard.Nodes.CloneForPaste(GraphScope.StoryFlow, out var repeatedIds);
            workspace.PrepareCopiedResources(repeated, repeatedIds);
            var repeatedId = repeated.Nodes[0].Properties["resource_id"].GetString()!;
            Assert.AreNotEqual(copiedId, repeatedId);
            Assert.IsTrue(repeated.Nodes.All(n => n.Properties["resource_id"].GetString() == repeatedId));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SnapshotDetachesAndKeepsOnlyInternalConnections()
    {
        var a = GraphNodeFactory.Create(GraphScope.Session, "line", "a");
        var b = GraphNodeFactory.Create(GraphScope.Session, "line", "b");
        var graph = new GraphDocument([a, b], [new("a", "flow_out", "b", "flow_in", GraphInterfaceKind.Flow), new("b", "flow_out", "external", "flow_in", GraphInterfaceKind.Flow)]);
        var copy = new GraphClipboardSnapshot(GraphScope.Session, graph, ["a", "b"]);
        a.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { new { page_id = "mutated", text = "Changed after copy" } });
        var pasted = copy.CloneForPaste(GraphScope.Session, out var ids);
        Assert.HasCount(2, pasted.Nodes); Assert.HasCount(1, pasted.Connections);
        Assert.AreNotEqual("a", ids["a"]);
        Assert.AreEqual(ids["a"], pasted.Connections[0].FromNodeId);
        Assert.AreNotEqual("Changed after copy", pasted.Nodes[0].Properties["pages"][0].GetProperty("text").GetString());
    }

    [TestMethod]
    public void FixedNodeRejectsWholePaste()
    {
        var start = GraphNodeFactory.CreateStoryStart("start");
        var copy = new GraphClipboardSnapshot(GraphScope.StoryFlow, new([start]), ["start"]);
        Assert.ThrowsExactly<InvalidOperationException>(() => copy.CloneForPaste(GraphScope.StoryFlow, out _));
    }

    [TestMethod]
    public void CrossGraphCompatibilityAndProjectClipboardIsolation()
    {
        var music = GraphNodeFactory.Create(GraphScope.Session, "music", "music");
        var copy = new GraphClipboardSnapshot(GraphScope.Session, new([music]), ["music"]);
        Assert.HasCount(1, copy.CloneForPaste(GraphScope.Session, out _).Nodes);
        var logic = GraphNodeFactory.Create(GraphScope.Session, "and", "logic");
        Assert.HasCount(1, new GraphClipboardSnapshot(GraphScope.Session, new([logic]), ["logic"]).CloneForPaste(GraphScope.StoryFlow, out _).Nodes);
        Assert.ThrowsExactly<InvalidOperationException>(() => copy.CloneForPaste(GraphScope.Task, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() => copy.CloneForPaste(GraphScope.Project, out _));
        var clipboard = new CanonicalGraphClipboard(); clipboard.SetProject("one");
        var host = new GraphEditorHostViewModel(new([music]), GraphScope.Session);
        clipboard.CopyNodes(host, ["music"]); clipboard.CopyParameters(host, "music");
        clipboard.SetProject("two");
        Assert.IsNull(clipboard.Nodes); Assert.IsNull(clipboard.Parameters);
    }

    [TestMethod]
    public void ParametersMatchUniqueNameAndNeverPosition()
    {
        var old = new GraphNode("a", "choice", "Choice", [new("old", "Yes", false, GraphInterfaceKind.Flow)]);
        var source = new GraphNode("source", "choice", "Choice", [new("new", "No", false, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([old], [new("a", "old", "other", "flow_in", GraphInterfaceKind.Flow)]);
        var changed = GraphClipboardSnapshot.PasteParameters(GraphScope.Session, graph, source, "a", out var removed);
        Assert.HasCount(1, removed); Assert.IsEmpty(changed.Connections); Assert.HasCount(1, graph.Connections);
        source.Ports[0].DisplayName = "Yes";
        changed = GraphClipboardSnapshot.PasteParameters(GraphScope.Session, graph, source, "a", out removed);
        Assert.IsEmpty(removed); Assert.AreEqual("old", changed.Connections[0].FromPortId);
    }

    [TestMethod]
    public void ClipboardMutationUndoesGraphAndLayoutTogether()
    {
        var host = new GraphEditorHostViewModel(new GraphDocument(), GraphScope.Session);
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line")]);
        host.CommitClipboardSnapshot(graph, new Dictionary<string, GraphEditorNodePosition> { ["line"] = new(80, 90) });
        Assert.HasCount(1, host.Nodes); Assert.AreEqual(80d, host.Nodes[0].X);
        Assert.IsTrue(host.Undo()); Assert.IsEmpty(host.Nodes);
        Assert.IsTrue(host.Redo()); Assert.AreEqual(90d, host.Nodes[0].Y);
    }

    [TestMethod]
    public void PortraitLandscapeAndSquareImportsKeepPixelAspect()
    {
        foreach (var (width, height) in new[] { (1920, 1080), (800, 1600), (1000, 1000) })
        {
            var geometry = SessionScreenEditor.InitialImageGeometry(width, height);
            Assert.AreEqual((double)width / height, geometry.Width * 320 / (geometry.Height * 180), 0.000001);
            Assert.IsTrue(geometry.Width <= 0.75 && geometry.Height <= 0.75);
            Assert.AreEqual(0.5, geometry.X + geometry.Width / 2, 0.000001);
            Assert.AreEqual(0.5, geometry.Y + geometry.Height / 2, 0.000001);
        }
    }

    [TestMethod]
    public void LineSpeedHasCompatibleDefaultAndUndo()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "line", "line"); node.Properties.Remove("text_speed");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session,
            new ResourceAddress(StoryUid.Parse("ST-2345-6789-ABCD-EFGH"), ResourceKind.Session, "session").ToKey(), "Session", new([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual(30d, inspector.LineTextSpeed);
        inspector.LineTextSpeed = 120; Assert.AreEqual(120d, inspector.LineTextSpeed);
        inspector.LineTextSpeed = 121; Assert.AreEqual(120d, inspector.LineTextSpeed);
        Assert.IsTrue(editor.Host.Undo()); Assert.AreEqual(30d, inspector.LineTextSpeed);
    }
}
