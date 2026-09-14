using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>A transient numeric gesture: preview locally and commit the binding exactly once.</summary>
public static class NumericDrag
{
    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached("Step", typeof(double), typeof(NumericDrag),
        new PropertyMetadata(0d, Attach));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached("Minimum", typeof(double), typeof(NumericDrag), new PropertyMetadata((double)int.MinValue));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached("Maximum", typeof(double), typeof(NumericDrag), new PropertyMetadata((double)int.MaxValue));
    public static readonly RoutedEvent CommittedEvent = EventManager.RegisterRoutedEvent(
        "Committed", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NumericDrag));

    public static void SetStep(DependencyObject target, double value) => target.SetValue(StepProperty, value);
    public static double GetStep(DependencyObject target) => (double)target.GetValue(StepProperty);
    public static void SetMinimum(DependencyObject target, double value) => target.SetValue(MinimumProperty, value);
    public static double GetMinimum(DependencyObject target) => (double)target.GetValue(MinimumProperty);
    public static void SetMaximum(DependencyObject target, double value) => target.SetValue(MaximumProperty, value);
    public static double GetMaximum(DependencyObject target) => (double)target.GetValue(MaximumProperty);
    public static void AddCommittedHandler(DependencyObject target, RoutedEventHandler handler)
        => ((UIElement)target).AddHandler(CommittedEvent, handler);
    public static void RemoveCommittedHandler(DependencyObject target, RoutedEventHandler handler)
        => ((UIElement)target).RemoveHandler(CommittedEvent, handler);

    /// <summary>Details of one completed numeric drag gesture.</summary>
    public sealed class CommittedEventArgs(string beforeText, string valueText) : RoutedEventArgs(CommittedEvent)
    {
        public string BeforeText { get; } = beforeText;
        public string ValueText { get; } = valueText;
    }

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(Gesture), typeof(NumericDrag));
    private sealed class Gesture(TextBox box)
    {
        public readonly TextBox Box = box;
        public Point Start;
        public string Before = "";
        public double Number;
        public BindingBase? Binding;
        public Cursor? CursorBefore;
        public bool Pending, Dragging, Finishing;
    }
    private static void Attach(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox box || (double)args.NewValue <= 0 || box.GetValue(StateProperty) is Gesture) return;
        var state = new Gesture(box); box.SetValue(StateProperty, state);
        box.ToolTip ??= "左右拖动调整数值；单击输入；Esc 取消拖动。";
        box.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (box.IsReadOnly || box.IsKeyboardFocusWithin || e.ClickCount != 1
                || !double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return;
            state.Start = e.GetPosition(null); state.Before = box.Text; state.Number = number; state.Pending = true;
            if (!box.CaptureMouse()) { state.Pending = false; return; }
            e.Handled = true;
        };
        box.PreviewMouseMove += (_, e) =>
        {
            if (!state.Pending) return;
            if (e.LeftButton != MouseButtonState.Pressed) { Finish(state, false); return; }
            var delta = e.GetPosition(null).X - state.Start.X;
            if (!state.Dragging && Math.Abs(delta) < SystemParameters.MinimumHorizontalDragDistance) return;
            if (!state.Dragging)
            {
                state.Binding = BindingOperations.GetBindingBase(box, TextBox.TextProperty);
                BindingOperations.ClearBinding(box, TextBox.TextProperty);
                state.Dragging = true; state.CursorBefore = box.Cursor; box.Focus(); box.Cursor = Cursors.SizeWE;
            }
            var value = Math.Clamp(state.Number + Math.Truncate(delta / 4) * GetStep(box), GetMinimum(box), GetMaximum(box));
            box.Text = value.ToString("0.########", CultureInfo.InvariantCulture); e.Handled = true;
        };
        box.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!state.Pending) return;
            var dragged = state.Dragging; Finish(state, true);
            if (!dragged) { box.Focus(); box.SelectAll(); }
            e.Handled = true;
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && state.Pending) { Finish(state, false); e.Handled = true; }
        };
        box.LostMouseCapture += (_, _) => { if (!state.Finishing && state.Pending) Finish(state, false); };
        box.Unloaded += (_, _) => { if (state.Pending) Finish(state, false); };
    }
    private static void Finish(Gesture state, bool commit)
    {
        if (state.Finishing) return;
        state.Finishing = true;
        try
        {
            var box = state.Box; var value = commit ? box.Text : state.Before;
            var dragged = state.Dragging;
            var binding = state.Binding;
            var changed = dragged && commit && !string.Equals(value, state.Before, StringComparison.Ordinal);
            if (dragged)
            {
                if (binding is not null)
                {
                    // Reattach first so the binding engine restores the source value.
                    // Applying the final text once after that produces one source edit;
                    // cancellation leaves the source untouched and restores its text.
                    BindingOperations.SetBinding(box, TextBox.TextProperty, binding);
                    if (commit)
                    {
                        box.SetCurrentValue(TextBox.TextProperty, value);
                        if (binding is not Binding { UpdateSourceTrigger: UpdateSourceTrigger.PropertyChanged })
                            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    }
                }
                else
                {
                    // Unbound fields still need the same cancel semantics; the
                    // routed commit event is the integration point for their
                    // host, so preview text must never become an edit on Esc.
                    box.Text = value;
                }
            }
            state.Pending = state.Dragging = false; state.Binding = null;
            box.ReleaseMouseCapture();
            if (state.CursorBefore is null) box.ClearValue(FrameworkElement.CursorProperty);
            else box.Cursor = state.CursorBefore;
            state.CursorBefore = null;
            if (changed)
                box.RaiseEvent(new CommittedEventArgs(state.Before, value));
            // A drag is a model history action. Do not leave keyboard focus in
            // TextBox's private undo stack; a subsequent click still enters text.
            if (dragged) box.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                // Run after TextBox's mouse-up class handling, which otherwise
                // takes focus back after the preview event has completed.
                DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(box);
                while (parent is not null)
                {
                    if (parent is UIElement { Focusable: true } owner) { Keyboard.Focus(owner); return; }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
                if (Window.GetWindow(box) is { } window)
                {
                    window.SetCurrentValue(UIElement.FocusableProperty, true);
                    Keyboard.Focus(window);
                }
            }));
        }
        finally { state.Finishing = false; }
    }
}
