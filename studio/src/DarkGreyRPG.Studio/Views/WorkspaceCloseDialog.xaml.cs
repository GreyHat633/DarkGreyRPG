using System.Windows;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Views;

public partial class WorkspaceCloseDialog : Window
{
    public WorkspaceCloseDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesChoice Choice { get; private set; } = UnsavedChangesChoice.Cancel;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Save;
        DialogResult = true;
    }

    private void Discard_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Discard;
        DialogResult = false;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Cancel;
        DialogResult = false;
    }
}
