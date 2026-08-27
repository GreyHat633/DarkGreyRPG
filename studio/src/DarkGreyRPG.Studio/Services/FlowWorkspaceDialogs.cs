using System.Windows;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Services;

public sealed class FlowWorkspaceDialogs(Func<Window?> ownerProvider) : IFlowWorkspaceDialogs
{
    public StoryFlowRecoveryChoice ChooseRecovery(StoryFlowRecoverySnapshot snapshot) =>
        MessageBox.Show(
            ownerProvider(),
            $"检测到 Story Flow '{snapshot.StoryId}' 的恢复草稿（{snapshot.CapturedUtc.LocalDateTime:g}）。\n\n“是”：恢复草稿\n“否”：本次忽略并打开正式版本\n“取消”：删除恢复草稿",
            "Story Flow 草稿恢复",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Yes) switch
        {
            MessageBoxResult.Yes => StoryFlowRecoveryChoice.Recover,
            MessageBoxResult.No => StoryFlowRecoveryChoice.Ignore,
            _ => StoryFlowRecoveryChoice.Delete,
        };

    public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(StoryFlowEditorViewModel flow) =>
        MessageBox.Show(
            ownerProvider(),
            $"Story Flow '{flow.Id}' 有未保存的更改。\n选择“是”保存并离开，“否”放弃草稿，“取消”返回编辑。",
            "未保存的 Story Flow",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };

    public ConnectedDynamicOutputChoice ConfirmConnectedDialogueExitChange(
        string nodeId,
        string oldOutput,
        string? suggestedOutput)
    {
        if (!string.IsNullOrWhiteSpace(suggestedOutput))
        {
            return MessageBox.Show(
                ownerProvider(),
                $"Dialogue Exit 节点 '{nodeId}' 的出口“{oldOutput}”仍有连接。\n\n“是”：将连接迁移到“{suggestedOutput}”\n“否”：删除旧出口及连接\n“取消”：保留原配置",
                "修改已连接的 Dialogue Exit",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) switch
            {
                MessageBoxResult.Yes => ConnectedDynamicOutputChoice.Migrate,
                MessageBoxResult.No => ConnectedDynamicOutputChoice.DeleteConnection,
                _ => ConnectedDynamicOutputChoice.Cancel,
            };
        }

        return MessageBox.Show(
            ownerProvider(),
            $"Dialogue Exit 节点 '{nodeId}' 的出口“{oldOutput}”仍有连接。删除该出口会同时删除连接。",
            "删除已连接的 Dialogue Exit",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) == MessageBoxResult.OK
                ? ConnectedDynamicOutputChoice.DeleteConnection
                : ConnectedDynamicOutputChoice.Cancel;
    }

    public bool ConfirmRemoveConnectedSequenceStep(string nodeId, string output) =>
        MessageBox.Show(
            ownerProvider(),
            $"Sequence 节点 '{nodeId}' 的步骤 {output} 仍有连接。删除最后一步会同时删除该连接。",
            "删除已连接的 Sequence 步骤",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) == MessageBoxResult.OK;
}

public enum ConnectedDynamicOutputChoice
{
    Cancel,
    Migrate,
    DeleteConnection,
}
