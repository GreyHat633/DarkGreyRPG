using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ResourceSelectorDropTests
{
    [STATestMethod]
    public void AllResourceFieldsAcceptExactIdsInInlineAndInspectorAndUndo()
    {
        foreach (var (scope, type, subtype) in new[] {
            (GraphScope.StoryFlow, "start", ""),
            (GraphScope.StoryFlow, "action", CanonicalStoryActionSchema.GiveItem),
            (GraphScope.Session, "line", ""),
            (GraphScope.Task, "reward", ""),
            (GraphScope.Task, "objective", CanonicalTaskObjectiveSchema.KillEntity),
            (GraphScope.Task, "objective", CanonicalTaskObjectiveSchema.InteractActor),
            (GraphScope.Task, "objective", CanonicalTaskObjectiveSchema.CollectItem),
            (GraphScope.Task, "objective", CanonicalTaskObjectiveSchema.SubmitItem) })
        {
            var node = GraphNodeFactory.Create(scope, type, "node");
            if (type == "start") StoryStartSchema.InitializeDefault(node, "entry", StoryStartSchema.ActorInteraction, "actor_a");
            if (type == "action") Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(node, subtype, out _));
            if (type == "objective") Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(node, subtype, "actor_a", out _));
            using var workspace = new CanonicalStoryWorkspaceViewModel(
                new(GraphResourceKind.Story, "story", "Story", new GraphDocument(scope == GraphScope.StoryFlow ? [node] : [])),
                actors: [new ActorResourceInfo("actor_a", "同名角色", "a.json", []), new ActorResourceInfo("actor_b", "同名角色", "b.json", [])],
                sessions: scope == GraphScope.Session ? [new(GraphResourceKind.Session, "session", "Session", new GraphDocument([node]))] : [],
                tasks: scope == GraphScope.Task ? [new(GraphResourceKind.Task, "task", "Task", new GraphDocument([node]))] : [],
                items: [new IndividualItemResource { ItemId = "item_a", DisplayName = "同名物品" }, new IndividualItemResource { ItemId = "item_b", DisplayName = "同名物品" }]);
            if (scope == GraphScope.Session) workspace.OpenGraphResource(workspace.SessionItems.Single());
            if (scope == GraphScope.Task) workspace.OpenGraphResource(workspace.TaskItems.Single());
            var full = new CanonicalStoryWorkspaceView(workspace) { Width = 1500, Height = 1000 };
            Layout(full);
            Assert.IsTrue(full.GraphView.SelectNode("node"));
            var inspector = workspace.NodeInspector!;
            if (type == "start")
            {
                Assert.IsTrue(inspector.AddStoryStartTrigger("第二条件"));
                Assert.IsTrue(inspector.SetStoryStartTriggerType(inspector.StoryStartTriggers[1].PortId, StoryStartSchema.ActorInteraction));
            }
            if (type == "reward")
            {
                inspector.RewardEntries[0].SelectedItem = inspector.RewardItemOptions.First();
                inspector.AddRewardEntryCommand.Execute(null);
                var second = inspector.RewardEntries[1];
                second.SelectedType = second.TypeOptions.Single(x => x.Value == "item");
            }
            var inline = new CanonicalInlineNodeEditorControl { Editor = inspector, Width = 280 };
            foreach (var surface in new FrameworkElement[] { inline, full })
            {
                Layout(surface);
                var selectors = Descendants(surface).OfType<ComboBox>()
                    .Where(c => Visible(c) && c.Items.OfType<object>().Any(o => o is CanonicalResourceSelectionOption or CanonicalSessionSpeakerOption)).ToArray();
                Assert.IsTrue(selectors.Length > 0, $"{type}/{subtype}/{surface.GetType().Name}: no rendered resource selectors");
                foreach (var selector in selectors)
                {
                    Assert.IsTrue(selector.AllowDrop, $"{type}/{subtype}: selector must enable native drops");
                    var isActor = selector.Items.OfType<CanonicalSessionSpeakerOption>().Any();
                    ICanonicalStoryTreeItem resource = isActor ? workspace.ActorItems.Single(a => a.Id == "actor_b") : workspace.ItemItems.Single(i => i.Id == "item_b");
                    var before = workspace.ActiveGraphHost.Graph.ToJson();
                    var moduleProperty = type == "reward" ? "entries" : type == "start" ? "triggers" : null;
                    var beforeModules = moduleProperty is null ? [] : workspace.ActiveGraphHost.Graph.Nodes.Single().Properties[moduleProperty].EnumerateArray().Select(e => e.GetRawText()).ToArray();
                    var beforeUndo = workspace.ActiveGraphHost.Session.UndoCount;
                    var binding = BindingOperations.GetBindingExpression(selector, ComboBox.SelectedItemProperty)
                        ?? BindingOperations.GetBindingExpression(selector, ComboBox.SelectedValueProperty);
                    var data = new DataObject(CanonicalStoryWorkspaceView.ResourceDragFormat, resource);
                    var dropped = Drop(selector, data);
                    Assert.IsTrue(dropped.Handled, $"{type}/{subtype}: drop bubbled past selector");
                    Assert.AreEqual(DragDropEffects.Link, dropped.Effects, $"{type}/{subtype}: rejected valid drop");
                    Assert.AreEqual(resource.Id, selector.SelectedItem switch { CanonicalSessionSpeakerOption a => a.Id, CanonicalResourceSelectionOption i => i.Id, _ => null });
                    Assert.IsNotNull(binding);
                    Assert.AreSame(binding, BindingOperations.GetBindingExpression(selector, ComboBox.SelectedItemProperty) ?? BindingOperations.GetBindingExpression(selector, ComboBox.SelectedValueProperty));
                    Assert.AreNotEqual(before, workspace.ActiveGraphHost.Graph.ToJson());
                    if (moduleProperty is not null)
                    {
                        var afterModules = workspace.ActiveGraphHost.Graph.Nodes.Single().Properties[moduleProperty].EnumerateArray().Select(e => e.GetRawText()).ToArray();
                        Assert.AreEqual(1, beforeModules.Zip(afterModules).Count(pair => pair.First != pair.Second), "Only the targeted module may change");
                    }
                    Assert.AreEqual(beforeUndo + 1, workspace.ActiveGraphHost.Session.UndoCount);
                    var after = workspace.ActiveGraphHost.Graph.ToJson();
                    Assert.AreEqual(after, GraphSerializer.Deserialize(GraphSerializer.Serialize(workspace.ActiveGraphHost.Graph)).ToJson());
                    Assert.IsTrue(workspace.ActiveGraphHost.Undo());
                    Assert.AreEqual(before, workspace.ActiveGraphHost.Graph.ToJson());
                    Assert.IsTrue(workspace.ActiveGraphHost.Redo());
                    Assert.AreEqual(after, workspace.ActiveGraphHost.Graph.ToJson());
                    Assert.IsTrue(workspace.ActiveGraphHost.Undo());
                }
            }
        }
    }

    [STATestMethod]
    public void InvalidResourceDropsAreConsumedWithoutChangingSelection()
    {
        var selector = new ComboBox { ItemsSource = new[] { new CanonicalResourceSelectionOption("allowed", "同名", true), new CanonicalResourceSelectionOption("missing", "同名", false) }, SelectedIndex = 0 };
        ResourceSelectorDrop.SetKind(selector, ResourceSelectorKind.Item);
        var parent = new StackPanel(); parent.Children.Add(selector);
        var bubbled = 0; parent.Drop += (_, _) => bubbled++;
        Layout(parent);
        foreach (var resource in new ICanonicalStoryTreeItem[] {
            new CanonicalStoryActorItem(new ActorResourceInfo("allowed", "同名", "a.json", [])),
            new CanonicalStoryItemItem(new IndividualItemResource { ItemId = "missing", DisplayName = "同名" }),
            new CanonicalStoryItemItem(new CollectiveItemResource { GroupId = "group", DisplayName = "同名" }) })
        {
            var result = Drop(selector, new DataObject(CanonicalStoryWorkspaceView.ResourceDragFormat, resource));
            Assert.IsTrue(result.Handled);
            Assert.AreEqual(DragDropEffects.None, result.Effects);
            Assert.AreEqual(0, selector.SelectedIndex);
        }
        selector.IsEnabled = false;
        Assert.IsFalse(ResourceSelectorDrop.TryApply(selector, new DataObject(CanonicalStoryWorkspaceView.ResourceDragFormat,
            new CanonicalStoryItemItem(new IndividualItemResource { ItemId = "allowed", DisplayName = "同名" }))));
        Assert.AreEqual(0, bubbled);
    }

    private static DragEventArgs Drop(ComboBox selector, IDataObject data)
    {
        var constructor = typeof(DragEventArgs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single(c => c.GetParameters().Length == 5);
        var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Link | DragDropEffects.Move, selector, new Point(5, 5)]);
        args.RoutedEvent = DragDrop.DropEvent;
        selector.RaiseEvent(args);
        return args;
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1500, 1000)); view.Arrange(new Rect(0, 0, 1500, 1000)); view.UpdateLayout();
    }
    private static bool Visible(DependencyObject control)
    {
        for (DependencyObject? current = control; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement element && element.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
        }
    }
}
