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

    private void SourceTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && DataContext is OfflineResourcePickerViewModel viewModel)
            viewModel.Select(null);
    }

    private void ResourceList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not OfflineResourcePickerViewModel viewModel) return;
        if (e.AddedItems.OfType<OfflineResourceChoice>().FirstOrDefault() is { } choice)
            viewModel.Select(choice);
    }

    private void ResourceList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OfflineResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is OfflineResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }
}
