using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphScopePolicyTests
{
    [TestMethod]
    public void GraphNodePersistsTypeWhileGenericConstructorRemainsAvailable()
    {
        var typed = new GraphNode("s", "start", "Start");
        StringAssert.Contains(GraphSerializer.Serialize(new GraphDocument([typed])), "\"type\":\"start\"");
        var generic = new GraphNode("g", "Generic");
        Assert.AreEqual(string.Empty, generic.Type);
    }

    [TestMethod]
    public void RegistryContainsDocumentedTypesAndRequiredMetadata()
    {
        CollectionAssert.AreEquivalent(new[] { "start", "terminate", "session", "task", "condition", "flow_judgment", "and", "or", "not", "action", "title", "logic_input", "logic_output" },
            GraphNodeDefinitionRegistry.ForScope(GraphScope.StoryFlow).Select(item => item.Type).ToArray());
        CollectionAssert.AreEquivalent(new[] { "start", "line", "music", "screen", "choice", "condition", "flow_judgment", "and", "or", "not", "logic_input", "logic_output", "end", "legacy_jump" },
            GraphNodeDefinitionRegistry.ForScope(GraphScope.Session).Select(item => item.Type).ToArray());
        var task = GraphNodeDefinitionRegistry.ForScope(GraphScope.Task);
        CollectionAssert.AreEquivalent(new[] { "activate", "objective", "and", "or", "not", "logic_input", "logic_output", "reward", "settle" }, task.Select(item => item.Type).ToArray());
        Assert.IsTrue(task.Single(item => item.Type == "activate").CompatibilityOnly);
        Assert.IsTrue(task.Single(item => item.Type == "settle").Unique);
    }

    [TestMethod]
    public void ScopePolicyRequiresUniqueEntryNodesAndRejectsWrongTypes()
    {
        var missing = GraphScopePolicy.Validate(new GraphDocument(), GraphScope.Session);
        CollectionAssert.Contains(missing.Select(issue => issue.Code).ToArray(), "graph.scope.required_node.missing");

        var wrong = new GraphDocument([new GraphNode("a", "objective", "Objective")]);
        var codes = GraphScopePolicy.Validate(wrong, GraphScope.Session).Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "graph.scope.node_type.wrong_scope");
    }

    [TestMethod]
    public void ScopePolicyRejectsDuplicateRequiredUniqueNodes()
    {
        var graph = new GraphDocument([
            new GraphNode("s1", "start", "Start 1"),
            new GraphNode("s2", "start", "Start 2")]);
        CollectionAssert.Contains(
            GraphScopePolicy.Validate(graph, GraphScope.Session).Select(issue => issue.Code).ToArray(),
            "graph.scope.required_node.duplicate");
    }

    [TestMethod]
    public void ScopePolicyRejectsUnknownNodeType()
    {
        var graph = new GraphDocument([new GraphNode("unknown", "not_registered", "Unknown")]);
        CollectionAssert.Contains(
            GraphScopePolicy.Validate(graph, GraphScope.StoryFlow).Select(issue => issue.Code).ToArray(),
            "graph.scope.node_type.unknown");
    }

    [TestMethod]
    public void CompatibilityJumpIsLoadableOnlyInCompatibilityMode()
    {
        var graph = new GraphDocument([new GraphNode("s", "start", "Start"), new GraphNode("j", "legacy_jump", "Jump")]);
        CollectionAssert.Contains(GraphScopePolicy.Validate(graph, GraphScope.Session).Select(issue => issue.Code).ToArray(), "graph.scope.node_type.compatibility_only");
        Assert.IsEmpty(GraphScopePolicy.Validate(graph, GraphScope.Session, compatibilityMode: true));
        Assert.IsFalse(GraphScopePolicy.CanCreateNode(GraphScope.Session, "legacy_jump"));
        Assert.IsTrue(GraphScopePolicy.CanCreateNode(GraphScope.Session, "legacy_jump", compatibilityMode: true));
    }

    [TestMethod]
    public void TaskRejectsFlowPortsAndConnections()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "activate", "Activate", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("s", "settle", "Settle", [new("in", "In", true, GraphInterfaceKind.Logic)])],
            [new("a", "out", "s", "in", GraphInterfaceKind.Flow)]);
        var codes = GraphScopePolicy.Validate(graph, GraphScope.Task).Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "graph.scope.task.flow_port.disallowed");
        CollectionAssert.Contains(codes, "graph.scope.task.flow_connection.disallowed");
    }

    [TestMethod]
    public void SessionProjectionKeepsStableDynamicOutputsAndFixedInputs()
    {
        var ports = GraphAggregatePortProjection.ProjectSession(
            [new GraphBoundary("accepted", "Accepted", 2, GraphInterfaceKind.Flow)],
            [new GraphBoundary("flag", "Flag", 1, GraphInterfaceKind.Logic)],
            includeLogicInput: true);
        Assert.HasCount(4, ports);
        Assert.AreEqual("flow_in", ports[0].Id);
        Assert.AreEqual("logic_in", ports[1].Id);
        Assert.AreEqual("accepted", ports[2].Id);
        Assert.AreEqual("flag", ports[3].Id);
        Assert.IsTrue(ports.All(port => port.Id is "flow_in" or "logic_in" || port.IsOutput));
    }

    [TestMethod]
    public void TaskProjectionUsesSettlementAndLogicOutputBoundaries()
    {
        var ports = GraphAggregatePortProjection.ProjectTask(
            [new GraphBoundary("done", "Done", 4, GraphInterfaceKind.Flow)],
            [new GraphBoundary("ready", "Ready", 1, GraphInterfaceKind.Logic)]);
        CollectionAssert.AreEqual(new[] { "flow_in", "done", "ready" }, ports.Select(port => port.Id).ToArray());
        Assert.IsTrue(ports.Skip(1).All(port => port.IsOutput));
    }

    [TestMethod]
    public void ProjectionPreservesBoundaryIdsAcrossRenameAndReorder()
    {
        var flow = new List<GraphBoundary> {
            new("first", "First", 0, GraphInterfaceKind.Flow),
            new("second", "Second", 1, GraphInterfaceKind.Flow),
        };
        var before = GraphAggregatePortProjection.ProjectTask(flow, []);
        flow[0] = flow[0] with { DisplayName = "Renamed", Order = 20 };
        flow.Reverse();
        var after = GraphAggregatePortProjection.ProjectTask(flow, []);
        var renamed = after.Single(port => port.Id == "first");
        Assert.AreEqual("Renamed", renamed.DisplayName);
        Assert.AreEqual(20, renamed.Order);
        CollectionAssert.AreEquivalent(before.Select(port => port.Id).ToArray(), after.Select(port => port.Id).ToArray());
    }

    [TestMethod]
    public void ProjectionKeepsFlowAndLogicOutputsOrderedWithinTheirGroups()
    {
        var ports = GraphAggregatePortProjection.ProjectTask(
            [new("flow_late", "Flow late", 9, GraphInterfaceKind.Flow), new("flow_early", "Flow early", 1, GraphInterfaceKind.Flow)],
            [new("logic_late", "Logic late", 8, GraphInterfaceKind.Logic), new("logic_early", "Logic early", 2, GraphInterfaceKind.Logic)]);
        CollectionAssert.AreEqual(new[] { "flow_early", "flow_late", "logic_early", "logic_late" }, ports.Skip(1).Select(port => port.Id).ToArray());
    }

    [TestMethod]
    public void ProjectionRejectsBlankBoundaryId()
    {
        var issues = GraphAggregatePortProjection.ValidateTask([new(" ", "Done", 0, GraphInterfaceKind.Flow)], []);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.id.required");
    }

    [TestMethod]
    public void ProjectionRejectsBlankBoundaryDisplayName()
    {
        var issues = GraphAggregatePortProjection.ValidateTask([new("done", " ", 0, GraphInterfaceKind.Flow)], []);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.display_name.required");
    }

    [TestMethod]
    public void ProjectionRejectsDuplicateBoundaryIds()
    {
        var issues = GraphAggregatePortProjection.ValidateTask(
            [new("same", "Done", 0, GraphInterfaceKind.Flow)],
            [new("same", "Ready", 0, GraphInterfaceKind.Logic)]);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.id.duplicate");
    }

    [TestMethod]
    public void ProjectionRejectsDuplicateDisplayNamesAcrossKinds()
    {
        var issues = GraphAggregatePortProjection.ValidateTask(
            [new("done", "Same", 0, GraphInterfaceKind.Flow)],
            [new("ready", "Same", 0, GraphInterfaceKind.Logic)]);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.display_name.duplicate");
    }

    [TestMethod]
    public void ProjectionRejectsReservedBoundaryIds()
    {
        var issues = GraphAggregatePortProjection.ValidateTask([new("flow_in", "Done", 0, GraphInterfaceKind.Flow)], []);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.reserved_id");
    }

    [TestMethod]
    public void ProjectionRejectsWrongBoundaryKind()
    {
        var issues = GraphAggregatePortProjection.ValidateTask([new("done", "Done", 0, GraphInterfaceKind.Logic)], []);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.boundary.kind.invalid");
    }

    [TestMethod]
    public void ProjectionRejectsInputDirectionBoundary()
    {
        var issues = GraphAggregatePortProjection.ValidateTask([new("done", "Done", 0, GraphInterfaceKind.Flow, IsInput: true)], []);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.boundary.direction.invalid");
    }

    [TestMethod]
    public void ProjectionRejectsInvalidBoundaryIdentityAndKind()
    {
        var exception = Assert.Throws<AggregatePortProjectionException>(() => GraphAggregatePortProjection.ProjectTask(
            [new GraphBoundary("flow_in", "Done", 0, GraphInterfaceKind.Flow), new GraphBoundary("x", "Same", 1, GraphInterfaceKind.Flow)],
            [new GraphBoundary("y", "Same", 2, GraphInterfaceKind.Flow)]));
        Assert.IsGreaterThan(1, exception.Issues.Count);
        CollectionAssert.Contains(exception.Issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.boundary.kind.invalid");
        CollectionAssert.Contains(exception.Issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.reserved_id");
        CollectionAssert.Contains(exception.Issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.display_name.duplicate");
    }
}
