using System.Collections.ObjectModel;
using System.IO;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record ReferencedResourceRow(OfflineResourceChoice Resource, RelayCommand ViewCommand, RelayCommand? GraphCommand = null)
{
    public string Label => $"{Resource.DisplayName} · {Resource.Id}";
}

public sealed record ReferencedPackageRow(string Label, string Identity, string Details,
    IReadOnlyList<ReferencedResourceRow> Resources, RelayCommand RemoveCommand, RelayCommand? SelectCommand = null,
    RelayCommand? StoryGraphCommand = null)
{
    public IReadOnlyList<ReferencedResourceFolder> Folders { get; } =
    [
        new("角色", Resources.Where(row => row.Resource.Kind == "Actor").ToArray()),
        new("物品", Resources.Where(row => row.Resource.Kind is "Item" or "ItemGroup").ToArray()),
        new("会话", Resources.Where(row => row.Resource.Kind == "Session").ToArray()),
        new("任务", Resources.Where(row => row.Resource.Kind == "Task").ToArray())
    ];
}

public sealed record ReferencedResourceFolder(string DisplayName, IReadOnlyList<ReferencedResourceRow> Items)
{
    public bool HasResources => Items.Count > 0;
}

public sealed partial class ShellViewModel
{
    private readonly IOfflinePackageDialogs _offlinePackageDialogs;
    public ObservableCollection<ReferencedPackageRow> ReferencedPackages { get; } = [];
    private ReferencedPackageRow? _selectedReferencedPackage;
    private OfflineResourceChoice? _selectedReferencedResource;
    public OfflineResourceChoice? SelectedReferencedResource
    {
        get => _selectedReferencedResource;
        set { if (SetProperty(ref _selectedReferencedResource, value)) OnPropertyChanged(nameof(SelectedReferenceGraphCommand)); }
    }
    public RelayCommand SelectedReferenceGraphCommand => new(() =>
    {
        if (SelectedReferencedResource is { HasGraph: true } resource) _offlinePackageDialogs.ShowReadOnlyResource(resource);
    });
    public ReferencedPackageRow? SelectedReferencedPackage
    {
        get => _selectedReferencedPackage;
        set { if (SetProperty(ref _selectedReferencedPackage, value)) SelectedReferencedResource = null; }
    }
    public RelayCommand ReferencePackageCommand { get; }
    public RelayCommand ImportPackageCommand { get; }

    private bool CanChangePackages()
    {
        if (!HasProject) return false;
        if (!HasUnsavedDocuments()) return true;
        ReportWarning("请先保存当前资源，再更改故事包。", "故事包");
        return false;
    }

    private void ReferencePackage()
    {
        if (!HasProject) return;
        if (_offlinePackageDialogs.PickPackageFile(OfflinePackageDialogKind.Reference) is { } path) ReferencePackageFromFile(path);
    }

    public void ReferencePackageFromFile(string path)
    {
        if (!HasProject) return;
        try
        {
            var before = CaptureReferenceFiles();
            var package = new OfflineReferencePackageService().AddOrUpdate(ProjectDirectory, path);
            RecordReferenceChange(before);
            RefreshOfflineWorkspace();
            ReportSuccess($"已引用故事包 {package.Identity}（只读）。", "故事包");
        }
        catch (Exception exception) { ReportFailure("引用故事包", exception); }
    }

    private void ImportPackage()
    {
        if (!CanChangePackages()) return;
        if (_offlinePackageDialogs.PickPackageFile(OfflinePackageDialogKind.Import) is { } path) ImportPackageFromFile(path);
    }

    public void ImportPackageFromFile(string path)
    {
        if (!CanChangePackages()) return;
        try
        {
            var service = new OfflineStoryPackageImportService();
            var plan = service.BuildPlan(ProjectDirectory, path);
            if (!plan.CanApply)
            {
                var text = "导入已取消，项目未修改：" + Environment.NewLine
                    + string.Join(Environment.NewLine, plan.Conflicts.Select(conflict => $"{conflict.Kind} {conflict.Id}：{conflict.Message}"));
                ReportWarning(text, "故事包冲突");
                return;
            }
            service.Apply(plan);
            RefreshOfflineWorkspace(plan.Package.Manifest.StoryId);
            ReportSuccess($"已导入故事 {plan.Package.Manifest.StoryId}，现为当前项目的可编辑内容。", "故事包");
        }
        catch (Exception exception) { ReportFailure("导入故事包", exception); }
    }

