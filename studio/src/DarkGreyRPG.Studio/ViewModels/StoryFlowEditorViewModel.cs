using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class StoryFlowEditorViewModel : ObservableObject, IWorkspaceEditorViewModel
{
    private readonly Stack<FlowSnapshot> _undoHistory = [];
    private readonly Stack<FlowSnapshot> _redoHistory = [];
    private bool _restoring;
    private StoryFlowNodeEditorItem? _selectedNode;
    private double _zoom = 1;
    private double _panX;
    private double _panY;

    public StoryFlowEditorViewModel(
        StoryDocument document,
        IReadOnlyList<string> actorIds,
        IReadOnlyList<string> dialogueIds,
        IReadOnlyList<string> questIds,
        IReadOnlyList<string> storyIds)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ActorIds = actorIds ?? [];
        DialogueIds = dialogueIds ?? [];
        QuestIds = questIds ?? [];
        StoryIds = storyIds ?? [];
        AddStoryStartCommand = new RelayCommand(() => AddNode("StoryStart"));
        AddActorInteractCommand = new RelayCommand(() => AddNode("ActorInteract"));
        AddPlayDialogueCommand = new RelayCommand(() => AddNode("PlayDialogue"));
        AddStartQuestCommand = new RelayCommand(() => AddNode("StartQuest"));
        AddWaitQuestCompleteCommand = new RelayCommand(() => AddNode("WaitQuestComplete"));
        AddDialogueExitBranchCommand = new RelayCommand(() => AddNode("DialogueExitBranch"));
        AddEnterStoryCommand = new RelayCommand(() => AddNode("EnterStory"));
        AddEndStoryCommand = new RelayCommand(() => AddNode("EndStory"));
        AddEndCommand = new RelayCommand(() => AddNode("End"));
        DeleteSelectionCommand = new RelayCommand(DeleteSelection, () => Nodes.Any(node => node.IsSelected));
        CopyCommand = new RelayCommand(CopySelection, () => Nodes.Any(node => node.IsSelected));
        PasteCommand = new RelayCommand(Paste, () => _clipboard is not null);
        UndoCommand = new RelayCommand(Undo, () => _undoHistory.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => _redoHistory.Count > 0);
        ReplaceEditors(document.ToResource());
        Document.PropertyChanged += (_, _) => RaiseDocumentState();
    }

    private FlowClipboard? _clipboard;
    public StoryDocument Document { get; }
    public string Id => Document.Id;
    public IReadOnlyList<string> ActorIds { get; }
    public IReadOnlyList<string> DialogueIds { get; }
    public IReadOnlyList<string> QuestIds { get; }
    public IReadOnlyList<string> StoryIds { get; }
    public ObservableCollection<StoryFlowNodeEditorItem> Nodes { get; } = [];
    public ObservableCollection<StoryFlowConnectionEditorItem> Connections { get; } = [];
    public RelayCommand AddStoryStartCommand { get; }
    public RelayCommand AddActorInteractCommand { get; }
    public RelayCommand AddPlayDialogueCommand { get; }
    public RelayCommand AddStartQuestCommand { get; }
    public RelayCommand AddWaitQuestCompleteCommand { get; }
    public RelayCommand AddDialogueExitBranchCommand { get; }
    public RelayCommand AddEnterStoryCommand { get; }
    public RelayCommand AddEndStoryCommand { get; }
    public RelayCommand AddEndCommand { get; }
    public RelayCommand DeleteSelectionCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }

    public StoryFlowNodeEditorItem? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetProperty(ref _selectedNode, value)) return;
            OnPropertyChanged(nameof(HasSelectedNode));
            OnPropertyChanged(nameof(SelectedResourceCandidates));
            OnPropertyChanged(nameof(SelectedResourceLabel));
        }
    }

    public bool HasSelectedNode => SelectedNode is not null;
    public IReadOnlyList<string> SelectedResourceCandidates => SelectedNode?.CanonicalType switch
    {
        "ActorInteract" => ActorIds, "PlayDialogue" => DialogueIds,
        "StartQuest" or "WaitQuestComplete" => QuestIds, "EnterStory" => StoryIds,
        _ => [],
    };
    public string SelectedResourceLabel => SelectedNode?.CanonicalType switch
    {
        "ActorInteract" => "Actor", "PlayDialogue" => "Dialogue",
        "StartQuest" or "WaitQuestComplete" => "Quest", "EnterStory" => "目标 Story",
        _ => "资源",
    };
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, Math.Clamp(value, 0.25, 2.5)); }
    public double PanX { get => _panX; set => SetProperty(ref _panX, value); }
    public double PanY { get => _panY; set => SetProperty(ref _panY, value); }
    public bool IsDirty => Document.IsDirty;
    public bool CanSave => IsDirty && ValidationErrors.Count == 0;
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public IReadOnlyList<ValidationIssue> ValidationIssues => [.. Document.ValidationIssues, .. ValidateReferences()];
    public IReadOnlyList<ValidationIssue> ValidationErrors => ValidationIssues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
    public string ValidationText => string.Join(Environment.NewLine, ValidationErrors.Select(issue => issue.Message));
    public string Summary => $"{Nodes.Count} 个节点 · {Connections.Count} 条连接 · {Math.Round(Zoom * 100)}%";

    public void SelectOnly(StoryFlowNodeEditorItem? node)
    {
        foreach (var candidate in Nodes) candidate.IsSelected = ReferenceEquals(candidate, node);
        SelectedNode = node;
        RaiseSelectionStates();
    }

    public void ToggleSelection(StoryFlowNodeEditorItem node)
    {
        node.IsSelected = !node.IsSelected;
        SelectedNode = node.IsSelected ? node : Nodes.LastOrDefault(candidate => candidate.IsSelected);
        RaiseSelectionStates();
    }

    public void SelectInRectangle(double left, double top, double right, double bottom)
    {
        foreach (var node in Nodes)
            node.IsSelected = node.X + StoryFlowNodeEditorItem.Width >= left && node.X <= right &&
                              node.Y + StoryFlowNodeEditorItem.Height >= top && node.Y <= bottom;
        SelectedNode = Nodes.LastOrDefault(node => node.IsSelected);
        RaiseSelectionStates();
    }

    public void MoveSelection(IReadOnlyDictionary<string, (double X, double Y)> positions)
    {
        if (positions.Count == 0) return;
        ApplyEdit(() =>
        {
            foreach (var pair in positions)
            {
                var node = Nodes.FirstOrDefault(candidate => candidate.Id == pair.Key);
                if (node is null) continue;
                node.SetPosition(pair.Value.X, pair.Value.Y);
            }
        });
    }

    public bool Connect(string fromNodeId, string output, string toNodeId)
    {
        if (fromNodeId == toNodeId || string.IsNullOrWhiteSpace(output)) return false;
        var changed = false;
        ApplyEdit(() =>
        {
            var existing = Connections.FirstOrDefault(connection => connection.From == fromNodeId && connection.Output == output);
            if (existing is not null) Connections.Remove(existing);
            Connections.Add(new(fromNodeId, output, toNodeId));
            changed = true;
        });
        return changed;
    }

    public void RemoveConnection(StoryFlowConnectionEditorItem connection) => ApplyEdit(() => Connections.Remove(connection));

    private void AddNode(string canonicalType)
    {
        ApplyEdit(() =>
        {
            var id = AllocateNodeId(TypeBaseId(canonicalType));
            var properties = DefaultProperties(canonicalType);
            var node = new StoryFlowNodeEditorItem(id, canonicalType, 120 - PanX / Zoom, 100 - PanY / Zoom, properties, ApplyEdit, RenameNode);
            Nodes.Add(node);
            SelectOnly(node);
        });
    }

    private Dictionary<string, string> DefaultProperties(string type) => type switch
    {
        "ActorInteract" => new() { ["actor_id"] = ActorIds.FirstOrDefault() ?? string.Empty },
        "PlayDialogue" => new() { ["dialogue_id"] = DialogueIds.FirstOrDefault() ?? string.Empty },
        "StartQuest" or "WaitQuestComplete" => new() { ["quest_id"] = QuestIds.FirstOrDefault() ?? string.Empty },
        "DialogueExitBranch" => new() { ["exit_names"] = "hand_over, conceal" },
        "EnterStory" => new() { ["target_story_id"] = StoryIds.FirstOrDefault(id => id != Document.Id) ?? string.Empty },
        _ => [],
    };

    private void DeleteSelection()
    {
        var ids = Nodes.Where(node => node.IsSelected).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0) return;
        ApplyEdit(() =>
        {
            for (var index = Nodes.Count - 1; index >= 0; index--)
                if (ids.Contains(Nodes[index].Id)) Nodes.RemoveAt(index);
            for (var index = Connections.Count - 1; index >= 0; index--)
                if (ids.Contains(Connections[index].From) || ids.Contains(Connections[index].To)) Connections.RemoveAt(index);
            SelectedNode = null;
        });
    }

    private void CopySelection()
    {
        var selected = Nodes.Where(node => node.IsSelected).ToArray();
        var ids = selected.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        _clipboard = new(
            selected.Select(node => node.ToResource()).ToArray(),
            Connections.Where(connection => ids.Contains(connection.From) && ids.Contains(connection.To)).Select(connection => connection.ToResource()).ToArray());
        PasteCommand.RaiseCanExecuteChanged();
    }

    private void Paste()
    {
        if (_clipboard is null) return;
        ApplyEdit(() =>
        {
            foreach (var node in Nodes) node.IsSelected = false;
            var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var resource in _clipboard.Nodes)
            {
                var newId = AllocateNodeId(resource.Id + "_copy");
                idMap[resource.Id] = newId;
                var item = StoryFlowNodeEditorItem.FromResource(resource, ApplyEdit, RenameNode, newId, 36, 36);
                item.IsSelected = true;
                Nodes.Add(item);
            }
            foreach (var connection in _clipboard.Connections)
                Connections.Add(new(idMap[connection.From], connection.Output, idMap[connection.To]));
            SelectedNode = Nodes.LastOrDefault(node => node.IsSelected);
        });
    }

    private void ApplyEdit(Action edit)
    {
        if (_restoring) { edit(); return; }
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
        if (!_undoHistory.TryPop(out var snapshot)) return;
        _redoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(snapshot);
        RaiseHistoryStates();
    }

    private void Redo()
    {
        if (!_redoHistory.TryPop(out var snapshot)) return;
        _undoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(snapshot);
        RaiseHistoryStates();
    }

    private FlowSnapshot CaptureSnapshot() => new(BuildResource(), Nodes.Where(node => node.IsSelected).Select(node => node.Id).ToArray());

    private void RestoreSnapshot(FlowSnapshot snapshot)
    {
        _restoring = true;
        try
        {
            Document.Replace(snapshot.Resource);
            ReplaceEditors(snapshot.Resource);
            var selected = snapshot.SelectedIds.ToHashSet(StringComparer.Ordinal);
            foreach (var node in Nodes) node.IsSelected = selected.Contains(node.Id);
            SelectedNode = Nodes.LastOrDefault(node => node.IsSelected);
        }
        finally { _restoring = false; }
    }

    private void ReplaceEditors(StoryResource resource)
    {
        Nodes.Clear();
        foreach (var node in resource.Nodes) Nodes.Add(StoryFlowNodeEditorItem.FromResource(node, ApplyEdit, RenameNode));
        Connections.Clear();
        foreach (var connection in resource.Connections) Connections.Add(new(connection.From, connection.Output, connection.To));
        SelectedNode = null;
        OnPropertyChanged(nameof(Summary));
    }

    private StoryResource BuildResource()
    {
        var original = Document.ToResource();
        return new StoryResource
        {
            SchemaVersion = original.SchemaVersion, Id = original.Id, DisplayName = original.DisplayName,
            Description = original.Description, Tags = [.. original.Tags], EntryPresentation = original.EntryPresentation,
            OwnedResources = original.OwnedResources.Clone(), ReferencedResources = original.ReferencedResources.Clone(),
            FlowRef = original.FlowRef, Title = original.Title,
            Entry = Nodes.FirstOrDefault(node => node.CanonicalType == "StoryStart")?.Id ?? original.Entry,
            Nodes = Nodes.Select(node => node.ToResource()).ToList(),
            Connections = Connections.Select(connection => connection.ToResource()).ToList(), Metadata = original.Metadata,
        };
    }

    private void CommitToDocument()
    {
        Document.Replace(BuildResource());
        RaiseDocumentState();
        OnPropertyChanged(nameof(Summary));
    }

    private IReadOnlyList<ValidationIssue> ValidateReferences()
    {
        var issues = new List<ValidationIssue>();
        foreach (var node in Nodes)
        {
            var (key, values, code, label) = node.CanonicalType switch
            {
                "ActorInteract" => ("actor_id", ActorIds, "story.flow.actor.missing", "Actor"),
                "PlayDialogue" => ("dialogue_id", DialogueIds, "story.flow.dialogue.missing", "Dialogue"),
                "StartQuest" or "WaitQuestComplete" => ("quest_id", QuestIds, "story.flow.quest.missing", "Quest"),
                "EnterStory" => ("target_story_id", StoryIds, "story.flow.story.missing", "Story"),
                _ => (string.Empty, (IReadOnlyList<string>)[], string.Empty, string.Empty),
            };
            if (key.Length == 0) continue;
            var value = node.GetProperty(key);
            if (!values.Contains(value, StringComparer.Ordinal))
                issues.Add(new(code, $"节点 '{node.Id}' 引用的 {label} '{value}' 不存在。", key));
        }
        return issues;
    }

    private string AllocateNodeId(string baseId)
    {
        if (Nodes.All(node => node.Id != baseId)) return baseId;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (Nodes.All(node => node.Id != candidate)) return candidate;
        }
    }

    private void RenameNode(StoryFlowNodeEditorItem node, string requestedId)
    {
        var newId = requestedId.Trim();
        if (newId.Length == 0 || newId == node.Id || Nodes.Any(candidate => !ReferenceEquals(candidate, node) && candidate.Id == newId)) return;
        ApplyEdit(() =>
        {
            var oldId = node.Id;
            node.SetIdCore(newId);
            for (var index = 0; index < Connections.Count; index++)
            {
                var connection = Connections[index];
                if (connection.From == oldId || connection.To == oldId)
                    Connections[index] = connection with
                    {
                        From = connection.From == oldId ? newId : connection.From,
                        To = connection.To == oldId ? newId : connection.To,
                    };
            }
        });
    }

    private static string TypeBaseId(string type) => type switch
    {
        "StoryStart" => "start", "ActorInteract" => "interact", "PlayDialogue" => "dialogue",
        "StartQuest" => "start_quest", "WaitQuestComplete" => "wait_quest", "DialogueExitBranch" => "dialogue_exit",
        "EnterStory" => "enter_story", "EndStory" => "end_story", _ => "end",
    };

    private static bool SnapshotsEqual(FlowSnapshot left, FlowSnapshot right) =>
        StorySerializer.Serialize(left.Resource) == StorySerializer.Serialize(right.Resource) &&
        left.SelectedIds.SequenceEqual(right.SelectedIds, StringComparer.Ordinal);

    private void RaiseSelectionStates()
    {
        DeleteSelectionCommand.RaiseCanExecuteChanged();
        CopyCommand.RaiseCanExecuteChanged();
    }

    private void RaiseHistoryStates()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        RaiseSelectionStates();
        RaiseDocumentState();
    }

    private void RaiseDocumentState()
    {
        OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveStateText)); OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(ValidationErrors)); OnPropertyChanged(nameof(ValidationText));
    }

    private sealed record FlowSnapshot(StoryResource Resource, IReadOnlyList<string> SelectedIds);
    private sealed record FlowClipboard(IReadOnlyList<StoryNodeResource> Nodes, IReadOnlyList<StoryConnectionResource> Connections);
}

