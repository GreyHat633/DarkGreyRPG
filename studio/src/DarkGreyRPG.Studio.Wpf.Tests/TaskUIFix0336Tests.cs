using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TaskUIFix0336Tests
{
    [STATestMethod]
    public void TaskMenuAllowsRepeatedSettlementsAndDeleteToZero()
    {
        var host = new GraphEditorHostViewModel(new GraphDocument(), GraphScope.Task);
        var view = new CanonicalGraphEditorView(host);
        Assert.IsTrue(view.CanAuthorNodeType("settle"));
        Assert.AreEqual("流程", view.AuthoringDefinitions.Single(d => d.Type == "settle").Category);
        Assert.IsTrue(view.AddNodeAt("settle", 100, 100));
        Assert.IsTrue(view.CanAuthorNodeType("settle"));
        Assert.IsTrue(view.AddNodeAt("settle", 450, 100));
        var nodes = host.Graph.Nodes.ToArray();
        Assert.AreEqual(2, nodes.Select(n => n.Properties["port_id"].GetString()).Distinct().Count());
        CollectionAssert.AreEqual(new[] { 0, 1 }, nodes.Select(PublicOutputSchema.Order).ToArray());
        Assert.IsTrue(host.RemoveNodes(nodes.Select(n => n.Id).ToArray(), true));
        Assert.IsEmpty(host.Graph.Nodes);
        Assert.IsTrue(host.Undo());
        CollectionAssert.AreEqual(nodes.Select(n => n.Properties["port_id"].GetString()).ToArray(),
            host.Graph.Nodes.Select(n => n.Properties["port_id"].GetString()).ToArray());
    }

    [STATestMethod]
    public void InlineOutputHandleAndGlyphOwnInteractionForBothAggregateKinds()
    {
        foreach (var scope in new[] { GraphScope.Session, GraphScope.Task })
        {
            var graph = new GraphDocument();
            var author = new GraphNodeAuthoringService();
            if (scope == GraphScope.Session) graph.Nodes.Add(GraphNodeFactory.Create(scope, "start", "start"));
            foreach (var type in new[] { scope == GraphScope.Task ? "settle" : "end", "logic_output" })
                graph.Nodes.Add(author.Create(graph, scope, type, type).Candidate!);
            var child = new GraphEditorHostViewModel(graph, scope);
            var resource = new GraphResourceEnvelope(scope == GraphScope.Task ? GraphResourceKind.Task : GraphResourceKind.Session,
                "ST-2345-6789-ABCD-EFGH~" + (scope == GraphScope.Task ? "task" : "session") + "~drag", "Drag", graph);
            var aggregate = CanonicalAggregateNodeFactory.Create(resource, "aggregate").Candidate!;
            var parent = new GraphEditorHostViewModel(new GraphDocument([aggregate]), GraphScope.StoryFlow);
            var view = new CanonicalGraphEditorView(parent);
            view.InlineEditorFactory = node => { var vm = new CanonicalNodeInspectorViewModel(parent, node); vm.ConfigurePublicOutputs(child); return vm; };
            view.RefreshInlineEditors();
            var root = new Grid { Width = 900, Height = 700 }; root.Children.Add(view);
            root.Measure(new Size(900, 700)); root.Arrange(new Rect(0, 0, 900, 700)); root.UpdateLayout();
            var visual = view.NodeVisuals.Single();
            var handles = Descendants<Border>(visual).Where(EntryReorder.GetIsHandle).ToArray();
            Assert.HasCount(2, handles);
            foreach (var handle in handles)
            {
                Assert.IsTrue(visual.IsParameterInteractionSource(handle));
                Assert.IsTrue(visual.IsParameterInteractionSource(Descendants<TextBlock>(handle).Single()));
            }
            Assert.IsTrue(Descendants<TextBlock>(visual).Any(text => text.Text == "输出端口"));
            Assert.IsFalse(visual.IsParameterInteractionSource(visual));
            visual.DisposeInlineEditor();
        }
    }

    [STATestMethod]
    public void EmptyOutputSectionHasNoCaptionOrCardsAndRefreshesAfterAddingBoundary()
    {
        var host = new GraphEditorHostViewModel(new GraphDocument(), GraphScope.Task);
        using var outputs = new PublicOutputsViewModel(host, true);
        var editor = new PublicOutputsEditor { DataContext = outputs };
        Assert.IsFalse(outputs.HasOutputs);
        Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
        Assert.IsTrue(host.AddNode(new GraphNodeAuthoringService().Create(host.Graph, GraphScope.Task, "settle", "end").Candidate!));
        Assert.IsTrue(outputs.HasOutputs);
        Assert.IsTrue(outputs.HasFlow);
        Assert.IsFalse(outputs.HasLogic);
        editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        Assert.AreEqual(Visibility.Visible, editor.Visibility);
        Assert.IsTrue(host.RemoveNode("end", true));
        editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
