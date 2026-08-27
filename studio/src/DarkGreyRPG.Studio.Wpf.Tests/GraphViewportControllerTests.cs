using System.Windows;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GraphViewportControllerTests
{
    [TestMethod]
    public void ZoomIsClampedToSupportedRange()
    {
        var controller = new GraphViewportController();
        controller.Zoom = -2;
        Assert.AreEqual(GraphCoordinateTransform.MinZoom, controller.Zoom);
        controller.Zoom = 4;
        Assert.AreEqual(GraphCoordinateTransform.MaxZoom, controller.Zoom);
    }

    [TestMethod]
    public void CursorCenteredZoomPreservesGraphPointUnderCursor()
    {
        var controller = new GraphViewportController { PanX = 70, PanY = -15, Zoom = 1.25 };
        var cursor = new Point(410, 235);
        var before = controller.ScreenToGraph(cursor);

        controller.ZoomAtCursor(cursor, 1.6);

        var after = controller.ScreenToGraph(cursor);
        Assert.AreEqual(before.X, after.X, 1e-10);
        Assert.AreEqual(before.Y, after.Y, 1e-10);
        Assert.AreEqual(2, controller.Zoom, 1e-10);
    }

    [TestMethod]
    public void ViewportCenteredZoomPreservesGraphPointAtCenter()
    {
        var controller = new GraphViewportController { PanX = -40, PanY = 25, Zoom = .8 };
        var viewport = new Size(1000, 600);
        var center = new Point(viewport.Width / 2, viewport.Height / 2);
        var before = controller.ScreenToGraph(center);

        controller.ZoomAtViewportCenter(viewport, 1.5);

        var after = controller.ScreenToGraph(center);
        Assert.AreEqual(before.X, after.X, 1e-10);
        Assert.AreEqual(before.Y, after.Y, 1e-10);
        Assert.AreEqual(1.2, controller.Zoom, 1e-10);
    }

    [TestMethod]
    public void PanByAndResetViewAreDeterministic()
    {
        var controller = new GraphViewportController();
        controller.PanBy(new Vector(32, -18));
        controller.Zoom = 1.75;
        Assert.AreEqual(new Point(32, -18), controller.GraphToScreen(new Point(0, 0)));

        controller.ResetView();

        Assert.AreEqual(1, controller.Zoom);
        Assert.AreEqual(0, controller.PanX);
        Assert.AreEqual(0, controller.PanY);
        Assert.AreEqual(new Point(12, -4), controller.GraphToScreen(new Point(12, -4)));
    }

    [TestMethod]
    public void FitToBoundsCentersAndFitsBounds()
    {
        var controller = new GraphViewportController();
        controller.FitToBounds(new Rect(100, 50, 400, 200), new Size(1000, 600), 20);

        Assert.AreEqual(2.4, controller.Zoom, 1e-10);
        Assert.AreEqual(new Point(20, 60), controller.GraphToScreen(new Point(100, 50)));
        Assert.AreEqual(new Point(980, 540), controller.GraphToScreen(new Point(500, 250)));
    }

    [TestMethod]
    public void FitToBoundsHandlesEmptyDegenerateAndInvalidInputsSafely()
    {
        var controller = new GraphViewportController { Zoom = 2, PanX = 10, PanY = 20 };
        controller.FitToBounds(Rect.Empty, new Size(800, 500));
        AssertFinite(controller);

        controller.FitToBounds(new Rect(15, 25, 0, 0), new Size(800, 500));
        AssertFinite(controller);
        Assert.AreEqual(new Point(400, 250), controller.GraphToScreen(new Point(15, 25)));

        controller.FitToBounds(new Rect(0, 0, 10, 10), new Size(double.NaN, 500));
        AssertFinite(controller);
    }

    private static void AssertFinite(GraphViewportController controller)
    {
        Assert.IsTrue(double.IsFinite(controller.Zoom));
        Assert.IsTrue(double.IsFinite(controller.PanX));
        Assert.IsTrue(double.IsFinite(controller.PanY));
    }
}
