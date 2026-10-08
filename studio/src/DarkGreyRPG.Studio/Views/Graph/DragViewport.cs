using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Unanimated viewport and current screen pointer, independent of routed child leave events.</summary>
internal static class DragViewport
{
    public static Point Pointer(FrameworkElement host)
        => GetCursorPos(out var point) ? host.PointFromScreen(new Point(point.X, point.Y)) : System.Windows.Input.Mouse.GetPosition(host);

    public static Rect Bounds(FrameworkElement host)
    {
        var bounds = new Rect(0, 0, host.ActualWidth, host.ActualHeight);
        for (DependencyObject? parent = host; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ScrollContentPresenter viewport) continue;
            bounds.Intersect(new Rect(viewport.TranslatePoint(default, host), new Size(viewport.ActualWidth, viewport.ActualHeight)));
        }
        if (host is ItemsControl)
        {
            var viewport = Descendants(host).OfType<ScrollContentPresenter>().FirstOrDefault();
            if (viewport is not null)
                bounds.Intersect(new Rect(viewport.TranslatePoint(default, host), new Size(viewport.ActualWidth, viewport.ActualHeight)));
        }
        return bounds;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X; public int Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);
}
