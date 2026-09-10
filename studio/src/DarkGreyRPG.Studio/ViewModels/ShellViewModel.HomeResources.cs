using System.IO;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record HomeStoryResourceRow(string StoryId, CanonicalStoryFolderKind FolderKind,
    string Id, string DisplayName, string IdentityText, bool IsReferenced, bool IsMissing)
{
    public string Label => (IsReferenced ? "[引用] " : string.Empty) + DisplayName;
    public string ToolTip => Label + "\n" + IdentityText;
}

public sealed record HomeStoryResourceFolder(string DisplayName, IReadOnlyList<HomeStoryResourceRow> Items)
{
    public bool HasResources => Items.Count > 0;
}

public sealed partial class ShellViewModel
{
    private readonly Dictionary<string, CanonicalStoryWorkspaceViewModel> _retainedStoryWorkspaces = new(StringComparer.Ordinal);
    public IReadOnlyList<HomeStoryResourceFolder> HomeResourceFolders { get; private set; } = [];

    private void RefreshHomeResourceFolders()
    {
        var storyId = ProjectHome.SelectedStory?.Id;
        CanonicalStoryWorkspaceViewModel? preview = null;
        try
        {
            var workspace = CanonicalStoryWorkspace?.StoryEditor.Id == storyId ? CanonicalStoryWorkspace
                : storyId is not null ? _retainedStoryWorkspaces.GetValueOrDefault(storyId) : null;
            if (workspace is null && storyId is not null && _canonicalGraphStore is { } store
                && File.Exists(store.Stories.GetPath(storyId)) && File.Exists(store.Memberships.GetPath(storyId)))
                workspace = preview = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(storyId));
            HomeResourceFolders = Enum.GetValues<CanonicalStoryFolderKind>().Select(kind =>
                new HomeStoryResourceFolder(kind switch
                {
                    CanonicalStoryFolderKind.Actors => "角色", CanonicalStoryFolderKind.Items => "物品",
                    CanonicalStoryFolderKind.Sessions => "会话", _ => "任务",
                }, workspace?.Folders.Single(folder => folder.Kind == kind).Items.Select(item =>
                    new HomeStoryResourceRow(storyId!, kind, item.Id, item.DisplayName, item.IdentityText,
                        item switch
                        {
                            CanonicalStoryActorItem actor => actor.IsReferenced,
                            CanonicalStoryItemItem resource => resource.IsReferenced,
                            CanonicalStoryGraphItem graph => graph.IsReferenced,
                            CanonicalStoryMissingItem missing => missing.IsReferenced, _ => false,
                        }, item is CanonicalStoryMissingItem)).ToArray() ?? [])).ToArray();
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            HomeResourceFolders = new[] { "角色", "物品", "会话", "任务" }
                .Select(label => new HomeStoryResourceFolder(label, [])).ToArray();
        }
        finally { preview?.Dispose(); }
        OnPropertyChanged(nameof(HomeResourceFolders));
    }

    public bool OpenHomeResource(HomeStoryResourceRow row)
    {
        if (row.IsMissing || row.StoryId != ProjectHome.SelectedStory?.Id
            || row.FolderKind is not (CanonicalStoryFolderKind.Sessions or CanonicalStoryFolderKind.Tasks)) return false;
        if (TryOpenCanonicalStory(row.StoryId) != CanonicalOpenResult.Opened) return false;
        var workspace = CanonicalStoryWorkspace!;
        var item = workspace.Folders.Single(folder => folder.Kind == row.FolderKind).Items
            .OfType<CanonicalStoryGraphItem>().FirstOrDefault(candidate => candidate.Id == row.Id);
        if (item is null) return false;
        workspace.SelectTreeItem(item);
        return workspace.OpenGraphResource(item);
    }
}
