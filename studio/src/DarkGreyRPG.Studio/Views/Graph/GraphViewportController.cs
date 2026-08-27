using System.Windows;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// Deterministic, view-agnostic graph viewport state and operations.
/// Pan is expressed in viewport pixels/DIPs and graph points use world coordinates.
/// </summary>
public sealed class GraphViewportController
{
    public const double DefaultZoomFactor = 1.12;
    public const double DefaultFitMargin = 24;

    private double _zoom = 1;
    private double _panX;
    private double _panY;

    public GraphViewportController()
    {
    }

    public GraphViewportController(double zoom, double panX = 0, double panY = 0)
    {
        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    public GraphViewportController(double zoom, Vector pan)
        : this(zoom, pan.X, pan.Y)
    {
    }

    public double Zoom
    {
        get => _zoom;
        set => _zoom = GraphCoordinateTransform.ClampZoom(value);
    }

    public double MinZoom => GraphCoordinateTransform.MinZoom;
    public double MaxZoom => GraphCoordinateTransform.MaxZoom;

    public double PanX
    {
        get => _panX;
        set => _panX = GraphCoordinateTransform.Sanitize(value);
    }

    public double PanY
    {
        get => _panY;
        set => _panY = GraphCoordinateTransform.Sanitize(value);
    }

    public Vector Pan => new(PanX, PanY);
    public GraphCoordinateTransform Transform => new(Zoom, PanX, PanY);

    public Point GraphToScreen(Point graphPoint) => Transform.GraphToScreen(graphPoint);
    public Point ScreenToGraph(Point screenPoint) => Transform.ScreenToGraph(screenPoint);
    public Point GraphToScreen(double graphX, double graphY) => GraphToScreen(new Point(graphX, graphY));
    public Point ScreenToGraph(double screenX, double screenY) => ScreenToGraph(new Point(screenX, screenY));

    public void PanBy(Vector delta) => PanBy(delta.X, delta.Y);

    public void PanBy(double deltaX, double deltaY)
    {
        PanX += GraphCoordinateTransform.Sanitize(deltaX);
        PanY += GraphCoordinateTransform.Sanitize(deltaY);
    }

    /// <summary>Scales by <paramref name="zoomFactor"/> around a viewport point.</summary>
    public void ZoomAt(Point viewportPoint, double zoomFactor)
    {
        if (!double.IsFinite(zoomFactor) || zoomFactor <= 0) return;
        SetZoomAt(Zoom * zoomFactor, viewportPoint);
    }

    public void ZoomAtCursor(Point cursor, double zoomFactor) => ZoomAt(cursor, zoomFactor);
    public void ZoomAtCursor(double zoomFactor, Point cursor) => ZoomAt(cursor, zoomFactor);

    /// <summary>Scales by <paramref name="zoomFactor"/> around the viewport center.</summary>
    public void ZoomAtViewportCenter(Size viewportSize, double zoomFactor)
    {
        if (!IsUsableSize(viewportSize)) return;
        ZoomAt(new Point(viewportSize.Width / 2, viewportSize.Height / 2), zoomFactor);
    }

    public void ZoomAtViewportCenter(double zoomFactor, double viewportWidth, double viewportHeight)
        => ZoomAtViewportCenter(new Size(viewportWidth, viewportHeight), zoomFactor);

    public void ZoomAtViewportCenter(double zoomFactor, Size viewportSize)
        => ZoomAtViewportCenter(viewportSize, zoomFactor);

    public void ZoomAtCenter(Size viewportSize, double zoomFactor)
        => ZoomAtViewportCenter(viewportSize, zoomFactor);

    public void SetZoomAt(double zoom, Point viewportPoint)
    {
        var graphPoint = ScreenToGraph(viewportPoint);
        Zoom = zoom;
        PanX = GraphCoordinateTransform.Sanitize(viewportPoint.X) - graphPoint.X * Zoom;
        PanY = GraphCoordinateTransform.Sanitize(viewportPoint.Y) - graphPoint.Y * Zoom;
    }

    public void ZoomTo(double zoom, Point viewportPoint) => SetZoomAt(zoom, viewportPoint);

    public void ZoomIn(Point viewportPoint, double zoomFactor = DefaultZoomFactor)
        => ZoomAt(viewportPoint, zoomFactor);

    public void ZoomOut(Point viewportPoint, double zoomFactor = DefaultZoomFactor)
        => ZoomAt(viewportPoint, 1 / zoomFactor);

    public void ResetView()
    {
        _zoom = 1;
        _panX = 0;
        _panY = 0;
    }

    /// <summary>
    /// Fits graph bounds into a viewport, leaving a margin on each side.
    /// Invalid, empty, or wholly degenerate bounds reset to a finite default view.
    /// </summary>
    public void FitToBounds(Rect bounds, Size viewportSize, double margin = DefaultFitMargin)
    {
        if (!IsFiniteRect(bounds) || !IsUsableSize(viewportSize))
        {
            ResetView();
            return;
        }

        var safeMargin = double.IsFinite(margin) ? Math.Max(0, margin) : DefaultFitMargin;
        var availableWidth = Math.Max(0, viewportSize.Width - 2 * safeMargin);
        var availableHeight = Math.Max(0, viewportSize.Height - 2 * safeMargin);
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            ResetView();
            return;
        }

        var width = bounds.Width;
        var height = bounds.Height;
        var hasWidth = width > double.Epsilon;
        var hasHeight = height > double.Epsilon;
        if (!hasWidth && !hasHeight)
        {
            ResetView();
            PanX = viewportSize.Width / 2 - bounds.X;
            PanY = viewportSize.Height / 2 - bounds.Y;
            return;
        }

        var widthZoom = hasWidth ? availableWidth / width : double.PositiveInfinity;
        var heightZoom = hasHeight ? availableHeight / height : double.PositiveInfinity;
        Zoom = Math.Min(widthZoom, heightZoom);
        var center = bounds.Location + new Vector(width / 2, height / 2);
        var viewportCenter = new Point(viewportSize.Width / 2, viewportSize.Height / 2);
        PanX = viewportCenter.X - center.X * Zoom;
        PanY = viewportCenter.Y - center.Y * Zoom;
    }

    public void FitToBounds(Rect bounds, double viewportWidth, double viewportHeight, double margin = DefaultFitMargin)
        => FitToBounds(bounds, new Size(viewportWidth, viewportHeight), margin);

    public void FitToBounds(IEnumerable<Point>? points, Size viewportSize, double margin = DefaultFitMargin)
    {
        if (points is null)
        {
            ResetView();
            return;
        }

        var finitePoints = points.Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)).ToArray();
        if (finitePoints.Length == 0)
        {
            ResetView();
            return;
        }

        var minX = finitePoints.Min(point => point.X);
        var minY = finitePoints.Min(point => point.Y);
        var maxX = finitePoints.Max(point => point.X);
        var maxY = finitePoints.Max(point => point.Y);
        FitToBounds(new Rect(minX, minY, maxX - minX, maxY - minY), viewportSize, margin);
    }

    private static bool IsUsableSize(Size size)
        => double.IsFinite(size.Width) && double.IsFinite(size.Height) && size.Width > 0 && size.Height > 0;

    private static bool IsFiniteRect(Rect rect)
        => !rect.IsEmpty && double.IsFinite(rect.X) && double.IsFinite(rect.Y) &&
           double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Width >= 0 && rect.Height >= 0;
}
