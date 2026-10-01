using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class ProjectCreationDialog : Window
{
    public ProjectCreationDialog(ProjectCreationDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectCreationDialogViewModel { CanConfirm: true })
        {
            DialogResult = true;
        }
    }

    private void BrowseDestination_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectCreationDialogViewModel viewModel)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择项目保存文件夹",
            Multiselect = false,
            InitialDirectory = FindExistingDirectory(viewModel.DestinationDirectory),
        };
        if (dialog.ShowDialog(this) == true)
        {
            viewModel.FullDestination = dialog.FolderName;
        }
    }

    private static string FindExistingDirectory(string path)
    {
        while (!string.IsNullOrEmpty(path) && !System.IO.Directory.Exists(path))
            path = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        return path;
    }
}
