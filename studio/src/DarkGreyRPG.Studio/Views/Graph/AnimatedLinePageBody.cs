using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Measure content at its natural height, then reveal it without scaling its text.</summary>
public sealed class AnimatedLinePageBody : Decorator
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(AnimatedLinePageBody),
        new PropertyMetadata(true, (d, _) => ((AnimatedLinePageBody)d).UpdateExpansion()));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(AnimatedLinePageBody),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, _) => ((AnimatedLinePageBody)d).UpdateVisibility()));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    private double Progress => (double)GetValue(ProgressProperty);
    public AnimatedLinePageBody()
    {
        ClipToBounds = true;
        Loaded += (_, _) => Snap();
        Unloaded += (_, _) => Snap();
    }
    private void Snap()
    {
        BeginAnimation(ProgressProperty, null);
        SetValue(ProgressProperty, IsExpanded ? 1d : 0d);
        UpdateVisibility();
    }
    private void UpdateExpansion()
    {
        if (!IsLoaded || !SystemParameters.ClientAreaAnimation) { Snap(); return; }
        var from = Progress;
        var target = IsExpanded ? 1d : 0d;
        if (Child is not null && IsExpanded) Child.Visibility = Visibility.Visible;
        IsHitTestVisible = IsExpanded;
        var animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop };
        SetValue(ProgressProperty, target);
        BeginAnimation(ProgressProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private void UpdateVisibility()
    {
        IsHitTestVisible = IsExpanded;
        if (Child is not null) Child.Visibility = Progress <= 0 ? Visibility.Hidden : Visibility.Visible;
    }
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return default;
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        return new Size(Child.DesiredSize.Width, Child.DesiredSize.Height * Math.Clamp(Progress, 0, 1));
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
