using System.Windows;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class ActorResourcePickerDialog : Window
{
    public ActorResourcePickerDialog(ActorResourcePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActorResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }

    private void ActorList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ActorResourcePickerViewModel { CanConfirm: true }) DialogResult = true;
    }
}
