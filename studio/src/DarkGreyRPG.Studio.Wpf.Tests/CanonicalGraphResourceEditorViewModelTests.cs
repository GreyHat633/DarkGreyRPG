using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

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
        StringAssert.Contains(editor.ValidationText, "line-1");
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
    public void LayoutOnlyMovementDoesNotPretendEnvelopePersistence()
    {
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")]);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", graph));

        editor.Host.SetNodePosition("line-1", 480, 270);

        Assert.AreEqual(0L, editor.GraphRevision);
        Assert.IsFalse(editor.IsDirty);
        Assert.AreEqual(new GraphEditorNodePosition(480, 270), editor.Host.Layout["line-1"]);
    }

    private static CanonicalGraphResourceEditorViewModel SessionEditor()
        => new(new GraphResourceEnvelope(
            GraphResourceKind.Session, "session", "Session", new GraphDocument()));
}
