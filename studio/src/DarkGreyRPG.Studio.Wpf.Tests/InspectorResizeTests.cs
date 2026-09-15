using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class InspectorResizeTests
{
    [STATestMethod]
    public void GraphViewportResizePreservesCentreAndRenderedNodeGeometry()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "start", "Source", [
                new("out", "Output", false, GraphInterfaceKind.Flow)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = new CanonicalGraphEditorView(host);
        var root = new Grid { Width = 900, Height = 600 };
        root.Children.Add(view);
        root.Measure(new Size(900, 600));
        root.Arrange(new Rect(0, 0, 900, 600));
        root.UpdateLayout();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        view.ViewportState = new GraphViewportState { PanX = 73, PanY = -29, Zoom = 1.4 };
        root.UpdateLayout();
        var visual = view.NodeVisuals.Single();
        var beforeViewport = view.ViewportElement.RenderSize;
        var beforeCentre = new Point(beforeViewport.Width / 2, beforeViewport.Height / 2);
        var beforeCentreGraph = view.ScreenToGraph(beforeCentre);
        var beforeScreen = visual.TransformToAncestor(view.ViewportElement).Transform(new Point());
        var beforeSize = new Size(visual.ActualWidth, visual.ActualHeight);
        var beforeZoom = view.ViewportController.Zoom;
        var beforeGraph = graph.ToJson();

        root.Width = 1_000;
        root.Measure(new Size(1_000, 600));
        root.Arrange(new Rect(0, 0, 1_000, 600));
        root.UpdateLayout();

        var afterViewport = view.ViewportElement.RenderSize;
        var afterCentre = new Point(afterViewport.Width / 2, afterViewport.Height / 2);
        var afterCentreGraph = view.ScreenToGraph(afterCentre);
        var afterScreen = visual.TransformToAncestor(view.ViewportElement).Transform(new Point());
        Assert.AreEqual(beforeCentreGraph.X, afterCentreGraph.X, 1e-8);
        Assert.AreEqual(beforeCentreGraph.Y, afterCentreGraph.Y, 1e-8);
        Assert.AreEqual((afterViewport.Width - beforeViewport.Width) / 2,
            afterScreen.X - beforeScreen.X, 1e-8);
        Assert.AreEqual((afterViewport.Height - beforeViewport.Height) / 2,
            afterScreen.Y - beforeScreen.Y, 1e-8);
        Assert.AreEqual(beforeZoom, view.ViewportController.Zoom, 1e-8);
        Assert.AreEqual(beforeSize, new Size(visual.ActualWidth, visual.ActualHeight));
        Assert.AreEqual(beforeGraph, graph.ToJson());
        Assert.AreEqual(0, host.Session.UndoCount);
    }

    [STATestMethod]
    public void GraphSwapRestoresTheSelectedCameraBeforeResize()
    {
        var firstHost = new GraphEditorHostViewModel(new GraphDocument([
            new GraphNode("first", "start", "First", [
                new("out", "Output", false, GraphInterfaceKind.Flow)])]), GraphScope.StoryFlow);
        var secondGraph = new GraphDocument([
            new GraphNode("second", "start", "Second", [
                new("out", "Output", false, GraphInterfaceKind.Flow)])]);
        var secondHost = new GraphEditorHostViewModel(secondGraph, GraphScope.StoryFlow);
        var view = new CanonicalGraphEditorView(firstHost);
        var root = new Grid { Width = 900, Height = 600 };
        root.Children.Add(view);
        root.Measure(new Size(900, 600));
        root.Arrange(new Rect(0, 0, 900, 600));
        root.UpdateLayout();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        view.ViewportState = new GraphViewportState { PanX = 21, PanY = 34, Zoom = 1.1 };
        view.Host = secondHost;
        view.ViewportState = new GraphViewportState { PanX = -57, PanY = 12, Zoom = .85 };
        root.UpdateLayout();
        var beforeViewport = view.ViewportElement.ActualWidth;
        var beforeCentre = new Point(view.ViewportElement.ActualWidth / 2, view.ViewportElement.ActualHeight / 2);
        var beforeCentreGraph = view.ScreenToGraph(beforeCentre);
        var beforeNode = view.NodeVisuals.Single();
        var beforeScreen = beforeNode.TransformToAncestor(view.ViewportElement).Transform(new Point());
        var beforeGraph = secondGraph.ToJson();

        root.Width = 1_000;
        root.Measure(new Size(1_000, 600));
        root.Arrange(new Rect(0, 0, 1_000, 600));
        root.UpdateLayout();
        var afterCentre = new Point(view.ViewportElement.ActualWidth / 2, view.ViewportElement.ActualHeight / 2);
        var afterCentreGraph = view.ScreenToGraph(afterCentre);
        var afterScreen = beforeNode.TransformToAncestor(view.ViewportElement).Transform(new Point());
        Assert.AreEqual(beforeCentreGraph.X, afterCentreGraph.X, 1e-8);
        Assert.AreEqual(beforeCentreGraph.Y, afterCentreGraph.Y, 1e-8);
        Assert.AreEqual((view.ViewportElement.ActualWidth - beforeViewport) / 2,
            afterScreen.X - beforeScreen.X, 1e-8);
        Assert.AreEqual(-57 + (view.ViewportElement.ActualWidth - beforeViewport) / 2,
            view.ViewportController.PanX, 1e-8);
        Assert.AreEqual(12, view.ViewportController.PanY, 1e-8);
        Assert.AreEqual(.85, view.ViewportController.Zoom, 1e-8);
        Assert.AreEqual(beforeGraph, secondGraph.ToJson());
    }

    [STATestMethod]
    public void LibraryAndInspectorSplittersPreserveRenderedGraphCamera()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([
                new GraphNode("source", "start", "Source", [
                    new("out", "Output", false, GraphInterfaceKind.Flow)])])));
        var view = new CanonicalStoryWorkspaceView(workspace);
        const double width = 1_200;
        var root = new Grid { Width = width, Height = 720 };
        root.Children.Add(view);
        root.Measure(new Size(width, 720));
        root.Arrange(new Rect(0, 0, width, 720));
        root.UpdateLayout();

        var layout = (Grid)view.Content;
        var graph = view.FindName("WorkspaceGraph") as CanonicalGraphEditorView;
        Assert.IsNotNull(graph);
        graph!.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        graph.ViewportState = new GraphViewportState { PanX = 41, PanY = 16, Zoom = 1.25 };
        root.UpdateLayout();
        var visual = graph.NodeVisuals.Single();

        var beforeLibrarySize = graph.ViewportElement.RenderSize;
        var beforeLibraryScreen = visual.TransformToAncestor(graph.ViewportElement).Transform(new Point());
        var librarySplitter = Descendants<GridSplitter>(view)
            .Single(candidate => Grid.GetColumn(candidate) == 1);
        librarySplitter.RaiseEvent(new DragStartedEventArgs(0, 0));
        librarySplitter.RaiseEvent(new DragDeltaEventArgs(-40, 0));
        librarySplitter.RaiseEvent(new DragCompletedEventArgs(-40, 0, false));
        root.UpdateLayout();
        var afterLibrarySize = graph.ViewportElement.RenderSize;
        var afterLibraryScreen = visual.TransformToAncestor(graph.ViewportElement).Transform(new Point());
        Assert.AreEqual((afterLibrarySize.Width - beforeLibrarySize.Width) / 2,
            afterLibraryScreen.X - beforeLibraryScreen.X, 1e-8);
        Assert.AreEqual((afterLibrarySize.Height - beforeLibrarySize.Height) / 2,
            afterLibraryScreen.Y - beforeLibraryScreen.Y, 1e-8);

        var beforeInspectorSize = afterLibrarySize;
        var beforeInspectorScreen = afterLibraryScreen;
        var inspectorSplitter = Descendants<GridSplitter>(view)
            .Single(candidate => Grid.GetColumn(candidate) == 3);
        inspectorSplitter.RaiseEvent(new DragStartedEventArgs(0, 0));
        inspectorSplitter.RaiseEvent(new DragDeltaEventArgs(-80, 0));
        inspectorSplitter.RaiseEvent(new DragCompletedEventArgs(-80, 0, false));
        root.UpdateLayout();
        var afterInspectorSize = graph.ViewportElement.RenderSize;
        var afterInspectorScreen = visual.TransformToAncestor(graph.ViewportElement).Transform(new Point());
        Assert.AreEqual((afterInspectorSize.Width - beforeInspectorSize.Width) / 2,
            afterInspectorScreen.X - beforeInspectorScreen.X, 1e-8);
        Assert.AreEqual((afterInspectorSize.Height - beforeInspectorSize.Height) / 2,
            afterInspectorScreen.Y - beforeInspectorScreen.Y, 1e-8);
        Assert.AreEqual(1.25, graph.ViewportController.Zoom, 1e-8);
        Assert.AreEqual(0, workspace.StoryEditor.Host.Session.UndoCount);
    }

    [STATestMethod]
    public void InspectorSplitterShrinksGraphViewport()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()));
        var view = new CanonicalStoryWorkspaceView(workspace);
        const double width = 867;
        var root = new Grid { Width = width, Height = 720 };
        root.Children.Add(view);
        root.Measure(new Size(width, 720));
        root.Arrange(new Rect(0, 0, width, 720));
        root.UpdateLayout();

        var layout = (Grid)view.Content;
        var graph = view.FindName("WorkspaceGraph") as CanonicalGraphEditorView;
        Assert.IsNotNull(graph);
        var splitter = Descendants<GridSplitter>(view)
            .Single(candidate => Grid.GetColumn(candidate) == 3);
        var beforeLibraryWidth = layout.ColumnDefinitions[0].ActualWidth;
        var beforeGraphWidth = graph.ActualWidth;
        var beforeInspectorWidth = layout.ColumnDefinitions[4].ActualWidth;

        splitter.RaiseEvent(new DragStartedEventArgs(0, 0));
        splitter.RaiseEvent(new DragDeltaEventArgs(-80, 0));
        splitter.RaiseEvent(new DragCompletedEventArgs(-80, 0, false));
        root.UpdateLayout();

        Assert.AreEqual(0d, layout.ColumnDefinitions[0].ActualWidth - beforeLibraryWidth, 0.1);
        Assert.IsGreaterThan(beforeInspectorWidth, layout.ColumnDefinitions[4].ActualWidth);
        Assert.IsLessThan(beforeGraphWidth, graph.ActualWidth);
        Assert.AreEqual(layout.ColumnDefinitions[2].ActualWidth, graph.ViewportElement.ActualWidth, 0.1);
        Assert.IsLessThanOrEqualTo(width + 0.1, layout.ColumnDefinitions.Cast<ColumnDefinition>().Sum(definition => definition.ActualWidth));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
