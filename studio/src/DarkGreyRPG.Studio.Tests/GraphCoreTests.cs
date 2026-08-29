using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphCoreTests
{
    [TestMethod]
    public void ConnectionJsonUsesCanonicalStableIdsAndLowercaseKind()
    {
        var connection = new GraphConnection("node_a", "port_x", "node_b", "port_y", GraphInterfaceKind.Flow);
        var json = GraphSerializer.SerializeConnection(connection);
        StringAssert.Contains(json, "\"from_node_id\":\"node_a\"");
        StringAssert.Contains(json, "\"from_port_id\":\"port_x\"");
        StringAssert.Contains(json, "\"to_node_id\":\"node_b\"");
        StringAssert.Contains(json, "\"to_port_id\":\"port_y\"");
        StringAssert.Contains(json, "\"interface_kind\":\"flow\"");
        Assert.AreEqual(connection, GraphSerializer.DeserializeConnection(json));
    }

    [TestMethod]
    public void PortJsonUsesStableIdKindAndLowercaseDirection()
    {
        var json = JsonSerializer.Serialize(new GraphPort("p1", "Visible", false, GraphInterfaceKind.Logic, 3), GraphSerializer.Options);
        StringAssert.Contains(json, "\"port_id\":\"p1\"");
        StringAssert.Contains(json, "\"display_name\":\"Visible\"");
        StringAssert.Contains(json, "\"order\":3");
        StringAssert.Contains(json, "\"kind\":\"logic\"");
        StringAssert.Contains(json, "\"direction\":\"output\"");
        Assert.IsFalse(json.Contains("is_input", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GraphSerializerRejectsUnknownMembersCommentsAndTrailingCommas()
    {
        Assert.Throws<JsonException>(() => GraphSerializer.DeserializeConnection("{\"from_node_id\":\"a\",\"unknown\":1}"));
        Assert.Throws<JsonException>(() => GraphSerializer.DeserializeConnection("{/* comment */\"from_node_id\":\"a\"}"));
        Assert.Throws<JsonException>(() => GraphSerializer.DeserializeConnection("{\"from_node_id\":\"a\",}"));
        Assert.Throws<JsonException>(() => GraphSerializer.Deserialize("{\"nodes\":[{\"id\":\"a\",\"unknown\":1}],\"connections\":[]}"));
    }

    [TestMethod]
    public void NodePropertiesRoundTripInCanonicalOrderIncludingNestedJson()
    {
        using var document = JsonDocument.Parse("{\"nested\":[1,{\"enabled\":true}],\"nothing\":null}");
        var node = new GraphNode("node", "start", "Node", [], new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["scalar"] = JsonSerializer.SerializeToElement(7),
            ["nested"] = document.RootElement.GetProperty("nested"),
            ["nothing"] = document.RootElement.GetProperty("nothing"),
        });
        var graph = new GraphDocument([node]);

        var json = graph.ToJson();
        StringAssert.Contains(json, "\"ports\":[],\"properties\":");
        var restored = GraphDocument.FromJson(json).Nodes.Single();
        Assert.AreEqual(JsonValueKind.Number, restored.Properties["scalar"].ValueKind);
        Assert.IsTrue(JsonElement.DeepEquals(node.Properties["nested"], restored.Properties["nested"]));
        Assert.AreEqual(JsonValueKind.Null, restored.Properties["nothing"].ValueKind);
    }

    [TestMethod]
    public void NodePropertySerializationIsIndependentOfInputDictionaryInsertionOrder()
    {
        var first = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["zeta"] = JsonSerializer.SerializeToElement(1),
            ["alpha"] = JsonSerializer.SerializeToElement(2),
        };
        var second = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["alpha"] = JsonSerializer.SerializeToElement(2),
            ["zeta"] = JsonSerializer.SerializeToElement(1),
        };

        var firstJson = new GraphDocument([new GraphNode("node", "start", "Node", [], first)]).ToJson();
        var secondJson = new GraphDocument([new GraphNode("node", "start", "Node", [], second)]).ToJson();

        Assert.AreEqual(firstJson, secondJson);
        Assert.IsLessThan(firstJson.IndexOf("\"zeta\"", StringComparison.Ordinal),
            firstJson.IndexOf("\"alpha\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RenamingAndReorderingPortsDoesNotChangeConnectionValidity()
    {
        var source = new GraphNode("source", "Source", [
            new("stable_out", "Before", false, GraphInterfaceKind.Flow, 0),
            new("other", "Other", false, GraphInterfaceKind.Flow, 1)]);
        var target = new GraphNode("target", "Target", [new("stable_in", "Input", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([source, target], [new("source", "stable_out", "target", "stable_in", GraphInterfaceKind.Flow)]);
        Assert.IsEmpty(GraphValidator.Validate(graph));

        source.Ports[0].DisplayName = "Renamed";
        source.Ports[0].Order = 20;
        source.Ports.Reverse();
        Assert.IsEmpty(GraphValidator.Validate(graph));
    }

    [TestMethod]
    public void ValidatorRejectsStructuralViolationsAndLogicCycles()
    {
        var output = new GraphPort("out", "Out", false, GraphInterfaceKind.Logic);
        var input = new GraphPort("in", "In", true, GraphInterfaceKind.Logic);
        var graph = new GraphDocument([
            new("a", "A", [output, input]),
            new("b", "B", [new("in", "In", true, GraphInterfaceKind.Logic), new("out", "Out", false, GraphInterfaceKind.Logic)])], [
            new("a", "out", "b", "in", GraphInterfaceKind.Logic),
            new("b", "out", "a", "in", GraphInterfaceKind.Logic),
            new("a", "out", "b", "in", GraphInterfaceKind.Logic),
            new("missing", "out", "b", "in", GraphInterfaceKind.Logic),
        ]);
        var codes = GraphValidator.Validate(graph).Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "graph.connection.duplicate");
        CollectionAssert.Contains(codes, "graph.connection.from.node.missing");
        CollectionAssert.Contains(codes, "graph.connection.logic.input.multiple_sources");
        CollectionAssert.Contains(codes, "graph.logic.cycle");
    }

    [TestMethod]
    public void ValidatorRejectsSourceInput()
    {
        var graph = Pair(GraphInterfaceKind.Flow, sourceInput: true, targetInput: true);
        AssertCode(graph, "graph.connection.source.direction");
    }

    [TestMethod]
    public void ValidatorRejectsTargetOutput()
    {
        var graph = Pair(GraphInterfaceKind.Flow, sourceInput: false, targetInput: false);
        AssertCode(graph, "graph.connection.target.direction");
    }

    [TestMethod]
    public void ValidatorRejectsMixedFlowAndLogicPorts()
    {
        var source = new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var target = new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Logic)]);
        AssertCode(new GraphDocument([source, target], [new("a", "out", "b", "in", GraphInterfaceKind.Flow)]), "graph.connection.kind.mixed");
    }

    [TestMethod]
    public void ValidatorRejectsSecondFlowOutputTarget()
    {
        var source = new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var b = new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var c = new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([source, b, c], [new("a", "out", "b", "in", GraphInterfaceKind.Flow), new("a", "out", "c", "in", GraphInterfaceKind.Flow)]);
        AssertCode(graph, "graph.connection.flow.output.multiple_targets");
    }

    [TestMethod]
    public void ValidatorRejectsSecondLogicInputSource()
    {
        var a = new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Logic)]);
        var b = new GraphNode("b", "B", [new("out", "Out", false, GraphInterfaceKind.Logic)]);
        var c = new GraphNode("c", "C", [new("in", "In", true, GraphInterfaceKind.Logic)]);
        var graph = new GraphDocument([a, b, c], [new("a", "out", "c", "in", GraphInterfaceKind.Logic), new("b", "out", "c", "in", GraphInterfaceKind.Logic)]);
        AssertCode(graph, "graph.connection.logic.input.multiple_sources");
    }

    [TestMethod]
    public void CardinalityAllowsFlowInputAndLogicOutputFanInFanOut()
    {
        var graph = new GraphDocument([
            new("a", "A", [new("out", "Out", false, GraphInterfaceKind.Logic), new("flow", "Flow", true, GraphInterfaceKind.Flow)]),
            new("b", "B", [new("in", "In", true, GraphInterfaceKind.Logic), new("flow", "Flow", true, GraphInterfaceKind.Flow)]),
            new("c", "C", [new("in", "In", true, GraphInterfaceKind.Logic), new("flow", "Flow", false, GraphInterfaceKind.Flow)]),
            new("d", "D", [new("flow", "Flow", false, GraphInterfaceKind.Flow)]),
        ], [
            new("a", "out", "b", "in", GraphInterfaceKind.Logic),
            new("a", "out", "c", "in", GraphInterfaceKind.Logic),
            new("c", "flow", "a", "flow", GraphInterfaceKind.Flow),
            new("d", "flow", "b", "flow", GraphInterfaceKind.Flow),
        ]);
        Assert.IsEmpty(GraphValidator.Validate(graph));
    }

    [TestMethod]
    public void ValidatorAllowsMultipleSourcesIntoFlowInput()
    {
        var target = new GraphNode("target", "Target", [new("in", "In", true, GraphInterfaceKind.Flow)]);
        var a = new GraphNode("a", "A", [new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var b = new GraphNode("b", "B", [new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([target, a, b], [new("a", "out", "target", "in", GraphInterfaceKind.Flow), new("b", "out", "target", "in", GraphInterfaceKind.Flow)]);
        Assert.IsEmpty(GraphValidator.Validate(graph));
    }

    [TestMethod]
    public void ValidatorAllowsMultipleConsumersFromLogicOutput()
    {
        var source = new GraphNode("source", "Source", [new("out", "Out", false, GraphInterfaceKind.Logic)]);
        var a = new GraphNode("a", "A", [new("in", "In", true, GraphInterfaceKind.Logic)]);
        var b = new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Logic)]);
        var graph = new GraphDocument([source, a, b], [new("source", "out", "a", "in", GraphInterfaceKind.Logic), new("source", "out", "b", "in", GraphInterfaceKind.Logic)]);
        Assert.IsEmpty(GraphValidator.Validate(graph));
    }

    [TestMethod]
    public void ValidatorAllowsExplicitFlowCycle()
    {
        var a = new GraphNode("a", "A", [new("in", "In", true, GraphInterfaceKind.Flow), new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var b = new GraphNode("b", "B", [new("in", "In", true, GraphInterfaceKind.Flow), new("out", "Out", false, GraphInterfaceKind.Flow)]);
        var graph = new GraphDocument([a, b], [new("a", "out", "b", "in", GraphInterfaceKind.Flow), new("b", "out", "a", "in", GraphInterfaceKind.Flow)]);
        Assert.IsEmpty(GraphValidator.Validate(graph));
    }

    private static GraphDocument Pair(GraphInterfaceKind kind, bool sourceInput, bool targetInput)
        => new([
            new GraphNode("a", "A", [new GraphPort("out", "Out", sourceInput, kind)]),
            new GraphNode("b", "B", [new GraphPort("in", "In", targetInput, kind)]),
        ], [new GraphConnection("a", "out", "b", "in", kind)]);

    private static void AssertCode(GraphDocument graph, string code)
        => CollectionAssert.Contains(GraphValidator.Validate(graph).Select(issue => issue.Code).ToArray(), code);
}
