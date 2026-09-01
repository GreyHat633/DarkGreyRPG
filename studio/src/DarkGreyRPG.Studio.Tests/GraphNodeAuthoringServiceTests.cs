using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphNodeAuthoringServiceTests
{
    [TestMethod]
    [DataRow(GraphScope.StoryFlow, "terminate", "终止")]
    [DataRow(GraphScope.Session, "line", "台词")]
    [DataRow(GraphScope.Task, "objective", "目标")]
    public void FixedOnlyCandidatesAreLocalizedDetachedAndAuthoringValid(
        GraphScope scope, string type, string expectedName)
    {
        var graph = new GraphDocument();
        var before = graph.ToJson();

        var result = new GraphNodeAuthoringService().Create(graph, scope, type, "new_node");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(expectedName, result.Candidate.DisplayName);
        if (scope == GraphScope.Task && type == CanonicalTaskObjectiveSchema.NodeType)
        {
            Assert.IsTrue(CanonicalTaskObjectiveSchema.IsUnselectedTarget(result.Candidate));
            CollectionAssert.Contains(GraphNodeShapeValidator.Validate(result.Candidate, scope)
                .Select(issue => issue.Code).ToArray(), "graph.objective.target.invalid");
        }
        else
        {
            Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, scope));
        }
        Assert.AreEqual(before, graph.ToJson());

        result.Candidate.DisplayName = "changed";
        if (result.Candidate.Ports.Count > 0) result.Candidate.Ports[0].DisplayName = "changed";
        Assert.AreEqual(expectedName, GraphNodeFactory.Create(scope, type, "fresh").DisplayName);
    }

    [TestMethod]
    [DataRow(GraphScope.StoryFlow, "and")]
    [DataRow(GraphScope.StoryFlow, "or")]
    [DataRow(GraphScope.Session, "and")]
    [DataRow(GraphScope.Session, "or")]
    [DataRow(GraphScope.Task, "and")]
    [DataRow(GraphScope.Task, "or")]
    public void AndOrCandidatesReceiveExactlyTwoDeterministicLogicInputs(GraphScope scope, string type)
    {
        var ids = new Queue<string>(["opaque_a", "opaque_b"]);
        var graph = new GraphDocument();
        var result = new GraphNodeAuthoringService(() => ids.Dequeue())
            .Create(graph, scope, type, "logic_node");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        var dynamicInputs = result.Candidate!.Ports
            .Where(port => port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic)
            .OrderBy(port => port.Order)
            .ToArray();
        Assert.HasCount(2, dynamicInputs);
        CollectionAssert.AreEqual(new[] { "opaque_a", "opaque_b" }, dynamicInputs.Select(port => port.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "输入 1", "输入 2" }, dynamicInputs.Select(port => port.DisplayName).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, dynamicInputs.Select(port => port.Order).ToArray());
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, scope));
        Assert.IsEmpty(graph.Nodes);
    }

    [TestMethod]
    [DataRow(GraphScope.StoryFlow, "start")]
    [DataRow(GraphScope.StoryFlow, "session")]
    [DataRow(GraphScope.StoryFlow, "task")]
    public void SemanticBoundRolesFailClosedWithoutConsumingIdsOrMutatingGraph(GraphScope scope, string type)
    {
        var sourceCalls = 0;
        var graph = new GraphDocument([new GraphNode("existing", ScopeBaselineType(scope), "Existing")]);
        var before = graph.ToJson();

        var result = new GraphNodeAuthoringService(() => { sourceCalls++; return "unused"; })
            .Create(graph, scope, type, "semantic_node");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate);
        Assert.AreEqual("graph.node.create.semantic_initializer.required", result.Issues.Single().Code);
        Assert.AreEqual(0, sourceCalls);
        Assert.AreEqual(before, graph.ToJson());
    }

    [TestMethod]
    public void TaskSettleReceivesOneStableLogicResultSlot()
    {
        var result = new GraphNodeAuthoringService(() => "settle_result_1")
            .Create(new GraphDocument(), GraphScope.Task, "settle", "settle");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        var slot = result.Candidate!.Ports.Single();
        Assert.IsTrue(slot.IsInput);
        Assert.AreEqual(GraphInterfaceKind.Logic, slot.InterfaceKind);
        Assert.AreEqual("settle_result_1", slot.Id);
        Assert.AreEqual(0, slot.Order);
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, GraphScope.Task));
    }

    [TestMethod]
    public void TaskLogicOutputReceivesOpaquePublicBoundaryProperties()
    {
        var result = new GraphNodeAuthoringService(() => "task_logic_1")
            .Create(new GraphDocument(), GraphScope.Task, "logic_output", "output");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual("task_logic_1", result.Candidate!.Properties["port_id"].GetString());
        Assert.AreEqual("逻辑输出", result.Candidate.Properties["display_name"].GetString());
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, GraphScope.Task));
    }

    [TestMethod]
    public void TaskBoundaryIdsRejectReservedAndCrossBoundaryCollisionsWithoutMutation()
    {
        var reservedGraph = new GraphDocument();
        var reservedBefore = reservedGraph.ToJson();
        AssertFailure(new GraphNodeAuthoringService(() => "flow_in")
            .Create(reservedGraph, GraphScope.Task, "settle", "settle"),
            "graph.node.create.public_port_id.reserved");
        Assert.AreEqual(reservedBefore, reservedGraph.ToJson());

        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("settle_result", "Result", true, GraphInterfaceKind.Logic, 0));
        var output = GraphNodeFactory.Create(GraphScope.Task, "logic_output", "output");
        output.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("public_id");
        output.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Public");
        var graph = new GraphDocument([settle, output]);
        var before = graph.ToJson();

        AssertFailure(new GraphNodeAuthoringService(() => "settle_result")
            .Create(graph, GraphScope.Task, "logic_output", "new_output"),
            "graph.node.create.public_port_id.duplicate");
        Assert.AreEqual(before, graph.ToJson());
    }

    [TestMethod]
    public void TaskLogicOutputDefaultDisplayNamesAreUniqueAcrossBoundaries()
    {
        var existing = GraphNodeFactory.Create(GraphScope.Task, "logic_output", "first");
        existing.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("first_id");
        existing.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("逻辑输出");
        var graph = new GraphDocument([existing]);

        var result = new GraphNodeAuthoringService(() => "second_id")
            .Create(graph, GraphScope.Task, "logic_output", "second");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual("逻辑输出 2", result.Candidate!.Properties["display_name"].GetString());
    }

    [TestMethod]
    public void SessionChoiceReceivesOneFlowOnlySemanticOptionWithoutMutatingGraph()
    {
        var ids = new Queue<string>(["option_1", "flow_1"]);
        var sourceCalls = 0;
        var graph = new GraphDocument();
        var before = graph.ToJson();

        var result = new GraphNodeAuthoringService(() =>
        {
            sourceCalls++;
            return ids.Dequeue();
        }).Create(graph, GraphScope.Session, "choice", "choice_1");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual(2, sourceCalls);
        Assert.AreEqual(before, graph.ToJson());
        var candidate = result.Candidate!;
        CollectionAssert.AreEqual(new[] { "flow_in", "flow_1" },
            candidate.Ports.Select(port => port.Id).ToArray());
        Assert.AreEqual("选项 1", candidate.Ports.Single(port => port.Id == "flow_1").DisplayName);
        Assert.IsFalse(candidate.Ports.Any(port => port.InterfaceKind == GraphInterfaceKind.Logic));
        var option = candidate.Properties["options"].EnumerateArray().Single();
        Assert.AreEqual("option_1", option.GetProperty("option_id").GetString());
        Assert.AreEqual("选项 1", option.GetProperty("display_text").GetString());
        Assert.AreEqual("flow_1", option.GetProperty("flow_port_id").GetString());
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(candidate, GraphScope.Session));
    }

    [TestMethod]
    public void SessionChoiceRejectsCollidingSemanticIdsWithoutPartialCandidate()
    {
        var result = new GraphNodeAuthoringService(() => "same")
            .Create(new GraphDocument(), GraphScope.Session, "choice", "choice_1");

        AssertFailure(result, "graph.node.create.dynamic_port_id.duplicate");
    }

    [TestMethod]
    [DataRow("end", "结束")]
    [DataRow("logic_output", "逻辑输出")]
    public void SessionPublicBoundariesReceiveOpaqueStableIdsAndAuthorFacingNames(
        string type,
        string expectedName)
    {
        var sourceCalls = 0;
        var graph = new GraphDocument();
        var result = new GraphNodeAuthoringService(() =>
        {
            sourceCalls++;
            return "public_boundary_1";
        }).Create(graph, GraphScope.Session, type, "boundary_1");

        Assert.IsTrue(result.IsSuccess, string.Join(",", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual(1, sourceCalls);
        Assert.AreEqual("public_boundary_1", result.Candidate!.Properties["port_id"].GetString());
        Assert.AreEqual(expectedName, result.Candidate.Properties["display_name"].GetString());
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(result.Candidate, GraphScope.Session));
        Assert.IsEmpty(graph.Nodes);
    }

    [TestMethod]
    public void SessionPublicBoundaryIdsRejectMissingReservedAndExistingValues()
    {
        AssertFailure(new GraphNodeAuthoringService(() => null)
            .Create(new GraphDocument(), GraphScope.Session, "end", "end"),
            "graph.node.create.public_port_id.required");
        AssertFailure(new GraphNodeAuthoringService(() => "flow_in")
            .Create(new GraphDocument(), GraphScope.Session, "end", "end"),
            "graph.node.create.public_port_id.reserved");

        var existing = GraphNodeFactory.Create(GraphScope.Session, "end", "existing");
        existing.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("already_used");
        existing.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Existing");
        AssertFailure(new GraphNodeAuthoringService(() => "already_used")
            .Create(new GraphDocument([existing]), GraphScope.Session, "logic_output", "output"),
            "graph.node.create.public_port_id.duplicate");
    }

    [TestMethod]
    public void InvalidIdentityTypeScopeAndCompatibilityReturnDistinctDiagnostics()
    {
        var service = new GraphNodeAuthoringService();
        var graph = new GraphDocument();

        CollectionAssert.AreEqual(
            new[] { "graph.node.create.id.required", "graph.node.create.type.required" },
            service.Create(graph, GraphScope.Session, " ", " ").Issues.Select(issue => issue.Code).ToArray());
        Assert.AreEqual("graph.node.create.type.unknown",
            service.Create(graph, GraphScope.Session, "missing", "x").Issues.Single().Code);
        Assert.AreEqual("graph.node.create.type.wrong_scope",
            service.Create(graph, GraphScope.Session, "objective", "x").Issues.Single().Code);
        Assert.AreEqual("graph.node.create.type.compatibility_only",
            service.Create(graph, GraphScope.Session, "legacy_jump", "x").Issues.Single().Code);
    }

    [TestMethod]
    public void DuplicateIdentityAndUniqueTypeTakePriorityOverSemanticInitialization()
    {
        var service = new GraphNodeAuthoringService();
        var duplicateId = new GraphDocument([new GraphNode("same", "terminate", "Existing")]);
        Assert.AreEqual("graph.node.create.id.duplicate",
            service.Create(duplicateId, GraphScope.StoryFlow, "start", "same").Issues.Single().Code);

        var uniqueType = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "start", "start_one")]);
        Assert.AreEqual("graph.node.create.type.unique",
            service.Create(uniqueType, GraphScope.Session, "start", "start_two").Issues.Single().Code);
        Assert.IsTrue(service.Create(uniqueType, GraphScope.Session, "line", "line_one").IsSuccess);
    }

    [TestMethod]
    public void DynamicIdFailuresReturnNoPartialCandidate()
    {
        static GraphNodeAuthoringResult CreateWith(params string?[] supplied)
        {
            var ids = new Queue<string?>(supplied);
            return new GraphNodeAuthoringService(() => ids.Dequeue())
                .Create(new GraphDocument(), GraphScope.Task, "and", "logic");
        }

        AssertFailure(CreateWith(null, "second"), "graph.node.create.dynamic_port_id.required");
        AssertFailure(CreateWith("dup", "dup"), "graph.node.create.dynamic_port_id.duplicate");
        AssertFailure(CreateWith("logic_out", "second"), "graph.node.create.dynamic_port_id.fixed_conflict");

        var unavailable = new GraphNodeAuthoringService(() => throw new InvalidOperationException("no id"))
            .Create(new GraphDocument(), GraphScope.Task, "or", "logic");
        AssertFailure(unavailable, "graph.node.create.dynamic_port_id.unavailable");
    }

    [TestMethod]
    public void ExplicitDisplayOverrideAndTrySeamArePreserved()
    {
        var service = new GraphNodeAuthoringService();
        Assert.IsTrue(service.TryCreate(new GraphDocument(), GraphScope.Session, "line", "line",
            out var candidate, out var issues, "  自定义台词  "));
        Assert.IsEmpty(issues);
        Assert.IsNotNull(candidate);
        Assert.AreEqual("自定义台词", candidate.DisplayName);
    }

    [TestMethod]
    public void NullGraphThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new GraphNodeAuthoringService().Create(null!, GraphScope.Session, "line", "line"));
    }

    private static void AssertFailure(GraphNodeAuthoringResult result, string code)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate);
        Assert.AreEqual(code, result.Issues.Single().Code);
    }

    private static string ScopeBaselineType(GraphScope scope) => scope switch
    {
        GraphScope.StoryFlow => "terminate",
        GraphScope.Session => "line",
        _ => "objective",
    };
}
