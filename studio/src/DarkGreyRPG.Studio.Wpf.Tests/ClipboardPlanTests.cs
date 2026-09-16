using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
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
    public void SharedAggregateParametersCancelAtomicallyAndUpdateRetainedWorkspaces()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "temp", "clipboard-shared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var source = new GraphResourceEnvelope(GraphResourceKind.Session, "source", "Source", new([GraphNodeFactory.Create(GraphScope.Session, "line", "source-line")]));
            var target = new GraphResourceEnvelope(GraphResourceKind.Session, "target", "Keep name", new([GraphNodeFactory.Create(GraphScope.Session, "line", "target-line")])) { Tags = ["keep-tag"] };
            store.Sessions.Create(source); store.Sessions.Create(target);
            GraphNode Place(GraphResourceEnvelope resource, string id) => CanonicalAggregateNodeFactory.Create(resource, id).Candidate!;
            var a = new GraphResourceEnvelope(GraphResourceKind.Story, "a", "Story A", new([Place(source, "source-placement"), Place(target, "target-placement")]));
            var b = new GraphResourceEnvelope(GraphResourceKind.Story, "b", "Story B", new([Place(target, "shared-placement")]));
            store.Stories.Create(a); store.Stories.Create(b);
            store.Memberships.Create(new("a", new() { Sessions = ["source", "target"] }));
            store.Memberships.Create(new("b", new() { Sessions = ["target"] }));
            using var first = new CanonicalStoryWorkspaceViewModel(a, sessions: [source, target]) { MediaProjectDirectory = root };
            using var second = new CanonicalStoryWorkspaceViewModel(store.Stories.Load("b"), sessions: [store.Sessions.Load("target")]) { MediaProjectDirectory = root };
            first.OpenProjectWorkspaces = () => [first, second];
            first.Clipboard.CopyParameters(first.StoryEditor.Host, "source-placement"); first.Clipboard.CaptureResources(first, true);
            var original = File.ReadAllText(store.Sessions.GetPath("target"));
            string? prompt = null;
            Assert.IsFalse(first.PasteAggregateParameters("target-placement", message => { prompt = message; return false; }));
            StringAssert.Contains(prompt!, "Story A"); StringAssert.Contains(prompt!, "Story B");
            Assert.AreEqual(original, File.ReadAllText(store.Sessions.GetPath("target")));
            Assert.AreEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.IsTrue(first.PasteAggregateParameters("target-placement", _ => true));
            var changed = store.Sessions.Load("target");
            Assert.AreEqual("Keep name", changed.DisplayName); CollectionAssert.AreEqual(new[] { "keep-tag" }, changed.Tags.ToArray());
            Assert.AreNotEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.IsTrue(first.StoryEditor.Host.Undo());
            Assert.AreEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
            Assert.AreEqual(original, File.ReadAllText(store.Sessions.GetPath("target")));
            Assert.IsTrue(first.StoryEditor.Host.Redo());
            Assert.AreNotEqual("target-line", second.SessionEditors.Single().Host.Graph.Nodes.Single().Id);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AggregateCopyClonesOnceAndUndoesMembershipAndResourceFiles()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "temp", "clipboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var source = new GraphResourceEnvelope(GraphResourceKind.Session, "chat", "聊天", new([GraphNodeFactory.Create(GraphScope.Session, "line", "line")])) { Tags = ["tag"] };
            store.Sessions.Create(source);
            GraphNode Aggregate(string id) => new(id, "session", "会话「聊天」", [new("flow_in", "Flow In", true, GraphInterfaceKind.Flow)], new Dictionary<string, JsonElement> { ["resource_id"] = JsonSerializer.SerializeToElement("chat") });
            var story = new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new([Aggregate("a"), Aggregate("b")]));
            store.Stories.Create(story);
            store.Memberships.Create(new("story", new() { Sessions = ["chat"] }));
            using var workspace = new CanonicalStoryWorkspaceViewModel(story, sessions: [source]) { MediaProjectDirectory = root };
            workspace.Clipboard.CopyNodes(workspace.StoryEditor.Host, ["a", "b"]);
            workspace.Clipboard.CaptureResources(workspace, false);
            var copied = workspace.Clipboard.Nodes!.CloneForPaste(GraphScope.StoryFlow, out var ids);
            var transaction = workspace.PrepareCopiedResources(copied, ids);
            var graph = GraphDocument.FromJson(story.Graph!.ToJson()); graph.Nodes.AddRange(copied.Nodes);
            workspace.StoryEditor.Host.CommitClipboardSnapshot(graph, workspace.StoryEditor.Host.Layout, transaction.Undo, transaction.Redo);
            Assert.HasCount(2, store.Sessions.List());
            Assert.HasCount(2, workspace.SessionItems);
            Assert.IsTrue(copied.Nodes.All(n => n.Properties["resource_id"].GetString() == "chat_copy"));
            Assert.AreEqual("聊天", store.Sessions.Load("chat_copy").DisplayName);
            Assert.AreNotEqual("line", store.Sessions.Load("chat_copy").Graph!.Nodes[0].Id);
            new CanonicalGraphLayoutStore(root).Save(GraphResourceKind.Story, "story",
                new Dictionary<string, DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout> { ["a"] = new() { X = 123, Y = 456 } });
            Assert.IsTrue(workspace.StoryEditor.Host.Undo());
            Assert.AreEqual(123d, new CanonicalGraphLayoutStore(root).Load(GraphResourceKind.Story, "story")["a"].X);
            Assert.HasCount(1, store.Sessions.List()); Assert.HasCount(1, workspace.SessionItems);
            Assert.IsTrue(workspace.StoryEditor.Host.Redo()); Assert.HasCount(2, store.Sessions.List());
            using var reopened = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load("story"));
            Assert.HasCount(2, reopened.SessionItems);
            Assert.AreEqual("chat_copy", reopened.SessionItems.Last().Id);
            var repeated = workspace.Clipboard.Nodes.CloneForPaste(GraphScope.StoryFlow, out var repeatedIds);
            workspace.PrepareCopiedResources(repeated, repeatedIds);
            Assert.IsTrue(repeated.Nodes.All(n => n.Properties["resource_id"].GetString() == "chat_copy1"));
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
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual(30d, inspector.LineTextSpeed);
        inspector.LineTextSpeed = 120; Assert.AreEqual(120d, inspector.LineTextSpeed);
        inspector.LineTextSpeed = 121; Assert.AreEqual(120d, inspector.LineTextSpeed);
        Assert.IsTrue(editor.Host.Undo()); Assert.AreEqual(30d, inspector.LineTextSpeed);
    }
}
