using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GraphEditorCommandBridgeTests
{
    [TestMethod]
    public void InputFirstAndOutputFirstGesturesNormalizeToOneConnection()
    {
        var graph = FlowGraph();
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        var output = GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow);
        var input = GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Flow);

        Assert.IsTrue(bridge.Connect(input, output));
        Assert.IsTrue(bridge.Undo());
        Assert.IsTrue(bridge.Connect(output, input));

        var edge = graph.Connections.Single();
        Assert.AreEqual(new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow), edge);
        Assert.IsFalse(bridge.LastValidationIssues.Any());
    }

    [TestMethod]
    public void ReconnectReplacesEitherEndpointAsOneAtomicEdit()
    {
        var graph = FlowGraph();
        var original = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(original);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));

        Assert.IsTrue(bridge.Reconnect(original,
            GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("c", "in", GraphInterfaceKind.Flow)));
        Assert.AreEqual(new GraphConnection("a", "out", "c", "in", GraphInterfaceKind.Flow), graph.Connections.Single());
        Assert.IsTrue(bridge.Undo());
        Assert.AreEqual(original, graph.Connections.Single());

        Assert.IsTrue(bridge.Reconnect(
            GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Output("d", "out", GraphInterfaceKind.Flow), original));
        Assert.AreEqual(new GraphConnection("d", "out", "b", "in", GraphInterfaceKind.Flow), graph.Connections.Single());
    }

    [TestMethod]
    public void BlankDropDisconnectsExistingWireAndNewWireIsNoOp()
    {
        var graph = FlowGraph();
        var original = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(original);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));

        Assert.IsTrue(bridge.CompleteConnectionDrag(
            GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), null, original));
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(bridge.CanUndo);

        Assert.IsFalse(bridge.CompleteWireDrag(
            GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), null));
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(bridge.CanUndo);
    }

    [TestMethod]
    public void InvalidEndpointPairIsStableNoOpBeforeCoreMutation()
    {
        var graph = FlowGraph();
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        var before = graph.ToJson();

        Assert.IsFalse(bridge.Connect(
            new GraphEditorEndpoint(" ", "out", GraphPortDirection.Output, GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("a", "in", GraphInterfaceKind.Flow)));
        var firstCodes = bridge.LastValidationIssues.Select(issue => issue.Code).ToArray();
        Assert.IsTrue(firstCodes.Contains("graph.editor.endpoint.from.node_id.required"));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);

        Assert.IsFalse(bridge.Connect(
            GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Output("b", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.editor.connection.direction.same");
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);
    }

    [TestMethod]
    public void OperationSnapshotClearsCoreErrorsOnBlankNoOpAndEmptyUndo()
    {
        var graph = FlowGraph();
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        var invalid = GraphEditorEndpoint.Output("a", "missing", GraphInterfaceKind.Flow);
        var input = GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Flow);

        Assert.IsFalse(bridge.Connect(invalid, input));
        Assert.IsTrue(bridge.LastValidationIssues.Any());
        Assert.IsFalse(bridge.CompleteWireDrag(invalid, null));
        Assert.IsEmpty(bridge.LastValidationIssues);

        Assert.IsFalse(bridge.Connect(invalid, input));
        Assert.IsFalse(bridge.Undo());
        Assert.IsEmpty(bridge.LastValidationIssues);
    }

    [TestMethod]
    public void CanConnectIsNonMutatingAndReportsCoreCardinalityAndCycleIssues()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in_logic", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in_logic", "In", true, GraphInterfaceKind.Logic)])],
            [new("a", "out", "b", "in", GraphInterfaceKind.Flow), new("a", "logic", "b", "in_logic", GraphInterfaceKind.Logic)]);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        var before = graph.ToJson();

        Assert.IsFalse(bridge.CanConnect(GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("c", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.flow.output.multiple_targets");
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);

        Assert.IsFalse(bridge.CanConnect(GraphEditorEndpoint.Output("b", "logic", GraphInterfaceKind.Logic), GraphEditorEndpoint.Input("a", "in", GraphInterfaceKind.Logic)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.logic.cycle");
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);

        Assert.IsTrue(bridge.CanConnect(GraphEditorEndpoint.Output("a", "logic", GraphInterfaceKind.Logic), GraphEditorEndpoint.Input("c", "in_logic", GraphInterfaceKind.Logic)));
        Assert.IsEmpty(bridge.LastValidationIssues);
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);
    }

    [TestMethod]
    public void CanReconnectExcludesOriginalAndDoesNotSubmit()
    {
        var graph = FlowGraph();
        var original = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(original);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        var before = graph.ToJson();

        Assert.IsTrue(bridge.CanReconnect(original,
            GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("c", "in", GraphInterfaceKind.Flow)));
        Assert.IsEmpty(bridge.LastValidationIssues);
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(bridge.CanUndo);
    }

    [TestMethod]
    public void UiEligibilityReportsMixedKindSameNodeAndBlankPort()
    {
        var graph = FlowGraph();
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));

        Assert.IsFalse(bridge.CanConnect(GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Logic)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.editor.connection.interface_kind.mixed");
        Assert.IsFalse(bridge.CanConnect(GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("a", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.editor.connection.nodes.same");
        Assert.IsFalse(bridge.CanConnect(GraphEditorEndpoint.Output("a", " ", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.editor.endpoint.from.port_id.required");
    }

    [TestMethod]
    public void CoreCardinalityAndLogicCycleRejectionsRemainAuthoritative()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in_logic", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Flow), new("logic", "Logic", false, GraphInterfaceKind.Logic), new("in_logic", "In", true, GraphInterfaceKind.Logic)])],
            [new("a", "out", "b", "in", GraphInterfaceKind.Flow), new("a", "logic", "b", "in_logic", GraphInterfaceKind.Logic)]);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));

        Assert.IsFalse(bridge.Connect(GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("c", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.flow.output.multiple_targets");
        Assert.IsFalse(bridge.Connect(GraphEditorEndpoint.Output("b", "logic", GraphInterfaceKind.Logic), GraphEditorEndpoint.Input("a", "in", GraphInterfaceKind.Logic)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.logic.cycle");
        Assert.HasCount(2, graph.Connections);
        Assert.IsFalse(bridge.CanUndo);
    }

    [TestMethod]
    public void UndoAndRedoExposeSessionHistoryThroughTheBridge()
    {
        var graph = FlowGraph();
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph));
        Assert.IsFalse(bridge.CanUndo);
        Assert.IsTrue(bridge.Connect(GraphEditorEndpoint.Output("a", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("b", "in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(bridge.CanUndo);
        Assert.IsTrue(bridge.Undo());
        Assert.IsTrue(bridge.CanRedo);
        Assert.IsTrue(bridge.Redo());
        Assert.IsFalse(bridge.CanRedo);
        Assert.HasCount(1, graph.Connections);
    }

    [TestMethod]
    public void SameBridgePathAcceptsStorySessionAndTaskScopes()
    {
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session, GraphScope.Task })
        {
            var graph = ScopedGraph(scope);
            var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph, scope));
            var kind = scope == GraphScope.Task ? GraphInterfaceKind.Logic : GraphInterfaceKind.Flow;
            Assert.IsTrue(bridge.Connect(GraphEditorEndpoint.Output("source", "out", kind), GraphEditorEndpoint.Input("target", "in", kind)), scope.ToString());
            Assert.AreEqual(scope, bridge.Scope);
            Assert.AreSame(graph, bridge.Graph);
        }
    }

    [TestMethod]
    public void TaskScopeFlowCompletionIsRejectedByCore()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("target", "settle", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)])]);
        var bridge = new GraphEditorCommandBridge(new GraphEditSession(graph, GraphScope.Task));

        Assert.IsFalse(bridge.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow), GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(bridge.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.scope.task.flow_connection.disallowed");
        Assert.IsEmpty(graph.Connections);
        Assert.IsFalse(bridge.CanUndo);
    }

    private static GraphDocument FlowGraph()
        => new([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow)]),
            new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Flow)]),
            new GraphNode("d", "D", [new("out", "Out", false, GraphInterfaceKind.Flow)])]);

    private static GraphDocument ScopedGraph(GraphScope scope)
    {
        var type = scope switch
        {
            GraphScope.StoryFlow => (Source: "start", Target: "terminate"),
            GraphScope.Session => (Source: "start", Target: "end"),
            _ => (Source: "objective", Target: "settle"),
        };
        var kind = scope == GraphScope.Task ? GraphInterfaceKind.Logic : GraphInterfaceKind.Flow;
        return new GraphDocument([
            new GraphNode("source", type.Source, "Source", [new("out", "Out", false, kind)]),
            new GraphNode("target", type.Target, "Target", [new("in", "In", true, kind)])]);
    }
}
