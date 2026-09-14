using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalTaskRewardInspectorTests
{
    [TestMethod]
    public void PackageEditsAreAtomicUndoableAndInvalidAmountsRemainDrafts()
    {
        var reward = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument([reward])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), itemItems:
            [new CanonicalStoryItemItem(new IndividualItemResource { ItemId = "Author:apple", DisplayName = "苹果" }),
             new CanonicalStoryItemItem(new CollectiveItemResource { GroupId = "Author:food", DisplayName = "食物组" })]);
        Assert.IsTrue(inspector.IsTaskReward);
        Assert.AreEqual(1, inspector.RewardItemOptions.Count);
        Assert.HasCount(1, inspector.RewardEntries);
        var row = inspector.RewardEntries.Single();
        Assert.AreEqual("item", row.SelectedType.Value);
        Assert.AreEqual("1", row.AmountText);
        var before = editor.Host.Session.UndoCount;
        row.SelectedType = row.TypeOptions.Single(type => type.Value == "xp");
        Assert.AreEqual(before + 1, editor.Host.Session.UndoCount);
        row.AmountText = "-5";
        Assert.AreEqual(-5, editor.Host.Graph.Nodes.Single().Properties["entries"][0].GetProperty("amount").GetInt32());
        row.AmountText = "invalid";
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue => issue.Code == "graph.reward.authoring"));
        Assert.AreEqual(-5, editor.Host.Graph.Nodes.Single().Properties["entries"][0].GetProperty("amount").GetInt32());
        row.AmountText = "0";
        Assert.AreEqual("", row.Error);
        inspector.AddRewardEntryCommand.Execute(null);
        Assert.AreEqual(2, inspector.RewardEntries.Count);
        inspector.RewardEntries.Last().AmountText = "250";
        inspector.RewardEntries.First().RemoveCommand.Execute(null);
        Assert.AreEqual(1, inspector.RewardEntries.Count);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(2, inspector.RewardEntries.Count);
        var json = GraphSerializer.Serialize(editor.Host.Graph);
        Assert.AreEqual(2, GraphSerializer.Deserialize(json).Nodes.Single().Properties["entries"].GetArrayLength());
    }
}
