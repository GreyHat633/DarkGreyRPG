using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalStoryWorkspaceView
{
    private void Usage_OnClick(object sender, MouseButtonEventArgs e) { if (sender is ListBox { SelectedItem: ResourceUsage usage }) Workspace?.NavigateUsage(usage); }
    private void Usage_OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && sender is ListBox { SelectedItem: ResourceUsage usage }) { Workspace?.NavigateUsage(usage); e.Handled = true; } }
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
            if (hit.GroupId is { } groupId) { WorkspaceGraph.FocusGroup(groupId); return; }
            var node = workspace.ActiveGraphHost.Nodes.FirstOrDefault(candidate => candidate.NodeId == hit.NodeId);
            if (node is not null) { WorkspaceGraph.FocusNode(node); workspace.SelectGraphNode(node); }
            else if (hit.FieldPath != "task_metadata.description") return;
            if (hit.OptionId is { } optionId)
            {
                var control = LinePagesEditor.Children<DynamicContentEditor>(this).FirstOrDefault(c => c.IsVisible && c.DataContext is CanonicalChoiceOptionViewModel option && option.OptionId == optionId);
                control?.BringIntoView(); control?.Body.Focus();
            }
            if (hit.PageId is not { } pageId) { FocusUsageField(hit.FieldPath); return; }
            var page = workspace.NodeInspector?.LinePages.FirstOrDefault(candidate => candidate.PageId == pageId);
            if (page is not null) page.IsExpanded = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!ReferenceEquals(Workspace, workspace) || !ReferenceEquals(workspace.SearchTarget, hit)) return;
                var scope = LinePagesEditor.Children<FrameworkElement>(this).Where(c => c.DataContext is CanonicalLinePageViewModel line && line.PageId == pageId).ToArray();
                var field = hit.FieldPath?.Split('.').Last();
                if (field is "voice_ref" or "portrait_variant")
                    foreach (var expander in scope.OfType<Expander>()) expander.IsExpanded = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (!ReferenceEquals(Workspace, workspace) || !ReferenceEquals(workspace.SearchTarget, hit)) return;
                    FrameworkElement? control = field == "voice_ref" ? scope.OfType<AudioPreviewControl>().FirstOrDefault(c => c.IsVisible)
                        : field == "portrait_variant" ? scope.OfType<ComboBox>().FirstOrDefault(c => c.IsVisible)
                        : scope.OfType<DynamicContentEditor>().FirstOrDefault(c => c.IsVisible);
                    control?.BringIntoView();
                    if (control is DynamicContentEditor text) text.Body.Focus(); else control?.Focus();
                }));
            }));
        }));
    }

    private void FocusUsageField(string? field)
    {
        var tail = field?.Split('.').Last();
        var path = tail switch
        {
            "task_metadata.description" => "InspectorTaskEditor.TaskDescription",
            "speaker_actor_id" => "SelectedSpeakerId",
            "text" => "LineText", "main" => "TitleMain", "subtitle" => "TitleSubtitle",
            "message" => "StoryActionMessage", "description" => "ObjectiveDescription",
            "item" => "SelectedObjectiveItemId", "entity" => "SelectedObjectiveActorId",
            "actor_id" => "SelectedObjectiveActorId", "item_id" => "SelectedStoryActionItemId",
            _ => null
        };
        if (field == "task_metadata.description") path = "InspectorTaskEditor.TaskDescription";
        var controls = LinePagesEditor.Children<FrameworkElement>(this).Where(c => c.IsVisible);
        if (field?.StartsWith("layers[", StringComparison.Ordinal) == true)
        {
            var end = field.IndexOf(']');
            if (end > 7 && int.TryParse(field.AsSpan(7, end - 7), out var index))
                controls.OfType<SessionScreenEditor>().FirstOrDefault()?.NavigateLayer(index);
            return;
        }
        foreach (var control in controls)
        {
            DependencyProperty? property = control is DynamicContentEditor ? DynamicContentEditor.TextProperty
                : control is TextBox ? TextBox.TextProperty : control is ComboBox ? ComboBox.SelectedValueProperty : null;
            if (property is null || path is null || BindingOperations.GetBinding(control, property)?.Path?.Path != path) continue;
            control.BringIntoView(); if (control is DynamicContentEditor text) text.Body.Focus(); else control.Focus(); return;
        }
    }
}
