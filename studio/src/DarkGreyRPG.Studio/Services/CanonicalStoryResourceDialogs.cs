using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class CanonicalStoryResourceDialogs(Func<Window?> ownerProvider) : ICanonicalStoryResourceDialogs
{
    public ResourceRenameRequest? RequestResourceRename(string resourceLabel, string id, string currentDisplayName, IReadOnlyList<string>? tags = null)
    {
        var viewModel = new DisplayNameDialogViewModel(resourceLabel, id, currentDisplayName, allowIdentityEdit: true, tags: tags);
        var dialog = new DisplayNameDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? new(viewModel.NewId, viewModel.DisplayName.Trim(), viewModel.Tags) : null;
    }

    public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
    {
        var viewModel = new DisplayNameDialogViewModel(resourceLabel, id, currentDisplayName);
        var dialog = new DisplayNameDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? viewModel.DisplayName.Trim() : null;
    }

    public CanonicalGraphResourceIdentityRequest? RequestCreate(GraphResourceKind resourceKind, string suggestedId)
    {
        CanonicalResourceIdentityDialogViewModel.EnsureSupportedKind(resourceKind);
        var viewModel = CanonicalResourceIdentityDialogViewModel.ForCreate(resourceKind, suggestedId);
        var dialog = new CanonicalResourceIdentityDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true
            ? new CanonicalGraphResourceIdentityRequest(viewModel.Id, viewModel.DisplayName.Trim(), viewModel.Tags)
            : null;
    }

    public CanonicalGraphResourceChoice? PickReference(
        GraphResourceKind resourceKind,
        IReadOnlyList<GraphResourceInfo> candidates,
        string storyDisplayName)
    {
        CanonicalResourceIdentityDialogViewModel.EnsureSupportedKind(resourceKind);
        var viewModel = new CanonicalResourcePickerViewModel(resourceKind, candidates, storyDisplayName);
        var dialog = new CanonicalResourcePickerDialog(viewModel) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true && viewModel.SelectedResource is { } selected
            ? new CanonicalGraphResourceChoice(selected)
            : null;
    }

    public bool ConfirmRemoveReference(CanonicalGraphResourceChoice resource, string storyDisplayName)
    {
        ValidateChoice(resource);
        return MessageBox.Show(
            ownerProvider(),
            $"从“{storyDisplayName}”解除对{ChineseLabel(resource.ResourceKind)}“{resource.DisplayName}”({resource.Id}) 的引用吗？\n资源文件会保留，不会被删除。",
            $"解除{ChineseLabel(resource.ResourceKind)}引用",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource)
    {
        ValidateChoice(resource);
        return MessageBox.Show(
            ownerProvider(),
            $"确定要删除{ChineseLabel(resource.ResourceKind)}“{resource.DisplayName}”({resource.Id}) 吗？\n该操作会删除 {DirectoryName(resource.ResourceKind)}/{resource.Id}.json。",
            $"删除{ChineseLabel(resource.ResourceKind)}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmAggregateInterfaceRemoval(
        CanonicalGraphResourceChoice resource,
        IReadOnlyList<GraphConnection> affectedConnections)
    {
        ValidateChoice(resource);
        ArgumentNullException.ThrowIfNull(affectedConnections);
        var details = affectedConnections.Count == 0
            ? "（未检测到外部连线。）"
            : string.Join(Environment.NewLine, affectedConnections.Select(connection =>
                $"• {connection.FromNodeId}.{connection.FromPortId} → {connection.ToNodeId}.{connection.ToPortId} [{connection.InterfaceKind}]"));
        return MessageBox.Show(
            ownerProvider(),
            $"保存{ChineseLabel(resource.ResourceKind)}“{resource.DisplayName}”({resource.Id}) 将删除以下 Story Flow 外部连线：\n{details}\n\n继续保存并删除这些连线吗？",
            "确认聚合接口变更",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowDeleteBlocked(CanonicalGraphResourceChoice resource, IReadOnlyList<string> storyIds)
    {
        ValidateChoice(resource);
        ArgumentNullException.ThrowIfNull(storyIds);
        var ids = storyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();
        var details = ids.Length == 0 ? "（未提供故事 ID。）" : string.Join(Environment.NewLine, ids.Select(id => $"• {id}"));
        MessageBox.Show(
            ownerProvider(),
            $"无法删除{ChineseLabel(resource.ResourceKind)}“{resource.DisplayName}”({resource.Id})。\n该资源仍被以下 Story 占用或引用：\n{details}\n请先解除这些占用或引用。",
            $"无法删除{ChineseLabel(resource.ResourceKind)}",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void ValidateChoice(CanonicalGraphResourceChoice resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        CanonicalResourceIdentityDialogViewModel.EnsureSupportedKind(resource.ResourceKind);
        if (string.IsNullOrWhiteSpace(resource.Id)) throw new ArgumentException("Canonical resource ID cannot be blank.", nameof(resource));
        if (string.IsNullOrWhiteSpace(resource.DisplayName)) throw new ArgumentException("Canonical resource display name cannot be blank.", nameof(resource));
    }

    private static string ChineseLabel(GraphResourceKind kind)
        => CanonicalResourceIdentityDialogViewModel.ChineseLabel(kind);

    private static string DirectoryName(GraphResourceKind kind) => kind switch
    {
        GraphResourceKind.Session => "sessions",
        GraphResourceKind.Task => "tasks",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>Non-interactive default used when the shell is hosted without WPF dialogs.</summary>
public sealed class NullCanonicalStoryResourceDialogs : ICanonicalStoryResourceDialogs
{
    public CanonicalGraphResourceIdentityRequest? RequestCreate(GraphResourceKind resourceKind, string suggestedId) => null;

    public CanonicalGraphResourceChoice? PickReference(
        GraphResourceKind resourceKind,
        IReadOnlyList<GraphResourceInfo> candidates,
        string storyDisplayName) => null;

    public bool ConfirmRemoveReference(CanonicalGraphResourceChoice resource, string storyDisplayName) => false;

    public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource) => false;

    public bool ConfirmAggregateInterfaceRemoval(
        CanonicalGraphResourceChoice resource,
        IReadOnlyList<GraphConnection> affectedConnections) => false;

    public void ShowDeleteBlocked(CanonicalGraphResourceChoice resource, IReadOnlyList<string> storyIds) { }
}
