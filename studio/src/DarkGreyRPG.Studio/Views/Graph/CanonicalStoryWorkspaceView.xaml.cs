using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Reusable visual projection of the parallel Story-first workspace state.</summary>
public partial class CanonicalStoryWorkspaceView : UserControl
{
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
            var tools = System.IO.Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
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
    private void AudioPreview_ClearRequested(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CanonicalNodeInspectorViewModel inspector })
        { if (inspector.IsMusic) inspector.SetMusic(null); else inspector.SetLineVoice(null); }
    }

    internal const string ResourceDragFormat = "DarkGreyRPG.Studio.CanonicalStoryGraphItem";
    private static readonly Vector ResourceNodePointerAnchor = new(116d, 46d);
    private Func<string?> _placementNodeIdSource = NextPlacementNodeId;
    private Point _resourceDragStart;
    private ICanonicalStoryTreeItem? _resourceDragItem;
    private Button? _resourceReorderPreviewButton;
    private bool _resourceReorderInsertAfter;
    private long _appliedStoryNodeFocusSequence;
    private bool _storyNodeFocusQueued;
    private Func<CanonicalChoiceOptionRemovalConfirmation, bool>? _choiceOptionRemovalConfirmation;
    private Func<CanonicalTaskResultSlotRemovalConfirmation, bool>? _taskResultSlotRemovalConfirmation;
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
    public string ResourceReorderPreviewPlacement => _resourceReorderPreviewButton is null
        ? string.Empty : _resourceReorderInsertAfter ? "after" : "before";

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

    public Func<CanonicalTaskResultSlotRemovalConfirmation, bool>? TaskResultSlotRemovalConfirmation
    {
        get => _taskResultSlotRemovalConfirmation;
        set
        {
            _taskResultSlotRemovalConfirmation = value;
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
        if (!workspace.IsStoryFlowActive || !ReferenceEquals(WorkspaceGraph.Host, workspace.StoryEditor.Host))
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

    private void ResourceItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        _resourceDragStart = args.GetPosition(this);
        _resourceDragItem = sender is Button { Tag: ICanonicalStoryTreeItem item }
            && (CanPlaceResource(item) || item is CanonicalStoryActorItem or CanonicalStoryItemItem)
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
        var data = new DataObject(ResourceDragFormat, item);
        try
        {
            _ = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Link | DragDropEffects.Move);
        }
        finally
        {
            CancelResourceDragPreview();
            ClearResourceReorderPreview();
        }
        args.Handled = true;
    }

    private void ResourceItem_OnDragOver(object sender, DragEventArgs args)
    {
        var button = sender as Button;
        var target = button?.Tag as ICanonicalStoryTreeItem;
        var valid = button is not null && target is not null
            && TryGetDraggedResource(args.Data, out var item) && item is not null
            && Workspace?.CanReorderResource(item, target!) == true;
        args.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        if (valid)
        {
            var insertAfter = args.GetPosition(button!).Y >= Math.Max(1d, button!.ActualHeight) / 2d;
            ShowResourceReorderPreview(button!, insertAfter);
        }
        else ClearResourceReorderPreview();
        args.Handled = true;
    }

    private void ResourceItem_OnDragLeave(object sender, DragEventArgs args)
    {
        if (ReferenceEquals(sender, _resourceReorderPreviewButton)) ClearResourceReorderPreview();
        args.Handled = true;
    }

    private void ResourceItem_OnDrop(object sender, DragEventArgs args)
    {
        var insertAfter = _resourceReorderInsertAfter;
        ClearResourceReorderPreview();
        args.Effects = sender is Button { Tag: ICanonicalStoryTreeItem target }
            && TryGetDraggedResource(args.Data, out var item)
            && item is not null
            && Workspace?.ReorderResource(item, target, insertAfter) == true
                ? DragDropEffects.Move
                : DragDropEffects.None;
        args.Handled = true;
    }

    private void ShowResourceReorderPreview(Button button, bool insertAfter)
    {
        if (!ReferenceEquals(_resourceReorderPreviewButton, button)) ClearResourceReorderPreview();
        _resourceReorderPreviewButton = button;
        _resourceReorderInsertAfter = insertAfter;
        button.BorderBrush = (Brush)FindResource("AccentFillColorDefaultBrush");
        button.BorderThickness = insertAfter ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
    }

    private void ClearResourceReorderPreview()
    {
        if (_resourceReorderPreviewButton is not null)
        {
            _resourceReorderPreviewButton.ClearValue(Control.BorderBrushProperty);
            _resourceReorderPreviewButton.ClearValue(Control.BorderThicknessProperty);
        }
        _resourceReorderPreviewButton = null;
        _resourceReorderInsertAfter = false;
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
                args.Effects = WorkspaceGraph.NodeAtViewportPoint(point) is not null
                    ? DragDropEffects.Link
                    : DragDropEffects.None;
            }
        }
        args.Handled = true;
    }

    private void WorkspaceGraph_OnDragLeave(object sender, DragEventArgs args)
    {
        CancelResourceDragPreview();
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
        view.QueueStoryNodeFocus();
    }

    private void Workspace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CanonicalStoryWorkspaceViewModel.ActiveGraphHost))
            Workspace?.ClearGraphSelection();
        if (args.PropertyName == nameof(CanonicalStoryWorkspaceViewModel.NodeInspector))
            ApplyRemovalConfirmations();
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActorItems)
            or nameof(CanonicalStoryWorkspaceViewModel.ItemItems))
        {
            WorkspaceGraph.ClearSelection();
            WorkspaceGraph.RefreshInlineEditors();
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
        inspector.TaskResultSlotRemovalConfirmationRequested =
            _taskResultSlotRemovalConfirmation ?? ShowTaskResultSlotRemovalConfirmation;
        inspector.StoryStartTriggerRemovalConfirmationRequested =
            _storyStartTriggerRemovalConfirmation ?? ShowStoryStartTriggerRemovalConfirmation;
    }

    private bool ShowChoiceOptionRemovalConfirmation(CanonicalChoiceOptionRemovalConfirmation confirmation)
        => MessageBox.Show(
            Window.GetWindow(this),
            $"删除选项“{confirmation.DisplayText}”将同时移除它的 Flow 和 Logic 引用/连接。\n确定继续吗？",
            "确认删除选择项",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    private bool ShowTaskResultSlotRemovalConfirmation(CanonicalTaskResultSlotRemovalConfirmation confirmation)
        => MessageBox.Show(
            Window.GetWindow(this),
            $"删除结果“{confirmation.DisplayName}”将同时移除它的 Flow 和 Logic 引用/连接。\n确定继续吗？",
            "确认删除任务结果",
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

    private void WorkspaceGraph_OnSelectionChanged(object? sender, GraphSelectionChangedEventArgs args)
    {
        if (Workspace is null) return;
        if (args.Kind == GraphSelectionKind.Node)
            _ = Workspace.SelectGraphNode(args.Node);
        else
            Workspace.ClearGraphSelection();
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
