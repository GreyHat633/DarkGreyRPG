using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;
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
        Assert.AreEqual(new Rect(16, 128, 288, 48), dialogue);
        Assert.AreEqual(new Rect(16, 14, 288, 68), choice);
        Assert.AreEqual(160, dialogue.Left + dialogue.Width / 2, 0.0001);
        Assert.AreEqual(160, choice.Left + choice.Width / 2, 0.0001);
        Assert.IsTrue(choice.Bottom < dialogue.Top);
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
            Assert.AreEqual(2, thumbs.Count(item => item.Cursor == Cursors.SizeAll));
            var move = thumbs.Where(item => item.Cursor == Cursors.SizeAll).ElementAt(1);
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
            move = Descendants<Thumb>(editor).First(item => item.Cursor == Cursors.SizeAll);
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
            var down = Descendants<Button>(editor).Single(item => Equals(item.Content, "下移"));
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
}
