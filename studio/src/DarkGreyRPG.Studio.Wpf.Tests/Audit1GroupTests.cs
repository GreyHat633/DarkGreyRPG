using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;
[TestClass, DoNotParallelize]
public sealed class Audit1GroupTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static GraphEditorHostViewModel Host() => new(new GraphDocument([
        GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "a"),
        GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "b"),
        GraphNodeFactory.Create(GraphScope.StoryFlow, "session", "s")]), GraphScope.StoryFlow);

    [STATestMethod]
    public void LiveResizeFrozenGDragAndSelectionBorderAgreeWithState()
    {
        var host = Host(); host.SetNodePosition("a", 100, 100); host.SetNodePosition("b", 600, 100);
        host.RestoreFrames([new("g", "Group", 0, 0, 100, 100, ["a", "b"]) { Color = "#CC7D87" }]);
        var view = new CanonicalGraphEditorView(host);
        var window = new Window { Content = view, Width = 1000, Height = 800, Left = -10000, ShowInTaskbar = false };
        try {
            window.Show(); window.UpdateLayout();
            var bounds = (Dictionary<string, Rect>)typeof(CanonicalGraphEditorView).GetField("_groupBounds", Private)!.GetValue(view)!;
            var visuals = (List<FrameworkElement>)typeof(CanonicalGraphEditorView).GetField("_frameVisuals", Private)!.GetValue(view)!;
            var border = (Border)visuals.Single();
            view.SelectGroup("g"); Assert.AreEqual(Brushes.DodgerBlue, border.BorderBrush);
            view.SelectNode("a"); Assert.IsEmpty(view.SelectedGroups);
            Assert.AreEqual(Color.FromRgb(204,125,135), ((SolidColorBrush)border.BorderBrush).Color);
            var before = bounds["g"];
            Assert.IsTrue(view.BeginSelectedNodeDrag("a"));
            view.UpdateSelectedNodeDrag(new Vector(-150, 0));
            Assert.AreEqual(before.Left - 150, bounds["g"].Left);
            view.CompleteSelectedNodeDrag(); Assert.IsTrue(host.Undo()); window.UpdateLayout();
            view.SelectNode("a"); view.BeginSelectedNodeDrag("a");
            typeof(CanonicalGraphEditorView).GetField("_gPressed", Private)!.SetValue(view, true);
            var frozen = bounds["g"];
            view.UpdateSelectedNodeDrag(new Vector(-400, 0)); Assert.AreEqual(frozen, bounds["g"]);
            view.CompleteSelectedNodeDrag();
            CollectionAssert.AreEqual(new[] { "b" }, host.Frames.Single().Members);
            Assert.IsTrue(host.Undo()); CollectionAssert.AreEquivalent(new[] { "a", "b" }, host.Frames.Single().Members);
            view.SelectGroup("g"); view.ClearSelection();
            Assert.AreEqual(Color.FromRgb(204,125,135), ((SolidColorBrush)border.BorderBrush).Color);
        } finally { window.Close(); }
    }

    [STATestMethod]
    public void UnifiedMenuUsesCapabilityAndPersistsColorWithUndo()
    {
        var host = Host(); var view = new CanonicalGraphEditorView(host);
        var window = new Window { Content = view, Width = 1000, Height = 800, Left = -10000, ShowInTaskbar = false };
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "audit1-colors-" + Guid.NewGuid().ToString("N"));
        try {
            window.Show(); window.UpdateLayout();
            Assert.IsTrue(CanonicalGraphEditorView.IsAdditiveSelectionModifier(ModifierKeys.Shift));
            Assert.IsTrue(CanonicalGraphEditorView.IsAdditiveSelectionModifier(ModifierKeys.Control));
            view.SelectNodes(["a", "b"]);
            var menu = view.CreateNodeContextMenu(host.Nodes.Single(n => n.NodeId == "a"));
            var group = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "组合"));
            Assert.IsFalse(menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "流程图")).IsEnabled);
            group.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "组合")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var frame = host.Frames.Single(); CollectionAssert.Contains(GraphCommentFrame.Palette, frame.Color);
            view.SelectGroup(frame.Id);
            var target = (FrameworkElement)view.FindName("GraphCanvas");
            var groupMenu = (ContextMenu)typeof(CanonicalGraphEditorView).GetMethod("CreateSelectionContextMenu", Private)!.Invoke(view, [null, target])!;
            CollectionAssert.AreEqual(new[] { "流程图", "复制", "粘贴", "组合", "删除" }, groupMenu.Items.OfType<MenuItem>().Select(i => (string)i.Header).ToArray());
            var colors = groupMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "组合")).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "修改颜色"));
            // Initial group colors are random; choose a different color so Undo targets this edit.
            var targetColor = frame.Color == "#B68ACC" ? GraphCommentFrame.Palette[0] : "#B68ACC";
            var color = colors.Items.OfType<MenuItem>().First(i => i.Header.ToString() == (frame.Color == "#B68ACC" ? "蓝色" : "紫色")); color.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual(targetColor, host.Frames.Single().Color);
            Assert.IsTrue(host.Undo()); Assert.AreEqual(frame.Color, host.Frames.Single().Color);
            Assert.IsTrue(host.Redo());
            var store = new CanonicalGraphLayoutStore(path); store.SaveFrames("story:x", host.Frames);
            Assert.AreEqual(targetColor, new CanonicalGraphLayoutStore(path).LoadFrames("story:x").Single().Color);
            var edits = 0; view.NodeEditRequested += _ => edits++;
            var flow = view.CreateNodeContextMenu(host.Nodes.Single(n => n.NodeId == "s")).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "流程图"));
            Assert.IsTrue(flow.IsEnabled); flow.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert.AreEqual(1, edits);
        } finally { window.Close(); if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true); }
    }
}
