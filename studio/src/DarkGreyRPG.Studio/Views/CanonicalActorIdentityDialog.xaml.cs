using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class CanonicalActorIdentityDialog : Window
{
    public CanonicalActorIdentityDialog(CanonicalActorIdentityDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is CanonicalActorIdentityDialogViewModel { CanConfirm: true }) DialogResult = true;
    }
}
