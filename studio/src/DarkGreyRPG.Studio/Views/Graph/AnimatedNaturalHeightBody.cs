using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Keep the top fixed while content changes grow or shrink the lower edge.</summary>
public sealed class AnimatedNaturalHeightBody : Decorator
{
    private static readonly DependencyProperty DisplayHeightProperty = DependencyProperty.Register(
        "DisplayHeight", typeof(double), typeof(AnimatedNaturalHeightBody),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    private double _target = double.NaN;
    private double DisplayHeight => (double)GetValue(DisplayHeightProperty);

    public AnimatedNaturalHeightBody()
    {
        ClipToBounds = true;
        Unloaded += (_, _) =>
        {
            BeginAnimation(DisplayHeightProperty, null);
            _target = double.NaN;
            InvalidateMeasure();
        };
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return default;
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        var next = Child.DesiredSize.Height;
        if (double.IsNaN(_target) || Math.Abs(next - _target) > .01)
        {
            var from = DisplayHeight;
            var animate = !double.IsNaN(_target) && IsLoaded && SystemParameters.ClientAreaAnimation;
            _target = next;
            BeginAnimation(DisplayHeightProperty, null);
            SetValue(DisplayHeightProperty, next);
            if (animate)
                BeginAnimation(DisplayHeightProperty, new DoubleAnimation(from, next, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    FillBehavior = FillBehavior.Stop
                }, HandoffBehavior.SnapshotAndReplace);
        }
        return new Size(Child.DesiredSize.Width, Math.Max(0, DisplayHeight));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
