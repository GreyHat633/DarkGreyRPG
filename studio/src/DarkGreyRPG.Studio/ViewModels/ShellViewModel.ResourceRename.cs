using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private void RenameCanonicalResourceIdentity(DgrResourceKind kind, string id, ResourceRenameRequest rename)
    {
        if (HasUnsavedDocuments())
        {
            ReportWarning("修改资源 ID 需要同步迁移项目内引用。请先保存未保存的编辑后重试；本次未修改任何文件。仅修改显示名称或标签不受此限制。", "编辑资源");
            return;
        }
        try
        {
            var storyId = CanonicalStoryWorkspace?.StoryEditor.Id;
            var preview = _namespaceMigration.PreviewResourceRename(ProjectDirectory, kind, id, rename.Id, rename.DisplayName, rename.Tags);
            _namespaceMigration.Apply(preview);
            // The identity migration is explicit and all-or-rollback; rebuild after its committed IDs change.
            SetCanonicalStoryWorkspace(null);
            LoadActorList();
            LoadStoryList();
            if (storyId is not null) TryOpenCanonicalStory(storyId);
            _referenceUndo.Clear();
            _referenceRedo.Clear();
            RefreshProblems();
            RaiseWorkspaceCommandStates();
            ReportSuccess($"已编辑资源：{id} → {rename.Id}，并更新项目内引用。", "编辑资源");
        }
        catch (Exception exception) { ReportFailure("编辑资源 ID", exception); }
    }
}