public sealed class StoryFlowNodeEditorItem : ObservableObject
{
    public const double Width = 190;
    public const double Height = 116;
    private readonly Action<Action> _applyEdit;
    private readonly Action<StoryFlowNodeEditorItem, string>? _renameNode;
    private readonly string _persistedType;
    private string _id;
    private double _x;
    private double _y;
    private bool _isSelected;
    private readonly Dictionary<string, string> _properties;

    private StoryFlowNodeEditorItem(string id, string canonicalType, string persistedType, double x, double y, Dictionary<string, string> properties, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode)
    { _id = id; CanonicalType = canonicalType; _persistedType = persistedType; _x = x; _y = y; _properties = properties; _applyEdit = applyEdit; _renameNode = renameNode; }

    public StoryFlowNodeEditorItem(string id, string canonicalType, double x, double y, Dictionary<string, string> properties, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode = null)
        : this(id, canonicalType, PersistedTypeFor(canonicalType), x, y, properties, applyEdit, renameNode) { }

    public static StoryFlowNodeEditorItem FromResource(StoryNodeResource resource, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode = null, string? id = null, double offsetX = 0, double offsetY = 0)
    {
        var type = StoryValidator.CanonicalizeType(resource.Type) ?? resource.Type;
        var properties = (resource.Properties ?? []).ToDictionary(pair => pair.Key, pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : pair.Value.GetRawText(), StringComparer.Ordinal);
        return new(id ?? resource.Id, type, resource.Type, resource.Position.X + offsetX, resource.Position.Y + offsetY, properties, applyEdit, renameNode);
    }

