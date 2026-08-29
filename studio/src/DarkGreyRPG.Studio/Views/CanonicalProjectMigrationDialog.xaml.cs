using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class CanonicalProjectMigrationDialog : Window
{
    public CanonicalProjectMigrationDialog(CanonicalProjectMigrationDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CanonicalProjectMigrationDialogViewModel { CanApply: true })
            DialogResult = true;
    }
}
