using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record QuestCompletionModeOption(string Value, string Label);

public sealed class QuestEditorViewModel : ObservableObject, IWorkspaceEditorViewModel
{
    private static IReadOnlyList<QuestCompletionModeOption> CompletionModes { get; } =
    [
        new("ALL", "全部目标完成"),
        new("ANY", "任意一个目标完成"),
        new("SEQUENCE", "按顺序完成"),
    ];
    public IReadOnlyList<QuestCompletionModeOption> CompletionModeOptions => CompletionModes;
    private readonly Stack<QuestEditorSnapshot> _undoHistory = [];
    private readonly Stack<QuestEditorSnapshot> _redoHistory = [];
    private List<ObjectiveGroupResource> _groups;
    private bool _restoring;
    private string _displayName;
    private string _description;
    private string _notes;
    private string _tagsText;
    private string _pendingCompletionMode = "ALL";
    private QuestObjectiveEditorItem? _selectedObjective;

    public QuestEditorViewModel(QuestDocument document, IReadOnlyList<string>? availableActorIds = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        AvailableActorIds = availableActorIds ?? [];
        _displayName = document.DisplayName;
        _description = document.Description;
        _notes = document.Metadata.Notes;
        _tagsText = string.Join(", ", document.Metadata.Tags);
        _groups = document.ObjectiveGroups.Select(group => group.Clone()).ToList();
        _pendingCompletionMode = _groups.FirstOrDefault(group => group.Id == "main")?.Mode
            ?? _groups.FirstOrDefault()?.Mode
            ?? "ALL";
        ReplaceObjectiveEditors(document.Objectives);
        AddKillCommand = new RelayCommand(() => AddObjective("kill_entity"));
        AddCollectCommand = new RelayCommand(() => AddObjective("collect_item"));
        AddInteractCommand = new RelayCommand(() => AddObjective("interact_actor"));
        DeleteObjectiveCommand = new RelayCommand(DeleteObjective, () => SelectedObjective is not null);
        MoveObjectiveUpCommand = new RelayCommand(() => MoveSelectedObjective(-1), () => SelectedObjective is not null && Objectives.IndexOf(SelectedObjective) > 0);
        MoveObjectiveDownCommand = new RelayCommand(() => MoveSelectedObjective(1), () => SelectedObjective is not null && Objectives.IndexOf(SelectedObjective) is var index && index >= 0 && index < Objectives.Count - 1);
        UndoCommand = new RelayCommand(Undo, () => _undoHistory.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => _redoHistory.Count > 0);
        SelectedObjective = Objectives.FirstOrDefault();
        Document.PropertyChanged += (_, _) => RaiseDocumentState();
    }

    public QuestDocument Document { get; }
    public string Id => Document.Id;
    public IReadOnlyList<string> AvailableActorIds { get; }
    public ObservableCollection<QuestObjectiveEditorItem> Objectives { get; } = [];
    public IReadOnlyList<ObjectiveGroupResource> ObjectiveGroups => _groups;
    public RelayCommand AddKillCommand { get; }
    public RelayCommand AddCollectCommand { get; }
    public RelayCommand AddInteractCommand { get; }
    public RelayCommand DeleteObjectiveCommand { get; }
    public RelayCommand MoveObjectiveUpCommand { get; }
    public RelayCommand MoveObjectiveDownCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand AddFirstKillCommand => AddKillCommand;
    public RelayCommand AddFirstCollectCommand => AddCollectCommand;
    public RelayCommand AddFirstInteractCommand => AddInteractCommand;

    public string DisplayName
    {
        get => _displayName;
        set => ApplyEdit(() => SetProperty(ref _displayName, value ?? string.Empty));
    }
    public string Description
    {
        get => _description;
        set => ApplyEdit(() => SetProperty(ref _description, value ?? string.Empty));
    }
    public string Notes
    {
        get => _notes;
        set => ApplyEdit(() => SetProperty(ref _notes, value ?? string.Empty));
    }
    public string TagsText
    {
        get => _tagsText;
        set => ApplyEdit(() => SetProperty(ref _tagsText, value ?? string.Empty));
    }

    public QuestObjectiveEditorItem? SelectedObjective
    {
        get => _selectedObjective;
        set
        {
            if (!SetProperty(ref _selectedObjective, value)) return;
            RaiseObjectiveCommandStates();
        }
    }

