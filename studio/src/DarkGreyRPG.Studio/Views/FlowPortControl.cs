using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DarkGreyRPG.Studio.Views;

public sealed class FlowPortControl : Button
{
    private Ellipse? _anchor;

    public static readonly DependencyProperty NodeIdProperty = DependencyProperty.Register(
        nameof(NodeId), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty PortNameProperty = DependencyProperty.Register(
        nameof(PortName), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsInputProperty = DependencyProperty.Register(
        nameof(IsInput), typeof(bool), typeof(FlowPortControl), new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsConnectingProperty = DependencyProperty.Register(
        nameof(IsConnecting), typeof(bool), typeof(FlowPortControl), new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsValidTargetProperty = DependencyProperty.Register(
        nameof(IsValidTarget), typeof(bool), typeof(FlowPortControl), new PropertyMetadata(false, OnVisualPropertyChanged));

    public FlowPortControl()
    {
        MinWidth = 20;
        Height = 20;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        BorderBrush = Brushes.Transparent;
        Background = Brushes.Transparent;
        Focusable = false;
        FocusVisualStyle = null;
        OverridesDefaultStyle = true;
        Template = CreateChromeFreeTemplate();
        Cursor = Cursors.Cross;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;
        Loaded += (_, _) => RebuildVisual();
    }

    public string NodeId { get => (string)GetValue(NodeIdProperty); set => SetValue(NodeIdProperty, value); }
    public string PortName { get => (string)GetValue(PortNameProperty); set => SetValue(PortNameProperty, value); }
    public bool IsInput { get => (bool)GetValue(IsInputProperty); set => SetValue(IsInputProperty, value); }
    public bool IsConnecting { get => (bool)GetValue(IsConnectingProperty); set => SetValue(IsConnectingProperty, value); }
    public bool IsValidTarget { get => (bool)GetValue(IsValidTargetProperty); set => SetValue(IsValidTargetProperty, value); }

    public Point GetAnchorPoint(UIElement relativeTo)
    {
        if (_anchor is null || _anchor.ActualWidth <= 0 || _anchor.ActualHeight <= 0)
            return TranslatePoint(new Point(ActualWidth / 2, ActualHeight / 2), relativeTo);
        return _anchor.TranslatePoint(new Point(_anchor.ActualWidth / 2, _anchor.ActualHeight / 2), relativeTo);
    }

    private static void OnVisualPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((FlowPortControl)dependencyObject).RebuildVisual();

    private void RebuildVisual()
    {
        var highlighted = IsValidTarget || IsConnecting;
        _anchor = new Ellipse
        {
            Width = highlighted ? 11 : 9,
            Height = highlighted ? 11 : 9,
            Fill = highlighted ? Brushes.White : new SolidColorBrush(Color.FromRgb(108, 177, 255)),
            Stroke = highlighted ? new SolidColorBrush(Color.FromRgb(45, 125, 230)) : new SolidColorBrush(Color.FromRgb(185, 215, 245)),
            StrokeThickness = highlighted ? 2 : 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!IsInput && !string.Equals(PortName, "next", StringComparison.Ordinal))
        {
            panel.Children.Add(new TextBlock
            {
                Text = PortName,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 11,
            });
        }
        // Keep the layout slot fixed at the largest anchor size. The ellipse can
        // grow for highlighting without changing the port's desired width.
        var anchorSlot = new Grid
        {
            Width = 11,
            Height = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        anchorSlot.Children.Add(_anchor);
        panel.Children.Add(anchorSlot);
        Content = panel;
        ToolTip = IsInput ? "输入端口" : $"输出端口：{PortName}";
        AutomationProperties.SetName(this, IsInput
            ? string.Equals(PortName, "input", StringComparison.Ordinal)
                ? $"{NodeId} 输入端口"
                : $"{NodeId} 入线端口 {PortName}"
            : $"{NodeId} 输出端口 {PortName}");
    }

    private static ControlTemplate CreateChromeFreeTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
        root.SetValue(FrameworkElement.MinWidthProperty, 20d);
        root.SetValue(FrameworkElement.HeightProperty, 20d);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        presenter.SetBinding(
            FrameworkElement.HorizontalAlignmentProperty,
            new System.Windows.Data.Binding(nameof(HorizontalContentAlignment))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });
        presenter.SetBinding(
            FrameworkElement.VerticalAlignmentProperty,
            new System.Windows.Data.Binding(nameof(VerticalContentAlignment))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });
        root.AppendChild(presenter);

        return new ControlTemplate(typeof(FlowPortControl)) { VisualTree = root };
    }
}
