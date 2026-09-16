using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>One drop, one model mutation. Only the explicit handle owns the gesture.</summary>
public static class EntryReorder
{
    public static readonly DependencyProperty IsHandleProperty = DependencyProperty.RegisterAttached("IsHandle", typeof(bool), typeof(EntryReorder), new PropertyMetadata(false, Changed));
    public static bool GetIsHandle(DependencyObject value) => (bool)value.GetValue(IsHandleProperty);
    public static void SetIsHandle(DependencyObject value, bool enabled) => value.SetValue(IsHandleProperty, enabled);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs args)
    {
        if (value is not FrameworkElement handle || args.NewValue is not true) return;
        Point? start = null;
        handle.Cursor = Cursors.SizeNS;
        handle.PreviewMouseLeftButtonDown += (_, e) => { handle.Focus(); start = e.GetPosition(handle); handle.CaptureMouse(); e.Handled = true; };
        handle.PreviewMouseLeftButtonUp += (_, e) => { start = null; handle.ReleaseMouseCapture(); e.Handled = true; };
        handle.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(handle);
            if (Math.Abs(point.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null; handle.ReleaseMouseCapture(); e.Handled = true;
            if (LinePagesEditor.Ancestor<ItemsControl>(handle) is { } list) Drag(handle, list);
        };
    }

    private static void Drag(FrameworkElement handle, ItemsControl list)
    {
        var source = handle.DataContext;
        var oldIndex = list.Items.IndexOf(source);
        if (oldIndex < 0) return;
        var marker = new InsertMarker(list);
        var layer = AdornerLayer.GetAdornerLayer(list);
        var previousAllowDrop = list.AllowDrop;
        int? destination = null;
        Point last = new(-1, -1);
        var format = "DarkGreyRPG.EntryReorder." + Guid.NewGuid().ToString("N");
        var scroll = LinePagesEditor.Ancestor<ScrollViewer>(list);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        void Locate(Point p)
        {
            destination = null;
            if (p.X < 0 || p.X > list.ActualWidth || p.Y < 0 || p.Y > list.ActualHeight) { marker.Show = false; marker.InvalidateVisual(); return; }
            var insertion = list.Items.Count;
            var y = list.ActualHeight;
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (list.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement row) continue;
                var top = row.TranslatePoint(new Point(), list).Y;
                if (p.Y < top + row.ActualHeight / 2) { insertion = i; y = top; break; }
            }
            destination = Math.Clamp(insertion > oldIndex ? insertion - 1 : insertion, 0, list.Items.Count - 1);
            marker.Y = y; marker.Show = true; marker.InvalidateVisual();
        }
        DragEventHandler over = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            last = e.GetPosition(list); Locate(last); e.Effects = destination.HasValue ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true;
        };
        DragEventHandler leave = (_, e) =>
        {
            var point = e.GetPosition(list);
            // Descendant targets also raise leave while the pointer is still inside our list.
            if (point.X >= 0 && point.X <= list.ActualWidth && point.Y >= 0 && point.Y <= list.ActualHeight) return;
            marker.Show = false; marker.InvalidateVisual(); last = new(-1, -1); destination = null; e.Handled = true;
        };
        int? committed = null;
        DragEventHandler drop = (_, e) => { if (!e.Data.GetDataPresent(format)) return; Locate(e.GetPosition(list)); committed = destination; e.Effects = committed.HasValue ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        timer.Tick += (_, _) =>
        {
            if (scroll is null || last.X < 0) return;
            var point = list.TranslatePoint(last, scroll);
            var offset = point.Y < 30 ? -12 : point.Y > scroll.ActualHeight - 30 ? 12 : 0;
            if (offset == 0) return;
            var before = scroll.VerticalOffset; scroll.ScrollToVerticalOffset(before + offset); scroll.UpdateLayout();
            last.Y += scroll.VerticalOffset - before; Locate(last);
        };
        list.AllowDrop = true; list.PreviewDragOver += over; list.PreviewDragLeave += leave; list.PreviewDrop += drop; layer?.Add(marker); timer.Start();
        try
        {
            var result = DragDrop.DoDragDrop(handle, new DataObject(format, source), DragDropEffects.Move);
            if (result != DragDropEffects.Move || committed is not { } index || index == oldIndex) return;
            switch (source)
            {
                case CanonicalLinePageViewModel page: page.Move(index); break;
                case CanonicalChoiceOptionViewModel choice: choice.MoveTo(index); break;
                case CanonicalTaskResultSlotViewModel resultSlot: resultSlot.MoveTo(index); break;
            }
        }
        finally { timer.Stop(); list.PreviewDragOver -= over; list.PreviewDragLeave -= leave; list.PreviewDrop -= drop; list.AllowDrop = previousAllowDrop; layer?.Remove(marker); }
    }

    private sealed class InsertMarker(UIElement element) : Adorner(element)
    {
        public double Y;
        public bool Show;
        protected override void OnRender(DrawingContext drawingContext)
        {
            IsHitTestVisible = false;
            if (Show) drawingContext.DrawLine(new Pen(SystemColors.HighlightBrush, 2), new Point(0, Y), new Point(AdornedElement.RenderSize.Width, Y));
        }
    }
}
