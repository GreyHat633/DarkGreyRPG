using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>
/// Coordinates replacement of one canonical graph resource from its editor.
/// The editor remains detached from repository state; a clean editor is an
/// explicit no-op and returns a fresh persistence snapshot without touching
/// disk.
/// </summary>
public sealed class CanonicalGraphResourceSaveCoordinator
{
    private readonly CanonicalProjectGraphStore _store;

    public CanonicalGraphResourceSaveCoordinator(CanonicalProjectGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CanonicalProjectGraphStore Store => _store;

    /// <summary>
    /// Replaces the editor's matching canonical resource when dirty. For a
    /// clean editor this performs no repository call and returns a detached
    /// snapshot. The editor is marked saved only after Replace and its reload
    /// both succeed; repository failures propagate unchanged.
    /// </summary>
    public GraphResourceEnvelope Replace(CanonicalGraphResourceEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var snapshot = editor.CreatePersistenceSnapshot();
        if (!editor.IsDirty)
            return snapshot;

        if (snapshot.ResourceKind == GraphResourceKind.Story)
            _store.StoryLogicGraph.ValidateStoryReplacement(snapshot);
        var persisted = RepositoryFor(snapshot.ResourceKind).Replace(snapshot);
        editor.MarkSaved();
        return persisted;
    }

    private GraphResourceRepository RepositoryFor(GraphResourceKind kind)
        => kind switch
        {
            GraphResourceKind.Story => _store.Stories,
            GraphResourceKind.Session => _store.Sessions,
            GraphResourceKind.Task => _store.Tasks,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported canonical graph resource kind."),
        };
}
