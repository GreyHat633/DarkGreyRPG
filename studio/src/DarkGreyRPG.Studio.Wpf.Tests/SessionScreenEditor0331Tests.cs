using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;
using System.Reflection;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class SessionScreenEditor0331Tests
{
    [TestMethod]
    public void SnapsToScreenAndOtherImageCentersAndCanBeBypassed()
    {
        var geometry = new ScreenLayerGeometry(0.507, 0.49, 0.2, 0.2, 0.5, 0.5);
        var snapped = SessionScreenEditor.SnapGeometry(geometry, []);
        Assert.AreEqual(0.5, snapped.Geometry.X, 0.000001);
        Assert.AreEqual(0.5, snapped.Geometry.Y, 0.000001);
        Assert.AreEqual(160d, snapped.GuideX);
        Assert.AreEqual(geometry, SessionScreenEditor.SnapGeometry(geometry, [], false).Geometry);
        var other = new ScreenLayerGeometry(0.31, 0.27, 0.2, 0.2, 0.5, 0.5);
        snapped = SessionScreenEditor.SnapGeometry(geometry with { X = 0.316, Y = 0.276 }, [other]);
        Assert.AreEqual(other.X, snapped.Geometry.X, 0.000001);
        Assert.AreEqual(other.Y, snapped.Geometry.Y, 0.000001);
    }

    [STATestMethod]
    public void SelectionPrecisionAndNamesStayConsistentAcrossEditorsAndReopen()
    {
        var root = Path.Combine(Path.GetFullPath("../../../../../../.tooling/0.3.3.1"), "screen-tests", Guid.NewGuid().ToString("N"));
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        using var resource = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Session, "selection-test", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.Single());
        inspector.SetScreenLayers(JsonSerializer.SerializeToElement(new[]
        {
            new { media_ref = "media/" + new string('a',64) + ".png", x=0.123456789012345, y=0.2, width=0.3, height=0.4, anchor_x=0d, anchor_y=0d, z=0 },
            new { media_ref = "media/" + new string('b',64) + ".png", x=0.8, y=0.7, width=0.2, height=0.3, anchor_x=0d, anchor_y=0d, z=1 }
        }));
        var first = new SessionScreenEditor { ProjectDirectory=root, DataContext=inspector };
        var second = new SessionScreenEditor { ProjectDirectory=root, DataContext=inspector };
        var panel = new StackPanel(); panel.Children.Add(first); panel.Children.Add(second);
        var window = new Window { Content=panel, Width=700, Height=900, ShowInTaskbar=false };
        try
        {
            window.Show(); window.UpdateLayout();
            ExpandProperties(first);
            ExpandProperties(second);
            var list = Descendants<ListBox>(first).Single();
            var otherList = Descendants<ListBox>(second).Single();
            var x = Descendants<TextBox>(first).Single(item => Equals(item.Tag,"x"));
            Assert.AreEqual("0.12",x.Text);
            var before = inspector.ScreenLayers.GetRawText();
            x.Focus();
            Assert.AreEqual("0.123456789012345",x.Text);
            Keyboard.ClearFocus();
            Assert.AreEqual(before,inspector.ScreenLayers.GetRawText(),"Merely focusing a rounded value cannot truncate it.");
            list.SelectedIndex=1;
            Assert.AreEqual(1,otherList.SelectedIndex);
            Assert.AreEqual("0.8",x.Text);
            Assert.AreEqual("0.8",Descendants<TextBox>(second).Single(item => Equals(item.Tag,"x")).Text);
            x.Focus(); x.Text="0.876543210987654"; Keyboard.ClearFocus();
            Assert.AreEqual(0.876543210987654,inspector.ScreenLayers[1].GetProperty("x").GetDouble());
            Assert.AreEqual("0.88",x.Text);
            var name = Descendants<TextBox>(first).Single(item => Equals(item.Tag,"name"));
            name.Focus(); name.Text="前景人物"; Keyboard.ClearFocus();
            Assert.IsTrue(otherList.Items[1].ToString()!.Contains("前景人物"));
            Assert.IsTrue(resource.Host.Undo());
            Assert.IsFalse(otherList.Items[1].ToString()!.Contains("前景人物"));
            Assert.IsTrue(resource.Host.Redo());
            Assert.IsTrue(otherList.Items[1].ToString()!.Contains("前景人物"));
            var down = Descendants<Button>(first).Single(item => Equals(item.Content,"▲"));
            down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(list.Items[0].ToString()!.Contains("前景人物"));
            Assert.IsTrue(resource.Host.Undo());
            Assert.IsTrue(list.Items[1].ToString()!.Contains("前景人物"));
            using var reopened = new CanonicalGraphResourceEditorViewModel(resource.CreateSnapshot());
            using var reopenedInspector = new CanonicalNodeInspectorViewModel(reopened.Host,reopened.Host.Nodes.Single());
            var reopenedEditor = new SessionScreenEditor { ProjectDirectory=root, DataContext=reopenedInspector };
            panel.Children.Add(reopenedEditor); window.UpdateLayout();
            Assert.IsTrue(Descendants<ListBox>(reopenedEditor).Single().Items[1].ToString()!.Contains("前景人物"));
            Assert.AreEqual(8,inspector.ScreenLayers[0].EnumerateObject().Count(),"Names must not change Runtime schema.");
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    [DataRow(0d, 0d)]
    [DataRow(0.5d, 0.5d)]
    [DataRow(0.2d, 0.7d)]
    public void TopLeftFieldsHideAnchorsPreserveLegacySceneAndResizeFromTopLeft(double anchorX, double anchorY)
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        using var resource = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Session, "legacy", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.Single());
        inspector.SetScreenLayers(JsonSerializer.SerializeToElement(new[]
        {
            new { media_ref = "media/" + new string('a',64) + ".png", x=0.5, y=0.6, width=0.4, height=0.6, anchor_x=anchorX, anchor_y=anchorY, z=0 }
        }));
        var editor = new SessionScreenEditor { DataContext=inspector };
        var window = new Window { Content=editor, Width=650, Height=850, ShowInTaskbar=false };
        try
        {
            var before = inspector.ScreenLayers.GetRawText();
            window.Show(); window.UpdateLayout();
            ExpandProperties(editor);
            Assert.IsFalse(Descendants<TextBox>(editor).Any(box => box.Tag is "anchor_x" or "anchor_y"));
            var left=0.5-anchorX*0.4; var top=0.6-anchorY*0.6;
            var x=Descendants<TextBox>(editor).Single(box => Equals(box.Tag,"x"));
            x.Focus();
            Assert.AreEqual(left,double.Parse(x.Text,System.Globalization.CultureInfo.InvariantCulture),1e-14);
            Keyboard.ClearFocus();
            Assert.AreEqual(before,inspector.ScreenLayers.GetRawText(),"Opening and focusing legacy data must not move it or rewrite anchors.");
            var width=Descendants<TextBox>(editor).Single(box => Equals(box.Tag,"width"));
            width.Focus(); width.Text="0.8"; Keyboard.ClearFocus();
            var layer=inspector.ScreenLayers[0];
            Assert.AreEqual(left,layer.GetProperty("x").GetDouble()-anchorX*layer.GetProperty("width").GetDouble(),1e-14);
            Assert.AreEqual(top,layer.GetProperty("y").GetDouble()-anchorY*layer.GetProperty("height").GetDouble(),1e-14);
            Assert.AreEqual(anchorX,layer.GetProperty("anchor_x").GetDouble());
            Assert.IsTrue(resource.Host.Undo()); Assert.AreEqual(before,inspector.ScreenLayers.GetRawText());
            x.Focus(); x.Text="0.15"; Keyboard.ClearFocus();
            layer=inspector.ScreenLayers[0];
            Assert.AreEqual(0.15,layer.GetProperty("x").GetDouble()-anchorX*layer.GetProperty("width").GetDouble(),1e-14);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ExposesEightResizeHandlesAndKeepsOppositeGeometryFixed()
    {
        Assert.AreEqual(8, Enum.GetValues<ScreenResizeHandle>().Length);
        var original = new ScreenLayerGeometry(0.5, 0.5, 0.4, 0.6, 0.25, 0.75);
        var before = original.CanvasRect();

        foreach (var handle in Enum.GetValues<ScreenResizeHandle>())
        {
            var resized = SessionScreenEditor.ResizeGeometry(original, handle, 16, 12);
            var after = resized.CanvasRect();
            Assert.IsTrue(after.Width > 0 && after.Height > 0, $"{handle} must keep a positive size.");
            if (handle is ScreenResizeHandle.Left or ScreenResizeHandle.TopLeft or ScreenResizeHandle.BottomLeft)
                Assert.AreEqual(before.Right, after.Right, 0.0001, $"{handle} must keep the right edge.");
            if (handle is ScreenResizeHandle.Right or ScreenResizeHandle.TopRight or ScreenResizeHandle.BottomRight)
                Assert.AreEqual(before.Left, after.Left, 0.0001, $"{handle} must keep the left edge.");
            if (handle is ScreenResizeHandle.Top or ScreenResizeHandle.TopLeft or ScreenResizeHandle.TopRight)
                Assert.AreEqual(before.Bottom, after.Bottom, 0.0001, $"{handle} must keep the bottom edge.");
            if (handle is ScreenResizeHandle.Bottom or ScreenResizeHandle.BottomLeft or ScreenResizeHandle.BottomRight)
                Assert.AreEqual(before.Top, after.Top, 0.0001, $"{handle} must keep the top edge.");
        }
    }

    [TestMethod]
    public void ResizeClampsToSchemaLimitsAndPreservesNonCornerAxis()
    {
        var original = new ScreenLayerGeometry(0.5, 0.5, 0.4, 0.6, 0.5, 0.5);
        var top = SessionScreenEditor.ResizeGeometry(original, ScreenResizeHandle.Top, 90, 1000);
        Assert.AreEqual(original.Width, top.Width, 0.0001);
        Assert.AreEqual(original.CanvasRect().Right, top.CanvasRect().Right, 0.0001);
        Assert.IsTrue(top.Height is > 0 and <= 4);
        Assert.IsTrue(top.X is >= -2 and <= 3);
        Assert.IsTrue(top.Y is >= -2 and <= 3);
    }

    [TestMethod]
    public void ReferenceRectsAreCenteredAndChoiceIsAboveDialogue()
    {
        var dialogue = SessionScreenEditor.DialogueReferenceRect;
        var choice = SessionScreenEditor.ChoiceReferenceRect;
        var rows = SessionScreenEditor.ChoiceReferenceOptionRects;
        Assert.AreEqual(new Rect(16, 128, 288, 48), dialogue);
        Assert.AreEqual(new Rect(16, 14, 288, 68), choice);
        CollectionAssert.AreEqual(
            new[] { new Rect(16, 14, 288, 20), new Rect(16, 38, 288, 20), new Rect(16, 62, 288, 20) },
            rows.ToArray());
        Assert.AreEqual(160, dialogue.Left + dialogue.Width / 2, 0.0001);
        Assert.AreEqual(160, choice.Left + choice.Width / 2, 0.0001);
        Assert.IsTrue(choice.Bottom < dialogue.Top);
        Assert.IsTrue(rows.All(row => row.Width > row.Height), "Each choice is a separate wide horizontal row.");
        Assert.IsFalse(rows.Any(row => row.Height == choice.Height), "Choice preview has no enclosing frame row.");
    }

    [STATestMethod]
    public void ChoicePreviewUsesIndependentRowsAndDialogueIncludesPortraitAndText()
    {
        var editor = new SessionScreenEditor();
        var window = new Window { Content = editor, Width = 700, Height = 700, ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
        try
        {
            window.Show();
            window.UpdateLayout();
            editor.UpdateLayout();
            var choice = Descendants<CheckBox>(editor).Single(item => Equals(item.Content, "选项框"));
            choice.IsChecked = true;
            editor.UpdateLayout();
            var canvas = Descendants<Canvas>(editor).Single();
            var rows = Descendants<Border>(canvas).Where(item => item.Width == 288 && item.Height == 20).ToArray();
            Assert.AreEqual(3, rows.Length);
            Assert.IsTrue(rows.All(item => !item.IsHitTestVisible));
            Assert.IsTrue(rows.SelectMany(Descendants<TextBlock>).All(item => item.TextAlignment == TextAlignment.Center));
            Assert.AreEqual(0, Descendants<Border>(canvas).Count(item => item.Width == 288 && item.Height == 68));
            Assert.IsTrue(Descendants<Border>(canvas).Where(item => item.Width == 288 && item.Height == 48).All(item => !item.IsHitTestVisible));
            Assert.IsTrue(Descendants<TextBlock>(canvas).Any(item => item.Text == "角色"));
            Assert.IsTrue(Descendants<TextBlock>(canvas).Any(item => item.Text == "头像"));
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void NumericPreviewMovesTransientLayerAndCommitsOneUndoOrRestoresOnCancel()
    {
        var layers = JsonSerializer.SerializeToElement(new[]
        {
            new { media_ref = "media/" + new string('c', 64) + ".png", x = 0.5, y = 0.5, width = 0.4, height = 0.5, anchor_x = 0.5, anchor_y = 0.5, z = 0 }
        });
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        using var resource = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Session, "session", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.Single());
        Assert.IsTrue(inspector.SetScreenLayers(layers));
        var editor = new SessionScreenEditor { DataContext = inspector };
        var window = new Window { Content = editor, Width = 700, Height = 700, ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
        try
        {
            window.Show();
            window.UpdateLayout();
            editor.UpdateLayout();
            ExpandProperties(editor);
            var field = Descendants<TextBox>(editor).Single(item => Equals(item.Tag, "x"));
            var before = inspector.ScreenLayers.GetRawText();
            var undoBefore = resource.Host.Session.UndoCount;

            field.RaiseEvent(new NumericDrag.PreviewedEventArgs("0.5", "0.75"));
            Assert.AreEqual(before, inspector.ScreenLayers.GetRawText(), "Preview stays in the editor's transient layer copy.");
            field.RaiseEvent(new NumericDrag.CommittedEventArgs("0.5", "0.75"));
            var committed = inspector.ScreenLayers.GetRawText();
            Assert.AreNotEqual(before, committed);
            Assert.AreEqual(undoBefore + 1, resource.Host.Session.UndoCount);

            field.RaiseEvent(new NumericDrag.PreviewedEventArgs("0.75", "0.25"));
            var stateProperty = (DependencyProperty)typeof(NumericDrag)
                .GetField("StateProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var state = field.GetValue(stateProperty)!;
            var stateType = state.GetType();
            stateType.GetField("Before")!.SetValue(state, "0.75");
            stateType.GetField("Pending")!.SetValue(state, true);
            stateType.GetField("Dragging")!.SetValue(state, true);
            var source = PresentationSource.FromVisual(field);
            Assert.IsNotNull(source);
            field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source!, 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.AreEqual(committed, inspector.ScreenLayers.GetRawText(), "Canceled numeric preview restores the committed layer.");
            Assert.AreEqual(undoBefore + 1, resource.Host.Session.UndoCount, "Canceled numeric preview adds no history.");
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void ImagePropertiesCardDefaultsCollapsedAndOnlyResetsForAnotherNode()
    {
        var firstNode = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen-a");
        var secondNode = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen-b");
        using var resource = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Session, "card", "Session", new GraphDocument([firstNode, secondNode])));
        using var first = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes[0]);
        using var second = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes[1]);
        var layers = JsonSerializer.SerializeToElement(new[]
        {
            new { media_ref = "media/" + new string('d', 64) + ".png", x = 0.5, y = 0.5, width = 0.4, height = 0.4, anchor_x = 0d, anchor_y = 0d, z = 0 }
        });
        Assert.IsTrue(first.SetScreenLayers(layers));
        Assert.IsTrue(second.SetScreenLayers(layers));
        var editor = new SessionScreenEditor { DataContext = first };
        var window = new Window { Content = editor, Width = 700, Height = 700, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            var header = PropertiesHeader(editor);
            Assert.AreEqual("展开图片属性", AutomationProperties.GetHelpText(header));
            var before = first.ScreenLayers.GetRawText();

            header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual("收起图片属性", AutomationProperties.GetHelpText(header));
            Assert.AreEqual(before, first.ScreenLayers.GetRawText(), "Toggling the card cannot mutate the runtime layer model.");

            editor.ProjectDirectory = Path.Combine(Path.GetFullPath("../../../../../../.tooling/0.3.3.1"), "card-tests", Guid.NewGuid().ToString("N"));
            Assert.AreEqual("收起图片属性", AutomationProperties.GetHelpText(header), "Refresh keeps the user's expansion choice.");
            var list = Descendants<ListBox>(editor).Single();
            list.SelectedIndex = 0;
            Assert.AreEqual("收起图片属性", AutomationProperties.GetHelpText(header), "Selection keeps the user's expansion choice.");

            editor.DataContext = second;
            Assert.AreEqual("展开图片属性", AutomationProperties.GetHelpText(header), "A different node starts collapsed.");
            Assert.AreEqual(before, first.ScreenLayers.GetRawText());
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void RoutedThumbGestureCommitsOnceSupportsUndoRedoAndEscapeCancel()
    {
        var imageOne = "media/" + new string('a', 64) + ".png";
        var imageTwo = "media/" + new string('b', 64) + ".png";
        var layers = JsonSerializer.SerializeToElement(new[]
        {
            new { media_ref = imageOne, x = 0.5, y = 0.5, width = 0.4, height = 0.5, anchor_x = 0.5, anchor_y = 0.5, z = 0 },
            new { media_ref = imageTwo, x = 0.5, y = 0.5, width = 0.2, height = 0.2, anchor_x = 0.5, anchor_y = 0.5, z = 1 }
        });
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        using var resource = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Session, "session", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.Single());
        Assert.IsTrue(inspector.SetScreenLayers(layers));
        var editor = new SessionScreenEditor { DataContext = inspector };
        var window = new Window { Content = editor, Width = 700, Height = 700, ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
        try
        {
            window.Show();
            window.UpdateLayout();
            editor.UpdateLayout();
            var thumbs = Descendants<Thumb>(editor).ToArray();
            Assert.AreEqual(10, thumbs.Length, "Two layers need move surfaces; the selected layer adds eight resize handles.");
            var moveThumbs = thumbs.Where(item => Equals(item.Tag, "screen-layer-move")).ToArray();
            Assert.AreEqual(2, moveThumbs.Length, "Two layers need independent move surfaces.");
            Assert.IsTrue(moveThumbs.All(item => item.Cursor?.ToString() == Cursors.SizeAll.ToString()), "Move surfaces retain their move cursor.");
            var move = moveThumbs[1];
            var before = inspector.ScreenLayers.GetRawText();
            var undoBefore = resource.Host.Session.UndoCount;

            move.RaiseEvent(new DragStartedEventArgs(0, 0));
            move.RaiseEvent(new DragDeltaEventArgs(24, 12));
            Assert.AreEqual(before, inspector.ScreenLayers.GetRawText(), "Live drag remains detached before release.");
            move.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
            var committed = inspector.ScreenLayers.GetRawText();
            Assert.AreNotEqual(before, committed);
            Assert.AreEqual(undoBefore + 1, resource.Host.Session.UndoCount, "One routed drag is one undo unit.");
            Assert.IsTrue(resource.Host.Undo());
            Assert.AreEqual(before, inspector.ScreenLayers.GetRawText());
            Assert.IsTrue(resource.Host.Redo());
            Assert.AreEqual(committed, inspector.ScreenLayers.GetRawText());

            var undoAtCancel = resource.Host.Session.UndoCount;
            move = Descendants<Thumb>(editor).First(item => Equals(item.Tag, "screen-layer-move"));
            move.RaiseEvent(new DragStartedEventArgs(0, 0));
            move.RaiseEvent(new DragDeltaEventArgs(40, 20));
            editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.AreEqual(committed, inspector.ScreenLayers.GetRawText(), "Escape restores the last committed snapshot.");
            Assert.AreEqual(undoAtCancel, resource.Host.Session.UndoCount, "Escape does not create history.");
            move.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
            Assert.AreEqual(undoAtCancel, resource.Host.Session.UndoCount, "A completion after Escape remains inert.");

            var list = Descendants<ListBox>(editor).Single();
            list.SelectedIndex = 0;
            var down = Descendants<Button>(editor).Single(item => Equals(item.Content, "▼"));
            var beforeReorder = inspector.ScreenLayers.GetRawText();
            var reorderUndo = resource.Host.Session.UndoCount;
            down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var reordered = inspector.ScreenLayers.GetRawText();
            Assert.AreNotEqual(beforeReorder, reordered);
            Assert.AreEqual(reorderUndo + 1, resource.Host.Session.UndoCount, "One reorder command is one undo unit.");
            Assert.AreEqual(0, inspector.ScreenLayers[0].GetProperty("z").GetInt32());
            Assert.IsTrue(resource.Host.Undo());
            Assert.AreEqual(beforeReorder, inspector.ScreenLayers.GetRawText());
            Assert.IsTrue(resource.Host.Redo());
            Assert.AreEqual(reordered, inspector.ScreenLayers.GetRawText());
        }
        finally { window.Close(); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static Button PropertiesHeader(SessionScreenEditor editor) =>
        Descendants<Button>(editor).Single(item => Equals(item.Tag, "screen-layer-properties-header"));

    private static void ExpandProperties(SessionScreenEditor editor)
    {
        var header = PropertiesHeader(editor);
        if (AutomationProperties.GetHelpText(header) == "展开图片属性")
            header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
