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
    private bool _disposed;

    public CanonicalGraphResourceEditorViewModel(GraphResourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var scope = GraphResourceScopeAdapter.GetScope(envelope.ResourceKind);
        Document = GraphResourceScopeAdapter.OpenDocument(envelope, scope);
        Host = new GraphEditorHostViewModel(Document.Graph, Document.Scope);
        _savedJson = SerializeCurrent();
        UndoCommand = new RelayCommand(() => Host.Undo(), () => Host.CanUndo);
        RedoCommand = new RelayCommand(() => Host.Redo(), () => Host.CanRedo);
        Host.GraphChanged += OnGraphChanged;
        Host.PropertyChanged += OnHostPropertyChanged;
    }

    public GraphResourceDocument Document { get; }
    public GraphEditorHostViewModel Host { get; }
    public string Id => Document.Id;
    public string DisplayName => Document.DisplayName;
    public GraphResourceKind ResourceKind => Document.ResourceKind;
    public GraphScope Scope => Document.Scope;
    public long GraphRevision => Host.GraphRevision;
    public bool IsDirty => !string.Equals(_savedJson, SerializeCurrent(), StringComparison.Ordinal);
    public bool CanSave => IsDirty && ValidationIssues.Count == 0;
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public IReadOnlyList<ValidationIssue> ValidationIssues => Host.LastValidationIssues;
    public string ValidationText => string.Join(Environment.NewLine,
        ValidationIssues.Select(ValidationIssuePresentation.Format));
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }

    /// <summary>Returns a detached envelope containing the current editor graph.</summary>
    public GraphResourceEnvelope CreatePersistenceSnapshot() => Document.ToEnvelope();

    public GraphResourceEnvelope CreateSnapshot() => CreatePersistenceSnapshot();

    /// <summary>
    /// Advances the saved baseline only after an external repository has
    /// durably accepted the supplied/current snapshot.
    /// </summary>
    public void MarkSaved()
    {
        ThrowIfDisposed();
        _savedJson = SerializeCurrent();
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
        Host.PropertyChanged -= OnHostPropertyChanged;
    }

    private void OnGraphChanged(object? sender, EventArgs args) => NotifyWorkspaceState();

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

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
