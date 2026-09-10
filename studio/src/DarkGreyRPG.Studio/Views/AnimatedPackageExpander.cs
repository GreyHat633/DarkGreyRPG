using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Animates layout height, so a bottom-docked header moves with its content.</summary>
public sealed class AnimatedPackageExpander : HeaderedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(AnimatedPackageExpander),
        new PropertyMetadata(false, (d, _) => ((AnimatedPackageExpander)d).RefreshHeight()));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    private FrameworkElement? _reveal;
    private ContentPresenter? _content;
    private double _target = -1;

    public AnimatedPackageExpander() => LayoutUpdated += (_, _) => RefreshHeight();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _reveal = GetTemplateChild("PART_Reveal") as FrameworkElement;
        _content = GetTemplateChild("PART_Content") as ContentPresenter;
        _target = -1;
        RefreshHeight();
    }

    private void RefreshHeight()
    {
        if (_reveal is null || _content is null || ActualWidth <= 0) return;
        _content.Measure(new Size(ActualWidth, double.PositiveInfinity));
        var target = IsExpanded ? Math.Min(230, _content.DesiredSize.Height) : 0;
        if (Math.Abs(target - _target) < .5) return;
        _target = target;
        _reveal.BeginAnimation(HeightProperty, new DoubleAnimation(_reveal.ActualHeight, target,
            TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
    }
}
