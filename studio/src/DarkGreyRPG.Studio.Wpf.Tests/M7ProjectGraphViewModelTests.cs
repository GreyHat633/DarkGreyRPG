using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M7ProjectGraphViewModelTests
{
    private const string A = "ST-2345-6789-ABCD-EFGH", B = "ST-JKLM-NPQR-STUV-WXYZ", C = "ST-AAAA-BBBB-CCCC-DDDD";
    private static CanonicalProjectStoryGraphSnapshot Snapshot((string Id, string Name)[] nodes, params CanonicalProjectStoryGraphTransition[] transitions)
        => new(nodes.Select(node => new CanonicalProjectStoryGraphNode(node.Id, node.Name, true, true, true, true, false, [])),
            transitions.GroupBy(edge => (edge.SourceStoryId, edge.TargetStoryId)).Select(group => new CanonicalProjectStoryGraphEdge(group.Key.SourceStoryId, group.Key.TargetStoryId, group)), []);

    [TestMethod]
    public void CurrentSnapshotFiltersIsolatedStoriesWithoutAddingDiagnostics()
    {
        var graph = new ProjectGraphViewModel(Snapshot([(A,"王城迷案"),(B,"王国线"),(C,"孤立支线")], new CanonicalProjectStoryGraphTransition(A,B,"external_session")));
        Assert.HasCount(1, graph.Edges);
        Assert.AreEqual(A, graph.Edges[0].SourceStoryId); Assert.AreEqual(B, graph.Edges[0].TargetStoryId);
        Assert.IsFalse(graph.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated"));
        graph.SearchText = "王国";
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == B).IsVisible);
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == A).IsVisible);
        graph.SearchText = string.Empty; graph.SelectedFilter = "孤立";
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == C).IsVisible);
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == B).IsVisible);
    }

    [TestMethod]
    public void LayoutPersistsSeparatelyAndOpenRequestTargetsCurrentStory()
    {
        using var directory = new GraphDirectory();
        var snapshot = Snapshot([(A,"王城迷案"),(B,"王国线")], new CanonicalProjectStoryGraphTransition(A,B,"external_session"));
        var graph = new ProjectGraphViewModel(snapshot, A, directory.Root);
        string? opened = null; graph.OpenStoryRequested += (_, id) => opened = id;
        graph.MoveNode(B,777,333); graph.OpenStoryFlow(B);
        Assert.AreEqual(B,opened);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root,"resources","editor","story-graph-layout.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root,"stories")));
        var restored = new ProjectGraphViewModel(snapshot,A,directory.Root);
        Assert.AreEqual(777,restored.Nodes.Single(node=>node.Id==B).X);
        Assert.AreEqual(333,restored.Nodes.Single(node=>node.Id==B).Y);
    }

    [TestMethod]
    public void CurrentCycleLayoutTerminatesWithFinitePositions()
    {
        var graph = new ProjectGraphViewModel(Snapshot([(A,"Alpha"),(B,"Beta")],new CanonicalProjectStoryGraphTransition(A,B,"forward"),new CanonicalProjectStoryGraphTransition(B,A,"backward")));
        Assert.HasCount(2,graph.Edges);
        Assert.HasCount(2,graph.Diagnostics.Where(issue=>issue.Code=="project_graph.story.cycle"));
        graph.AutoLayout();
        Assert.IsTrue(graph.Nodes.All(node=>double.IsFinite(node.X)&&double.IsFinite(node.Y)));
        Assert.AreEqual(graph.Nodes[0].X,graph.Nodes[1].X); Assert.AreNotEqual(graph.Nodes[0].Y,graph.Nodes[1].Y);
    }

    [TestMethod]
    public void CurrentSelfLoopPreservesWarningAndFiniteLayout()
    {
        var graph=new ProjectGraphViewModel(Snapshot([(A,"Loop")],new CanonicalProjectStoryGraphTransition(A,A,"external_session")));
        Assert.IsTrue(graph.Edges.Single().IsSelfLoop);
        Assert.IsTrue(graph.Diagnostics.Any(issue=>issue.Code=="project_graph.story.cycle"&&issue.StoryId==A));
        graph.AutoLayout();Assert.IsTrue(double.IsFinite(graph.Nodes.Single().X));
    }

    [TestMethod]
    public void CurrentParallelRelationsKeepStableBranchDetails()
    {
        var graph=new ProjectGraphViewModel(Snapshot([(A,"Source"),(B,"Target")],
            new CanonicalProjectStoryGraphTransition(A,B,"z_external",incomingBranchOutputs:["conceal","accept","accept"]),new CanonicalProjectStoryGraphTransition(A,B,"a_external"),new CanonicalProjectStoryGraphTransition(A,B,"m_external")));
        var edge=graph.Edges.Single();Assert.AreEqual(3,edge.Count);
        CollectionAssert.AreEqual(new[]{"a_external","m_external","z_external"},edge.Transitions.Select(item=>item.NodeId).ToArray());
        CollectionAssert.AreEqual(new[]{"accept","conceal"},edge.Transitions.Single(item=>item.NodeId=="z_external").IncomingBranchOutputs.ToArray());
        Assert.IsFalse(edge.IsSelfLoop);StringAssert.Contains(edge.Tooltip,"z_external");
    }

    [TestMethod]
    public void ProblemFocusMakesCurrentStoryVisible()
    {
        var graph=new ProjectGraphViewModel(Snapshot([(A,"Source"),(B,"Other")]));
        graph.SearchText="Other";Assert.IsFalse(graph.Nodes.Single(node=>node.Id==A).IsVisible);
        Assert.IsTrue(graph.RequestProblemFocus(A));Assert.AreEqual(A,graph.ProblemFocusRequest?.StoryId);
        Assert.IsTrue(graph.Nodes.Single(node=>node.Id==A).IsVisible);Assert.IsFalse(graph.RequestProblemFocus("missing"));
    }

    [TestMethod]
    public void CanonicalStructuralDiagnosticsAreErrorsAndRemainPreciselyAddressable()
    {
        var issue = new CanonicalProjectStoryGraphDiagnostic(
            "story.discovery.resource.invalid",
            "ambiguous external_session node",
            "ST-2345-6789-ABCD-EFGH",
            "shared");
        var canonical = new CanonicalProjectStoryGraphSnapshot(
            [new("ST-2345-6789-ABCD-EFGH", "Source", true, true, true, false, true, [issue])],
            [],
            [issue]);

        var graph = new ProjectGraphViewModel(canonical);

        Assert.AreEqual(1, graph.ErrorCount);
        Assert.AreEqual("shared", graph.Diagnostics.Single(item => item.Code == issue.Code).NodeId);
        Assert.IsTrue(graph.Nodes.Single().HasWarning);
    }

    [TestMethod]
    public void PublicLogicEditorAddsReloadsAndRemovesStableCrossStoryConnection()
    {
        using var directory = new GraphDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(CanonicalStory("ST-2345-6789-ABCD-EFGH", "logic_output", "out", "rescued"));
        store.Stories.Create(CanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "logic_input", "in", "kingdom_gate"));
        var graph = new ProjectGraphViewModel(new CanonicalProjectStoryGraphSnapshot([],[],[]), projectDirectory: directory.Root);

        graph.SelectedLogicSource = graph.LogicSources.Single();
        graph.SelectedLogicTarget = graph.LogicTargets.Single();
        graph.AddLogicConnectionCommand.Execute(null);

        Assert.HasCount(1, graph.LogicConnections);
        Assert.AreEqual("rescued", store.StoryLogicGraph.Load().Connections.Single().SourcePortId);
        var restored = new ProjectGraphViewModel(new CanonicalProjectStoryGraphSnapshot([],[],[]), projectDirectory: directory.Root);
        Assert.HasCount(1, restored.LogicConnections);

        restored.LogicConnections.Single().RemoveCommand.Execute(null);
        Assert.IsEmpty(restored.LogicConnections);
        Assert.IsEmpty(store.StoryLogicGraph.Load().Connections);
    }

    [TestMethod]
    public void DerivedGroupNamesSurviveGraphUndoRedoAndProjectReopen()
    {
        using var directory = new GraphDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(CanonicalStory("ST-2345-6789-ABCD-EFGH", "logic_output", "out", "rescued"));
        store.Stories.Create(CanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "logic_input", "in", "gate"));
        store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "rescued", "ST-JKLM-NPQR-STUV-WXYZ", "gate", "Logic")]);
        var graph = new ProjectGraphViewModel(new CanonicalProjectStoryGraphSnapshot([],[],[]), projectDirectory: directory.Root);
        var host = graph.CanonicalHost!;
        Assert.HasCount(1, graph.StoryGroups.Groups);
        var key = graph.StoryGroups.Groups.Single().Key;
        var defaultName = graph.StoryGroups.Groups.Single().DisplayName;
        graph.RenameStoryGroup(key, "王城主线");
        Assert.AreEqual("王城主线", host.Frames.Single().Title);
        var edge = host.Graph.Connections.Single();
        Assert.IsTrue(host.Disconnect(edge));
        Assert.IsEmpty(graph.StoryGroups.Groups);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual("王城主线", graph.StoryGroups.Groups.Single().DisplayName);
        Assert.IsFalse(host.Connect(new(edge.FromNodeId, edge.FromPortId, false, GraphInterfaceKind.Logic),
            new(edge.ToNodeId, edge.ToPortId, true, GraphInterfaceKind.Logic)));
        Assert.AreEqual("王城主线", new ProjectGraphViewModel(new CanonicalProjectStoryGraphSnapshot([],[],[]), projectDirectory: directory.Root).StoryGroups.Groups.Single().DisplayName);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(defaultName, graph.StoryGroups.Groups.Single().DisplayName);
        Assert.IsTrue(host.Redo());
        Assert.AreEqual("王城主线", graph.StoryGroups.Groups.Single().DisplayName);
        Assert.AreEqual(edge, host.Graph.Connections.Single());

    }

    private static GraphResourceEnvelope CanonicalStory(string storyId, string type, string nodeId, string portId)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, nodeId);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(portId);
        return new(GraphResourceKind.Story, storyId, storyId, new GraphDocument([node]));
    }

    private sealed class GraphDirectory : IDisposable
    {
        public GraphDirectory()
        {
            Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m7-test-data", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Root);
        }
        public string Root { get; }
        public void Dispose()
        {
            var safeRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m7-test-data"));
            if (!Root.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe M7 cleanup path.");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