    public string GroupSummary => string.Join(", ", _groups.Select(group => $"{group.Id} ({group.Mode})"));
    public bool HasObjectives => Objectives.Count > 0;
    public bool IsEmptyState => !HasObjectives;
    public bool IsStarterEmptyState => IsEmptyState;
    public string CompletionMode
    {
        get => MainGroup?.Mode ?? _pendingCompletionMode;
        set
        {
            var mode = CompletionModes.Any(option => option.Value == value) ? value : "ALL";
            _pendingCompletionMode = mode;
            if (Objectives.Count == 0 && _groups.Count == 0)
            {
                if (string.Equals(_pendingCompletionMode, mode, StringComparison.Ordinal)) return;
                _pendingCompletionMode = mode;
                OnPropertyChanged(nameof(CompletionMode));
                OnPropertyChanged(nameof(CompletionModeLabel));
                return;
            }
            ApplyEdit(() =>
            {
                var group = EnsureDefaultGroup();
                var index = _groups.IndexOf(group);
                _groups[index] = new ObjectiveGroupResource { Id = group.Id, Mode = mode, Objectives = group.Objectives.ToList() };
                OnPropertyChanged(nameof(CompletionMode));
                OnPropertyChanged(nameof(CompletionModeLabel));
            });
        }
    }
    public string CompletionModeLabel => CompletionModes.First(option => option.Value == CompletionMode).Label;
    public bool IsDirty => Document.IsDirty;
    public bool CanSave => IsDirty && Document.ValidationErrors.Count == 0;
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public string ValidationText => string.Join(Environment.NewLine, Document.ValidationErrors.Select(issue => issue.Message));
    public IReadOnlyList<ValidationIssue> ValidationIssues => Document.ValidationIssues;

    private void AddObjective(string type)
    {
        ApplyEdit(() =>
        {
            var id = AllocateObjectiveId(type switch
            {
                "kill_entity" => "kill",
                "collect_item" => "collect",
                _ => "interact",
            });
            var resource = type switch
            {
                "kill_entity" => QuestObjectiveResource.Kill(id, "消灭史莱姆", "minecraft:slime", 10),
                "collect_item" => QuestObjectiveResource.Collect(id, "收集史莱姆凝胶", "modid:slime_gel", 0, 5),
                _ => QuestObjectiveResource.Interact(id, "向角色复命", AvailableActorIds.FirstOrDefault() ?? string.Empty, 1),
            };
            var item = CreateObjectiveEditor(resource);
            Objectives.Add(item);
            EnsureDefaultGroup().Objectives.Add(id);
            SelectedObjective = item;
            OnPropertyChanged(nameof(HasObjectives));
            OnPropertyChanged(nameof(IsEmptyState));
            OnPropertyChanged(nameof(IsStarterEmptyState));
            OnPropertyChanged(nameof(GroupSummary));
        });
    }

    private void DeleteObjective()
    {
        if (SelectedObjective is null) return;
        ApplyEdit(() =>
        {
            var index = Objectives.IndexOf(SelectedObjective);
            var id = SelectedObjective.Id;
            Objectives.Remove(SelectedObjective);
            foreach (var group in _groups) group.Objectives.RemoveAll(value => value == id);
            _groups.RemoveAll(group => group.Objectives.Count == 0);
            SelectedObjective = Objectives.Count == 0 ? null : Objectives[Math.Clamp(index, 0, Objectives.Count - 1)];
            OnPropertyChanged(nameof(HasObjectives));
            OnPropertyChanged(nameof(IsEmptyState));
            OnPropertyChanged(nameof(IsStarterEmptyState));
            OnPropertyChanged(nameof(GroupSummary));
        });
    }

    private void MoveSelectedObjective(int offset)
    {
        if (SelectedObjective is null) return;
        var source = Objectives.IndexOf(SelectedObjective);
        var target = source + offset;
        if (source < 0 || target < 0 || target >= Objectives.Count) return;
        ApplyEdit(() => Objectives.Move(source, target));
        RaiseObjectiveCommandStates();
    }

    private void ApplyEdit(Action edit)
    {
        if (_restoring)
        {
            edit();
            return;
        }
        var before = CaptureSnapshot();
        edit();
        CommitToDocument();
        var after = CaptureSnapshot();
        if (SnapshotsEqual(before, after)) return;
        _undoHistory.Push(before);
        _redoHistory.Clear();
        RaiseHistoryStates();
    }

