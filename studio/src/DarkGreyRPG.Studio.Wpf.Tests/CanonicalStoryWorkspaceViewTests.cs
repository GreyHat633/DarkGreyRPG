using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
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
    public void StorySessionAndTaskRestoreIndependentTransientViewports()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);
        view.GraphView.ViewportController.PanX = 10;
        view.GraphView.ViewportController.PanY = 20;
        view.GraphView.ViewportController.Zoom = 1.1;

        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.AreEqual((0d, 0d, 1d), Viewport(view));
        view.GraphView.ViewportController.PanX = 30;
        view.GraphView.ViewportController.PanY = 40;
        view.GraphView.ViewportController.Zoom = 1.3;

        Assert.IsTrue(view.ActivateResourceItem(workspace.TaskItems.Single()));
        Assert.AreEqual((0d, 0d, 1d), Viewport(view));
        view.GraphView.ViewportController.PanX = 50;
        view.GraphView.ViewportController.PanY = 60;
        view.GraphView.ViewportController.Zoom = .8;

        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.AreEqual((30d, 40d, 1.3d), Viewport(view));
        Assert.IsTrue(view.ReturnToStory());
        Assert.AreEqual((10d, 20d, 1.1d), Viewport(view));
        Assert.IsTrue(view.ActivateResourceItem(workspace.TaskItems.Single()));
        Assert.AreEqual((50d, 60d, .8d), Viewport(view));
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
    public void ResourceSelectionClearsVisualNodeSoSameNodeCanRestoreInspector()
    {
        using var workspace = Workspace("story-a");
        var view = Arrange(workspace);

        Assert.IsTrue(view.GraphView.SelectNode("story-a"));
        Assert.IsNotNull(workspace.NodeInspector);

        Assert.IsTrue(view.SelectResourceItem(workspace.ActorItems.Single()));
        Assert.IsNull(view.GraphView.SelectedNode);
        Assert.AreSame(workspace.ActorItems.Single(), workspace.InspectorSelection);

        Assert.IsTrue(view.GraphView.SelectNode("story-a"));
        Assert.IsNotNull(workspace.NodeInspector);
        Assert.AreEqual("story-a", workspace.NodeInspector!.NodeId);
    }

    [STATestMethod]
    public void InlineEditorsCanRefreshResourceOptionsWithoutRebuildingGraphState()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "actor-trigger");
        Assert.IsTrue(new GraphEditSession(new GraphDocument([start]), GraphScope.StoryFlow)
            .SetStoryStartTriggerType("start", "actor-trigger", StoryStartSchema.ActorInteraction, "actor"));
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([start])),
            [new ActorResourceInfo("actor", "Actor", "actor.json", [])]);
        var view = Arrange(workspace);
        var visual = Descendants<CanonicalGraphNodeControl>(view).Single();
        var original = visual.InlineEditor;
        var selected = view.GraphView.SelectNode("start");
        var viewport = Viewport(view);

        view.GraphView.RefreshInlineEditors();

        Assert.IsTrue(selected);
        Assert.IsNotNull(visual.InlineEditor);
        Assert.AreNotSame(original, visual.InlineEditor);
        Assert.AreEqual("actor", visual.InlineEditor!.StoryStartTriggers.Single().SelectedActor?.Id);
        Assert.AreEqual("start", view.GraphView.SelectedNode?.NodeId);
        Assert.AreEqual(viewport, Viewport(view));
        Assert.HasCount(1, workspace.StoryEditor.Host.Graph.Nodes);
    }

    [STATestMethod]
    public void StoryStartRepeatableCheckboxesShareCanonicalProjection()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "opaque-start");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        var view = Arrange(workspace);
        var visual = Descendants<CanonicalGraphNodeControl>(view).Single();
        var inlineToggle = Descendants<CheckBox>(visual).Single(control =>
            AutomationProperties.GetAutomationId(control) == "InlineStartRepeatableToggle");

        Assert.IsTrue(view.GraphView.SelectNode("start"));
        view.UpdateLayout();
        var inspectorToggle = Descendants<CheckBox>(view).Single(control =>
            AutomationProperties.GetAutomationId(control) == "StoryStartRepeatableToggle");
        Assert.AreEqual("可重复", inlineToggle.Content);
        Assert.AreEqual("可重复", inspectorToggle.Content);
        Assert.AreEqual(false, inlineToggle.IsChecked);
        Assert.AreEqual(false, inspectorToggle.IsChecked);

        inlineToggle.IsChecked = true;
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(true, inlineToggle.IsChecked);
        Assert.AreEqual(true, inspectorToggle.IsChecked);
        Assert.AreEqual(StoryStartSchema.Repeatable,
            workspace.StoryEditor.Host.Graph.Nodes.Single().Properties[StoryStartSchema.RepeatPolicyProperty].GetString());

        inspectorToggle.IsChecked = false;
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(false, inlineToggle.IsChecked);
        Assert.AreEqual(StoryStartSchema.Once,
            workspace.StoryEditor.Host.Graph.Nodes.Single().Properties[StoryStartSchema.RepeatPolicyProperty].GetString());
    }

    [STATestMethod]
    public void ActionTitlesDropdownsAndFieldLabelsUseOneAuthorVocabulary()
    {
        var actions = new[]
        {
            CreateAction("give-item", CanonicalStoryActionSchema.GiveItem),
            CreateAction("give-xp", CanonicalStoryActionSchema.GiveXp),
            CreateAction("send-message", CanonicalStoryActionSchema.SendMessage),
        };
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument(actions)));
        var view = Arrange(workspace);
        var cases = new[]
        {
            (Id: "give-item", Name: "物品给予", InspectorLabel: "StoryActionItemLabel", InlineLabel: "InlineActionItemLabel"),
            (Id: "give-xp", Name: "经验给予", InspectorLabel: "StoryActionXpLabel", InlineLabel: "InlineActionXpLabel"),
            (Id: "send-message", Name: "消息发送", InspectorLabel: "StoryActionMessageLabel", InlineLabel: "InlineActionMessageLabel"),
        };

        foreach (var testCase in cases)
        {
            Assert.IsTrue(view.GraphView.SelectNode(testCase.Id));
            view.UpdateLayout();
            var visual = view.GraphView.NodeVisuals.Single(node => node.Node?.NodeId == testCase.Id);
            Assert.AreEqual($"执行「{testCase.Name}」", visual.Node!.DisplayName);
            Assert.AreEqual(testCase.Name, visual.InlineEditor!.SelectedStoryActionType!.DisplayName);
            Assert.AreEqual(testCase.Name, workspace.NodeInspector!.SelectedStoryActionType!.DisplayName);
            Assert.AreEqual(Visibility.Visible, Field(view, testCase.InspectorLabel).Visibility);
            Assert.AreEqual(Visibility.Visible, Field(visual, testCase.InlineLabel).Visibility);
            Assert.AreEqual(Visibility.Visible, Field(view, "StoryActionTypeLabel").Visibility);
            Assert.AreEqual(Visibility.Visible, Field(visual, "InlineActionTypeLabel").Visibility);
        }
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
        choice.Ports.Add(new GraphPort("option_1", "已选择：One", false, GraphInterfaceKind.Logic, 0));
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
    public void ChoiceOptionRowsExposeOneFlowOutputPerStableOptionForAllSupportedSizes()
    {
        foreach (var count in new[] { 1, 2, 5, 10 })
        {
            using var workspace = ChoiceWorkspace(count);
            var view = Arrange(workspace);
            Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
            Assert.IsTrue(view.GraphView.SelectNode("choice"));
            view.UpdateLayout();

            var visual = view.GraphView.NodeVisuals.Single(node => node.Node?.NodeId == "choice");
            Assert.HasCount(count, visual.ChoiceOptionRows);
            CollectionAssert.AreEqual(
                Enumerable.Range(1, count).Select(index => $"option_{index}").ToArray(),
                visual.ChoiceOptionRows.Select(row => row.OptionId).ToArray());
            CollectionAssert.AreEqual(
                Enumerable.Range(1, count).Select(index => $"flow_{index}").ToArray(),
                visual.ChoiceOptionRows.Select(row => row.FlowPortId).ToArray());

            foreach (var row in visual.ChoiceOptionRows)
            {
                Assert.AreEqual(row.FlowPortId, row.FlowOutput.PortId);
                Assert.AreEqual(GraphInterfaceKind.Flow, row.FlowOutput.InterfaceKind);
                Assert.IsFalse(row.FlowOutput.IsInput);
                Assert.IsNull(row.LegacyLogicOutput);
                Assert.HasCount(2, row.OutputGroup.Children);
                Assert.AreSame(row.FlowOutput, row.OutputGroup.Children[0]);
                Assert.AreSame(row.DisplayLabel, row.OutputGroup.Children[1]);
                Assert.AreEqual(row.DisplayText, row.DisplayLabel.Text);
                Assert.AreEqual(108d, row.OutputGroup.Width);
                Assert.AreEqual(TextAlignment.Left, row.DisplayLabel.TextAlignment);
                Assert.AreEqual(HorizontalAlignment.Right, row.DisplayLabel.HorizontalAlignment);
                Assert.AreEqual(82d, row.DisplayLabel.MaxWidth);
                Assert.AreEqual(22d, row.DisplayLabel.Margin.Right);
                Assert.AreEqual(11d, row.DisplayLabel.FontSize);
                Assert.AreEqual(20d, row.OutputGroup.Height);
                Assert.AreEqual(12d, row.OutputGroup.Margin.Bottom);
                Assert.AreEqual(0, Grid.GetRow(row.FlowOutput));
            }

            var flowAnchorXs = visual.ChoiceOptionRows
                .Select(row => row.FlowOutput.GetAnchorPoint(visual).X).ToArray();
            Assert.IsTrue(flowAnchorXs.All(x => Math.Abs(x - flowAnchorXs[0]) < 0.01));
            Assert.IsGreaterThan(visual.ActualWidth / 2d, flowAnchorXs[0]);
            if (visual.ChoiceOptionRows.Count > 1)
            {
                var betweenGroups = visual.ChoiceOptionRows[1].FlowOutput.GetAnchorPoint(visual).Y
                    - visual.ChoiceOptionRows[0].FlowOutput.GetAnchorPoint(visual).Y;
                Assert.IsGreaterThanOrEqualTo(20d, betweenGroups);
            }
        }
    }

    [STATestMethod]
    public void FitAllUsesRenderedChoiceHeightForTenStackedOptionGroups()
    {
        using var workspace = ChoiceWorkspace(10);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        view.UpdateLayout();

        var choice = view.GraphView.NodeVisuals.Single(node => node.Node?.NodeId == "choice");
        Assert.IsGreaterThan(300d, choice.ActualHeight);

        view.GraphView.FitAllNodes();

        Assert.IsLessThan(2d, view.GraphView.ViewportController.Zoom,
            "A ten-option stacked Choice must fit from its rendered height instead of the historical fixed 100-DIP height.");
    }

    [STATestMethod]
    public void ChoiceRenameAndReorderRefreshOnlySelectedNodeAndPreserveStableConnections()
    {
        using var workspace = ChoiceWorkspace(2);
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(session.Editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "target-1")));
        Assert.IsTrue(session.Editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "target-2")));
        Assert.IsTrue(session.Editor.Host.Connect(
            GraphEditorEndpoint.Output("choice", "flow_1", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target-1", "flow_in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(session.Editor.Host.Connect(
            GraphEditorEndpoint.Output("choice", "flow_2", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target-2", "flow_in", GraphInterfaceKind.Flow)));
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(session));
        Assert.IsTrue(view.GraphView.SelectNode("choice"));
        view.UpdateLayout();
        var visual = view.GraphView.NodeVisuals.Single(node => node.Node?.NodeId == "choice");
        var position = (visual.Node!.X, visual.Node.Y);
        Assert.IsTrue(session.Editor.Host.RenameSessionChoiceOption("choice", "option_2", new string('改', 60)),
            string.Join(",", session.Editor.Host.LastValidationIssues.Select(issue => issue.Code)));
        Assert.IsTrue(session.Editor.Host.ReorderSessionChoiceOption("choice", "option_2", 0));
        view.UpdateLayout();

        Assert.AreSame(visual, view.GraphView.NodeVisuals.Single(node => node.Node?.NodeId == "choice"));
        Assert.AreEqual("choice", view.GraphView.SelectedNode?.NodeId);
        Assert.AreEqual(position, (visual.Node!.X, visual.Node.Y));
        CollectionAssert.AreEqual(new[] { "option_2", "option_1" },
            visual.ChoiceOptionRows.Select(row => row.OptionId).ToArray());
        CollectionAssert.AreEqual(new[] { "flow_2", "flow_1" },
            visual.ChoiceOptionRows.Select(row => row.FlowPortId).ToArray());
        CollectionAssert.AreEquivalent(new[] { "flow_1", "flow_2" },
            session.Editor.Host.Connections.Select(connection => connection.FromPortId).ToArray());
        var flowAnchorXs = visual.ChoiceOptionRows.Select(row => row.FlowOutput.GetAnchorPoint(visual).X).ToArray();
        Assert.IsTrue(flowAnchorXs.All(x => Math.Abs(x - flowAnchorXs[0]) < 0.01));
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
            Assert.AreEqual("启动条件 1", confirmation.DisplayName);
            return true;
        };
        var inline = view.GraphView.NodeVisuals.Single(visual => visual.Node?.NodeId == "start").InlineEditor
            ?? throw new AssertFailedException("Start node inline inspector was not created.");
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
        view.Resources["TextFillColorPrimaryBrush"] = Brushes.Black;
        view.Resources["TextFillColorSecondaryBrush"] = Brushes.Gray;
        view.Resources["TextOnAccentFillColorPrimaryBrush"] = Brushes.White;
        var item = workspace.ItemItems.Single();
        var itemButton = Descendants<Button>(view).Single(button => ReferenceEquals(button.Tag, item));
        var itemTitle = Descendants<TextBlock>(itemButton).Single();
        var runs = itemTitle.Inlines.OfType<System.Windows.Documents.Run>().Select(run => run.Text).ToArray();
        CollectionAssert.Contains(runs, "Item");
        CollectionAssert.Contains(runs, "[Item_ID] item");
        Assert.AreEqual(TextTrimming.CharacterEllipsis, itemTitle.TextTrimming);
        Assert.AreEqual(TextAlignment.Left, itemTitle.TextAlignment);

        Assert.AreEqual(Brushes.Black, itemTitle.Foreground);

        Assert.IsTrue(view.SelectResourceItem(item));
        view.UpdateLayout();

        Assert.AreSame(item, workspace.SelectedTreeItem);
        Assert.AreSame(item, workspace.InspectorSelection);
        Assert.AreNotEqual(Brushes.Transparent, itemButton.Background);
        Assert.AreNotEqual(Brushes.Transparent, itemButton.BorderBrush);
        Assert.AreEqual(Brushes.White, itemTitle.Foreground);

        Assert.IsTrue(view.SelectResourceItem(workspace.SessionItems.Single()));
        view.UpdateLayout();
        Assert.AreEqual(Brushes.Transparent, itemButton.Background);
        Assert.AreEqual(Brushes.Transparent, itemButton.BorderBrush);
        Assert.AreEqual(Brushes.Black, itemTitle.Foreground);
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
        Assert.AreEqual(new Point(184, 194), view.ResourceDragGhostViewportPosition);
        Assert.AreEqual(new Vector(116, 46), view.ResourceDragGhostPointerOffset);
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
        Assert.AreEqual(new Point(184d, 194d),
            CanonicalStoryWorkspaceView.ResourceNodeTopLeftFromPointer(pointer));
        Assert.IsTrue(view.PreviewResourceDrag(workspace.SessionItems.Single(), pointer));
        Assert.AreEqual(new Point(184d, 194d), view.ResourceDragGhostViewportPosition);
        Assert.AreEqual(new Point(72d, 107d), view.ResourceGraphPositionFromPointer(pointer));
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

    [STATestMethod]
    public void TextDraftChangesTwentyTimesAndCommitsOneGraphRevision()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        line.Properties["text"] = JsonSerializer.SerializeToElement("原值");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([line]))]);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.IsTrue(view.GraphView.SelectNode("line-1"));
        view.UpdateLayout();

        var draft = Descendants<TextBox>(view)
            .First(textBox => string.Equals(
                textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path,
                "LineText",
                StringComparison.Ordinal));
        var binding = draft.GetBindingExpression(TextBox.TextProperty)!;
        Assert.AreEqual(System.Windows.Data.UpdateSourceTrigger.LostFocus,
            binding.ParentBinding.UpdateSourceTrigger);
        var beforeRevision = workspace.ActiveEditor.GraphRevision;

        for (var index = 1; index <= 20; index++)
        {
            draft.Text = new string('字', index);
            Assert.AreEqual(beforeRevision, workspace.ActiveEditor.GraphRevision,
                $"Draft character {index} must not mutate canonical state.");
        }

        binding.UpdateSource();

        Assert.AreEqual(beforeRevision + 1, workspace.ActiveEditor.GraphRevision);
        Assert.AreEqual(new string('字', 20),
            workspace.ActiveEditor.Host.Graph.Nodes.Single(node => node.NodeId == "line-1")
                .Properties["text"].GetString());
    }

    [STATestMethod]
    public void CtrlSFlushesActiveTextDraftAsOneCanonicalCommit()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        line.Properties["text"] = JsonSerializer.SerializeToElement("原值");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([line]))]);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.IsTrue(view.GraphView.SelectNode("line-1"));
        view.UpdateLayout();

        var draft = Descendants<TextBox>(view)
            .First(textBox => string.Equals(
                textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path,
                "LineText",
                StringComparison.Ordinal));
        var beforeRevision = workspace.ActiveEditor.GraphRevision;
        var beforeUndo = workspace.ActiveGraphHost.Session.UndoCount;
        draft.Text = "焦点仍在输入框的最后一个字";

        DarkGreyRPG.Studio.MainWindow.FlushFocusedDraft(draft);

        Assert.AreEqual(beforeRevision + 1, workspace.ActiveEditor.GraphRevision);
        Assert.AreEqual(beforeUndo + 1, workspace.ActiveGraphHost.Session.UndoCount);
        Assert.AreEqual("焦点仍在输入框的最后一个字",
            workspace.ActiveEditor.Host.Graph.Nodes.Single(node => node.NodeId == "line-1")
                .Properties["text"].GetString());
    }

    [STATestMethod]
    public void DirectInlineObjectiveActorSelectionKeepsSelectedInspectorAndGraphInSync()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(
            objective, CanonicalTaskObjectiveSchema.InteractActor, "actor", out _));
        objective.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] =
            JsonSerializer.SerializeToElement(3);
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            actors:
            [
                new ActorResourceInfo("actor", "测试角色", "actor.json", []),
                new ActorResourceInfo("group", "测试角色组", "group.json", [],
                    CollectiveActorResource.ResourceType),
            ],
            tasks: [new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task",
                new GraphDocument([objective]))]);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.TaskItems.Single()));
        Assert.IsTrue(view.GraphView.SelectNode("objective"));
        view.UpdateLayout();

        var actorSelectors = Descendants<ComboBox>(view)
            .Where(combo => string.Equals(
                combo.GetBindingExpression(Selector.SelectedValueProperty)?.ParentBinding.Path.Path,
                "SelectedObjectiveActorId", StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(2, actorSelectors);
        var selectedInspectorSelector = actorSelectors.Single(combo =>
            AutomationProperties.GetAutomationId(combo) == "TaskObjectiveActorSelector");
        var inlineSelector = actorSelectors.Single(combo => !ReferenceEquals(combo, selectedInspectorSelector));

        inlineSelector.SelectedValue = "group";
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        Assert.AreEqual("group", ((CanonicalSessionSpeakerOption)selectedInspectorSelector.SelectedItem).Id);
        Assert.AreEqual("group", ((CanonicalSessionSpeakerOption)inlineSelector.SelectedItem).Id);
        Assert.AreEqual("group", workspace.ActiveGraphHost.Graph.Nodes.Single().Properties[
            CanonicalTaskObjectiveSchema.ActorIdProperty].GetString());
    }

    [STATestMethod]
    public void SessionLineInspectorPlacesActorSelectorAboveTextEditor()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([line]))]);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.SessionItems.Single()));
        Assert.IsTrue(view.GraphView.SelectNode("line"));
        view.UpdateLayout();

        var actor = Descendants<ComboBox>(view).Single(control =>
            AutomationProperties.GetAutomationId(control) == "SessionLineActorSelector");
        var text = Descendants<TextBox>(view).Single(control =>
            AutomationProperties.GetAutomationId(control) == "SessionLineTextEditor");
        Assert.AreEqual(Visibility.Visible, actor.Visibility);
        Assert.AreEqual(Visibility.Visible, text.Visibility);
        Assert.IsLessThan(
            text.TranslatePoint(new Point(), view).Y,
            actor.TranslatePoint(new Point(), view).Y);
    }

    [STATestMethod]
    public void ObjectivePrerequisiteHelperAppearsOnlyWhenEnabled()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            tasks: [new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task",
                new GraphDocument([objective]))]);
        var view = Arrange(workspace);
        Assert.IsTrue(view.ActivateResourceItem(workspace.TaskItems.Single()));
        Assert.IsTrue(view.GraphView.SelectNode("objective"));
        view.UpdateLayout();

        var toggle = Descendants<CheckBox>(view).Single(control =>
            AutomationProperties.GetAutomationId(control) == "TaskObjectivePrerequisiteToggle");
        var helper = Descendants<TextBlock>(view).Single(control =>
            AutomationProperties.GetAutomationId(control) == "TaskObjectivePrerequisiteHelp");
        Assert.AreEqual("前置条件", toggle.Content);
        Assert.AreEqual(Visibility.Collapsed, helper.Visibility);

        toggle.IsChecked = true;
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        Assert.AreEqual("前置条件为 True 时激活", helper.Text);
        Assert.AreEqual(Visibility.Visible, helper.Visibility);
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

    private static TextBlock Field(DependencyObject root, string automationId)
        => Descendants<TextBlock>(root).Single(text =>
            AutomationProperties.GetAutomationId(text) == automationId);

    private static GraphNode CreateAction(string id, string actionType)
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, id);
        Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(action, actionType, out var issues),
            string.Join("; ", issues.Select(issue => issue.Message)));
        return action;
    }

    private static (double PanX, double PanY, double Zoom) Viewport(CanonicalStoryWorkspaceView view)
        => (view.GraphView.ViewportController.PanX, view.GraphView.ViewportController.PanY,
            view.GraphView.ViewportController.Zoom);

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

    private static CanonicalStoryWorkspaceViewModel ChoiceWorkspace(int count)
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        var options = new List<object> { new { option_id = "option_1", display_text = "短选项 1", flow_port_id = "flow_1" } };
        choice.Ports.Single(port => port.Id == "flow_1").DisplayName = "短选项 1";
        for (var index = 2; index <= count; index++)
        {
            var text = index == count ? new string('长', 80) : $"短选项 {index}";
            options.Add(new { option_id = $"option_{index}", display_text = text, flow_port_id = $"flow_{index}" });
            choice.Ports.Add(new GraphPort($"flow_{index}", text, false, GraphInterfaceKind.Flow, index - 1));
        }
        choice.Properties[SessionChoiceSchema.OptionsProperty] = JsonSerializer.SerializeToElement(options);
        return new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            sessions: [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([choice]))]);
    }

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
