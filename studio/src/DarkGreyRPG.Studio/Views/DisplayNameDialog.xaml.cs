using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class DisplayNameDialog : Window
{
    public DisplayNameDialog(DisplayNameDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayNameDialogViewModel { CanConfirm: true }) DialogResult = true;
    }
}
