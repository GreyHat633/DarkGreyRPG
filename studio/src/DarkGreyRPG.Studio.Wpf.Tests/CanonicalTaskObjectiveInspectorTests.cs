using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
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
        CollectionAssert.AreEquivalent(new[] { "objective_type", "description", "required", "item", "metadata", "prerequisite_enabled" },
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
        Assert.IsFalse(inspector.ObjectiveActorOptions.Any(option => option.IsPlaceholder));
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

    [TestMethod]
    public void ObjectiveTypeSelectionIsOneAtomicRevisionWithLegalPayload()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), [new CanonicalStoryActorItem(new ActorResourceInfo(
                "actor", "角色", "actor.json", []))]);
        var revisions = 0;
        editor.Host.NodesChanged += (_, args) => revisions += args.NodeIds.Contains("objective") ? 1 : 0;

        inspector.SelectedObjectiveType = inspector.ObjectiveTypeOptions.Single(option =>
            option.Value == CanonicalTaskObjectiveSchema.CollectItem);

        Assert.AreEqual(1L, editor.Host.GraphRevision);
        Assert.AreEqual(1, editor.Host.Session.UndoCount);
        Assert.AreEqual(1, revisions);
        var changed = editor.Host.Graph.Nodes.Single().Properties;
        Assert.AreEqual(CanonicalTaskObjectiveSchema.CollectItem,
            changed[CanonicalTaskObjectiveSchema.TypeProperty].GetString());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.UnselectedTarget,
            changed[CanonicalTaskObjectiveSchema.ItemProperty].GetString());
        Assert.AreEqual(10, changed[CanonicalTaskObjectiveSchema.RequiredProperty].GetInt32());
        Assert.IsFalse(changed.ContainsKey(CanonicalTaskObjectiveSchema.EntityProperty));
        // An explicit blank target is the legal authoring state; the session
        // intentionally suppresses only the target validation issue until a
        // resource is selected.
        Assert.IsFalse(editor.Host.LastValidationIssues.Any(issue =>
            issue.Code == "graph.objective.type.invalid"
                || issue.Code == "graph.objective.property.unsupported"));
    }

    [TestMethod]
    public void InspectorPrerequisiteToggleProjectsStablePortAndHelperContract()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        Assert.IsFalse(inspector.ObjectivePrerequisiteEnabled);
        Assert.AreEqual("前置条件为 True 时激活", inspector.ObjectivePrerequisiteHelpText);
        inspector.ObjectivePrerequisiteEnabled = true;

        Assert.IsTrue(inspector.ObjectivePrerequisiteEnabled);
        var persisted = editor.Host.Graph.Nodes.Single();
        Assert.IsTrue(persisted.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty].GetBoolean());
        Assert.AreEqual(CanonicalTaskObjectiveSchema.PrerequisitePortId,
            persisted.Ports.Single(port => port.IsInput).Id);
    }

    [TestMethod]
    public void RepeatedObjectiveActorProjectionIsIdempotentAndDoesNotReenter()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var actors = new[]
        {
            new CanonicalStoryActorItem(new ActorResourceInfo("actor-a", "甲", "a.json", [])),
            new CanonicalStoryActorItem(new ActorResourceInfo("actor-b", "乙", "b.json", [])),
        };
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), actors);
        var nodeChanges = 0;
        var stableOptions = inspector.ObjectiveActorOptions;
        editor.Host.NodesChanged += (_, args) => nodeChanges += args.NodeIds.Contains("objective") ? 1 : 0;

        for (var index = 0; index < 100; index++)
        {
            var id = index % 2 == 0 ? "actor-a" : "actor-b";
            inspector.SelectedObjectiveActor = inspector.ObjectiveActorOptions.Single(option => option.Id == id);
            // Reprojection recreates option instances; feeding the projected
            // selection back must remain a no-op rather than a new mutation.
            editor.Host.Refresh();
            inspector.SelectedObjectiveActor = inspector.ObjectiveActorOptions.Single(option => option.Id == id);
        }

        Assert.AreEqual(100L, editor.Host.GraphRevision);
        Assert.AreEqual(100, editor.Host.Session.UndoCount);
        Assert.AreEqual(100, nodeChanges);
        Assert.AreEqual("actor-b", editor.Host.Graph.Nodes.Single().Properties[
            CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        Assert.AreEqual("actor-b", inspector.SelectedObjectiveActor!.Id);
        Assert.AreSame(stableOptions, inspector.ObjectiveActorOptions);
    }

    [TestMethod]
    public void ObjectiveActorChangeSynchronizesEveryInspectorForTheSameNode()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var actors = new[]
        {
            new CanonicalStoryActorItem(new ActorResourceInfo("actor-a", "甲", "a.json", [])),
            new CanonicalStoryActorItem(new ActorResourceInfo("actor-b", "乙", "b.json", [])),
        };
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inlineInspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), actors);
        using var selectedInspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), actors);
        var inlineOptions = inlineInspector.ObjectiveActorOptions;
        var selectedOptions = selectedInspector.ObjectiveActorOptions;

        selectedInspector.SelectedObjectiveActor = selectedInspector.ObjectiveActorOptions
            .Single(option => option.Id == "actor-b");

        Assert.AreEqual("actor-b", editor.Host.Graph.Nodes.Single().Properties[
            CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        Assert.AreEqual("actor-b", inlineInspector.SelectedObjectiveActor!.Id);
        Assert.AreEqual("actor-b", selectedInspector.SelectedObjectiveActor!.Id);
        Assert.AreSame(inlineOptions, inlineInspector.ObjectiveActorOptions);
        Assert.AreSame(selectedOptions, selectedInspector.ObjectiveActorOptions);
    }

    [TestMethod]
    public void RepeatedObjectiveItemProjectionIsIdempotentAndDoesNotDuplicateRevision()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(objective,
            CanonicalTaskObjectiveSchema.CollectItem, out _));
        var items = new[]
        {
            new CanonicalStoryItemItem(new IndividualItemResource { ItemId = "item-a", DisplayName = "甲" }),
            new CanonicalStoryItemItem(new CollectiveItemResource { GroupId = "item-b", DisplayName = "乙" }),
        };
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), itemItems: items);
        var nodeChanges = 0;
        var stableOptions = inspector.ObjectiveItemOptions;
        editor.Host.NodesChanged += (_, args) => nodeChanges += args.NodeIds.Contains("objective") ? 1 : 0;

        for (var index = 0; index < 100; index++)
        {
            var id = index % 2 == 0 ? "item-a" : "item-b";
            inspector.SelectedObjectiveItem = inspector.ObjectiveItemOptions.Single(option => option.Id == id);
            editor.Host.Refresh();
            inspector.SelectedObjectiveItem = inspector.ObjectiveItemOptions.Single(option => option.Id == id);
        }

        Assert.AreEqual(100L, editor.Host.GraphRevision);
        Assert.AreEqual(100, editor.Host.Session.UndoCount);
        Assert.AreEqual(100, nodeChanges);
        Assert.AreEqual("item-b", editor.Host.Graph.Nodes.Single().Properties[
            CanonicalTaskObjectiveSchema.ItemProperty].GetString());
        Assert.AreEqual("item-b", inspector.ObjectiveTarget);
        Assert.AreSame(stableOptions, inspector.ObjectiveItemOptions);
    }

    [TestMethod]
    public void ObjectiveTargetStatusUsesOnlyUnresolvedLegacyIdentifiers()
    {
        var kill = GraphNodeFactory.Create(GraphScope.Task, "objective", "kill");
        using var killEditor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "kill-task", "Task", new GraphDocument([kill])));
        using var killInspector = new CanonicalNodeInspectorViewModel(
            killEditor.Host, killEditor.Host.Nodes.Single());
        Assert.AreEqual(string.Empty, killInspector.ObjectiveActorStatusText);
        Assert.IsFalse(killInspector.HasObjectiveActorStatus);
        Assert.IsNull(killInspector.SelectedObjectiveActor);
        Assert.IsEmpty(killInspector.ObjectiveActorOptions);

        var collect = GraphNodeFactory.Create(GraphScope.Task, "objective", "collect");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(
            collect, CanonicalTaskObjectiveSchema.CollectItem, out _));
        using var collectEditor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "collect-task", "Task", new GraphDocument([collect])));
        using var collectInspector = new CanonicalNodeInspectorViewModel(
            collectEditor.Host, collectEditor.Host.Nodes.Single());
        Assert.AreEqual(string.Empty, collectInspector.ObjectiveItemStatusText);
        Assert.IsFalse(collectInspector.HasObjectiveItemStatus);
        Assert.IsNull(collectInspector.SelectedObjectiveItem);
        Assert.IsEmpty(collectInspector.ObjectiveItemOptions);

        collectEditor.Host.SetNodeProperty("collect", CanonicalTaskObjectiveSchema.ItemProperty,
            JsonSerializer.SerializeToElement("deleted-item"));
        Assert.AreEqual("物品未解析：deleted-item", collectInspector.ObjectiveItemStatusText);
        Assert.IsTrue(collectInspector.HasObjectiveItemStatus);

        var interact = GraphNodeFactory.Create(GraphScope.Task, "objective", "interact");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(
            interact, CanonicalTaskObjectiveSchema.InteractActor, "legacy-actor", out _));
        interact.Properties[CanonicalTaskObjectiveSchema.ActorIdProperty] =
            JsonSerializer.SerializeToElement(string.Empty);
        using var interactEditor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "interact-task", "Task", new GraphDocument([interact])));
        using var interactInspector = new CanonicalNodeInspectorViewModel(
            interactEditor.Host, interactEditor.Host.Nodes.Single());
        Assert.AreEqual(string.Empty, interactInspector.ObjectiveActorStatusText);
        Assert.IsFalse(interactInspector.HasObjectiveActorStatus);

        interactEditor.Host.SetNodeProperty("interact", CanonicalTaskObjectiveSchema.ActorIdProperty,
            JsonSerializer.SerializeToElement("deleted-actor"));
        Assert.AreEqual("角色未解析：deleted-actor", interactInspector.ObjectiveActorStatusText);
        Assert.IsTrue(interactInspector.HasObjectiveActorStatus);
    }
}
