using System.Windows;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GraphAccessibilityTests
{
    [STATestMethod]
    public void SharedCanvasPeersExposeDeclaredAutomationIds()
    {
        AssertCanvasAutomationId(new StoryFlowEditorView(), "CanvasViewport", "StoryFlowCanvasViewport");
        var projectGraph = new ProjectGraphView();
        var editor = projectGraph.FindName("Editor") as CanonicalGraphEditorView;
        Assert.IsNotNull(editor);
        AssertAutomationId(editor.ViewportElement, "CanonicalGraphViewport");
    }

    private static void AssertCanvasAutomationId(FrameworkElement view, string elementName, string expectedId)
    {
        var canvasViewport = view.FindName(elementName) as UIElement;
        Assert.IsNotNull(canvasViewport);
        AssertAutomationId(canvasViewport, expectedId);
    }

    private static void AssertAutomationId(UIElement element, string expectedId)
    {
        // CanonicalGraphEditorView uses a Border viewport, which has no
        // default automation peer until hosted. Verify the declared attached
        // automation identity directly at this view-level boundary.
        Assert.AreEqual(expectedId, System.Windows.Automation.AutomationProperties.GetAutomationId(element));
    }
}
