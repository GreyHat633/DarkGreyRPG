using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalGraphEditorView
{
    private readonly List<FrameworkElement> _frameVisuals = [];
    private readonly Dictionary<string, (Brush Border, Brush Fill)> _groupColors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _groupBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect[]> _groupLayoutInputs = new(StringComparer.Ordinal);
    private Dictionary<string, Rect> _groupDragBounds = new(StringComparer.Ordinal);
    private bool _gPressed, _gUsedForDrag;
    private string? _groupDropTarget;
    private Thumb? _activeGroupThumb;
    private readonly HashSet<string> _hiddenGroupMembers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _groupCollapsedStates = new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> SelectedGroups => _selectedGroups;

    private void CreateCommentFrame(Point point) => CombineSelected();
    private void Group_OnClick(object sender, RoutedEventArgs e) { CombineSelected(); e.Handled = true; }
    public bool CombineSelected()
    {
        if (_host is null || IsReadOnly || _host.Scope == Core.Graphs.Definitions.GraphScope.Project) return false;
        var result = _host.CombineSelection(_selectedNodes.Select(n => n.NodeId), _selectedGroups);
        _selectedGroups.IntersectWith(_host.Frames.Select(f => f.Id));
        DrawCommentFrames();
        return result;
    }
    public bool SelectGroup(string id, bool additive = false)
    {
        if (_host is null || !_host.Frames.Any(f => f.Id == id)) return false;
        if (!additive) ClearSelection();
        if (additive && _selectedGroups.Contains(id)) _selectedGroups.Remove(id); else _selectedGroups.Add(id);
        DrawCommentFrames();
        return true;
    }

    private void DrawCommentFrames()
    {
        if (_host is null) return;
        var frames = _host.FrameSnapshot();
        var hiddenMembers = frames.Where(frame => frame.Collapsed).SelectMany(frame => frame.Members).ToHashSet(StringComparer.Ordinal);
        if (hiddenMembers.Count != 0 || _hiddenGroupMembers.Count != 0)
        {
            foreach (var pair in _nodeVisuals)
                pair.Value.Visibility = hiddenMembers.Contains(pair.Key.NodeId) ? Visibility.Hidden : Visibility.Visible;
            foreach (var pair in _connectionVisuals)
            {
                var edge = pair.Key.Connection;
                var visibility = hiddenMembers.Contains(edge.FromNodeId) || hiddenMembers.Contains(edge.ToNodeId) ? Visibility.Hidden : Visibility.Visible;
                pair.Value.Line.Visibility = visibility; pair.Value.Hit.Visibility = visibility;
            }
            _hiddenGroupMembers.Clear();
            _hiddenGroupMembers.UnionWith(hiddenMembers);
        }
        if (frames.Count == 0 && _frameVisuals.Count == 0) return;
        var alive = frames.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        _selectedGroups.IntersectWith(alive);
        foreach (var dead in _frameVisuals.Where(v => v.Tag is not string id || !alive.Contains(id)).ToArray())
        { GraphCanvas.Children.Remove(dead); _frameVisuals.Remove(dead); }
        foreach (var dead in _groupBounds.Keys.Where(id => !alive.Contains(id)).ToArray())
        { _groupBounds.Remove(dead); _groupLayoutInputs.Remove(dead); _groupCollapsedStates.Remove(dead); }
        var nodeBounds = _host.Nodes.Where(n => _nodeVisuals.ContainsKey(n) && !string.IsNullOrWhiteSpace(n.NodeId))
            .GroupBy(n => n.NodeId, StringComparer.Ordinal).Where(group => group.Count() == 1)
            .Select(group => group.First()).ToDictionary(n => n.NodeId, n =>
        {
            var visual = _nodeVisuals[n];
            return new Rect(n.X, n.Y, Math.Max(1, visual.ActualWidth), Math.Max(1, visual.ActualHeight));
        }, StringComparer.Ordinal);
        var measured = new HashSet<string>(StringComparer.Ordinal);
        var pending = frames.ToList();
        while (pending.Count > 0)
        {
            bool progress = false;
            foreach (var frame in pending.ToArray())
            {
                if (frame.Groups.Any(id => !measured.Contains(id))) continue;
                if (_gPressed && _pointerState.Is(GraphPointerMode.NodeDrag) && _groupDragBounds.TryGetValue(frame.Id, out var frozen))
                {
                    _groupBounds[frame.Id] = frozen;
                    _groupLayoutInputs.Remove(frame.Id);
                    measured.Add(frame.Id); pending.Remove(frame); progress = true; continue;
                }
                var inputs = frame.Members.Where(nodeBounds.ContainsKey).Select(id => nodeBounds[id])
                    .Concat(frame.Groups.Select(id => _groupBounds[id])).ToArray();
                if (inputs.Length == 0) inputs = [new Rect(frame.X + 16, frame.Y + 34, 48, 1)];
                if (!_groupLayoutInputs.TryGetValue(frame.Id, out var previous) || !inputs.SequenceEqual(previous))
                {
                    var bounds = Rect.Empty;
                    foreach (var input in inputs) bounds.Union(input);
                    _groupBounds[frame.Id] = new Rect(bounds.Left - 16, bounds.Top - 34, Math.Max(80, bounds.Width + 32), Math.Max(50, bounds.Height + 50));
                    _groupLayoutInputs[frame.Id] = inputs;
                }
                measured.Add(frame.Id);
                pending.Remove(frame); progress = true;
            }
            if (!progress) break; // Malformed sidecars must not trap the UI thread.
        }
        var parents = frames.SelectMany(f => f.Groups.Select(id => (id, f.Id))).ToDictionary(p => p.id, p => p.Id);
        var bodies = _frameVisuals.OfType<Border>().Where(v => v.Tag is string)
            .ToDictionary(v => (string)v.Tag, StringComparer.Ordinal);
        var canvasChildren = GraphCanvas.Children.Cast<UIElement>().ToHashSet();
        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            var frame = frames[frameIndex];
            if (!_groupBounds.TryGetValue(frame.Id, out var bounds)) continue;
            if (!bodies.TryGetValue(frame.Id, out var body) || !canvasChildren.Contains(body))
            {
                body = MakeGroupVisual(frame.Id);
                GraphCanvas.Children.Add(body); _frameVisuals.Add(body);
            }
            body.Width = frame.Collapsed ? Math.Min(280, bounds.Width) : bounds.Width;
            var nextHeight = frame.Collapsed ? 50 : bounds.Height;
            var changedCollapse = _groupCollapsedStates.TryGetValue(frame.Id, out var wasCollapsed) && wasCollapsed != frame.Collapsed;
            _groupCollapsedStates[frame.Id] = frame.Collapsed;
            var displayedHeight = body.ActualHeight;
            if (changedCollapse) body.BeginAnimation(HeightProperty, null);
            body.Height = nextHeight;
            if (changedCollapse && IsLoaded && SystemParameters.ClientAreaAnimation)
                body.BeginAnimation(HeightProperty, new DoubleAnimation(displayedHeight, nextHeight, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    FillBehavior = FillBehavior.Stop
                }, HandoffBehavior.SnapshotAndReplace);
            Canvas.SetLeft(body, bounds.X); Canvas.SetTop(body, bounds.Y);
            var depth = 0; var cursor = frame.Id;
            while (parents.TryGetValue(cursor, out var parent)) { depth++; cursor = parent; }
            // More specific groups are above ancestors; all remain behind nodes/wires.
            Panel.SetZIndex(body, int.MinValue + 1 + depth * (frames.Count + 1) + frameIndex);
            var key = frame.Color ?? "#82919B";
            if (!_groupColors.TryGetValue(key, out var brushes)) {
                var color = GroupColor(key);
                var border = new SolidColorBrush(color); border.Freeze();
                var fill = new SolidColorBrush(Color.FromArgb(28, color.R, color.G, color.B)); fill.Freeze();
                _groupColors[key] = brushes = (border, fill);
            }
            body.Background = brushes.Fill;
            body.BorderBrush = _selectedGroups.Contains(frame.Id) || _gPressed && _groupDropTarget == frame.Id ? Brushes.DodgerBlue : brushes.Border;
            var title = ((Grid)body.Child).Children.OfType<TextBlock>().Single();
            title.Text = frame.Collapsed ? $"{frame.Title} · {frame.Members.Length} 个故事" : frame.Title;
            body.ToolTip = frame.Title;
        }
    }
    private Border MakeGroupVisual(string id)
    {
        var grid = new Grid();
        var body = new Border { Tag = id, Child = grid, ClipToBounds = true, Background = new SolidColorBrush(Color.FromArgb(24, 130, 145, 155)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        var drag = new Thumb { Background = Brushes.Transparent, Cursor = Cursors.SizeAll, IsEnabled = !IsReadOnly, Template = TransparentThumbTemplate() };
        grid.Children.Add(drag);
        var title = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 5, 8, 0), VerticalAlignment = VerticalAlignment.Top, TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        grid.Children.Add(title);
        Dictionary<string, GraphEditorNodePosition>? positions = null;
        Point origin = default;
        bool thresholdPassed = false;
        drag.PreviewMouseLeftButtonDown += (_, e) =>
        {
            FocusGraphCanvas();
            if (!_selectedGroups.Contains(id) || IsAdditiveSelectionModifier(Keyboard.Modifiers))
                SelectGroup(id, IsAdditiveSelectionModifier(Keyboard.Modifiers));
        };
        drag.DragStarted += (_, _) =>
        {
            _activeGroupThumb = drag;
            thresholdPassed = false;
            if (_host is null) return;
            var ids = GraphGroupOperations.DescendantNodes(_host.Frames, _selectedGroups);
            ids.UnionWith(_selectedNodes.Select(n => n.NodeId));
            positions = _host.Nodes.Where(n => ids.Contains(n.NodeId)).ToDictionary(n => n.NodeId, n => n.Position);
            origin = Mouse.GetPosition(GraphCanvas);
            if (!_host.BeginLayoutMove(positions.Keys)) positions = null;
        };
        drag.DragDelta += (_, _) =>
        {
            if (_host is null || positions is null) return;
            var delta = Mouse.GetPosition(GraphCanvas) - origin;
            thresholdPassed |= CrossedDragThreshold(delta);
            if (!thresholdPassed) return;
            if (_gPressed) _gUsedForDrag = true;
            foreach (var pair in positions) _host.SetNodePosition(pair.Key, pair.Value.X + delta.X, pair.Value.Y + delta.Y);
            DrawCommentFrames();
        };
        drag.DragCompleted += (_, e) =>
        {
            _activeGroupThumb = null;
            if (_host is null || positions is null) return;
            if (e.Canceled) _host.CancelLayoutMove(); else _host.CommitLayoutMove();
            positions = null; DrawCommentFrames();
        };
        if (_host?.Scope == Core.Graphs.Definitions.GraphScope.Project)
            System.Windows.Automation.AutomationProperties.SetAutomationId(body, "StoryGroupFrame_" + id);
        body.ContextMenu = FluentContextMenuFactory.Create(body);
        body.ContextMenuOpening += (_, _) =>
        {
            if (!_selectedGroups.Contains(id)) SelectGroup(id);
            if (_host?.Scope == Core.Graphs.Definitions.GraphScope.Project)
            {
                var menu = FluentContextMenuFactory.Create(body);
                menu.Items.Add(FluentContextMenuFactory.CreateItem("重命名", () => {
                    if (_host.Frames.FirstOrDefault(f => f.Id == id) is { } frame) EditFrameTitle(frame);
                }));
                var colors = FluentContextMenuFactory.CreateSubmenu("修改颜色");
                foreach (var color in GraphCommentFrame.Palette) {
                    var item = FluentContextMenuFactory.CreateItem(color, () => {
                        if (_host.Frames.FirstOrDefault(f => f.Id == id) is { } frame) _host.UpdateFrame(frame with { Color = color });
                    });
                    item.Icon = new Border { Width = 14, Height = 14, Background = new SolidColorBrush(GroupColor(color)) };
                    colors.Items.Add(item);
                }
                menu.Items.Add(colors);
                var selectedFrame = _host.Frames.Single(frame => frame.Id == id);
                menu.Items.Add(FluentContextMenuFactory.CreateItem(selectedFrame.Collapsed ? "展开" : "折叠", () =>
                    _host.UpdateFrame(selectedFrame with { Collapsed = !selectedFrame.Collapsed })));
                menu.Items.Add(FluentContextMenuFactory.CreateItem("删除故事组", () => DeleteSelectedGroups()));
                body.ContextMenu = menu;
            }
            else body.ContextMenu = CreateSelectionContextMenu(null, body);
        };
        return body;
    }
    private static Color GroupColor(string? value)
    {
        try { return (Color)ColorConverter.ConvertFromString(value ?? "#82919B"); }
        catch { return Color.FromRgb(130, 145, 155); }
    }
    private string? ContextGroupId()
    {
        if (_selectedGroups.Count == 1) return _selectedGroups.Single();
        if (_selectedNodes.Count == 0 || _host is null) return null;
        var owners = _host.Frames.Where(f => f.Members.Intersect(_selectedNodes.Select(n => n.NodeId)).Any()).ToArray();
        return owners.Length == 1 ? owners[0].Id : null;
    }
    private void AddGroupMenuItems(ContextMenu menu)
    {
        var group = FluentContextMenuFactory.CreateSubmenu("组合");
        group.Items.Add(FluentContextMenuFactory.CreateItem("组合", () => CombineSelected(), !IsReadOnly && _selectedNodes.Count + _selectedGroups.Count >= 2));
        var id = ContextGroupId();
        group.Items.Add(FluentContextMenuFactory.CreateItem("取消组合", () => { if (id is not null) _host?.RemoveFrame(id); }, !IsReadOnly && id is not null));
        group.Items.Add(FluentContextMenuFactory.CreateItem("修改标题", () => { if (_host?.Frames.FirstOrDefault(f => f.Id == id) is { } frame) EditFrameTitle(frame); }, !IsReadOnly && id is not null));
        var colors = FluentContextMenuFactory.CreateSubmenu("修改颜色");
        colors.IsEnabled = !IsReadOnly && id is not null;
        string[] names = ["蓝色", "绿色", "紫色", "橙色", "玫红", "橄榄绿"];
        for (var i = 0; i < GraphCommentFrame.Palette.Length; i++)
        {
            var color = GraphCommentFrame.Palette[i];
            var item = FluentContextMenuFactory.CreateItem(names[i], () => { if (_host?.Frames.FirstOrDefault(f => f.Id == id) is { } frame) _host.UpdateFrame(frame with { Color = color }); });
            item.Icon = new Border { Width = 14, Height = 14, Background = new SolidColorBrush(GroupColor(color)) };
            colors.Items.Add(item);
        }
        group.Items.Add(colors);
        menu.Items.Add(group);
    }
    private bool DeleteSelectedGroups()
    {
        if (_host is null || IsReadOnly || _selectedGroups.Count == 0) return false;
        var result = _host.Scope == Core.Graphs.Definitions.GraphScope.Project
            ? DeleteStoryGroupsRequested?.Invoke(_selectedGroups.ToArray()) == true
            : _host.DeleteGroups(_selectedGroups, _selectedNodes.Select(n => n.NodeId), true);
        if (result) ClearSelection();
        else _host.SetAuthoringIssue("graph.groups.delete", new("graph.groups.delete", "组合包含固定、只读或受保护对象，未删除任何内容。"));
        DrawCommentFrames();
        return result;
    }
    private void UpdateGroupDrop(Point pointer)
    {
        if (!_gPressed || !_nodeDragThresholdPassed) { DrawCommentFrames(); return; }
        _gUsedForDrag = true;
        var frames = _host?.Frames ?? [];
        var candidates = frames.Where(f => _groupDragBounds.TryGetValue(f.Id, out var bounds) && bounds.Contains(pointer)).ToArray();
        var parents = frames.SelectMany(f => f.Groups.Select(child => (child, f.Id))).ToDictionary(p => p.child, p => p.Id);
        int Depth(string id) { int depth = 0; while (parents.TryGetValue(id, out var parent)) { id = parent; depth++; } return depth; }
        _groupDropTarget = candidates.OrderByDescending(f => Depth(f.Id)).ThenByDescending(f => frames.ToList().IndexOf(f)).FirstOrDefault()?.Id;
        CanvasViewport.ToolTip = _groupDropTarget is null ? "移出组合" : $"移入「{frames.First(f => f.Id == _groupDropTarget).Title}」";
        DrawCommentFrames();
    }
    private void ResetGroupGesture()
    { _gPressed = _gUsedForDrag = false; _groupDropTarget = null; CanvasViewport.ToolTip = null; }

    private static ControlTemplate TransparentThumbTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = border };
    }
    private bool IsFrameInteraction(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && _frameVisuals.Contains(element)) return true;
            source = LinePagesEditor.InputParent(source);
        }
        return false;
    }
    private void EditFrameTitle(GraphCommentFrame frame)
    {
        ResetGroupGesture();
        var box = new TextBox { Text = frame.Title, MaxLength = 1024, Margin = new Thickness(12), MinWidth = 240 };
        var save = new Button { Content = "保存", Margin = new Thickness(12), IsDefault = true };
        var panel = new StackPanel(); panel.Children.Add(box); panel.Children.Add(save);
        var dialog = new Window { Title = "组合标题", Content = panel, Owner = Window.GetWindow(this), SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        save.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(box.Text)) return;
            if (_host?.Scope == Core.Graphs.Definitions.GraphScope.Project) RenameStoryGroupRequested?.Invoke(frame.Id, box.Text.Trim());
            else _host?.UpdateFrame(frame with { Title = box.Text.Trim() });
            dialog.DialogResult = true;
        };
        dialog.ShowDialog();
    }
}
