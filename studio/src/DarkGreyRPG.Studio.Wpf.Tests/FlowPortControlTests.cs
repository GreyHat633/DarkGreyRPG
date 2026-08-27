using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FlowPortControlTests
{
    [STATestMethod]
    public void UsesChromeFreeTransparentButtonSurface()
    {
        var control = CreateControl("node", "next", isInput: false);

        Assert.IsFalse(control.Focusable);
        Assert.IsNull(control.FocusVisualStyle);
        Assert.IsTrue(control.OverridesDefaultStyle);
        Assert.AreSame(Brushes.Transparent, control.Background);
        Assert.AreEqual(0, control.BorderThickness.Left);
        Assert.AreEqual(0, control.BorderThickness.Top);
        Assert.AreEqual(0, control.BorderThickness.Right);
        Assert.AreEqual(0, control.BorderThickness.Bottom);
        Assert.AreEqual(20d, control.MinWidth);
        Assert.AreEqual(20d, control.Height);
        Assert.IsNotNull(control.Template);
        Assert.AreEqual(typeof(Grid), control.Template!.VisualTree.Type);
    }

    [STATestMethod]
    public void HighlightChangesOnlyEllipseSizeInsideStableAnchorSlot()
    {
        var control = CreateControl("node", "next", isInput: false);
        var normal = GetAnchor(control);

        Assert.AreEqual(9d, normal.Ellipse.Width);
        Assert.AreEqual(11d, normal.Slot.Width);
        Assert.AreEqual(11d, normal.Slot.Height);

        control.IsConnecting = true;
        var highlighted = GetAnchor(control);

        Assert.AreEqual(11d, highlighted.Ellipse.Width);
        Assert.AreEqual(11d, highlighted.Ellipse.Height);
        Assert.AreEqual(11d, highlighted.Slot.Width);
        Assert.AreEqual(11d, highlighted.Slot.Height);
    }

    [STATestMethod]
    public void InputAndOutputAutomationNamesRemainStable()
    {
        var input = CreateControl("story", "input", isInput: true);
        Assert.AreEqual("story 输入端口", AutomationProperties.GetName(input));

        input.PortName = "branch";
        Assert.AreEqual("story 入线端口 branch", AutomationProperties.GetName(input));

        input.IsInput = false;
        Assert.AreEqual("story 输出端口 branch", AutomationProperties.GetName(input));
    }

    private static FlowPortControl CreateControl(string nodeId, string portName, bool isInput)
    {
        var control = new FlowPortControl
        {
            NodeId = nodeId,
            PortName = portName,
            IsInput = isInput,
        };
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        return control;
    }

    private static (Grid Slot, Ellipse Ellipse) GetAnchor(FlowPortControl control)
    {
        var panel = (StackPanel)control.Content!;
        var slot = (Grid)panel.Children[^1];
        return (slot, (Ellipse)slot.Children[0]);
    }
}
