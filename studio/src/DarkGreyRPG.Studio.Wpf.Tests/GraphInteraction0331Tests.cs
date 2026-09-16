using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;
using WpfPath = System.Windows.Shapes.Path;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GraphInteraction0331Tests
{
    [STATestMethod]
    public void FreeWirePreviewNeverOvershootsInEitherDirectionOrNearTheAnchor()
    {
        var start = new Point(300, 200);
        foreach (var end in new[] { new Point(30, 450), new Point(600, 10), new Point(301, 260), new Point(300, 200) })
        {
            var geometry = CanonicalGraphEditorView.DragWireGeometry(start, end);
            var curve = (BezierSegment)geometry.Figures.Single().Segments.Single();
            Assert.AreEqual(end, curve.Point3);
            for (var step = 0; step <= 100; step++)
            {
                var t = step / 100d;
                var u = 1 - t;
                var x = u * u * u * start.X + 3 * u * u * t * curve.Point1.X + 3 * u * t * t * curve.Point2.X + t * t * t * end.X;
                Assert.IsTrue(x >= Math.Min(start.X, end.X) - .001 && x <= Math.Max(start.X, end.X) + .001);
            }
        }
    }

    [STATestMethod]
    public void ParameterBlankHitRemainsDraggableWhileRenderedEditorControlsOwnPresses()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var host = new GraphEditorHostViewModel(new GraphDocument([line]), GraphScope.Session);
        var view = Arrange(host);
        var visual = view.NodeVisuals.Single();
        var textBox = Descendants<TextBox>(visual).Single(control =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "InlineLineTextEditor");
        var textHit = VisualTreeHelper.HitTest(visual, textBox.TranslatePoint(
            new Point(Math.Max(1, textBox.ActualWidth / 2), Math.Max(1, textBox.ActualHeight / 2)), visual))?.VisualHit as DependencyObject;
        Assert.IsNotNull(textHit, "The rendered TextBox must be hit-testable.");
        Assert.IsTrue(visual.IsParameterInteractionSource(textHit));

        var expander = Descendants<Expander>(visual).Single();
        var blankHit = FindNonInteractiveParameterHit(visual, expander);
        Assert.IsNotNull(blankHit,
            "Expected a real hit in the rendered parameter surface outside editor controls.");
        Assert.IsFalse(visual.IsParameterInteractionSource(blankHit));
        // Exercise the actual routed press: classifying the surface alone
        // misses a second header-only gate in the viewport event handler.
        textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Assert.IsFalse(view.UpdateSelectedNodeDrag(new Vector(40, 20)), "Editing text must not begin a node drag.");
        var blankElement = blankHit as UIElement;
        Assert.IsNotNull(blankElement);
        var originalPosition = host.Nodes.Single().Position;
        blankElement.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Assert.IsTrue(view.UpdateSelectedNodeDrag(new Vector(40, 20)), "A routed blank-surface press must enter node drag mode.");
        Assert.IsTrue(view.CompleteSelectedNodeDrag());
        var movedPosition = host.Nodes.Single().Position;
        Assert.AreNotEqual(originalPosition, movedPosition);
        Assert.IsTrue(host.Undo(), "Single-node dragging must record a layout history transaction.");
        Assert.AreEqual(originalPosition, host.Nodes.Single().Position);
        Assert.IsTrue(host.Redo());
        Assert.AreEqual(movedPosition, host.Nodes.Single().Position);
    }

    [STATestMethod]
    public void LayoutCompletionReanchorsVisibleAndHitWiresAfterRepeatedUndoRedo()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "action", "Source", [
                new("flow_out", "Output", false, GraphInterfaceKind.Flow)]),
            new GraphNode("target", "terminate", "Target", [
                new("flow_in", "Input", true, GraphInterfaceKind.Flow)])],
            [new("source", "flow_out", "target", "flow_in", GraphInterfaceKind.Flow)]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        host.SetNodePosition("source", 40, 40);
        host.SetNodePosition("target", 360, 170);
        var view = Arrange(host);
        // Re-emit one real layout notification after the visual tree is
        // arranged; this is the same completion path used by Undo/Redo and
        // ensures the baseline also exercises post-measure anchor refresh.
        host.SetNodePosition("source", 41, 40);
        host.SetNodePosition("target", 361, 170);
        PumpLayout(view);
        AssertWireAnchors(view);

        Assert.IsTrue(view.SelectNodes(["source", "target"]));
        for (var round = 0; round < 50; round++)
        {
            var delta = new Vector(45 + round, -12 + round % 5);
            Assert.IsTrue(view.BeginSelectedNodeDrag("source"));
            Assert.IsTrue(view.UpdateSelectedNodeDrag(delta));
            Assert.IsTrue(view.CompleteSelectedNodeDrag());
            PumpLayout(view);
            AssertWireAnchors(view);

            Assert.IsTrue(host.Undo());
            PumpLayout(view);
            AssertWireAnchors(view);

            Assert.IsTrue(host.Redo());
            PumpLayout(view);
            AssertWireAnchors(view);
        }
    }

    private static DependencyObject? FindNonInteractiveParameterHit(
        CanonicalGraphNodeControl visual, Expander expander)
    {
        // Search the actual rendered visual surface below the Expander header;
        // this avoids asserting against a synthetic Grid or a guessed offset.
        var startY = Math.Min(expander.ActualHeight - 1, 24);
        for (var y = startY; y < expander.ActualHeight; y += 2)
        {
            for (var x = 0d; x < expander.ActualWidth; x += 2)
            {
                var point = expander.TranslatePoint(new Point(x, y), visual);
                if (point.X < 0 || point.Y < 0 || point.X > visual.ActualWidth || point.Y > visual.ActualHeight)
                    continue;
                if (VisualTreeHelper.HitTest(visual, point)?.VisualHit is not DependencyObject hit
                    || !expander.IsAncestorOf(hit)
                    || visual.IsParameterInteractionSource(hit))
                    continue;
                return hit;
            }
        }

        return null;
    }

    private static void AssertWireAnchors(CanonicalGraphEditorView view)
    {
        view.UpdateLayout();
        var from = view.PortVisuals.Single(port => port.NodeId == "source");
        var to = view.PortVisuals.Single(port => port.NodeId == "target");
        var wire = view.ConnectionVisuals.Single();
        var hit = view.ConnectionHitTargets.Single();
        Assert.IsTrue(view.NodeVisuals.All(node => Panel.GetZIndex(wire) > Panel.GetZIndex(node)));
        Assert.IsFalse(wire.IsHitTestVisible, "The raised visual must not block ports or editors.");
        AssertPathMatchesAnchor(wire, from, start: true);
        AssertPathMatchesAnchor(wire, to, start: false);
        AssertPathMatchesAnchor(hit, from, start: true);
        AssertPathMatchesAnchor(hit, to, start: false);
    }

    private static void AssertPathMatchesAnchor(
        WpfPath path, FlowPortControl port, bool start)
    {
        var geometry = (PathGeometry)path.Data;
        var figure = geometry.Figures.Single();
        var point = start
            ? figure.StartPoint
            : ((BezierSegment)figure.Segments.Single()).Point3;
        // Path.Data is authored in its parent Canvas coordinate system. A
        // Shape's layout transform may scale the geometry for rendering, so
        // compare the canonical data coordinates directly with the same
        // parent-relative anchor coordinates.
        var pathPoint = point;
        Assert.IsInstanceOfType(path.Parent, typeof(Canvas));
        var anchor = port.GetAnchorPoint((Canvas)path.Parent);
        Assert.AreEqual(anchor.X, pathPoint.X, .1, $"Wire {(start ? "start" : "end")} X is stale.");
        Assert.AreEqual(anchor.Y, pathPoint.Y, .1, $"Wire {(start ? "start" : "end")} Y is stale.");
    }

    private static void PumpLayout(CanonicalGraphEditorView view)
    {
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        view.UpdateLayout();
    }

    private static CanonicalGraphEditorView Arrange(GraphEditorHostViewModel host)
    {
        var view = new CanonicalGraphEditorView(host);
        var root = new Grid { Width = 900, Height = 600 };
        root.Children.Add(view);
        root.Measure(new Size(900, 600));
        root.Arrange(new Rect(0, 0, 900, 600));
        root.UpdateLayout();
        return view;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
