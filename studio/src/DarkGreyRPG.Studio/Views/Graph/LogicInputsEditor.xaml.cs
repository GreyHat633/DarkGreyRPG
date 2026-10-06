using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class LogicInputsEditor : UserControl
{
    private Window? _inputWindow;
    public LogicInputsEditor()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DetachWindow();
            _inputWindow = Window.GetWindow(this);
            _inputWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown), true);
            _inputWindow?.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Window_OnFocus), true);
            if (_inputWindow is not null) _inputWindow.Deactivated += Window_OnDeactivated;
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is CanonicalNodeInspectorViewModel owner) owner.ClearLogicInputSelection();
            DetachWindow();
        };
        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is CanonicalNodeInspectorViewModel old && !ReferenceEquals(args.OldValue, args.NewValue))
                old.ClearLogicInputSelection();
        };
    }
    private void DetachWindow()
    {
        if (_inputWindow is null) return;
        _inputWindow.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown));
        _inputWindow.RemoveHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Window_OnFocus));
        _inputWindow.Deactivated -= Window_OnDeactivated;
        _inputWindow = null;
    }
    private void Window_OnDeactivated(object? sender, EventArgs args)
    {
        if (DataContext is CanonicalNodeInspectorViewModel owner) owner.ClearLogicInputSelection();
    }
    private bool OwnsInput(DependencyObject? source)
    {
        var editor = LinePagesEditor.Ancestor<LogicInputsEditor>(source);
        return editor?.DataContext is CanonicalNodeInspectorViewModel target
            && DataContext is CanonicalNodeInspectorViewModel owner
            && ReferenceEquals(owner.Host, target.Host) && owner.NodeId == target.NodeId;
    }
    private static Border? Card(DependencyObject? source)
    {
        for (var current = source; current is not null; current = LinePagesEditor.InputParent(current))
            if (current is Border { Name: "LogicInputCard" } card) return card;
        return null;
    }
    private static bool IsAction(DependencyObject? source) =>
        LinePagesEditor.Ancestor<Button>(source) is { } button
        && System.Windows.Automation.AutomationProperties.GetAutomationId(button)
            is "AddLogicInput" or "RemoveSelectedLogicInput";
    private void Window_OnMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel owner) return;
        var source = args.OriginalSource as DependencyObject;
        if (!OwnsInput(source)) { owner.ClearLogicInputSelection(); return; }
        if (Card(source)?.DataContext is CanonicalNodeInspectorViewModel.LogicInputRowViewModel row)
        {
            owner.SelectLogicInput(row.PortId);
            // Card padding selects the input; it must not start a canvas-node drag.
            if (source is Border { Name: "LogicInputCard" } || LinePagesEditor.Ancestor<Control>(source) is ItemsControl)
                args.Handled = true;
        }
        else if (!IsAction(source)) owner.ClearLogicInputSelection();
    }
    private void Window_OnFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel owner) return;
        var source = args.NewFocus as DependencyObject;
        if (!OwnsInput(source)) { owner.ClearLogicInputSelection(); return; }
        if (Card(source)?.DataContext is CanonicalNodeInspectorViewModel.LogicInputRowViewModel row)
            owner.SelectLogicInput(row.PortId);
        else if (!IsAction(source)) owner.ClearLogicInputSelection();
    }
    private void Add_OnClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel owner) return;
        // Button executes its command after Click; focus the new row after projection.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!IsLoaded || !ReferenceEquals(DataContext, owner)) return;
            var field = LinePagesEditor.Children<TextBox>(Inputs).FirstOrDefault(input =>
                input.DataContext is CanonicalNodeInspectorViewModel.LogicInputRowViewModel { IsSelected: true });
            field?.BringIntoView();
            field?.Focus();
            field?.SelectAll();
        }));
    }
    private static void CommitName(TextBox input)
    {
        var binding = input.GetBindingExpression(TextBox.TextProperty);
        binding?.UpdateSource();
        binding?.UpdateTarget();
    }
    private void Name_OnLostFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (sender is TextBox input) CommitName(input);
    }
    private void Name_OnKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is not TextBox input) return;
        if (args.Key == Key.Enter)
        {
            CommitName(input); args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            args.Handled = true;
        }
    }
}
