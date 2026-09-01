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
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node), typeof(GraphEditorNodeViewModel), typeof(CanonicalGraphNodeControl),
        new PropertyMetadata(null, OnNodeChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(CanonicalGraphNodeControl),
        new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty InlineEditorProperty = DependencyProperty.Register(
        nameof(InlineEditor), typeof(CanonicalNodeInspectorViewModel), typeof(CanonicalGraphNodeControl));

    public CanonicalGraphNodeControl()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => UpdatePortAutomation();
    }

    public CanonicalGraphNodeControl(GraphEditorNodeViewModel node,
        CanonicalNodeInspectorViewModel? inlineEditor = null) : this()
    {
        Node = node;
        InlineEditor = inlineEditor;
    }

    public GraphEditorNodeViewModel? Node
    {
        get => (GraphEditorNodeViewModel?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public CanonicalNodeInspectorViewModel? InlineEditor
    {
        get => (CanonicalNodeInspectorViewModel?)GetValue(InlineEditorProperty);
        set => SetValue(InlineEditorProperty, value);
    }

    /// <summary>Transient editor selection; it is never written to graph JSON.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public IReadOnlyList<FlowPortControl> PortControls => _portControls;
    private readonly List<FlowPortControl> _portControls = [];

    /// <summary>Refreshes only this node's rendered port controls.</summary>
    public void RefreshPorts()
    {
        RebuildPorts();
        UpdatePortAutomation();
    }

    public bool IsHeaderDragSource(DependencyObject? source)
        => source is not null && HeaderDragZone.IsAncestorOf(source);

    public bool IsParameterInteractionSource(DependencyObject? source)
        => source is not null && ParameterInteractiveZone.IsAncestorOf(source);

    public void DisposeInlineEditor()
    {
        InlineEditor?.Dispose();
        InlineEditor = null;
    }

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
            control.NodeBorder.SetResourceReference(Border.BorderBrushProperty,
                control.IsSelected ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush");
    }

    private void RebuildPorts()
    {
        if (Node is null) return;
        InputPortsPanel.Children.Clear();
        InputPortsPanel.RowDefinitions.Clear();
        OutputPortsPanel.Children.Clear();
        OutputPortsPanel.RowDefinitions.Clear();
        _portControls.Clear();
        foreach (var item in Node.Inputs) AddPort(InputPortsPanel, item);
        foreach (var item in Node.Outputs) AddPort(OutputPortsPanel, item);
    }

    private void AddPort(Grid panel, GraphEditorPortViewModel item)
    {
        var port = CreatePort(item);
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(port, panel.RowDefinitions.Count - 1);
        panel.Children.Add(port);
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
            // Occupy the complete half of the fixed-width node and align the
            // content toward the corresponding edge. Label length must not
            // move an anchor along that edge.
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = item.IsInput
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
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
