using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class ActorIdentityDialog : Window
{
    public ActorIdentityDialog(ActorIdentityDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActorIdentityDialogViewModel { CanConfirm: true })
        {
            DialogResult = true;
        }
    }
}

