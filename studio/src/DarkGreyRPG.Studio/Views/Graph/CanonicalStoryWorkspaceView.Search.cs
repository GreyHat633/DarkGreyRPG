using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalStoryWorkspaceView
{
    private async void Search_OnClick(object sender, RoutedEventArgs e) { if (Workspace is { } workspace) await workspace.SearchAsync(AuthoringSearchBox.Text); }
    private async void Search_OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Workspace is { } workspace) { e.Handled = true; await workspace.SearchAsync(AuthoringSearchBox.Text); } }
    private void SearchResult_OnDoubleClick(object sender, MouseButtonEventArgs e) { if (sender is ListBox { SelectedItem: AuthoringSearchHit hit }) Workspace?.NavigateSearch(hit); }
    private void SearchResult_OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && sender is ListBox { SelectedItem: AuthoringSearchHit hit }) { Workspace?.NavigateSearch(hit); e.Handled = true; } }

    private void QueueSearchFocus()
    {
        var workspace = Workspace;
        var hit = workspace?.SearchTarget;
        if (hit is null) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!ReferenceEquals(Workspace, workspace) || !ReferenceEquals(workspace!.SearchTarget, hit)) return;
            var node = workspace.ActiveGraphHost.Nodes.FirstOrDefault(candidate => candidate.NodeId == hit.NodeId);
            if (node is null) return;
            WorkspaceGraph.FocusNode(node);
            workspace.SelectGraphNode(node);
            if (hit.PageId is not { } pageId) return;
            var page = workspace.NodeInspector?.LinePages.FirstOrDefault(candidate => candidate.PageId == pageId);
            if (page is not null) page.IsExpanded = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!ReferenceEquals(Workspace, workspace) || !ReferenceEquals(workspace.SearchTarget, hit)) return;
                var box = LinePagesEditor.Children<TextBox>(this).FirstOrDefault(control => control.IsVisible && control.DataContext is CanonicalLinePageViewModel line && line.PageId == pageId);
                box?.BringIntoView(); box?.Focus();
            }));
        }));
    }
}
