using System.Windows;
using System.Windows.Controls;

namespace DarkGreyRPG.Studio.Views;

/// <summary>A selectable directory leaf that retains ordinary button and drag/drop behavior.</summary>
public sealed class DirectoryTreeRowButton : Button
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(DirectoryTreeRowButton), new PropertyMetadata(false));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
}
