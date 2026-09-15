using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// Enables precise actor/item resource drops on a ComboBox without changing
/// the options exposed by its existing ItemsSource.
/// </summary>
public static class ResourceSelectorDrop
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
        "Kind",
        typeof(ResourceSelectorKind),
        typeof(ResourceSelectorDrop),
        new PropertyMetadata(ResourceSelectorKind.None, KindChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(State),
        typeof(ResourceSelectorDrop));

    public static void SetKind(DependencyObject target, ResourceSelectorKind value)
        => target.SetValue(KindProperty, value);

    public static ResourceSelectorKind GetKind(DependencyObject target)
        => (ResourceSelectorKind)target.GetValue(KindProperty);

    /// <summary>Returns whether the payload resolves to an allowed option for the selector.</summary>
    public static bool CanApply(ComboBox? selector, IDataObject? data)
        => TryResolveOption(selector, data, out _);

    /// <summary>Applies a valid resource payload while retaining the selector binding.</summary>
    public static bool TryApply(ComboBox? selector, IDataObject? data)
    {
        if (!TryResolveOption(selector, data, out var option)) return false;

        // SetCurrentValue changes the current selection without replacing a
        // SelectedItem or SelectedValue binding. SelectedValue bindings are
        // therefore updated through the existing SelectedItem/Id projection.
        selector!.SetCurrentValue(Selector.SelectedItemProperty, option);
        return true;
    }

    private static void KindChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ComboBox selector) return;
        var state = (State?)selector.GetValue(StateProperty);
        var enabled = args.NewValue is ResourceSelectorKind.Actor or ResourceSelectorKind.Item;
        if (enabled && state is null)
        {
            state = new State(selector);
            selector.SetValue(StateProperty, state);
            selector.SetCurrentValue(UIElement.AllowDropProperty, true);
            selector.AddHandler(UIElement.DragOverEvent, state.DragOverHandler, handledEventsToo: true);
            selector.AddHandler(UIElement.DropEvent, state.DropHandler, handledEventsToo: true);
        }
        else if (!enabled && state is not null)
        {
            selector.RemoveHandler(UIElement.DragOverEvent, state.DragOverHandler);
            selector.RemoveHandler(UIElement.DropEvent, state.DropHandler);
            selector.SetCurrentValue(UIElement.AllowDropProperty, state.PreviousAllowDrop);
            selector.ClearValue(StateProperty);
        }
    }

    private static bool TryResolveOption(ComboBox? selector, IDataObject? data, out object? option)
    {
        option = null;
        if (selector is null || data is null || !selector.IsEnabled
            || !TryGetDraggedResource(data, out var resource)) return false;

        var kind = GetKind(selector);
        switch (kind)
        {
            case ResourceSelectorKind.Actor when resource is CanonicalStoryActorItem actor:
                option = selector.Items.OfType<CanonicalSessionSpeakerOption>()
                    .FirstOrDefault(candidate => candidate.IsResolved
                        && string.Equals(candidate.Id, actor.Id, StringComparison.Ordinal));
                break;
            case ResourceSelectorKind.Item when resource is CanonicalStoryItemItem item:
                option = selector.Items.OfType<CanonicalResourceSelectionOption>()
                    .FirstOrDefault(candidate => candidate.IsResolved
                        && string.Equals(candidate.Id, item.Id, StringComparison.Ordinal));
                break;
        }

        return option is not null;
    }

    private static bool TryGetDraggedResource(IDataObject data, out ICanonicalStoryTreeItem? resource)
    {
        resource = data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)
            ? data.GetData(CanonicalStoryWorkspaceView.ResourceDragFormat) as ICanonicalStoryTreeItem
            : null;
        return resource is not null;
    }

    private static bool IsResourcePayload(IDataObject data)
        => data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)
            && data.GetData(CanonicalStoryWorkspaceView.ResourceDragFormat) is ICanonicalStoryTreeItem;

    private sealed class State
    {
        public State(ComboBox selector)
        {
            PreviousAllowDrop = selector.AllowDrop;
            DragOverHandler = (_, args) => OnDragOver(selector, args);
            DropHandler = (_, args) => OnDrop(selector, args);
        }

        public DragEventHandler DragOverHandler { get; }
        public DragEventHandler DropHandler { get; }
        public bool PreviousAllowDrop { get; }
    }

    private static void OnDragOver(ComboBox selector, DragEventArgs args)
    {
        if (!IsResourcePayload(args.Data)) return;

        // Consume every canonical resource drag, including a rejected one, so
        // the graph surface cannot interpret it as a node/parameter drop.
        args.Effects = CanApply(selector, args.Data) ? DragDropEffects.Link : DragDropEffects.None;
        args.Handled = true;
    }

    private static void OnDrop(ComboBox selector, DragEventArgs args)
    {
        if (!IsResourcePayload(args.Data)) return;

        args.Effects = TryApply(selector, args.Data) ? DragDropEffects.Link : DragDropEffects.None;
        args.Handled = true;
    }
}

public enum ResourceSelectorKind
{
    None,
    Actor,
    Item,
}
