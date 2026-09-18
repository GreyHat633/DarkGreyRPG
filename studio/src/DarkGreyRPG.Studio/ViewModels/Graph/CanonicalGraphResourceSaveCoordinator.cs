using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Identity;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

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
    private readonly CanonicalGraphLayoutStore _layoutStore;
    private static readonly ConditionalWeakTable<CanonicalGraphResourceEditorViewModel, List<CanonicalStoryLogicConnection>> RemovedBoundaryEdges = new();

    public CanonicalGraphResourceSaveCoordinator(
        CanonicalProjectGraphStore store,
        CanonicalGraphLayoutStore? layoutStore = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _layoutStore = layoutStore ?? new CanonicalGraphLayoutStore(store.ProjectDirectory);
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
        var graphDirty = editor.IsGraphDirty;
        var layoutDirty = editor.IsLayoutDirty;
        if (!graphDirty && !layoutDirty)
            return snapshot;

        var persisted = snapshot;
        if (graphDirty)
        {
            persisted = snapshot.ResourceKind == GraphResourceKind.Story
                ? ReplaceStoryAndBoundaryEdges(editor, snapshot)
                : RepositoryFor(snapshot.ResourceKind).Replace(snapshot);
            editor.MarkGraphSaved();
        }

        if (layoutDirty)
        {
            _layoutStore.Save(
                editor.ResourceKind,
                editor.Id,
                editor.CreateLayoutSnapshot().ToDictionary(
                    pair => pair.Key,
                    pair => new ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y },
                    StringComparer.Ordinal));
            _layoutStore.SaveFrames(editor.Host.AuthoringResourceKey, editor.Host.FrameSnapshot());
            editor.MarkLayoutSaved();
        }
        return persisted;
    }

    private GraphResourceEnvelope ReplaceStoryAndBoundaryEdges(CanonicalGraphResourceEditorViewModel editor, GraphResourceEnvelope snapshot)
    {
        var repository = _store.StoryLogicGraph;
        var current = repository.Load();
        var removed = RemovedBoundaryEdges.GetOrCreateValue(editor);
        bool Exists(CanonicalStoryLogicConnection edge)
            => (edge.SourceStoryId != snapshot.Id || CanonicalStoryLogicGraphRepository.HasBoundary(snapshot, edge.SourcePortId,
                    edge.InterfaceKind == "Flow" ? "terminate" : "logic_output"))
                && (edge.TargetStoryId != snapshot.Id || CanonicalStoryLogicGraphRepository.HasBoundary(snapshot, edge.TargetPortId,
                    edge.InterfaceKind == "Flow" ? "flow_driven" : "logic_input"));
        var dropped = current.Connections.Where(edge => !Exists(edge)).ToArray();
        var restored = removed.Where(Exists).ToArray();
        var next = new CanonicalStoryLogicGraph(2, current.Connections.Where(Exists).Concat(restored).Distinct().ToArray());
        repository.Validate(next, snapshot);
        if (dropped.Length == 0 && restored.Length == 0) return _store.Stories.Replace(snapshot);
        var storyPath = _store.Stories.GetPath(snapshot.Id);
        var changes = new[]
        {
            new NamespaceFileChange(Path.GetRelativePath(_store.ProjectDirectory, storyPath), File.ReadAllBytes(storyPath),
                Encoding.UTF8.GetBytes(GraphResourceEnvelopeSerializer.Serialize(snapshot))),
            new NamespaceFileChange(Path.GetRelativePath(_store.ProjectDirectory, repository.Path), File.Exists(repository.Path) ? File.ReadAllBytes(repository.Path) : null,
                JsonSerializer.SerializeToUtf8Bytes(next, new JsonSerializerOptions { WriteIndented = true }))
        };
        new NamespaceFileTransaction().Apply(_store.ProjectDirectory, changes, () => repository.Validate(next, snapshot));
        foreach (var edge in dropped) if (!removed.Contains(edge)) removed.Add(edge);
        foreach (var edge in restored) removed.Remove(edge);
        return _store.Stories.Load(snapshot.Id);
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
