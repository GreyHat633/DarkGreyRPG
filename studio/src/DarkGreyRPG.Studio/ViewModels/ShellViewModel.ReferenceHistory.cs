using System.IO;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private sealed record ReferenceHistory(string Project, long Sequence, IReadOnlyList<NamespaceFileChange> Changes);
    private readonly Stack<ReferenceHistory> _referenceUndo = [];
    private readonly Stack<ReferenceHistory> _referenceRedo = [];
    private long _referenceRedoEpoch;
    private bool IsCurrentReferenceHistory(ReferenceHistory entry) => HasProject
        && string.Equals(Path.GetFullPath(ProjectDirectory), entry.Project, StringComparison.OrdinalIgnoreCase);
    private bool CanUndoReference() => _referenceUndo.TryPeek(out var entry) && IsCurrentReferenceHistory(entry);
    private bool CanRedoReference() => _referenceRedo.TryPeek(out var entry) && IsCurrentReferenceHistory(entry)
        && _referenceRedoEpoch == EditHistoryClock.Current;

    private Dictionary<string, byte[]> CaptureReferenceFiles(string? storyId = null)
    {
        if (storyId is not null)
        {
            var path = _canonicalGraphStore!.Memberships.GetPath(storyId);
            return new(StringComparer.OrdinalIgnoreCase) { [Path.GetRelativePath(ProjectDirectory, path)] = File.ReadAllBytes(path) };
        }
        var directory = Path.Combine(ProjectDirectory, "references");
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.dgrs").ToDictionary(path => Path.GetRelativePath(ProjectDirectory, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    private void RecordReferenceChange(Dictionary<string, byte[]> before, string? storyId = null)
    {
        var after = CaptureReferenceFiles(storyId);
        var changes = before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).Select(path =>
            new NamespaceFileChange(path, before.GetValueOrDefault(path), after.GetValueOrDefault(path)))
            .Where(change => !(change.ExpectedBytes ?? []).SequenceEqual(change.DesiredBytes ?? [])).ToArray();
        if (changes.Length == 0) return;
        _referenceUndo.Push(new(Path.GetFullPath(ProjectDirectory), EditHistoryClock.Next(), changes));
        _referenceRedo.Clear();
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
    }

    private bool TryUndoReference()
    {
        if (!CanUndoReference()) return false;
        var entry = _referenceUndo.Peek();
        if (ActiveEditor is CanonicalGraphResourceEditorViewModel editor && editor.Host.UndoSequence > entry.Sequence) return false;
        try
        {
            new NamespaceFileTransaction().Apply(ProjectDirectory,
                entry.Changes.Select(change => new NamespaceFileChange(change.RelativePath, change.DesiredBytes, change.ExpectedBytes)).ToArray(), () => { });
            _referenceUndo.Pop();
            _referenceRedo.Push(entry);
            RefreshOfflineWorkspace();
            _referenceRedoEpoch = EditHistoryClock.Current;
            ReportSuccess("已撤销引用操作。", "引用");
        }
        catch (Exception exception) { ReportFailure("撤销引用操作", exception); }
        return true;
    }

    private GraphEditorHostViewModel? ActiveProjectGraphHost => !IsCanonicalStoryWorkspaceVisible && ActiveEditor is null && Navigation.SelectedItem?.Page == "Story" ? ProjectHome.Graph.CanonicalHost : null;

    private void RedoCurrent()
    {
        if (CanonicalStoryWorkspace?.InspectorPortraitEditor is { } portrait) { portrait.RedoCommand.Execute(null); return; }
        if (ActiveProjectGraphHost is { CanRedo: true } graph) { graph.Redo(); return; }
        if (CanRedoReference() && (ActiveEditor is not CanonicalGraphResourceEditorViewModel editor
            || editor.Host.RedoSequence > _referenceRedo.Peek().Sequence))
        {
            try
            {
                var entry = _referenceRedo.Peek();
                new NamespaceFileTransaction().Apply(ProjectDirectory, entry.Changes, () => { });
                _referenceRedo.Pop();
                _referenceUndo.Push(entry);
                RefreshOfflineWorkspace();
                ReportSuccess("已重做引用操作。", "引用");
            }
            catch (Exception exception) { ReportFailure("重做引用操作", exception); }
        }
        else ActiveEditor?.RedoCommand.Execute(null);
    }

    private bool TryRemoveIndependentReference(ICanonicalStoryTreeItem item)
    {
        DgrResourceKind? kind = item switch
        {
            CanonicalStoryActorItem { IsReferenced: true } => DgrResourceKind.Actor,
            CanonicalStoryItemItem { IsReferenced: true } resource => resource.Item is CollectiveItemResource ? DgrResourceKind.ItemGroup : DgrResourceKind.Item,
            CanonicalStoryGraphItem { IsReferenced: true } graph => graph.ResourceKind == GraphResourceKind.Session ? DgrResourceKind.Session : DgrResourceKind.Task,
            CanonicalStoryMissingItem { IsReferenced: true } missing => missing.FolderKind switch
            {
                CanonicalStoryFolderKind.Actors => DgrResourceKind.Actor,
                CanonicalStoryFolderKind.Items => missing.OrderHandle.StartsWith("item_group:", StringComparison.Ordinal) ? DgrResourceKind.ItemGroup : DgrResourceKind.Item,
                CanonicalStoryFolderKind.Sessions => DgrResourceKind.Session,
                _ => DgrResourceKind.Task,
            },
            _ => null,
        };
        if (kind is null) return false;
        try
        {
            var storyId = CanonicalStoryWorkspace!.StoryEditor.Id;
            var before = CaptureReferenceFiles(storyId);
            var membership = _canonicalGraphStore!.Memberships.Load(storyId);
            var referenced = membership.ReferencedResources;
            var members = kind switch
            {
                DgrResourceKind.Actor => referenced.Actors,
                DgrResourceKind.Item => referenced.Items,
                DgrResourceKind.ItemGroup => referenced.ItemGroups,
                DgrResourceKind.Session => referenced.Sessions,
                _ => referenced.Tasks,
            };
            if (!members.Remove(item.Id)) return true;
            // Membership is independent of both provider files and authored graph content.
            _canonicalGraphStore.Memberships.Replace(new CanonicalStoryMembershipManifest(membership.StoryId, membership.OwnedResources, referenced) { SchemaVersion = membership.SchemaVersion, DisplayOrder = membership.DisplayOrder });
            RecordReferenceChange(before, storyId);
            RefreshOfflineWorkspace();
            ReportSuccess($"已解除 {item.DisplayName} 的引用。", "引用");
        }
        catch (Exception exception) { ReportFailure("解除资源引用", exception); }
        return true;
    }
}
