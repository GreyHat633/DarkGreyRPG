using System.Windows;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class ResourceWorkspaceDialogs(Func<Window?> ownerProvider) : IResourceWorkspaceDialogs
{
    public StoryCreationRequest? RequestCreateStory(string allocatedStoryUid)
    {
        var viewModel = new StoryCreationViewModel(allocatedStoryUid);
        var dialog = new StoryCreationDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? new(viewModel.DisplayName.Trim()) : null;
    }

    public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates,
        ResourcePickerMode mode, string storyDisplayName)
    {
        var dialog = new ResourcePickerDialog(new ResourcePickerViewModel(type, candidates, mode, storyDisplayName))
            { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? ((ResourcePickerViewModel)dialog.DataContext).SelectedResource : null;
    }
}
