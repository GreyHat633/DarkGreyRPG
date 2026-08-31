using System.Windows;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class ActorWorkspaceDialogs(Func<Window?> ownerProvider) : IActorWorkspaceDialogs
{
    public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
    {
        var viewModel = new DisplayNameDialogViewModel(resourceLabel, id, currentDisplayName);
        var dialog = new DisplayNameDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.DisplayName.Trim() : null;
    }

    public bool SupportsCanonicalActorKinds => true;

    public CanonicalStoryActorKind? RequestCanonicalCreationKind(string storyDisplayName)
    {
        var viewModel = new CanonicalActorCreationChoiceViewModel(storyDisplayName);
        var dialog = new CanonicalActorCreationChoiceDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedKind : null;
    }

    public CanonicalActorIdentityRequest? RequestCreateCanonical(CanonicalStoryActorKind kind, string suggestedId)
    {
        var viewModel = new CanonicalActorIdentityDialogViewModel(kind, suggestedId);
        var dialog = new CanonicalActorIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new CanonicalActorIdentityRequest(kind, viewModel.Id, viewModel.DisplayName.Trim(), viewModel.Tags)
            : null;
    }

    public ActorCreationMode? RequestCreationMode(string storyDisplayName)
    {
        var viewModel = new ActorCreationChoiceViewModel(storyDisplayName);
        var dialog = new ActorCreationChoiceDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
    }

    public ActorIdentityRequest? RequestCreate(string suggestedId)
    {
        var viewModel = ActorIdentityDialogViewModel.ForCreate(suggestedId);
        var dialog = new ActorIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new ActorIdentityRequest(viewModel.Id, viewModel.DisplayName.Trim())
            : null;
    }

    public ActorIdentityRequest? RequestImportIdentity(ActorResourceInfo source, string suggestedId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var viewModel = ActorIdentityDialogViewModel.ForImport(source.DisplayName, suggestedId);
        var dialog = new ActorIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new ActorIdentityRequest(viewModel.Id, viewModel.DisplayName.Trim())
            : null;
    }

    public ActorResourceInfo? PickActor(
        IReadOnlyList<ActorResourceInfo> candidates,
        ActorPickerMode mode,
        string storyDisplayName)
    {
        var viewModel = new ActorResourcePickerViewModel(candidates, mode, storyDisplayName);
        var dialog = new ActorResourcePickerDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedActor : null;
    }

    public string? RequestRename(ActorResourceInfo actor, string suggestedId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var viewModel = ActorIdentityDialogViewModel.ForRename(actor.Id, suggestedId);
        var dialog = new ActorIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.Id : null;
    }

    public bool ConfirmDelete(ActorResourceInfo actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return MessageBox.Show(
            ownerProvider(),
            $"确定要删除角色“{actor.DisplayName}”（{actor.Id}）吗？\n该操作会删除 actors/{actor.Id}.json。",
            "删除角色",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return MessageBox.Show(
            ownerProvider(),
            $"从“{storyDisplayName}”解除对角色“{actor.DisplayName}”（{actor.Id}）的引用吗？\n角色文件及其归属故事不会被删除。",
            "解除角色引用",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references)
    {
        var dialog = new ActorReferencesDialog(new ActorReferencesViewModel(actor, references))
        {
            Owner = ownerProvider(),
        };
        dialog.ShowDialog();
    }

    public bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor) =>
        MessageBox.Show(
            ownerProvider(),
            $"角色“{actor.DisplayName}”（{actor.Id}）有未保存的更改。\n保存后再切换资源吗？",
            "未保存的更改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes) == MessageBoxResult.Yes;

    public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor) =>
        MessageBox.Show(
            ownerProvider(),
            $"角色“{actor.DisplayName}”（{actor.Id}）有未保存的更改。\n选择“是”保存并退出，“否”放弃更改，“取消”返回编辑。",
            "未保存的更改",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
}
