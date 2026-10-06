using System.IO;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private bool DeleteStoryGroups(IReadOnlyCollection<string> keys)
    {
        if (_canonicalGraphStore is null || _projectService.CurrentProject is not { } project) return false;
        try
        {
            // The undo transaction captures the latest authored content, including drafts.
            if (HasUnsavedDocuments() && !TrySaveAll()) return false;
            var selected = ProjectHome.Graph.StoryGroups.Groups.Where(group => keys.Contains(group.Key)).ToArray();
            var members = selected.SelectMany(group => group.Members).ToHashSet(StringComparer.Ordinal);
            if (members.Count == 0) return false;
            var catalog = OfflineProviderCatalog.Load(ProjectDirectory);
            if (catalog.Diagnostics.Count != 0) throw new InvalidDataException(string.Join("；", catalog.Diagnostics.Select(d => d.Message)));
            var providers = catalog.Providers;
            var containers = providers.Where(provider => members.Contains(provider.Manifest.StoryId))
                .Select(provider => provider.PackagePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedProviders = providers.Where(provider => containers.Contains(provider.PackagePath)).ToArray();
            // A reference container is indivisible. Its complete member set is shown before committing.
            members.UnionWith(removedProviders.Select(provider => provider.Manifest.StoryId));
            var referencedIds = removedProviders.SelectMany(provider => provider.Resources).Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
            var local = members.Where(id => !ProjectHome.Graph.IsReferencedStory(id)).ToArray();
            foreach (var id in local)
                referencedIds.UnionWith(MembershipKeys(_canonicalGraphStore.Memberships.Load(id).OwnedResources).Select(key => key.Id));
            var changes = new CanonicalStoryLifecycleService(_canonicalGraphStore, project.Actors, project.Stories)
                .PlanGroupDeletion(local).ToList();
            var dependencies = new List<string>();
            foreach (var provider in providers.Where(provider => !containers.Contains(provider.PackagePath)))
            {
                foreach (var path in provider.Manifest.RequiredResources.CanonicalMemberships)
                {
                    var membership = CanonicalStoryMembershipSerializer.Deserialize(Encoding.UTF8.GetString(provider.GetEntry(path)));
                    foreach (var key in MembershipKeys(membership.ReferencedResources).Where(key => referencedIds.Contains(key.Id)))
                        dependencies.Add($"引用故事 {membership.StoryId}（{Path.GetFileName(provider.PackagePath)}）仍引用 {key.Id}");
                }
                var providerEdges = provider.ContainerConnections.Connections.AsEnumerable();
                if (provider.Manifest.RequiredResources.StoryLogicGraph is { } graphPath)
                {
                    using var json = JsonDocument.Parse(provider.GetEntry(graphPath));
                    providerEdges = providerEdges.Concat(CanonicalStoryLogicGraphRepository.Parse(json.RootElement).Connections);
                }
                foreach (var edge in providerEdges.Where(edge => members.Contains(edge.SourceStoryId) != members.Contains(edge.TargetStoryId)))
                    dependencies.Add($"引用故事连线：{edge.SourceStoryId} / {edge.SourcePortId} → {edge.TargetStoryId} / {edge.TargetPortId}");
            }
            foreach (var info in _canonicalGraphStore.Memberships.List().Where(info => !members.Contains(info.StoryId)))
            {
                var membership = _canonicalGraphStore.Memberships.Load(info.StoryId);
                foreach (var key in MembershipKeys(membership.ReferencedResources).Where(key => referencedIds.Contains(key.Id)))
                    dependencies.Add($"故事 {info.StoryId} 仍引用 {key.Id}");
            }
            var edges = _canonicalGraphStore.StoryLogicGraph.Load();
            foreach (var edge in edges.Connections.Where(edge => members.Contains(edge.SourceStoryId) != members.Contains(edge.TargetStoryId)))
                dependencies.Add($"故事连线：{edge.SourceStoryId} / {edge.SourcePortId} → {edge.TargetStoryId} / {edge.TargetPortId}");
            if (dependencies.Count != 0) { ReportWarning("无法删除故事组：\n" + string.Join("\n", dependencies), "故事组依赖"); return false; }
            foreach (var path in containers)
                changes.Add(new ProjectFileChange(Path.GetRelativePath(ProjectDirectory, path), File.ReadAllBytes(path), null));
            if (File.Exists(_canonicalGraphStore.StoryLogicGraph.Path))
            {
                var remaining = edges.Connections.Where(edge => !members.Contains(edge.SourceStoryId) && !members.Contains(edge.TargetStoryId)).ToArray();
                changes.Add(new ProjectFileChange(Path.GetRelativePath(ProjectDirectory, _canonicalGraphStore.StoryLogicGraph.Path),
                    File.ReadAllBytes(_canonicalGraphStore.StoryLogicGraph.Path), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                        new CanonicalStoryLogicGraph(2, remaining), new JsonSerializerOptions { WriteIndented = true }))));
            }
            var affected = changes.Select(change => change.RelativePath).ToArray();
            if (!_projectWorkspaceDialogs.ConfirmDeleteStoryGroups(string.Join("、", selected.Select(group => group.DisplayName)),
                $"删除 {local.Length} 个本地故事，解除 {containers.Count} 个完整来源容器的引用（外部原包保留）", affected)) return false;
            new ProjectFileTransaction().Apply(ProjectDirectory, changes, () => _canonicalGraphStore.StoryLogicGraph.Load());
            _referenceUndo.Push(new(Path.GetFullPath(ProjectDirectory), EditHistoryClock.Next(), changes, "故事组删除", local));
            _referenceRedo.Clear();
            ReleaseDeletedStoryWorkspaces(local);
            LoadActorList(); LoadStoryList();
            UndoCurrentCommand.RaiseCanExecuteChanged(); RedoCurrentCommand.RaiseCanExecuteChanged();
            ReportSuccess("故事组已删除，可撤销恢复。", "故事组");
            return true;
        }
        catch (Exception exception) { ReportFailure("删除故事组", exception); return false; }
    }

    private void ReleaseDeletedStoryWorkspaces(IReadOnlyCollection<string> storyIds)
    {
        foreach (var document in _projectService.OpenActorDocuments.ToArray())
            if (storyIds.Any(id => document.Id.StartsWith(id + "~", StringComparison.Ordinal)))
                _projectService.ReleaseOpenActor(document.Id, discardUnsavedChanges: true);
        foreach (var id in storyIds)
            if (_retainedStoryWorkspaces.Remove(id, out var draft)) draft.Dispose();
        if (_canonicalStoryWorkspace is { } workspace && storyIds.Contains(workspace.StoryEditor.Id))
        {
            workspace.PropertyChanged -= OnCanonicalStoryWorkspacePropertyChanged;
            workspace.Dispose();
            _canonicalStoryWorkspace = null;
            SetCanonicalStoryWorkspaceVisible(false);
            OnPropertyChanged(nameof(CanonicalStoryWorkspace));
            OnPropertyChanged(nameof(HasCanonicalStoryWorkspace));
            OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
            RaiseCurrentEditorStates();
        }
    }
}
