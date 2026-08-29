using System.Windows;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class CanonicalResourcePickerDialog : Window
{
    public CanonicalResourcePickerDialog(CanonicalResourcePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CanonicalResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }

    private void ResourceList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CanonicalResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }
}
