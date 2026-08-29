using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalAggregateNodeFactoryTests
{
    [TestMethod]
    public void SessionAggregateBindsResourceAndProjectsBoundariesInChildOrder()
    {
        var end = BoundaryNode(GraphScope.Session, "end", "end", "accepted", "Accepted");
        var input = BoundaryNode(GraphScope.Session, "logic_input", "input", "available", "Available");
        var logic = BoundaryNode(GraphScope.Session, "logic_output", "logic", "known", "Known");
        var source = new GraphResourceEnvelope(GraphResourceKind.Session, "session-1", "会话", new GraphDocument([end, input, logic]));
        var target = new GraphDocument();
        var before = source.ToJson();

        var result = CanonicalAggregateNodeFactory.Create(target, source, "placement-1");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        var candidate = result.Candidate!;
        Assert.AreEqual("session", candidate.Type);
        Assert.AreEqual("session-1", candidate.Properties["resource_id"].GetString());
        CollectionAssert.AreEqual(new[] { "flow_in", "available", "accepted", "known" }, candidate.Ports.Select(port => port.Id).ToArray());
        Assert.IsTrue(candidate.Ports[1].IsInput);
        Assert.AreEqual(GraphInterfaceKind.Logic, candidate.Ports[1].InterfaceKind);
        Assert.IsTrue(candidate.Ports.Skip(2).All(port => port.IsOutput));
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(candidate, GraphScope.StoryFlow));
        Assert.AreEqual(before, source.ToJson());
        Assert.IsEmpty(target.Nodes);
    }

    [TestMethod]
    public void TaskAggregateProjectsSettleLogicInputsAsFlowAndLogicOutputsSeparately()
    {
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new("done", "Done", true, GraphInterfaceKind.Logic, 3));
        var logic = BoundaryNode(GraphScope.Task, "logic_output", "logic", "ready", "Ready");
        var source = new GraphResourceEnvelope(GraphResourceKind.Task, "task-1", "任务", new GraphDocument([settle, logic]));

        var result = CanonicalAggregateNodeFactory.Create(source, "placement-1");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        CollectionAssert.AreEqual(new[] { "flow_in", "done", "ready" }, result.Candidate!.Ports.Select(port => port.Id).ToArray());
        Assert.AreEqual(GraphInterfaceKind.Flow, result.Candidate.Ports[1].InterfaceKind);
        Assert.AreEqual(GraphInterfaceKind.Logic, result.Candidate.Ports[2].InterfaceKind);
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, GraphScope.StoryFlow));
    }

    [TestMethod]
    public void InvalidKindAndSettleBoundariesFailWithoutCandidates()
    {
        var story = new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument());
        AssertFailure(CanonicalAggregateNodeFactory.Create(story, "placement"), "graph.aggregate.resource.kind.unsupported");

        var task = new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument());
        AssertFailure(CanonicalAggregateNodeFactory.Create(task, "placement"), "graph.aggregate.task.settle.required");

        var first = GraphNodeFactory.Create(GraphScope.Task, "settle", "one");
        first.Ports.Add(new("one", "One", true, GraphInterfaceKind.Logic));
        var second = GraphNodeFactory.Create(GraphScope.Task, "settle", "two");
        second.Ports.Add(new("two", "Two", true, GraphInterfaceKind.Logic));
        var duplicate = new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument([first, second]));
        AssertFailure(CanonicalAggregateNodeFactory.Create(duplicate, "placement"), "graph.aggregate.task.settle.duplicate");
    }

    [TestMethod]
    public void InvalidPublicBoundaryContractsFailClosed()
    {
        var first = BoundaryNode(GraphScope.Session, "end", "first", "same", "Same");
        var second = BoundaryNode(GraphScope.Session, "end", "second", "same", "Same");
        var source = new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([first, second]));

        var result = CanonicalAggregateNodeFactory.Create(source, "placement");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate);
        CollectionAssert.Contains(result.Issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.id.duplicate");
        CollectionAssert.Contains(result.Issues.Select(issue => issue.Code).ToArray(), "graph.aggregate.port.display_name.duplicate");
    }

    private static GraphNode BoundaryNode(GraphScope scope, string type, string id, string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(scope, type, id);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }

    private static void AssertFailure(GraphNodeAuthoringResult result, string code)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate);
        CollectionAssert.Contains(result.Issues.Select(issue => issue.Code).ToArray(), code);
    }
}
