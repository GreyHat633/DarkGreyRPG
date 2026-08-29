using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GraphConnectionVisualStyleTests
{
    [TestMethod]
    public void FlowUsesCyanBlueThreeDipStrokeAndFlowAutomationText()
    {
        var style = GraphConnectionVisualStyle.For(GraphInterfaceKind.Flow);

        Assert.AreEqual(Color.FromRgb(108, 177, 255), style.StrokeColor);
        Assert.AreEqual(3d, style.StrokeThickness);
        StringAssert.Contains(style.AutomationLabel, "Flow");
    }

    [TestMethod]
    public void LogicUsesAmberGoldTwoDipStrokeAndLogicAutomationText()
    {
        var style = GraphConnectionVisualStyle.Get(GraphInterfaceKind.Logic);

        Assert.AreEqual(Color.FromRgb(245, 181, 61), style.StrokeColor);
        Assert.AreEqual(2d, style.StrokeThickness);
        StringAssert.Contains(style.AutomationName, "Logic");
    }

    [TestMethod]
    public void SelectedVariantsIncreaseContrastAndThicknessWithoutChangingKind()
    {
        var flow = GraphConnectionVisualStyle.For(GraphInterfaceKind.Flow, selected: true);
        var logic = GraphConnectionVisualStyle.For(GraphInterfaceKind.Logic, selected: true);

        Assert.AreEqual(GraphInterfaceKind.Flow, flow.InterfaceKind);
        Assert.AreEqual(GraphInterfaceKind.Logic, logic.InterfaceKind);
        Assert.AreEqual(4d, flow.StrokeThickness);
        Assert.AreEqual(3d, logic.StrokeThickness);
        Assert.AreNotEqual(GraphConnectionVisualStyle.FlowNormalColor, flow.StrokeColor);
        Assert.AreNotEqual(GraphConnectionVisualStyle.LogicNormalColor, logic.StrokeColor);
        StringAssert.Contains(flow.AutomationLabel, "Flow");
        StringAssert.Contains(logic.AutomationLabel, "Logic");
    }

    [STATestMethod]
    public void StoryFlowNodeInvocationCarriesStableEffectivePortIdsAfterLayout()
    {
        var node = new StoryFlowNodeEditorItem(
            "sequence_node",
            "Sequence",
            0,
            0,
            new Dictionary<string, string>(),
            edit => edit());
        var control = new StoryFlowNodeControl(node);
        var root = new Grid { Width = 500, Height = 300 };
        root.Children.Add(control);

        root.Measure(new Size(500, 300));
        root.Arrange(new Rect(0, 0, 500, 300));
        root.UpdateLayout();

        var output = control.OutputPorts.Single();
        output.PortId = "stable_step_id";
        output.DisplayName = "显示步骤";
        FlowPortInvokedEventArgs? outputArgs = null;
        control.OutputInvoked += (_, args) => outputArgs = args;

        output.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.IsNotNull(outputArgs);
        Assert.AreEqual("stable_step_id", outputArgs!.PortName);
        Assert.AreEqual(output.EffectivePortId, outputArgs.Port.EffectivePortId);
        Assert.AreNotEqual(output.DisplayName, outputArgs.PortName);

        var input = control.Input;
        input.PortId = "stable_input_id";
        input.DisplayName = "显示输入";
        FlowPortInvokedEventArgs? inputArgs = null;
        control.InputInvoked += (_, args) => inputArgs = args;

        input.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.IsNotNull(inputArgs);
        Assert.AreEqual("stable_input_id", inputArgs!.PortName);
        Assert.AreEqual(input.EffectivePortId, inputArgs.Port.EffectivePortId);
        Assert.AreNotEqual(input.DisplayName, inputArgs.PortName);
    }
}
