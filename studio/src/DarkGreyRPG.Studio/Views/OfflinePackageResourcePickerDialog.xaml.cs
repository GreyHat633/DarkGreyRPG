using DarkGreyRPG.Studio.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class OfflinePackageResourcePickerDialog : Window
{
    public OfflinePackageResourcePickerDialog(OfflineResourcePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private bool _selecting;
    private void ResourceList_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox list && DataContext is OfflineResourcePickerViewModel { SelectedChoice: { } choice }
            && list.Items.Contains(choice)) list.SelectedItem = choice;
    }
    private void ResourceList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selecting || DataContext is not OfflineResourcePickerViewModel viewModel) return;
        if (e.AddedItems.OfType<OfflineResourceChoice>().FirstOrDefault() is not { } choice) return;
        _selecting = true;
        try
        {
            foreach (var list in Lists(this).Where(list => !ReferenceEquals(list, sender))) list.SelectedItem = null;
            viewModel.Select(choice);
        }
        finally { _selecting = false; }
    }
    private static IEnumerable<ListBox> Lists(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is ListBox list) yield return list;
            foreach (var nested in Lists(child)) yield return nested;
        }
    }

    private void ResourceList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(list, source) is ListBoxItem { DataContext: OfflineResourceChoice choice }
            && DataContext is OfflineResourcePickerViewModel viewModel)
        {
            viewModel.Select(choice);
            DialogResult = true;
        }
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is OfflineResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }
}
