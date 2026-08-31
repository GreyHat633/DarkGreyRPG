using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Text.Json;
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
public sealed class CanonicalStoryWorkspaceViewTests
{
    [STATestMethod]
    public void ThreeColumnViewBindsStoryHomeGraphByDefault()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);

        Assert.AreSame(workspace.ActiveGraphHost, view.GraphView.Host);
        Assert.AreSame(workspace.StoryEditor.Host, view.GraphView.Host);
        Assert.IsGreaterThan(0d, view.ActualWidth);
        Assert.IsGreaterThan(0d, view.ActualHeight);
    }

    [STATestMethod]
    public void InstantFolderTemplateKeepsEveryFolderHeaderVisible()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);
        var visibleHeaders = Descendants<ToggleButton>(view)
            .Where(toggle => toggle.Name == "HeaderToggle")
            .Select(toggle => toggle.Content)
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .ToArray();

        foreach (var expected in new[] { "角色", "物品", "会话", "任务" })
            CollectionAssert.Contains(visibleHeaders, expected);
    }

    [STATestMethod]
    public void ActorSelectionIsInspectorOnlyAndSessionActivationChangesGraphHost()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);
        var storyHost = view.GraphView.Host;

        Assert.IsTrue(view.SelectResourceItem(workspace.ActorItems.Single()));
        Assert.AreSame(storyHost, view.GraphView.Host);
        Assert.AreEqual("Actor", workspace.InspectorTitle);

        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.AreSame(workspace.SessionItems.Single().Editor.Host, view.GraphView.Host);
        Assert.HasCount(3, workspace.Breadcrumbs);
        Assert.IsTrue(view.ReturnToStory());
        Assert.AreSame(storyHost, view.GraphView.Host);
    }

    [STATestMethod]
    public void GraphNodeSelectionRoutesNodeInspectorAndClearRestoresActiveResource()
    {
        using var workspace = Workspace("story-a");
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(session.Editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")));
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(session));

        Assert.IsTrue(view.GraphView.SelectNode("line-1"));
        Assert.IsNotNull(workspace.NodeInspector);
        Assert.AreEqual("line-1", workspace.NodeInspector!.NodeId);
        Assert.AreSame(workspace.NodeInspector, workspace.InspectorSelection);

        view.GraphView.ClearSelection();
        Assert.IsNull(workspace.NodeInspector);
        Assert.AreSame(session.Editor, workspace.InspectorSelection);
    }

    [STATestMethod]
    public void ChoiceOptionRemovalConfirmationIsInjectedAtViewBoundary()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        choice.Properties["options"] = JsonSerializer.SerializeToElement(new[]
        {
            new { option_id = "option_1", display_text = "One", flow_port_id = "flow_1" },
            new { option_id = "option_2", display_text = "Two", flow_port_id = "flow_2" },
        });
        choice.Ports.Single(port => port.Id == "flow_1").DisplayName = "One";
        choice.Ports.Single(port => port.Id == "option_1").DisplayName = "已选择：One";
        choice.Ports.Add(new GraphPort("flow_2", "Two", false, GraphInterfaceKind.Flow, 1));
        choice.Ports.Add(new GraphPort("option_2", "已选择：Two", false, GraphInterfaceKind.Logic, 1));
        var target = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic");
        target.Properties["port_id"] = JsonSerializer.SerializeToElement("known");
        target.Properties["display_name"] = JsonSerializer.SerializeToElement("Known");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([choice, target], [new GraphConnection("choice", "option_2", "logic", "logic_in", GraphInterfaceKind.Logic)]))]);
        var view = Arrange(workspace);
        var prompts = 0;
        view.ChoiceOptionRemovalConfirmation = confirmation =>
        {
            prompts++;
            Assert.AreEqual("Two", confirmation.DisplayText);
            return true;
        };
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(view.ActivateResourceItem(session));
        Assert.IsTrue(view.GraphView.SelectNode("choice"));

        Assert.IsTrue(workspace.NodeInspector!.RemoveChoiceOption("option_2"));
        Assert.AreEqual(1, prompts);
        Assert.HasCount(1, workspace.NodeInspector.ChoiceOptions);
        Assert.IsEmpty(session.Editor.Host.Graph.Connections);
    }

    [STATestMethod]
    public void InlineStartRemovalConfirmationIsInjectedAtViewBoundary()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "region");
        var target = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "target");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story",
                new GraphDocument([start, target],
                    [new GraphConnection("start", "region", "target", "flow_in", GraphInterfaceKind.Flow)])));
        var view = Arrange(workspace);
        var prompts = 0;
        view.StoryStartTriggerRemovalConfirmation = confirmation =>
        {
            prompts++;
            Assert.AreEqual("进入区域", confirmation.DisplayName);
            return true;
        };
        var inline = view.GraphView.InlineEditorFactory!(workspace.ActiveGraphHost.Nodes.Single(node => node.Id == "start"));
        Assert.IsNotNull(inline);
        Assert.IsTrue(inline.AddStoryStartTrigger("角色触发", StoryStartSchema.ActorInteraction,
            StoryStartSchema.DefaultTriggerProperties(StoryStartSchema.ActorInteraction, "actor")));

        Assert.IsTrue(inline.RemoveStoryStartTrigger("region"));
        Assert.AreEqual(1, prompts);
        Assert.HasCount(1, inline.StoryStartTriggers);
        Assert.AreEqual(StoryStartSchema.ActorInteraction, inline.StoryStartTriggers.Single().TriggerType);
        Assert.IsEmpty(workspace.StoryEditor.Host.Graph.Connections);
    }

    [STATestMethod]
    public void DirtyGraphAllowsVisualActivationAndKeepsOriginalDraft()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);
        var storyHost = view.GraphView.Host;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "dirty-action")));

        Assert.IsTrue(view.ActivateResourceItem(workspace.TaskItems.Single()));

        Assert.AreNotSame(storyHost, view.GraphView.Host);
        Assert.IsFalse(workspace.IsStoryFlowActive);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsTrue(workspace.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "dirty-action"));
    }

    [STATestMethod]
    public void ReplacingWorkspaceDependencyPropertyRebindsCanonicalHost()
    {
        using var first = Workspace("story-a");
        using var second = Workspace("story-b");
        var view = Arrange(first);

        view.Workspace = second;
        view.UpdateLayout();

        Assert.AreSame(second.ActiveGraphHost, view.GraphView.Host);
        Assert.AreEqual("story-b", view.GraphView.Host!.Graph.Nodes.Single().Id);
    }

    [STATestMethod]
    public void ResourceSelectionKeepsVisibleHighlightAndInspectorInSync()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument())],
            items: [new IndividualItemResource { ItemId = "item", DisplayName = "Item" }]);
        var view = Arrange(workspace);
        view.Resources["AccentFillColorSecondaryBrush"] = Brushes.LightBlue;
        view.Resources["AccentFillColorDefaultBrush"] = Brushes.Blue;
        var item = workspace.ItemItems.Single();
        var itemButton = Descendants<Button>(view).Single(button => ReferenceEquals(button.Tag, item));

        Assert.IsTrue(view.SelectResourceItem(item));
        view.UpdateLayout();

        Assert.AreSame(item, workspace.SelectedTreeItem);
        Assert.AreSame(item, workspace.InspectorSelection);
        Assert.AreNotEqual(Brushes.Transparent, itemButton.Background);
        Assert.AreNotEqual(Brushes.Transparent, itemButton.BorderBrush);

        Assert.IsTrue(view.SelectResourceItem(workspace.SessionItems.Single()));
        view.UpdateLayout();
        Assert.AreEqual(Brushes.Transparent, itemButton.Background);
        Assert.AreEqual(Brushes.Transparent, itemButton.BorderBrush);
    }

    [STATestMethod]
    public void PendingProjectGraphFocusSelectsCanonicalStoryNode()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);

        Assert.IsTrue(workspace.RequestStoryNodeFocus("story-a", "target_story_id"));
        view.UpdateLayout();

        Assert.IsTrue(view.ApplyPendingStoryNodeFocus());
        Assert.AreEqual("story-a", view.GraphView.SelectedNode?.NodeId);
    }

    [STATestMethod]
    public void ViewRoutesFolderAndItemActionsWithoutChangingGraphHost()
    {
        using var workspace = Workspace("story-a");
        GraphResourceKind? created = null;
        GraphResourceKind? referenced = null;
        var actorCreated = 0;
        var actorReferenced = 0;
        ICanonicalStoryTreeItem? deleted = null;
        workspace.CreateResourceRequested = kind => created = kind;
        workspace.ReferenceResourceRequested = kind => referenced = kind;
        workspace.CreateActorRequested = () => actorCreated++;
        workspace.ReferenceActorRequested = () => actorReferenced++;
        workspace.DeleteResourceRequested = item => deleted = item;
        workspace.RefreshResourceCommandStates();
        var view = Arrange(workspace);
        var storyHost = view.GraphView.Host;

        Assert.IsTrue(view.RequestCreateResource(CanonicalStoryFolderKind.Sessions));
        Assert.IsTrue(view.RequestReferenceResource(CanonicalStoryFolderKind.Tasks));
        Assert.IsTrue(view.RequestCreateResource(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(view.RequestReferenceResource(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(view.RequestDeleteResource(workspace.ActorItems.Single()));
        Assert.IsTrue(view.RequestDeleteResource(workspace.TaskItems.Single()));

        Assert.AreEqual(GraphResourceKind.Session, created);
        Assert.AreEqual(GraphResourceKind.Task, referenced);
        Assert.AreEqual(1, actorCreated);
        Assert.AreEqual(1, actorReferenced);
        Assert.AreSame(workspace.TaskItems.Single(), deleted);
        Assert.AreSame(storyHost, view.GraphView.Host);
    }

    [STATestMethod]
    public void ResolvedSessionAndTaskMembersPlaceBoundAggregatesAtGraphCoordinates()
    {
        using var workspace = AggregateWorkspace();
        var ids = new Queue<string?>(["session-placement", "task-placement", "session-placement-2"]);
        var view = Arrange(workspace, ids.Dequeue);

        Assert.IsTrue(view.GraphView.AllowDrop);
        Assert.IsTrue(view.CanPlaceResource(workspace.SessionItems.Single()));
        Assert.IsTrue(view.PlaceResourceAt(workspace.SessionItems.Single(), 120.5, 240.25));
        Assert.IsTrue(view.PlaceResourceAt(workspace.TaskItems.Single(), 360, 180));
        Assert.IsTrue(view.PlaceResourceAt(workspace.SessionItems.Single(), 520, 260));

        var nodes = workspace.StoryEditor.Host.Graph.Nodes;
        var session = nodes.Single(node => node.Id == "session-placement");
        var task = nodes.Single(node => node.Id == "task-placement");
        Assert.AreEqual("session", session.Type);
        Assert.AreEqual("session", session.Properties["resource_id"].GetString());
        CollectionAssert.AreEqual(new[] { "flow_in", "session_done", "session_known" },
            session.Ports.Select(port => port.Id).ToArray());
        Assert.AreEqual("task", task.Type);
        Assert.AreEqual("task", task.Properties["resource_id"].GetString());
        CollectionAssert.AreEqual(new[] { "flow_in", "task_done", "task_known" },
            task.Ports.Select(port => port.Id).ToArray());
        Assert.AreEqual(new GraphEditorNodePosition(120.5, 240.25), workspace.StoryEditor.Host.Layout["session-placement"]);
        Assert.AreEqual(new GraphEditorNodePosition(360, 180), workspace.StoryEditor.Host.Layout["task-placement"]);
        Assert.AreEqual("session-placement-2", view.GraphView.SelectedNode?.NodeId);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsEmpty(workspace.LastAggregateAuthoringIssues);
        Assert.IsEmpty(ids);
    }

    [STATestMethod]
    public void ResourceDragPreviewShowsGhostWithoutMutatingStoryAndCancelsCleanly()
    {
        using var workspace = AggregateWorkspace();
        var view = Arrange(workspace);
        var before = workspace.StoryEditor.Host.Graph.ToJson();
        var item = workspace.SessionItems.Single();

        Assert.IsTrue(view.PreviewResourceDrag(item, new Point(300, 240)));
        Assert.IsTrue(view.IsResourceDragGhostVisible);
        Assert.AreEqual(item.DisplayName, view.ResourceDragGhostDisplayName);
        Assert.AreEqual(new Point(184, 202), view.ResourceDragGhostViewportPosition);
        Assert.AreEqual(before, workspace.StoryEditor.Host.Graph.ToJson());

        view.CancelResourceDragPreview();
        Assert.IsFalse(view.IsResourceDragGhostVisible);
        Assert.AreEqual(before, workspace.StoryEditor.Host.Graph.ToJson());
    }

    [STATestMethod]
    public void ResourceGhostAndDropShareTopLeftAnchorAcrossZoomAndPan()
    {
        using var workspace = AggregateWorkspace();
        var view = Arrange(workspace);
        view.GraphView.ViewportController.Zoom = 2d;
        view.GraphView.ViewportController.PanX = 40d;
        view.GraphView.ViewportController.PanY = -20d;

        var pointer = new Point(300d, 240d);
        Assert.AreEqual(new Point(184d, 202d),
            CanonicalStoryWorkspaceView.ResourceNodeTopLeftFromPointer(pointer));
        Assert.IsTrue(view.PreviewResourceDrag(workspace.SessionItems.Single(), pointer));
        Assert.AreEqual(new Point(184d, 202d), view.ResourceDragGhostViewportPosition);
        Assert.AreEqual(new Point(72d, 111d), view.ResourceGraphPositionFromPointer(pointer));
    }

    [STATestMethod]
    public void AggregateNodeEditRequestOpensItsSessionGraph()
    {
        using var workspace = AggregateWorkspace();
        var view = Arrange(workspace, () => "session-placement");
        Assert.IsTrue(view.PlaceResourceAt(workspace.SessionItems.Single(), 120, 180));
        var aggregate = workspace.StoryEditor.Host.Nodes.Single(node => node.NodeId == "session-placement");

        Assert.IsTrue(view.GraphView.RequestNodeEdit(aggregate));

        Assert.AreSame(workspace.SessionItems.Single().Editor.Host, view.GraphView.Host);
        Assert.AreSame(workspace.SessionItems.Single(), workspace.SelectedTreeItem);
    }

    [STATestMethod]
    public void ActorForeignLocalGraphAndInvalidPositionDropsFailWithoutStoryMutation()
    {
        using var workspace = AggregateWorkspace();
        var idCalls = 0;
        var view = Arrange(workspace, () => { idCalls++; return "unused"; });
        var before = workspace.StoryEditor.Host.Graph.ToJson();
        var beforeLayout = workspace.StoryEditor.Host.Layout.ToArray();

        Assert.IsFalse(view.CanPlaceResource(workspace.ActorItems.Single()));
        Assert.IsFalse(view.PlaceResourceAt(workspace.ActorItems.Single(), 10, 20));
        using var foreignEditor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "foreign", "Foreign", new GraphDocument()));
        Assert.IsFalse(view.PlaceResourceAt(new CanonicalStoryGraphItem(foreignEditor), 10, 20));
        Assert.AreEqual(0, idCalls);

        Assert.IsFalse(view.PlaceResourceAt(workspace.SessionItems.Single(), double.NaN, 20));
        Assert.AreEqual(1, idCalls);
        Assert.AreEqual("graph.aggregate.drop.position.invalid", workspace.LastAggregateAuthoringIssues.Single().Code);

        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.IsFalse(view.CanPlaceResource(workspace.TaskItems.Single()));
        Assert.IsFalse(view.PlaceResourceAt(workspace.TaskItems.Single(), 10, 20));
        Assert.AreEqual(1, idCalls);
        Assert.AreEqual(before, workspace.StoryEditor.Host.Graph.ToJson());
        CollectionAssert.AreEqual(beforeLayout, workspace.StoryEditor.Host.Layout.ToArray());
    }

    private static CanonicalStoryWorkspaceView Arrange(
        CanonicalStoryWorkspaceViewModel workspace,
        Func<string?>? placementNodeIdSource = null)
    {
        var view = placementNodeIdSource is null
            ? new CanonicalStoryWorkspaceView(workspace)
            : new CanonicalStoryWorkspaceView(workspace, placementNodeIdSource);
        var root = new Grid { Width = 1280, Height = 720 };
        root.Children.Add(view);
        root.Measure(new Size(1280, 720));
        root.Arrange(new Rect(0, 0, 1280, 720));
        root.UpdateLayout();
        return view;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static CanonicalStoryWorkspaceViewModel Workspace(string storyId)
        => new(
            new GraphResourceEnvelope(
                GraphResourceKind.Story,
                storyId,
                storyId,
                new GraphDocument([new GraphNode(storyId, "action", storyId)])),
            [new ActorResourceInfo("actor", "Actor", "actor.json", [])],
            [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument())],
            [new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument())]);

    private static CanonicalStoryWorkspaceViewModel AggregateWorkspace()
    {
        var sessionEnd = GraphNodeFactory.Create(GraphScope.Session, "end", "session-end");
        sessionEnd.Properties["port_id"] = JsonSerializer.SerializeToElement("session_done");
        sessionEnd.Properties["display_name"] = JsonSerializer.SerializeToElement("Session Done");
        var sessionLogic = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "session-logic");
        sessionLogic.Properties["port_id"] = JsonSerializer.SerializeToElement("session_known");
        sessionLogic.Properties["display_name"] = JsonSerializer.SerializeToElement("Session Known");

        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "task-settle");
        settle.Ports.Add(new GraphPort("task_done", "Task Done", true, GraphInterfaceKind.Logic, 0));
        var taskLogic = GraphNodeFactory.Create(GraphScope.Task, "logic_output", "task-logic");
        taskLogic.Properties["port_id"] = JsonSerializer.SerializeToElement("task_known");
        taskLogic.Properties["display_name"] = JsonSerializer.SerializeToElement("Task Known");

        return new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            [new ActorResourceInfo("actor", "Actor", "actor.json", [])],
            [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([sessionEnd, sessionLogic]))],
            [new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument([settle, taskLogic]))]);
    }
}
