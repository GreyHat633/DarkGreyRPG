using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class EmptyResourceInspectorTests
{
    [TestMethod]
    public void StartActorSwitchSynchronizesBothViewsUndoRedoAndReopenWithEmptyPickers()
    {
        using var editor = Editor(GraphScope.StoryFlow, GraphNodeFactory.CreateStoryStart("start", triggerPortId: "stable"));
        using var inline = Inspector(editor);
        using var sidebar = Inspector(editor);
        var row = inline.StoryStartTriggers.Single();
        row.SelectedTriggerType = row.TriggerTypeOptions.Single(option => option.Value == StoryStartSchema.ActorInteraction);
        Assert.IsTrue(row.IsActorInteraction);
        Assert.IsTrue(sidebar.StoryStartTriggers.Single().IsActorInteraction);
        Assert.IsEmpty(row.ActorOptions);
        Assert.IsNull(row.SelectedActor);
        Assert.IsEmpty(editor.ValidationIssues);
        Assert.IsTrue(editor.CanSave);
        Assert.AreEqual("stable", editor.Host.Graph.Nodes.Single().Ports.Single().Id);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(StoryStartSchema.RegionEntry, inline.StoryStartTriggers.Single().TriggerType);
        Assert.AreEqual(StoryStartSchema.RegionEntry, sidebar.StoryStartTriggers.Single().TriggerType);
        Assert.IsTrue(editor.Host.Redo());
        using var reopened = new CanonicalGraphResourceEditorViewModel(editor.CreatePersistenceSnapshot());
        using var restored = Inspector(reopened);
        Assert.IsTrue(restored.StoryStartTriggers.Single().IsActorInteraction);
        Assert.IsNull(restored.StoryStartTriggers.Single().SelectedActor);
    }

    [TestMethod]
    public void EveryObjectiveTypeCanBeSelectedWithNoActorsOrItems()
    {
        using var editor = Editor(GraphScope.Task, GraphNodeFactory.Create(GraphScope.Task, "objective", "objective"));
        using var inline = Inspector(editor);
        using var sidebar = Inspector(editor);
        foreach (var option in inline.ObjectiveTypeOptions)
        {
            inline.SelectedObjectiveType = option;
            Assert.AreEqual(option.Value, inline.ObjectiveType);
            Assert.AreEqual(option.Value, sidebar.ObjectiveType);
            Assert.IsEmpty(inline.ObjectiveActorOptions);
            Assert.IsEmpty(inline.ObjectiveItemOptions);
            Assert.IsNull(sidebar.SelectedObjectiveActor);
            Assert.IsNull(sidebar.SelectedObjectiveItem);
            Assert.IsEmpty(editor.ValidationIssues);
        }
    }

    [TestMethod]
    public void RewardSwitchToItemHasAnEmptyPickerAndRemainsUndoable()
    {
        using var editor = Editor(GraphScope.Task, GraphNodeFactory.Create(GraphScope.Task, "reward", "reward"));
        using var inline = Inspector(editor);
        using var sidebar = Inspector(editor);
        var row = inline.RewardEntries.Single();
        row.SelectedType = row.TypeOptions.Single(type => type.Value == "xp");
        row.SelectedType = row.TypeOptions.Single(type => type.Value == "item");
        Assert.IsTrue(row.IsItem);
        Assert.IsEmpty(row.ItemOptions);
        Assert.IsNull(row.SelectedItem);
        Assert.AreEqual("", row.Error);
        Assert.IsTrue(sidebar.RewardEntries.Single().IsItem);
        Assert.IsNull(sidebar.RewardEntries.Single().SelectedItem);
        Assert.IsEmpty(editor.ValidationIssues);
        Assert.IsTrue(editor.CanSave);
        Assert.IsTrue(editor.Host.Undo());
        Assert.IsFalse(sidebar.RewardEntries.Single().IsItem);
        Assert.IsTrue(editor.Host.Redo());
        using var reopened = new CanonicalGraphResourceEditorViewModel(editor.CreatePersistenceSnapshot());
        using var restored = Inspector(reopened);
        Assert.IsNull(restored.RewardEntries.Single().SelectedItem);
        Assert.IsNotEmpty(CanonicalTaskRewardSchema.Validate(reopened.Host.Graph.Nodes.Single()));
    }

    [TestMethod]
    public void ActionTypesAndDialogueSpeakerRemainEditableWithoutResources()
    {
        using var editor = Editor(GraphScope.StoryFlow, GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action"));
        using var inspector = Inspector(editor);
        foreach (var option in inspector.StoryActionTypeOptions)
        {
            inspector.SelectedStoryActionType = option;
            Assert.AreEqual(option.Value, inspector.StoryActionType);
            Assert.IsEmpty(inspector.StoryActionItemOptions);
            Assert.IsNull(inspector.SelectedStoryActionItem);
            Assert.IsEmpty(editor.ValidationIssues);
        }
        using var dialogue = Editor(GraphScope.Session, GraphNodeFactory.Create(GraphScope.Session, "line", "line"));
        using var line = Inspector(dialogue);
        Assert.IsEmpty(line.SpeakerOptions);
        Assert.IsNull(line.SelectedSpeaker);
        Assert.IsEmpty(dialogue.ValidationIssues);
    }

    private static CanonicalGraphResourceEditorViewModel Editor(GraphScope scope, GraphNode node)
        => new(new GraphResourceEnvelope(scope switch { GraphScope.Task => GraphResourceKind.Task, GraphScope.Session => GraphResourceKind.Session, _ => GraphResourceKind.Story }, CurrentIdentityFixture.GraphId(scope), "Resource", new GraphDocument([node])));
    private static CanonicalNodeInspectorViewModel Inspector(CanonicalGraphResourceEditorViewModel editor)
        => new(editor.Host, editor.Host.Nodes.Single());
}
