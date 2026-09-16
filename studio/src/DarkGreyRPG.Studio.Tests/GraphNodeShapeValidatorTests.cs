using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphNodeShapeValidatorTests
{
    [TestMethod]
    public void TopLevelNullArgumentsThrowAndMalformedPortEntriesDoNot()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            GraphNodeShapeValidator.Validate((GraphNode)null!, GraphScope.Session));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            GraphNodeShapeValidator.Validate((GraphDocument)null!, GraphScope.Session));

        var node = Node(GraphScope.Session, "line");
        node.Ports.Add(null!);
        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.port.required");
    }

    [TestMethod]
    public void ValidFixedShapeIgnoresPresentationMetadata()
    {
        var node = Node(GraphScope.Session, "line");
        node.Ports.Reverse();
        node.Ports[0].DisplayName = "Renamed";
        node.Ports[0].Order = 99;

        Assert.IsEmpty(GraphNodeShapeValidator.Validate(node, GraphScope.Session));
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(node, GraphScope.Session));
    }

    [TestMethod]
    public void FixedShapeReportsMissingDuplicateAndCanonicalMismatches()
    {
        var node = Node(GraphScope.Session, "line");
        node.Ports.RemoveAll(port => port.Id == "flow_out");
        node.Ports[0].IsInput = false;
        node.Ports[0].Kind = GraphInterfaceKind.Logic;

        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.fixed_port.missing");
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.fixed_port.direction");
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.fixed_port.kind");
        Assert.IsTrue(issues.All(issue => issue.NodeId == "line_node"));

        var duplicate = Node(GraphScope.Session, "line");
        duplicate.Ports.Add(new GraphPort("flow_in", "Duplicate", true, GraphInterfaceKind.Flow));
        CollectionAssert.Contains(
            GraphNodeShapeValidator.Validate(duplicate, GraphScope.Session).Select(issue => issue.Code).ToArray(),
            "graph.node.shape.fixed_port.duplicate");
    }

    [TestMethod]
    public void UnexpectedPortsMustMatchRegisteredDynamicRole()
    {
        var node = Node(GraphScope.Session, "line");
        node.Ports.Add(new GraphPort("unexpected", "Unexpected", false, GraphInterfaceKind.Flow));
        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);

        var issue = issues.Single(item => item.Code == "graph.node.shape.dynamic_port.unexpected");
        Assert.AreEqual("ports[unexpected]", issue.Field);
        Assert.AreEqual("line_node", issue.NodeId);
    }

    [TestMethod]
    public void DynamicRolesEnforceEveryRegisteredMinimum()
    {
        foreach (var role in GraphDynamicPortPolicy.Roles)
        {
            var node = role.Scope == GraphScope.StoryFlow && role.NodeType == "start"
                ? GraphNodeFactory.CreateStoryStart("start", triggerPortId: "trigger")
                : role.Scope == GraphScope.Task && role.NodeType == "objective"
                    ? GraphNodeFactory.Create(GraphScope.Task, "objective", "objective")
                : Node(role.Scope, role.NodeType);
            if (role.Scope == GraphScope.Task && role.NodeType == "objective")
                node.Properties[CanonicalTaskObjectiveSchema.EntityProperty] =
                    JsonSerializer.SerializeToElement("test_actor");
            if (role.Scope == GraphScope.Task && role.NodeType == "objective") node.Properties["description"] = JsonSerializer.SerializeToElement("Test objective");
            if (role.Scope == GraphScope.Session && role.NodeType == "choice")
            {
                SessionChoiceSchema.InitializeDefault(node, "option_1", "flow_1");
                Assert.IsEmpty(GraphNodeShapeValidator.Validate(node, role.Scope));
                continue;
            }
            var existingCount = node.Ports.Count(port =>
                port.IsInput == (role.Direction == GraphPortDirection.Input)
                && port.InterfaceKind == role.InterfaceKind
                && !GraphNodeDefinitionRegistry.Get(role.Scope, role.NodeType)!.FixedPorts.Any(fixedPort => fixedPort.Id == port.Id));
            for (var index = existingCount; index < role.MinimumCount; index++)
            {
                node.Ports.Add(new GraphPort($"dynamic_{index}", $"Dynamic {index}",
                    role.Direction == GraphPortDirection.Input, role.InterfaceKind, index));
            }

            Assert.IsEmpty(GraphNodeShapeValidator.Validate(node, role.Scope),
                $"Role {role.Scope}/{role.NodeType}/{role.Direction}/{role.InterfaceKind} should meet its minimum.");
        }

        var deficient = Node(GraphScope.Task, "settle");
        var issue = GraphNodeShapeValidator.Validate(deficient, GraphScope.Task)
            .Single(item => item.Code == "graph.node.shape.dynamic_port.minimum");
        StringAssert.Contains(issue.Message, "requires at least 1");

        var mutatedFixedPort = Node(GraphScope.Task, "and");
        mutatedFixedPort.Ports.Single(port => port.Id == "logic_out").IsInput = true;
        mutatedFixedPort.Ports.Add(new GraphPort("only_dynamic", "Only Dynamic", true, GraphInterfaceKind.Logic));
        var mutatedIssues = GraphNodeShapeValidator.Validate(mutatedFixedPort, GraphScope.Task);
        CollectionAssert.Contains(mutatedIssues.Select(item => item.Code).ToArray(),
            "graph.node.shape.fixed_port.direction");
        CollectionAssert.Contains(mutatedIssues.Select(item => item.Code).ToArray(),
            "graph.node.shape.dynamic_port.minimum");
    }

    [TestMethod]
    public void SessionChoiceRequiresFlowPortsAndValidatesRetainedLegacyLogicPorts()
    {
        var node = Node(GraphScope.Session, "choice");
        SessionChoiceSchema.InitializeDefault(node, "option_1", "flow_1");
        Assert.IsEmpty(GraphNodeShapeValidator.Validate(node, GraphScope.Session));

        node.Ports.Add(new("option_1", "Desynchronized", false, GraphInterfaceKind.Logic, 0));
        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(),
            "graph.session.choice.legacy_logic.presentation");

        node = Node(GraphScope.Session, "choice");
        node.Properties["options"] = JsonSerializer.SerializeToElement(new[]
        {
            new { option_id = "same", display_text = "One", flow_port_id = "same" },
        });
        node.Ports.Add(new("same", "One", false, GraphInterfaceKind.Flow, 0));
        node.Ports.Add(new("same", "已选择：One", false, GraphInterfaceKind.Logic, 0));
        issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(),
            "graph.session.choice.port_id.collision");
    }

    [TestMethod]
    public void RequiredPropertiesCheckMissingAndJsonKindButAllowUnknownExtras()
    {
        var node = Node(GraphScope.Session, "line");
        node.Properties.Remove("text");
        node.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(42);
        node.Properties["unknown"] = JsonSerializer.SerializeToElement(true);

        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        Assert.IsTrue(issues.Any(issue => issue.Code == "graph.session.line" && issue.Field == "text"));
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.property.kind");
        Assert.IsFalse(issues.Any(issue => issue.Field == "properties.unknown"));
    }

    [TestMethod]
    public void CompatibilityDefinitionRequiresOptInAndValidatesWhenEnabled()
    {
        var node = new GraphNode("legacy", "legacy_jump", "Legacy");
        var blocked = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
        CollectionAssert.AreEqual(new[] { "graph.node.shape.compatibility.required" }, blocked.Select(issue => issue.Code).ToArray());
        Assert.IsEmpty(GraphNodeShapeValidator.Validate(node, GraphScope.Session, compatibilityMode: true));
    }

    [TestMethod]
    public void LegacySessionStartLogicOutputLoadsWithExplicitMigrationWarning()
    {
        var start = Node(GraphScope.Session, "start");
        start.Ports.Add(new GraphPort("logic_out", "旧版逻辑输出", false, GraphInterfaceKind.Logic));

        var issue = GraphNodeShapeValidator.Validate(start, GraphScope.Session)
            .Single(candidate => candidate.Code == "graph.session.start.logic_output.legacy");

        Assert.AreEqual(ValidationSeverity.Warning, issue.Severity);
        StringAssert.Contains(issue.Message, "旧版");
    }

    [TestMethod]
    public void UnknownAndWrongScopeDefinitionsAreTerminalSingleIssues()
    {
        foreach (var node in new[] {
            new GraphNode("unknown", "not_registered", "Unknown", [new(null!, "bad", false, GraphInterfaceKind.Flow)]),
            Node(GraphScope.Task, "objective")
        })
        {
            var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session);
            Assert.HasCount(1, issues);
            Assert.AreEqual("graph.node.shape.definition.unavailable", issues[0].Code);
        }
    }

    [TestMethod]
    public void GraphAggregationDiagnosesNullEntriesAndKeepsNodePaths()
    {
        var graph = new GraphDocument([null!, Node(GraphScope.Session, "line")]);
        graph.Nodes[1].Properties.Remove("text");

        var issues = GraphNodeShapeValidator.Validate(graph, GraphScope.Session);
        CollectionAssert.Contains(issues.Select(issue => issue.Code).ToArray(), "graph.node.shape.node.required");
        var missing = issues.Single(issue => issue.Code == "graph.session.line" && issue.Field == "text");
        Assert.AreEqual("line_node", missing.NodeId);
        Assert.AreEqual("text", missing.Field);
        Assert.IsFalse(GraphNodeShapeValidator.IsValid(graph, GraphScope.Session));
    }

    private static GraphNode Node(GraphScope scope, string type)
    {
        var definition = GraphNodeDefinitionRegistry.Get(scope, type)!;
        var properties = definition.PropertyDefinitions
            .Where(property => property.DefaultValue.HasValue)
            .ToDictionary(property => property.Name, property => property.DefaultValue!.Value,
                StringComparer.Ordinal);
        return new GraphNode($"{type}_node", type, type, definition.FixedPorts.Select(port => port.CreatePort()), properties);
    }
}
