using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Views;

public sealed class FlowPortControl : Button
{
    private FrameworkElement? _anchor;
    private Grid? _anchorHitTarget;

    public static readonly DependencyProperty NodeIdProperty = DependencyProperty.Register(
        nameof(NodeId), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty PortNameProperty = DependencyProperty.Register(
        nameof(PortName), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty PortIdProperty = DependencyProperty.Register(
        nameof(PortId), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
        nameof(DisplayName), typeof(string), typeof(FlowPortControl), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty InterfaceKindProperty = DependencyProperty.Register(
        nameof(InterfaceKind), typeof(GraphInterfaceKind), typeof(FlowPortControl),
        new PropertyMetadata(GraphInterfaceKind.Flow, OnVisualPropertyChanged));

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
    public string PortId { get => (string)GetValue(PortIdProperty); set => SetValue(PortIdProperty, value); }
    public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public GraphInterfaceKind InterfaceKind { get => (GraphInterfaceKind)GetValue(InterfaceKindProperty); set => SetValue(InterfaceKindProperty, value); }
    public bool IsInput { get => (bool)GetValue(IsInputProperty); set => SetValue(IsInputProperty, value); }
    public bool IsConnecting { get => (bool)GetValue(IsConnectingProperty); set => SetValue(IsConnectingProperty, value); }
    public bool IsValidTarget { get => (bool)GetValue(IsValidTargetProperty); set => SetValue(IsValidTargetProperty, value); }

    /// <summary>Stable connection identity, falling back to the legacy PortName.</summary>
    public string EffectivePortId => string.IsNullOrWhiteSpace(PortId) ? PortName : PortId;

    /// <summary>Author-facing label, falling back to the legacy PortName.</summary>
    public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? PortName : DisplayName;

    /// <summary>
    /// Returns true only when the pointer source is the rendered circle or
    /// diamond. Labels deliberately remain outside the wire hit target.
    /// </summary>
    public bool IsAnchorHitTarget(DependencyObject? source)
        => source is not null && _anchorHitTarget is not null
            && (ReferenceEquals(source, _anchorHitTarget)
                || _anchorHitTarget.IsAncestorOf(source));

    /// <summary>
    /// Checks the inexpensive endpoint invariants needed before a drag can ask
    /// the Story view model to validate a connection. This method deliberately
    /// has no side effects; cardinality, scope, and cycle rules remain owned by
    /// the view model/Core validator.
    /// </summary>
    public bool IsCompatibleEndpoint(FlowPortControl? other) =>
        other is not null
        && !string.IsNullOrWhiteSpace(EffectivePortId)
        && !string.IsNullOrWhiteSpace(other.EffectivePortId)
        && !string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
        && IsInput != other.IsInput
        && InterfaceKind == other.InterfaceKind;

    // Naming aliases keep the compatibility boundary discoverable to callers
    // that use either capability- or predicate-style terminology.
    public bool IsCompatibleWith(FlowPortControl? other) => IsCompatibleEndpoint(other);
    public bool CanConnectTo(FlowPortControl? other) => IsCompatibleEndpoint(other);

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
        _anchor = CreateAnchor(highlighted);
        _anchorHitTarget = CreateAnchorHitTarget(_anchor);

        var panel = new Grid { VerticalAlignment = VerticalAlignment.Center };
        panel.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = IsInput ? new GridLength(20) : new GridLength(1, GridUnitType.Star),
            MinWidth = 0,
        });
        panel.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = IsInput ? new GridLength(1, GridUnitType.Star) : new GridLength(20),
            MinWidth = 0,
        });
        if (IsInput)
        {
            Grid.SetColumn(_anchorHitTarget!, 0);
            panel.Children.Add(_anchorHitTarget!);
            var label = new TextBlock
            {
                Text = EffectiveDisplayName,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            Grid.SetColumn(label, 1);
            panel.Children.Add(label);
        }
        else if (!string.Equals(EffectivePortId, "next", StringComparison.Ordinal))
        {
            var label = new TextBlock
            {
                Text = EffectiveDisplayName,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            Grid.SetColumn(label, 0);
            panel.Children.Add(label);
        }
        // Keep the layout slot fixed at the largest anchor size. Either inner
        // shape can grow for highlighting without changing the port's footprint.
        if (!IsInput)
        {
            Grid.SetColumn(_anchorHitTarget!, 1);
            panel.Children.Add(_anchorHitTarget!);
        }
        Content = panel;
        if (InterfaceKind == GraphInterfaceKind.Logic)
        {
            ToolTip = IsInput ? $"逻辑输入端口：{EffectiveDisplayName}" : $"逻辑输出端口：{EffectiveDisplayName}";
            AutomationProperties.SetName(this, IsInput
                ? $"{NodeId} 逻辑输入端口"
                : $"{NodeId} 逻辑输出端口");
        }
        else
        {
            ToolTip = IsInput ? "输入端口" : $"输出端口：{EffectiveDisplayName}";
            AutomationProperties.SetName(this, IsInput
                ? string.Equals(EffectivePortId, "input", StringComparison.Ordinal)
                    ? $"{NodeId} 输入端口"
                    : $"{NodeId} 入线端口 {EffectivePortId}"
                : $"{NodeId} 输出端口 {EffectivePortId}");
        }
    }

    private FrameworkElement CreateAnchor(bool highlighted)
    {
        var size = highlighted ? 11 : 9;
        if (InterfaceKind == GraphInterfaceKind.Logic)
        {
            var half = size / 2d;
            return new Polygon
            {
                Width = size,
                Height = size,
                Points = new PointCollection
                {
                    new(half, 0),
                    new(size, half),
                    new(half, size),
                    new(0, half),
                },
                Fill = highlighted ? new SolidColorBrush(Color.FromRgb(255, 224, 138)) : new SolidColorBrush(Color.FromRgb(245, 181, 61)),
                Stroke = highlighted ? new SolidColorBrush(Color.FromRgb(190, 125, 15)) : new SolidColorBrush(Color.FromRgb(255, 218, 125)),
                StrokeThickness = highlighted ? 2 : 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = highlighted ? Brushes.White : new SolidColorBrush(Color.FromRgb(108, 177, 255)),
            Stroke = highlighted ? new SolidColorBrush(Color.FromRgb(45, 125, 230)) : new SolidColorBrush(Color.FromRgb(185, 215, 245)),
            StrokeThickness = highlighted ? 2 : 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Grid CreateAnchorHitTarget(FrameworkElement anchor)
    {
        // The visible shape remains 9-11 DIP while this centered transparent
        // surface gives users a forgiving 20-DIP target. The label is a
        // sibling outside this surface and therefore cannot start a wire.
        var hitTarget = new Grid
        {
            Width = 20,
            Height = 20,
            Background = Brushes.Transparent,
            IsHitTestVisible = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        hitTarget.Children.Add(anchor);
        return hitTarget;
    }

    private static ControlTemplate CreateChromeFreeTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
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
