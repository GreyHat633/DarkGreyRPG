using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Reusable visual projection of the parallel Story-first workspace state.</summary>
public partial class CanonicalStoryWorkspaceView : UserControl
{
    private void Inspector_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (Workspace is not { } workspace || workspace.NodeInspector is not { } inspector) return;
        var node = workspace.ActiveGraphHost.Nodes.SingleOrDefault(n => n.NodeId == inspector.NodeId);
        if (node is null) return;
        var menu = FluentContextMenuFactory.Create((FrameworkElement)sender);
        WorkspaceGraph.AddParameterMenuItems(menu, node);
        menu.PlacementTarget = (UIElement)sender; menu.IsOpen = true; e.Handled = true;
    }

    private void EditActorPortrait_OnClick(object sender, RoutedEventArgs e) => Workspace?.EditActorPortrait();
    internal async void ImportLineAudio_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not CanonicalNodeInspectorViewModel inspector
            || Workspace?.MediaProjectDirectory is not { } projectRoot || !Workspace.IsWritableEditor(Workspace.ActiveEditor)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "音频文件|*.mp3;*.wav;*.ogg", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        button.IsEnabled = false;
        try
        {
            var tools = Services.StudioStoragePaths.Default.MediaTools;
            var store = new DarkGreyRPG.Studio.Core.Media.ProjectMediaStore(projectRoot,
                System.IO.Path.Combine(tools, "ffmpeg.exe"), System.IO.Path.Combine(tools, "ffprobe.exe"));
            var result = await store.ImportAudioAsync(dialog.FileName);
            if (inspector.IsMusic) inspector.SetMusic(result.MediaRef);
            else inspector.SetLineVoice(result.MediaRef);
        }
        catch (Exception exception) when (exception is System.IO.IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this), exception.Message, "音频导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { button.IsEnabled = true; }
    }

    internal void RemoveLineAudio_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CanonicalNodeInspectorViewModel inspector }) { if (inspector.IsMusic) inspector.SetMusic(null); else inspector.SetLineVoice(null); }
    }

    private static CanonicalNodeInspectorViewModel? VolumeInspector(object sender)
        => (sender as FrameworkElement)?.DataContext as CanonicalNodeInspectorViewModel;

    private void VolumeSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => VolumeInspector(sender)?.CommitVolumePreview();

    private void VolumeSlider_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => VolumeInspector(sender)?.CommitVolumePreview();

    private void VolumeSlider_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { VolumeInspector(sender)?.CommitVolumePreview(); e.Handled = true; }
        else if (e.Key == Key.Escape) { VolumeInspector(sender)?.CommitVolumePreview(false); e.Handled = true; }
    }

    internal const string ResourceDragFormat = "DarkGreyRPG.Studio.CanonicalStoryGraphItem";
    private static readonly Vector ResourceNodePointerAnchor = new(116d, 46d);
    private Func<string?> _placementNodeIdSource = NextPlacementNodeId;
    private Point _resourceDragStart;
    private ICanonicalStoryTreeItem? _resourceDragItem;
    private bool _resourceDragReleased;
    private OutputReorderPreview? _resourceReorderPreview;
    private ItemsControl? _resourceReorderList;
    private ICanonicalStoryTreeItem[]? _resourceReorderItems;
    private AdornerLayer? _resourceReorderLayer;
    private int _resourceReorderOldIndex;
    private DispatcherTimer? _resourceReorderTimer;
    private long _appliedStoryNodeFocusSequence;
    private bool _storyNodeFocusQueued;
    private Func<CanonicalChoiceOptionRemovalConfirmation, bool>? _choiceOptionRemovalConfirmation;
    private Func<CanonicalStoryStartTriggerRemovalConfirmation, bool>? _storyStartTriggerRemovalConfirmation;

    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(
        nameof(Workspace),
        typeof(CanonicalStoryWorkspaceViewModel),
        typeof(CanonicalStoryWorkspaceView),
        new PropertyMetadata(null, OnWorkspaceChanged));

    public CanonicalStoryWorkspaceView()
    {
        InitializeComponent();
        WorkspaceGraph.InlineEditorFactory = CreateInlineEditor;
        WorkspaceGraph.SelectionChanged += WorkspaceGraph_OnSelectionChanged;
        WorkspaceGraph.NodeEditRequested += WorkspaceGraph_OnNodeEditRequested;
        Unloaded += (_, _) => { CancelResourceDragPreview(); ClearResourceReorderPreview(); Workspace?.ClearParameterDropMessage(); };
    }

    public CanonicalStoryWorkspaceView(CanonicalStoryWorkspaceViewModel workspace)
        : this()
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public CanonicalStoryWorkspaceView(CanonicalStoryWorkspaceViewModel workspace, Func<string?> placementNodeIdSource)
        : this(workspace)
    {
        _placementNodeIdSource = placementNodeIdSource ?? throw new ArgumentNullException(nameof(placementNodeIdSource));
    }

    public CanonicalStoryWorkspaceViewModel? Workspace
    {
        get => (CanonicalStoryWorkspaceViewModel?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    public CanonicalGraphEditorView GraphView => WorkspaceGraph;
    public bool IsResourceDragGhostVisible => ResourceDragGhost.Visibility == Visibility.Visible;
    public string ResourceDragGhostDisplayName => ResourceDragGhostTitle.Text;
    public Point ResourceDragGhostViewportPosition =>
        new(Canvas.GetLeft(ResourceDragGhost), Canvas.GetTop(ResourceDragGhost));
    public Vector ResourceDragGhostPointerOffset => ResourceNodePointerAnchor;
    public string ResourceReorderPreviewPlacement => _resourceReorderPreview is null ? string.Empty : "animated";

    /// <summary>
    /// Optional test/host injection for Choice-option removal confirmation.
    /// When unset, the live WPF MessageBox is owner-bound to this view's
    /// containing Window.
    /// </summary>
    public Func<CanonicalChoiceOptionRemovalConfirmation, bool>? ChoiceOptionRemovalConfirmation
    {
        get => _choiceOptionRemovalConfirmation;
        set
        {
            _choiceOptionRemovalConfirmation = value;
            ApplyRemovalConfirmations();
        }
    }


    public Func<CanonicalStoryStartTriggerRemovalConfirmation, bool>? StoryStartTriggerRemovalConfirmation
    {
        get => _storyStartTriggerRemovalConfirmation;
        set
        {
            _storyStartTriggerRemovalConfirmation = value;
            ApplyRemovalConfirmations();
        }
    }

    public bool SelectResourceItem(ICanonicalStoryTreeItem? item)
    {
        // Resource selection owns the Inspector. Clear the visual graph selection
        // as well so clicking that same node later emits a fresh selection event.
        WorkspaceGraph.ClearSelection();
        return Workspace?.SelectTreeItem(item) == true;
    }

    public bool ActivateResourceItem(ICanonicalStoryTreeItem? item)
    {
        if (Workspace is null || item is null || !SelectResourceItem(item)) return false;
        return item is CanonicalStoryGraphItem graph && Workspace.OpenGraphResource(graph);
    }

    public bool ActivateSelectedResource() => Workspace?.OpenSelectedResource() == true;

    private void DraftTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { AcceptsReturn: false } textBox) return;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    public bool ReturnToStory() => Workspace?.ReturnToStory() == true;

    public bool ApplyPendingStoryNodeFocus()
    {
        var workspace = Workspace;
        var request = workspace?.StoryNodeFocusRequest;
        if (workspace is null || request is null) return false;
        if (_appliedStoryNodeFocusSequence == request.Sequence) return true;
        if (workspace.ActiveEditor.Id != (request.ResourceId ?? workspace.StoryEditor.Id)
            || !ReferenceEquals(WorkspaceGraph.Host, workspace.ActiveGraphHost))
            return false;
        if (!WorkspaceGraph.SelectNode(request.NodeId)) return false;
        _appliedStoryNodeFocusSequence = request.Sequence;
        return true;
    }

    public bool SelectResourceFolder(CanonicalStoryFolderKind folderKind)
        => Workspace?.SelectFolder(folderKind) == true;

    public bool RequestCreateResource(CanonicalStoryFolderKind folderKind)
        => Workspace?.RequestCreate(folderKind) == true;

    public bool RequestReferenceResource(CanonicalStoryFolderKind folderKind)
        => Workspace?.RequestReference(folderKind) == true;

    public bool RequestDeleteResource(ICanonicalStoryTreeItem item)
        => Workspace?.RequestDelete(item) == true;

    public bool CanPlaceResource(ICanonicalStoryTreeItem? item)
        => item is CanonicalStoryGraphItem graph && Workspace?.CanPlaceAggregate(graph) == true;

    public bool PlaceResourceAt(ICanonicalStoryTreeItem? item, double graphX, double graphY)
    {
        if (item is not CanonicalStoryGraphItem graph || Workspace is null || !Workspace.CanPlaceAggregate(graph))
            return false;

        string? placementNodeId;
        try
        {
            placementNodeId = _placementNodeIdSource();
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            _ = exception;
            return false;
        }

        if (!Workspace.PlaceAggregate(graph, placementNodeId, graphX, graphY))
            return false;
        _ = WorkspaceGraph.SelectNode(placementNodeId);
        return true;
    }

    public bool PreviewResourceDrag(ICanonicalStoryTreeItem? item, Point viewportPoint)
    {
        if (item is not CanonicalStoryGraphItem graph
            || !CanPlaceResource(graph)
            || !WorkspaceGraph.ContainsViewportPoint(viewportPoint))
        {
            CancelResourceDragPreview();
            return false;
        }

        ResourceDragGhostTitle.Text = graph.DisplayName;
        var topLeft = ResourceNodeTopLeftFromPointer(viewportPoint);
        Canvas.SetLeft(ResourceDragGhost, topLeft.X);
        Canvas.SetTop(ResourceDragGhost, topLeft.Y);
        ResourceDragGhost.Visibility = Visibility.Visible;
        return true;
    }

    public void CancelResourceDragPreview()
    {
        ResourceDragGhost.Visibility = Visibility.Collapsed;
        ResourceDragGhostTitle.Text = string.Empty;
    }

    public static Point ResourceNodeTopLeftFromPointer(Point viewportPoint)
        => viewportPoint - ResourceNodePointerAnchor;

    public Point ResourceGraphPositionFromPointer(Point viewportPoint)
        => WorkspaceGraph.ScreenToGraph(ResourceNodeTopLeftFromPointer(viewportPoint));

    private CanonicalNodeInspectorViewModel? CreateInlineEditor(GraphEditorNodeViewModel node)
    {
        var workspace = Workspace;
        if (workspace is null || !workspace.ActiveGraphHost.Nodes.Contains(node)) return null;
        var inspector = new CanonicalNodeInspectorViewModel(
            workspace.ActiveGraphHost, node, workspace.ActorItems, workspace.ItemItems,
            subscribeToHostChanges: false);
        workspace.ConfigurePublicOutputs(inspector);
        ConfigureRemovalConfirmations(inspector);
        return inspector;
    }

    private void ResourceItem_OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ICanonicalStoryTreeItem item })
            _ = SelectResourceItem(item);
    }

    private void ResourceItem_OnMouseDoubleClick(object sender, MouseButtonEventArgs args)
    {
        if (sender is Button { Tag: ICanonicalStoryTreeItem item } && ActivateResourceItem(item))
            args.Handled = true;
    }

    private void ResourceSidebar_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        var source = args.OriginalSource as DependencyObject;
        while (source is not null && !ReferenceEquals(source, sender))
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or TextBox
                or System.Windows.Controls.Primitives.ScrollBar) return;
            source = LinePagesEditor.InputParent(source);
        }
        Workspace?.ClearGraphSelection();
    }

    private void ResourceItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        _resourceDragStart = args.GetPosition(this);
        _resourceDragItem = sender is Button { Tag: ICanonicalStoryTreeItem item }
            && (item is CanonicalStoryGraphItem or CanonicalStoryActorItem or CanonicalStoryItemItem)
                ? item
                : null;
    }

    private void ResourceItem_OnPreviewMouseMove(object sender, MouseEventArgs args)
    {
        if (args.LeftButton != MouseButtonState.Pressed || _resourceDragItem is not { } item)
            return;
        var current = args.GetPosition(this);
        if (Math.Abs(current.X - _resourceDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _resourceDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _resourceDragItem = null;
        Workspace?.ClearParameterDropMessage();
        var data = new DataObject(ResourceDragFormat, item);
        var effect = DragDropEffects.None;
        var source = (DependencyObject)sender;
        _resourceDragReleased = false;
        var dragWorkspace = Workspace;
        QueryContinueDragEventHandler observeRelease = (_, query) =>
        {
            _resourceDragReleased = !query.EscapePressed && (query.KeyStates & DragDropKeyStates.LeftMouseButton) == 0;
            if (query.EscapePressed || !IsLoaded || !IsVisible || !ReferenceEquals(Workspace, dragWorkspace)
                || Window.GetWindow(this) is { IsActive: false })
            { query.Action = DragAction.Cancel; query.Handled = true; }
        };
        DragDrop.AddQueryContinueDragHandler(source, observeRelease);
        try
        {
            effect = DragDrop.DoDragDrop(source, data, DragDropEffects.Link | DragDropEffects.Move);
        }
        finally
        {
            DragDrop.RemoveQueryContinueDragHandler(source, observeRelease);
            CancelResourceDragPreview();
            ClearResourceReorderPreview();
            DynamicContentEditor.NotifyResourceDragFinished(effect != DragDropEffects.None);
        }
        // WPF does not raise Drop when DragOver returned None. Keep the rejected
        // preview's short feedback on release, and clear it when the drag is cancelled.
        if (effect == DragDropEffects.None && item is CanonicalStoryActorItem or CanonicalStoryItemItem
            && Workspace is { } workspace)
        {
            if (_resourceDragReleased && Window.GetWindow(this)?.IsActive == true && workspace.HasParameterDropMessage)
                workspace.RestartParameterDropMessageTimeout();
            else workspace.ClearParameterDropMessage();
        }
        _resourceDragReleased = false;
        args.Handled = true;
    }

    private void ResourceItem_OnDragOver(object sender, DragEventArgs args)
    {
        var valid = sender is Button button && TryGetDraggedResource(args.Data, out var item) && item is not null
            && LinePagesEditor.Ancestor<ItemsControl>(button) is { } list && PreviewResourceReorder(list, item, args);
        args.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        if (!valid) ClearResourceReorderPreview();
        args.Handled = true;
    }

    private void ResourceList_OnDragOver(object sender, DragEventArgs args)
    {
        var valid = sender is ItemsControl list && TryGetDraggedResource(args.Data, out var item) && item is not null
            && PreviewResourceReorder(list, item, args);
        args.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        if (!valid) ClearResourceReorderPreview();
        args.Handled = true;
    }

    private bool PreviewResourceReorder(ItemsControl list, ICanonicalStoryTreeItem source, DragEventArgs args)
    {
        if (!list.Items.Contains(source) || Workspace is null) return false;
        var items = list.Items.Cast<ICanonicalStoryTreeItem>().ToArray();
        if (items.Length < 2) return false;
        if (_resourceReorderList != list || _resourceReorderItems is null || !items.SequenceEqual(_resourceReorderItems))
        {
            ClearResourceReorderPreview();
            var index = Array.IndexOf(items, source);
            var layer = AdornerLayer.GetAdornerLayer(list);
            if (index < 0 || layer is null) return false;
            _resourceReorderList = list; _resourceReorderItems = items; _resourceReorderOldIndex = index;
            _resourceReorderLayer = layer;
            _resourceReorderPreview = new OutputReorderPreview(list, source.DisplayName, false, index, TranslatePoint(_resourceDragStart, list));
            layer.Add(_resourceReorderPreview);
            _resourceReorderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _resourceReorderTimer.Tick += (_, _) =>
            {
                if (_resourceReorderList is not { } active || _resourceReorderPreview is null) return;
                var scroll = LinePagesEditor.Ancestor<ScrollViewer>(active);
                if (scroll is null) return;
                var point = Mouse.GetPosition(scroll);
                if (point.X < 0 || point.X > scroll.ActualWidth) return;
                var delta = point.Y < 30 ? -12 : point.Y > scroll.ActualHeight - 30 ? 12 : 0;
                if (delta == 0) return;
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + delta); scroll.UpdateLayout();
                _resourceReorderPreview.Locate(Mouse.GetPosition(active));
            };
            _resourceReorderTimer.Start();
        }
        return _resourceReorderPreview!.Locate(args.GetPosition(list)).HasValue;
    }

    private void ResourceItem_OnDragLeave(object sender, DragEventArgs args)
    {
        if (_resourceReorderList is { } list)
        {
            var point = args.GetPosition(list);
            if (point.X < 0 || point.X > list.ActualWidth || point.Y < 0 || point.Y > list.ActualHeight)
                ClearResourceReorderPreview();
        }
        args.Handled = true;
    }

    private void ResourceItem_OnDrop(object sender, DragEventArgs args)
    {
        var index = _resourceReorderList is { } list ? _resourceReorderPreview?.Locate(args.GetPosition(list)) : null;
        var items = _resourceReorderItems;
        var oldIndex = _resourceReorderOldIndex;
        ClearResourceReorderPreview();
        args.Effects = DragDropEffects.None;
        if (items is not null && index is { } destination && TryGetDraggedResource(args.Data, out var source)
            && source is not null && ReferenceEquals(items[oldIndex], source))
        {
            if (destination == oldIndex) args.Effects = DragDropEffects.Move;
            else
            {
                var remaining = items.Where(item => !ReferenceEquals(item, source)).ToArray();
                var after = destination == remaining.Length;
                var target = after ? remaining[^1] : remaining[destination];
                if (Workspace?.ReorderResource(source, target, after) == true) args.Effects = DragDropEffects.Move;
            }
        }
        args.Handled = true;
    }

    private void ClearResourceReorderPreview()
    {
        _resourceReorderTimer?.Stop(); _resourceReorderTimer = null;
        _resourceReorderPreview?.Dispose();
        if (_resourceReorderPreview is not null) _resourceReorderLayer?.Remove(_resourceReorderPreview);
        _resourceReorderPreview = null; _resourceReorderList = null; _resourceReorderItems = null; _resourceReorderLayer = null;
    }

    private void WorkspaceGraph_OnDragOver(object sender, DragEventArgs args)
    {
        var point = args.GetPosition(WorkspaceGraph.ViewportElement);
        args.Effects = DragDropEffects.None;
        if (TryGetDraggedResource(args.Data, out var item) && item is not null)
        {
            if (item is CanonicalStoryGraphItem)
                args.Effects = PreviewResourceDrag(item, point) ? DragDropEffects.Link : DragDropEffects.None;
            else
            {
                CancelResourceDragPreview();
                var node = WorkspaceGraph.NodeAtViewportPoint(point);
                bool accepted = Workspace?.CanApplyResourceToNodeParameter(node, item) == true;
                args.Effects = accepted
                    ? DragDropEffects.Link
                    : DragDropEffects.None;
                if (!accepted) Workspace?.ApplyResourceToNodeParameter(node, item);
            }
        }
        args.Handled = true;
    }

    private void WorkspaceGraph_OnDragLeave(object sender, DragEventArgs args)
    {
        CancelResourceDragPreview();
        if (!_resourceDragReleased) Workspace?.ClearParameterDropMessage();
        args.Handled = true;
    }

    private void WorkspaceGraph_OnDrop(object sender, DragEventArgs args)
    {
        var point = args.GetPosition(WorkspaceGraph.ViewportElement);
        CancelResourceDragPreview();
        if (!TryGetDraggedResource(args.Data, out var item)
            || !WorkspaceGraph.ContainsViewportPoint(point))
        {
            args.Effects = DragDropEffects.None;
            args.Handled = true;
            return;
        }

        if (item is CanonicalStoryGraphItem)
        {
            var graphPoint = ResourceGraphPositionFromPointer(point);
            args.Effects = PlaceResourceAt(item, graphPoint.X, graphPoint.Y)
                ? DragDropEffects.Link
                : DragDropEffects.None;
        }
        else
        {
            var node = WorkspaceGraph.NodeAtViewportPoint(point);
            args.Effects = Workspace?.ApplyResourceToNodeParameter(node, item) == true
                ? DragDropEffects.Link
                : DragDropEffects.None;
        }
        args.Handled = true;
    }

    private void ResourceItem_OnKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && sender is Button { Tag: ICanonicalStoryTreeItem item }
            && ActivateResourceItem(item))
            args.Handled = true;
    }

    private void Folder_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is FrameworkElement { Tag: CanonicalStoryFolderViewModel folder })
            _ = SelectResourceFolder(folder.Kind);
    }

    private void Folder_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: CanonicalStoryFolderViewModel folder } target
            || Workspace is null)
            return;

        _ = SelectResourceFolder(folder.Kind);
        var menu = FluentContextMenuFactory.Create(target);
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            $"新建{folder.DisplayName}",
            () => RequestCreateResource(folder.Kind),
            Workspace.CreateSelectedResourceCommand.CanExecute(null)));
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            $"引用已有{folder.DisplayName}",
            () => RequestReferenceResource(folder.Kind),
            Workspace.ReferenceSelectedResourceCommand.CanExecute(null)));
        menu.IsOpen = true;
        args.Handled = true;
    }

    private void ResourceItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is not Button { Tag: ICanonicalStoryTreeItem item } target || Workspace is null)
            return;

        _ = SelectResourceItem(item);
        var referenced = item switch
        {
            CanonicalStoryActorItem actor => actor.IsReferenced,
            CanonicalStoryItemItem itemResource => itemResource.IsReferenced,
            CanonicalStoryGraphItem graph => graph.IsReferenced,
            CanonicalStoryMissingItem missing => missing.IsReferenced,
            _ => false,
        };
        var folderKind = item switch
        {
            CanonicalStoryItemItem => CanonicalStoryFolderKind.Items,
            CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Session } => CanonicalStoryFolderKind.Sessions,
            CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Task } => CanonicalStoryFolderKind.Tasks,
            CanonicalStoryMissingItem missing => missing.FolderKind,
            _ => CanonicalStoryFolderKind.Actors,
        };
        var label = folderKind switch
        {
            CanonicalStoryFolderKind.Actors => "角色",
            CanonicalStoryFolderKind.Items => "物品",
            CanonicalStoryFolderKind.Sessions => "会话",
            CanonicalStoryFolderKind.Tasks => "任务",
            _ => throw new ArgumentOutOfRangeException(nameof(folderKind), folderKind, null),
        };
        var isReadOnly = item is CanonicalStoryActorItem { IsReadOnly: true }
            or CanonicalStoryItemItem { IsReadOnly: true } or CanonicalStoryGraphItem { IsReadOnly: true };
        var menu = FluentContextMenuFactory.Create(target);
        if (item is CanonicalStoryGraphItem)
            menu.Items.Add(FluentContextMenuFactory.CreateItem(
                ((CanonicalStoryGraphItem)item).ResourceKind == GraphResourceKind.Session ? "会话图" : "任务图",
                () => _ = ActivateResourceItem(item)));
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            "编辑",
            () => _ = Workspace.RequestRename(item),
            item is not CanonicalStoryMissingItem && !isReadOnly));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            $"新建{label}",
            () => RequestCreateResource(folderKind),
            Workspace.CreateSelectedResourceCommand.CanExecute(null)));
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            $"引用已有{label}",
            () => RequestReferenceResource(folderKind),
            Workspace.ReferenceSelectedResourceCommand.CanExecute(null)));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            referenced ? "解除引用" : "删除资源",
            () => RequestDeleteResource(item),
            Workspace.DeleteSelectedResourceCommand.CanExecute(null),
            critical: !referenced));
        target.ContextMenu = menu;
        menu.IsOpen = true;
        args.Handled = true;
    }

    private void Breadcrumb_OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CanonicalStoryBreadcrumb breadcrumb })
            _ = Workspace?.ActivateBreadcrumb(breadcrumb);
    }

    private static bool TryGetDraggedResource(IDataObject data, out ICanonicalStoryTreeItem? item)
    {
        item = data.GetDataPresent(ResourceDragFormat)
            ? data.GetData(ResourceDragFormat) as ICanonicalStoryTreeItem
            : null;
        return item is not null;
    }

    private static string NextPlacementNodeId() => $"aggregate_{Guid.NewGuid():N}";

    private static void OnWorkspaceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var view = (CanonicalStoryWorkspaceView)dependencyObject;
        if (args.OldValue is CanonicalStoryWorkspaceViewModel oldWorkspace)
            oldWorkspace.PropertyChanged -= view.Workspace_OnPropertyChanged;
        if (args.NewValue is CanonicalStoryWorkspaceViewModel newWorkspace)
            newWorkspace.PropertyChanged += view.Workspace_OnPropertyChanged;
        view._appliedStoryNodeFocusSequence = 0;
        view.QueueSearchFocus();
        view.QueueStoryNodeFocus();
    }

    private void Workspace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CanonicalStoryWorkspaceViewModel.SearchTarget)) QueueSearchFocus();
        if (args.PropertyName == nameof(CanonicalStoryWorkspaceViewModel.ActiveGraphHost))
            Workspace?.ClearGraphSelection();
        if (args.PropertyName == nameof(CanonicalStoryWorkspaceViewModel.NodeInspector))
            ApplyRemovalConfirmations();
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActorItems)
            or nameof(CanonicalStoryWorkspaceViewModel.ItemItems))
        {
            if (Workspace is not { } workspace) return;
            foreach (var visual in WorkspaceGraph.NodeVisuals)
                visual.InlineEditor?.UpdateResourceOptions(workspace.ActorItems, workspace.ItemItems);
            workspace.NodeInspector?.UpdateResourceOptions(workspace.ActorItems, workspace.ItemItems);
        }
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.StoryNodeFocusRequest)
            or nameof(CanonicalStoryWorkspaceViewModel.ActiveGraphHost))
            QueueStoryNodeFocus();
    }

    private void ApplyRemovalConfirmations()
    {
        if (Workspace?.NodeInspector is { } inspector)
            ConfigureRemovalConfirmations(inspector);
        foreach (var inline in WorkspaceGraph.NodeVisuals
                     .Select(visual => visual.InlineEditor)
                     .Where(editor => editor is not null))
            ConfigureRemovalConfirmations(inline!);
    }

    private void ConfigureRemovalConfirmations(CanonicalNodeInspectorViewModel inspector)
    {
        inspector.ChoiceOptionRemovalConfirmationRequested =
            _choiceOptionRemovalConfirmation ?? ShowChoiceOptionRemovalConfirmation;
        inspector.StoryStartTriggerRemovalConfirmationRequested =
            _storyStartTriggerRemovalConfirmation ?? ShowStoryStartTriggerRemovalConfirmation;
        inspector.CanEditLogicInputs = () => Workspace?.IsActiveGraphReadOnly == false;
        inspector.LogicInputRemovalConfirmationRequested = (name, count) => MessageBox.Show(Window.GetWindow(this),
            $"删除输入“{name}”将同时移除 {count} 条连接。确定继续吗？", "确认删除逻辑输入",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool ShowChoiceOptionRemovalConfirmation(CanonicalChoiceOptionRemovalConfirmation confirmation)
        => MessageBox.Show(
            Window.GetWindow(this),
            $"删除选项“{confirmation.DisplayText}”将同时移除它的 Flow 和 Logic 引用/连接。\n确定继续吗？",
            "确认删除选择项",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;


    private bool ShowStoryStartTriggerRemovalConfirmation(CanonicalStoryStartTriggerRemovalConfirmation confirmation)
        => MessageBox.Show(
            Window.GetWindow(this),
            $"删除启动条件“{confirmation.DisplayName}”将同时移除它的 Flow 和 Logic 引用/连接。\n确定继续吗？",
            "确认删除启动条件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    private GraphSelectionChangedEventArgs? _pendingMarqueeSelection;
    private bool _marqueeInspectorQueued;
    private void WorkspaceGraph_OnSelectionChanged(object? sender, GraphSelectionChangedEventArgs args)
    {
        if (Workspace is null) return;
        if (!args.IsMarquee && args.Kind is not (GraphSelectionKind.Node or GraphSelectionKind.MultipleNodes))
        {
            _pendingMarqueeSelection = null;
            Workspace.ClearGraphSelection();
            return;
        }
        if (args.IsMarquee)
        {
            _pendingMarqueeSelection = args;
            if (_marqueeInspectorQueued) return;
            _marqueeInspectorQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _marqueeInspectorQueued = false;
                var latest = _pendingMarqueeSelection; _pendingMarqueeSelection = null;
                if (latest is not null) ApplyInspectorSelection(latest);
            }));
        }
        else { _pendingMarqueeSelection = null; ApplyInspectorSelection(args); }
    }
    private void ApplyInspectorSelection(GraphSelectionChangedEventArgs args)
    {
        if (Workspace is null) return;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        int before = Workspace.InspectorCreationCount;
        Workspace.SelectGraphNodes(args.Kind == GraphSelectionKind.Node && args.Node is not null ? [args.Node]
            : args.Kind == GraphSelectionKind.MultipleNodes ? args.Nodes : []);
        SelectionDiagnostics.Record(args.IsMarquee, Workspace.InspectorTitle, before, Workspace.InspectorCreationCount, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private void WorkspaceGraph_OnNodeEditRequested(GraphEditorNodeViewModel node)
    {
        if (Workspace is null || !Workspace.IsStoryFlowActive
            || !node.Properties.TryGetValue("resource_id", out var resourceId)
            || resourceId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(resourceId.GetString()))
            return;

        var id = resourceId.GetString()!;
        var item = node.Type switch
        {
            "session" => Workspace.SessionItems.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.Ordinal)),
            "task" => Workspace.TaskItems.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.Ordinal)),
            _ => null,
        };
        if (item is not null) _ = Workspace.OpenGraphResource(item);
    }

    private void QueueStoryNodeFocus()
    {
        if (_storyNodeFocusQueued || Workspace?.StoryNodeFocusRequest is null) return;
        _storyNodeFocusQueued = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _storyNodeFocusQueued = false;
            if (!ApplyPendingStoryNodeFocus() && IsLoaded)
                _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => _ = ApplyPendingStoryNodeFocus()));
        }));
    }
}
