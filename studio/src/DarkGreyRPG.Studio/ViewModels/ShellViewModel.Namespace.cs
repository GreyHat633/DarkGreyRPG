using System.IO;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Settings;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private readonly ISettingsService? _namespaceSettings;
    private readonly INamespaceDialogs _namespaceDialogs;
    private readonly NamespaceProjectMigrationService _namespaceMigration = new();
    private NamespaceMigrationPreview? _lastNamespaceMigration;

    public RelayCommand ChangeGlobalNamespaceCommand { get; }
    public RelayCommand ChangeStoryNamespaceCommand { get; }
    public RelayCommand ReturnStoryToGlobalNamespaceCommand { get; }
    public RelayCommand UndoNamespaceMigrationCommand { get; }
    public RelayCommand AddExternalReferenceCommand { get; }

    public void AddExternalReference(string? storyId)
        => PickOfflineResourceReference(storyId, null, externalOnly: true);

    private bool PrepareLegacyProjectCompatibility(string root)
    {
        // Production opening upgrades old authoring storage once, with the existing
        // validated backup transaction. There is no user-facing architecture command.
        if (_namespaceSettings is null) return true;
        var legacy = Path.Combine(root, "stories");
        var canonical = Path.Combine(root, "resources", "canonical", "stories");
        if (!Directory.Exists(legacy) || !Directory.EnumerateFiles(legacy, "*.json", SearchOption.AllDirectories).Any()
            || (Directory.Exists(canonical) && Directory.EnumerateFiles(canonical, "*.json", SearchOption.AllDirectories).Any())) return true;
        try
        {
            var preview = DarkGreyRPG.Studio.Core.Graphs.Migration.CanonicalProjectMigrationPreview.PreviewProject(root);
            if (!preview.CanApply) throw new InvalidDataException("旧项目格式无法自动升级：" + string.Join("；", preview.Issues.Select(issue => issue.Message)));
            if (preview.ProposedWrites.Count != 0)
                new DarkGreyRPG.Studio.Core.Graphs.Migration.CanonicalProjectMigrationTransaction().Apply(preview);
            return true;
        }
        catch (Exception exception) { ReportFailure("打开旧项目", exception); return false; }
    }

    public void InitializeNamespaceWorkspace(string? lastProject)
    {
        if (_namespaceSettings is null) { RestoreLastProject(lastProject); return; }
        try
        {
            RestoreLastProject(lastProject);
        }
        catch (Exception exception) { ReportFailure("初始化 NameSpace", exception); }
    }

    private string? RequestInitialNamespace()
    {
        if (_namespaceSettings is null) return null;
        var current = _namespaceSettings.Load().GlobalNamespace;
        var value = _namespaceDialogs.RequestNamespace(current, firstUse: true);
        if (value is null) return null;
        if (!DgrResourceId.IsValidNamespace(value)) throw new InvalidOperationException("NameSpace 格式无效，未保存设置。");
        return value;
    }

    private void InitializeNewProjectNamespace(string root, string? value)
    {
        if (_namespaceSettings is null || value is null) return;
        var preview = _namespaceMigration.PreviewGlobal(root, value);
        _namespaceMigration.Apply(preview);
        try { _namespaceSettings.Save(_namespaceSettings.Load() with { GlobalNamespace = value }); }
        catch { _namespaceMigration.Undo(preview); throw; }
    }

    private bool PrepareProjectNamespace(string root)
    {
        if (_namespaceSettings is null) return true;
        try
        {
            var policy = NamespacePolicyStore.Load(root);
            if (policy is not null) return true;
            var value = _namespaceDialogs.RequestNamespace(_namespaceSettings.Load().GlobalNamespace, firstUse: false);
            if (value is null) return false;
            if (!DgrResourceId.IsValidNamespace(value)) throw new InvalidOperationException("NameSpace 格式无效。");
            var preview = _namespaceMigration.PreviewGlobal(root, value!);
            if (!_namespaceDialogs.ConfirmMigration(preview)) return false;
            _namespaceMigration.Apply(preview);
            _lastNamespaceMigration = preview;
            return true;
        }
        catch (Exception exception) { ReportFailure("准备项目 NameSpace", exception); return false; }
    }

    private bool CanChangeNamespace()
    {
        if (_namespaceSettings is null || !HasProject) return false;
        if (!HasUnsavedDocuments()) return true;
        ReportWarning("请先保存当前项目资源，再修改 NameSpace。", "NameSpace");
        return false;
    }

    private void ChangeGlobalNamespace()
    {
        if (!CanChangeNamespace()) return;
        try
        {
            var current = NamespacePolicyStore.Load(ProjectDirectory)?.GlobalNamespace;
            var value = _namespaceDialogs.RequestNamespace(current, false);
            if (value is null || value == current) return;
            if (!DgrResourceId.IsValidNamespace(value)) throw new InvalidOperationException("NameSpace 格式无效。");
            if (!HasProject) { ReportWarning("请先新建或打开项目，再修改全局 NameSpace。", "NameSpace"); return; }
            var preview = _namespaceMigration.PreviewGlobal(ProjectDirectory, value);
            ApplyNamespacePreview(preview);
        }
        catch (Exception exception) { ReportFailure("修改全局 NameSpace", exception); }
    }

    public void ChangeStoryNamespace(string? storyId, bool returnToGlobal)
    {
        if (storyId is null || !HasProject || !CanChangeNamespace()) return;
        try
        {
            var policy = NamespacePolicyStore.Load(ProjectDirectory)
                ?? throw new InvalidOperationException("请先完成项目 NameSpace 迁移。");
            var value = returnToGlobal ? null : _namespaceDialogs.RequestNamespace(policy.EffectiveNamespace(storyId), false);
            if (!returnToGlobal && value is null) return;
            var preview = returnToGlobal ? _namespaceMigration.PreviewReturnToGlobal(ProjectDirectory, storyId)
                : _namespaceMigration.PreviewCustom(ProjectDirectory, storyId, value!);
            ApplyNamespacePreview(preview);
        }
        catch (Exception exception) { ReportFailure("修改故事 NameSpace", exception); }
    }

    private void ApplyNamespacePreview(NamespaceMigrationPreview preview)
    {
        if (!_namespaceDialogs.ConfirmMigration(preview)) return;
        _namespaceMigration.Apply(preview);
        _lastNamespaceMigration = preview;
        OpenProjectFromDirectory(preview.ProjectDirectory, false);
        ReportSuccess($"NameSpace 已更新，共修改 {preview.ChangedFileCount} 个文件；可撤销本次迁移。", "NameSpace");
    }

    private bool CanUndoProjectNamespace() => _lastNamespaceMigration is { } preview && HasProject
        && string.Equals(Path.GetFullPath(ProjectDirectory), Path.GetFullPath(preview.ProjectDirectory), StringComparison.OrdinalIgnoreCase)
        && !HasUnsavedDocuments();

    private void UndoCurrent()
    {
        if (CanonicalStoryWorkspace?.InspectorPortraitEditor is { } portrait) { portrait.UndoCommand.Execute(null); return; }
        if (ActiveProjectGraphHost is { CanUndo: true } graph) { graph.Undo(); return; }
        if (TryUndoReference()) return;
        if (ActiveEditor?.UndoCommand.CanExecute(null) == true) ActiveEditor.UndoCommand.Execute(null);
        else if (CanUndoProjectNamespace()) UndoNamespaceMigration();
    }

    private void UndoNamespaceMigration()
    {
        if (_lastNamespaceMigration is not { } preview || !CanChangeNamespace()) return;
        if (!HasProject || !string.Equals(Path.GetFullPath(ProjectDirectory), Path.GetFullPath(preview.ProjectDirectory), StringComparison.OrdinalIgnoreCase))
        {
            ReportWarning("只能撤销当前项目的 NameSpace 迁移。", "NameSpace");
            return;
        }
        try
        {
            _namespaceMigration.Undo(preview);
            _lastNamespaceMigration = null;
            // Reload without proposing the migration which has just been undone.
            ReloadAfterNamespaceUndo(preview.ProjectDirectory);
            ReportSuccess("本次 NameSpace 迁移已完整撤销。", "NameSpace");
        }
        catch (Exception exception) { ReportFailure("撤销 NameSpace 迁移", exception); }
    }

    private void ReloadAfterNamespaceUndo(string root)
    {
        _projectService.OpenProject(root);
        SetCanonicalStoryWorkspace(null);
        _canonicalGraphStore = _canonicalGraphStoreFactory(root);
        _canonicalSaveCoordinator = new(_canonicalGraphStore);
        CurrentActor = null; CurrentDialogue = null; CurrentQuest = null; CurrentFlow = null;
        LoadActorList(); LoadStoryList(); StoryWorkspace.CloseStory(); ProjectHome.ShowHome();
        RefreshProblems(); RaiseWorkspaceCommandStates();
    }

    private string NamespaceCreationId(string id, string? storyId = null)
    {
        if (_namespaceSettings is null) return id;
        var policy = NamespacePolicyStore.Load(ProjectDirectory)
            ?? throw new InvalidOperationException("请先完成项目 NameSpace 迁移。");
        var value = storyId is null ? policy.GlobalNamespace : policy.EffectiveNamespace(storyId);
        if (!id.Contains(':')) return DgrResourceId.Qualify(value, id);
        if (!DgrResourceId.IsFullId(id) || DgrResourceId.Namespace(id) != value)
            throw new InvalidOperationException($"新建资源必须使用当前 NameSpace：{value}。");
        return id;
    }
}
