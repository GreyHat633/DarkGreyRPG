using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GraphPointerStateTests
{
    [TestMethod]
    public void StartsIdleAndSupportsAllGestureModes()
    {
        var state = new GraphPointerState();

        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
        foreach (var mode in new[]
                 { GraphPointerMode.NodeDrag, GraphPointerMode.BoxSelect, GraphPointerMode.WireDrag, GraphPointerMode.Pan })
        {
            Assert.IsTrue(state.Begin(mode));
            Assert.IsTrue(state.Is(mode));
            Assert.IsTrue(state.End(mode));
            Assert.IsTrue(state.Is(GraphPointerMode.Idle));
        }
    }

    [TestMethod]
    public void BeginIdleIsRejectedAndDoesNotChangeState()
    {
        var state = new GraphPointerState();

        Assert.IsFalse(state.Begin(GraphPointerMode.Idle));
        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
    }

    [TestMethod]
    public void BeginRejectsOverlappingGestureWithoutDisplacingActiveMode()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.NodeDrag));

        Assert.IsFalse(state.Begin(GraphPointerMode.WireDrag));
        Assert.AreEqual(GraphPointerMode.NodeDrag, state.Mode);
    }

    [TestMethod]
    public void EndRequiresTheExpectedActiveMode()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.WireDrag));

        Assert.IsFalse(state.End(GraphPointerMode.NodeDrag));
        Assert.AreEqual(GraphPointerMode.WireDrag, state.Mode);
        Assert.IsFalse(state.End(GraphPointerMode.Idle));
        Assert.AreEqual(GraphPointerMode.WireDrag, state.Mode);
        Assert.IsTrue(state.End(GraphPointerMode.WireDrag));
        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
    }

    [TestMethod]
    public void CancelReturnsActiveModeAndIsIdempotent()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.BoxSelect));

        Assert.AreEqual(GraphPointerMode.BoxSelect, state.Cancel());
        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
        Assert.AreEqual(GraphPointerMode.Idle, state.Cancel());
        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
    }

    [TestMethod]
    public void PanPreemptsWireDragAndReportsDisplacedMode()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.WireDrag));

        Assert.AreEqual(GraphPointerMode.WireDrag, state.PreemptForPan());
        Assert.AreEqual(GraphPointerMode.Pan, state.Mode);
    }

    [TestMethod]
    public void PanPreemptsNodeDragAndReportsDisplacedMode()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.NodeDrag));

        Assert.AreEqual(GraphPointerMode.NodeDrag, state.PreemptForPan());
        Assert.AreEqual(GraphPointerMode.Pan, state.Mode);
    }

    [TestMethod]
    public void PanPreemptsBoxSelectAndReportsDisplacedMode()
    {
        var state = new GraphPointerState();
        Assert.IsTrue(state.Begin(GraphPointerMode.BoxSelect));

        Assert.AreEqual(GraphPointerMode.BoxSelect, state.PreemptForPan());
        Assert.AreEqual(GraphPointerMode.Pan, state.Mode);
    }

    [TestMethod]
    public void PanCanStartFromIdleAndEndsNormally()
    {
        var state = new GraphPointerState();

        Assert.AreEqual(GraphPointerMode.Idle, state.PreemptForPan());
        Assert.AreEqual(GraphPointerMode.Pan, state.Mode);
        Assert.IsTrue(state.End(GraphPointerMode.Pan));
        Assert.AreEqual(GraphPointerMode.Idle, state.Mode);
    }
}
