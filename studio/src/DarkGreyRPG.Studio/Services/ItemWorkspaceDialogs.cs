using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class ItemWorkspaceDialogs(Func<Window?> ownerProvider) : IItemWorkspaceDialogs
{
    public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
    {
        var viewModel = new DisplayNameDialogViewModel(resourceLabel, id, currentDisplayName);
        var dialog = new DisplayNameDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.DisplayName.Trim() : null;
    }

    public ItemCreationMode? RequestCreationMode(string storyDisplayName)
    {
        var viewModel = new CanonicalItemCreationChoiceViewModel(storyDisplayName);
        var dialog = new CanonicalItemCreationChoiceDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
    }

    public ItemIdentityRequest? RequestCreate(CanonicalStoryItemKind kind, string suggestedId)
    {
        var viewModel = CanonicalItemIdentityDialogViewModel.ForCreate(kind, suggestedId);
        var dialog = new CanonicalItemIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true && viewModel.CanConfirm
            ? new ItemIdentityRequest(kind, viewModel.Id, viewModel.DisplayName.Trim(), viewModel.Tags)
            : null;
    }

    public ItemWorkspaceChoice? PickReference(IReadOnlyList<ItemResourceInfo> candidates, string storyDisplayName)
    {
        var viewModel = new CanonicalItemPickerViewModel(candidates, storyDisplayName);
        var dialog = new CanonicalItemPickerDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true && viewModel.SelectedResource is { } selected
            ? new ItemWorkspaceChoice(selected)
            : null;
    }

    public bool ConfirmRemoveReference(ItemWorkspaceChoice resource, string storyDisplayName)
    {
        ValidateChoice(resource);
        return MessageBox.Show(ownerProvider(),
            $"从“{storyDisplayName}”解除对{ChineseLabel(resource.Kind)}“{resource.DisplayName}”({resource.Id}) 的引用吗？\n资源文件会保留，不会被删除。",
            $"解除{ChineseLabel(resource.Kind)}引用", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteOwned(ItemWorkspaceChoice resource)
    {
        ValidateChoice(resource);
        return MessageBox.Show(ownerProvider(),
            $"确定要删除{ChineseLabel(resource.Kind)}“{resource.DisplayName}”({resource.Id}) 吗？\n该操作会删除 {DirectoryName(resource.Kind)}/{resource.Id}.json。",
            $"删除{ChineseLabel(resource.Kind)}", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowDeleteBlocked(ItemWorkspaceChoice resource, IReadOnlyList<string> storyIds)
    {
        ValidateChoice(resource);
        var details = storyIds.Count == 0 ? "（未提供故事 ID。）" : string.Join(Environment.NewLine, storyIds.Select(id => $"• {id}"));
        MessageBox.Show(ownerProvider(),
            $"无法删除{ChineseLabel(resource.Kind)}“{resource.DisplayName}”({resource.Id})。\n该资源仍被以下 Story 占用或引用：\n{details}\n请先解除这些占用或引用。",
            $"无法删除{ChineseLabel(resource.Kind)}", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void ValidateChoice(ItemWorkspaceChoice resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!Enum.IsDefined(resource.Kind) || string.IsNullOrWhiteSpace(resource.Id) || string.IsNullOrWhiteSpace(resource.DisplayName))
            throw new ArgumentException("Item choice is incomplete.", nameof(resource));
    }

    private static string ChineseLabel(CanonicalStoryItemKind kind) => kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组";
    private static string DirectoryName(CanonicalStoryItemKind kind) => kind == CanonicalStoryItemKind.Individual ? "items" : "item_groups";
}

public sealed class NullItemWorkspaceDialogs : IItemWorkspaceDialogs
{
    public ItemCreationMode? RequestCreationMode(string storyDisplayName) => null;
    public ItemIdentityRequest? RequestCreate(CanonicalStoryItemKind kind, string suggestedId) => null;
    public ItemWorkspaceChoice? PickReference(IReadOnlyList<ItemResourceInfo> candidates, string storyDisplayName) => null;
    public bool ConfirmRemoveReference(ItemWorkspaceChoice resource, string storyDisplayName) => false;
    public bool ConfirmDeleteOwned(ItemWorkspaceChoice resource) => false;
    public void ShowDeleteBlocked(ItemWorkspaceChoice resource, IReadOnlyList<string> storyIds) { }
}