    public string Id
    {
        get => _id;
        set
        {
            var requested = value ?? string.Empty;
            if (_renameNode is not null) _renameNode(this, requested);
            else SetEdited(_id, requested, next => _id = next, nameof(Id));
        }
    }
    public string CanonicalType { get; }
    public string TypeLabel => CanonicalType switch
    {
        "StoryStart" => "剧情开始", "ActorInteract" => "角色交互", "PlayDialogue" => "播放对话",
        "StartQuest" => "开始任务", "WaitQuestComplete" => "等待任务完成", "DialogueExitBranch" => "按 Dialogue Exit 分支",
        "EnterStory" => "进入剧情", "EndStory" => "结束当前剧情", "End" => "结束", _ => CanonicalType,
    };
    public string Accent => CanonicalType switch { "StoryStart" => "#4CAF50", "ActorInteract" => "#42A5F5", "PlayDialogue" or "DialogueExitBranch" => "#AB47BC", "StartQuest" or "WaitQuestComplete" => "#FFA726", "EnterStory" or "EndStory" => "#26A69A", _ => "#78909C" };
    public double X => _x;
    public double Y => _y;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string ResourceValue
    {
        get => GetProperty(ResourceKey);
        set { if (ResourceKey.Length != 0) SetPropertyValue(ResourceKey, value ?? string.Empty); }
    }
    public string ResourceKey => CanonicalType switch { "ActorInteract" => "actor_id", "PlayDialogue" => "dialogue_id", "StartQuest" or "WaitQuestComplete" => "quest_id", "EnterStory" => "target_story_id", _ => string.Empty };
    public string ExitNames { get => GetProperty("exit_names"); set => SetPropertyValue("exit_names", value ?? string.Empty); }
    public IReadOnlyList<string> Outputs => CanonicalType switch
    {
        "DialogueExitBranch" => ExitNames.Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray(),
        "EnterStory" or "EndStory" or "End" => [],
        _ => ["next"],
    };

