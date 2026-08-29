using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class StoryFlowNodeControl : UserControl
{
    public static readonly DependencyProperty IsAdvancedExpandedProperty = DependencyProperty.Register(
        nameof(IsAdvancedExpanded), typeof(bool), typeof(StoryFlowNodeControl), new PropertyMetadata(false));

    public static readonly DependencyProperty ResourceCandidatesProperty = DependencyProperty.Register(
        nameof(ResourceCandidates), typeof(IReadOnlyList<FlowResourceCandidate>), typeof(StoryFlowNodeControl),
        new PropertyMetadata(Array.Empty<FlowResourceCandidate>(), OnResourceCandidatesChanged));

    public static readonly DependencyProperty ResourceLabelProperty = DependencyProperty.Register(
        nameof(ResourceLabel), typeof(string), typeof(StoryFlowNodeControl), new PropertyMetadata("资源"));

    public static readonly DependencyProperty ResourceCandidatesViewProperty = DependencyProperty.Register(
        nameof(ResourceCandidatesView), typeof(ICollectionView), typeof(StoryFlowNodeControl), new PropertyMetadata(null));

    public static readonly DependencyProperty CanAddSelectedReferenceProperty = DependencyProperty.Register(
        nameof(CanAddSelectedReference), typeof(bool), typeof(StoryFlowNodeControl), new PropertyMetadata(false));

    public static readonly DependencyProperty IncomingConnectionsProperty = DependencyProperty.Register(
        nameof(IncomingConnections), typeof(IReadOnlyList<StoryFlowConnectionEditorItem>), typeof(StoryFlowNodeControl),
        new PropertyMetadata(Array.Empty<StoryFlowConnectionEditorItem>(), OnIncomingConnectionsChanged));

    private static readonly DependencyPropertyKey HasMultipleIncomingConnectionsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasMultipleIncomingConnections), typeof(bool), typeof(StoryFlowNodeControl), new PropertyMetadata(false));

    public static readonly DependencyProperty HasMultipleIncomingConnectionsProperty = HasMultipleIncomingConnectionsPropertyKey.DependencyProperty;

    public StoryFlowNodeControl(StoryFlowNodeEditorItem node)
    {
        InitializeComponent();
        Node = node ?? throw new ArgumentNullException(nameof(node));
        DataContext = Node;
        Width = StoryFlowNodeEditorItem.Width;
        Tag = Node;
        AutomationProperties.SetName(this, $"Flow 节点 {node.TypeLabel} {node.Id}");
    }

    public StoryFlowNodeEditorItem Node { get; }
    public bool IsAdvancedExpanded { get => (bool)GetValue(IsAdvancedExpandedProperty); set => SetValue(IsAdvancedExpandedProperty, value); }
    public IReadOnlyList<FlowResourceCandidate> ResourceCandidates { get => (IReadOnlyList<FlowResourceCandidate>)GetValue(ResourceCandidatesProperty); set => SetValue(ResourceCandidatesProperty, value); }
    public ICollectionView? ResourceCandidatesView { get => (ICollectionView?)GetValue(ResourceCandidatesViewProperty); private set => SetValue(ResourceCandidatesViewProperty, value); }
    public bool CanAddSelectedReference { get => (bool)GetValue(CanAddSelectedReferenceProperty); private set => SetValue(CanAddSelectedReferenceProperty, value); }
    public string ResourceLabel { get => (string)GetValue(ResourceLabelProperty); set => SetValue(ResourceLabelProperty, value); }
    public IReadOnlyList<StoryFlowConnectionEditorItem> IncomingConnections { get => (IReadOnlyList<StoryFlowConnectionEditorItem>)GetValue(IncomingConnectionsProperty); set => SetValue(IncomingConnectionsProperty, value); }
    public bool HasMultipleIncomingConnections => (bool)GetValue(HasMultipleIncomingConnectionsProperty);
    public FlowPortControl Input => InputPort;

    public event EventHandler<FlowPortInvokedEventArgs>? InputInvoked;
    public event EventHandler<FlowPortInvokedEventArgs>? InputDragStarted;
    public event EventHandler<FlowPortInvokedEventArgs>? OutputInvoked;
    public event EventHandler<FlowPortInvokedEventArgs>? OutputDragStarted;
    public event EventHandler<IncomingConnectionDragEventArgs>? IncomingConnectionDragStarted;
    public event EventHandler? AddReferenceRequested;
    public event EventHandler? ExpandedChanged;
    public event EventHandler<DialogueExitsChangeRequestedEventArgs>? DialogueExitsChangeRequested;
    public event EventHandler? SequenceStepAddRequested;
    public event EventHandler? SequenceStepRemoveRequested;

    public FlowPortControl? FindOutput(string output) =>
        FindVisualChildren<FlowPortControl>(OutputItems).FirstOrDefault(port => string.Equals(port.EffectivePortId, output, StringComparison.Ordinal));

    public IReadOnlyList<FlowPortControl> OutputPorts => [.. FindVisualChildren<FlowPortControl>(OutputItems)];

    public void FocusField(string? field)
    {
        IsAdvancedExpanded = true;
        UpdateLayout();

        FrameworkElement? target = field switch
        {
            "actor_id" or "dialogue_id" or "quest_id" or "target_story_id" => ResourceSelector,
            "exit_names" or "exits" or "outputs" => FindVisualChildren<TextBox>(this)
                .FirstOrDefault(control => AutomationProperties.GetName(control) == "节点内 Dialogue Exit 输出"),
            "input" => InputPort,
            "output" => OutputPorts.FirstOrDefault(),
            "Id" or "id" => ExpandAdvancedAndReturnIdEditor(),
            _ => FindPropertyEditor(field) ?? (string.IsNullOrWhiteSpace(field) ? null : FindOutput(field)),
        };
        target ??= this;
        target.BringIntoView();
        FocusTarget(target);
        target.Dispatcher.BeginInvoke(() => FocusTarget(target), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static void FocusTarget(FrameworkElement target)
    {
        target.Focus();
        Keyboard.Focus(target);
        if (target is TextBox textBox) textBox.SelectAll();
    }

    private FrameworkElement? FindPropertyEditor(string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        var target = FindVisualChildren<TextBox>(this)
            .FirstOrDefault(control => control.DataContext is StoryFlowPropertyEditorItem editor &&
                                       string.Equals(editor.Key, field, StringComparison.Ordinal));
        if (target is not null) return target;
        AdvancedPropertiesExpander.IsExpanded = true;
        UpdateLayout();
        return FindVisualChildren<TextBox>(this)
            .FirstOrDefault(control => control.DataContext is StoryFlowPropertyEditorItem editor &&
                                       string.Equals(editor.Key, field, StringComparison.Ordinal));
    }

    private FrameworkElement ExpandAdvancedAndReturnIdEditor()
    {
        AdvancedPropertiesExpander.IsExpanded = true;
        UpdateLayout();
        return (FrameworkElement?)FindVisualChildren<TextBox>(AdvancedPropertiesExpander)
            .FirstOrDefault(control => AutomationProperties.GetName(control) == "Flow 节点高级 ID") ?? this;
    }

    public FlowPortControl FindInput(StoryFlowConnectionEditorItem connection)
    {
        if (!HasMultipleIncomingConnections) return InputPort;
        return FindVisualChildren<FlowPortControl>(IncomingHandleItems)
            .FirstOrDefault(port => Equals(port.Tag, connection)) ?? InputPort;
    }

    private void InputPort_OnClick(object sender, RoutedEventArgs e)
    {
        InputInvoked?.Invoke(this, new FlowPortInvokedEventArgs(Node.Id, InputPort.EffectivePortId, InputPort));
        e.Handled = true;
    }

    private void InputPort_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        InputDragStarted?.Invoke(this, new FlowPortInvokedEventArgs(Node.Id, InputPort.EffectivePortId, InputPort));
        e.Handled = true;
    }

    private void OutputPort_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FlowPortControl port) return;
        OutputInvoked?.Invoke(this, new FlowPortInvokedEventArgs(Node.Id, port.EffectivePortId, port));
        e.Handled = true;
    }

    private void OutputPort_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FlowPortControl port) return;
        OutputDragStarted?.Invoke(this, new FlowPortInvokedEventArgs(Node.Id, port.EffectivePortId, port));
        e.Handled = true;
    }

    private void IncomingHandle_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FlowPortControl { Tag: StoryFlowConnectionEditorItem connection } port) return;
        IncomingConnectionDragStarted?.Invoke(this, new IncomingConnectionDragEventArgs(connection, port));
        e.Handled = true;
    }

    private void AdvancedToggle_OnChanged(object sender, RoutedEventArgs e) => ExpandedChanged?.Invoke(this, EventArgs.Empty);

    private void ExitNames_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && !string.Equals(textBox.Text, Node.ExitNames, StringComparison.Ordinal))
            DialogueExitsChangeRequested?.Invoke(this, new DialogueExitsChangeRequestedEventArgs(textBox.Text));
    }

    private void AddSequenceStep_OnClick(object sender, RoutedEventArgs e)
    {
        SequenceStepAddRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RemoveSequenceStep_OnClick(object sender, RoutedEventArgs e)
    {
        SequenceStepRemoveRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static void OnResourceCandidatesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (StoryFlowNodeControl)dependencyObject;
        var view = new ListCollectionView(control.ResourceCandidates.ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FlowResourceCandidate.Group)));
        control.ResourceCandidatesView = view;
        control.UpdateReferenceAction();
    }

    private static void OnIncomingConnectionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (StoryFlowNodeControl)dependencyObject;
        control.SetValue(HasMultipleIncomingConnectionsPropertyKey, control.IncomingConnections.Count > 1);
    }

    private void ResourceSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateReferenceAction();

    private void UpdateReferenceAction() =>
        CanAddSelectedReference = ResourceSelector?.SelectedItem is FlowResourceCandidate { IsInStoryMembership: false };

    private void AddReference_OnClick(object sender, RoutedEventArgs e)
    {
        AddReferenceRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }
}

public sealed record FlowPortInvokedEventArgs(string NodeId, string PortName, FlowPortControl Port);
public sealed record IncomingConnectionDragEventArgs(StoryFlowConnectionEditorItem Connection, FlowPortControl InputPort);
public sealed record DialogueExitsChangeRequestedEventArgs(string RequestedNames);
