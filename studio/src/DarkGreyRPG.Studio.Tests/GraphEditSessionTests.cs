using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphEditSessionTests
{
    [TestMethod]
    public void AddNodeIsScopedCandidateLocalClonedAndAtomic()
    {
        var unrelatedDraft = new GraphNode("", "unknown", "Broken");
        unrelatedDraft.Ports.Add(null!);
        var graph = new GraphDocument([unrelatedDraft, null!]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var supplied = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action-1", "Action");
        supplied.Ports[0].DisplayName = "In";
        supplied.Ports[1].DisplayName = "Out";

        Assert.IsTrue(session.AddNode(supplied));
        supplied.Id = "caller-mutated";
        supplied.Ports[0].DisplayName = "Caller mutation";
        Assert.AreEqual("action-1", graph.Nodes.Single(node => node is not null && node.Id == "action-1").Id);
        Assert.AreEqual("In", graph.Nodes.Single(node => node is not null && node.Id == "action-1").Ports[0].DisplayName);
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(graph.Nodes.Any(node => node is not null && node.Id == "action-1"));
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(graph.Nodes.Any(node => node is not null && node.Id == "action-1"));

        var before = graph.ToJson();
        Assert.IsFalse(session.AddNode(new GraphNode("", "action", "Blank")));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.node.id.required"));
        Assert.AreEqual(before, graph.ToJson());
    }

    [TestMethod]
    public void AddNodeRejectsScopeTypeCompatibilityForbiddenKindAndUniqueConflicts()
    {
        var graph = new GraphDocument([
            new GraphNode("start-1", "start", "Start", [new("out", "Out", false, GraphInterfaceKind.Flow)])]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        Assert.IsFalse(session.AddNode(new GraphNode("bad", "does_not_exist", "Unknown")));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.node_type.unknown"));
        Assert.IsFalse(session.AddNode(new GraphNode("bad", "objective", "Wrong scope")));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.node_type.wrong_scope"));
        Assert.IsFalse(session.AddNode(new GraphNode("bad", "start", "Second")));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.required_node.duplicate"));
        Assert.IsFalse(session.AddNode(new GraphNode("start-1", "action", "Duplicate ID")));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.node.id.duplicate"));
        Assert.IsFalse(session.AddNode(new GraphNode("logic", "action", "Forbidden", [
            new("logic", "Logic", false, GraphInterfaceKind.Logic)])));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.port.interface_kind.disallowed"));

        var compatibility = new GraphEditSession(new GraphDocument(), GraphScope.Session);
        Assert.IsFalse(compatibility.AddNode(new GraphNode("legacy", "legacy_jump", "Legacy")));
        Assert.IsTrue(compatibility.LastValidationIssues.Any(issue => issue.Code == "graph.scope.node_type.compatibility_only"));
        Assert.IsTrue(new GraphEditSession(new GraphDocument(), GraphScope.Session, compatibilityMode: true)
            .AddNode(new GraphNode("legacy", "legacy_jump", "Legacy")));
        Assert.IsFalse(new GraphEditSession(new GraphDocument()).AddNode(new GraphNode("x", "action", "Unscoped")));
    }

    [TestMethod]
    public void NodeReferencesAreDetachedAndRemovalCleansIncidentEdgesAsOneUnit()
    {
        var graph = new GraphDocument([
            new GraphNode("start", "start", "Start", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("action", "action", "Action", [
                new("in", "In", true, GraphInterfaceKind.Flow),
                new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("end", "terminate", "End", [new("in", "In", true, GraphInterfaceKind.Flow)])], [
            new("start", "out", "action", "in", GraphInterfaceKind.Flow),
            new("action", "out", "end", "in", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);

        var references = session.GetNodeReferences("action");
        Assert.HasCount(2, references);
        references[0].FromNodeId = "mutated";
        Assert.AreEqual("start", graph.Connections[0].FromNodeId);
        Assert.IsEmpty(session.GetNodeReferences("missing"));
        graph.Nodes.Add(new GraphNode("action", "action", "Ambiguous"));
        Assert.IsEmpty(session.GetNodeReferences("action"));
        graph.Nodes.RemoveAt(graph.Nodes.Count - 1);

        var before = graph.ToJson();
        Assert.IsFalse(session.RemoveNode("action"));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.node.references.confirmation_required"));
        Assert.IsTrue(session.RemoveNode("action", confirmReferencedRemoval: true));
        Assert.IsFalse(graph.Nodes.Any(node => node.Id == "action"));
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(graph.Nodes.Any(node => node.Id == "action"));
        Assert.HasCount(2, graph.Connections);
        Assert.IsTrue(session.Redo());
        Assert.IsEmpty(graph.Connections);
        Assert.IsFalse(session.RemoveNode("start"));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.node.not_deletable"));
    }

    [TestMethod]
    public void UnknownNodeCanBeRemovedToRepairMalformedDraft()
    {
        var graph = new GraphDocument([
            new GraphNode("unknown", "not_registered", "Unknown"),
            new GraphNode("wrong-scope", "objective", "Wrong scope")]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);

        Assert.IsTrue(session.RemoveNode("unknown"));
        Assert.IsTrue(session.RemoveNode("wrong-scope"));
        Assert.IsEmpty(graph.Nodes);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(graph.Nodes.Any(node => node.Id == "wrong-scope"));
    }

    [TestMethod]
    public void ConnectAndDisconnectAreAtomicUndoUnits()
    {
        var graph = FlowGraph();
        var session = new GraphEditSession(graph);
        var edge = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);

        Assert.IsTrue(session.Connect(edge));
        Assert.HasCount(1, graph.Connections);
        Assert.IsTrue(session.Undo());
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(edge, graph.Connections.Single());
        Assert.IsTrue(session.Disconnect(edge));
        Assert.IsEmpty(graph.Connections);
    }

    [TestMethod]
    public void FailedConnectDoesNotMutateDocumentOrHistory()
    {
        var graph = FlowGraph();
        graph.Connections.Add(new("a", "out", "b", "in", GraphInterfaceKind.Flow));
        var before = graph.ToJson();
        var session = new GraphEditSession(graph);

        Assert.IsFalse(session.Connect(new("a", "out", "missing", "in", GraphInterfaceKind.Flow)));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.CanUndo);
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.to.node.missing");
    }

    [TestMethod]
    public void CandidateRejectsDuplicateAndAffectedEndpointCardinality()
    {
        var graph = FlowGraph();
        var existing = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(existing);
        var session = new GraphEditSession(graph);

        Assert.IsFalse(session.Connect(existing));
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.duplicate");
        Assert.IsFalse(session.Connect(new("a", "out", "c", "in", GraphInterfaceKind.Flow)));
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.flow.output.multiple_targets");

        var logic = new GraphDocument([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "B", [new("out", "Out", false, GraphInterfaceKind.Logic)]),
            new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Logic)])],
            [new("a", "out", "c", "in", GraphInterfaceKind.Logic)]);
        var logicSession = new GraphEditSession(logic);
        Assert.IsFalse(logicSession.Connect(new("b", "out", "c", "in", GraphInterfaceKind.Logic)));
        CollectionAssert.Contains(logicSession.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.logic.input.multiple_sources");
    }

    [TestMethod]
    public void CandidateRejectsDirectionAndKindMismatch()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "A", [new("input", "Input", true, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "B", [new("output", "Output", false, GraphInterfaceKind.Logic)])]);
        var issues = CandidateEdgeValidator.Validate(graph, new("a", "input", "b", "output", GraphInterfaceKind.Flow));
        var codes = issues.Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "graph.connection.source.direction");
        CollectionAssert.Contains(codes, "graph.connection.target.direction");
        CollectionAssert.Contains(codes, "graph.connection.target.kind.mismatch");
    }

    [TestMethod]
    public void WrongScopeIsCheckedOnlyWhenScopeIsProvided()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "line", "Line", [new("out", "Out", false, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "objective", "Objective", [new("in", "In", true, GraphInterfaceKind.Logic)])]);
        var candidate = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Logic);
        Assert.IsEmpty(CandidateEdgeValidator.Validate(graph, candidate));
        CollectionAssert.Contains(CandidateEdgeValidator.Validate(graph, candidate, GraphScope.Task)
            .Select(issue => issue.Code).ToArray(), "graph.scope.node_type.wrong_scope");
    }

    [TestMethod]
    public void ExistingUnrelatedLogicCycleDoesNotBlockLocalEdit()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "B", [new("out", "Out", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("c", "C", [new("out", "Out", false, GraphInterfaceKind.Logic)]),
            new GraphNode("d", "D", [new("in", "In", true, GraphInterfaceKind.Logic)])],
            [new("a", "out", "b", "in", GraphInterfaceKind.Logic), new("b", "out", "a", "in", GraphInterfaceKind.Logic)]);
        var session = new GraphEditSession(graph);
        Assert.IsTrue(session.Connect(new("c", "out", "d", "in", GraphInterfaceKind.Logic)));
    }

    [TestMethod]
    public void ReconnectValidatesReplacementBeforeRemovingOriginal()
    {
        var graph = FlowGraph();
        graph.Nodes.Add(new GraphNode("d", "D", [new("out", "Out", false, GraphInterfaceKind.Flow)]));
        var original = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(new("d", "out", "b", "in", GraphInterfaceKind.Flow));
        graph.Connections.Add(original);
        var session = new GraphEditSession(graph);

        Assert.IsFalse(session.Reconnect(original, new("a", "missing", "c", "in", GraphInterfaceKind.Flow)));
        Assert.AreEqual(original, graph.Connections[1]);
        Assert.IsFalse(session.CanUndo);
        Assert.IsTrue(session.Reconnect(original, new("a", "out", "c", "in", GraphInterfaceKind.Flow)));
        Assert.AreEqual("c", graph.Connections[1].ToNodeId);
        Assert.AreEqual("d", graph.Connections[0].FromNodeId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("b", graph.Connections[1].ToNodeId);
    }

    [TestMethod]
    public void DuplicateOriginalReconnectExcludesOnlyOneEdgeAndCannotHideCardinality()
    {
        var graph = FlowGraph();
        var original = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(original);
        graph.Connections.Add(new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow));
        var before = graph.ToJson();
        var session = new GraphEditSession(graph);

        Assert.IsFalse(session.Reconnect(original, new("a", "out", "c", "in", GraphInterfaceKind.Flow)));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.CanUndo);
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(), "graph.connection.flow.output.multiple_targets");
    }

    [TestMethod]
    public void RenameAndReorderPreserveStablePortAndConnectionIdentity()
    {
        var graph = FlowGraph();
        var edge = new GraphConnection("a", "out", "b", "in", GraphInterfaceKind.Flow);
        graph.Connections.Add(edge);
        var session = new GraphEditSession(graph);

        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Renamed"));
        Assert.IsTrue(session.ReorderPort("a", "out", 42));
        Assert.AreEqual("out", graph.Nodes.Single(node => node.Id == "a").Ports.Single().Id);
        Assert.AreEqual(edge, graph.Connections.Single());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, graph.Nodes.Single(node => node.Id == "a").Ports.Single().Order);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(42, graph.Nodes.Single(node => node.Id == "a").Ports.Single().Order);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("Out", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("Renamed", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
    }

    [TestMethod]
    public void DeepHistoryIgnoresExternalChangesAndNewEditClearsRedo()
    {
        var graph = FlowGraph();
        var session = new GraphEditSession(graph);
        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Renamed"));
        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Renamed Again"));
        graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName = "External";
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("Renamed", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("Renamed Again", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Branch"));
        Assert.IsFalse(session.CanRedo);
    }

    [TestMethod]
    public void ApplyingHistorySnapshotsDoesNotExposeBeforeObjects()
    {
        var graph = FlowGraph();
        var session = new GraphEditSession(graph);
        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Renamed"));
        Assert.IsTrue(session.Undo());
        graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName = "External after undo";
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("Out", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
    }

    [TestMethod]
    public void LocalCandidateValidationIgnoresUnrelatedProblemsAndRejectsLogicCycle()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("b", "B", [new("out", "Out", false, GraphInterfaceKind.Logic), new("in", "In", true, GraphInterfaceKind.Logic)]),
            new GraphNode("broken", "Broken", [new("x", "X", false, GraphInterfaceKind.Flow)]),
        ], [new("a", "out", "b", "in", GraphInterfaceKind.Logic), new("missing", "x", "broken", "x", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph);

        Assert.IsFalse(session.Connect(new("b", "out", "a", "in", GraphInterfaceKind.Logic)));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.logic.cycle"));
        Assert.IsFalse(session.Connect(new("a", "in", "broken", "x", GraphInterfaceKind.Flow)));
        Assert.IsFalse(session.CanUndo);
    }

    [TestMethod]
    public void ScopeIsOptionalAndTaskRejectsFlowWithoutRequiringOtherNodes()
    {
        var graph = new GraphDocument([
            new GraphNode("a", "objective", "Objective", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "objective", "Objective 2", [new("in", "In", true, GraphInterfaceKind.Flow)]),
        ]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsFalse(session.Connect(new("a", "out", "b", "in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.task.flow_connection.disallowed"));
        Assert.IsFalse(session.LastValidationIssues.Any(issue => issue.Code == "graph.scope.required_node.missing"));
    }

    [TestMethod]
    public void NodePropertyEditsAreAtomicAndHistoryIsolated()
    {
        using var source = JsonDocument.Parse("{\"items\":[{\"name\":\"one\"}],\"enabled\":true}");
        var input = source.RootElement.Clone();
        var graph = new GraphDocument([new GraphNode("node", "Node", [])]);
        var session = new GraphEditSession(graph);

        Assert.IsTrue(session.SetNodeProperty("node", "config", input));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsFalse(session.SetNodeProperty("node", "config", input));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.SetNodeProperty("node", "other", JsonSerializer.SerializeToElement(2)));
        graph.Nodes.Single().Properties["config"] = JsonSerializer.SerializeToElement(new { mutated = true });
        graph.Nodes.Single().Properties["other"] = JsonSerializer.SerializeToElement(99);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(JsonElement.DeepEquals(input, graph.Nodes.Single().Properties["config"]));
        Assert.IsFalse(graph.Nodes.Single().Properties.ContainsKey("other"));
        graph.Nodes.Single().Properties["config"] = JsonSerializer.SerializeToElement(new { mutated_again = true });
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(JsonElement.DeepEquals(input, graph.Nodes.Single().Properties["config"]));
        Assert.AreEqual(2, graph.Nodes.Single().Properties["other"].GetInt32());

        Assert.IsFalse(session.SetNodeProperty("node", "", JsonSerializer.SerializeToElement(1)));
        Assert.IsFalse(session.SetNodeProperty("node", "bad", default));
        Assert.IsFalse(session.RemoveNodeProperty("node", "missing"));
        Assert.AreEqual(1, session.LastValidationIssues.Count(issue => issue.Code == "graph.node.property.missing"));
    }

    [TestMethod]
    public void NodePropertyRemoveUndoRedoDoesNotAliasLiveOrHistoryValues()
    {
        var graph = new GraphDocument([new GraphNode("node", "Node", [])]);
        var session = new GraphEditSession(graph);
        Assert.IsTrue(session.SetNodeProperty("node", "config", JsonSerializer.SerializeToElement(new { nested = new[] { 1, 2 } })));
        Assert.IsTrue(session.RemoveNodeProperty("node", "config"));
        Assert.IsTrue(session.Undo());
        var restored = graph.Nodes.Single().Properties["config"];
        Assert.IsTrue(session.Redo());
        Assert.IsFalse(graph.Nodes.Single().Properties.ContainsKey("config"));
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(JsonElement.DeepEquals(restored, graph.Nodes.Single().Properties["config"]));
    }

    [TestMethod]
    public void RollbackLastEditRestoresRedoHistoryDisplacedByOuterTransaction()
    {
        var graph = FlowGraph();
        var session = new GraphEditSession(graph);
        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "First"));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.UndoCount);
        Assert.AreEqual(1, session.RedoCount);

        Assert.IsTrue(session.RenamePortDisplayName("a", "out", "Transactional"));
        Assert.AreEqual(1, session.UndoCount);
        Assert.AreEqual(0, session.RedoCount);
        Assert.IsTrue(session.RollbackLastEdit());

        Assert.AreEqual("Out", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
        Assert.AreEqual(0, session.UndoCount);
        Assert.AreEqual(1, session.RedoCount);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("First", graph.Nodes.Single(node => node.Id == "a").Ports.Single().DisplayName);
    }

    private static GraphDocument FlowGraph()
        => new([
            new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow)]),
            new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Flow)]),
        ]);
}
