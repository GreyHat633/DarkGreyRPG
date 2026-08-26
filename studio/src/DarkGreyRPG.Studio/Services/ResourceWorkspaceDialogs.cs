using System.Windows;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class ResourceWorkspaceDialogs(Func<Window?> ownerProvider) : IResourceWorkspaceDialogs
{
    public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName)
    {
        var viewModel = new ResourceCreationChoiceViewModel(type, storyDisplayName);
        var dialog = new ResourceCreationChoiceDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
    }

    public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId)
    {
        var viewModel = ResourceIdentityDialogViewModel.ForCreate(type, suggestedId);
        var dialog = new ResourceIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new ResourceIdentityRequest(viewModel.Id, viewModel.DisplayName.Trim())
            : null;
    }

    public ResourceIdentityRequest? RequestImportIdentity(
        ProjectResourceType type,
        ResourceDescriptor source,
        string suggestedId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var viewModel = ResourceIdentityDialogViewModel.ForImport(type, source.DisplayName, suggestedId);
        var dialog = new ResourceIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new ResourceIdentityRequest(viewModel.Id, viewModel.DisplayName.Trim())
            : null;
    }

    public ResourceDescriptor? PickResource(
        ProjectResourceType type,
        IReadOnlyList<ResourceDescriptor> candidates,
        ResourcePickerMode mode,
        string storyDisplayName)
    {
        var viewModel = new ResourcePickerViewModel(type, candidates, mode, storyDisplayName);
        var dialog = new ResourcePickerDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedResource : null;
    }

    public bool ConfirmDelete(ResourceDescriptor resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var label = ResourceIdentityDialogViewModel.ChineseLabel(resource.Type);
        return MessageBox.Show(
            ownerProvider(),
            $"确定要删除{label}“{resource.DisplayName}”({resource.Id}) 吗？\n该操作会删除 {DirectoryName(resource.Type)}/{resource.Id}.json。",
            $"删除{label}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var label = ResourceIdentityDialogViewModel.ChineseLabel(resource.Type);
        return MessageBox.Show(
            ownerProvider(),
            $"从“{storyDisplayName}”解除对{label}“{resource.DisplayName}”({resource.Id}) 的引用吗？\n资源文件及其 Home Story 不会被删除。",
            $"解除{label}引用",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references)
    {
        var dialog = new ResourceReferencesDialog(new ResourceReferencesViewModel(resource, references))
        {
            Owner = ownerProvider(),
        };
        dialog.ShowDialog();
    }

    public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) =>
        MessageBox.Show(
            ownerProvider(),
            $"{ResourceIdentityDialogViewModel.Label(resource.Type)} “{resource.DisplayName}”({resource.Id}) 有未保存的更改。\n保存后再切换资源吗？",
            "未保存的更改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes) == MessageBoxResult.Yes;

    public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) =>
        MessageBox.Show(
            ownerProvider(),
            $"{ResourceIdentityDialogViewModel.Label(resource.Type)} “{resource.DisplayName}”({resource.Id}) 有未保存的更改。\n选择“是”保存并退出，“否”放弃更改，“取消”返回编辑。",
            "未保存的更改",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };

    private static string DirectoryName(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Dialogue => "dialogues",
        ProjectResourceType.Quest => "quests",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
