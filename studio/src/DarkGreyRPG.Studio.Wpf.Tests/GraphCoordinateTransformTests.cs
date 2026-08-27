using System.Windows;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GraphCoordinateTransformTests
{
    [TestMethod]
    public void GraphAndScreenConversionsRoundTrip()
    {
        var transform = new GraphCoordinateTransform(1.5, 40, -12);
        var graph = new Point(12.25, -8.5);

        var screen = transform.GraphToScreen(graph);
        var roundTrip = transform.ScreenToGraph(screen);

        Assert.AreEqual(graph.X, roundTrip.X, 1e-10);
        Assert.AreEqual(graph.Y, roundTrip.Y, 1e-10);
        Assert.AreEqual(58.375, screen.X, 1e-10);
        Assert.AreEqual(-24.75, screen.Y, 1e-10);
    }

    [TestMethod]
    public void ConstructorClampsZoomAndSanitizesNonFiniteState()
    {
        var low = new GraphCoordinateTransform(0, double.NaN, double.PositiveInfinity);
        var high = new GraphCoordinateTransform(9, double.NegativeInfinity, double.NaN);

        Assert.AreEqual(GraphCoordinateTransform.MinZoom, low.Zoom);
        Assert.AreEqual(GraphCoordinateTransform.MaxZoom, high.Zoom);
        Assert.AreEqual(0, low.PanX);
        Assert.AreEqual(0, low.PanY);
        Assert.IsTrue(double.IsFinite(high.PanX) && double.IsFinite(high.PanY));
    }
}
