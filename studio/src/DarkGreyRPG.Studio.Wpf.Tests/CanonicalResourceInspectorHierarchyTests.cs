using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CanonicalResourceInspectorHierarchyTests
{
    [STATestMethod]
    public void SelectedNodeUsesHelpInsteadOfSaveIndicator()
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "title", "title");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([node])));
        Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));
        Assert.AreEqual(string.Empty, workspace.InspectorSaveStateText);
        Assert.IsFalse(string.IsNullOrWhiteSpace(workspace.NodeInspector?.HelpText));
    }

    [STATestMethod]
    public void CanonicalTypedActorHasAnEditPortraitEntry()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            actors: [new ActorResourceInfo("actor", "角色", "actor.json", [], IndividualActorResource.ResourceType)]);
        workspace.PortraitEditorFactory = actor =>
            new ActorEditorViewModel(ActorDocument.CreateIndividual(actor.Id, actor.DisplayName));
        var view = Arrange(workspace);
        Assert.IsTrue(workspace.SelectTreeItem(workspace.ActorItems.Single()));
        view.UpdateLayout();
        Assert.IsTrue(workspace.CanEditActorPortrait);
        string? edited = null;
        workspace.EditActorPortraitRequested = actor => edited = actor.Id;
        workspace.EditActorPortrait();
        Assert.AreEqual("actor", edited);
        var entry = Descendants<ActorPortraitEditor>(view).Single();
        Assert.AreSame(workspace.InspectorPortraitEditor, entry.Editor);
        Assert.AreEqual(Visibility.Visible, entry.Visibility);
        workspace.ClearGraphSelection();
        Assert.IsFalse(workspace.CanEditActorPortrait);
    }

    [STATestMethod]
    public void PresentationNodesExposeTheirControlsInTheRealWorkspaceView()
    {
        foreach (var type in new[] { "title", "music", "screen" })
        {
            var story = new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument());
            var scope = type == "title" ? GraphScope.StoryFlow : GraphScope.Session;
            var node = DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory.Create(scope, type, type);
            var resource = new GraphResourceEnvelope(type == "title" ? GraphResourceKind.Story : GraphResourceKind.Session,
                type == "title" ? "story" : "session", type, new GraphDocument([node]));
            using var workspace = new CanonicalStoryWorkspaceViewModel(type == "title" ? resource : story,
                sessions: type == "title" ? [] : [resource]);
            if (type != "title") Assert.IsTrue(workspace.OpenGraphResource(workspace.SessionItems.Single()));
            var view = new CanonicalStoryWorkspaceView(workspace);
            var window = new Window { Content = view, Width = 1280, Height = 900, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                window.UpdateLayout();
                var scroll = Descendants<ScrollViewer>(view).Single(s => AutomationProperties.GetAutomationId(s) == "CanonicalInspectorScrollViewer");
                var fields = Descendants<TextBox>(scroll).Where(t => t.IsVisible).ToArray();
                Assert.IsTrue(fields.Length > 0, $"{type} inspector must expose actual editable controls.");
            }
            finally { window.Close(); }
        }
    }

    [STATestMethod]
    public void ActorAndItemInspectorsUseFieldHierarchyAndAuthorTerminologyWithoutOwnershipStatus()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()),
            actors:
            [
                new ActorResourceInfo("actor", "角色甲", "actor.json", ["merchant"]),
                new ActorResourceInfo("actors", "角色组甲", "actors.json", ["quest"],
                    CollectiveActorResource.ResourceType),
            ],
            items:
            [
                new IndividualItemResource { ItemId = "item", DisplayName = "物品甲", Tags = ["tool"] },
                new CollectiveItemResource { GroupId = "items", DisplayName = "物品组甲", Tags = ["loot"] },
            ]);
        var view = Arrange(workspace);
        var cases = new (ICanonicalStoryTreeItem Item, string Kind, string IdentityLabel, string Id)[]
        {
            (workspace.ActorItems.Single(item => item.Id == "actor"), "角色", "NPC_ID", "actor"),
            (workspace.ActorItems.Single(item => item.Id == "actors"), "角色组", "Group_ID", "actors"),
            (workspace.ItemItems.Single(item => item.Id == "item"), "物品", "Item_ID", "item"),
            (workspace.ItemItems.Single(item => item.Id == "items"), "物品组", "Group_ID", "items"),
        };

        foreach (var (item, kind, identityLabel, id) in cases)
        {
            Assert.IsTrue(workspace.SelectTreeItem(item));
            view.UpdateLayout();

            Assert.AreEqual(kind, workspace.InspectorKindText);
            Assert.AreEqual($"[{identityLabel}] {id}", Field(view, "ResourceInspectorIdentityLine").Text);
            Assert.AreEqual("标签：", Field(view, "ResourceInspectorTagsLabel").Text);
            Assert.AreEqual(14d, Field(view, "ResourceInspectorTagsLabel").FontSize);
            Assert.AreEqual(14d, Field(view, "ResourceInspectorTagsValue").FontSize);
            var tagsRow = Descendants<Grid>(view).Single(grid =>
                AutomationProperties.GetAutomationId(grid) == "ResourceInspectorTagsRow");
            Assert.AreEqual(2, tagsRow.ColumnDefinitions.Count);
            Assert.IsTrue(tagsRow.ColumnDefinitions[0].Width.IsAuto);
            Assert.IsTrue(tagsRow.ColumnDefinitions[1].Width.IsStar);
            Assert.AreEqual(1, Grid.GetColumn(Field(view, "ResourceInspectorTagsValue")));
            Assert.IsFalse(Descendants<TextBlock>(view).Any(text => text.Text is "资源属性" or "显示名称"));
            Assert.IsFalse(Descendants<TextBlock>(view).Any(text => text.Text == "拥有/引用状态"));
        }

        Assert.IsTrue(Descendants<ScrollViewer>(view).Any(scroll =>
            AutomationProperties.GetAutomationId(scroll) == "CanonicalInspectorScrollViewer"
            && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto));
    }

    private static TextBlock Field(CanonicalStoryWorkspaceView view, string automationId)
        => Descendants<TextBlock>(view).Single(text =>
            AutomationProperties.GetAutomationId(text) == automationId);

    private static CanonicalStoryWorkspaceView Arrange(CanonicalStoryWorkspaceViewModel workspace)
    {
        var view = new CanonicalStoryWorkspaceView(workspace);
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
}