    private void Undo()
    {
        if (!_undoHistory.TryPop(out var target)) return;
        _redoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(target);
        RaiseHistoryStates();
    }

    private void Redo()
    {
        if (!_redoHistory.TryPop(out var target)) return;
        _undoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(target);
        RaiseHistoryStates();
    }

    private void RestoreSnapshot(QuestEditorSnapshot snapshot)
    {
        _restoring = true;
        try
        {
            _displayName = snapshot.Resource.DisplayName;
            _description = snapshot.Resource.Description;
            _notes = snapshot.Resource.Metadata.Notes;
            _tagsText = string.Join(", ", snapshot.Resource.Metadata.Tags);
            _groups = snapshot.Resource.ObjectiveGroups.Select(group => group.Clone()).ToList();
            _pendingCompletionMode = _groups.FirstOrDefault(group => group.Id == "main")?.Mode
                ?? _groups.FirstOrDefault()?.Mode
                ?? "ALL";
            ReplaceObjectiveEditors(snapshot.Resource.Objectives);
            SelectedObjective = Objectives.FirstOrDefault(item => item.Id == snapshot.SelectedObjectiveId) ?? Objectives.FirstOrDefault();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(TagsText));
            OnPropertyChanged(nameof(GroupSummary));
            OnPropertyChanged(nameof(CompletionMode));
            OnPropertyChanged(nameof(CompletionModeLabel));
            OnPropertyChanged(nameof(HasObjectives));
            OnPropertyChanged(nameof(IsEmptyState));
            OnPropertyChanged(nameof(IsStarterEmptyState));
            CommitToDocument();
        }
        finally
        {
            _restoring = false;
        }
    }

    private QuestEditorSnapshot CaptureSnapshot() => new(BuildResource(), SelectedObjective?.Id);
    private QuestResource BuildResource() => new()
    {
        SchemaVersion = QuestResource.CurrentSchemaVersion,
        Id = Document.Id,
        Title = _displayName,
        DisplayName = _displayName,
        Description = _description,
        HomeStoryId = Document.HomeStoryId,
        Objectives = Objectives.Select(item => item.ToResource()).ToList(),
        ObjectiveGroups = _groups.Select(group => group.Clone()).ToList(),
        Metadata = new QuestMetadata { Notes = _notes, Tags = ParseTags(_tagsText).ToList() },
    };

    private void CommitToDocument()
    {
        var resource = BuildResource();
        Document.Title = resource.Title;
        Document.DisplayName = resource.DisplayName;
        Document.Description = resource.Description;
        Document.Metadata = resource.Metadata;
        Document.ReplaceObjectives(resource.Objectives);
        Document.ReplaceGroups(resource.ObjectiveGroups);
        RaiseDocumentState();
    }

    private void ReplaceObjectiveEditors(IEnumerable<QuestObjectiveResource> values)
    {
        Objectives.Clear();
        foreach (var value in values) Objectives.Add(CreateObjectiveEditor(value));
    }

    private QuestObjectiveEditorItem CreateObjectiveEditor(QuestObjectiveResource value) => new(value, ApplyEdit);
    private ObjectiveGroupResource EnsureDefaultGroup()
    {
        if (_groups.Count == 0) _groups.Add(new ObjectiveGroupResource { Id = "main", Mode = _pendingCompletionMode, Objectives = [] });
        return _groups[0];
    }

    private ObjectiveGroupResource? MainGroup => _groups.FirstOrDefault(group => group.Id == "main") ?? _groups.FirstOrDefault();

    private string AllocateObjectiveId(string baseId)
    {
        if (Objectives.All(item => item.Id != baseId)) return baseId;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (Objectives.All(item => item.Id != candidate)) return candidate;
        }
        throw new InvalidOperationException("无法分配目标 ID。");
    }

    private static IEnumerable<string> ParseTags(string value) => value.Split(
        [',', '，', ';', '；', '\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal);
    private static bool SnapshotsEqual(QuestEditorSnapshot left, QuestEditorSnapshot right) =>
        JsonSerializer.Serialize(left.Resource) == JsonSerializer.Serialize(right.Resource) &&
        string.Equals(left.SelectedObjectiveId, right.SelectedObjectiveId, StringComparison.Ordinal);

    private void RaiseObjectiveCommandStates()
    {
        DeleteObjectiveCommand.RaiseCanExecuteChanged();
        MoveObjectiveUpCommand.RaiseCanExecuteChanged();
        MoveObjectiveDownCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CompletionMode));
        OnPropertyChanged(nameof(CompletionModeLabel));
    }
    private void RaiseHistoryStates()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        RaiseDocumentState();
    }
    private void RaiseDocumentState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveStateText));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(ValidationIssues));
    }

    private sealed record QuestEditorSnapshot(QuestResource Resource, string? SelectedObjectiveId);
}

