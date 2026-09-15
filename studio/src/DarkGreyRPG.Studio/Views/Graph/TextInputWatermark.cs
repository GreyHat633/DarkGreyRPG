using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// Displays helper text over an empty, unfocused TextBox. The TextBox.Text
/// property remains the sole source of user data; this behavior only owns an
/// adorner and never writes a placeholder into the control.
/// </summary>
public static class TextInputWatermark
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TextInputWatermark),
        new PropertyMetadata(string.Empty, TextChanged));
    // Watermark is an intentionally equivalent spelling for XAML call sites.
    // Both names point at the same attached property and therefore share one
    // lifecycle and one value.
    public static readonly DependencyProperty WatermarkProperty = TextProperty;

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(TextInputWatermark));

    public static void SetText(DependencyObject target, string? value) => target.SetValue(TextProperty, value ?? string.Empty);
    public static string GetText(DependencyObject target) => (string)target.GetValue(TextProperty);
    public static void SetWatermark(DependencyObject target, string? value) => SetText(target, value);
    public static string GetWatermark(DependencyObject target) => GetText(target);

    private sealed class State(TextBox box)
    {
        public readonly TextBox Box = box;
        public WatermarkAdorner? Adorner;
        public bool Attached;
    }

    private static void TextChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox box) return;
        if (box.GetValue(StateProperty) is not State state)
        {
            if (string.IsNullOrEmpty((string?)args.NewValue)) return;
            state = new State(box);
            box.SetValue(StateProperty, state);
            box.Loaded += OnLoaded;
            box.Unloaded += OnUnloaded;
            box.TextChanged += OnRoutedStateChanged;
            box.GotKeyboardFocus += OnRoutedStateChanged;
            box.LostKeyboardFocus += OnRoutedStateChanged;
            box.IsVisibleChanged += OnEnabledChanged;
            box.IsEnabledChanged += OnEnabledChanged;
        }

        if (string.IsNullOrEmpty((string?)args.NewValue))
        {
            Dispose(state);
            box.ClearValue(StateProperty);
        }
        else
        {
            Attach(state);
            Refresh(state);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBox box && box.GetValue(StateProperty) is State state)
        {
            Attach(state);
            Refresh(state);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBox box && box.GetValue(StateProperty) is State state) Detach(state);
    }

    private static void OnRoutedStateChanged(object? sender, RoutedEventArgs args)
    {
        if (sender is TextBox box && box.GetValue(StateProperty) is State state) Refresh(state);
    }

    private static void OnEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is TextBox box && box.GetValue(StateProperty) is State state) Refresh(state);
    }

    private static void Dispose(State state)
    {
        var box = state.Box;
        Detach(state);
        box.Loaded -= OnLoaded;
        box.Unloaded -= OnUnloaded;
        box.TextChanged -= OnRoutedStateChanged;
        box.GotKeyboardFocus -= OnRoutedStateChanged;
        box.LostKeyboardFocus -= OnRoutedStateChanged;
        box.IsVisibleChanged -= OnEnabledChanged;
        box.IsEnabledChanged -= OnEnabledChanged;
    }

    private static void Attach(State state)
    {
        if (state.Attached) return;
        var layer = AdornerLayer.GetAdornerLayer(state.Box);
        if (layer is null) return;
        state.Adorner = new WatermarkAdorner(state.Box);
        layer.Add(state.Adorner);
        state.Attached = true;
    }

    private static void Detach(State state)
    {
        if (!state.Attached || state.Adorner is null) return;
        var layer = AdornerLayer.GetAdornerLayer(state.Box);
        layer?.Remove(state.Adorner);
        state.Adorner = null;
        state.Attached = false;
    }

    private static void Refresh(State state)
    {
        if (state.Adorner is null) return;
        var box = state.Box;
        state.Adorner.SetText(GetText(box));
        state.Adorner.Visibility = box.IsEnabled && box.IsVisible
            && !box.IsKeyboardFocusWithin && string.IsNullOrEmpty(box.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        state.Adorner.InvalidateMeasure();
        state.Adorner.InvalidateVisual();
    }

    private sealed class WatermarkAdorner : Adorner
    {
        private readonly TextBlock _label;

        public WatermarkAdorner(TextBox box) : base(box)
        {
            IsHitTestVisible = false;
            IsClipEnabled = true;
            ClipToBounds = true;
            _label = new TextBlock
            {
                Text = GetText(box),
                FontFamily = box.FontFamily,
                FontSize = box.FontSize,
                FontStretch = box.FontStretch,
                FontStyle = box.FontStyle,
                FontWeight = box.FontWeight,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(box.Padding.Left + 2, box.Padding.Top, box.Padding.Right + 2, box.Padding.Bottom)
            };
            AuthoringText.BindPlaceholder(_label, box);
            AddVisualChild(_label);
            AddLogicalChild(_label);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => index == 0 ? _label : throw new ArgumentOutOfRangeException(nameof(index));
        protected override Size MeasureOverride(Size constraint)
        {
            var size = AdornedElement.RenderSize;
            _label.Measure(size);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = AdornedElement.RenderSize;
            _label.Arrange(new Rect(new Point(0, 0), size));
            Clip = new RectangleGeometry(new Rect(size));
            return size;
        }

        public void SetText(string text)
        {
            if (_label.Text == text) return;
            _label.Text = text;
            InvalidateMeasure();
        }
    }
}
