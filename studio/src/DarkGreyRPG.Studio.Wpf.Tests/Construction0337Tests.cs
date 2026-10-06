using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass, DoNotParallelize]
public sealed class Construction0337Tests
{
    [STATestMethod]
    public void LogicInputsInBothSurfacesKeepStableIdsWiresUndoAndMinimumAcrossScopes()
    {
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session, GraphScope.Task })
        foreach (var type in new[] { "and", "or" })
        {
            var gate = new GraphNodeAuthoringService().Create(new GraphDocument(), scope, type, "gate").Candidate!;
            var source = GraphNodeFactory.Create(scope, "logic_input", "source");
            source.Properties["port_id"] = JsonSerializer.SerializeToElement("external");
            source.Properties["display_name"] = JsonSerializer.SerializeToElement("External");
            var first = gate.Ports.First(port => port.IsInput).Id;
            var graph = new GraphDocument([source, gate], [new("source", "logic_out", "gate", first, GraphInterfaceKind.Logic)]);
            var host = new GraphEditorHostViewModel(graph, scope);
            using var inspector = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single(node => node.NodeId == "gate"));
            Assert.HasCount(2, inspector.LogicInputs);
            Assert.IsFalse(inspector.LogicInputs[0].RemoveCommand.CanExecute(null));
            var inline = new CanonicalInlineNodeEditorControl { Editor = inspector, Width = 240 };
            var inspect = new LogicInputsEditor { DataContext = inspector, Width = 300 };
            foreach (var view in new FrameworkElement[] { inline, inspect })
            {
                Layout(view);
                var add = Descendants(view).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "AddLogicInput");
                Assert.AreEqual(Visibility.Visible, add.Visibility);
                add.Command.Execute(null);
            }
            Assert.HasCount(4, inspector.LogicInputs);
            while (inspector.LogicInputs.Count < 8) Assert.IsTrue(inspector.AddLogicInput());
            var ids = inspector.LogicInputs.Select(row => row.PortId).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            var reopened = GraphDocument.FromJson(host.Graph.ToJson());
            CollectionAssert.AreEqual(ids, reopened.Nodes.Single(node => node.Id == "gate").Ports.Where(port => port.IsInput).OrderBy(port => port.Order).Select(port => port.Id).ToArray());
            Assert.IsFalse(inspector.RemoveLogicInput(first));
            Assert.HasCount(1, host.Graph.Connections);
            inspector.LogicInputRemovalConfirmationRequested = (_, _) => true;
            Assert.IsTrue(inspector.RemoveLogicInput(first));
            Assert.IsEmpty(host.Graph.Connections);
            Assert.IsTrue(host.Undo());
            Assert.HasCount(8, inspector.LogicInputs);
            Assert.HasCount(1, host.Graph.Connections);
            Assert.IsTrue(host.Redo());
            while (inspector.LogicInputs.Count > 2) Assert.IsTrue(inspector.RemoveLogicInput(inspector.LogicInputs[0].PortId));
            Assert.IsFalse(inspector.RemoveLogicInput(inspector.LogicInputs[0].PortId));
            Assert.HasCount(1, host.Graph.Nodes.Single(node => node.Id == "gate").Ports.Where(port => !port.IsInput).ToArray());
            inspector.CanEditLogicInputs = () => false;
            Assert.IsFalse(inspector.AddLogicInput());
        }
    }

    [STATestMethod]
    public void DropFeedbackExpiresReplacesAndClearsOnSwitchAndDispose()
    {
        using var workspace = Workspace();
        var gate = workspace.ActiveGraphHost.Nodes.Single();
        var actor = workspace.ActorItems.Single();
        Assert.IsFalse(workspace.CanApplyResourceToNodeParameter(gate, actor));
        Assert.IsFalse(workspace.ApplyResourceToNodeParameter(gate, actor));
        Assert.IsTrue(workspace.HasParameterDropMessage);
        Pump(1800);
        Assert.IsFalse(workspace.ApplyResourceToNodeParameter(gate, actor));
        Pump(1500);
        Assert.IsTrue(workspace.HasParameterDropMessage, "Earlier expiry must not erase replacement with identical text.");
        Pump(1700);
        Assert.IsFalse(workspace.HasParameterDropMessage);
        workspace.ApplyResourceToNodeParameter(gate, actor);
        Assert.IsTrue(workspace.OpenGraphResource(workspace.SessionItems.Single()));
        Assert.IsFalse(workspace.HasParameterDropMessage);
        var line = workspace.ActiveGraphHost.Nodes.Single();
        Assert.IsTrue(workspace.CanApplyResourceToNodeParameter(line, actor));
        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(line, actor));
        var before = workspace.ActiveGraphHost.Graph.ToJson();
        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(line, actor));
        Assert.AreEqual(before, workspace.ActiveGraphHost.Graph.ToJson(), "Repeat is a harmless success.");
        workspace.Dispose();
        Pump(50);
        Assert.IsFalse(workspace.HasParameterDropMessage);
    }

    [STATestMethod]
    public void PixelWheelMovesContentContinuouslyAndKeepsVirtualization()
    {
        var list = new ListBox { ItemsSource = Enumerable.Range(0, 500).Select(index => $"Item {index}").ToArray() };
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        var window = new Window { Width = 320, Height = 200, Content = list, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            var viewer = Descendants(list).OfType<ScrollViewer>().Single();
            PixelScroll.Enable(viewer);
            window.UpdateLayout();
            Assert.IsTrue(viewer.CanContentScroll);
            Assert.AreEqual(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(list));
            viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                { RoutedEvent = UIElement.PreviewMouseWheelEvent, Source = viewer });
            if (SystemParameters.ClientAreaAnimation)
            {
                Assert.AreEqual(0d, viewer.VerticalOffset);
                Pump(50); Assert.IsTrue(viewer.VerticalOffset > 0 && viewer.VerticalOffset < 48);
            }
            Pump(450); window.UpdateLayout();
            Assert.AreEqual(48d, viewer.VerticalOffset, .1);
            Assert.IsTrue(Descendants(list).OfType<ListBoxItem>().Count() < 100);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void RetiredBottomTabFallsBackToOutput()
    {
        var panel = new BottomPanelViewModel { SelectedTab = new("Minecraft", "Minecraft", "Minecraft") };
        Assert.AreEqual("Output", panel.SelectedTab.Page);
    }

    private static CanonicalStoryWorkspaceViewModel Workspace() => new(
        new(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story", new GraphDocument([GraphNodeFactory.Create(GraphScope.StoryFlow, "and", "gate")])),
        actors: [new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~speaker", "Speaker", "speaker.json", [])],
        sessions: [new(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session", new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line")]))]);
    private static void Layout(FrameworkElement view) { view.Measure(new Size(1000, 1000)); view.Arrange(new Rect(0, 0, 1000, 1000)); view.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
