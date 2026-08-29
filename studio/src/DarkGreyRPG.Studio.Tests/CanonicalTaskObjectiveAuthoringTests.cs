using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalTaskObjectiveAuthoringTests
{
    [TestMethod]
    public void FactoryCreatesCompleteKillContract()
    {
        var node = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "entity" }, node.Properties.Keys.ToArray());
        Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(node));
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
        Assert.AreEqual("minecraft:stone", graph.Nodes.Single().Properties["item"].GetString());
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
}
