using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalGraphResourceEditorViewModelTests
{
    [TestMethod]
    public void AllResourceKindsOpenAtTheirCanonicalScopeWithoutAliasingSource()
    {
        foreach (var (kind, scope) in new[]
        {
            (GraphResourceKind.Story, GraphScope.StoryFlow),
            (GraphResourceKind.Session, GraphScope.Session),
            (GraphResourceKind.Task, GraphScope.Task),
        })
        {
            var source = new GraphDocument();
            var envelope = new GraphResourceEnvelope(kind, "id", "Name", source);
            using var editor = new CanonicalGraphResourceEditorViewModel(envelope);
            source.Nodes.Add(new GraphNode("caller", "unknown", "Caller"));

            Assert.AreEqual(scope, editor.Scope);
            Assert.AreEqual(scope, editor.Host.Scope);
            Assert.AreSame(editor.Document.Graph, editor.Host.Graph);
            Assert.IsEmpty(editor.Host.Graph.Nodes);
            Assert.IsFalse(editor.IsDirty);
        }
    }

    [TestMethod]
    public void SuccessfulMutationAdvancesRevisionDirtyStateAndDetachedSnapshot()
    {
        using var editor = SessionEditor();
        var changed = 0;
        editor.Host.GraphChanged += (_, _) => changed++;

        Assert.IsTrue(editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")));

        Assert.AreEqual(1L, editor.GraphRevision);
        Assert.AreEqual(1, changed);
        Assert.IsTrue(editor.IsDirty);
        Assert.IsTrue(editor.CanSave);
        var snapshot = editor.CreatePersistenceSnapshot();
        Assert.AreEqual("line-1", snapshot.Graph!.Nodes.Single().Id);
        var snapshotText = snapshot.Graph!.Nodes.Single().Properties["text"].GetString();

        Assert.IsTrue(editor.Host.SetNodeProperty("line-1", "text", "changed"));
        Assert.AreEqual(snapshotText, snapshot.Graph!.Nodes.Single().Properties["text"].GetString());
        editor.MarkSaved();
        Assert.IsFalse(editor.IsDirty);
        Assert.AreEqual("已保存", editor.SaveStateText);
    }

    [TestMethod]
    public void FailedMutationPublishesProblemsWithoutRevisionOrDirtyChange()
    {
        using var editor = SessionEditor();
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        Assert.IsTrue(editor.Host.AddNode(line));
        editor.MarkSaved();
        var revision = editor.GraphRevision;

        Assert.IsFalse(editor.Host.AddNode(line));

        Assert.AreEqual(revision, editor.GraphRevision);
        Assert.IsFalse(editor.IsDirty);
        Assert.IsFalse(editor.CanSave);
        Assert.AreEqual("graph.node.id.duplicate", editor.ValidationIssues.Single().Code);
        StringAssert.Contains(editor.ValidationText, "无法创建或更新该节点");
        StringAssert.Contains(editor.ValidationText, "“问题”面板");
        Assert.IsFalse(editor.ValidationText.Contains("line-1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UndoRedoAdvanceRevisionAndReturnToSavedGraphByContent()
    {
        using var editor = SessionEditor();
        Assert.IsTrue(editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")));
        Assert.IsTrue(editor.UndoCommand.CanExecute(null));
        Assert.IsTrue(editor.IsDirty);

        editor.UndoCommand.Execute(null);

        Assert.AreEqual(2L, editor.GraphRevision);
        Assert.IsFalse(editor.IsDirty);
        Assert.IsTrue(editor.RedoCommand.CanExecute(null));

        editor.RedoCommand.Execute(null);

        Assert.AreEqual(3L, editor.GraphRevision);
        Assert.IsTrue(editor.IsDirty);
        Assert.AreEqual("line-1", editor.Host.Graph.Nodes.Single().Id);
    }

    [TestMethod]
    public void LayoutOnlyMovementMarksLayoutDirtyWithoutPretendingGraphRevision()
    {
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")]);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", graph));

        editor.Host.SetNodePosition("line-1", 480, 270);

        Assert.AreEqual(0L, editor.GraphRevision);
        Assert.IsFalse(editor.IsGraphDirty);
        Assert.IsTrue(editor.IsLayoutDirty);
        Assert.IsTrue(editor.IsDirty);
        Assert.IsTrue(editor.CanSave);
        Assert.AreEqual(new GraphEditorNodePosition(480, 270), editor.Host.Layout["line-1"]);

        editor.MarkLayoutSaved();

        Assert.IsFalse(editor.IsLayoutDirty);
        Assert.IsFalse(editor.IsDirty);
    }

    [TestMethod]
    public void SuppliedLayoutIsTheSavedBaselineAndMovingBackClearsLayoutDirty()
    {
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")]);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", graph),
            new Dictionary<string, GraphEditorNodePosition>(StringComparer.Ordinal)
            {
                ["line-1"] = new(480, 270),
                ["deleted-node"] = new(900, 900),
            });

        Assert.AreEqual(new GraphEditorNodePosition(480, 270), editor.Host.Layout["line-1"]);
        Assert.IsFalse(editor.IsLayoutDirty);
        CollectionAssert.AreEquivalent(
            new[] { "line-1" },
            editor.CreateLayoutSnapshot().Keys.ToArray());

        editor.Host.SetNodePosition("line-1", 500, 300);
        Assert.IsTrue(editor.IsLayoutDirty);

        editor.Host.SetNodePosition("line-1", 480, 270);
        Assert.IsFalse(editor.IsLayoutDirty);
    }

    [TestMethod]
    public void MovingNodeDoesNotChangeCanonicalSerialization()
    {
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")]);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", graph));
        var before = GraphResourceEnvelopeSerializer.Serialize(editor.CreatePersistenceSnapshot(), indented: false);

        editor.Host.SetNodePosition("line-1", 765.25, 432.75);

        var after = GraphResourceEnvelopeSerializer.Serialize(editor.CreatePersistenceSnapshot(), indented: false);
        Assert.AreEqual(before, after);
        Assert.IsFalse(editor.IsGraphDirty);
        Assert.IsTrue(editor.IsLayoutDirty);
    }

    [TestMethod]
    public void WireReconnectMarksCanonicalEditorDirtyAndEntersPersistenceSnapshot()
    {
        var start = GraphNodeFactory.Create(GraphScope.Session, "start", "start");
        var source = GraphNodeFactory.Create(GraphScope.Session, "line", "source");
        var replacement = GraphNodeFactory.Create(GraphScope.Session, "line", "replacement");
        var target = GraphNodeFactory.Create(GraphScope.Session, "line", "target");
        var original = new GraphConnection(
            source.Id, "flow_out", target.Id, "flow_in", GraphInterfaceKind.Flow);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([start, source, replacement, target], [original])));

        Assert.IsTrue(editor.Host.CompleteWireDrag(
            GraphEditorEndpoint.Input(target.Id, "flow_in", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Output(replacement.Id, "flow_out", GraphInterfaceKind.Flow),
            original));

        Assert.IsTrue(editor.IsGraphDirty);
        Assert.IsTrue(editor.IsDirty);
        Assert.IsTrue(editor.CanSave);
        var connection = editor.CreatePersistenceSnapshot().Graph!.Connections.Single();
        Assert.AreEqual(replacement.Id, connection.FromNodeId);
        Assert.AreEqual(target.Id, connection.ToNodeId);
    }

    private static CanonicalGraphResourceEditorViewModel SessionEditor()
        => new(new GraphResourceEnvelope(
            GraphResourceKind.Session, "session", "Session", new GraphDocument()));
}
