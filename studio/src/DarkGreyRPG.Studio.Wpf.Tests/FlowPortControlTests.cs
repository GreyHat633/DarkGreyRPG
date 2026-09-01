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
    public void HighlightChangesOnlyEllipseSizeInsideCenteredHitTarget()
    {
        var control = CreateControl("node", "next", isInput: false);
        var normal = GetAnchor(control);

        Assert.AreEqual(9d, normal.Ellipse.Width);
        Assert.AreEqual(20d, normal.Slot.Width);
        Assert.AreEqual(20d, normal.Slot.Height);

        control.IsConnecting = true;
        var highlighted = GetAnchor(control);

        Assert.AreEqual(11d, highlighted.Ellipse.Width);
        Assert.AreEqual(11d, highlighted.Ellipse.Height);
        Assert.AreEqual(20d, highlighted.Slot.Width);
        Assert.AreEqual(20d, highlighted.Slot.Height);
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
        Assert.AreEqual("条件已满足", ((TextBlock)((Grid)control.Content!).Children[0]).Text);
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
        Assert.AreEqual(20d, slot.Width);
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

        Assert.AreEqual(20d, normal.Slot.Width);
        Assert.AreEqual(20d, highlighted.Slot.Width);
        Assert.AreEqual(9d, normal.Shape.Width);
        Assert.AreEqual(11d, highlighted.Shape.Width);
        Assert.AreEqual(20d, highlighted.Slot.Height);
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
    public void InputFallbackUsesFixedLeftAnchorSlotCenterForFlowAndLogic()
    {
        var root = new Grid { Width = 400, Height = 100 };
        var flow = CreateControl("flow", "a very long input label", isInput: true);
        var logic = CreateControl("logic", "另一个很长的逻辑输入标签", isInput: true);
        logic.InterfaceKind = GraphInterfaceKind.Logic;
        root.Children.Add(flow);
        root.Children.Add(logic);
        root.Measure(new Size(400, 100));
        root.Arrange(new Rect(0, 0, 400, 100));

        CollapseAnchor(flow);
        CollapseAnchor(logic);
        root.UpdateLayout();

        Assert.AreEqual(10d, flow.GetAnchorPoint(flow).X, 0.01);
        Assert.AreEqual(10d, logic.GetAnchorPoint(logic).X, 0.01);
    }

    [STATestMethod]
    public void OutputFallbackUsesRightAnchorSlotCenterWithoutLabelCenterDrift()
    {
        var root = new Grid { Width = 400, Height = 100 };
        var shortOutput = CreateControl("short", "x", isInput: false);
        var longOutput = CreateControl("long", "一个非常长的输出标签", isInput: false);
        var logicOutput = CreateControl("logic", "逻辑输出标签", isInput: false);
        logicOutput.InterfaceKind = GraphInterfaceKind.Logic;
        shortOutput.Width = 120;
        longOutput.Width = 120;
        logicOutput.Width = 120;
        root.Children.Add(shortOutput);
        root.Children.Add(longOutput);
        root.Children.Add(logicOutput);
        root.Measure(new Size(400, 100));
        root.Arrange(new Rect(0, 0, 400, 100));

        CollapseAnchor(shortOutput);
        CollapseAnchor(longOutput);
        CollapseAnchor(logicOutput);
        root.UpdateLayout();

        Assert.AreEqual(110d, shortOutput.GetAnchorPoint(shortOutput).X, 0.01);
        Assert.AreEqual(110d, longOutput.GetAnchorPoint(longOutput).X, 0.01);
        Assert.AreEqual(110d, logicOutput.GetAnchorPoint(logicOutput).X, 0.01);
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

    [STATestMethod]
    public void OnlyRenderedAnchorIsAWireHitTarget()
    {
        var flow = CreateControl("node", "branch", isInput: false);
        var flowPanel = (Grid)flow.Content!;
        var flowAnchor = flowPanel.Children.OfType<Grid>().Single().Children[0];
        var flowLabel = flowPanel.Children.OfType<TextBlock>().Single();

        Assert.IsTrue(flow.IsAnchorHitTarget(flowAnchor));
        Assert.IsFalse(flow.IsAnchorHitTarget(flowLabel));

        var logic = CreateControl("node", "condition", isInput: true);
        logic.InterfaceKind = GraphInterfaceKind.Logic;
        var logicPanel = (Grid)logic.Content!;
        var logicAnchor = logicPanel.Children.OfType<Grid>().Single().Children[0];
        var logicLabel = logicPanel.Children.OfType<TextBlock>().Single();

        Assert.IsTrue(logic.IsAnchorHitTarget(logicAnchor));
        Assert.IsFalse(logic.IsAnchorHitTarget(logicLabel));
    }

    [STATestMethod]
    public void AnchorHitTargetIsCenteredAndDoesNotIncludeLabel()
    {
        var control = CreateControl("node", "a long output label", isInput: false);
        var panel = (Grid)control.Content!;
        var surface = panel.Children.OfType<Grid>().Single();
        var label = panel.Children.OfType<TextBlock>().Single();

        Assert.AreEqual(20d, surface.Width);
        Assert.AreEqual(20d, surface.Height);
        Assert.IsTrue(control.IsAnchorHitTarget(surface));
        Assert.IsFalse(control.IsAnchorHitTarget(label));
        Assert.AreEqual(9d, ((Ellipse)surface.Children[0]).Width);
        Assert.AreEqual(HorizontalAlignment.Center, surface.HorizontalAlignment);
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
        var panel = (Grid)control.Content!;
        var slot = panel.Children.OfType<Grid>().Single();
        return (slot, (Ellipse)slot.Children[0]);
    }

    private static (Grid Slot, FrameworkElement Shape) GetShape(FlowPortControl control)
    {
        var panel = (Grid)control.Content!;
        var slot = panel.Children.OfType<Grid>().Single();
        return (slot, (FrameworkElement)slot.Children[0]);
    }

    private static void CollapseAnchor(FlowPortControl control)
    {
        var panel = (Grid)control.Content!;
        panel.Children.OfType<Grid>().Single().Children[0].Visibility = Visibility.Collapsed;
    }
}
