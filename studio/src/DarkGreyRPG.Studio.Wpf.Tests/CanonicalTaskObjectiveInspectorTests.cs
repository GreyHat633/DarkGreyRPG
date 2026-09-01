using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalTaskObjectiveInspectorTests
{
    [TestMethod]
    public void InspectorEditsTypedObjectiveAndUsesAtomicTypeSwitch()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        Assert.IsTrue(inspector.IsObjective);
        inspector.ObjectiveDescription = "Defeat the boss";
        inspector.ObjectiveRequiredText = "3";
        inspector.ObjectiveRequiredText = "not-a-number";
        Assert.AreEqual("not-a-number", inspector.ObjectiveRequiredText);
        StringAssert.Contains(inspector.ObjectiveRequiredError, "不小于 1");
        Assert.AreEqual(3, editor.Host.Graph.Nodes.Single().Properties["required"].GetInt32());
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue =>
            issue.Code == "graph.objective.required.authoring_invalid"));
        inspector.ObjectiveRequiredText = "3";
        Assert.AreEqual(string.Empty, inspector.ObjectiveRequiredError);
        Assert.IsTrue(inspector.IsKillEntityObjective);
        var undoCount = editor.Host.Session.UndoCount;
        inspector.SelectedObjectiveType = inspector.ObjectiveTypeOptions.Single(option => option.Value == "collect_item");
        Assert.AreEqual(undoCount + 1, editor.Host.Session.UndoCount);
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "item", "metadata" },
            editor.Host.Graph.Nodes.Single().Properties.Keys.ToArray());
        Assert.AreEqual("Defeat the boss", editor.Host.Graph.Nodes.Single().Properties["description"].GetString());
        Assert.AreEqual(3, editor.Host.Graph.Nodes.Single().Properties["required"].GetInt32());
    }

    [TestMethod]
    public void InteractActorPickerRetainsUnknownIdAndResolvesKnownChoice()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(objective, "interact_actor", "deleted", out _));
        objective.Properties[CanonicalTaskObjectiveSchema.ActorIdProperty] = JsonSerializer.SerializeToElement("deleted");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), [new CanonicalStoryActorItem(new ActorResourceInfo("known", "已知角色", "known.json", []))]);

        Assert.IsTrue(inspector.IsInteractActorObjective);
        Assert.IsTrue(inspector.IsObjectiveActorUnresolved);
        Assert.AreEqual("deleted", inspector.SelectedObjectiveActor!.Id);
        inspector.SelectedObjectiveActor = inspector.ObjectiveActorOptions.Single(item => item.Id == "known");
        Assert.AreEqual("known", editor.Host.Graph.Nodes.Single().Properties["actor_id"].GetString());
        Assert.IsTrue(inspector.IsObjectiveActorResolved);
    }

    [TestMethod]
    public void LegacyInteractRequiredOneIsRemovedAndMarksEditorDirtyForNextSave()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(objective,
            CanonicalTaskObjectiveSchema.InteractActor, "known", out _));
        objective.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] = JsonSerializer.SerializeToElement(1);

        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));

        Assert.IsTrue(editor.IsDirty);
        Assert.IsFalse(editor.Host.Graph.Nodes.Single().Properties.ContainsKey(
            CanonicalTaskObjectiveSchema.RequiredProperty));
        Assert.IsTrue(editor.CanSave);
    }

    [TestMethod]
    public void InspectorUsesFirstAvailableActorOrFailsAtomically()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), [new CanonicalStoryActorItem(new ActorResourceInfo("known", "已知", "known.json", []))]);
        inspector.SelectedObjectiveType = inspector.ObjectiveTypeOptions.Single(item => item.Value == "interact_actor");
        Assert.AreEqual("interact_actor", inspector.ObjectiveType);
        Assert.AreEqual("known", editor.Host.Graph.Nodes.Single().Properties["actor_id"].GetString());
        Assert.AreEqual(1, editor.Host.Session.UndoCount);

        var noActorObjective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var noActorEditor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task-2", "Task", new GraphDocument([noActorObjective])));
        using var noActorInspector = new CanonicalNodeInspectorViewModel(noActorEditor.Host,
            noActorEditor.Host.Nodes.Single());
        var before = noActorEditor.Host.Graph.ToJson();
        noActorInspector.SelectedObjectiveType = noActorInspector.ObjectiveTypeOptions.Single(item => item.Value == "interact_actor");
        Assert.AreEqual(before, noActorEditor.Host.Graph.ToJson());
        Assert.AreEqual("kill_entity", noActorInspector.ObjectiveType);
        Assert.AreEqual(0, noActorEditor.Host.Session.UndoCount);
    }
}
