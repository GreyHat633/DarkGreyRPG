using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.Core.Graphs;
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
    public void LegacyPortNameProvidesCompatibleIdAndDisplayNameAndFlowDefault()
    {
        var control = CreateControl("node", "next", isInput: false);

        Assert.AreEqual(GraphInterfaceKind.Flow, control.InterfaceKind);
        Assert.AreEqual("next", control.EffectivePortId);
        Assert.AreEqual("next", control.EffectiveDisplayName);
        Assert.AreEqual("node 输出端口 next", AutomationProperties.GetName(control));
    }

    [STATestMethod]
    public void StablePortIdSurvivesDisplayNameRename()
    {
        var control = CreateControl("node", "check", isInput: false);
        control.PortId = "check_condition";
        control.DisplayName = "检查条件";

        Assert.AreEqual("check_condition", control.EffectivePortId);
        Assert.AreEqual("检查条件", control.EffectiveDisplayName);

        control.DisplayName = "条件已满足";

        Assert.AreEqual("check_condition", control.EffectivePortId);
        Assert.AreEqual("条件已满足", ((TextBlock)((StackPanel)control.Content!).Children[0]).Text);
    }

    [STATestMethod]
    public void EndpointCompatibilityRequiresIdsDifferentNodesOppositeDirectionAndSameKind()
    {
        var output = CreateControl("source", "out", isInput: false);
        var input = CreateControl("target", "in", isInput: true);

        Assert.IsTrue(output.IsCompatibleEndpoint(input));
        Assert.IsTrue(input.IsCompatibleWith(output));

        input.InterfaceKind = GraphInterfaceKind.Logic;
        Assert.IsFalse(output.IsCompatibleEndpoint(input));
        input.InterfaceKind = GraphInterfaceKind.Flow;
        input.IsInput = false;
        Assert.IsFalse(output.IsCompatibleEndpoint(input));
        input.IsInput = true;
        input.NodeId = output.NodeId;
        Assert.IsFalse(output.IsCompatibleEndpoint(input));
        input.NodeId = "target";
        input.PortId = "   ";
        input.PortName = "   ";
        Assert.IsFalse(output.IsCompatibleEndpoint(input));

        Assert.AreEqual("out", output.EffectivePortId);
        Assert.AreEqual("source", output.NodeId);
        Assert.IsFalse(output.IsInput);
    }

    [STATestMethod]
    public void LogicUsesAmberDiamondAndLogicAutomationSemantics()
    {
        var control = CreateControl("node", "condition_internal", isInput: false);
        control.PortId = "condition_internal";
        control.DisplayName = "条件";
        control.InterfaceKind = GraphInterfaceKind.Logic;

        var (slot, shape) = GetShape(control);
        Assert.AreEqual(11d, slot.Width);
        Assert.IsInstanceOfType(shape, typeof(Polygon));
        Assert.AreEqual(9d, shape.Width);
        Assert.IsGreaterThan((byte)200, ((SolidColorBrush)((Polygon)shape).Fill).Color.R);
        StringAssert.Contains(AutomationProperties.GetName(control), "逻辑输出端口");
        StringAssert.Contains((string)control.ToolTip, "逻辑输出端口");
        Assert.IsFalse(AutomationProperties.GetName(control).Contains("condition_internal", StringComparison.Ordinal));
        Assert.IsFalse(((string)control.ToolTip).Contains("condition_internal", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void LogicHighlightKeepsAnchorSlotStable()
    {
        var control = CreateControl("node", "condition", isInput: true);
        control.InterfaceKind = GraphInterfaceKind.Logic;
        var normal = GetShape(control);

        control.IsValidTarget = true;
        var highlighted = GetShape(control);

        Assert.AreEqual(11d, normal.Slot.Width);
        Assert.AreEqual(11d, highlighted.Slot.Width);
        Assert.AreEqual(9d, normal.Shape.Width);
        Assert.AreEqual(11d, highlighted.Shape.Width);
        Assert.AreEqual(11d, highlighted.Slot.Height);
    }

    [STATestMethod]
    public void LogicAnchorPointIsFiniteAfterLayoutInSharedVisualTree()
    {
        var root = new Grid { Width = 100, Height = 100 };
        var control = CreateControl("node", "condition", isInput: true);
        control.InterfaceKind = GraphInterfaceKind.Logic;
        root.Children.Add(control);

        root.Measure(new Size(100, 100));
        root.Arrange(new Rect(0, 0, 100, 100));
        root.UpdateLayout();

        var anchor = control.GetAnchorPoint(root);

        Assert.IsTrue(double.IsFinite(anchor.X));
        Assert.IsTrue(double.IsFinite(anchor.Y));
        Assert.IsTrue(anchor.X >= 0 && anchor.X <= root.ActualWidth);
        Assert.IsTrue(anchor.Y >= 0 && anchor.Y <= root.ActualHeight);
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
        var slot = panel.Children.OfType<Grid>().Single();
        return (slot, (Ellipse)slot.Children[0]);
    }

    private static (Grid Slot, FrameworkElement Shape) GetShape(FlowPortControl control)
    {
        var panel = (StackPanel)control.Content!;
        var slot = panel.Children.OfType<Grid>().Single();
        return (slot, (FrameworkElement)slot.Children[0]);
    }
}
