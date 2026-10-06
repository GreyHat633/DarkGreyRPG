using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass, DoNotParallelize]
public sealed class LogicInputCards0337Tests
{
    [STATestMethod]
    public void NamesOrderSelectionAndWiresStayStableAcrossScopesAndProjections()
    {
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session, GraphScope.Task })
        foreach (var type in new[] { "and", "or" })
        {
            var host = Host(scope, type);
            using var inline = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single(node => node.NodeId == "gate"));
            using var inspect = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single(node => node.NodeId == "gate"));
            var first = inline.LogicInputs[0];
            Assert.AreEqual("逻辑输入", inline.LogicInputsTitle);
            Assert.IsTrue(inline.AddLogicInput());
            Assert.IsTrue(inline.AddLogicInput());
            Assert.IsTrue(inline.RemoveSelectedLogicInputCommand.CanExecute(null));
            inline.SelectLogicInput(first.PortId);
            Assert.IsTrue(inspect.LogicInputs.Single(row => row.PortId == first.PortId).IsSelected);
            first.DisplayName = "条件成立";
            Assert.AreSame(first, inline.LogicInputs[0]);
            Assert.AreEqual("条件成立", inspect.LogicInputs[0].DisplayName);
            first.DisplayName = " ";
            Assert.AreEqual("条件成立", first.DisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(first.NameError));
            first.DisplayName = inline.LogicInputs[1].DisplayName;
            Assert.AreEqual("条件成立", first.DisplayName);
            var before = host.Graph.ToJson();
            var ids = inline.LogicInputs.Select(row => row.PortId).ToArray();
            Assert.IsTrue(inline.MoveLogicInput(first.PortId, 3));
            Assert.AreSame(first, inline.LogicInputs[3]);
            Assert.AreEqual(first.PortId, inspect.LogicInputs[3].PortId);
            Assert.AreEqual(first.PortId, host.Graph.Connections.Single().ToPortId);
            Assert.IsTrue(inspect.LogicInputs[3].IsSelected);
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(before, host.Graph.ToJson(), "A move is one complete history unit.");
            Assert.IsTrue(host.Redo());
            var restored = new GraphEditorHostViewModel(GraphDocument.FromJson(host.Graph.ToJson()), scope);
            using var reopened = new CanonicalNodeInspectorViewModel(restored, restored.Nodes.Single(node => node.NodeId == "gate"));
            CollectionAssert.AreEqual(ids.Skip(1).Append(first.PortId).ToArray(), reopened.LogicInputs.Select(row => row.PortId).ToArray());
            Assert.AreEqual("条件成立", reopened.LogicInputs.Last().DisplayName);
            Assert.IsFalse(inspect.RemoveSelectedLogicInput(), "Connected input needs confirmation.");
            inspect.LogicInputRemovalConfirmationRequested = (_, count) => count == 1;
            Assert.IsTrue(inspect.RemoveSelectedLogicInput());
            Assert.IsEmpty(host.Graph.Connections);
            Assert.IsFalse(inline.LogicInputs.Any(row => row.IsSelected));
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(first.PortId, host.Graph.Connections.Single().ToPortId);
            inline.SelectLogicInput(first.PortId);
            inline.ClearLogicInputSelection();
            Assert.IsFalse(inspect.LogicInputs.Any(row => row.IsSelected));
            Assert.IsFalse(inspect.RemoveSelectedLogicInputCommand.CanExecute(null));
            inline.CanEditLogicInputs = () => false;
            Assert.IsTrue(inline.LogicInputs.All(row => row.IsReadOnly));
            Assert.IsFalse(inline.RenameLogicInput(first.PortId, "只读"));
            Assert.IsFalse(inline.MoveLogicInput(first.PortId, 0));
        }
    }

    [STATestMethod]
    public void RealControlsCommitCancelAndClearSelectionOutsideWithoutClearingMinusTarget()
    {
        var host = Host(GraphScope.Session, "and");
        using var owner = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single(node => node.NodeId == "gate"));
        var editor = new LogicInputsEditor { DataContext = owner };
        var outside = new Button { Content = "Outside" };
        var panel = new StackPanel(); panel.Children.Add(editor); panel.Children.Add(outside);
        var window = new Window { Content = panel, Width = 340, Height = 420, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            owner.AddLogicInput(); window.UpdateLayout();
            var field = Descendants(editor).OfType<TextBox>().First();
            field.Focus();
            Assert.IsTrue(owner.LogicInputs[0].IsSelected);
            field.Text = "直接输入名称";
            Assert.AreNotEqual(field.Text, owner.LogicInputs[0].DisplayName, "Draft does not mutate per keystroke.");
            RaiseKey(field, window, Key.Enter);
            Assert.AreEqual("直接输入名称", owner.LogicInputs[0].DisplayName);
            field.Text = "取消的草稿";
            RaiseKey(field, window, Key.Escape);
            Assert.AreEqual("直接输入名称", field.Text);
            field.Text = "失焦提交";
            outside.Focus();
            Assert.AreEqual("失焦提交", owner.LogicInputs[0].DisplayName);
            Assert.IsFalse(owner.LogicInputs.Any(row => row.IsSelected));
            field.Focus();
            var minus = Descendants(editor).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "RemoveSelectedLogicInput");
            minus.Focus();
            Assert.IsTrue(owner.LogicInputs[0].IsSelected);
            Assert.IsTrue(minus.Command.CanExecute(null));
            editor.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = editor });
            Assert.IsFalse(owner.LogicInputs.Any(row => row.IsSelected), "Empty editor area clears selection even with keyboard focus retained.");
            var cards = Descendants(editor).OfType<Border>().Where(border => border.Name == "LogicInputCard").ToArray();
            cards[1].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = cards[1] });
            Assert.IsTrue(owner.LogicInputs[1].IsSelected);
            Assert.HasCount(1, Descendants(editor).OfType<Button>().Where(button => (string?)button.Content == "−").ToArray());
            Assert.IsFalse(Descendants(editor).OfType<TextBlock>().Any(text => text.Text.Contains("逻辑输入（")));
            var inputs = Descendants(editor).OfType<ItemsControl>().Single();
            var rows = owner.LogicInputs.Select(row => row.PortId).ToArray();
            using (var preview = new OutputReorderPreview(inputs, owner.LogicInputs[1].DisplayName, false, 1, new Point(15, 65)))
            {
                Assert.IsTrue(preview.GapHeight > 0);
                Assert.AreEqual(0, preview.Locate(new Point(15, 0)));
                CollectionAssert.AreEqual(rows, owner.LogicInputs.Select(row => row.PortId).ToArray(), "Preview leaves model untouched.");
            }
            window.Content = null;
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Assert.IsFalse(owner.LogicInputs.Any(row => row.IsSelected));
        }
        finally { window.Close(); }
    }
    private static GraphEditorHostViewModel Host(GraphScope scope, string type)
    {
        var gate = new GraphNodeAuthoringService().Create(new GraphDocument(), scope, type, "gate").Candidate!;
        var source = GraphNodeFactory.Create(scope, "logic_input", "source");
        source.Properties["port_id"] = JsonSerializer.SerializeToElement("external");
        source.Properties["display_name"] = JsonSerializer.SerializeToElement("External");
        var first = gate.Ports.First(port => port.IsInput).Id;
        return new(new GraphDocument([source, gate], [new("source", "logic_out", "gate", first, GraphInterfaceKind.Logic)]), scope);
    }
    private static void RaiseKey(TextBox input, Window window, Key key) => input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(window), Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent, Source = input });
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
