using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ActionSelectionDefaultsTests
{
    [STATestMethod]
    public void CreationAndBothToggleDirectionsSelectFirstOptionInBothSurfaces()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument()));
        Assert.IsTrue(workspace.ActiveGraphHost.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action")));
        var view = new CanonicalStoryWorkspaceView(workspace) { Width = 1500, Height = 1000 };
        Layout();
        Assert.IsTrue(view.GraphView.SelectNode("action"));
        Layout();
        var inspector = workspace.NodeInspector!;
        CheckFirst();
        // A different native type must not be restored when advanced mode is disabled.
        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions.Single(o => o.Value == CanonicalStoryActionSchema.GiveXp);
        for (var i = 0; i < 6; i++)
        {
            var toggle = Children(view).OfType<CheckBox>().First(c => Visible(c) && Equals(c.Content, "高级选项"));
            toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
            Layout(); CheckFirst();
            Assert.AreEqual(CanonicalStoryActionSchema.ExecuteCommand, inspector.StoryActionType);
            toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
            Layout(); CheckFirst();
            Assert.AreEqual(CanonicalStoryActionSchema.GiveItem, inspector.StoryActionType);
            Assert.IsTrue(workspace.ActiveGraphHost.Undo()); Layout(); CheckFirst();
            Assert.IsTrue(workspace.ActiveGraphHost.Redo()); Layout(); CheckFirst();
        }
        void Layout() { view.Measure(new Size(1500, 1000)); view.Arrange(new Rect(0, 0, 1500, 1000)); view.UpdateLayout(); }
        void CheckFirst()
        {
            var selectors = Children(view).OfType<ComboBox>().Where(c => Visible(c) && c.Items.OfType<CanonicalStoryActionTypeOption>().Any()).ToArray();
            Assert.AreEqual(2, selectors.Length, "Both node and Inspector must be checked");
            foreach (var selector in selectors)
            {
                Assert.AreEqual(0, selector.SelectedIndex);
                Assert.AreSame(selector.Items[0], selector.SelectedItem);
                Assert.AreEqual(workspace.ActiveGraphHost.Graph.Nodes.Single().Properties[CanonicalStoryActionSchema.TypeProperty].GetString(), selector.SelectedValue);
            }
        }
    }
    private static bool Visible(DependencyObject control)
    {
        for (DependencyObject? current = control; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement element && element.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Children(child)) yield return next;
        }
    }
}
