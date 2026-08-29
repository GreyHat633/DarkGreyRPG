using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Reusable visual projection of one canonical graph node.</summary>
public partial class CanonicalGraphNodeControl : UserControl
{
    private static readonly Brush DefaultBorderBrush = CreateFrozenBrush(Color.FromRgb(0x59, 0x61, 0x6D));
    private static readonly Brush SelectedBorderBrush = CreateFrozenBrush(Color.FromRgb(0x70, 0xD7, 0xFF));

    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node), typeof(GraphEditorNodeViewModel), typeof(CanonicalGraphNodeControl),
        new PropertyMetadata(null, OnNodeChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(CanonicalGraphNodeControl),
        new PropertyMetadata(false, OnIsSelectedChanged));

    public CanonicalGraphNodeControl()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => UpdatePortAutomation();
    }

    public CanonicalGraphNodeControl(GraphEditorNodeViewModel node) : this() => Node = node;

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public GraphEditorNodeViewModel? Node
    {
        get => (GraphEditorNodeViewModel?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>Transient editor selection; it is never written to graph JSON.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public IReadOnlyList<FlowPortControl> PortControls => _portControls;
    private readonly List<FlowPortControl> _portControls = [];

    private static void OnNodeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (CanonicalGraphNodeControl)sender;
        if (control.Node is not null)
        {
            AutomationProperties.SetName(control, $"图节点 {control.Node.DisplayName} {control.Node.NodeId}");
            AutomationProperties.SetAutomationId(control, $"CanonicalGraphNode_{control.Node.NodeId}");
            control.RebuildPorts();
        }
    }

    private static void OnIsSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (CanonicalGraphNodeControl)sender;
        if (control.NodeBorder is not null)
            control.NodeBorder.BorderBrush = control.IsSelected ? SelectedBorderBrush : DefaultBorderBrush;
    }

    private void RebuildPorts()
    {
        if (Node is null) return;
        InputPortsPanel.Children.Clear();
        OutputPortsPanel.Children.Clear();
        _portControls.Clear();
        foreach (var item in Node.Inputs) InputPortsPanel.Children.Add(CreatePort(item));
        foreach (var item in Node.Outputs) OutputPortsPanel.Children.Add(CreatePort(item));
    }

    private FlowPortControl CreatePort(GraphEditorPortViewModel item)
    {
        var port = new FlowPortControl
        {
            NodeId = Node?.NodeId ?? string.Empty,
            PortId = item.PortId,
            PortName = item.PortId,
            DisplayName = item.DisplayName,
            InterfaceKind = item.InterfaceKind,
            IsInput = item.IsInput,
        };
        var id = port.EffectivePortId;
        var direction = port.IsInput ? "输入" : "输出";
        AutomationProperties.SetAutomationId(port, $"CanonicalGraphPort_{port.NodeId}_{id}");
        AutomationProperties.SetName(port, $"{port.NodeId} {direction}端口 {id} ({port.InterfaceKind})");
        _portControls.Add(port);
        return port;
    }

    private void UpdatePortAutomation()
    {
        foreach (var port in FindVisualChildren<FlowPortControl>(this))
        {
            var id = port.EffectivePortId;
            var direction = port.IsInput ? "输入" : "输出";
            AutomationProperties.SetAutomationId(port, $"CanonicalGraphPort_{port.NodeId}_{id}");
            AutomationProperties.SetName(port, $"{port.NodeId} {direction}端口 {id} ({port.InterfaceKind})");
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T item) yield return item;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }
}
