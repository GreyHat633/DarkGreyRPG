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
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "entity", "prerequisite_enabled" }, node.Properties.Keys.ToArray());
        Assert.IsFalse(node.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty].GetBoolean());
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
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "item", "metadata", "prerequisite_enabled" }, objective.Properties.Keys.ToArray());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.UnselectedTarget, graph.Nodes.Single().Properties["item"].GetString());
        Assert.AreEqual(0, graph.Nodes.Single().Properties["metadata"].EnumerateObject().Count());
        Assert.IsTrue(session.Undo());
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "entity", "prerequisite_enabled" }, graph.Nodes.Single().Properties.Keys.ToArray());
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
    public void InteractHasNoRequiredFieldAndRejectsRequiredEdits()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsTrue(session.ChangeObjectiveType("objective", CanonicalTaskObjectiveSchema.InteractActor, "actor-1"));
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "actor_id", "prerequisite_enabled" },
            objective.Properties.Keys.ToArray());
        Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(objective));
        Assert.IsFalse(session.SetObjectiveRequired("objective", 2));
        Assert.IsFalse(objective.Properties.ContainsKey(CanonicalTaskObjectiveSchema.RequiredProperty));
        Assert.AreEqual("graph.objective.interact.required.unsupported", session.LastValidationIssues.Single().Code);
    }

    [TestMethod]
    public void LegacyInteractRequiredOneNormalizesButHigherCountRequestsMigration()
    {
        var safe = GraphNodeFactory.Create(GraphScope.Task, "objective", "safe");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(safe,
            CanonicalTaskObjectiveSchema.InteractActor, "actor-1", out _));
        safe.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] = JsonSerializer.SerializeToElement(1);
        Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(safe));
        var graph = new GraphDocument([safe]);
        Assert.AreEqual(1, CanonicalTaskObjectiveSchema.NormalizeLegacyInteractRequired(graph));
        Assert.IsFalse(safe.Properties.ContainsKey(CanonicalTaskObjectiveSchema.RequiredProperty));

        var ambiguous = GraphNodeFactory.Create(GraphScope.Task, "objective", "ambiguous");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(ambiguous,
            CanonicalTaskObjectiveSchema.InteractActor, "actor-2", out _));
        ambiguous.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] = JsonSerializer.SerializeToElement(3);
        Assert.AreEqual(0, CanonicalTaskObjectiveSchema.NormalizeLegacyInteractRequired(new GraphDocument([ambiguous])));
        CollectionAssert.Contains(CanonicalTaskObjectiveSchema.Validate(ambiguous).Select(issue => issue.Code).ToArray(),
            "graph.objective.interact.required.legacy_count");
    }

    [TestMethod]
    public void TargetChangeRepairsIdentityWhileRetainingUnrelatedLegacyIssue()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(objective,
            CanonicalTaskObjectiveSchema.InteractActor, "actor", out _));
        objective.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] =
            JsonSerializer.SerializeToElement(3);
        var graph = new GraphDocument([objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsTrue(session.ChangeObjectiveTarget("objective", "actor-group"));

        Assert.AreEqual("actor-group", objective.Properties[
            CanonicalTaskObjectiveSchema.ActorIdProperty].GetString());
        Assert.AreEqual(1, session.UndoCount);
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.objective.interact.required.legacy_count");
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("actor", graph.Nodes.Single().Properties[
            CanonicalTaskObjectiveSchema.ActorIdProperty].GetString());
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

    [TestMethod]
    public void PrerequisiteToggleOwnsStablePortCleansWiresAndRoundTripsUndo()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        objective.Properties[CanonicalTaskObjectiveSchema.EntityProperty] = JsonSerializer.SerializeToElement("boss");
        var source = GraphNodeFactory.Create(GraphScope.Task, "logic_input", "source");
        source.Properties["port_id"] = JsonSerializer.SerializeToElement("source_gate");
        source.Properties["display_name"] = JsonSerializer.SerializeToElement("Source Gate");
        var graph = new GraphDocument([source, objective]);
        var session = new GraphEditSession(graph, GraphScope.Task);

        Assert.IsTrue(session.SetObjectivePrerequisiteEnabled("objective", true));
        Assert.IsTrue(objective.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty].GetBoolean());
        var prerequisite = objective.Ports.Single(port => port.IsInput);
        Assert.AreEqual(CanonicalTaskObjectiveSchema.PrerequisitePortId, prerequisite.Id);
        Assert.AreEqual(CanonicalTaskObjectiveSchema.PrerequisiteDisplayName, prerequisite.DisplayName);
        Assert.IsFalse(session.AddDynamicPort("objective", "another", GraphPortDirection.Input,
            GraphInterfaceKind.Logic));

        graph.Connections.Add(new GraphConnection("source", "logic_out", "objective",
            CanonicalTaskObjectiveSchema.PrerequisitePortId, GraphInterfaceKind.Logic));
        var beforeDisable = graph.ToJson();
        Assert.IsTrue(session.SetObjectivePrerequisiteEnabled("objective", false));
        Assert.IsFalse(objective.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty].GetBoolean());
        Assert.IsFalse(objective.Ports.Any(port => port.IsInput));
        Assert.AreEqual(0, graph.Connections.Count);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(beforeDisable, graph.ToJson());
        var restored = GraphDocument.FromJson(graph.ToJson());
        Assert.IsTrue(restored.Nodes.Single(node => node.Id == "objective")
            .Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty].GetBoolean());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.PrerequisitePortId,
            restored.Nodes.Single(node => node.Id == "objective").Ports.Single(port => port.IsInput).Id);
    }

    [TestMethod]
    public void LegacyObjectiveWithoutPrerequisiteFlagRemainsDefaultActiveShape()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "legacy");
        objective.Properties[CanonicalTaskObjectiveSchema.EntityProperty] = JsonSerializer.SerializeToElement("boss");
        objective.Properties.Remove(CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty);

        Assert.IsFalse(CanonicalTaskObjectiveSchema.IsPrerequisiteEnabled(objective));
        Assert.IsTrue(GraphNodeShapeValidator.IsValid(objective, GraphScope.Task));
    }
}
