using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private long _canonicalRedoEpoch;
    private long _workspaceHistorySequence;
    private long WorkspaceHistorySequence
    {
        get
        {
            var newest = CanonicalHistoryHosts().Select(host => host.LastEditSequence)
                .Concat(_referenceUndo.Select(entry => entry.Sequence)).Concat(_referenceRedo.Select(entry => entry.Sequence))
                .DefaultIfEmpty(0).Max();
            return _workspaceHistorySequence = Math.Max(_workspaceHistorySequence, newest);
        }
    }
    private IEnumerable<GraphEditorHostViewModel> CanonicalHistoryHosts()
    {
        if (!HasProject) yield break;
        if (ProjectHome.Graph.CanonicalHost is { } projectHost) yield return projectHost;
        var workspaces = _retainedStoryWorkspaces.Values.AsEnumerable();
        if (CanonicalStoryWorkspace is { } current) workspaces = workspaces.Append(current);
        foreach (var workspace in workspaces.Distinct())
            foreach (var editor in workspace.Editors.Where(workspace.IsWritableEditor)) yield return editor.Host;
    }
    private GraphEditorHostViewModel? LatestCanonicalUndoHost => CanonicalHistoryHosts()
        .Where(host => host.CanUndo).OrderByDescending(host => host.UndoSequence).FirstOrDefault();
    private GraphEditorHostViewModel? NextCanonicalRedoHost => _canonicalRedoEpoch != WorkspaceHistorySequence ? null
        : CanonicalHistoryHosts().Where(host => host.CanRedo).OrderBy(host => host.RedoSequence).FirstOrDefault();
    public RelayCommand AddExternalReferenceCommand { get; }
    public void AddExternalReference(string? storyId)
        => PickOfflineResourceReference(storyId);

    private void UndoCurrent()
    {
        if (CanonicalStoryWorkspace?.InspectorPortraitEditor is { } portrait) { portrait.UndoCommand.Execute(null); return; }
        if (LatestCanonicalUndoHost is { } graph
            && (!CanUndoReference() || graph.UndoSequence > _referenceUndo.Peek().Sequence))
        {
            if (graph.Undo()) _canonicalRedoEpoch = WorkspaceHistorySequence;
            RaiseCurrentEditorStates();
            return;
        }
        if (TryUndoReference()) { _canonicalRedoEpoch = WorkspaceHistorySequence; RaiseCurrentEditorStates(); return; }
        if (ActiveEditor?.UndoCommand.CanExecute(null) == true) ActiveEditor.UndoCommand.Execute(null);
    }

}