    public string GetProperty(string key) => key.Length != 0 && _properties.TryGetValue(key, out var value) ? value : string.Empty;
    public void SetPosition(double x, double y) { _x = x; _y = y; OnPropertyChanged(nameof(X)); OnPropertyChanged(nameof(Y)); }
    private void SetPropertyValue(string key, string value)
    {
        if (GetProperty(key) == value) return;
        _applyEdit(() => { _properties[key] = value; OnPropertyChanged(nameof(ResourceValue)); OnPropertyChanged(nameof(ExitNames)); OnPropertyChanged(nameof(Outputs)); });
    }
    private void SetEdited(string current, string value, Action<string> assign, string propertyName)
    { if (current == value) return; _applyEdit(() => { assign(value); OnPropertyChanged(propertyName); }); }
    internal void SetIdCore(string value) { _id = value; OnPropertyChanged(nameof(Id)); }

    public StoryNodeResource ToResource() => new()
    {
        Id = Id, Type = _persistedType,
        Position = new StoryNodePosition { X = X, Y = Y },
        Properties = _properties.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal),
    };

    private static string PersistedTypeFor(string canonicalType) => canonicalType switch
    {
        "StoryStart" => "story_start", "ActorInteract" => "interact_actor", "PlayDialogue" => "play_dialogue",
        "StartQuest" => "start_quest", "WaitQuestComplete" => "quest_completed", "DialogueExitBranch" => "dialogue_exit_branch",
        "EnterStory" => "enter_story", "EndStory" => "end_story", "End" => "end", _ => canonicalType,
    };
}

public sealed record StoryFlowConnectionEditorItem(string From, string Output, string To)
{
    public StoryConnectionResource ToResource() => new() { From = From, Output = Output, To = To };
}
