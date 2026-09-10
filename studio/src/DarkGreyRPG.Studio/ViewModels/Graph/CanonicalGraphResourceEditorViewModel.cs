using System.ComponentModel;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>
/// Workspace-facing owner of one detached canonical resource document and the
/// shared graph host that edits it. Persistence remains an explicit snapshot
/// operation owned by the future repository layer.
/// </summary>
public sealed class CanonicalGraphResourceEditorViewModel : ObservableObject,
    IWorkspaceEditorViewModel, IDisposable
{
    private string _savedJson;
    private IReadOnlyDictionary<string, GraphEditorNodePosition> _savedLayout;
    private bool _disposed;

    public CanonicalGraphResourceEditorViewModel(
        GraphResourceEnvelope envelope,
        IReadOnlyDictionary<string, GraphEditorNodePosition>? layout = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var scope = GraphResourceScopeAdapter.GetScope(envelope.ResourceKind);
        Document = GraphResourceScopeAdapter.OpenDocument(envelope, scope);
        _savedJson = SerializeCurrent();
        if (scope == GraphScope.Task)
            CanonicalTaskObjectiveSchema.NormalizeLegacyInteractRequired(Document.Graph);
        Host = new GraphEditorHostViewModel(Document.Graph, Document.Scope, layout);
        _savedLayout = CreateLayoutSnapshot();
        UndoCommand = new RelayCommand(() => Host.Undo(), () => Host.CanUndo);
        RedoCommand = new RelayCommand(() => Host.Redo(), () => Host.CanRedo);
        Host.GraphChanged += OnGraphChanged;
        Host.LayoutChanged += OnLayoutChanged;
        Host.PropertyChanged += OnHostPropertyChanged;
    }

    public GraphResourceDocument Document { get; }
    public GraphEditorHostViewModel Host { get; }
    public GraphViewportState ViewportState { get; } = new();
    public string Id => Document.Id;
    public string DisplayName => Document.DisplayName;
    public IReadOnlyList<string> Tags => Document.Tags;
    public GraphResourceKind ResourceKind => Document.ResourceKind;
    public GraphScope Scope => Document.Scope;
    public long GraphRevision => Host.GraphRevision;
    public bool IsGraphDirty => !string.Equals(_savedJson, SerializeCurrent(), StringComparison.Ordinal);
    public bool IsLayoutDirty => !LayoutEquals(_savedLayout, CreateLayoutSnapshot());
    public bool IsDirty => IsGraphDirty || IsLayoutDirty;
    public bool CanSave => IsLayoutDirty || (IsGraphDirty && ValidationIssues.Count == 0);
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public IReadOnlyList<ValidationIssue> ValidationIssues => Host.LastValidationIssues;
    public string ValidationText => string.Join(Environment.NewLine,
        ValidationIssues.Select(ValidationIssuePresentation.FormatCompact));
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }

    /// <summary>Returns a detached envelope containing the current editor graph.</summary>
    public GraphResourceEnvelope CreatePersistenceSnapshot() => Document.ToEnvelope();

    public GraphResourceEnvelope CreateSnapshot() => CreatePersistenceSnapshot();

    /// <summary>Returns finite host-only positions for nodes currently present in this graph.</summary>
    public IReadOnlyDictionary<string, GraphEditorNodePosition> CreateLayoutSnapshot()
    {
        var liveNodeIds = (Document.Graph.Nodes ?? [])
            .Where(node => node is not null && !string.IsNullOrWhiteSpace(node.Id))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        return Host.Layout
            .Where(pair => liveNodeIds.Contains(pair.Key) && pair.Value.IsFinite)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Advances the saved baseline only after an external repository has
    /// durably accepted the supplied/current snapshot.
    /// </summary>
    public void MarkSaved()
    {
        ThrowIfDisposed();
        _savedJson = SerializeCurrent();
        _savedLayout = CreateLayoutSnapshot();
        NotifyWorkspaceState();
    }

    public void MarkGraphSaved()
    {
        ThrowIfDisposed();
        _savedJson = SerializeCurrent();
        NotifyWorkspaceState();
    }

    public void MarkLayoutSaved()
    {
        ThrowIfDisposed();
        _savedLayout = CreateLayoutSnapshot();
        NotifyWorkspaceState();
    }

    /// <summary>Applies an already-persisted display-name change without consuming graph dirtiness.</summary>
    public void ApplyPersistedDisplayName(string displayName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        var normalized = displayName.Trim();
        if (string.Equals(DisplayName, normalized, StringComparison.Ordinal)) return;
        var savedEnvelope = GraphResourceEnvelopeSerializer.Deserialize(_savedJson);
        savedEnvelope.DisplayName = normalized;
        _savedJson = GraphResourceEnvelopeSerializer.Serialize(savedEnvelope, indented: false);
        Document.SetDisplayName(normalized);
        OnPropertyChanged(nameof(DisplayName));
        NotifyWorkspaceState();
    }

    /// <summary>
    /// Applies a repository-committed replacement only when its normalized
    /// contents differ from this editor's saved baseline. Unrelated membership
    /// refreshes therefore preserve current graph edits and undo history.
    /// </summary>
    public void ApplyPersistedTags(IReadOnlyList<string> tags)
    {
        ThrowIfDisposed();
        var savedEnvelope = GraphResourceEnvelopeSerializer.Deserialize(_savedJson);
        savedEnvelope.Tags = tags.ToArray();
        _savedJson = GraphResourceEnvelopeSerializer.Serialize(savedEnvelope, indented: false);
        Document.SetTags(tags);
        OnPropertyChanged(nameof(Tags));
        NotifyWorkspaceState();
    }

    public bool ApplyPersistedSnapshot(GraphResourceEnvelope envelope)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.ResourceKind != ResourceKind || !string.Equals(envelope.Id, Id, StringComparison.Ordinal))
            throw new ArgumentException("Persisted snapshot identity must match the open editor.", nameof(envelope));

        var normalized = GraphResourceEnvelopeSerializer.Serialize(envelope, indented: false);
        if (string.Equals(normalized, _savedJson, StringComparison.Ordinal)) return false;
        if (IsDirty)
            throw new InvalidOperationException("A changed persisted snapshot cannot replace unsaved graph edits.");

        var graph = GraphResourceScopeAdapter.Open(envelope, Scope);
        if (Scope == GraphScope.Task)
            CanonicalTaskObjectiveSchema.NormalizeLegacyInteractRequired(graph);
        Document.SetDisplayName(envelope.DisplayName);
        Document.SetTags(envelope.Tags);
        OnPropertyChanged(nameof(Tags));
        Host.ApplyPersistedSnapshot(graph);
        _savedJson = SerializeCurrent();
        OnPropertyChanged(nameof(DisplayName));
        NotifyWorkspaceState();
        return true;
    }

    /// <summary>Reprojects direct document changes and refreshes workspace state.</summary>
    public void RefreshFromDocument()
    {
        ThrowIfDisposed();
        Host.Refresh();
        NotifyWorkspaceState();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Host.GraphChanged -= OnGraphChanged;
        Host.LayoutChanged -= OnLayoutChanged;
        Host.PropertyChanged -= OnHostPropertyChanged;
    }

    private void OnGraphChanged(object? sender, EventArgs args) => NotifyWorkspaceState();

    private void OnLayoutChanged(object? sender, EventArgs args) => NotifyWorkspaceState();

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(GraphEditorHostViewModel.CanUndo)
            or nameof(GraphEditorHostViewModel.CanRedo)
            or nameof(GraphEditorHostViewModel.LastValidationIssues))
            NotifyWorkspaceState();
    }

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(GraphRevision));
        OnPropertyChanged(nameof(IsGraphDirty));
        OnPropertyChanged(nameof(IsLayoutDirty));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveStateText));
        OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(ValidationText));
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private string SerializeCurrent()
        => GraphResourceEnvelopeSerializer.Serialize(Document.ToEnvelope(), indented: false);

    private static bool LayoutEquals(
        IReadOnlyDictionary<string, GraphEditorNodePosition> left,
        IReadOnlyDictionary<string, GraphEditorNodePosition> right)
        => left.Count == right.Count
           && left.All(pair => right.TryGetValue(pair.Key, out var position) && position == pair.Value);

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
