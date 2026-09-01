using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CanonicalResourceInspectorHierarchyTests
{
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
            Assert.AreEqual(identityLabel, Field(view, "ResourceInspectorIdentityLabel").Text);
            Assert.AreEqual(id, Field(view, "ResourceInspectorIdentityValue").Text);
            Assert.AreEqual(11d, Field(view, "ResourceInspectorDisplayNameLabel").FontSize);
            Assert.AreEqual(14d, Field(view, "ResourceInspectorDisplayNameValue").FontSize);
            Assert.AreEqual(11d, Field(view, "ResourceInspectorTagsLabel").FontSize);
            Assert.AreEqual(14d, Field(view, "ResourceInspectorTagsValue").FontSize);
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
