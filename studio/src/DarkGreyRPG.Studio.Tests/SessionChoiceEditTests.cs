using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class SessionChoiceEditTests
{
    [TestMethod]
    public void AddRenameAndReorderKeepOptionsAndBothOutputKindsSynchronized()
    {
        var ids = new Queue<string>(["option_2", "flow_2"]);
        var choice = Choice();
        var flowTarget = Line("flow_target");
        var logicTarget = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic_target");
        var graph = new GraphDocument([choice, flowTarget, logicTarget]);
        var session = new GraphEditSession(graph, GraphScope.Session, dynamicPortIdSource: ids.Dequeue);

        Assert.IsTrue(session.AddSessionChoiceOption("choice", "Second"));
        AssertOptionShape(choice,
            ("option_1", "选项 1", "flow_1"),
            ("option_2", "Second", "flow_2"));
        Assert.AreEqual(1, session.UndoCount);
        graph.Connections.Add(new("choice", "flow_2", "flow_target", "flow_in", GraphInterfaceKind.Flow));
        graph.Connections.Add(new("choice", "option_2", "logic_target", "logic_in", GraphInterfaceKind.Logic));

        Assert.IsTrue(session.RenameSessionChoiceOption("choice", "option_2", "Renamed"));
        AssertOptionShape(choice,
            ("option_1", "选项 1", "flow_1"),
            ("option_2", "Renamed", "flow_2"));

        Assert.IsTrue(session.ReorderSessionChoiceOption("choice", "option_2", 0));
        AssertOptionShape(choice,
            ("option_2", "Renamed", "flow_2"),
            ("option_1", "选项 1", "flow_1"));
        CollectionAssert.AreEquivalent(new[] { "flow_2", "option_2" },
            graph.Connections.Select(connection => connection.FromPortId).ToArray());
        Assert.IsTrue(session.Undo());
        AssertOptionShape(graph.Nodes.Single(node => node.Id == "choice"),
            ("option_1", "选项 1", "flow_1"),
            ("option_2", "Renamed", "flow_2"));
    }

    [TestMethod]
    public void RemoveRequiresConfirmationForFlowAndLogicReferencesAndIsAtomic()
    {
        var ids = new Queue<string>(["option_2", "flow_2"]);
        var choice = Choice();
        var flowTarget = Line("flow_target");
        var logicTarget = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic_target");
        var graph = new GraphDocument([choice, flowTarget, logicTarget]);
        var session = new GraphEditSession(graph, GraphScope.Session, dynamicPortIdSource: ids.Dequeue);
        Assert.IsTrue(session.AddSessionChoiceOption("choice", "Second"));
        graph.Connections.Add(new("choice", "flow_2", "flow_target", "flow_in", GraphInterfaceKind.Flow));
        graph.Connections.Add(new("choice", "option_2", "logic_target", "logic_in", GraphInterfaceKind.Logic));
        var before = graph.ToJson();
        var undoBefore = session.UndoCount;

        Assert.IsFalse(session.RemoveSessionChoiceOption("choice", "option_2"));
        Assert.AreEqual("graph.session.choice.references.confirmation_required",
            session.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(undoBefore, session.UndoCount);

        Assert.IsTrue(session.RemoveSessionChoiceOption("choice", "option_2", true));
        Assert.IsEmpty(graph.Connections);
        AssertOptionShape(choice, ("option_1", "选项 1", "flow_1"));
        Assert.IsFalse(session.RemoveSessionChoiceOption("choice", "option_1", true));
        Assert.AreEqual("graph.session.choice.options.minimum", session.LastValidationIssues.Single().Code);
    }

    [TestMethod]
    public void InvalidScopeNodeAndMalformedChoiceFailWithoutMutationOrHistory()
    {
        var choice = Choice();
        var wrongScopeGraph = new GraphDocument([choice]);
        var wrongScope = new GraphEditSession(wrongScopeGraph, GraphScope.StoryFlow);
        var before = wrongScopeGraph.ToJson();
        Assert.IsFalse(wrongScope.AddSessionChoiceOption("choice", "Second"));
        Assert.AreEqual("graph.session.choice.scope.required", wrongScope.LastValidationIssues.Single().Code);
        Assert.AreEqual(before, wrongScopeGraph.ToJson());

        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var wrongNode = new GraphEditSession(new GraphDocument([line]), GraphScope.Session);
        Assert.IsFalse(wrongNode.AddSessionChoiceOption("line", "Second"));
        Assert.AreEqual("graph.session.choice.node.required", wrongNode.LastValidationIssues.Single().Code);

        choice.Ports.RemoveAll(port => port.Id == "option_1");
        var malformedGraph = new GraphDocument([choice]);
        var malformed = new GraphEditSession(malformedGraph, GraphScope.Session);
        before = malformedGraph.ToJson();
        Assert.IsFalse(malformed.RenameSessionChoiceOption("choice", "option_1", "Changed"));
        Assert.AreEqual(before, malformedGraph.ToJson());
        Assert.AreEqual(0, malformed.UndoCount);
        CollectionAssert.Contains(malformed.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.node.shape.dynamic_port.minimum");
    }

    private static GraphNode Choice()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(node, "option_1", "flow_1");
        return node;
    }

    private static GraphNode Line(string id)
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "line", id);
        node.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("actor");
        node.Properties["text"] = JsonSerializer.SerializeToElement("text");
        return node;
    }

    private static void AssertOptionShape(GraphNode node,
        params (string OptionId, string DisplayText, string FlowPortId)[] expected)
    {
        var options = node.Properties["options"].EnumerateArray().Select(element => (
            element.GetProperty("option_id").GetString()!,
            element.GetProperty("display_text").GetString()!,
            element.GetProperty("flow_port_id").GetString()!)).ToArray();
        CollectionAssert.AreEqual(expected, options);
        for (var index = 0; index < expected.Length; index++)
        {
            var option = expected[index];
            var flow = node.Ports.Single(port => port.Id == option.FlowPortId);
            var logic = node.Ports.Single(port => port.Id == option.OptionId);
            Assert.AreEqual(GraphInterfaceKind.Flow, flow.InterfaceKind);
            Assert.AreEqual(GraphInterfaceKind.Logic, logic.InterfaceKind);
            Assert.AreEqual(option.DisplayText, flow.DisplayName);
            Assert.AreEqual($"已选择：{option.DisplayText}", logic.DisplayName);
            Assert.AreEqual(index, flow.Order);
            Assert.AreEqual(index, logic.Order);
        }
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(node, GraphScope.Session));
    }
}
