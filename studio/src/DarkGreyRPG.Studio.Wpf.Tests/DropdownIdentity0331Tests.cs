using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class DropdownIdentity0331Tests
{
    [TestMethod]
    public void StartRowsSurviveEditsReorderAndUndoWithoutCollectionReset()
    {
        var start = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start");
        StoryStartSchema.InitializeDefault(start, "entry");
        using var editor = Editor(GraphResourceKind.Story, start);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var row = inspector.StoryStartTriggers.Single();
        var options = row.TriggerTypeOptions;
        var resets = 0;
        inspector.StoryStartTriggers.CollectionChanged += (_, e) =>
        { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
        Assert.IsTrue(inspector.AddStoryStartTrigger("第二入口"));
        for (var index = 0; index < 100; index++)
        {
            Assert.IsTrue(inspector.RenameStoryStartTrigger(row.PortId, "入口 " + index));
            Assert.AreSame(row, inspector.StoryStartTriggers.Single(item => item.PortId == row.PortId));
            Assert.AreSame(options, row.TriggerTypeOptions);
            Assert.IsTrue(editor.Host.Undo());
            Assert.IsTrue(editor.Host.Redo());
        }
        Assert.IsTrue(inspector.ReorderStoryStartTrigger(row.PortId, 1));
        Assert.AreSame(row, inspector.StoryStartTriggers[1]);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreSame(row, inspector.StoryStartTriggers[0]);
        Assert.AreEqual(0, resets);
    }

    [TestMethod]
    public void RewardProjectionKeepsRowsAndDraftsAcrossOtherEditsAndUndo()
    {
        var reward = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
        using var editor = Editor(GraphResourceKind.Task, reward);
        using var first = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var second = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var row = second.RewardEntries.Single();
        var options = second.RewardItemOptions;
        var resets = 0;
        second.RewardEntries.CollectionChanged += (_, e) =>
        { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
        for (var index = 2; index <= 101; index++)
        {
            first.RewardEntries[0].AmountText = index.ToString();
            Assert.AreSame(row, second.RewardEntries[0]);
            Assert.AreEqual(index.ToString(), row.AmountText);
            Assert.IsTrue(editor.Host.Undo());
            Assert.IsTrue(editor.Host.Redo());
            Assert.AreSame(row, second.RewardEntries[0]);
        }
        row.AmountText = "-";
        first.AddRewardEntryCommand.Execute(null);
        Assert.AreSame(row, second.RewardEntries[0]);
        Assert.AreEqual("-", row.AmountText);
        Assert.IsFalse(string.IsNullOrEmpty(row.Error));
        first.RewardEntries[1].RemoveCommand.Execute(null);
        Assert.AreSame(row, second.RewardEntries[0]);
        Assert.AreSame(options, second.RewardItemOptions);
        Assert.AreEqual(0, resets);
    }

    [TestMethod]
    public void ActionOptionItemsRemainStableWhileParametersChange()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action");
        using var editor = Editor(GraphResourceKind.Story, action);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var options = inspector.StoryActionTypeOptions;
        inspector.SelectedStoryActionType = options.Single(item => item.Value == CanonicalStoryActionSchema.GiveHealth);
        for (var index = 1; index <= 100; index++)
        {
            inspector.HealthDelta = index.ToString();
            Assert.AreSame(options, inspector.StoryActionTypeOptions);
            Assert.AreSame(options.Single(item => item.Value == CanonicalStoryActionSchema.GiveHealth), inspector.SelectedStoryActionType);
        }
    }

    private static CanonicalGraphResourceEditorViewModel Editor(GraphResourceKind kind, GraphNode node)
        => new(new GraphResourceEnvelope(kind, "resource", "Resource", new GraphDocument([node])));
}
