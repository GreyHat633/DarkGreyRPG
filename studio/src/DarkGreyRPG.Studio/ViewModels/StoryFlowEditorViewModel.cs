using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Stories.Definitions;
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
    private long _problemFocusSequence;
    private FlowProblemFocusRequest? _problemFocusRequest;
    private readonly HashSet<string> _storyActorIds;
    private readonly HashSet<string> _storyDialogueIds;
    private readonly HashSet<string> _storyQuestIds;

    public StoryFlowEditorViewModel(
        StoryDocument document,
        IReadOnlyList<string> actorIds,
        IReadOnlyList<string> dialogueIds,
        IReadOnlyList<string> questIds,
        IReadOnlyList<string> storyIds,
        IReadOnlyList<string>? storyActorIds = null,
        IReadOnlyList<string>? storyDialogueIds = null,
        IReadOnlyList<string>? storyQuestIds = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ActorIds = actorIds ?? [];
        DialogueIds = dialogueIds ?? [];
        QuestIds = questIds ?? [];
        StoryIds = storyIds ?? [];
        _storyActorIds = (storyActorIds ?? ActorIds).ToHashSet(StringComparer.Ordinal);
        _storyDialogueIds = (storyDialogueIds ?? DialogueIds).ToHashSet(StringComparer.Ordinal);
        _storyQuestIds = (storyQuestIds ?? QuestIds).ToHashSet(StringComparer.Ordinal);
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
    public IReadOnlySet<string> StoryActorIds => _storyActorIds;
    public IReadOnlySet<string> StoryDialogueIds => _storyDialogueIds;
    public IReadOnlySet<string> StoryQuestIds => _storyQuestIds;
    public IReadOnlyList<StoryNodeDefinition> NodeDefinitions => StoryNodeDefinitionRegistry.Definitions;
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
        "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => QuestIds, "EnterStory" => StoryIds,
        _ => [],
    };
    public string SelectedResourceLabel => SelectedNode?.CanonicalType switch
    {
        "ActorInteract" => "Actor", "PlayDialogue" => "Dialogue",
        "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => "Quest", "EnterStory" => "目标 Story",
        _ => "资源",
    };
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, Math.Clamp(value, 0.25, 2.5)); }
    public double PanX { get => _panX; set => SetProperty(ref _panX, value); }
    public double PanY { get => _panY; set => SetProperty(ref _panY, value); }
    public FlowProblemFocusRequest? ProblemFocusRequest
    {
        get => _problemFocusRequest;
        private set => SetProperty(ref _problemFocusRequest, value);
    }
    public bool IsDirty => Document.IsDirty;
    public bool CanSave => IsDirty && ValidationErrors.Count == 0;
    public string SaveStateText => IsDirty ? "未保存" : "已保存";
    public IReadOnlyList<ValidationIssue> ValidationIssues => [.. Document.ValidationIssues, .. ValidateReferences()];
    public IReadOnlyList<ValidationIssue> ValidationErrors => ValidationIssues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
    public int ErrorCount => ValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
    public int WarningCount => ValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);
    public string ValidationText => string.Join(Environment.NewLine, ValidationErrors.Select(issue => issue.Message));
    public string Summary => $"{Nodes.Count} 个节点 · {Connections.Count} 条连接 · {Math.Round(Zoom * 100)}%";

    public IReadOnlyList<FlowResourceCandidate> GetResourceCandidates(StoryFlowNodeEditorItem node) => node.CanonicalType switch
    {
        "ActorInteract" => GroupCandidates(ActorIds, StoryActorIds),
        "PlayDialogue" => GroupCandidates(DialogueIds, StoryDialogueIds),
        "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => GroupCandidates(QuestIds, StoryQuestIds),
        "EnterStory" => StoryIds.Select(id => new FlowResourceCandidate(id, "项目 Story", true)).ToArray(),
        _ => [],
    };

    public bool IsResourceInStoryMembership(StoryFlowNodeEditorItem node, string id) => node.CanonicalType switch
    {
        "ActorInteract" => StoryActorIds.Contains(id),
        "PlayDialogue" => StoryDialogueIds.Contains(id),
        "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => StoryQuestIds.Contains(id),
        "EnterStory" => StoryIds.Contains(id, StringComparer.Ordinal),
        _ => true,
    };

    public static string GetResourceLabel(StoryFlowNodeEditorItem node) => node.CanonicalType switch
    {
        "ActorInteract" => "Actor",
        "PlayDialogue" => "Dialogue",
        "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => "Quest",
        "EnterStory" => "目标 Story",
        _ => "资源",
    };

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
        var changedPositions = positions
            .Select(pair => (pair.Key, pair.Value.X, pair.Value.Y,
                Node: Nodes.FirstOrDefault(candidate => candidate.Id == pair.Key)))
            .Where(item => item.Node is not null && (item.Node.X != item.X || item.Node.Y != item.Y))
            .ToArray();
        if (changedPositions.Length == 0) return;
        ApplyEdit(() =>
        {
            foreach (var item in changedPositions) item.Node!.SetPosition(item.X, item.Y);
        });
    }

    /// <summary>Returns all connections entering <paramref name="nodeId"/> in a stable order.</summary>
    public IReadOnlyList<StoryFlowConnectionEditorItem> GetIncomingConnections(string nodeId) =>
        Connections
            .Where(connection => string.Equals(connection.To, nodeId, StringComparison.Ordinal))
            .OrderBy(connection => connection.From, StringComparer.Ordinal)
            .ThenBy(connection => connection.Output, StringComparer.Ordinal)
            .ThenBy(connection => connection.To, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Returns the concrete connection currently occupying a source output, if any.</summary>
    public StoryFlowConnectionEditorItem? GetConnection(string fromNodeId, string output) =>
        Connections.FirstOrDefault(connection =>
            string.Equals(connection.From, fromNodeId, StringComparison.Ordinal) &&
            string.Equals(connection.Output, output, StringComparison.Ordinal));

    /// <summary>
    /// Analyzes a requested DialogueExitBranch port set without changing the
    /// editor. The result is deliberately a data object so a view can ask for
    /// confirmation before applying a connected-port deletion.
    /// </summary>
    public DialogueExitNameChangePlan AnalyzeDialogueExitNames(StoryFlowNodeEditorItem node, IEnumerable<string> requestedNames)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(requestedNames);
        return AnalyzeDialogueExitNames(node.Id, requestedNames);
    }

    public DialogueExitNameChangePlan AnalyzeDialogueExitNames(StoryFlowNodeEditorItem node, string requestedNames) =>
        AnalyzeDialogueExitNames(node, ParseDynamicNames(requestedNames));

    public DialogueExitNameChangePlan AnalyzeDialogueExitNames(string nodeId, string requestedNames) =>
        AnalyzeDialogueExitNames(nodeId, ParseDynamicNames(requestedNames));

    public DialogueExitNameChangePlan AnalyzeDialogueExitNames(string nodeId, IEnumerable<string> requestedNames)
    {
        ArgumentNullException.ThrowIfNull(requestedNames);
        var node = Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        var requested = requestedNames.Select(name => name?.Trim() ?? string.Empty).ToArray();
        var current = node?.Outputs.ToArray() ?? [];
        var normalized = requested.Where(name => name.Length > 0).ToArray();
        var invalid = node is null || node.CanonicalType != "DialogueExitBranch"
            ? "The selected node is not a DialogueExitBranch."
            : requested.Any(name => !IsRuntimeDynamicOutputName(name))
                ? "Dialogue exit names must be lowercase ASCII letters, digits, or underscores."
                : requested.Distinct(StringComparer.Ordinal).Count() != requested.Length
                    ? "Dialogue exit names must be ordinal-unique."
                    : requested.Any(name => name.Length == 0)
                        ? "Dialogue exit names cannot be empty."
                        : null;
        var removed = current.Where(name => !normalized.Contains(name, StringComparer.Ordinal)).ToArray();
        var connected = node is null
            ? []
            : Connections.Where(connection => connection.From == node.Id && removed.Contains(connection.Output, StringComparer.Ordinal)).ToArray();
        var added = normalized.Where(name => !current.Contains(name, StringComparer.Ordinal)).ToArray();
        var removedAt = removed.Select(name => Array.IndexOf(current, name)).Where(index => index >= 0).ToArray();
        var addedAt = added.Select(name => Array.IndexOf(normalized, name)).Where(index => index >= 0).ToArray();
        DialogueExitRenameCandidate? candidate = null;
        if (removed.Length == 1 && added.Length == 1 && removedAt.Length == 1 && addedAt.Length == 1 && removedAt[0] == addedAt[0])
            candidate = new(removed[0], added[0], removedAt[0]);
        var conflict = candidate is not null && connected.Any(connection =>
            string.Equals(connection.Output, candidate.NewOutput, StringComparison.Ordinal) &&
            !string.Equals(connection.Output, candidate.OldOutput, StringComparison.Ordinal));
        return new(
            nodeId,
            current,
            normalized,
            invalid is null,
            invalid,
            removed,
            connected,
            candidate,
            conflict,
            removed.Length == 0 && current.SequenceEqual(normalized, StringComparer.Ordinal));
    }

    /// <summary>Applies one analyzed exit-name edit. false is a cancellation,
    /// invalid plan, or an occupied migration target; no state is changed.</summary>
    public bool ApplyDialogueExitNameChange(DialogueExitNameChangePlan plan) =>
        ApplyDialogueExitNameChange(plan, plan.RenameCandidate is not null, plan.RemovedConnectedOutputs.Count == 0 || plan.RenameCandidate is not null);

    public bool ApplyDialogueExitNameChange(DialogueExitNameChangePlan plan, bool migrateConnectedOutput, bool confirmConnectedDeletion = true)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid || plan.IsNoOp || !confirmConnectedDeletion) return false;
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == plan.NodeId);
        if (node is null || node.CanonicalType != "DialogueExitBranch" ||
            !node.Outputs.SequenceEqual(plan.CurrentNames, StringComparer.Ordinal) ||
            !plan.RequestedNames.SequenceEqual(plan.NormalizedRequestedNames, StringComparer.Ordinal)) return false;
        if (plan.HasMigrationConflict) return false;
        if (migrateConnectedOutput && plan.RenameCandidate is not null &&
            GetConnection(node.Id, plan.RenameCandidate.NewOutput) is not null) return false;

        ApplyEdit(() =>
        {
            node.SetPropertyCore("exit_names", string.Join(", ", plan.NormalizedRequestedNames));
            if (plan.RenameCandidate is not null && migrateConnectedOutput)
            {
                var rename = plan.RenameCandidate;
                for (var index = Connections.Count - 1; index >= 0; index--)
                    if (Connections[index].From == node.Id && Connections[index].Output == rename.OldOutput)
                    {
                        var connection = Connections[index];
                        Connections[index] = connection with { Output = rename.NewOutput };
                    }
            }
            else if (plan.RemovedConnectedOutputs.Count > 0)
            {
                for (var index = Connections.Count - 1; index >= 0; index--)
                    if (Connections[index].From == node.Id && plan.RemovedOutputs.Contains(Connections[index].Output, StringComparer.Ordinal))
                        Connections.RemoveAt(index);
            }
            RefreshNodeOutputs(node.Id);
        });
        return true;
    }

    public bool ApplyDialogueExitNameChange(DialogueExitNameChangePlan plan, DialogueExitOutputDisposition disposition) =>
        ApplyDialogueExitNameChange(plan, disposition == DialogueExitOutputDisposition.Migrate, disposition != DialogueExitOutputDisposition.Cancel);

    // Plan terminology and menu terminology are both useful to callers.
    public DialogueExitNameChangePlan AnalyzeDynamicOutputChange(StoryFlowNodeEditorItem node, IEnumerable<string> requestedNames) =>
        AnalyzeDialogueExitNames(node, requestedNames);
    public bool RenameDynamicOutput(StoryFlowNodeEditorItem node, string oldOutput, string newOutput, bool migrateConnection = true)
    {
        if (node.CanonicalType != "DialogueExitBranch" || !node.Outputs.Contains(oldOutput, StringComparer.Ordinal) ||
            (GetConnection(node.Id, newOutput.Trim()) is not null && !string.Equals(oldOutput, newOutput.Trim(), StringComparison.Ordinal))) return false;
        var names = node.Outputs.Select(output => string.Equals(output, oldOutput, StringComparison.Ordinal) ? newOutput : output);
        var plan = AnalyzeDialogueExitNames(node, names);
        return ApplyDialogueExitNameChange(plan, migrateConnection);
    }

    public bool RenameDynamicOutput(string nodeId, string oldOutput, string newOutput, bool migrateConnection = true) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId) is { } node && RenameDynamicOutput(node, oldOutput, newOutput, migrateConnection);

    public bool RemoveDynamicOutput(StoryFlowNodeEditorItem node, string output, bool removeConnection = false)
    {
        var plan = AnalyzeDialogueExitNames(node, node.Outputs.Where(candidate => !string.Equals(candidate, output, StringComparison.Ordinal)));
        return ApplyDialogueExitNameChange(plan, migrateConnectedOutput: false, confirmConnectedDeletion: removeConnection || plan.RemovedConnectedOutputs.Count == 0);
    }

    public bool RemoveDynamicOutput(string nodeId, string output, bool removeConnection = false) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId) is { } node && RemoveDynamicOutput(node, output, removeConnection);

    public string GetNextSequenceOutput(StoryFlowNodeEditorItem node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.CanonicalType != "Sequence") return string.Empty;
        var numbers = node.Outputs.Select(ParsePositiveOutput).Where(number => number > 0).ToArray();
        var next = numbers.Length == 0 ? 1 : numbers.Max() + 1;
        return next.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public string GetNextSequenceOutput(string nodeId) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId) is { } node ? GetNextSequenceOutput(node) : string.Empty;

    public SequenceStepChangePlan AnalyzeSequenceStepAddition(StoryFlowNodeEditorItem node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var output = GetNextSequenceOutput(node);
        return new(node.Id, output, node.CanonicalType == "Sequence" && output.Length > 0,
            node.CanonicalType == "Sequence" ? null : "The selected node is not a Sequence.", false);
    }

    public SequenceStepChangePlan AnalyzeSequenceStepAddition(string nodeId) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId) is { } node
            ? AnalyzeSequenceStepAddition(node)
            : new(nodeId, string.Empty, false, "Sequence node was not found.", false);

    public SequenceStepChangePlan AnalyzeSequenceStepRemoval(StoryFlowNodeEditorItem node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var numericOutputs = node.CanonicalType == "Sequence"
            ? node.Outputs.Select((value, index) => (value, number: ParsePositiveOutput(value), index))
                .Where(item => item.number > 0).ToArray()
            : [];
        var output = numericOutputs.OrderByDescending(item => item.number).ThenByDescending(item => item.index)
            .Select(item => item.value).FirstOrDefault() ?? string.Empty;
        var connection = output.Length == 0 ? null : GetConnection(node.Id, output);
        return new(node.Id, output, node.CanonicalType == "Sequence" && numericOutputs.Length > 1,
            connection is not null ? "The last Sequence output is connected." : null, connection is not null);
    }

    public SequenceStepChangePlan AnalyzeSequenceStepRemoval(string nodeId) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId) is { } node
            ? AnalyzeSequenceStepRemoval(node)
            : new(nodeId, string.Empty, false, "Sequence node was not found.", false);

    public bool AddSequenceStep(StoryFlowNodeEditorItem node) => AddSequenceStep(AnalyzeSequenceStepAddition(node));
    public bool AddSequenceStep(string nodeId) => AddSequenceStep(AnalyzeSequenceStepAddition(nodeId));
    public bool AddSequenceStep(SequenceStepChangePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid || plan.Output.Length == 0) return false;
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == plan.NodeId);
        if (node is null || node.CanonicalType != "Sequence" || GetNextSequenceOutput(node) != plan.Output) return false;
        var number = ParsePositiveOutput(plan.Output);
        ApplyEdit(() => node.SetPropertyCore("step_count", number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return true;
    }

    public bool RemoveSequenceStep(StoryFlowNodeEditorItem node, bool allowConnectedRemoval = false) =>
        RemoveSequenceStep(AnalyzeSequenceStepRemoval(node), allowConnectedRemoval);

    public bool RemoveSequenceStep(string nodeId, bool allowConnectedRemoval = false) =>
        RemoveSequenceStep(AnalyzeSequenceStepRemoval(nodeId), allowConnectedRemoval);

    public bool RemoveSequenceStep(SequenceStepChangePlan plan, bool allowConnectedRemoval = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid || plan.Output.Length == 0 || (plan.IsConnected && !allowConnectedRemoval)) return false;
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == plan.NodeId);
        if (node is null || node.CanonicalType != "Sequence") return false;
        var current = AnalyzeSequenceStepRemoval(node);
        if (current.Output != plan.Output || (current.IsConnected && !allowConnectedRemoval)) return false;
        ApplyEdit(() =>
        {
            if (current.IsConnected) RemoveConnectionsForOutput(node.Id, current.Output);
            var configuredCount = ReadSequenceCount(node);
            if (configuredCount == ParsePositiveOutput(current.Output) && configuredCount > 1)
                node.SetPropertyCore("step_count", (configuredCount - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            RefreshNodeOutputs(node.Id);
        });
        return true;
    }

    /// <summary>
    /// Validates an endpoint pair without changing the document or editor state.
    /// Output names are persisted exactly as supplied by the caller.
    /// </summary>
    public bool ValidateConnection(string fromNodeId, string output, string toNodeId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(toNodeId)) return false;
        var source = Nodes.FirstOrDefault(node => string.Equals(node.Id, fromNodeId, StringComparison.Ordinal));
        var target = Nodes.FirstOrDefault(node => string.Equals(node.Id, toNodeId, StringComparison.Ordinal));
        return source is not null && target is not null && !ReferenceEquals(source, target) && target.AllowsInput &&
               source.Outputs.Contains(output, StringComparer.Ordinal);
    }

    // Naming aliases keep the side-effect-free contract discoverable to callers that
    // use capability-style naming instead of the validator's verb.
    public bool CanConnect(string fromNodeId, string output, string toNodeId) => ValidateConnection(fromNodeId, output, toNodeId);
    public bool IsConnectionValid(string fromNodeId, string output, string toNodeId) => ValidateConnection(fromNodeId, output, toNodeId);

    public bool Connect(string fromNodeId, string output, string toNodeId)
    {
        if (!ValidateConnection(fromNodeId, output, toNodeId)) return false;
        var existing = GetConnection(fromNodeId, output);
        if (existing is not null && string.Equals(existing.To, toNodeId, StringComparison.Ordinal)
            && Connections.Count(connection => string.Equals(connection.From, fromNodeId, StringComparison.Ordinal)
                                             && string.Equals(connection.Output, output, StringComparison.Ordinal)) == 1)
            return false;

        var changed = false;
        ApplyEdit(() =>
        {
            RemoveConnectionsForOutput(fromNodeId, output);
            Connections.Add(new(fromNodeId, output, toNodeId));
            RefreshNodeOutputs(fromNodeId);
            changed = true;
        });
        return changed;
    }

    /// <summary>
    /// Replaces one concrete existing connection in one undoable edit. If the new
    /// source output is occupied, that connection is deterministically replaced by
    /// the requested connection, preserving the one-output cardinality invariant.
    /// </summary>
    public bool ReconnectConnection(StoryFlowConnectionEditorItem? originalConnection, string newFromNodeId, string newOutput, string newToNodeId)
    {
        if (originalConnection is null) return false;
        var originalIndex = FindConnectionIndex(originalConnection);
        if (originalIndex < 0 ||
            (string.Equals(originalConnection.From, newFromNodeId, StringComparison.Ordinal) &&
             string.Equals(originalConnection.Output, newOutput, StringComparison.Ordinal) &&
             string.Equals(originalConnection.To, newToNodeId, StringComparison.Ordinal)) ||
            !ValidateConnection(newFromNodeId, newOutput, newToNodeId)) return false;

        var changed = false;
        ApplyEdit(() =>
        {
            var affectedSources = new HashSet<string>(StringComparer.Ordinal) { originalConnection.From, newFromNodeId };
            // Remove exactly the selected original first. The second pass also
            // removes any other connection occupying the requested output.
            Connections.RemoveAt(originalIndex);
            RemoveConnectionsForOutput(newFromNodeId, newOutput);
            Connections.Add(new(newFromNodeId, newOutput, newToNodeId));
            foreach (var sourceId in affectedSources) RefreshNodeOutputs(sourceId);
            changed = true;
        });
        return changed;
    }

    public bool ReconnectConnection(StoryFlowConnectionEditorItem? originalConnection, string newToNodeId) =>
        originalConnection is not null && ReconnectConnection(originalConnection, originalConnection.From, originalConnection.Output, newToNodeId);

    public bool ReconnectConnection(
        string originalFromNodeId, string originalOutput, string originalToNodeId,
        string newFromNodeId, string newOutput, string newToNodeId)
    {
        var original = GetConnection(originalFromNodeId, originalOutput);
        return original is not null && string.Equals(original.To, originalToNodeId, StringComparison.Ordinal) &&
               ReconnectConnection(original, newFromNodeId, newOutput, newToNodeId);
    }

    public void RemoveConnection(StoryFlowConnectionEditorItem connection) => ApplyEdit(() =>
    {
        if (Connections.Remove(connection)) RefreshNodeOutputs(connection.From);
    });

    public StoryFlowNodeEditorItem AddNodeAt(string canonicalType, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x), "Node coordinates must be finite.");
        var definition = StoryNodeDefinitionRegistry.Get(canonicalType)
            ?? throw new ArgumentException($"Unsupported Story node type '{canonicalType}'.", nameof(canonicalType));
        StoryFlowNodeEditorItem? created = null;
        ApplyEdit(() =>
        {
            var id = AllocateNodeId(TypeBaseId(definition.CanonicalType));
            created = new StoryFlowNodeEditorItem(id, definition.CanonicalType, x, y, DefaultProperties(definition), ApplyEdit, RenameNode);
            Nodes.Add(created);
            SelectOnly(created);
        });
        return created!;
    }

    public void DisconnectNode(string nodeId) => ApplyEdit(() =>
    {
        var affectedSources = new HashSet<string>(StringComparer.Ordinal);
        for (var index = Connections.Count - 1; index >= 0; index--)
            if (Connections[index].From == nodeId || Connections[index].To == nodeId)
            {
                affectedSources.Add(Connections[index].From);
                Connections.RemoveAt(index);
            }
        foreach (var sourceId in affectedSources) RefreshNodeOutputs(sourceId);
    });

    public void DuplicateSelection()
    {
        CopySelection();
        Paste();
    }

    public void SelectAll()
    {
        foreach (var node in Nodes) node.IsSelected = true;
        SelectedNode = Nodes.LastOrDefault();
        RaiseSelectionStates();
    }

    public IReadOnlyList<ValidationIssue> GetNodeIssues(string nodeId) => ValidationIssues
        .Where(issue => string.Equals(issue.NodeId, nodeId, StringComparison.Ordinal))
        .ToArray();

    public bool RequestProblemFocus(string nodeId, string? field)
    {
        var node = Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node is null) return false;
        SelectOnly(node);
        ProblemFocusRequest = new(nodeId, field, ++_problemFocusSequence);
        return true;
    }

    public bool AddResourceAsReference(StoryFlowNodeEditorItem node)
    {
        var id = node.ResourceValue;
        if (id.Length == 0 || IsResourceInStoryMembership(node, id)) return false;
        var projectIds = node.CanonicalType switch
        {
            "ActorInteract" => ActorIds,
            "PlayDialogue" => DialogueIds,
            "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => QuestIds,
            _ => [],
        };
        if (!projectIds.Contains(id, StringComparer.Ordinal)) return false;
        ApplyEdit(() =>
        {
            var resource = Document.ToResource();
            switch (node.CanonicalType)
            {
                case "ActorInteract":
                    resource.ReferencedResources.Actors.Add(id);
                    _storyActorIds.Add(id);
                    break;
                case "PlayDialogue":
                    resource.ReferencedResources.Dialogues.Add(id);
                    _storyDialogueIds.Add(id);
                    break;
                case "StartQuest":
                case "WaitQuestComplete":
                case "CompleteQuest":
                case "QuestState":
                    resource.ReferencedResources.Quests.Add(id);
                    _storyQuestIds.Add(id);
                    break;
            }
            Document.Replace(resource);
        });
        RaiseDocumentState();
        return true;
    }

    private void AddNode(string canonicalType)
    {
        AddNodeAt(canonicalType, 120 - PanX / Zoom, 100 - PanY / Zoom);
    }

    private Dictionary<string, string> DefaultProperties(StoryNodeDefinition definition)
    {
        var properties = definition.DefaultProperties
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);
        switch (definition.CanonicalType)
        {
            case "ActorInteract": properties["actor_id"] = ActorIds.FirstOrDefault() ?? string.Empty; break;
            case "PlayDialogue": properties["dialogue_id"] = DialogueIds.FirstOrDefault() ?? string.Empty; break;
            case "StartQuest":
            case "WaitQuestComplete":
            case "CompleteQuest":
            case "QuestState": properties["quest_id"] = QuestIds.FirstOrDefault() ?? string.Empty; break;
            case "EnterStory": properties["target_story_id"] = StoryIds.FirstOrDefault(id => id != Document.Id) ?? string.Empty; break;
        }
        return properties;
    }

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
            RefreshAllNodeOutputs();
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
            RefreshAllNodeOutputs();
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
        var existingById = Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var desiredNodes = new List<StoryFlowNodeEditorItem>(resource.Nodes.Count);
        foreach (var nodeResource in resource.Nodes)
        {
            var outputs = resource.Connections
                .Where(connection => connection.From == nodeResource.Id)
                .Select(connection => connection.Output);
            if (existingById.TryGetValue(nodeResource.Id, out var existing) && existing.CanRestoreFrom(nodeResource))
            {
                existing.RestoreFrom(nodeResource, outputs);
                desiredNodes.Add(existing);
            }
            else
            {
                var editor = StoryFlowNodeEditorItem.FromResource(nodeResource, ApplyEdit, RenameNode);
                editor.SetLoadedOutputs(outputs);
                desiredNodes.Add(editor);
            }
        }

        for (var index = Nodes.Count - 1; index >= 0; index--)
            if (!desiredNodes.Contains(Nodes[index])) Nodes.RemoveAt(index);
        for (var index = 0; index < desiredNodes.Count; index++)
        {
            var desired = desiredNodes[index];
            if (index < Nodes.Count && ReferenceEquals(Nodes[index], desired)) continue;
            var currentIndex = Nodes.IndexOf(desired);
            if (currentIndex >= 0) Nodes.Move(currentIndex, index);
            else Nodes.Insert(index, desired);
        }

        var desiredConnections = resource.Connections
            .Select(connection => new StoryFlowConnectionEditorItem(connection.From, connection.Output, connection.To))
            .ToArray();
        if (!Connections.SequenceEqual(desiredConnections))
        {
            Connections.Clear();
            foreach (var connection in desiredConnections) Connections.Add(connection);
        }
        SelectedNode = null;
        OnPropertyChanged(nameof(Summary));
    }

    private void RefreshNodeOutputs(string nodeId)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == nodeId);
        if (node is not null)
            node.SetLoadedOutputs(Connections.Where(connection => connection.From == nodeId).Select(connection => connection.Output));
    }

    private int FindConnectionIndex(StoryFlowConnectionEditorItem connection)
    {
        for (var index = 0; index < Connections.Count; index++)
            if (ReferenceEquals(Connections[index], connection)) return index;
        for (var index = 0; index < Connections.Count; index++)
            if (Connections[index].Equals(connection)) return index;
        return -1;
    }

    private void RemoveConnectionsForOutput(string fromNodeId, string output)
    {
        for (var index = Connections.Count - 1; index >= 0; index--)
            if (string.Equals(Connections[index].From, fromNodeId, StringComparison.Ordinal) &&
                string.Equals(Connections[index].Output, output, StringComparison.Ordinal))
                Connections.RemoveAt(index);
    }

    private void RefreshAllNodeOutputs()
    {
        foreach (var node in Nodes) RefreshNodeOutputs(node.Id);
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
            var (key, values, memberships, code, label) = node.CanonicalType switch
            {
                "ActorInteract" => ("actor_id", ActorIds, StoryActorIds, "story.flow.actor.missing", "Actor"),
                "PlayDialogue" => ("dialogue_id", DialogueIds, StoryDialogueIds, "story.flow.dialogue.missing", "Dialogue"),
                "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => ("quest_id", QuestIds, StoryQuestIds, "story.flow.quest.missing", "Quest"),
                "EnterStory" => ("target_story_id", StoryIds, (IReadOnlySet<string>)StoryIds.ToHashSet(StringComparer.Ordinal), "story.flow.story.missing", "Story"),
                _ => (string.Empty, (IReadOnlyList<string>)[], (IReadOnlySet<string>)new HashSet<string>(), string.Empty, string.Empty),
            };
            if (key.Length == 0) continue;
            var value = node.GetProperty(key);
            if (!values.Contains(value, StringComparer.Ordinal))
                issues.Add(new(code, $"节点 '{node.Id}' 引用的 {label} '{value}' 不存在。", key, NodeId: node.Id));
            else if (!memberships.Contains(value))
                issues.Add(new(
                    code.Replace(".missing", ".membership", StringComparison.Ordinal),
                    $"节点 '{node.Id}' 引用的 {label} '{value}' 存在于项目中，但尚未加入当前 Story Membership。",
                    key,
                    ValidationSeverity.Warning,
                    node.Id));
        }
        return issues;
    }

    private static IReadOnlyList<FlowResourceCandidate> GroupCandidates(IReadOnlyList<string> projectIds, IReadOnlySet<string> membershipIds) =>
        projectIds
            .OrderBy(id => membershipIds.Contains(id) ? 0 : 1)
            .ThenBy(id => id, StringComparer.Ordinal)
            .Select(id => new FlowResourceCandidate(id, membershipIds.Contains(id) ? "当前 Story 资源" : "项目中的其他资源", membershipIds.Contains(id)))
            .ToArray();

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

    private static readonly Regex RuntimeDynamicOutputPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool IsRuntimeDynamicOutputName(string value) => value.Length > 0 && RuntimeDynamicOutputPattern.IsMatch(value);

    private static IReadOnlyList<string> ParseDynamicNames(string value) =>
        (value ?? string.Empty).Split([',', '，', ';', '；', '\n'], StringSplitOptions.None)
            .Select(name => name.Trim()).ToArray();

    private static int ParsePositiveOutput(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0 ? number : 0;

    private static int ReadSequenceCount(StoryFlowNodeEditorItem node)
    {
        foreach (var key in new[] { "step_count", "stepCount", "count" })
            if (int.TryParse(node.GetProperty(key), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count))
                return Math.Max(0, count);
        var steps = node.GetProperty("steps");
        return steps.Length == 0 ? 0 : steps.Split([',', '，', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

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
        OnPropertyChanged(nameof(ErrorCount)); OnPropertyChanged(nameof(WarningCount));
    }

    private sealed record FlowSnapshot(StoryResource Resource, IReadOnlyList<string> SelectedIds);
    private sealed record FlowClipboard(IReadOnlyList<StoryNodeResource> Nodes, IReadOnlyList<StoryConnectionResource> Connections);
}

public sealed record FlowProblemFocusRequest(string NodeId, string? Field, long Sequence);

public sealed class StoryFlowNodeEditorItem : ObservableObject
{
    public const double Width = 228;
    public const double Height = 116;
    private readonly Action<Action> _applyEdit;
    private readonly Action<StoryFlowNodeEditorItem, string>? _renameNode;
    private readonly string _persistedType;
    private string _id;
    private double _x;
    private double _y;
    private bool _isSelected;
    private readonly Dictionary<string, string> _properties;
    private readonly StoryNodeDefinition? _definition;
    private readonly HashSet<string> _loadedOutputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoryFlowPropertyEditorItem> _propertyEditors = new(StringComparer.Ordinal);

    private StoryFlowNodeEditorItem(string id, string canonicalType, string persistedType, double x, double y, Dictionary<string, string> properties, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode)
    {
        _id = id;
        CanonicalType = canonicalType;
        _persistedType = persistedType;
        _x = x;
        _y = y;
        _properties = properties;
        _applyEdit = applyEdit;
        _renameNode = renameNode;
        _definition = StoryNodeDefinitionRegistry.Get(canonicalType);
        RebuildPropertyEditors();
    }

    public StoryFlowNodeEditorItem(string id, string canonicalType, double x, double y, Dictionary<string, string> properties, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode = null)
        : this(id, canonicalType, PersistedTypeFor(canonicalType), x, y, properties, applyEdit, renameNode) { }

    public static StoryFlowNodeEditorItem FromResource(StoryNodeResource resource, Action<Action> applyEdit, Action<StoryFlowNodeEditorItem, string>? renameNode = null, string? id = null, double offsetX = 0, double offsetY = 0)
    {
        var type = StoryValidator.CanonicalizeType(resource.Type) ?? resource.Type;
        var properties = (resource.Properties ?? []).ToDictionary(pair => pair.Key, pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : pair.Value.GetRawText(), StringComparer.Ordinal);
        return new(id ?? resource.Id, type, resource.Type, resource.Position.X + offsetX, resource.Position.Y + offsetY, properties, applyEdit, renameNode);
    }

    internal bool CanRestoreFrom(StoryNodeResource resource) =>
        string.Equals(CanonicalType, StoryValidator.CanonicalizeType(resource.Type) ?? resource.Type, StringComparison.Ordinal);

    internal void RestoreFrom(StoryNodeResource resource, IEnumerable<string> loadedOutputs)
    {
        if (!CanRestoreFrom(resource)) throw new InvalidOperationException("Cannot restore a different node type into an existing editor item.");

        if (_x != resource.Position.X || _y != resource.Position.Y) SetPosition(resource.Position.X, resource.Position.Y);

        var nextProperties = (resource.Properties ?? []).ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : pair.Value.GetRawText(),
            StringComparer.Ordinal);
        var propertiesChanged = _properties.Count != nextProperties.Count ||
                                _properties.Any(pair => !nextProperties.TryGetValue(pair.Key, out var value) || value != pair.Value);
        if (propertiesChanged)
        {
            var keysChanged = !_properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(nextProperties.Keys);
            _properties.Clear();
            foreach (var pair in nextProperties) _properties[pair.Key] = pair.Value;
            if (keysChanged) RebuildPropertyEditors();
            else
                foreach (var pair in _propertyEditors)
                    if (nextProperties.TryGetValue(pair.Key, out var value)) pair.Value.SetValueCore(value);
            OnPropertyChanged(nameof(ResourceValue));
            OnPropertyChanged(nameof(ExitNames));
            OnPropertyChanged(nameof(Outputs));
            OnPropertyChanged(nameof(ParameterCount));
            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(ParameterHeader));
            OnPropertyChanged(nameof(ParameterAutomationName));
        }
        SetLoadedOutputs(loadedOutputs);
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
    public string TypeLabel => _definition?.DisplayName ?? CanonicalType;
    public string Accent => CanonicalType switch { "StoryStart" => "#4CAF50", "ActorInteract" => "#42A5F5", "PlayDialogue" or "DialogueExitBranch" => "#AB47BC", "StartQuest" or "WaitQuestComplete" => "#FFA726", "EnterStory" or "EndStory" => "#26A69A", _ => "#78909C" };
    public double X => _x;
    public double Y => _y;
    public bool AllowsInput => _definition?.AllowsInput ?? true;
    public int ParameterCount => _properties.Count;
    public bool HasParameters => ParameterCount > 0;
    public bool HasCoreParameters => _definition?.CoreProperties.Count > 0;
    /// <summary>Registry-defined and persisted core properties, in registry order.</summary>
    public ObservableCollection<StoryFlowPropertyEditorItem> CoreProperties { get; } = [];
    /// <summary>Registry-defined advanced properties followed by unknown persisted properties in ordinal key order.</summary>
    public ObservableCollection<StoryFlowPropertyEditorItem> AdvancedProperties { get; } = [];
    public IReadOnlyList<StoryFlowPropertyEditorItem> PropertyEditors => [.. CoreProperties, .. AdvancedProperties];
    public ObservableCollection<StoryFlowPropertyEditorItem> CorePropertyEditors => CoreProperties;
    public ObservableCollection<StoryFlowPropertyEditorItem> AdvancedPropertyEditors => AdvancedProperties;
    public int PropertyEditorCount => CoreProperties.Count + AdvancedProperties.Count;
    public string ParameterHeader
    {
        get
        {
            if (!HasParameters) return "参数（0）";
            var definitions = (_definition?.Properties ?? [])
                .ToDictionary(property => property.Name, StringComparer.Ordinal);
            var orderedKeys = (_definition?.Properties.Select(property => property.Name) ?? [])
                .Concat(_properties.Keys.Where(key => !definitions.ContainsKey(key)).OrderBy(key => key, StringComparer.Ordinal));
            var summary = orderedKeys
                .Distinct(StringComparer.Ordinal)
                .Select(key =>
                {
                    var value = GetProperty(key);
                    if (value.Length > 18) value = value[..17] + "…";
                    return value.Length > 0 ? value : definitions.TryGetValue(key, out var property) ? property.DisplayName : key;
                })
                .Take(2)
                .ToArray();
            return summary.Length == 0 ? $"参数（{ParameterCount}）" : $"参数（{ParameterCount}） · {string.Join(" / ", summary)}";
        }
    }
    public string ParameterAutomationName => $"{Id} 参数：{ParameterHeader}";
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string ResourceValue
    {
        get => GetProperty(ResourceKey);
        set { if (ResourceKey.Length != 0) SetPropertyValue(ResourceKey, value ?? string.Empty); }
    }
    public string ResourceKey => CanonicalType switch { "ActorInteract" => "actor_id", "PlayDialogue" => "dialogue_id", "StartQuest" or "WaitQuestComplete" or "CompleteQuest" or "QuestState" => "quest_id", "EnterStory" => "target_story_id", _ => string.Empty };
    public string ExitNames { get => GetProperty("exit_names"); set => SetPropertyValue("exit_names", value ?? string.Empty); }
    public IReadOnlyList<string> Outputs => _definition is null
        ? _loadedOutputs.ToArray()
        : StoryNodeDefinitionRegistry.ResolveOutputs(CanonicalType, _properties, _loadedOutputs);

    public string GetProperty(string key) => key.Length != 0 && _properties.TryGetValue(key, out var value) ? value : string.Empty;
    public void SetPosition(double x, double y) { _x = x; _y = y; OnPropertyChanged(nameof(X)); OnPropertyChanged(nameof(Y)); }
    private void SetPropertyValue(string key, string value)
    {
        if (GetProperty(key) == value) return;
        _applyEdit(() =>
        {
            _properties[key] = value;
            if (!_propertyEditors.ContainsKey(key) && !IsSpecializedProperty(key)) RebuildPropertyEditors();
            else if (_propertyEditors.TryGetValue(key, out var editor)) editor.SetValueCore(value);
            OnPropertyChanged(nameof(ResourceValue));
            OnPropertyChanged(nameof(ExitNames));
            OnPropertyChanged(nameof(Outputs));
            OnPropertyChanged(nameof(ParameterCount));
            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(ParameterHeader));
            OnPropertyChanged(nameof(ParameterAutomationName));
        });
    }
    private void SetEdited(string current, string value, Action<string> assign, string propertyName)
    { if (current == value) return; _applyEdit(() => { assign(value); OnPropertyChanged(propertyName); }); }
    internal void SetIdCore(string value) { _id = value; OnPropertyChanged(nameof(Id)); OnPropertyChanged(nameof(ParameterAutomationName)); }
    internal void SetPropertyCore(string key, string value)
    {
        _properties[key] = value;
        if (!_propertyEditors.ContainsKey(key) && !IsSpecializedProperty(key)) RebuildPropertyEditors();
        else if (_propertyEditors.TryGetValue(key, out var editor)) editor.SetValueCore(value);
        OnPropertyChanged(nameof(ResourceValue));
        OnPropertyChanged(nameof(ExitNames));
        OnPropertyChanged(nameof(Outputs));
        OnPropertyChanged(nameof(ParameterCount));
        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(ParameterHeader));
        OnPropertyChanged(nameof(ParameterAutomationName));
    }
    internal void SetLoadedOutputs(IEnumerable<string> outputs)
    {
        var next = outputs.Where(output => !string.IsNullOrWhiteSpace(output)).ToHashSet(StringComparer.Ordinal);
        if (_loadedOutputs.SetEquals(next)) return;
        _loadedOutputs.Clear();
        _loadedOutputs.UnionWith(next);
        OnPropertyChanged(nameof(Outputs));
    }

    private void RebuildPropertyEditors()
    {
        CoreProperties.Clear();
        AdvancedProperties.Clear();
        _propertyEditors.Clear();

        var knownKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in _definition?.Properties ?? [])
        {
            if (IsSpecializedProperty(definition.Name))
            {
                knownKeys.Add(definition.Name);
                foreach (var alias in definition.Aliases) knownKeys.Add(alias);
                continue;
            }
            var persistedKey = FindPersistedKey(definition);
            var row = new StoryFlowPropertyEditorItem(
                definition.Name,
                definition.DisplayName,
                definition.IsCore,
                isKnown: true,
                persistedKey,
                persistedKey is not null ? _properties[persistedKey] : definition.DefaultValue ?? string.Empty,
                SetPropertyFromEditor,
                definition);
            _propertyEditors[definition.Name] = row;
            knownKeys.Add(definition.Name);
            foreach (var alias in definition.Aliases) knownKeys.Add(alias);
            (definition.IsCore ? CoreProperties : AdvancedProperties).Add(row);
        }

        foreach (var key in _properties.Keys
                     .Where(key => !knownKeys.Contains(key) && !IsSpecializedProperty(key))
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            var row = new StoryFlowPropertyEditorItem(
                key,
                key,
                isCore: false,
                isKnown: false,
                key,
                _properties[key],
                SetPropertyFromEditor,
                definition: null);
            _propertyEditors[key] = row;
            AdvancedProperties.Add(row);
        }

        OnPropertyChanged(nameof(PropertyEditors));
        OnPropertyChanged(nameof(CorePropertyEditors));
        OnPropertyChanged(nameof(AdvancedPropertyEditors));
        OnPropertyChanged(nameof(PropertyEditorCount));
    }

    private string? FindPersistedKey(StoryPropertyDefinition definition)
    {
        if (_properties.ContainsKey(definition.Name)) return definition.Name;
        return definition.Aliases.FirstOrDefault(_properties.ContainsKey);
    }

    private void SetPropertyFromEditor(StoryFlowPropertyEditorItem editor, string value)
    {
        var storageKey = editor.StorageKey;
        if (GetProperty(storageKey) == value) return;
        _applyEdit(() =>
        {
            _properties[storageKey] = value;
            editor.SetValueCore(value);
            OnPropertyChanged(nameof(ResourceValue));
            OnPropertyChanged(nameof(ExitNames));
            OnPropertyChanged(nameof(Outputs));
            OnPropertyChanged(nameof(ParameterCount));
            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(ParameterHeader));
            OnPropertyChanged(nameof(ParameterAutomationName));
        });
    }

    private bool IsSpecializedProperty(string key) =>
        string.Equals(key, ResourceKey, StringComparison.Ordinal) ||
        string.Equals(key, "exit_names", StringComparison.Ordinal) ||
        string.Equals(key, "exitNames", StringComparison.Ordinal) ||
        (CanonicalType == "Sequence" && (key is "step_count" or "stepCount" or "count"));

    public StoryNodeResource ToResource() => new()
    {
        Id = Id, Type = _persistedType,
        Position = new StoryNodePosition { X = X, Y = Y },
        Properties = _properties.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal),
    };

    private static string PersistedTypeFor(string canonicalType) =>
        StoryNodeDefinitionRegistry.Get(canonicalType)?.PersistedType ?? canonicalType;
}

