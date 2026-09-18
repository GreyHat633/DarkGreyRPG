using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class LinePagesAuthoringTests
{
    [STATestMethod]
    public void AnimatedBodyReversesAndSettlesAtNaturalHeight()
    {
        var body = new AnimatedLinePageBody { Width = 250, VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Child = new System.Windows.Controls.Border { Height = 200 } };
        var window = new System.Windows.Window { Content = body, Width = 300, Height = 350, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.AreEqual(200d, body.ActualHeight, .5);
            body.IsExpanded = false;
            Pump(65);
            var midway = body.ActualHeight;
            if (System.Windows.SystemParameters.ClientAreaAnimation)
                Assert.IsTrue(midway > 0 && midway < 200, $"Expected an intermediate height, got {midway}");
            body.IsExpanded = true;
            Pump(300);
            Assert.AreEqual(200d, body.ActualHeight, .5);
            body.IsExpanded = false;
            Pump(300);
            Assert.AreEqual(0d, body.ActualHeight, .5);
            Assert.AreEqual(System.Windows.Visibility.Hidden, body.Child.Visibility);
            Assert.IsFalse(body.IsHitTestVisible);
        }
        finally { window.Close(); }
        static void Pump(int milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
    }
    [STATestMethod]
    public void CardEdgeSelectsWithoutExpandingAndOutsideClickClearsBothEditors()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var first = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var second = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var view = new LinePagesEditor { DataContext = first, Width = 320 };
        var peer = new LinePagesEditor { DataContext = second, Width = 320 };
        var outside = new System.Windows.Controls.Button { Content = "Other node / canvas" };
        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(view); panel.Children.Add(peer); panel.Children.Add(outside);
        var window = new System.Windows.Window { Content = panel, Width = 350, Height = 700, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            var card = Descendants<System.Windows.Controls.Border>(view).Single(b => b.Name == "LinePageCard");
            var click = Click(card);
            Assert.IsTrue(click.Handled, "Edge must not fall through to canvas dragging");
            Assert.IsTrue(first.LinePages[0].IsSelected);
            Assert.IsTrue(second.LinePages[0].IsSelected);
            Assert.IsTrue(first.LinePages[0].IsExpanded, "Edge does not toggle expansion");
            Click(outside);
            Assert.IsFalse(first.LinePages[0].IsSelected);
            Assert.IsFalse(second.LinePages[0].IsSelected);
            Assert.IsFalse(first.CanRemoveSelectedLinePages);
            Click(card);
            Click(view);
            Assert.IsFalse(first.LinePages[0].IsSelected, "Empty editor space must also clear selection");
        }
        finally { window.Close(); }

        static System.Windows.Input.MouseButtonEventArgs Click(System.Windows.UIElement target)
        {
            var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent };
            target.RaiseEvent(args);
            return args;
        }
    }

    private static IEnumerable<T> Descendants<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    [TestMethod]
    public void MultiSelectionSharesStableIdsAndBatchDeleteAllowsEmptyWithSingleUndo()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        for (var i = 1; i < 5; i++) inline.AddLinePage();
        var ids = inline.LinePages.Select(p => p.PageId).ToArray();
        var undo = editor.Host.Session.UndoCount;
        inline.SelectLinePage(ids[1]);
        inline.SelectLinePage(ids[3], control: true);
        CollectionAssert.AreEqual(new[] { ids[1], ids[3] }, inspector.LinePages.Where(p => p.IsSelected).Select(p => p.PageId).ToArray());
        inline.SelectLinePage(ids[3], control: true);
        Assert.IsFalse(inspector.LinePages[3].IsSelected);
        inline.SelectLinePage(ids[1]);
        inspector.SelectLinePage(ids[3], shift: true);
        Assert.AreEqual(3, inline.LinePages.Count(p => p.IsSelected));
        Assert.AreEqual(undo, editor.Host.Session.UndoCount);
        Assert.IsTrue(inline.RemoveSelectedLinePages());
        CollectionAssert.AreEqual(new[] { ids[0], ids[4] }, inspector.LinePages.Select(p => p.PageId).ToArray());
        Assert.AreEqual(undo + 1, editor.Host.Session.UndoCount);
        Assert.IsTrue(editor.Host.Undo());
        CollectionAssert.AreEqual(ids, inline.LinePages.Select(p => p.PageId).ToArray());
        inline.SelectLinePage(ids[0]);
        inline.SelectLinePage(ids[4], shift: true);
        Assert.IsTrue(inspector.RemoveSelectedLinePages());
        Assert.AreEqual(0, inline.LinePages.Count);
        Assert.IsFalse(inline.CanRemoveSelectedLinePages);
        Assert.IsTrue(editor.Host.Undo());
        CollectionAssert.AreEqual(ids, inline.LinePages.Select(p => p.PageId).ToArray());
        inline.SelectLinePage(ids[0]);
        inline.SelectLinePage(ids[4], shift: true);
        inline.RemoveSelectedLinePages();
        Assert.IsNotNull(inline.AddLinePage());
        Assert.AreEqual(1, inspector.LinePages.Count);
    }
    [TestMethod]
    public void Collapse0332IsSharedByStablePageIdWithoutDataOrUndoChanges()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        for (var i = 1; i < 50; i++) inline.AddLinePage();
        var first = inline.LinePages.First();
        first.Text = "保留台词和声音设置";
        var before = JsonSerializer.Serialize(editor.Host.Graph);
        var undo = editor.Host.Session.UndoCount;
        Assert.IsTrue(first.IsExpanded);
        Assert.AreEqual("▲", first.ExpansionGlyph);
        first.IsExpanded = false;
        Assert.IsFalse(inspector.LinePages.First().IsExpanded);
        Assert.AreEqual("▼", first.ExpansionGlyph);
        Assert.AreEqual(before, JsonSerializer.Serialize(editor.Host.Graph));
        Assert.AreEqual(undo, editor.Host.Session.UndoCount);
        Assert.IsTrue(first.Move(49));
        Assert.IsFalse(inspector.LinePages.Last().IsExpanded);
        Assert.IsTrue(first.Remove());
        Assert.IsTrue(editor.Host.Undo());
        Assert.IsFalse(inline.LinePages.Last().IsExpanded);
        Assert.AreEqual(first.PageId, inline.LinePages.Last().PageId);
        using var freshEditor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "fresh", "Fresh", editor.Host.Graph));
        using var fresh = new CanonicalNodeInspectorViewModel(freshEditor.Host, freshEditor.Host.Nodes.Single());
        Assert.IsTrue(fresh.LinePages.All(p => p.IsExpanded));
    }

    [TestMethod]
    public void PagesKeepIdentitySettingsAndBothProjectionsThroughSingleUndoReorder()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var first = inline.LinePages.Single();
        Assert.IsTrue(first.CanRemove);
        first.Text = "第一句";
        var secondId = inline.AddLinePage();
        Assert.IsNotNull(secondId);
        var second = inline.LinePages.Last();
        second.Text = "第二句";
        second.CustomSpeed = true; second.Speed = 120; second.Volume = .35;
        second.AudioEnabled = true;
        Assert.IsTrue(inspector.LinePages.Last().AudioEnabled);
        Assert.IsFalse(inspector.LinePages.First().AudioEnabled);
        var voice = "media/" + new string('a', 64) + ".ogg";
        Assert.IsTrue(second.SetVoice(voice));
        var undo = editor.Host.Session.UndoCount;
        Assert.IsTrue(second.Move(0));
        Assert.AreEqual(undo + 1, editor.Host.Session.UndoCount);
        Assert.AreSame(second, inline.LinePages.First());
        Assert.AreEqual(secondId, inspector.LinePages.First().PageId);
        Assert.AreEqual(voice, inspector.LinePages.First().VoiceRef);
        Assert.AreEqual(.35, inspector.LinePages.First().Volume);
        Assert.AreEqual(120d, inspector.LinePages.First().Speed);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreSame(first, inline.LinePages.First());
        Assert.AreSame(second, inline.LinePages.Last());
        Assert.IsTrue(editor.Host.Redo());
        Assert.AreSame(second, inline.LinePages.First());
        Assert.IsTrue(second.Remove());
        Assert.IsTrue(inline.LinePages.Single().CanRemove);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(voice, inline.LinePages.First().VoiceRef);
    }

    [TestMethod]
    public void LegacyFirstPagePreservesConfigWhileNewPageHasDefaults()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "old");
        line.Properties.Remove("pages");
        line.Properties.Remove("pages");
        line.Properties["text"] = JsonSerializer.SerializeToElement("旧台词");
        line.Properties["text_speed"] = JsonSerializer.SerializeToElement(45);
        line.Properties["custom_text_speed"] = JsonSerializer.SerializeToElement(true);
        line.Properties["voice_volume"] = JsonSerializer.SerializeToElement(.4);
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var vm = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual("旧台词", vm.LinePages.Single().Text);
        Assert.AreEqual(45d, vm.LinePages.Single().Speed);
        vm.AddLinePage();
        Assert.AreEqual(.4, vm.LinePages.First().Volume);
        var added = vm.LinePages.Last();
        Assert.IsFalse(added.CustomSpeed); Assert.IsFalse(added.AudioEnabled); Assert.IsNull(added.PortraitVariant);
        Assert.AreEqual(30d, added.Speed); Assert.AreEqual(1d, added.Volume);
    }

    [STATestMethod]
    public void SpecialHeadersUseDistinctFrozenColorsAndOtherTypesKeepThemeAccent()
    {
        Assert.AreEqual("#FFF5B53D", NodeHeaderPalette.ForType("task")!.ToString());
        Assert.AreEqual("#FF65C3AD", NodeHeaderPalette.ForType("session")!.ToString());
        Assert.AreEqual("#FFE58A83", NodeHeaderPalette.ForType("story")!.ToString());
        Assert.IsNull(NodeHeaderPalette.ForType("line"));
        Assert.IsTrue(NodeHeaderPalette.Task.IsFrozen);
    }
}
