using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
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

    /// <summary>
    /// The ordered Choice projection used by the visual node.  The row is
    /// keyed by the persisted semantic IDs, rather than by display text or
    /// the incidental order of the node's mixed output list.
    /// </summary>
    public IReadOnlyList<ChoiceOptionRow> ChoiceOptionRows => _choiceOptionRows;

    /// <summary>Compatibility alias for callers that describe this as port rows.</summary>
    public IReadOnlyList<ChoiceOptionRow> ChoiceRows => _choiceOptionRows;

    private readonly List<ChoiceOptionRow> _choiceOptionRows = [];

    /// <summary>Refreshes only this node's rendered port controls.</summary>
    public void RefreshPorts()
    {
        RebuildPorts();
        UpdatePortAutomation();
    }

    public bool IsHeaderDragSource(DependencyObject? source)
        => source is not null && HeaderDragZone.IsAncestorOf(source);

    public bool IsParameterInteractionSource(DependencyObject? source)
    {
        // Routed text events can originate from a Run rather than a Visual.
        while (source is FrameworkContentElement content) source = content.Parent;
        if (source is null || !ParameterInteractiveZone.IsAncestorOf(source)) return false;

        // The parameter expander contains large stretches of layout-only
        // StackPanel/Grid/Border surface.  Only controls which have their own
        // pointer gesture may claim the routed press; the remaining surface is
        // deliberately available for node dragging.
        if (FindAncestor<Expander>(source) is not null
            && FindAncestor<ToggleButton>(source) is not null)
            return true;

        return EntryReorder.IsHandleInteractionSource(source)
            || FindAncestor<TextBoxBase>(source) is not null
            || FindAncestor<PasswordBox>(source) is not null
            || FindAncestor<ComboBox>(source) is not null
            || FindAncestor<ListBoxItem>(source) is not null
            || FindAncestor<TreeViewItem>(source) is not null
            || FindAncestor<ButtonBase>(source) is not null
            || FindAncestor<RangeBase>(source) is not null
            || FindAncestor<Thumb>(source) is not null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

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
            if (NodeHeaderPalette.ForType(control.Node.Type) is { } color)
            {
                control.HeaderDragZone.Background = color;
                control.HeaderTitle.Foreground = NodeHeaderPalette.Foreground;
            }
            else
            {
                control.HeaderDragZone.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
                control.HeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
            }
        }
    }

    private static void OnIsSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (CanonicalGraphNodeControl)sender;
        if (control.NodeBorder is not null)
            control.NodeBorder.SetResourceReference(Border.BorderBrushProperty,
                control.IsSelected ? "AccentFillColorDefaultBrush" : "TextFillColorSecondaryBrush");
    }

    private void RebuildPorts()
    {
        if (Node is null) return;
        InputPortsPanel.Children.Clear();
        InputPortsPanel.RowDefinitions.Clear();
        OutputPortsPanel.Children.Clear();
        OutputPortsPanel.RowDefinitions.Clear();
        ChoicePortsPanel.Children.Clear();
        ChoicePortsPanel.RowDefinitions.Clear();
        ChoicePortsPanel.ColumnDefinitions.Clear();
        ChoicePortsPanel.Visibility = Visibility.Collapsed;
        OutputPortsPanel.Visibility = Visibility.Visible;
        _portControls.Clear();
        InputPortsPanel.Visibility = Visibility.Visible;
        _choiceOptionRows.Clear();
        if (!TryBuildChoiceRows())
        {
            foreach (var item in Node.Inputs) AddPort(InputPortsPanel, item);
            foreach (var item in Node.Outputs) AddPort(OutputPortsPanel, item);
            return;
        }

        InputPortsPanel.Visibility = Visibility.Collapsed;
        Grid.SetColumn(ChoicePortsPanel, 0);
        Grid.SetColumnSpan(ChoicePortsPanel, 2);
        OutputPortsPanel.Visibility = Visibility.Collapsed;
        ChoicePortsPanel.Visibility = Visibility.Visible;
    }

    private void AddPort(Grid panel, GraphEditorPortViewModel item)
    {
        var port = CreatePort(item);
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(port, panel.RowDefinitions.Count - 1);
        panel.Children.Add(port);
    }

    private bool TryBuildChoiceRows()
    {
        if (Node is null || !string.Equals(Node.Type, "choice", StringComparison.Ordinal)
            || !Node.Properties.TryGetValue(SessionChoiceSchema.OptionsProperty, out var options)
            || options.ValueKind != JsonValueKind.Array)
            return false;

        var outputs = Node.Outputs.ToArray();
        var flowPorts = outputs.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow)
            .GroupBy(port => port.PortId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count() == 1 ? group.Single() : null,
                StringComparer.Ordinal);
        if (outputs.Any(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Logic)) return false;

        var parsed = new List<(string OptionId, string DisplayText, string FlowPortId)>();
        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object
                || !TryReadString(option, "option_id", out var optionId)
                || !TryReadString(option, "display_text", out var displayText)
                || !TryReadString(option, "flow_port_id", out var flowPortId))
                return false;
            if (parsed.Any(item => string.Equals(item.OptionId, optionId, StringComparison.Ordinal)
                || string.Equals(item.FlowPortId, flowPortId, StringComparison.Ordinal)))
                return false;
            if (!flowPorts.TryGetValue(flowPortId, out var flowPort) || flowPort is null)
                return false;
            parsed.Add((optionId, displayText, flowPortId));
        }

        if (parsed.Count == 0 || flowPorts.Count != parsed.Count)
            return false;

        ChoicePortsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        ChoicePortsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        ChoicePortsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        var inputRow = 0;
        void AddChoiceInput(FlowPortControl input)
        {
            input.Height = ChoiceOutputGroupHeight;
            input.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(input, inputRow++);
            ChoicePortsPanel.Children.Add(input);
        }
        foreach (var input in Node.Inputs.Where(p => p.InterfaceKind == GraphInterfaceKind.Flow))
            AddChoiceInput(CreatePort(input));
        foreach (var (optionId, displayText, flowPortId) in parsed)
        {
            // One compact stable-ID row per Flow result.
            var flowPort = CreatePort(flowPorts[flowPortId]!);
            var optionLabel = new TextBlock
            {
                Text = ReadablePortLabel(displayText),
                MaxWidth = ChoiceOptionLabelMaxWidth,
                Margin = new Thickness(0, 0, ChoiceOptionLabelRightInset, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Left,
                FontSize = 11,
                IsHitTestVisible = false,
                ToolTip = ReadablePortLabel(displayText),
            };
            optionLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            flowPort.Width = 20;
            flowPort.MinWidth = 20;
            flowPort.HorizontalAlignment = HorizontalAlignment.Right;
            flowPort.HorizontalContentAlignment = HorizontalAlignment.Right;

            var group = new Grid
            {
                Width = ChoiceOutputGroupWidth,
                Height = ChoiceOutputGroupHeight,
                Margin = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            group.Children.Add(flowPort);
            group.Children.Add(optionLabel);

            Grid.SetColumn(group, 2);
            Grid.SetRow(group, _choiceOptionRows.Count);
            ChoicePortsPanel.Children.Add(group);
            var option = options.EnumerateArray().First(e => e.GetProperty("option_id").GetString() == optionId);
            if (TryReadString(option, "condition_port_id", out var conditionId)
                && Node.Inputs.FirstOrDefault(p => p.PortId == conditionId) is { } condition)
            {
                var input = CreatePort(condition);
                input.DisplayName = ReadablePortLabel(displayText);
                input.ToolTip = input.DisplayName;
                AddChoiceInput(input);
            }
            group.ToolTip = ReadablePortLabel(displayText);
            _choiceOptionRows.Add(new ChoiceOptionRow(
                _choiceOptionRows.Count, optionId, displayText, flowPortId,
                flowPort, group, optionLabel));
        }
        for (var index = 0; index < Math.Max(inputRow, parsed.Count); index++)
            ChoicePortsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ChoicePortRowHeight) });

        return true;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    // The fixed-width node has two equal columns separated by an 8 DIP gutter.
    // Inputs and outputs use independent compact rows with stable edge anchors.
    private const double ChoiceOutputGroupWidth = 104d;
    private const double ChoiceOptionLabelMaxWidth = 78d;
    private const double ChoiceOptionLabelRightInset = 22d;
    private const double ChoiceOutputGroupHeight = 20d;
    private const double ChoicePortRowHeight = 24d;

    private FlowPortControl CreatePort(GraphEditorPortViewModel item)
    {
        var port = new FlowPortControl
        {
            NodeId = Node?.NodeId ?? string.Empty,
            PortId = item.PortId,
            PortName = item.PortId,
            DisplayName = ReadablePortLabel(item.DisplayName),
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

    private static string ReadablePortLabel(string text)
    {
        try { return DynamicContentText.Display(text); }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { return "〔动态内容无效〕"; }
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

/// <summary>One stable-ID-backed visual row for a Session Choice option.</summary>
public sealed class ChoiceOptionRow
{
    internal ChoiceOptionRow(int order, string optionId, string displayText, string flowPortId,
        FlowPortControl flowOutput, Grid outputGroup,
        TextBlock displayLabel)
    {
        Order = order;
        OptionId = optionId;
        DisplayText = displayText;
        FlowPortId = flowPortId;
        FlowOutput = flowOutput;
        OutputGroup = outputGroup;
        DisplayLabel = displayLabel;
    }

    public int Order { get; }
    public string OptionId { get; }
    public string DisplayText { get; }
    public string FlowPortId { get; }
    public FlowPortControl FlowOutput { get; }
    public Grid OutputGroup { get; }
    public TextBlock DisplayLabel { get; }
    public FlowPortControl FlowPort => FlowOutput;
}
