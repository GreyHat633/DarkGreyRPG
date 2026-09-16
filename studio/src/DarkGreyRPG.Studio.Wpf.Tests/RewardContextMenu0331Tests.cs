using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class RewardContextMenu0331Tests
{
    [STATestMethod]
    public void RewardMenuCreatesSecondEditableNodeAndSupportsUndoSaveReopen()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var original = GraphNodeFactory.Create(GraphScope.Task, "reward", "existing");
        original.Properties["entries"] = JsonSerializer.SerializeToElement(new[] { new { type = "xp", amount = 1 } });
        store.Tasks.Create(new(GraphResourceKind.Task, "task", "任务", new GraphDocument([original])));
        using var editor = new CanonicalGraphResourceEditorViewModel(store.Tasks.Load("task"));
        var view = new CanonicalGraphEditorView { Host = editor.Host };
        var menu = view.CreateCanvasContextMenu(new Point(320, 180));
        var add = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "添加");
        var task = add.Items.OfType<MenuItem>().Single(item => (string)item.Header == "任务");
        var reward = task.Items.OfType<MenuItem>().Single(item => (string)item.Header == "奖励");
        Assert.IsTrue(reward.IsEnabled);
        reward.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.HasCount(2, editor.Host.Nodes);
        var created = editor.Host.Nodes.Single(node => node.NodeId != "existing");
        Assert.AreEqual(320d, created.X);
        Assert.AreEqual(180d, created.Y);
        Assert.IsTrue(editor.Host.Undo());
        Assert.HasCount(1, editor.Host.Nodes);
        Assert.IsTrue(editor.Host.Redo());
        var node = editor.Host.Graph.Nodes.Single(node => node.Id == created.NodeId);
        Assert.IsNotEmpty(CanonicalTaskRewardSchema.Validate(node)); // Unselected item remains invalid for export.
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(n => n.NodeId == created.NodeId));
        var row = inspector.RewardEntries.Single();
        row.SelectedType = row.TypeOptions.Single(option => option.Value == "xp");
        row.AmountText = "12";
        Assert.IsEmpty(CanonicalTaskRewardSchema.Validate(editor.Host.Graph.Nodes.Single(n => n.Id == created.NodeId)));
        new CanonicalGraphResourceSaveCoordinator(store).Replace(editor);
        var reopened = store.Tasks.Load("task").Graph!;
        Assert.HasCount(2, reopened.Nodes);
        Assert.AreEqual(12, reopened.Nodes.Single(n => n.Id == created.NodeId).Properties["entries"][0].GetProperty("amount").GetInt32());
    }

    [TestMethod]
    public void DraftAllowanceDoesNotHideMalformedRewardValues()
    {
        var node = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
        Assert.IsEmpty(CanonicalTaskRewardSchema.AllowDraftIssues(node, CanonicalTaskRewardSchema.Validate(node)));
        node.Properties["entries"] = JsonSerializer.SerializeToElement(new[] { new { type = "item", item = "", amount = "bad" } });
        var issues = CanonicalTaskRewardSchema.AllowDraftIssues(node, CanonicalTaskRewardSchema.Validate(node));
        Assert.HasCount(1, issues);
        Assert.AreEqual("properties.entries[0].amount", issues.Single().Field);
    }
}
