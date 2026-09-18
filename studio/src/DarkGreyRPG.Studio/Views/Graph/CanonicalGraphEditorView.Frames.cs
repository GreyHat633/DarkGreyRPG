using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalGraphEditorView
{
    private readonly List<FrameworkElement> _frameVisuals = [];

    private void CreateCommentFrame(Point point)
    {
        if (_host is null || IsReadOnly) return;
        var nodes = _selectedNodes.ToArray();
        var x = nodes.Length == 0 ? point.X : nodes.Min(node => node.X) - 24;
        var y = nodes.Length == 0 ? point.Y : nodes.Min(node => node.Y) - 42;
        var right = nodes.Length == 0 ? x + 300 : nodes.Max(node => node.X + (_nodeVisuals.TryGetValue(node, out var visual) ? visual.ActualWidth : 280)) + 24;
        var bottom = nodes.Length == 0 ? y + 180 : nodes.Max(node => node.Y + (_nodeVisuals.TryGetValue(node, out var visual) ? visual.ActualHeight : 160)) + 24;
        _host.AddFrame(nodes.Select(node => node.NodeId), x, y, right - x, bottom - y);
    }

    private void DrawCommentFrames()
    {
        foreach (var visual in _frameVisuals) GraphCanvas.Children.Remove(visual);
        _frameVisuals.Clear();
        if (_host is null) return;
        foreach (var frame in _host.Frames)
        {
            // Body is deliberately not hit-testable: nodes, wires and marquee retain input.
            var body = new Border { Width = frame.Width, Height = frame.Height, IsHitTestVisible = false,
                Background = new SolidColorBrush(Color.FromArgb(24, 130, 145, 155)), BorderBrush = new SolidColorBrush(Color.FromArgb(140, 130, 145, 155)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            AddFrameVisual(body, frame.X, frame.Y);
            var header = new Grid { Tag = frame, Width = frame.Width, Height = 28, Background = new SolidColorBrush(Color.FromArgb(55, 130, 145, 155)) };
            var drag = new Thumb { Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeAll, IsEnabled = !IsReadOnly };
            drag.Template = TransparentThumbTemplate();
            header.Children.Add(drag);
            var title = new TextBlock { Text = frame.Title, Margin = new Thickness(8, 4, 8, 0), IsHitTestVisible = false, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            header.Children.Add(title);
            double dx = 0, dy = 0;
            var owner = _host;
            var memberPositions = owner.Nodes.Where(node => frame.Members.Contains(node.NodeId))
                .ToDictionary(node => node.NodeId, node => node.Position);
            bool movingMembers = false;
            Thumb? resizeGrip = null;
            drag.DragStarted += (_, _) => { dx = dy = 0; movingMembers = owner.BeginLayoutMove(memberPositions.Keys); };
            drag.DragDelta += (_, e) =>
            {
                dx += e.HorizontalChange; dy += e.VerticalChange;
                Canvas.SetLeft(header, frame.X + dx); Canvas.SetTop(header, frame.Y + dy);
                Canvas.SetLeft(body, frame.X + dx); Canvas.SetTop(body, frame.Y + dy);
                if (resizeGrip is not null) { Canvas.SetLeft(resizeGrip, frame.X + frame.Width - 12 + dx); Canvas.SetTop(resizeGrip, frame.Y + frame.Height - 12 + dy); }
                if (movingMembers) foreach (var member in memberPositions)
                    owner.SetNodePosition(member.Key, member.Value.X + dx, member.Value.Y + dy);
            };
            drag.DragCompleted += (_, e) =>
            {
                // Rewind the transient preview before committing frame and member positions together.
                if (movingMembers) owner.CancelLayoutMove();
                if (!e.Canceled) owner.UpdateFrame(frame with { X = frame.X + dx, Y = frame.Y + dy }, moveMembers: true);
                else DrawCommentFrames();
            };
            var menu = FluentContextMenuFactory.Create(header);
            menu.Items.Add(FluentContextMenuFactory.CreateItem("修改标题", () => EditFrameTitle(frame), !IsReadOnly));
            menu.Items.Add(FluentContextMenuFactory.CreateItem("将选中节点加入此组", () => _host?.UpdateFrame(frame with { Members = frame.Members.Concat(_selectedNodes.Select(node => node.NodeId)).Distinct().ToArray() }), !IsReadOnly));
            menu.Items.Add(FluentContextMenuFactory.CreateItem("将选中节点移出此组", () => _host?.UpdateFrame(frame with { Members = frame.Members.Except(_selectedNodes.Select(node => node.NodeId)).ToArray() }), !IsReadOnly));
            menu.Items.Add(FluentContextMenuFactory.CreateItem("删除分组框（保留节点）", () => _host?.RemoveFrame(frame.Id), !IsReadOnly));
            header.ContextMenu = menu;
            AddFrameVisual(header, frame.X, frame.Y);
            var resize = new Thumb { Tag = frame, Width = 12, Height = 12, Background = Brushes.Gray, Cursor = System.Windows.Input.Cursors.SizeNWSE, IsEnabled = !IsReadOnly };
            resizeGrip = resize;
            var gripBorder = new FrameworkElementFactory(typeof(Border));
            gripBorder.SetValue(Border.BorderBrushProperty, Brushes.Gray);
            gripBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 2, 2));
            gripBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            resize.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = gripBorder };
            double dw = 0, dh = 0;
            resize.DragStarted += (_, _) => { dw = dh = 0; };
            resize.DragDelta += (_, e) => { dw += e.HorizontalChange; dh += e.VerticalChange; body.Width = Math.Max(80, frame.Width + dw); header.Width = body.Width; body.Height = Math.Max(50, frame.Height + dh); Canvas.SetLeft(resize, frame.X + body.Width - 12); Canvas.SetTop(resize, frame.Y + body.Height - 12); };
            resize.DragCompleted += (_, e) => { if (!e.Canceled) _host?.UpdateFrame(frame with { Width = Math.Max(80, frame.Width + dw), Height = Math.Max(50, frame.Height + dh) }); else DrawCommentFrames(); };
            AddFrameVisual(resize, frame.X + frame.Width - 12, frame.Y + frame.Height - 12);
        }
    }

    private static ControlTemplate TransparentThumbTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = border };
    }

    private static bool IsFrameInteraction(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: GraphCommentFrame }) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void AddFrameVisual(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y); Panel.SetZIndex(element, -10);
        GraphCanvas.Children.Add(element); _frameVisuals.Add(element);
    }

    private void EditFrameTitle(GraphCommentFrame frame)
    {
        var box = new TextBox { Text = frame.Title, MaxLength = 1024, Margin = new Thickness(12), MinWidth = 240 };
        var save = new Button { Content = "保存", Margin = new Thickness(12), IsDefault = true };
        var panel = new StackPanel(); panel.Children.Add(box); panel.Children.Add(save);
        var dialog = new Window { Title = "分组标题", Content = panel, Owner = Window.GetWindow(this), SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        save.Click += (_, _) => { _host?.UpdateFrame(frame with { Title = box.Text }); dialog.DialogResult = true; };
        dialog.ShowDialog();
    }
}
