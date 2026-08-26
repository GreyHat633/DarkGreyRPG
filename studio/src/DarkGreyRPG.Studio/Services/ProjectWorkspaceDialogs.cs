using System.IO;
using System.Windows;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class ProjectWorkspaceDialogs(Func<Window?> ownerProvider) : IProjectWorkspaceDialogs
{
    public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null)
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(initialParentDirectory);
        var dialog = new ProjectCreationDialog(viewModel) { Owner = ownerProvider() };
        if (dialog.ShowDialog() != true || !viewModel.CanConfirm)
        {
            return null;
        }

        return new ProjectCreationRequest(
            Path.GetFullPath(viewModel.DestinationDirectory),
            viewModel.Id,
            viewModel.DisplayName.Trim());
    }
}
