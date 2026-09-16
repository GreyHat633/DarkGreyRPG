using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class ReorderEntriesEditor : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ReorderEntriesEditor));
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public ReorderEntriesEditor() => InitializeComponent();
}