/// <summary>A bindable view-model row for one Story Flow node property.</summary>
public sealed class StoryFlowPropertyEditorItem : ObservableObject
{
    private readonly Action<StoryFlowPropertyEditorItem, string> _setValue;
    private string _value;

    internal StoryFlowPropertyEditorItem(
        string key,
        string label,
        bool isCore,
        bool isKnown,
        string? storageKey,
        string value,
        Action<StoryFlowPropertyEditorItem, string> setValue,
        StoryPropertyDefinition? definition)
    {
        Key = key;
        Label = label;
        IsCore = isCore;
        IsKnown = isKnown;
        StorageKey = storageKey ?? key;
        _value = value;
        _setValue = setValue;
        Definition = definition;
    }

    public string Key { get; }
    public string Name => Key;
    public string Label { get; }
    public string DisplayName => Label;
    public bool IsCore { get; }
    public bool IsAdvanced => !IsCore;
    public bool IsKnown { get; }
    public bool IsCompatibility => !IsKnown;
    public StoryPropertyDefinition? Definition { get; }
    public StoryPropertyDefinition? PropertyDefinition => Definition;
    public string Value
    {
        get => _value;
        set
        {
            var next = value ?? string.Empty;
            if (_value == next) return;
            _setValue(this, next);
        }
    }

