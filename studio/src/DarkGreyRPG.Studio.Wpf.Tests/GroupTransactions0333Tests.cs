using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GroupTransactions0333Tests
{
    [STATestMethod]
    public void GroupContextMenuDeleteRemovesNestedContentsAndUndoesAsOneEdit()
    {
        var host = Host();
        host.RestoreFrames([
            new("inner", "Inner", 0, 0, 100, 100, ["a"]),
            new("outer", "Outer", 0, 0, 200, 200, ["b"]) { Groups = ["inner"] }]);
        var before = host.Graph.ToJson();
        var view = new DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, Left = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            view.SelectGroup("outer");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var menu = (System.Windows.Controls.ContextMenu)view.GetType().GetMethod("CreateSelectionContextMenu", flags)!
                .Invoke(view, [null, (System.Windows.FrameworkElement)view.FindName("GraphCanvas")])!;
            var delete = menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(item => Equals(item.Header, "删除"));
            Assert.IsTrue(delete.IsEnabled);
            delete.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            Assert.IsEmpty(host.Frames);
            CollectionAssert.AreEqual(new[] { "c" }, host.Nodes.Select(n => n.NodeId).ToArray());
            Assert.IsTrue(host.Undo());
            Assert.IsTrue(System.Text.Json.Nodes.JsonNode.DeepEquals(
                System.Text.Json.Nodes.JsonNode.Parse(before), System.Text.Json.Nodes.JsonNode.Parse(host.Graph.ToJson())));
            CollectionAssert.AreEquivalent(new[] { "inner", "outer" }, host.Frames.Select(f => f.Id).ToArray());
            Assert.IsTrue(host.Redo());
            Assert.IsEmpty(host.Frames);
            CollectionAssert.AreEqual(new[] { "c" }, host.Nodes.Select(n => n.NodeId).ToArray());
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void GroupBlankClickFocusesCanvasAndRoutesDeleteAsOneUndo()
    {
        var host = Host();
        host.RestoreFrames([new("group", "Group", 0, 0, 100, 100, ["a", "b"])]);
        var view = new DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.Activate(); window.UpdateLayout();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var visuals = (List<System.Windows.FrameworkElement>)view.GetType().GetField("_frameVisuals", flags)!.GetValue(view)!;
            var body = (System.Windows.Controls.Border)visuals.Single();
            var thumb = ((System.Windows.Controls.Grid)body.Child).Children.OfType<System.Windows.Controls.Primitives.Thumb>().Single();
            thumb.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.UIElement.PreviewMouseLeftButtonDownEvent });
            var canvas = (System.Windows.Controls.Canvas)view.FindName("GraphCanvas");
            Assert.AreSame(canvas, System.Windows.Input.Keyboard.FocusedElement);
            CollectionAssert.AreEqual(new[] { "group" }, view.SelectedGroups.ToArray());
            canvas.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(view)!, 0, System.Windows.Input.Key.Delete)
            { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
            Assert.IsEmpty(host.Frames);
            Assert.AreEqual(1, host.Nodes.Count);
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(3, host.Nodes.Count);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, host.Frames.Single().Members);
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void MovingNestedMemberUpdatesAncestorsAndReusesUnchangedGroupBounds()
    {
        var host = Host();
        host.SetNodePosition("a", 0, 100);
        host.SetNodePosition("b", 500, 100);
        host.SetNodePosition("c", 1000, 100);
        host.RestoreFrames([
            new("inner", "Inner", 0, 0, 100, 100, ["a"]),
            new("outer", "Outer", 0, 0, 100, 100, ["b"]) { Groups = ["inner"] },
            new("other", "Other", 0, 0, 100, 100, ["c"])]);
        var view = new DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var bounds = (Dictionary<string, System.Windows.Rect>)view.GetType().GetField("_groupBounds", flags)!.GetValue(view)!;
            var inputs = (Dictionary<string, System.Windows.Rect[]>)view.GetType().GetField("_groupLayoutInputs", flags)!.GetValue(view)!;
            var inner = bounds["inner"]; var outer = bounds["outer"]; var other = bounds["other"];
            var unchanged = inputs["other"];
            view.SelectNodes(["a"]);
            Assert.IsTrue(view.MoveSelectedNodes(new System.Windows.Vector(-100, 0)));
            window.UpdateLayout();
            Assert.AreEqual(inner.Left - 100, bounds["inner"].Left);
            Assert.AreEqual(outer.Left - 100, bounds["outer"].Left);
            Assert.AreEqual(other, bounds["other"]);
            Assert.AreSame(unchanged, inputs["other"]);
            CollectionAssert.AreEqual(new[] { "a" }, host.Frames.Single(f => f.Id == "inner").Members);
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void GReleaseCombinesOnceButModifierAndPointerGesturesDoNot()
    {
        var host = Host();
        var view = new DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            window.UpdateLayout();
            view.SelectNodes(["a", "b"]);
            void Key(System.Windows.Input.Key key, bool down) => view.RaiseEvent(new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(view)!, 0, key)
            { RoutedEvent = down ? System.Windows.Input.Keyboard.PreviewKeyDownEvent : System.Windows.Input.Keyboard.PreviewKeyUpEvent });
            Key(System.Windows.Input.Key.G, true);
            Assert.IsEmpty(host.Frames);
            Key(System.Windows.Input.Key.LeftCtrl, true);
            Key(System.Windows.Input.Key.LeftCtrl, false);
            Key(System.Windows.Input.Key.G, false);
            Assert.IsEmpty(host.Frames);
            view.BeginMarqueeSelection(new System.Windows.Point(0, 0));
            Key(System.Windows.Input.Key.G, true);
            Key(System.Windows.Input.Key.G, false);
            Assert.IsEmpty(host.Frames);
            view.CompleteMarqueeSelection();
            view.SelectNodes(["a", "b"]);
            Key(System.Windows.Input.Key.G, true);
            Key(System.Windows.Input.Key.G, false);
            Assert.HasCount(1, host.Frames);
            Key(System.Windows.Input.Key.G, false);
            Assert.HasCount(1, host.Frames);
            Assert.IsTrue(host.Undo());
            Assert.IsEmpty(host.Frames);
        }
        finally { window.Close(); }
    }

    private static GraphEditorHostViewModel Host() => new(new([
        GraphNodeFactory.Create(GraphScope.Session, "line", "a"),
        GraphNodeFactory.Create(GraphScope.Session, "line", "b"),
        GraphNodeFactory.Create(GraphScope.Session, "line", "c")]), GraphScope.Session);

    [TestMethod]
    public void DeleteLastDescendantsPrunesAncestorsAndUndoRestoresMembership()
    {
        var host = Host();
        host.RestoreFrames([
            new("inner", "Inner", 0, 0, 100, 100, ["a", "b"]),
            new("outer", "Outer", 0, 0, 100, 100, []) { Groups = ["inner"] }]);
        Assert.IsTrue(host.RemoveNodes(["a", "b"], true));
        Assert.IsEmpty(host.Frames);
        Assert.HasCount(1, host.Nodes);
        Assert.IsTrue(host.Undo());
        Assert.HasCount(3, host.Nodes);
        Assert.HasCount(2, host.Frames);
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, host.Frames.Single(f => f.Id == "inner").Members);
        Assert.IsTrue(host.Redo());
        Assert.IsEmpty(host.Frames);
        Assert.HasCount(1, host.Nodes);
    }

    [TestMethod]
    public void ReparentAndCoordinatesShareOneUndoWithoutGraphMutation()
    {
        var host = Host();
        host.RestoreFrames([new("group", "Group", 0, 0, 100, 100, ["a", "b"])]);
        var json = host.Graph.ToJson();
        var before = new Dictionary<string, GraphEditorNodePosition> { ["c"] = host.Nodes.Single(n => n.NodeId == "c").Position };
        var after = new Dictionary<string, GraphEditorNodePosition> { ["c"] = new(500, 300) };
        host.CommitGroupMove(before, after, ["c"], "group");
        Assert.AreEqual(500d, host.Nodes.Single(n => n.NodeId == "c").X);
        CollectionAssert.Contains(host.Frames.Single().Members, "c");
        Assert.AreEqual(json, host.Graph.ToJson());
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(before["c"], host.Nodes.Single(n => n.NodeId == "c").Position);
        CollectionAssert.DoesNotContain(host.Frames.Single().Members, "c");
        Assert.IsTrue(host.Redo());
        Assert.AreEqual(after["c"], host.Nodes.Single(n => n.NodeId == "c").Position);
    }

    [TestMethod]
    public void GroupDeleteRejectsProtectedDescendantAtomically()
    {
        var start = GraphNodeFactory.Create(GraphScope.Session, "start", "start");
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var host = new GraphEditorHostViewModel(new([start, line]), GraphScope.Session);
        host.RestoreFrames([new("group", "Group", 0, 0, 100, 100, ["start", "line"])]);
        var before = host.Graph.ToJson();
        Assert.IsFalse(host.DeleteGroups(["group"], [], true));
        Assert.AreEqual(before, host.Graph.ToJson());
        Assert.HasCount(1, host.Frames);
        Assert.IsFalse(host.CanUndo);
    }
}