    private void LoadReferencedPackages()
    {
        ReferencedPackages.Clear();
        SelectedReferencedPackage = null;
        if (!HasProject) return;
        var catalog = OfflineProviderCatalog.Load(_projectService.CurrentProject!.ProjectDirectory);
        foreach (var package in catalog.Providers)
        {
            var choices = package.Resources.Select(ToOfflineChoice).ToArray();
            var graphs = choices.Where(choice => choice.HasGraph).ToArray();
            choices = choices.Select(choice => choice with { RelatedGraphs = graphs }).ToArray();
            ReferencedPackageRow? row = null;
            row = new ReferencedPackageRow(
                package.Resources.FirstOrDefault(resource => resource.Kind == DgrResourceKind.Story)?.DisplayName ?? package.Identity.PackageId,
                package.Identity.ToString(),
                $"故事包：{Path.GetFileName(package.PackagePath)}\nStory ID：{package.Manifest.StoryId}",
                choices.Where(choice => choice.Kind != "Story").Select(choice => new ReferencedResourceRow(choice,
                    new RelayCommand(() => SelectedReferencedResource = choice),
                    new RelayCommand(() => _offlinePackageDialogs.ShowReadOnlyResource(choice)))).ToArray(),
                new RelayCommand(() => RemoveReferencedPackage(package.Identity.PackageId)),
                new RelayCommand(() => SelectedReferencedPackage = row),
                new RelayCommand(() => _offlinePackageDialogs.ShowReadOnlyResource(choices.Single(choice => choice.Kind == "Story"))));
            ReferencedPackages.Add(row);
        }
    }

    public void RemoveReferencedPackage(string packageId)
    {
        if (!HasProject) return;
        try
        {
            var package = OfflineProviderCatalog.Load(ProjectDirectory).Providers.Single(provider => provider.Identity.PackageId == packageId);
            var keys = package.Resources.Select(resource => new DgrResourceKey(resource.Kind, resource.Id)).ToHashSet();
            var consumers = _canonicalGraphStore!.Memberships.List().Select(info => _canonicalGraphStore.Memberships.Load(info.StoryId))
                .Where(member => MembershipKeys(member.ReferencedResources).Any(keys.Contains))
                .Select(member => member.StoryId).ToArray();
            if (!_offlinePackageDialogs.ConfirmRemoval(package.Identity.ToString(), consumers)) return;
            var before = CaptureReferenceFiles();
            new OfflineReferencePackageService().Remove(ProjectDirectory, packageId, allowUnresolvedReferences: true);
            RecordReferenceChange(before);
            RefreshOfflineWorkspace();
            ReportSuccess($"已移除引用包 {packageId}；资源引用记录已保留。", "故事包");
        }
        catch (Exception exception) { ReportFailure("移除引用故事包", exception); }
    }

    private void RefreshOfflineWorkspace(string? openStoryId = null)
    {
        // Refresh membership/provider projections without replacing native drafts or their history.
        LoadReferencedPackages();
        if (CanonicalStoryWorkspace is { } workspace)
        {
            var snapshot = new CanonicalStoryWorkspaceLoader(_canonicalGraphStore!).Load(workspace.StoryEditor.Id);
            workspace.ApplyResourceSnapshot(snapshot, workspace.SelectedFolderKind ?? CanonicalStoryFolderKind.Actors);
        }
        foreach (var retained in _retainedStoryWorkspaces.Values)
        {
            var snapshot = new CanonicalStoryWorkspaceLoader(_canonicalGraphStore!).Load(retained.StoryEditor.Id);
            retained.ApplyResourceSnapshot(snapshot, retained.SelectedFolderKind ?? CanonicalStoryFolderKind.Actors);
        }
        RefreshHomeResourceFolders();
        if (openStoryId is not null && CanonicalStoryWorkspace?.StoryEditor.Id != openStoryId)
        {
            LoadStoryList();
            if (ProjectHome.Stories.FirstOrDefault(story => story.Id == openStoryId) is { } selected)
            {
                ProjectHome.SelectedStory = selected;
                OpenSelectedStory();
            }
        }
        RefreshProblems();
        RaiseWorkspaceCommandStates();
    }

    private static OfflineResourceChoice ToOfflineChoice(OfflineProviderResource resource)
        => new(resource.Kind.ToString(), resource.Id, resource.DisplayName,
            ProviderDescription(resource), resource.DefinitionJson)
        {
            SourcePackageName = Path.GetFileName(resource.PackagePath),
            SourceStoryId = resource.StoryId,
            ResourceIdLabel = ResourceIdLabel(resource),
        };

