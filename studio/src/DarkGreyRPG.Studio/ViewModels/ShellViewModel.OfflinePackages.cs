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
    public string Label => $"{Resource.DisplayName} {Resource.TypeLabel}";
}

public sealed record ReferencedPackageRow(string Label, string Identity, string Details,
    IReadOnlyList<ReferencedResourceRow> Resources, RelayCommand RemoveCommand, RelayCommand? SelectCommand = null,
    RelayCommand? StoryGraphCommand = null)
{
    public IReadOnlyList<ReferencedResourceFolder> Folders { get; } =
    [
        new("故事", Resources.Where(row => row.Resource.Kind == "Story").ToArray()),
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
            RefreshOfflineWorkspace(plan.ImportedStoryId);
            ReportSuccess($"已导入 {plan.StoryUidMap.Count} 个可编辑故事，并分配新 UID。" + (plan.ReferencedPackagePath is null ? "" : "外部资源依赖保留在完整的只读来源包中。"), "故事包");
        }
        catch (Exception exception) { ReportFailure("导入故事包", exception); }
    }

    private void LoadReferencedPackages()
    {
        ReferencedPackages.Clear();
        SelectedReferencedPackage = null;
        if (!HasProject) return;
        var catalog = OfflineProviderCatalog.Load(_projectService.CurrentProject!.ProjectDirectory);
        foreach (var container in catalog.Providers.GroupBy(package => package.PackagePath, StringComparer.OrdinalIgnoreCase))
        {
            var package = container.First();
            var choices = container.SelectMany(member => member.Resources).DistinctBy(resource => (resource.Kind, resource.Id)).Select(ToOfflineChoice).ToArray();
            var graphs = choices.Where(choice => choice.HasGraph).ToArray();
            choices = choices.Select(choice => choice with { RelatedGraphs = graphs }).ToArray();
            var containerChoice = package.GroupDisplayName is null ? choices.First(choice => choice.Kind == "Story")
                : new OfflineResourceChoice("StoryGroup", string.Empty, package.GroupDisplayName,
                    Path.GetFileName(package.PackagePath), string.Empty)
                { RelatedGraphs = graphs, ContainerGraph = package.ContainerConnections };
            ReferencedPackageRow? row = null;
            row = new ReferencedPackageRow(
                package.GroupDisplayName ?? package.Resources.FirstOrDefault(resource => resource.Kind == DgrResourceKind.Story)?.DisplayName ?? package.Identity.PackageId,
                package.Identity.ToString(),
                $"故事包：{Path.GetFileName(package.PackagePath)}\nStory UID：{string.Join("、", container.Select(member => member.Manifest.StoryId))}",
                choices.Where(choice => package.GroupDisplayName is not null || choice.Kind != "Story").Select(choice => new ReferencedResourceRow(choice,
                    new RelayCommand(() => SelectedReferencedResource = choice),
                    new RelayCommand(() => _offlinePackageDialogs.ShowReadOnlyResource(choice)))).ToArray(),
                new RelayCommand(() => RemoveReferencedPackage(package.Identity.PackageId)),
                new RelayCommand(() => SelectedReferencedPackage = row),
                new RelayCommand(() => _offlinePackageDialogs.ShowReadOnlyResource(containerChoice)));
            ReferencedPackages.Add(row);
        }
    }

    public void RemoveReferencedPackage(string packageId)
    {
        if (!HasProject) return;
        try
        {
            var package = OfflineProviderCatalog.Load(ProjectDirectory).Providers.Single(provider => provider.Identity.PackageId == packageId);
            var keys = OfflineProviderCatalog.Load(ProjectDirectory).Providers.Where(member => member.PackagePath == package.PackagePath)
                .SelectMany(member => member.Resources).Select(resource => new DgrResourceKey(resource.Kind, resource.Id)).ToHashSet();
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
        ProjectHome.Graph.RefreshReferencedStories();
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
            IsExternal = true,
        };

    private static string ResourceIdLabel(OfflineProviderResource resource)
    {
        using var json = System.Text.Json.JsonDocument.Parse(resource.DefinitionJson);
        if (json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "collective") return "Group ID";
        return resource.Kind.ToString() switch { "Actor" => "NPC ID", "Item" => "Item ID", "ItemGroup" => "Group ID", "Session" => "Session ID", "Task" => "Task ID", "Story" => "Story ID", _ => "资源 ID" };
    }

    internal static string ProviderDescription(OfflineProviderResource? resource)
        => resource is null ? "当前项目的引用资源；请在所属故事中编辑，或导入为本地资源。" : $"来源故事包：{Path.GetFileName(resource.PackagePath)}";

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

    private IReadOnlyList<OfflineResourceChoice> ResourceReferenceChoices()
    {
        var store = _canonicalGraphStore!;
        var native = NativeResourceChoices().ToDictionary(choice => new DgrResourceKey(Enum.Parse<DgrResourceKind>(choice.Kind), choice.Id));
        var choices = new List<OfflineResourceChoice>();
        var order = ProjectHome.Graph.Presentation.NavigationOrder;
        int Rank(StoryListItemViewModel story) { var index = Array.IndexOf(order, story.NavigationKey); return index < 0 ? int.MaxValue : index; }
        foreach (var story in ProjectHome.Stories.Where(story => story.HasCanonicalStory).OrderBy(Rank).ThenBy(story => story.NavigationKey, StringComparer.Ordinal).ThenBy(story => story.DisplayName, StringComparer.CurrentCulture))
        {
            var membership = store.Memberships.Load(story.Id);
            foreach (var key in MembershipKeys(membership.OwnedResources))
                if (native.TryGetValue(key, out var choice)) choices.Add(choice with { SourceStoryId = story.Id, SourceStoryName = story.DisplayName });
        }
        var providers = OfflineProviderCatalog.Load(ProjectDirectory);
        foreach (var package in providers.Providers)
        {
            var story = package.Resources.Single(resource => resource.Kind == DgrResourceKind.Story && resource.Id == package.Manifest.StoryId);
            var membership = package.Manifest.RequiredResources.CanonicalMemberships
                .Select(path => CanonicalStoryMembershipSerializer.Deserialize(System.Text.Encoding.UTF8.GetString(package.GetEntry(path))))
                .Single(member => member.StoryId == story.Id);
            var owned = MembershipKeys(membership.OwnedResources).ToHashSet();
            foreach (var resource in package.Resources.Where(resource => owned.Contains(new(resource.Kind, resource.Id))))
                choices.Add(ToOfflineChoice(resource) with { SourceStoryId = story.Id, SourceStoryName = story.DisplayName });
        }
        return choices;
    }

    private void PickOfflineResourceReference(string? storyId)
    {
        if (storyId is null || _canonicalGraphStore is null || !HasProject) return;
        OfflineResourceChoice? choice = null;
        try
        {
            var membership = _canonicalGraphStore.Memberships.Load(storyId);
            var present = MembershipKeys(membership.OwnedResources).Concat(MembershipKeys(membership.ReferencedResources)).ToHashSet();
            bool Offered(OfflineResourceChoice candidate) => Enum.TryParse<DgrResourceKind>(candidate.Kind, out var kind)
                && kind != DgrResourceKind.Story && !present.Contains(new(kind, candidate.Id));
            choice = _offlinePackageDialogs.PickResource(ResourceReferenceChoices().Where(Offered).ToArray(), "引用资源");
            if (choice is null) return;
            if (!ResourceReferenceChoices().Any(candidate => candidate.Kind == choice.Kind && candidate.Id == choice.Id
                    && candidate.SourceStoryId == choice.SourceStoryId && candidate.IsExternal == choice.IsExternal))
                throw new InvalidOperationException("所选资源的来源已变化，请重新选择。");
            var before = CaptureReferenceFiles(storyId);
            try
            {
                new CanonicalProjectResourceReferenceService(_canonicalGraphStore).AddReference(storyId,
                    Enum.Parse<DgrResourceKind>(choice.Kind), choice.Id, choice.SourceStoryId, choice.IsExternal);
                // Validate and apply the new workspace before publishing its Undo entry.
                RefreshOfflineWorkspace(storyId);
            }
            catch
            {
                var after = CaptureReferenceFiles(storyId);
                new Core.IO.ProjectFileTransaction().Apply(ProjectDirectory,
                    before.Select(file => new Core.IO.ProjectFileChange(file.Key, after[file.Key], file.Value)).ToArray(), () => { });
                throw;
            }
            RecordReferenceChange(before, storyId);
            ReportSuccess($"已引用“{choice.SourceStoryName}”中的“{choice.DisplayName}”。", "故事包");
        }
        catch (Exception exception)
        {
            // Keep machine identities in diagnostic logs; author-facing messages use names.
            var message = choice is null ? "无法读取引用目录，请检查故事或引用包是否有效。"
                : $"无法引用“{choice.SourceStoryName}”中的“{choice.DisplayName}”：{ReferenceFailureReason(exception)}";
            ReportFailure("引用资源", new InvalidOperationException(message, exception));
        }
    }
    private static string ReferenceFailureReason(Exception exception) => exception switch
    {
        CanonicalExternalReferenceException { Code: "story.external_reference.source.invalid" } => "来源故事包无效或存在冲突。",
        CanonicalExternalReferenceException { Code: "story.external_reference.duplicate" or "story.external_reference.owned" } => "该故事已拥有或引用此资源。",
        CanonicalStoryActorLifecycleException => "角色来源或成员关系已变化，请重新选择。",
        _ => "来源已失效、存在冲突或资源已被引用；请重新选择并检查问题列表。",
    };

    private static IEnumerable<DgrResourceKey> MembershipKeys(CanonicalStoryMembershipSet set)
        => set.Actors.Select(id => new DgrResourceKey(DgrResourceKind.Actor, id))
            .Concat(set.Items.Select(id => new DgrResourceKey(DgrResourceKind.Item, id)))
            .Concat(set.ItemGroups.Select(id => new DgrResourceKey(DgrResourceKind.ItemGroup, id)))
            .Concat(set.Sessions.Select(id => new DgrResourceKey(DgrResourceKind.Session, id)))
            .Concat(set.Tasks.Select(id => new DgrResourceKey(DgrResourceKind.Task, id)));
}
