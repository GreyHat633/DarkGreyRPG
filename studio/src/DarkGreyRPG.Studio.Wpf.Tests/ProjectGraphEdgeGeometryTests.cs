using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ProjectGraphEdgeGeometryTests
{
    [TestMethod]
    public void NormalEdgeEndsAtTargetAndProducesFiniteArrow()
    {
        var target = new Rect(400, 200, 210, 82);
        var shape = ProjectGraphEdgeGeometry.Create(new Rect(40, 80, 210, 82), target, false);
        var arrow = ProjectGraphEdgeGeometry.CreateArrow(shape);

        Assert.IsFalse(shape.IsSelfLoop);
        Assert.AreEqual(target.Left, shape.End.X);
        Assert.AreEqual(target.Top + target.Height / 2, shape.End.Y);
        Assert.HasCount(3, arrow);
        Assert.IsTrue(arrow.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [TestMethod]
    public void SelfLoopHasVisibleBoundsAboveAndRightOfNodeWithArrowAtTargetEnd()
    {
        var node = new Rect(120, 160, 210, 82);
        var shape = ProjectGraphEdgeGeometry.Create(node, node, true);
        var arrow = ProjectGraphEdgeGeometry.CreateArrow(shape);

        Assert.IsTrue(shape.IsSelfLoop);
        Assert.IsFalse(shape.Geometry.Bounds.IsEmpty);
        Assert.IsLessThan(node.Top, shape.Geometry.Bounds.Top);
        Assert.IsGreaterThan(node.Right, shape.Geometry.Bounds.Right);
        Assert.AreEqual(shape.End, arrow[0]);
    }

    [STATestMethod]
    public void HitPathExposesInvokeAutomationPattern()
    {
        var invoked = false;
        var path = new ProjectGraphAccessiblePath
        {
            Data = new LineGeometry(new Point(0, 0), new Point(100, 40)),
            InvokeAction = () => invoked = true,
        };

        var peer = UIElementAutomationPeer.CreatePeerForElement(path);
        Assert.IsNotNull(peer);
        var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
        Assert.IsNotNull(provider);

        provider.Invoke();

        Assert.IsTrue(invoked);
    }
}