public sealed class QuestObjectiveEditorItem : ObservableObject
{
    private readonly Action<Action> _applyEdit;
    private string _id;
    private string _description;
    private string _entity;
    private string _item;
    private int _itemMetadata;
    private int _required;
    private int _dimension;
    private double _x;
    private double _y;
    private double _z;
    private double _radius;
    private string _actorId;

    public QuestObjectiveEditorItem(QuestObjectiveResource resource, Action<Action> applyEdit)
    {
        _applyEdit = applyEdit;
        Type = resource.Type;
        _id = resource.Id;
        _description = resource.Description;
        _entity = resource.Entity ?? string.Empty;
        _item = resource.Item ?? string.Empty;
        _itemMetadata = resource.ItemMetadata ?? -1;
        _required = resource.Required ?? 1;
        _dimension = resource.Dimension ?? 0;
        _x = resource.X ?? 0;
        _y = resource.Y ?? 64;
        _z = resource.Z ?? 0;
        _radius = resource.Radius ?? 3;
        _actorId = resource.ActorId ?? string.Empty;
    }

    public string Type { get; }
    public string TypeLabel => Type switch { "kill_entity" => "击杀实体", "collect_item" => "收集物品", "interact_actor" => "与角色交互", "reach_location" => "到达位置（兼容）", _ => Type };
    public bool IsKill => Type == "kill_entity";
    public bool IsCollect => Type == "collect_item";
    public bool IsInteract => Type == "interact_actor";
    public bool IsReach => Type == "reach_location";
    public string Id { get => _id; set => SetEdited(_id, value ?? string.Empty, next => _id = next, nameof(Id)); }
    public string Description { get => _description; set => SetEdited(_description, value ?? string.Empty, next => _description = next, nameof(Description)); }
    public string Entity { get => _entity; set => SetEdited(_entity, value ?? string.Empty, next => _entity = next, nameof(Entity)); }
    public string Item { get => _item; set => SetEdited(_item, value ?? string.Empty, next => _item = next, nameof(Item)); }
    public int ItemMetadata { get => _itemMetadata; set => SetEdited(_itemMetadata, value, next => _itemMetadata = next, nameof(ItemMetadata)); }
    public int Required { get => _required; set => SetEdited(_required, value, next => _required = next, nameof(Required)); }
    public int Dimension { get => _dimension; set => SetEdited(_dimension, value, next => _dimension = next, nameof(Dimension)); }
    public double X { get => _x; set => SetEdited(_x, value, next => _x = next, nameof(X)); }
    public double Y { get => _y; set => SetEdited(_y, value, next => _y = next, nameof(Y)); }
    public double Z { get => _z; set => SetEdited(_z, value, next => _z = next, nameof(Z)); }
    public double Radius { get => _radius; set => SetEdited(_radius, value, next => _radius = next, nameof(Radius)); }
    public string ActorId { get => _actorId; set => SetEdited(_actorId, value ?? string.Empty, next => _actorId = next, nameof(ActorId)); }

    public QuestObjectiveResource ToResource() => new()
    {
        Id = Id,
        Type = Type,
        Description = Description,
        Entity = IsKill ? Entity : null,
        Item = IsCollect ? Item : null,
        ItemMetadata = IsCollect ? ItemMetadata : null,
        Required = IsKill || IsCollect || IsInteract ? Required : null,
        Dimension = IsReach ? Dimension : null,
        X = IsReach ? X : null,
        Y = IsReach ? Y : null,
        Z = IsReach ? Z : null,
        Radius = IsReach ? Radius : null,
        ActorId = IsInteract ? ActorId : null,
    };

    private void SetEdited<T>(T current, T value, Action<T> assign, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        _applyEdit(() =>
        {
            assign(value);
            OnPropertyChanged(propertyName);
        });
    }
}
