using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class CanonicalItemCreationChoiceDialog : Window
{
    public CanonicalItemCreationChoiceDialog(CanonicalItemCreationChoiceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
