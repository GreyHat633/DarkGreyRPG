using System.ComponentModel;
using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ActorEditorViewModel : ObservableObject, IWorkspaceEditorViewModel
{
    private readonly Stack<EditorSnapshot> _undoHistory = [];
    private readonly Stack<EditorSnapshot> _redoHistory = [];
    private string _tagsText;
    private EditorSnapshot _lastSnapshot;
    private bool _isApplyingEdit;
    private bool _isRestoringSnapshot;
    private bool _isReadOnly;

    public ActorEditorViewModel(ActorDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _tagsText = string.Join(", ", document.Tags);
        _lastSnapshot = CaptureSnapshot();
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        Document.PropertyChanged += OnDocumentPropertyChanged;
    }

    public ActorDocument Document { get; }
    public Func<string, byte[]?>? PortraitPreviewData { get; set; }

    public bool SupportsPortraits => Document.SupportsPortraits;

    /// <summary>
    /// The inspector can use this for package or cross-story read-only views.
    /// Keeping the flag on the editor prevents a control-level disable from
    /// accidentally allowing a command or a future caller to mutate data.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (!SetProperty(ref _isReadOnly, value)) return;
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(CanSave));
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Returns true when a portrait variant is currently referenced by a line.</summary>
    public Func<ActorPortraitVariant, bool>? IsPortraitVariantReferenced { get; set; }

    public string PortraitEditError { get; private set; } = string.Empty;

    public string Id => Document.Id;

    public string DisplayName
    {
        get => Document.DisplayName;
        set
        {
            var next = value ?? string.Empty;
            if (IsReadOnly || string.Equals(Document.DisplayName, next, StringComparison.Ordinal))
            {
                return;
            }

            ApplyEdit(() => Document.DisplayName = next);
        }
    }

    public string? DefaultPortraitRef
    {
        get => Document.DefaultPortraitRef;
        set { if (SupportsPortraits && !IsReadOnly) ApplyEdit(() => Document.DefaultPortraitRef = value); }
    }

    private string _portraitVariantName = string.Empty;
    private ActorPortraitVariant? _selectedPortraitVariant;
    public string PortraitVariantName { get => _portraitVariantName; set => SetProperty(ref _portraitVariantName, value); }
    public ActorPortraitVariant? SelectedPortraitVariant { get => _selectedPortraitVariant; set => SetProperty(ref _selectedPortraitVariant, value); }
    public string DefaultPortraitStatus => DefaultPortraitRef is null ? "未配置默认头像" : "已配置默认头像";
    public IReadOnlyList<ActorPortraitVariant> PortraitVariants => Document.PortraitVariants;
    public void SetPortraitVariants(IEnumerable<ActorPortraitVariant> values)
    {
        if (SupportsPortraits && !IsReadOnly) ApplyEdit(() => Document.SetPortraitVariants(values));
    }

    public bool TryRenamePortraitVariant(ActorPortraitVariant variant, string? proposedName)
    {
        ClearPortraitEditError();
        var name = proposedName?.Trim() ?? string.Empty;
        if (!SupportsPortraits || IsReadOnly)
            return FailPortraitEdit("当前角色只读，无法修改表情差分。");
        if (!Document.PortraitVariants.Contains(variant))
            return FailPortraitEdit("选中的表情差分已不存在，请重新选择。");
        if (string.IsNullOrWhiteSpace(name))
            return FailPortraitEdit("表情差分名称不能为空。");
        if (Document.PortraitVariants.Any(value => !Equals(value, variant) && string.Equals(value.Name, name, StringComparison.Ordinal)))
            return FailPortraitEdit("表情差分名称不能重复。");
        if (IsPortraitVariantReferenced?.Invoke(variant) == true)
            return FailPortraitEdit("该表情差分仍被台词引用；请先解除引用或使用完整的引用同步操作。");

        ApplyEdit(() => Document.SetPortraitVariants(Document.PortraitVariants.Select(value =>
            Equals(value, variant) ? value with { Name = name } : value)));
        SelectedPortraitVariant = Document.PortraitVariants.First(value => value.Name == name);
        PortraitVariantName = name;
        return true;
    }

    public bool TryRemovePortraitVariant(ActorPortraitVariant variant)
    {
        ClearPortraitEditError();
        if (!SupportsPortraits || IsReadOnly)
            return FailPortraitEdit("当前角色只读，无法修改表情差分。");
        if (!Document.PortraitVariants.Contains(variant))
            return FailPortraitEdit("选中的表情差分已不存在，请重新选择。");
        if (IsPortraitVariantReferenced?.Invoke(variant) == true)
            return FailPortraitEdit("该表情差分仍被台词引用；请先解除引用，避免台词静默显示其他头像。");

        ApplyEdit(() => Document.SetPortraitVariants(Document.PortraitVariants.Where(value => !Equals(value, variant))));
        if (Equals(SelectedPortraitVariant, variant)) SelectedPortraitVariant = null;
        return true;
    }

    private bool FailPortraitEdit(string message)
    {
        PortraitEditError = message;
        OnPropertyChanged(nameof(PortraitEditError));
        return false;
    }

    private void ClearPortraitEditError()
    {
        if (PortraitEditError.Length == 0) return;
        PortraitEditError = string.Empty;
        OnPropertyChanged(nameof(PortraitEditError));
    }

    public string Notes
    {
        get => Document.Notes;
        set
        {
            var next = value ?? string.Empty;
            if (IsReadOnly || string.Equals(Document.Notes, next, StringComparison.Ordinal))
            {
                return;
            }

            ApplyEdit(() => Document.Notes = next);
        }
    }

    public string TagsText
    {
        get => _tagsText;
        set
        {
            var next = value ?? string.Empty;
            if (IsReadOnly || string.Equals(_tagsText, next, StringComparison.Ordinal))
            {
                return;
            }

            ApplyEdit(() =>
            {
                SetProperty(ref _tagsText, next);
                Document.SetTags(ParseTags(next));
            });
        }
    }

    public RelayCommand UndoCommand { get; }

    public RelayCommand RedoCommand { get; }

    public bool CanUndo => !IsReadOnly && _undoHistory.Count > 0;

    public bool CanRedo => !IsReadOnly && _redoHistory.Count > 0;

    public bool CanSave => !IsReadOnly && Document.IsDirty && Document.ValidationErrors.Count == 0;

    public bool IsDirty => Document.IsDirty;

    public IReadOnlyList<DarkGreyRPG.Studio.Core.Validation.ValidationIssue> ValidationIssues =>
        Document.ValidationIssues;

    public string SaveStateText => Document.IsDirty ? "未保存" : "已保存";

    public string ValidationText => string.Join(
        Environment.NewLine,
        Document.ValidationErrors.Select(issue => issue.Message));

    private void Undo()
    {
        if (IsReadOnly) return;
        if (!_undoHistory.TryPop(out var target))
        {
            return;
        }

        _redoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(target);
        NotifyHistoryChanged();
    }

    private void Redo()
    {
        if (IsReadOnly) return;
        if (!_redoHistory.TryPop(out var target))
        {
            return;
        }

        _undoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(target);
        NotifyHistoryChanged();
    }

    private void ApplyEdit(Action edit)
    {
        var before = CaptureSnapshot();
        _isApplyingEdit = true;
        try
        {
            edit();
        }
        finally
        {
            _isApplyingEdit = false;
        }

        var after = CaptureSnapshot();
        _lastSnapshot = after;
        if (SnapshotsEqual(before, after))
        {
            return;
        }

        _undoHistory.Push(before);
        _redoHistory.Clear();
        NotifyHistoryChanged();
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        _isRestoringSnapshot = true;
        try
        {
            Document.DisplayName = snapshot.DisplayName;
            Document.Notes = snapshot.Notes;
            Document.SetTags(snapshot.Tags);
            Document.DefaultPortraitRef = snapshot.DefaultPortraitRef;
            Document.SetPortraitVariants(snapshot.PortraitVariants);

            if (!string.Equals(_tagsText, snapshot.TagsText, StringComparison.Ordinal))
            {
                _tagsText = snapshot.TagsText;
                OnPropertyChanged(nameof(TagsText));
            }
        }
        finally
        {
            _isRestoringSnapshot = false;
        }

        _lastSnapshot = CaptureSnapshot();
    }

    private EditorSnapshot CaptureSnapshot() => new(
        Document.DisplayName,
        Document.Notes,
        _tagsText,
        [.. Document.Tags], Document.DefaultPortraitRef, [.. Document.PortraitVariants]);

    private static bool SnapshotsEqual(EditorSnapshot left, EditorSnapshot right) =>
        string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
        string.Equals(left.Notes, right.Notes, StringComparison.Ordinal) &&
        string.Equals(left.TagsText, right.TagsText, StringComparison.Ordinal) &&
        left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal) &&
        left.DefaultPortraitRef == right.DefaultPortraitRef && left.PortraitVariants.SequenceEqual(right.PortraitVariants);

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private static IEnumerable<string> ParseTags(string value) => value.Split(
        [',', '，', ';', '；', '\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ActorDocument.Tags) &&
            !_isApplyingEdit &&
            !_isRestoringSnapshot)
        {
            var tagsText = string.Join(", ", Document.Tags);
            if (!string.Equals(_tagsText, tagsText, StringComparison.Ordinal))
            {
                _tagsText = tagsText;
                OnPropertyChanged(nameof(TagsText));
            }
        }

        var currentSnapshot = CaptureSnapshot();
        if (!_isApplyingEdit &&
            !_isRestoringSnapshot &&
            !SnapshotsEqual(_lastSnapshot, currentSnapshot))
        {
            _undoHistory.Push(_lastSnapshot);
            _redoHistory.Clear();
            NotifyHistoryChanged();
        }

        _lastSnapshot = currentSnapshot;
        if (!string.IsNullOrWhiteSpace(eventArgs.PropertyName))
        {
            OnPropertyChanged(eventArgs.PropertyName);
        }

        OnPropertyChanged(nameof(DefaultPortraitStatus));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveStateText));
        OnPropertyChanged(nameof(ValidationText));
    }

    private sealed record EditorSnapshot(
        string DisplayName,
        string Notes,
        string TagsText,
        IReadOnlyList<string> Tags, string? DefaultPortraitRef, IReadOnlyList<ActorPortraitVariant> PortraitVariants);
}