    // The canonical Key is the public/editing key; this retains a legacy
    // alias as the actual persisted key when loading compatibility data.
    internal string StorageKey { get; }

    internal void SetValueCore(string value)
    {
        if (_value == value) return;
        _value = value;
        OnPropertyChanged(nameof(Value));
    }
}

public sealed record StoryFlowConnectionEditorItem(string From, string Output, string To)
{
    public StoryConnectionResource ToResource() => new() { From = From, Output = Output, To = To };
}

public sealed record FlowResourceCandidate(string Id, string Group, bool IsInStoryMembership);

public enum DialogueExitOutputDisposition
{
    Cancel,
    Migrate,
    Delete,
}

public sealed record DialogueExitRenameCandidate(string OldOutput, string NewOutput, int Position)
{
    public string OldName => OldOutput;
    public string NewName => NewOutput;
    public int Index => Position;
}

public sealed record DialogueExitNameChangePlan(
    string NodeId,
    IReadOnlyList<string> CurrentNames,
    IReadOnlyList<string> NormalizedRequestedNames,
    bool IsValid,
    string? ValidationError,
    IReadOnlyList<string> RemovedOutputs,
    IReadOnlyList<StoryFlowConnectionEditorItem> RemovedConnectedOutputs,
    DialogueExitRenameCandidate? RenameCandidate,
    bool HasMigrationConflict,
    bool IsNoOp)
{
    public IReadOnlyList<string> RequestedNames => NormalizedRequestedNames;
    public IReadOnlyList<string> RemovedConnectedOutputNames => RemovedConnectedOutputs.Select(connection => connection.Output).Distinct(StringComparer.Ordinal).ToArray();
    public bool RequiresConnectedOutputDecision => RemovedConnectedOutputs.Count > 0;
    public bool CanMigrate => IsValid && !HasMigrationConflict && RenameCandidate is not null;
}

public sealed record SequenceStepChangePlan(
    string NodeId,
    string Output,
    bool IsValid,
    string? ValidationError,
    bool IsConnected)
{
    public string NextOutput => Output;
    public string StepOutput => Output;
    public bool RequiresConnectedRemoval => IsConnected;
}
