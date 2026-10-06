using System.Windows;
using System.Windows.Controls;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Animates layout height, so a bottom-docked header moves with its content.</summary>
public sealed class AnimatedPackageExpander : HeaderedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(AnimatedPackageExpander),
        new PropertyMetadata(false));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
}