    private static string ResourceIdLabel(OfflineProviderResource resource)
    {
        using var json = System.Text.Json.JsonDocument.Parse(resource.DefinitionJson);
        if (json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "collective") return "Group ID";
        return resource.Kind.ToString() switch { "Actor" => "NPC ID", "Item" => "Item ID", "ItemGroup" => "Group ID", "Session" => "Session ID", "Task" => "Task ID", "Story" => "Story ID", _ => "资源 ID" };
    }

    internal static string ProviderDescription(OfflineProviderResource resource)
        => $"故事包：{Path.GetFileName(resource.PackagePath)}\nStory ID：{(string.IsNullOrEmpty(resource.StoryId) ? resource.PackageIdentity.PackageId : resource.StoryId)}";

    private IEnumerable<OfflineResourceChoice> NativeResourceChoices()
    {
        var project = _projectService.CurrentProject!;
        foreach (var actor in project.Actors.ListActors())
            yield return new("Actor", actor.Id, actor.DisplayName, "当前项目", File.ReadAllText(actor.SourcePath));
        var items = new ItemRepository(ProjectDirectory);
        foreach (var item in items.ListItems())
            yield return new("Item", item.Id, item.DisplayName, "当前项目", File.ReadAllText(items.GetItemPath(item.Id)));
        foreach (var item in items.ListGroups())
            yield return new("ItemGroup", item.Id, item.DisplayName, "当前项目", File.ReadAllText(items.GetGroupPath(item.Id)));
        foreach (var repository in new[] { _canonicalGraphStore!.Sessions, _canonicalGraphStore.Tasks })
            foreach (var info in repository.List())
            {
                var graph = repository.Load(info.Id);
                yield return new(graph.ResourceKind.ToString(), graph.Id, graph.DisplayName, "当前项目", graph.ToJson());
            }
    }

    private void PickOfflineResourceReference(string? storyId, IReadOnlySet<DgrResourceKind>? kinds, bool externalOnly = false)
    {
        if (storyId is null || _canonicalGraphStore is null || !HasProject) return;
        try
        {
            var membership = _canonicalGraphStore.Memberships.Load(storyId);
            var present = MembershipKeys(membership.OwnedResources).Concat(MembershipKeys(membership.ReferencedResources)).ToHashSet();
            bool Offered(OfflineResourceChoice choice) => Enum.TryParse<DgrResourceKind>(choice.Kind, out var kind)
                && kind != DgrResourceKind.Story && (kinds is null || kinds.Contains(kind)) && !present.Contains(new(kind, choice.Id));
            var native = externalOnly ? [] : NativeResourceChoices().Where(Offered).ToArray();
            var external = OfflineProviderCatalog.Load(ProjectDirectory).Resources.Select(ToOfflineChoice).Where(Offered).ToArray();
            var choice = _offlinePackageDialogs.PickResource(native, external, "引用资源");
            if (choice is null) return;
            if (!native.Contains(choice) && !external.Contains(choice)) throw new InvalidOperationException("所选资源不在当前 Provider 目录中。");
            var selectedKind = Enum.Parse<DgrResourceKind>(choice.Kind);
            var before = CaptureReferenceFiles(storyId);
            new CanonicalExternalReferenceService(_canonicalGraphStore).AddReference(storyId, selectedKind, choice.Id);
            RecordReferenceChange(before, storyId);
            RefreshOfflineWorkspace(storyId);
            ReportSuccess($"已引用 {choice.DisplayName} · {choice.Id}。", "故事包");
        }
        catch (Exception exception) { ReportFailure("引用资源", exception); }
    }

    private static IEnumerable<DgrResourceKey> MembershipKeys(CanonicalStoryMembershipSet set)
        => set.Actors.Select(id => new DgrResourceKey(DgrResourceKind.Actor, id))
            .Concat(set.Items.Select(id => new DgrResourceKey(DgrResourceKind.Item, id)))
            .Concat(set.ItemGroups.Select(id => new DgrResourceKey(DgrResourceKind.ItemGroup, id)))
            .Concat(set.Sessions.Select(id => new DgrResourceKey(DgrResourceKind.Session, id)))
            .Concat(set.Tasks.Select(id => new DgrResourceKey(DgrResourceKind.Task, id)));
}
