using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views;

/// <summary>WPF's GroupItem peer omits interactive headers; expose the naming and folding controls.</summary>
public sealed class StoryNavigationList : ListBox
{
    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationPeer(this);
    private sealed class NavigationPeer(StoryNavigationList owner) : ListBoxAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var children = base.GetChildrenCore() ?? [];
            foreach (var element in HeaderControls(owner))
                if (UIElementAutomationPeer.CreatePeerForElement(element) is { } peer) children.Add(peer);
            return children;
        }
        private static IEnumerable<UIElement> HeaderControls(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement { IsVisible: true, Name: "GroupToggle" or "GroupName" } element) yield return element;
                foreach (var descendant in HeaderControls(child)) yield return descendant;
            }
        }
    }
}
