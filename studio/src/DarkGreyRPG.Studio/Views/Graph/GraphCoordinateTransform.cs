using System.Windows;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// Converts points between graph (world) coordinates and viewport coordinates.
/// The viewport transform is screen = graph * zoom + pan.
/// </summary>
public sealed class GraphCoordinateTransform
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 2.5;

    public GraphCoordinateTransform(double zoom = 1, double panX = 0, double panY = 0)
    {
        Zoom = ClampZoom(zoom);
        PanX = Sanitize(panX);
        PanY = Sanitize(panY);
    }

    public GraphCoordinateTransform(double zoom, Vector pan)
        : this(zoom, pan.X, pan.Y)
    {
    }

    public double Zoom { get; }
    public double PanX { get; }
    public double PanY { get; }
    public Vector Pan => new(PanX, PanY);

    public Point GraphToScreen(Point graphPoint) => GraphToScreen(graphPoint, Zoom, PanX, PanY);
    public Point ScreenToGraph(Point screenPoint) => ScreenToGraph(screenPoint, Zoom, PanX, PanY);
    public Point GraphToScreen(double graphX, double graphY) => GraphToScreen(new Point(graphX, graphY));
    public Point ScreenToGraph(double screenX, double screenY) => ScreenToGraph(new Point(screenX, screenY));

    public static Point GraphToScreen(Point graphPoint, double zoom, Vector pan)
        => GraphToScreen(graphPoint, zoom, pan.X, pan.Y);

    public static Point GraphToScreen(Point graphPoint, double zoom, double panX, double panY)
    {
        var safeZoom = ClampZoom(zoom);
        return new Point(
            Sanitize(graphPoint.X) * safeZoom + Sanitize(panX),
            Sanitize(graphPoint.Y) * safeZoom + Sanitize(panY));
    }

    public static Point ScreenToGraph(Point screenPoint, double zoom, Vector pan)
        => ScreenToGraph(screenPoint, zoom, pan.X, pan.Y);

    public static Point ScreenToGraph(Point screenPoint, double zoom, double panX, double panY)
    {
        var safeZoom = ClampZoom(zoom);
        return new Point(
            (Sanitize(screenPoint.X) - Sanitize(panX)) / safeZoom,
            (Sanitize(screenPoint.Y) - Sanitize(panY)) / safeZoom);
    }

    public GraphCoordinateTransform With(double? zoom = null, double? panX = null, double? panY = null)
        => new(zoom ?? Zoom, panX ?? PanX, panY ?? PanY);

    public static double ClampZoom(double zoom)
    {
        if (double.IsNaN(zoom)) return 1;
        if (double.IsPositiveInfinity(zoom)) return MaxZoom;
        if (double.IsNegativeInfinity(zoom)) return MinZoom;
        return Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    internal static double Sanitize(double value) => double.IsFinite(value) ? value : 0;
}
