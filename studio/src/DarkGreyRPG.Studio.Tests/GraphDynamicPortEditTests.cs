using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphDynamicPortEditTests
{
    [TestMethod]
    public void CanonicalRolesUseExactScopeTypeDirectionAndKind()
    {
        var expected = new[]
        {
            (GraphScope.StoryFlow, "start", GraphPortDirection.Output, GraphInterfaceKind.Flow, 1),
            (GraphScope.StoryFlow, "session", GraphPortDirection.Output, GraphInterfaceKind.Flow, 0),
            (GraphScope.StoryFlow, "session", GraphPortDirection.Output, GraphInterfaceKind.Logic, 0),
            (GraphScope.StoryFlow, "task", GraphPortDirection.Output, GraphInterfaceKind.Flow, 0),
            (GraphScope.StoryFlow, "task", GraphPortDirection.Output, GraphInterfaceKind.Logic, 0),
            (GraphScope.StoryFlow, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.StoryFlow, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.Session, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.Session, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.Session, "choice", GraphPortDirection.Output, GraphInterfaceKind.Flow, 1),
            (GraphScope.Session, "choice", GraphPortDirection.Output, GraphInterfaceKind.Logic, 1),
            (GraphScope.Task, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.Task, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
            (GraphScope.Task, "settle", GraphPortDirection.Input, GraphInterfaceKind.Logic, 1),
        };
        Assert.HasCount(expected.Length, GraphDynamicPortPolicy.Roles);
        foreach (var item in expected)
        {
            Assert.IsTrue(GraphDynamicPortPolicy.TryGetRole(item.Item1, item.Item2, item.Item3, item.Item4, out var role));
            Assert.AreEqual(item.Item5, role.MinimumCount);
        }
        Assert.IsFalse(GraphDynamicPortPolicy.TryGetRole(GraphScope.Task, "AND", GraphPortDirection.Input, GraphInterfaceKind.Logic, out _));
        Assert.IsFalse(GraphDynamicPortPolicy.TryGetRole(GraphScope.Task, "settle", GraphPortDirection.Input, GraphInterfaceKind.Flow, out _));
        Assert.IsTrue(GraphDynamicPortPolicy.Roles
            .Where(role => role.Scope == GraphScope.StoryFlow && role.NodeType is "session" or "task")
            .All(role => !role.UserEditable));
        Assert.IsTrue(GraphDynamicPortPolicy.Roles
            .Where(role => role.Scope == GraphScope.Session && role.NodeType == "choice")
            .All(role => !role.UserEditable));
    }

    [TestMethod]
    public void AggregateProjectionPortsRejectEveryDirectDynamicEditWithoutMutation()
    {
        var graph = new GraphDocument([new GraphNode("aggregate", "session", "Session", [
            new("flow_in", "Flow In", true, GraphInterfaceKind.Flow, 0),
            new("finished", "Finished", false, GraphInterfaceKind.Flow, 0),
            new("known", "Known", false, GraphInterfaceKind.Logic, 0),
        ])]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow, dynamicPortIdSource: () => "manual");
        var before = graph.ToJson();

        Assert.IsFalse(session.AddDynamicPort("aggregate", "Manual", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        AssertReadOnly(session, graph, before);
        Assert.IsFalse(session.AddDynamicPort("aggregate", "Manual"));
        AssertReadOnly(session, graph, before);
        Assert.IsFalse(session.RemoveDynamicPort("aggregate", "finished", true));
        AssertReadOnly(session, graph, before);
        Assert.IsFalse(session.RenamePortDisplayName("aggregate", "finished", "Changed"));
        AssertReadOnly(session, graph, before);
        Assert.IsFalse(session.ReorderPort("aggregate", "known", 4));
        AssertReadOnly(session, graph, before);
        Assert.AreEqual(0, session.UndoCount);
    }

    [TestMethod]
    public void AddUsesOpaqueUniqueOrderAndOneUndoUnit()
    {
        var graph = new GraphDocument([new GraphNode("start-1", "start", "Start", [
            new("fixed", "Existing", false, GraphInterfaceKind.Flow, 0)])]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow, dynamicPortIdSource: () => "opaque-id");
        Assert.IsTrue(session.AddDynamicPort("start-1", "Branch", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        var added = graph.Nodes.Single().Ports.Single(port => port.DisplayName == "Branch");
        Assert.AreEqual("opaque-id", added.Id);
        Assert.AreEqual(1, added.Order);
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsEmpty(graph.Nodes.Single().Ports.Where(port => port.DisplayName == "Branch"));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("opaque-id", graph.Nodes.Single().Ports.Single(port => port.DisplayName == "Branch").Id);
    }

    [TestMethod]
    public void MoveDynamicPortShiftsContiguousRoleAsOneUndoUnit()
    {
        var graph = new GraphDocument([new GraphNode("settle", "settle", "Settle", [
            new("first", "First", true, GraphInterfaceKind.Logic, 0),
            new("second", "Second", true, GraphInterfaceKind.Logic, 1),
            new("third", "Third", true, GraphInterfaceKind.Logic, 2)])]);
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsTrue(session.MoveDynamicPort("settle", "third", 0));
        CollectionAssert.AreEqual(new[] { "third", "first", "second" },
            graph.Nodes.Single().Ports.OrderBy(port => port.Order).Select(port => port.Id).ToArray());
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.Undo());
        CollectionAssert.AreEqual(new[] { "first", "second", "third" },
            graph.Nodes.Single().Ports.OrderBy(port => port.Order).Select(port => port.Id).ToArray());
    }

    [TestMethod]
    public void TaskSettleDynamicPortRejectsPublicIdsAndDisplayNameCollisionsWithoutUndo()
    {
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("result", "Result", true, GraphInterfaceKind.Logic, 0));
        var output = GraphNodeFactory.Create(GraphScope.Task, "logic_output", "output");
        output.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("public_id");
        output.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Public");
        var graph = new GraphDocument([settle, output]);
        var session = new GraphEditSession(graph, GraphScope.Task, dynamicPortIdSource: () => "public_id");
        var before = graph.ToJson();

        Assert.IsFalse(session.AddDynamicPort("settle", "Another", GraphPortDirection.Input, GraphInterfaceKind.Logic));
        Assert.AreEqual("graph.dynamic_port.port_id.unavailable", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);

        Assert.IsFalse(session.AddDynamicPort("settle", "Public", GraphPortDirection.Input, GraphInterfaceKind.Logic));
        Assert.AreEqual("graph.dynamic_port.label.duplicate", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);

        Assert.IsFalse(session.RenamePortDisplayName("settle", "result", "Public"));
        Assert.AreEqual("graph.dynamic_port.label.duplicate", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);

        Assert.IsFalse(session.SetNodeProperty("output", "display_name",
            System.Text.Json.JsonSerializer.SerializeToElement("Result")));
        Assert.AreEqual("graph.dynamic_port.label.duplicate", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);
    }

    [TestMethod]
    public void RemovingMiddleTaskSettleSlotReindexesAndUndoRestoresEdgesAndOrder()
    {
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("first", "First", true, GraphInterfaceKind.Logic, 0));
        settle.Ports.Add(new GraphPort("middle", "Middle", true, GraphInterfaceKind.Logic, 1));
        settle.Ports.Add(new GraphPort("last", "Last", true, GraphInterfaceKind.Logic, 2));
        var graph = new GraphDocument([settle, GraphNodeFactory.Create(GraphScope.Task, "activate", "activate")], [
            new GraphConnection("activate", "logic_out", "settle", "middle", GraphInterfaceKind.Logic)]);
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsFalse(session.RemoveDynamicPort("settle", "middle"));
        Assert.AreEqual("graph.dynamic_port.references.confirmation_required", session.LastValidationIssues.Single().Code);
        Assert.IsTrue(session.RemoveDynamicPort("settle", "middle", confirmReferencedRemoval: true));
        CollectionAssert.AreEqual(new[] { "first", "last" },
            graph.Nodes.Single(node => node.Id == "settle").Ports.OrderBy(port => port.Order).Select(port => port.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 },
            graph.Nodes.Single(node => node.Id == "settle").Ports.OrderBy(port => port.Order).Select(port => port.Order).ToArray());
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(session.MoveDynamicPort("settle", "last", 0));
        CollectionAssert.AreEqual(new[] { "last", "first" },
            graph.Nodes.Single(node => node.Id == "settle").Ports.OrderBy(port => port.Order).Select(port => port.Id).ToArray());

        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Undo());
        CollectionAssert.AreEqual(new[] { "first", "middle", "last" },
            graph.Nodes.Single(node => node.Id == "settle").Ports.OrderBy(port => port.Order).Select(port => port.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 },
            graph.Nodes.Single(node => node.Id == "settle").Ports.OrderBy(port => port.Order).Select(port => port.Order).ToArray());
        Assert.HasCount(1, graph.Connections);
    }

    [TestMethod]
    public void TaskLogicOutputPublicPropertiesFailClosedAtGenericPropertyBoundary()
    {
        var output = GraphNodeFactory.Create(GraphScope.Task, "logic_output", "output");
        output.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("stable_id");
        output.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Public");
        var graph = new GraphDocument([output]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        var before = graph.ToJson();

        Assert.IsFalse(session.SetNodeProperty("output", "port_id",
            System.Text.Json.JsonSerializer.SerializeToElement("other_id")));
        Assert.AreEqual("graph.task.logic_output.port_id.immutable", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);

        // A same-value identity assignment remains a no-op and cannot create history.
        Assert.IsFalse(session.SetNodeProperty("output", "port_id",
            System.Text.Json.JsonSerializer.SerializeToElement("stable_id")));
        Assert.IsEmpty(session.LastValidationIssues);
        Assert.AreEqual(0, session.UndoCount);

        Assert.IsFalse(session.RemoveNodeProperty("output", "port_id"));
        Assert.AreEqual("graph.task.logic_output.port_id.immutable", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());

        Assert.IsFalse(session.SetNodeProperty("output", "display_name",
            System.Text.Json.JsonSerializer.SerializeToElement(" ")));
        Assert.AreEqual("graph.task.logic_output.display_name.invalid", session.LastValidationIssues.Single().Code);
        Assert.IsFalse(session.SetNodeProperty("output", "display_name",
            System.Text.Json.JsonSerializer.SerializeToElement(42)));
        Assert.AreEqual("graph.task.logic_output.display_name.invalid", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);

        Assert.IsTrue(session.SetNodeProperty("output", "display_name",
            System.Text.Json.JsonSerializer.SerializeToElement("Ready")));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsFalse(session.RemoveNodeProperty("output", "display_name"));
        Assert.AreEqual("graph.task.logic_output.display_name.immutable", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(1, session.UndoCount);
    }

    [TestMethod]
    public void FailedAddAndUnconfirmedReferencedRemoveAreNoOps()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "start", "Start", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "start", "Other", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
        ], [new("a", "out", "b", "out", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var before = graph.ToJson();
        Assert.IsFalse(session.AddDynamicPort("a", " ", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, session.UndoCount);
        Assert.IsTrue(session.AddDynamicPort("a", "Branch", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        var addedId = graph.Nodes[0].Ports.Single(port => port.DisplayName == "Branch").Id;
        graph.Connections.Add(new("a", addedId, "b", "out", GraphInterfaceKind.Flow));
        var beforeRemove = graph.ToJson();
        var undoBefore = session.UndoCount;
        Assert.IsFalse(session.RemoveDynamicPort("a", addedId));
        Assert.AreEqual(beforeRemove, graph.ToJson());
        Assert.AreEqual(undoBefore, session.UndoCount);
        Assert.IsTrue(session.RemoveDynamicPort("a", addedId, true));
    }

    [TestMethod]
    public void ChoiceSemanticPortsRejectGenericRenameAndReorder()
    {
        var graph = new GraphDocument([new GraphNode("choice", "choice", "Choice", [
            new("one", "One", false, GraphInterfaceKind.Flow, 0),
            new("two", "Two", false, GraphInterfaceKind.Flow, 1),
            new("fixed", "Fixed", true, GraphInterfaceKind.Flow, 0)])]);
        var session = new GraphEditSession(graph, GraphScope.Session);

        var before = graph.ToJson();
        Assert.IsFalse(session.RenamePortDisplayName("choice", "one", "Changed"));
        Assert.AreEqual("graph.dynamic_port.role.read_only", session.LastValidationIssues.Single().Code);
        Assert.IsFalse(session.ReorderPort("choice", "one", 1));
        Assert.AreEqual("graph.dynamic_port.role.read_only", session.LastValidationIssues.Single().Code);
        Assert.IsTrue(session.RenamePortDisplayName("choice", "fixed", " "));
        Assert.IsTrue(session.ReorderPort("choice", "fixed", -1));
    }

    [TestMethod]
    public void WrongScopeAndFixedPortsCannotBeRemovedAndMinimumIsProtected()
    {
        var graph = new GraphDocument([new GraphNode("choice", "choice", "Choice", [
            new("only", "Only", false, GraphInterfaceKind.Flow, 0),
            new("fixed", "Fixed", true, GraphInterfaceKind.Flow, 0)])]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        Assert.IsFalse(session.RemoveDynamicPort("choice", "only"));
        StringAssert.Contains(session.LastValidationIssues.Single().Message, "fixed");
        Assert.IsFalse(session.RemoveDynamicPort("choice", "fixed"));
        var minimumSession = new GraphEditSession(new GraphDocument([new GraphNode("choice", "choice", "Choice", [
            new("only", "Only", false, GraphInterfaceKind.Flow, 0)])]), GraphScope.Session);
        Assert.IsFalse(minimumSession.RemoveDynamicPort("choice", "only"));
        CollectionAssert.Contains(minimumSession.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.dynamic_port.role.read_only");
        var noScope = new GraphEditSession(graph);
        Assert.IsFalse(noScope.RemoveDynamicPort("choice", "only"));
        CollectionAssert.Contains(noScope.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.dynamic_port.scope.required");
    }

    [TestMethod]
    public void InjectedBlankAndExistingIdsAreSkippedBeforeUniqueId()
    {
        var graph = new GraphDocument([new GraphNode("start", "start", "Start", [
            new("existing", "Existing", false, GraphInterfaceKind.Flow, 0)])]);
        var ids = new Queue<string>([" ", "existing", "new-id"]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow, dynamicPortIdSource: ids.Dequeue);
        Assert.IsTrue(session.AddDynamicPort("start", "Added", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        Assert.AreEqual("new-id", graph.Nodes.Single().Ports.Single(port => port.DisplayName == "Added").Id);
    }

    [TestMethod]
    public void ChoiceSemanticPortsRejectGenericRemovalEvenWhenConfirmed()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "choice", "Choice", [new("first", "First", false, GraphInterfaceKind.Flow), new("second", "Second", false, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "choice", "Other", [new("in", "In", true, GraphInterfaceKind.Flow)]),
            new GraphNode("c", "choice", "Another", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
        ], [new("a", "first", "b", "in", GraphInterfaceKind.Flow), new("c", "out", "a", "first", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.Session);
        var before = graph.ToJson();
        Assert.IsFalse(session.RemoveDynamicPort("a", "first", true));
        Assert.AreEqual("graph.dynamic_port.role.read_only", session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.HasCount(2, graph.Connections);
        Assert.AreEqual(0, session.UndoCount);
    }

    [TestMethod]
    public void NewBranchDoesNotRecycleAnUndoneGeneratedId()
    {
        var graph = new GraphDocument([new GraphNode("start", "start", "Start", [
            new("base", "Base", false, GraphInterfaceKind.Flow)])]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        Assert.IsTrue(session.AddDynamicPort("start", "One", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        var firstId = graph.Nodes.Single().Ports.Single(port => port.DisplayName == "One").Id;
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.AddDynamicPort("start", "Two", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        var secondId = graph.Nodes.Single().Ports.Single(port => port.DisplayName == "Two").Id;
        Assert.AreNotEqual(firstId, secondId);
    }

    private static void AssertReadOnly(GraphEditSession session, GraphDocument graph, string before)
    {
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual("graph.dynamic_port.role.read_only", session.LastValidationIssues.Single().Code);
    }
}
