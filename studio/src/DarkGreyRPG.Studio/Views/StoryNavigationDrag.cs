using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Reorders top-level stories/components with a visible source, landing line and drag card.</summary>
public static class StoryNavigationDrag
{
    public sealed record Landing(string Key, bool After, double Y);

    public static Landing? Locate(StoryNavigationList list, string sourceKey, Point point)
    {
        if (point.X < 0 || point.X > list.ActualWidth || point.Y < 0 || point.Y > list.ActualHeight) return null;
        var rows = Groups(list).Where(row => row.IsVisible && row.ActualHeight > 0)
            .Select(row => (Row: row, Key: Key(row),
                Top: row.TranslatePoint(new Point(), list).Y))
            .OrderBy(row => row.Top).ToArray();
        if (rows.Length == 0) return null;
        var target = rows.FirstOrDefault(row => point.Y < row.Top + row.Row.ActualHeight / 2);
        var after = target.Row is null;
        if (after) target = rows[^1];
        if (target.Key == sourceKey) return null;
        return new Landing(target.Key!, after,
            Math.Clamp(target.Top + (after ? target.Row!.ActualHeight : 0), 2, Math.Max(2, list.ActualHeight - 2)));
    }

    public static void Begin(StoryNavigationList list, string sourceKey, Action<string, string, bool> commit)
    {
        var source = Groups(list).FirstOrDefault(row => Key(row) == sourceKey);
        if (source?.DataContext is not CollectionViewGroup section) return;
        var title = section.Name switch { StoryGroup group => group.DisplayName,
            ViewModels.StoryListItemViewModel story => story.DisplayName, _ => sourceKey };
        var subtitle = section.Name is StoryGroup component ? $"故事组 · {component.Members.Count} 个故事" : "故事";
        var overlay = new DragOverlay(list, title, subtitle);
        var layer = AdornerLayer.GetAdornerLayer(list);
        var scroll = Descendants<ScrollViewer>(list).FirstOrDefault();
        var opacity = source.Opacity;
        var format = "Dgr.StoryNavigation." + Guid.NewGuid().ToString("N");
        Point last = new(-1, -1);
        Landing? landing = null, dropped = null;
        void Update(Point point)
        {
            last = point;
            landing = Locate(list, sourceKey, point);
            overlay.Position = point;
            overlay.Landing = landing;
            overlay.Show = point.X >= 0 && point.X <= list.ActualWidth && point.Y >= 0 && point.Y <= list.ActualHeight;
            overlay.InvalidateVisual();
        }
        DragEventHandler over = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            Update(e.GetPosition(list));
            e.Effects = landing is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        };
        DragEventHandler leave = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            Update(e.GetPosition(list));
            e.Handled = true;
        };
        DragEventHandler drop = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            Update(e.GetPosition(list)); dropped = landing;
            e.Effects = dropped is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            if (scroll is null || !overlay.Show) return;
            var offset = last.Y < 32 ? -12 : last.Y > list.ActualHeight - 32 ? 12 : 0;
            if (offset == 0) return;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + offset);
            scroll.UpdateLayout(); Update(last);
        };
        source.SetCurrentValue(UIElement.OpacityProperty, 0.35);
        list.PreviewDragOver += over; list.PreviewDragLeave += leave; list.PreviewDrop += drop;
        layer?.Add(overlay); Update(Mouse.GetPosition(list)); timer.Start();
        try
        {
            var result = DragDrop.DoDragDrop(list, new DataObject(format, sourceKey), DragDropEffects.Move);
            // Only commit on an accepted drop; Escape and drops outside leave persistent order unchanged.
            if (result == DragDropEffects.Move && dropped is { } target) commit(sourceKey, target.Key, target.After);
        }
        finally
        {
            timer.Stop(); source.SetCurrentValue(UIElement.OpacityProperty, opacity);
            list.PreviewDragOver -= over; list.PreviewDragLeave -= leave; list.PreviewDrop -= drop;
            layer?.Remove(overlay);
        }
    }

    private static IEnumerable<GroupItem> Groups(DependencyObject root)
        => Descendants<GroupItem>(root).Where(row => Key(row) is not null);

    private static string? Key(GroupItem row) => (row.DataContext as CollectionViewGroup)?.Name switch
    {
        StoryGroup group => group.Key,
        ViewModels.StoryListItemViewModel story => story.NavigationKey,
        _ => null
    };

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class DragOverlay : Adorner
    {
        private readonly string _title, _subtitle;
        public Point Position { get; set; }
        public Landing? Landing { get; set; }
        public bool Show { get; set; }
        public DragOverlay(UIElement element, string title, string subtitle) : base(element)
        { _title = title; _subtitle = subtitle; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext drawing)
        {
            if (!Show) return;
            var element = (FrameworkElement)AdornedElement;
            var accent = element.TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.HighlightBrush;
            var background = Window.GetWindow(element)?.Background ?? SystemColors.WindowBrush;
            var fill = element.TryFindResource("LayerFillColorAltBrush") as Brush ?? Brushes.Transparent;
            var text = element.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
            var width = Math.Max(30, Math.Min(260, element.ActualWidth - 16));
            if (Landing is { } target)
            {
                drawing.DrawLine(new Pen(accent, 3), new Point(8, target.Y), new Point(element.ActualWidth - 8, target.Y));
                drawing.DrawEllipse(accent, null, new Point(8, target.Y), 4, 4);
            }
            var origin = new Point(Math.Clamp(Position.X + 12, 8, Math.Max(8, element.ActualWidth - width - 8)),
                Math.Clamp(Position.Y + 18, 4, Math.Max(4, element.ActualHeight - 65)));
            var card = new Rect(origin, new Size(width, 58));
            drawing.DrawRoundedRectangle(background, null, card, 5, 5);
            drawing.DrawRoundedRectangle(fill, new Pen(accent, 2), card, 5, 5);
            DrawText(_title, 13, new Point(origin.X + 10, origin.Y + 8));
            DrawText(Landing is null ? _subtitle : _subtitle + " · 松开到蓝线处", 11, new Point(origin.X + 10, origin.Y + 32));
            void DrawText(string value, double size, Point at)
            {
                var label = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"), size, text, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    { MaxTextWidth = Math.Max(10, width - 20), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                drawing.DrawText(label, at);
            }
        }
    }
}
