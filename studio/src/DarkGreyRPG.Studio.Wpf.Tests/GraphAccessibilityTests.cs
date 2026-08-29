using System.Windows;
using System.Windows.Automation.Peers;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GraphAccessibilityTests
{
    [STATestMethod]
    public void SharedCanvasPeersExposeDeclaredAutomationIds()
    {
        AssertCanvasAutomationId(new StoryFlowEditorView(), "CanvasViewport", "StoryFlowCanvasViewport");
        AssertCanvasAutomationId(new ProjectGraphView(), "CanvasViewport", "ProjectGraphCanvasViewport");
    }

    private static void AssertCanvasAutomationId(FrameworkElement view, string elementName, string expectedId)
    {
        var canvasViewport = view.FindName(elementName) as UIElement;
        Assert.IsNotNull(canvasViewport);

        var peer = UIElementAutomationPeer.CreatePeerForElement(canvasViewport);
        Assert.IsNotNull(peer);
        Assert.AreEqual(expectedId, peer.GetAutomationId());
    }
}
