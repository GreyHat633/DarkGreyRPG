using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CommentFrame0332Tests
{
    [TestMethod]
    public void ClipboardFramesUseNewNodeIdsAndUndoWithThePaste()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([node])));
        var host = editor.Host;
        host.AddFrame([node.Id], 10, 20, 300, 200);
        var clipboard = new CanonicalGraphClipboard();
        clipboard.CopyNodes(host, [node.Id]);
        var cloned = clipboard.Nodes!.CloneForPaste(host.Scope, out var ids);
        var graph = GraphDocument.FromJson(host.Graph.ToJson());
        graph.Nodes.AddRange(cloned.Nodes);
        var originalFrame = host.Frames.Single();
        var newFrame = originalFrame with { Id = "copied-frame", Members = [ids[node.Id]] };
        host.CommitClipboardSnapshot(graph, host.Layout, frames: [originalFrame, newFrame]);
        CollectionAssert.AreEqual(new[] { ids[node.Id] }, host.Frames.Last().Members);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(1, host.Nodes.Count);
        Assert.AreEqual(originalFrame.Id, host.Frames.Single().Id);
        Assert.IsTrue(host.Redo());
        Assert.AreEqual(2, host.Nodes.Count);
        Assert.AreEqual(ids[node.Id], host.Frames.Last().Members.Single());
    }

    [TestMethod]
    public void FrameMovesMembersInOneUndoAndDeleteNeverDeletesGraph()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([node])));
        var host = editor.Host;
        host.SetNodePosition(node.Id, 100, 100);
        editor.MarkSaved();
        var graphJson = host.Graph.ToJson();
        var id = host.AddFrame([node.Id], 80, 60, 400, 250);
        Assert.IsTrue(editor.IsLayoutDirty);
        Assert.IsFalse(editor.IsGraphDirty);
        var frame = host.Frames.Single();
        host.UpdateFrame(frame with { X = 180, Y = 160 }, moveMembers: true);
        Assert.AreEqual(200d, host.Nodes.Single().X);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(100d, host.Nodes.Single().X);
        Assert.AreEqual(80d, host.Frames.Single().X);
        host.RemoveFrame(id);
        Assert.AreEqual(0, host.Frames.Count);
        Assert.AreEqual(graphJson, host.Graph.ToJson());
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(1, host.Frames.Count);
        host.UpdateFrame(host.Frames.Single() with { Width = 500 });
        Assert.AreEqual(100d, host.Nodes.Single().X);
        Assert.AreEqual(graphJson, host.Graph.ToJson());
        var second = host.AddFrame([node.Id], 0, 0, 300, 200);
        Assert.AreEqual(0, host.Frames.First().Members.Length);
        Assert.AreEqual(node.Id, host.Frames.Last().Members.Single());
    }
}
