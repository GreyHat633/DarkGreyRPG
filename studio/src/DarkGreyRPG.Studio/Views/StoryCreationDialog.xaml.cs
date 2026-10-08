using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class StoryCreationDialog : Window
{
    public StoryCreationDialog(StoryCreationViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is StoryCreationViewModel { CanConfirm: true }) DialogResult = true;
    }
}
