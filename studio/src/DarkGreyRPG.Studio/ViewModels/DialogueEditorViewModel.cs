using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class DialogueEditorViewModel : ObservableObject, IWorkspaceEditorViewModel
{
    private readonly Stack<DialogueEditorSnapshot> _undoHistory = [];
    private readonly Stack<DialogueEditorSnapshot> _redoHistory = [];
    private bool _restoring;
    private string _displayName;
    private string _notes;
    private string _tagsText;
    private string _entry;
    private DialogueNodeEditorItem? _selectedNode;

    public DialogueEditorViewModel(DialogueDocument document, IReadOnlyList<string>? availableActorIds = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        AvailableActorIds = availableActorIds ?? [];
        _displayName = document.DisplayName;
        _notes = document.Metadata.Notes;
        _tagsText = string.Join(", ", document.Metadata.Tags);
        _entry = document.Entry;
        ReplaceNodeEditors(document.Nodes);
        AddLineCommand = new RelayCommand(AddLine);
        AddChoiceCommand = new RelayCommand(AddChoice);
        AddEndCommand = new RelayCommand(AddEnd);
        DeleteNodeCommand = new RelayCommand(DeleteNode, () => SelectedNode is not null && Nodes.Count > 1);
        MoveNodeUpCommand = new RelayCommand(() => MoveSelectedNode(-1), () => SelectedNode is not null && Nodes.IndexOf(SelectedNode) > 0);
        MoveNodeDownCommand = new RelayCommand(() => MoveSelectedNode(1), () => SelectedNode is not null && Nodes.IndexOf(SelectedNode) is var index && index >= 0 && index < Nodes.Count - 1);
        UndoCommand = new RelayCommand(Undo, () => _undoHistory.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => _redoHistory.Count > 0);
        SelectedNode = Nodes.FirstOrDefault();
        Document.PropertyChanged += (_, _) => RaiseDocumentState();
    }

    public DialogueDocument Document { get; }
    public string Id => Document.Id;
    public IReadOnlyList<string> AvailableActorIds { get; }
    public ObservableCollection<DialogueNodeEditorItem> Nodes { get; } = [];
    public RelayCommand AddLineCommand { get; }
    public RelayCommand AddChoiceCommand { get; }
    public RelayCommand AddEndCommand { get; }
    public RelayCommand DeleteNodeCommand { get; }
    public RelayCommand MoveNodeUpCommand { get; }
    public RelayCommand MoveNodeDownCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }

    public string DisplayName
    {
        get => _displayName;
        set => ApplyEdit(() => SetProperty(ref _displayName, value ?? string.Empty));
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

    public string Entry
    {
        get => _entry;
        set => ApplyEdit(() => SetProperty(ref _entry, value ?? string.Empty));
    }

    public DialogueNodeEditorItem? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetProperty(ref _selectedNode, value)) return;
            RaiseNodeCommandStates();
        }
    }

    public bool IsDirty => Document.IsDirty;
    public bool CanSave => IsDirty && Document.ValidationErrors.Count == 0;
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public string ValidationText => string.Join(Environment.NewLine, Document.ValidationErrors.Select(issue => issue.Message));
    public IReadOnlyList<ValidationIssue> ValidationIssues => Document.ValidationIssues;

    private void AddLine()
    {
        ApplyEdit(() =>
        {
            var id = AllocateNodeId("line");
            var speaker = AvailableActorIds.FirstOrDefault() ?? string.Empty;
            var next = Nodes.LastOrDefault()?.Id ?? string.Empty;
            var node = CreateNodeEditor(DialogueNodeResource.Line(id, speaker, "新台词", next));
            Nodes.Insert(Math.Max(0, Nodes.Count - 1), node);
            SelectedNode = node;
            Entry = Nodes.Count == 1 ? id : Entry;
        });
    }

    private void AddChoice()
    {
        ApplyEdit(() =>
        {
            var choiceId = AllocateNodeId("choice");
            var firstEndId = AllocateNodeId(choiceId + "_first_end");
            var secondEndId = AllocateNodeId(choiceId + "_second_end");
            var choice = CreateNodeEditor(DialogueNodeResource.Choice(
                choiceId,
                "请选择",
                [new DialogueChoiceResource { Text = "选项一", Next = firstEndId },
                 new DialogueChoiceResource { Text = "选项二", Next = secondEndId }]));
            Nodes.Add(choice);
            Nodes.Add(CreateNodeEditor(DialogueNodeResource.End(firstEndId, "choice_one")));
            Nodes.Add(CreateNodeEditor(DialogueNodeResource.End(secondEndId, "choice_two")));
            SelectedNode = choice;
        });
    }

    private void AddEnd()
    {
        ApplyEdit(() =>
        {
            var id = AllocateNodeId("end");
            var node = CreateNodeEditor(DialogueNodeResource.End(id, "complete"));
            Nodes.Add(node);
            SelectedNode = node;
        });
    }

    private void DeleteNode()
    {
        if (SelectedNode is null || Nodes.Count <= 1) return;
        ApplyEdit(() =>
        {
            var index = Nodes.IndexOf(SelectedNode);
            var removedId = SelectedNode.Id;
            Nodes.Remove(SelectedNode);
            if (string.Equals(Entry, removedId, StringComparison.Ordinal)) _entry = Nodes[0].Id;
            SelectedNode = Nodes[Math.Clamp(index, 0, Nodes.Count - 1)];
        });
    }

    private void MoveSelectedNode(int offset)
    {
        if (SelectedNode is null) return;
        var source = Nodes.IndexOf(SelectedNode);
        var target = source + offset;
        if (source < 0 || target < 0 || target >= Nodes.Count) return;
        ApplyEdit(() => Nodes.Move(source, target));
        RaiseNodeCommandStates();
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

    private void RestoreSnapshot(DialogueEditorSnapshot snapshot)
    {
        _restoring = true;
        try
        {
            _displayName = snapshot.Resource.DisplayName;
            _notes = snapshot.Resource.Metadata.Notes;
            _tagsText = string.Join(", ", snapshot.Resource.Metadata.Tags);
            _entry = snapshot.Resource.Entry;
            ReplaceNodeEditors(snapshot.Resource.Nodes);
            SelectedNode = Nodes.FirstOrDefault(node => node.Id == snapshot.SelectedNodeId) ?? Nodes.FirstOrDefault();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(TagsText));
            OnPropertyChanged(nameof(Entry));
            CommitToDocument();
        }
        finally
        {
            _restoring = false;
        }
    }

    private DialogueEditorSnapshot CaptureSnapshot() => new(BuildResource(), SelectedNode?.Id);

    private DialogueResource BuildResource() => new()
    {
        SchemaVersion = DialogueResource.CurrentSchemaVersion,
        Id = Document.Id,
        Title = _displayName,
        DisplayName = _displayName,
        HomeStoryId = Document.HomeStoryId,
        Speakers = Nodes.Where(node => node.IsLine && !string.IsNullOrWhiteSpace(node.Speaker))
            .Select(node => node.Speaker.Trim()).Distinct(StringComparer.Ordinal).ToList(),
        Entry = _entry,
        Nodes = Nodes.Select(node => node.ToResource()).ToList(),
        Metadata = new DialogueMetadata { Notes = _notes, Tags = ParseTags(_tagsText).ToList() },
    };

    private void CommitToDocument()
    {
        var resource = BuildResource();
        Document.Title = resource.Title;
        Document.DisplayName = resource.DisplayName;
        Document.Entry = resource.Entry;
        Document.Metadata = resource.Metadata;
        Document.SetSpeakers(resource.Speakers);
        Document.ReplaceNodes(resource.Nodes);
        RaiseDocumentState();
    }

    private void ReplaceNodeEditors(IEnumerable<DialogueNodeResource> values)
    {
        Nodes.Clear();
        foreach (var value in values) Nodes.Add(CreateNodeEditor(value));
    }

    private DialogueNodeEditorItem CreateNodeEditor(DialogueNodeResource value) => new(value, ApplyEdit);

    private string AllocateNodeId(string baseId)
    {
        if (Nodes.All(node => node.Id != baseId)) return baseId;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (Nodes.All(node => node.Id != candidate)) return candidate;
        }
        throw new InvalidOperationException("无法分配节点 ID。");
    }

    private static IEnumerable<string> ParseTags(string value) => value.Split(
        [',', '，', ';', '；', '\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal);

    private static bool SnapshotsEqual(DialogueEditorSnapshot left, DialogueEditorSnapshot right) =>
        JsonSerializer.Serialize(left.Resource) == JsonSerializer.Serialize(right.Resource) &&
        string.Equals(left.SelectedNodeId, right.SelectedNodeId, StringComparison.Ordinal);

    private void RaiseNodeCommandStates()
    {
        DeleteNodeCommand.RaiseCanExecuteChanged();
        MoveNodeUpCommand.RaiseCanExecuteChanged();
        MoveNodeDownCommand.RaiseCanExecuteChanged();
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

    private sealed record DialogueEditorSnapshot(DialogueResource Resource, string? SelectedNodeId);
}

public sealed class DialogueNodeEditorItem : ObservableObject
{
    private readonly Action<Action> _applyEdit;
    private string _id;
    private string _speaker;
    private string _text;
    private string _next;
    private string _prompt;
    private string _target;
    private string _result;

    public DialogueNodeEditorItem(DialogueNodeResource resource, Action<Action> applyEdit)
    {
        _applyEdit = applyEdit;
        _id = resource.Id;
        Type = resource.Type;
        _speaker = resource.Speaker ?? string.Empty;
        _text = resource.Text ?? string.Empty;
        _next = resource.Next ?? string.Empty;
        _prompt = resource.Prompt ?? string.Empty;
        _target = resource.Target ?? string.Empty;
        _result = resource.Result ?? string.Empty;
        foreach (var choice in resource.Choices ?? []) Choices.Add(CreateChoiceEditor(choice));
        AddChoiceOptionCommand = new RelayCommand(() => _applyEdit(() => Choices.Add(CreateChoiceEditor(new DialogueChoiceResource { Text = "新选项", Next = string.Empty }))));
    }

    public string Type { get; }
    public string TypeLabel => Type switch { "line" => "台词", "choice" => "玩家选择", "jump" => "跳转", "end" => "命名出口", _ => Type };
    public bool IsLine => Type == "line";
    public bool IsChoice => Type == "choice";
    public bool IsJump => Type == "jump";
    public bool IsEnd => Type == "end";
    public ObservableCollection<DialogueChoiceEditorItem> Choices { get; } = [];
    public RelayCommand AddChoiceOptionCommand { get; }

    public string Id { get => _id; set => SetEdited(_id, value ?? string.Empty, next => _id = next, nameof(Id)); }
    public string Speaker { get => _speaker; set => SetEdited(_speaker, value ?? string.Empty, next => _speaker = next, nameof(Speaker)); }
    public string Text { get => _text; set => SetEdited(_text, value ?? string.Empty, next => _text = next, nameof(Text)); }
    public string Next { get => _next; set => SetEdited(_next, value ?? string.Empty, next => _next = next, nameof(Next)); }
    public string Prompt { get => _prompt; set => SetEdited(_prompt, value ?? string.Empty, next => _prompt = next, nameof(Prompt)); }
    public string Target { get => _target; set => SetEdited(_target, value ?? string.Empty, next => _target = next, nameof(Target)); }
    public string Result { get => _result; set => SetEdited(_result, value ?? string.Empty, next => _result = next, nameof(Result)); }

    public void RemoveChoice(DialogueChoiceEditorItem choice) => _applyEdit(() => Choices.Remove(choice));

    private DialogueChoiceEditorItem CreateChoiceEditor(DialogueChoiceResource choice) =>
        new(choice, _applyEdit, RemoveChoice);

    public DialogueNodeResource ToResource() => new()
    {
        Id = Id,
        Type = Type,
        Speaker = IsLine ? Speaker : null,
        Text = IsLine ? Text : null,
        Next = IsLine ? Next : null,
        Prompt = IsChoice ? Prompt : null,
        Choices = IsChoice ? Choices.Select(choice => choice.ToResource()).ToList() : null,
        Target = IsJump ? Target : null,
        Result = IsEnd ? Result : null,
    };

    private void SetEdited(string current, string value, Action<string> assign, string propertyName)
    {
        if (string.Equals(current, value, StringComparison.Ordinal)) return;
        _applyEdit(() =>
        {
            assign(value);
            OnPropertyChanged(propertyName);
        });
    }
}

public sealed class DialogueChoiceEditorItem : ObservableObject
{
    private readonly Action<Action> _applyEdit;
    private string _text;
    private string _next;

    public DialogueChoiceEditorItem(
        DialogueChoiceResource resource,
        Action<Action> applyEdit,
        Action<DialogueChoiceEditorItem> remove)
    {
        _applyEdit = applyEdit;
        _text = resource.Text;
        _next = resource.Next;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public RelayCommand RemoveCommand { get; }
    public string Text { get => _text; set => SetEdited(_text, value ?? string.Empty, next => _text = next, nameof(Text)); }
    public string Next { get => _next; set => SetEdited(_next, value ?? string.Empty, next => _next = next, nameof(Next)); }
    public DialogueChoiceResource ToResource() => new() { Text = Text, Next = Next };

    private void SetEdited(string current, string value, Action<string> assign, string propertyName)
    {
        if (string.Equals(current, value, StringComparison.Ordinal)) return;
        _applyEdit(() =>
        {
            assign(value);
            OnPropertyChanged(propertyName);
        });
    }
}
