using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class CanonicalActorCreationChoiceDialog : Window
{
    public CanonicalActorCreationChoiceDialog(CanonicalActorCreationChoiceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs args) => DialogResult = true;
}
