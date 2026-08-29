using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalAggregateSynchronizationTests
{
    [TestMethod]
    public void AnalysisSynchronizesEveryRepeatedPlacementAndPreservesCompatibleEdges()
    {
        var first = Aggregate("one", "Old");
        var second = Aggregate("two", "Old");
        var target = new GraphNode("target", "choice", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([first, second, target], [
            new("one", "accepted", "target", "in", GraphInterfaceKind.Flow),
            new("two", "accepted", "target", "in", GraphInterfaceKind.Flow)]);
        var resource = Session("session-1", "New", "accepted", "Accepted");
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);

        var plan = session.AnalyzeAggregateSynchronization(resource);
        Assert.IsTrue(plan.IsSuccess, string.Join(";", plan.Issues.Select(x => x.Code)));
        Assert.HasCount(2, plan.Placements);
        Assert.IsTrue(session.ApplyAggregateSynchronization(plan));
        Assert.IsEmpty(session.LastValidationIssues);
        Assert.IsTrue(graph.Nodes.Where(n => n?.Type == "session").All(n => n!.Ports.Any(p => p.Id == "accepted" && p.DisplayName == "Accepted")));
        Assert.HasCount(2, graph.Connections);
        Assert.IsTrue(graph.Connections.All(e => e.FromPortId == "accepted"));
        Assert.AreEqual(1, session.UndoCount);
    }

    [TestMethod]
    public void ReferencedRemovalIsDetachedAndRequiresConfirmationWithoutMutation()
    {
        var aggregate = Aggregate("one", "Old");
        var target = new GraphNode("target", "choice", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([aggregate, target], [new("one", "accepted", "target", "in", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var resource = Session("session-1", "New", "different", "Different");
        var before = graph.ToJson();

        var plan = session.AnalyzeAggregateSynchronization(resource);
        Assert.IsTrue(plan.RequiresConfirmation);
        Assert.HasCount(1, plan.ObsoleteReferences);
        plan.ObsoleteReferences[0].FromNodeId = "detached";
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.ApplyAggregateSynchronization(plan));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);
        CollectionAssert.Contains(session.LastValidationIssues.Select(x => x.Code).ToArray(), "graph.aggregate.sync.references.confirmation_required");
    }

    [TestMethod]
    public void ConfirmedRemovalIsOneUndoRedoUnit()
    {
        var aggregate = Aggregate("one", "Old");
        var target = new GraphNode("target", "choice", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([aggregate, target], [new("one", "accepted", "target", "in", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var plan = session.AnalyzeAggregateSynchronization(Session("session-1", "New", "different", "Different"));

        Assert.IsTrue(session.ApplyAggregateSynchronization(plan, true));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("Old", graph.Nodes.Single(n => n!.Id == "one")!.Ports.Single(p => p.Id == "accepted").DisplayName);
        Assert.HasCount(1, graph.Connections);
        Assert.IsTrue(session.Redo());
        Assert.IsEmpty(graph.Connections);
        Assert.AreEqual("Different", graph.Nodes.Single(n => n!.Id == "one")!.Ports.Single(p => p.Id == "different").DisplayName);
    }

    [TestMethod]
    public void InvalidBindingAndUnchangedProjectionFailOrNoOpWithoutHistory()
    {
        var invalid = new GraphNode("bad", "session", "Bad");
        invalid.Properties["resource_id"] = JsonSerializer.SerializeToElement(42);
        var graph = new GraphDocument([invalid]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var resource = Session("session-1", "Accepted", "accepted", "Accepted");
        var before = graph.ToJson();

        var invalidPlan = session.AnalyzeAggregateSynchronization(resource);
        Assert.IsFalse(invalidPlan.IsSuccess);
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.ApplyAggregateSynchronization(invalidPlan));
        Assert.AreEqual(0, session.UndoCount);

        var unchanged = Aggregate("ok", "Accepted");
        unchanged.Properties["resource_id"] = JsonSerializer.SerializeToElement("session-1");
        var noOpSession = new GraphEditSession(new GraphDocument([unchanged]), GraphScope.StoryFlow);
        var noOp = noOpSession.AnalyzeAggregateSynchronization(resource);
        Assert.IsTrue(noOp.IsNoOp);
        Assert.IsTrue(noOpSession.ApplyAggregateSynchronization(noOp));
        Assert.AreEqual(0, noOpSession.UndoCount);
    }

    [TestMethod]
    public void NewConnectionToObsoletePortAfterAnalysisFailsClosedEvenWhenConfirmed()
    {
        var aggregate = Aggregate("one", "Old");
        var target = new GraphNode("target", "choice", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([aggregate, target]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var plan = session.AnalyzeAggregateSynchronization(Session("session-1", "New", "different", "Different"));
        graph.Connections.Add(new("one", "accepted", "target", "in", GraphInterfaceKind.Flow));
        var before = graph.ToJson();

        Assert.IsFalse(session.ApplyAggregateSynchronization(plan, true));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);
        CollectionAssert.Contains(session.LastValidationIssues.Select(x => x.Code).ToArray(), "graph.aggregate.sync.references.stale");
    }

    [TestMethod]
    public void NullScopeFailsAnalysisAndApplyWithoutMutation()
    {
        var graph = new GraphDocument([Aggregate("one", "Old")]);
        var session = new GraphEditSession(graph);
        var before = graph.ToJson();
        var plan = session.AnalyzeAggregateSynchronization(Session("session-1", "New", "accepted", "Accepted"));

        Assert.IsFalse(plan.IsSuccess);
        CollectionAssert.Contains(plan.Issues.Select(x => x.Code).ToArray(), "graph.aggregate.sync.scope.invalid");
        Assert.IsFalse(session.ApplyAggregateSynchronization(plan, true));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);
    }

    private static GraphResourceEnvelope Session(string id, string name, string portId, string displayName)
    {
        var end = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        end.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        end.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return new(GraphResourceKind.Session, id, name, new GraphDocument([end]));
    }

    private static GraphNode Aggregate(string id, string displayName)
    {
        var node = new GraphNode(id, "session", displayName, [
            new("flow_in", "Flow In", true, GraphInterfaceKind.Flow, 0),
            new("logic_in", "Logic In", true, GraphInterfaceKind.Logic, 1),
            new("accepted", displayName, false, GraphInterfaceKind.Flow, 0)]);
        node.Properties["resource_id"] = JsonSerializer.SerializeToElement("session-1");
        return node;
    }
}
