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
    public void CurrentNodeInvocationUsesEffectivePortIdAfterLayout()
    {
        var host = new ViewModels.Graph.GraphEditorHostViewModel(new GraphDocument([
            Core.Graphs.Definitions.GraphNodeFactory.Create(Core.Graphs.Definitions.GraphScope.Session, "line", "line")]), Core.Graphs.Definitions.GraphScope.Session);
        var control = new CanonicalGraphNodeControl(host.Nodes.Single());
        var root = new Grid { Width = 500, Height = 300 }; root.Children.Add(control);
        root.Measure(new Size(500, 300)); root.Arrange(new Rect(0, 0, 500, 300)); root.UpdateLayout();
        foreach (var port in control.PortControls.Where(port => port.InterfaceKind == GraphInterfaceKind.Flow))
        {
            var stableId = port.EffectivePortId;
            port.DisplayName = "显示名称";
            string? invoked = null;
            port.Click += (_, _) => invoked = port.EffectivePortId;
            port.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(stableId, invoked);
            Assert.AreNotEqual(port.DisplayName, invoked);
        }
        Assert.HasCount(2, control.PortControls);
    }
}
