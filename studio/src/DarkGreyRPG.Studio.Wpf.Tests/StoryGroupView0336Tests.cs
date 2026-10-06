using System.Windows;
using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Projects;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class StoryGroupView0336Tests
{
    private const string A = "ST-2345-6789-ABCD-EFGH", B = "ST-JKLM-NPQR-STUV-WXYZ";

    [TestMethod]
    public void ReferenceRefreshRetainsHostPositionsAndHistoryAcrossAddRemoveAndRestore()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "temp", "reference0336-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "Source"); var target = Path.Combine(root, "Target");
            new ProjectService().CreateProject(source, "source", "Source");
            new ProjectService().CreateProject(target, "target", "Target");
            var store = new CanonicalProjectGraphStore(source);
            var lifecycle = new CanonicalStoryLifecycleService(store);
            foreach (var entry in new[] { (A, "logic_output", "out"), (B, "logic_input", "in") })
            {
                var story = lifecycle.Create(entry.Item1, entry.Item1);
                var graph = story.Graph!;
                var boundary = GraphNodeFactory.Create(GraphScope.StoryFlow, entry.Item2, entry.Item2);
                boundary.Properties["port_id"] = JsonSerializer.SerializeToElement(entry.Item3);
                boundary.Properties["display_name"] = JsonSerializer.SerializeToElement(entry.Item3);
                graph.Nodes.Add(boundary); story.Graph = graph; store.Stories.Replace(story);
            }
            var aStory = store.Stories.Load(A); var aGraph = aStory.Graph!;
            var input = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "native_entry");
            input.Properties["port_id"] = JsonSerializer.SerializeToElement("native_entry");
            input.Properties["display_name"] = JsonSerializer.SerializeToElement("Native entry");
            aGraph.Nodes.Add(input); aStory.Graph = aGraph; store.Stories.Replace(aStory);
            store.StoryLogicGraph.Save([new(A, "out", B, "in")]);
            var package = Path.Combine(root, "group.dgrs.g");
            new DgrsGroupPackageExporter(source).Build(A, package, "0.3.3.6");
            const string native = "ST-AAAA-BBBB-CCCC-DDDD";
            var targetStore = new CanonicalProjectGraphStore(target);
            var nativeStory = new CanonicalStoryLifecycleService(targetStore).Create(native, "Native");
            var nativeGraph = nativeStory.Graph!;
            var output = new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "logic_output", "native_output").Candidate!;
            output.Properties["port_id"] = JsonSerializer.SerializeToElement("native_output");
            output.Properties["display_name"] = JsonSerializer.SerializeToElement("Native output");
            nativeGraph.Nodes.Add(output); nativeStory.Graph = nativeGraph; targetStore.Stories.Replace(nativeStory);
            var model = new ProjectGraphViewModel([], projectDirectory: target);
            var host = model.CanonicalHost!;
            var edit = 0; host.EditMetadata(() => edit = 0, () => edit = 1);
            var references = Path.Combine(target, "references"); Directory.CreateDirectory(references);
            var installed = Path.Combine(references, "group.dgrs.g"); File.Copy(package, installed);
            targetStore.StoryLogicGraph.Save([new(native, "native_output", A, "native_entry")]);
            var nativeEdges = File.ReadAllBytes(targetStore.StoryLogicGraph.Path);
            model.RefreshReferencedStories();
            Assert.AreSame(host, model.CanonicalHost);
            Assert.HasCount(3, host.Nodes); Assert.HasCount(2, host.Connections);
            Assert.HasCount(1, model.StoryGroups.Groups); Assert.IsTrue(model.IsReferencedStory(A));
            host.SetNodePosition(A, 320, 180);
            File.Move(installed, installed + ".removed"); model.RefreshReferencedStories();
            Assert.HasCount(1, host.Nodes); Assert.IsTrue(host.CanUndo);
            Assert.IsFalse(host.CommitGraphChange!());
            CollectionAssert.AreEqual(nativeEdges, File.ReadAllBytes(targetStore.StoryLogicGraph.Path));
            File.Move(installed + ".removed", installed); model.RefreshReferencedStories();
            Assert.AreEqual(320d, host.Nodes.Single(node => node.NodeId == A).X);
            Assert.IsTrue(host.Undo()); Assert.AreEqual(0, edit);
            Assert.HasCount(3, host.Nodes); Assert.HasCount(2, host.Connections);
            Assert.IsTrue(host.Redo()); Assert.AreEqual(1, edit);
        }
        finally { Directory.Delete(root, true); }
    }

    [STATestMethod]
    public void DerivedFrameFollowsPositionsNamesAndSplitWithoutManualMembership()
    {
        var host = new GraphEditorHostViewModel(new GraphDocument([new(A, "story", "A"), new(B, "story", "B")]), GraphScope.Project);
        host.SetNodePosition(A, 40, 60); host.SetNodePosition(B, 440, 60);
        var group = StoryGroupCatalog.Derive([A, B], new(2, [new(A, "out", B, "in")]));
        host.RestoreFrames(group.Groups.Select(g => new GraphCommentFrame(g.Key, g.DisplayName, 0, 0, 80, 50, g.Members.ToArray())));
        var view = new CanonicalGraphEditorView(host) { StoryGroups = group };
        var window = new Window { Content = view, Width = 1000, Height = 600, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout(); view.FitAllNodes(); window.UpdateLayout();
            Border Frame() => Descendants(view).OfType<Border>().Single(border => AutomationProperties.GetAutomationId(border).StartsWith("StoryGroupFrame_", StringComparison.Ordinal));
            var frame = Frame(); var width = frame.Width;
            Assert.IsTrue(frame.IsHitTestVisible);
            Assert.HasCount(1, host.Frames);
            host.SetNodePosition(B, 800, 60); view.FitAllNodes();
            Assert.AreSame(frame, Frame()); Assert.IsGreaterThan(width, frame.Width);
            host.UpdateFrame(host.Frames.Single() with { Title = "新故事组名称" });
            StringAssert.Contains(((Grid)frame.Child).Children.OfType<TextBlock>().Single().Text, "新故事组名称");
            host.RestoreFrames([]);
            view.StoryGroups = StoryGroupCatalog.Derive([A, B], new(2, []), group);
            Assert.IsFalse(Descendants(view).OfType<Border>().Any(border => AutomationProperties.GetAutomationId(border).StartsWith("StoryGroupFrame_", StringComparison.Ordinal)));
            Assert.HasCount(2, host.Nodes);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void WholeContainerViewerShowsBothStoriesAndNavigatesBackFromStory()
    {
        OfflineResourceChoice Story(string uid, string type, string port)
        {
            var boundary = GraphNodeFactory.Create(GraphScope.StoryFlow, type, type);
            boundary.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement(port);
            boundary.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement(port);
            var definition = new GraphResourceEnvelope(GraphResourceKind.Story, uid, uid, new GraphDocument([boundary]));
            return new("Story", uid, uid, "group", definition.ToJson());
        }
        var choice = new OfflineResourceChoice("StoryGroup", "", "只读组合", "group.dgrs.g", "")
        {
            RelatedGraphs = [Story(A, "logic_output", "out"), Story(B, "logic_input", "in")],
            ContainerGraph = new(2, [new(A, "out", B, "in")])
        };
        using var model = new OfflineReadOnlyResourceViewModel(choice);
        Assert.HasCount(2, model.PreviewHost!.Nodes); Assert.HasCount(1, model.PreviewHost.Connections);
        Assert.IsTrue(model.TryOpenSubgraph(model.PreviewHost.Nodes.First()));
        Assert.AreEqual("Story", model.Choice.Kind);
        model.BackCommand.Execute(null);
        Assert.AreEqual("StoryGroup", model.Choice.Kind); Assert.HasCount(2, model.PreviewHost!.Nodes);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
