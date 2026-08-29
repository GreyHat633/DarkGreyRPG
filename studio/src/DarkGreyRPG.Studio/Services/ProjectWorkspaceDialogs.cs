using System.IO;
using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
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

    public bool ConfirmDeleteStory(
        string storyId,
        string displayName,
        IReadOnlyList<string> resourcesToDelete)
        => ConfirmDeleteStoryCore(
            storyId,
            displayName,
            $"stories/{storyId}.json",
            resourcesToDelete);

    public bool ConfirmDeleteCanonicalStory(
        string storyId,
        string displayName,
        IReadOnlyList<string> resourcesToDelete)
        => ConfirmDeleteStoryCore(
            storyId,
            displayName,
            $"resources/canonical/stories/{storyId}.json 与 resources/canonical/memberships/{storyId}.json",
            resourcesToDelete);

    public bool ConfirmCanonicalProjectMigration(CanonicalProjectMigrationPreviewResult preview)
    {
        var viewModel = new CanonicalProjectMigrationDialogViewModel(preview);
        var dialog = new CanonicalProjectMigrationDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true && viewModel.CanApply;
    }

    private bool ConfirmDeleteStoryCore(
        string storyId,
        string displayName,
        string storyPaths,
        IReadOnlyList<string> resourcesToDelete)
    {
        var resourceWarning = resourcesToDelete.Count == 0
            ? string.Empty
            : $"\n\n还将永久删除以下归属资源：\n- {string.Join("\n- ", resourcesToDelete)}";
        return MessageBox.Show(
            ownerProvider(),
            $"确定要永久删除剧情“{displayName}”（{storyId}）吗？\n\n将删除 {storyPaths}{resourceWarning}\n\n此操作无法撤销。",
            "确认删除剧情",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
