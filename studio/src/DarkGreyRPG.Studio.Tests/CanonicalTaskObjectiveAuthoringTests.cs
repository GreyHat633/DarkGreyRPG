using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalTaskObjectiveAuthoringTests
{
    [TestMethod]
    public void FactoryCreatesUnselectedKillContract()
    {
        var node = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "entity" }, node.Properties.Keys.ToArray());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.UnselectedTarget, node.Properties["entity"].GetString());
        Assert.IsTrue(CanonicalTaskObjectiveSchema.IsUnselectedTarget(node));
        Assert.IsFalse(CanonicalTaskObjectiveSchema.IsValid(node));
        CollectionAssert.Contains(GraphNodeShapeValidator.Validate(node, GraphScope.Task)
            .Select(issue => issue.Code).ToArray(), "graph.objective.target.invalid");
    }

    [TestMethod]
    public void TypeSwitchReplacesPayloadAndUndoRestoresItAsOneEdit()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        var undoCount = session.UndoCount;

        Assert.IsTrue(session.ChangeObjectiveType("objective", "collect_item"));
        Assert.AreEqual(undoCount + 1, session.UndoCount);
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "item", "metadata" }, objective.Properties.Keys.ToArray());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.UnselectedTarget, graph.Nodes.Single().Properties["item"].GetString());
        Assert.AreEqual(0, graph.Nodes.Single().Properties["metadata"].EnumerateObject().Count());
        Assert.IsTrue(session.Undo());
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "entity" }, graph.Nodes.Single().Properties.Keys.ToArray());
    }

    [TestMethod]
    public void InvalidTypedMutationsAreRejectedWithoutPartialChanges()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        var before = graph.ToJson();

        Assert.IsFalse(session.SetNodeProperty("objective", "entity", JsonSerializer.SerializeToElement(" ")));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.SetNodeProperty("objective", "unsupported", JsonSerializer.SerializeToElement("x")));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.ChangeObjectiveType("objective", "reach_location"));
        Assert.AreEqual(before, graph.ToJson());
    }

    [TestMethod]
    public void CollectMetadataRequiresObjectWithStringValues()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsTrue(session.ChangeObjectiveType("objective", "collect_item"));
        var before = graph.ToJson();

        Assert.IsFalse(session.SetNodeProperty("objective", "metadata", JsonSerializer.SerializeToElement(new { grade = 1 })));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.SetNodeProperty("objective", "metadata", JsonSerializer.SerializeToElement("metadata")));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.SetNodeProperty("objective", "item", JsonSerializer.SerializeToElement(" ")));
        Assert.AreEqual(before, graph.ToJson());
    }

    [TestMethod]
    public void EveryInvalidBoundaryIsRejectedWithoutMutation()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        foreach (var (property, value) in new[]
        {
            ("description", JsonSerializer.SerializeToElement(" ")),
            ("required", JsonSerializer.SerializeToElement(0)),
            ("required", JsonSerializer.SerializeToElement(-1)),
            ("required", JsonSerializer.SerializeToElement(1.5)),
            ("required", JsonSerializer.SerializeToElement("one")),
            ("entity", JsonSerializer.SerializeToElement(" ")),
            ("objective_type", JsonSerializer.SerializeToElement("collect_item")),
        })
        {
            var before = graph.ToJson();
            Assert.IsFalse(session.SetNodeProperty("objective", property, value), property);
            Assert.AreEqual(before, graph.ToJson(), property);
        }
        Assert.IsFalse(session.ChangeObjectiveType("objective", "interact_actor"));
        Assert.AreEqual("kill_entity", graph.Nodes.Single().Properties["objective_type"].GetString());
    }

    [TestMethod]
    public void InteractRequiresExplicitActorAndSameTypeIsNoOp()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsTrue(session.ChangeObjectiveType("objective", "interact_actor", "actor-1"));
        Assert.AreEqual(1, session.UndoCount);
        Assert.AreEqual("actor-1", graph.Nodes.Single().Properties["actor_id"].GetString());
        graph.Nodes.Single().Properties["actor_id"] = JsonSerializer.SerializeToElement("custom");
        var before = graph.ToJson();
        var undoCount = session.UndoCount;
        Assert.IsTrue(session.ChangeObjectiveType("objective", "interact_actor"));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(undoCount, session.UndoCount);
    }

    [TestMethod]
    public void AddAllowsUnselectedObjectiveUntilDgrTargetIsSelected()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument();
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsTrue(session.AddNode(objective));
        Assert.AreEqual(CanonicalTaskObjectiveSchema.UnselectedTarget,
            graph.Nodes.Single().Properties[CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        CollectionAssert.Contains(GraphNodeShapeValidator.Validate(graph, GraphScope.Task)
            .Select(issue => issue.Code).ToArray(), "graph.objective.target.invalid");

        Assert.IsTrue(session.SetNodeProperty("objective", CanonicalTaskObjectiveSchema.EntityProperty, "tavern_boss"));
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(graph, GraphScope.Task));
    }

    [TestMethod]
    public void LegacyMinecraftTargetsRemainValidAndDgrIdsRoundTripThroughJson()
    {
        var kill = GraphNodeFactory.Create(GraphScope.Task, "objective", "kill");
        kill.Properties[CanonicalTaskObjectiveSchema.EntityProperty] = JsonSerializer.SerializeToElement("minecraft:zombie");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(kill));

        var authoredKill = GraphNodeFactory.Create(GraphScope.Task, "objective", "authored_kill");
        var collect = GraphNodeFactory.Create(GraphScope.Task, "objective", "collect");
        var collectGraph = new GraphDocument([authoredKill, collect]);
        var session = new GraphEditSession(collectGraph, GraphScope.Task);
        Assert.IsTrue(session.SetNodeProperty("authored_kill", CanonicalTaskObjectiveSchema.EntityProperty, "tavern_boss"));
        Assert.IsTrue(session.ChangeObjectiveType("collect", CanonicalTaskObjectiveSchema.CollectItem));
        Assert.IsTrue(session.SetNodeProperty("collect", CanonicalTaskObjectiveSchema.ItemProperty, "herb_bundle"));
        var restored = GraphDocument.FromJson(collectGraph.ToJson());

        Assert.AreEqual("tavern_boss", restored.Nodes.Single(node => node.Id == "authored_kill")
            .Properties[CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        Assert.AreEqual("herb_bundle", restored.Nodes.Single(node => node.Id == "collect")
            .Properties[CanonicalTaskObjectiveSchema.ItemProperty].GetString());
        Assert.AreEqual("collect_item", restored.Nodes.Single(node => node.Id == "collect")
            .Properties[CanonicalTaskObjectiveSchema.TypeProperty].GetString());
        Assert.IsFalse(collectGraph.ToJson().Contains("minecraft:slime", StringComparison.Ordinal));
        Assert.IsFalse(collectGraph.ToJson().Contains("minecraft:stone", StringComparison.Ordinal));
    }
}
