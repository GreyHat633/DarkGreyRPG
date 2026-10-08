using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Reorders top-level stories/components with one preview, a row-sized gap and animated neighbors.</summary>
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
        var rows = Groups(list).Where(row => row.IsVisible && row.ActualHeight > 0)
            .OrderBy(row => row.TranslatePoint(default, list).Y).ToArray();
        var oldIndex = Array.FindIndex(rows, row => Key(row) == sourceKey);
        if (oldIndex < 0 || rows.Length < 2 || Section(rows[oldIndex]) is not { } section) return;
        var keys = rows.Select(row => Key(row)!).ToArray();
        var remaining = keys.Where(key => key != sourceKey).ToArray();
        var title = section.Name switch { StoryGroup group => group.DisplayName,
            ViewModels.StoryListItemViewModel story => story.DisplayName, _ => sourceKey };
        var layer = AdornerLayer.GetAdornerLayer(list);
        if (layer is null) return;
        var scroll = Descendants<ScrollViewer>(list).FirstOrDefault();
        var originalScroll = scroll?.VerticalOffset ?? 0;
        var format = "Dgr.StoryNavigation." + Guid.NewGuid().ToString("N");
        using var preview = new OutputReorderPreview(list, rows, title, false, oldIndex, Mouse.GetPosition(list));
        Point last = new(-1, -1);
        int? destination = null, dropped = null;
        var cancelled = false;
        void Update(Point point)
        {
            last = point;
            preview.SetScrollShift(originalScroll - (scroll?.VerticalOffset ?? 0));
            destination = preview.Locate(point);
        }
        DragEventHandler over = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            Update(e.GetPosition(list));
            e.Effects = destination.HasValue ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        DragEventHandler leave = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            var point = e.GetPosition(list);
            if (point.X >= 0 && point.X <= list.ActualWidth && point.Y >= 0 && point.Y <= list.ActualHeight) return;
            destination = null;
            preview.Locate(point);
            e.Handled = true;
        };
        QueryContinueDragEventHandler continuation = (_, e) =>
        {
            if (e.EscapePressed || !list.IsLoaded || !list.IsVisible || Window.GetWindow(list) is { IsActive: false }
                || !Groups(list).Select(Key).Where(key => key is not null).SequenceEqual(keys)) cancelled = true;
            if (cancelled) { e.Action = DragAction.Cancel; e.Handled = true; }
        };
        DragEventHandler drop = (_, e) =>
        {
            if (!e.Data.GetDataPresent(format)) return;
            Update(e.GetPosition(list)); dropped = destination;
            e.Effects = dropped.HasValue ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            if (scroll is null || !destination.HasValue) return;
            var offset = last.Y < 32 ? -12 : last.Y > list.ActualHeight - 32 ? 12 : 0;
            if (offset == 0) return;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + offset);
            scroll.UpdateLayout(); Update(Mouse.GetPosition(list));
        };
        list.PreviewDragOver += over; list.PreviewDragLeave += leave; list.PreviewDrop += drop;
        list.QueryContinueDrag += continuation;
        layer.Add(preview); Update(Mouse.GetPosition(list)); timer.Start();
        DragDropEffects result;
        try { result = DragDrop.DoDragDrop(list, new DataObject(format, sourceKey), DragDropEffects.Move); }
        finally
        {
            timer.Stop(); preview.Dispose();
            list.PreviewDragOver -= over; list.PreviewDragLeave -= leave; list.PreviewDrop -= drop;
            list.QueryContinueDrag -= continuation; layer.Remove(preview);
        }
        if (cancelled || result != DragDropEffects.Move || dropped is not { } index || index == oldIndex) return;
        var after = index == remaining.Length;
        commit(sourceKey, after ? remaining[^1] : remaining[index], after);
    }


    private static IEnumerable<GroupItem> Groups(DependencyObject root)
        => Descendants<GroupItem>(root).Where(row => Key(row) is not null);

    private static CollectionViewGroup? Section(GroupItem row)
        => row.Content as CollectionViewGroup ?? row.DataContext as CollectionViewGroup;

    private static string? Key(GroupItem row) => Section(row)?.Name switch
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

}
