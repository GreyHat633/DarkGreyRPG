using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>
/// Host-backed authoring projection for one selected graph node. It deliberately
/// exposes only author-facing Session fields; node and port identity stays with
/// the canonical graph and is never edited by this view model.
/// </summary>
public sealed class CanonicalNodeInspectorViewModel : ObservableObject, IDisposable
{
    private readonly GraphEditorHostViewModel _host;
    private readonly IReadOnlyList<CanonicalStoryActorItem> _actorItems;
    private string _lineText = string.Empty;
    private string _speakerActorId = string.Empty;
    private string _choicePrompt = string.Empty;
    private string _endDisplayName = string.Empty;
    private string _logicOutputDisplayName = string.Empty;
    private string _objectiveType = CanonicalTaskObjectiveSchema.KillEntity;
    private string _objectiveDescription = string.Empty;
    private string _objectiveRequiredText = string.Empty;
    private string _objectiveTarget = string.Empty;
    private string _objectiveActorId = string.Empty;
    private IReadOnlyList<CanonicalSessionSpeakerOption> _speakerOptions = [];
    private IReadOnlyList<CanonicalSessionSpeakerOption> _objectiveActorOptions = [];
    private CanonicalSessionSpeakerOption? _selectedSpeaker;
    private CanonicalSessionSpeakerOption? _selectedObjectiveActor;
    private bool _disposed;
    private string _repeatPolicy = StoryStartSchema.Once;
    private string _actionType = CanonicalStoryActionSchema.SendMessage;
    private string _actionItem = string.Empty;
    private string _actionMetadataText = string.Empty;
    private string _actionAmountText = string.Empty;
    private string _actionMessage = string.Empty;

    public CanonicalNodeInspectorViewModel(
        GraphEditorHostViewModel host,
        GraphEditorNodeViewModel node,
        IEnumerable<CanonicalStoryActorItem>? actorItems = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _actorItems = (actorItems ?? []).Where(item => item is not null).ToArray();
        AddChoiceOptionCommand = new RelayCommand(() => AddChoiceOption(), () => IsChoice);
        AddTaskResultSlotCommand = new RelayCommand(() => AddTaskResultSlot(), () => IsTaskSettle);
        AddStoryStartTriggerCommand = new RelayCommand(() => AddStoryStartTrigger(), () => IsStoryStart);
        RefreshFromHost();
        _host.GraphChanged += HostOnGraphChanged;
        _host.PropertyChanged += HostOnPropertyChanged;
    }

    public GraphEditorNodeViewModel Node { get; }
    public GraphEditorNodeViewModel SelectedNode => Node;
    public GraphEditorHostViewModel Host => _host;
    public string NodeId => Node.NodeId;
    public string NodeType => Node.Type;
    public string DisplayName => Node.DisplayName;
    public bool IsSessionNode => _host.Scope == GraphScope.Session;
    public bool IsTaskNode => _host.Scope == GraphScope.Task;
    public bool IsLine => IsSessionNode && string.Equals(NodeType, "line", StringComparison.Ordinal);
    public bool IsChoice => IsSessionNode && string.Equals(NodeType, "choice", StringComparison.Ordinal);
    public bool IsEnd => IsSessionNode && string.Equals(NodeType, "end", StringComparison.Ordinal);
    public bool IsLogicOutput => (IsSessionNode || IsTaskNode)
        && string.Equals(NodeType, "logic_output", StringComparison.Ordinal);
    public bool IsTaskSettle => IsTaskNode && string.Equals(NodeType, "settle", StringComparison.Ordinal);
    public bool IsStoryStart => _host.Scope == GraphScope.StoryFlow
        && string.Equals(NodeType, "start", StringComparison.Ordinal);
    public bool IsStoryAction => _host.Scope == GraphScope.StoryFlow
        && string.Equals(NodeType, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal);
    public bool IsGiveItemAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.GiveItem;
    public bool IsGiveXpAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.GiveXp;
    public bool IsSendMessageAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.SendMessage;
    public bool IsObjective => IsTaskNode && string.Equals(NodeType, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal);
    public bool IsKillEntityObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.KillEntity;
    public bool IsCollectItemObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.CollectItem;
    public bool IsInteractActorObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.InteractActor;
    public bool HasEditableFields => IsLine || IsChoice || IsEnd || IsLogicOutput || IsTaskSettle || IsObjective
        || IsStoryStart || IsStoryAction;

    public IReadOnlyList<CanonicalStoryActionTypeOption> StoryActionTypeOptions { get; } =
    [
        new(CanonicalStoryActionSchema.GiveItem, "给予物品"),
        new(CanonicalStoryActionSchema.GiveXp, "给予经验"),
        new(CanonicalStoryActionSchema.SendMessage, "发送消息"),
    ];

    public CanonicalStoryActionTypeOption? SelectedStoryActionType
    {
        get => StoryActionTypeOptions.FirstOrDefault(option => option.Value == _actionType);
        set
        {
            if (value is null || !IsStoryAction || value.Value == _actionType) return;
            _host.ChangeStoryActionType(NodeId, value.Value);
            RefreshFromHost();
        }
    }

    public string StoryActionType => _actionType;
    public string StoryActionItem
    {
        get => _actionItem;
        set => SetActionString(CanonicalStoryActionSchema.ItemProperty, value, ref _actionItem, nameof(StoryActionItem));
    }
    public string StoryActionMetadataText
    {
        get => _actionMetadataText;
        set => SetActionInteger(CanonicalStoryActionSchema.MetadataProperty, value, 0, ref _actionMetadataText,
            nameof(StoryActionMetadataText));
    }
    public string StoryActionAmountText
    {
        get => _actionAmountText;
        set => SetActionInteger(CanonicalStoryActionSchema.AmountProperty, value, 1, ref _actionAmountText,
            nameof(StoryActionAmountText));
    }
    public string StoryActionMessage
    {
        get => _actionMessage;
        set => SetActionString(CanonicalStoryActionSchema.MessageProperty, value, ref _actionMessage,
            nameof(StoryActionMessage));
    }

    public IReadOnlyList<string> RepeatPolicyOptions { get; } = StoryStartSchema.SupportedRepeatPolicies;
    public IReadOnlyList<CanonicalStoryStartRepeatPolicyOption> RepeatPolicyChoices { get; } =
    [
        new(StoryStartSchema.Once, "仅一次"),
        new(StoryStartSchema.Repeatable, "可重复"),
    ];
    public CanonicalStoryStartRepeatPolicyOption? SelectedRepeatPolicy
    {
        get => RepeatPolicyChoices.FirstOrDefault(option => option.Value == _repeatPolicy);
        set { if (value is not null) RepeatPolicy = value.Value; }
    }
    public string RepeatPolicy
    {
        get => _repeatPolicy;
        set
        {
            if (!IsStoryStart || string.Equals(_repeatPolicy, value, StringComparison.Ordinal)) return;
            if (_host.Session.SetStoryStartRepeatPolicy(NodeId, value))
            {
                _host.Refresh();
                RefreshFromHost();
            }
            else OnPropertyChanged(nameof(RepeatPolicy));
        }
    }

    public IReadOnlyList<CanonicalObjectiveTypeOption> ObjectiveTypeOptions { get; } =
    [
        new(CanonicalTaskObjectiveSchema.KillEntity, "击杀实体"),
        new(CanonicalTaskObjectiveSchema.CollectItem, "收集物品"),
        new(CanonicalTaskObjectiveSchema.InteractActor, "交互角色"),
    ];

    public CanonicalObjectiveTypeOption? SelectedObjectiveType
    {
        get => ObjectiveTypeOptions.FirstOrDefault(option => option.Value == _objectiveType);
        set
        {
            if (value is null || !IsObjective || value.Value == _objectiveType) return;
            var actorId = value.Value == CanonicalTaskObjectiveSchema.InteractActor
                ? _actorItems.FirstOrDefault()?.Id
                : null;
            _host.ChangeObjectiveType(NodeId, value.Value, actorId);
            RefreshFromHost();
        }
    }

    public string ObjectiveType => _objectiveType;
    public string ObjectiveDescription
    {
        get => _objectiveDescription;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_objectiveDescription, next, StringComparison.Ordinal)) return;
            if (_host.SetObjectiveDescription(NodeId, next)) _objectiveDescription = next;
            else RefreshFromHost();
            OnPropertyChanged();
        }
    }

    public string ObjectiveRequiredText
    {
        get => _objectiveRequiredText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_objectiveRequiredText, next, StringComparison.Ordinal)) return;
            if (int.TryParse(next, out var parsed) && _host.SetObjectiveRequired(NodeId, parsed))
                _objectiveRequiredText = next;
            else
                RefreshFromHost();
            OnPropertyChanged();
        }
    }

    public int ObjectiveRequired => int.TryParse(_objectiveRequiredText, out var value) ? value : 0;
    public string ObjectiveTarget
    {
        get => _objectiveTarget;
        set
        {
            var next = value ?? string.Empty;
            if (!IsKillEntityObjective && !IsCollectItemObjective) return;
            var property = IsKillEntityObjective ? CanonicalTaskObjectiveSchema.EntityProperty : CanonicalTaskObjectiveSchema.ItemProperty;
            if (_host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(next))) _objectiveTarget = next;
            else RefreshFromHost();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CanonicalSessionSpeakerOption> ObjectiveActorOptions => _objectiveActorOptions;
    public CanonicalSessionSpeakerOption? SelectedObjectiveActor
    {
        get => _selectedObjectiveActor;
        set
        {
            if (!IsInteractActorObjective || value is null || value.IsPlaceholder) return;
            if (_host.SetNodeProperty(NodeId, CanonicalTaskObjectiveSchema.ActorIdProperty,
                    JsonSerializer.SerializeToElement(value.Id))) RefreshFromHost();
            else RefreshFromHost();
        }
    }
    public string ObjectiveActorId => _objectiveActorId;
    public bool IsObjectiveActorResolved => !string.IsNullOrWhiteSpace(_objectiveActorId)
        && _objectiveActorOptions.Any(option => option.IsResolved && option.Id == _objectiveActorId);
    public bool IsObjectiveActorUnresolved => !string.IsNullOrWhiteSpace(_objectiveActorId) && !IsObjectiveActorResolved;
    public string ObjectiveActorStatusText => IsObjectiveActorUnresolved
        ? $"角色未解析：{_objectiveActorId}" : IsObjectiveActorResolved ? string.Empty : "请选择角色";

    /// <summary>
    /// Author-facing Session Line speaker choices. The first item is an
    /// explicit blank state; an existing ID that is not in the current Story
    /// is retained as a read-only unresolved item.
    /// </summary>
    public IReadOnlyList<CanonicalSessionSpeakerOption> SpeakerOptions => _speakerOptions;
    public IReadOnlyList<CanonicalSessionSpeakerOption> ActorOptions => SpeakerOptions;
    public IReadOnlyList<CanonicalSessionSpeakerOption> SpeakerActors => SpeakerOptions;
    public IReadOnlyList<CanonicalStoryActorItem> ActorItems => _actorItems;

    public CanonicalSessionSpeakerOption? SelectedSpeaker
    {
        get => _selectedSpeaker;
        set => SetSelectedSpeaker(value);
    }

    public CanonicalSessionSpeakerOption? SelectedSpeakerActor
    {
        get => SelectedSpeaker;
        set => SelectedSpeaker = value;
    }

    public CanonicalStoryActorItem? SelectedActor
    {
        get => SelectedSpeaker?.ActorItem;
        set
        {
            if (value is null)
            {
                SelectedSpeaker = SpeakerOptions.FirstOrDefault(option => option.IsPlaceholder);
                return;
            }

            var option = SpeakerOptions.FirstOrDefault(candidate =>
                candidate.IsResolved && string.Equals(candidate.Id, value.Id, StringComparison.Ordinal));
            if (option is not null) SelectedSpeaker = option;
        }
    }

    public string SpeakerActorId => _speakerActorId;

    /// <summary>True only when the stored ID resolves to a current Story Actor.</summary>
    public bool IsSpeakerResolved => !string.IsNullOrWhiteSpace(_speakerActorId)
        && _speakerOptions.Any(option => option.IsResolved
            && string.Equals(option.Id, _speakerActorId, StringComparison.Ordinal));

    public bool IsSpeakerUnresolved => !string.IsNullOrWhiteSpace(_speakerActorId) && !IsSpeakerResolved;

    public string SpeakerStatusText => IsSpeakerUnresolved
        ? $"角色未解析：{_speakerActorId}"
        : IsSpeakerResolved ? string.Empty : "请选择角色（运行时尚未有效）";

    public string LineText
    {
        get => _lineText;
        set => SetStringProperty("text", value, ref _lineText, nameof(LineText));
    }

    public string Text { get => LineText; set => LineText = value; }

    public string ChoicePrompt
    {
        get => _choicePrompt;
        set => SetStringProperty(SessionChoiceSchema.PromptProperty, value, ref _choicePrompt, nameof(ChoicePrompt));
    }

    public string Prompt { get => ChoicePrompt; set => ChoicePrompt = value; }

    public string EndDisplayName
    {
        get => _endDisplayName;
        set => SetStringProperty("display_name", value, ref _endDisplayName, nameof(EndDisplayName));
    }

    public string LogicOutputDisplayName
    {
        get => _logicOutputDisplayName;
        set => SetStringProperty("display_name", value, ref _logicOutputDisplayName, nameof(LogicOutputDisplayName));
    }

    public ObservableCollection<CanonicalChoiceOptionViewModel> ChoiceOptions { get; } = [];
    public IReadOnlyList<CanonicalChoiceOptionViewModel> Options => ChoiceOptions;
    public RelayCommand AddChoiceOptionCommand { get; }

    /// <summary>Visible priority-ordered result slots on a Task settle node.</summary>
    public ObservableCollection<CanonicalTaskResultSlotViewModel> TaskResultSlots { get; } = [];
    public IReadOnlyList<CanonicalTaskResultSlotViewModel> ResultSlots => TaskResultSlots;
    public IReadOnlyList<CanonicalTaskResultSlotViewModel> SettlementResultSlots => TaskResultSlots;
    public RelayCommand AddTaskResultSlotCommand { get; }
    public RelayCommand AddResultSlotCommand => AddTaskResultSlotCommand;
    public ObservableCollection<CanonicalStoryStartTriggerViewModel> StoryStartTriggers { get; } = [];
    public IReadOnlyList<CanonicalStoryStartTriggerViewModel> TriggerSlots => StoryStartTriggers;
    public RelayCommand AddStoryStartTriggerCommand { get; }

    /// <summary>
    /// Optional UI confirmation boundary for removing a referenced Choice
    /// option. The Core edit remains fail-closed until this callback accepts
    /// the exact confirmation-required result.
    /// </summary>
    public Func<CanonicalChoiceOptionRemovalConfirmation, bool>? ChoiceOptionRemovalConfirmationRequested { get; set; }

    /// <summary>
    /// Optional UI confirmation boundary for removing a referenced Task result
    /// slot. Core remains fail-closed until this callback accepts the exact
    /// confirmation-required result.
    /// </summary>
    public Func<CanonicalTaskResultSlotRemovalConfirmation, bool>? TaskResultSlotRemovalConfirmationRequested { get; set; }
    public Func<CanonicalStoryStartTriggerRemovalConfirmation, bool>? StoryStartTriggerRemovalConfirmationRequested { get; set; }

    public bool AddStoryStartTrigger(string displayName = "新触发",
        string triggerType = StoryStartSchema.EnterStory,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
    {
        if (!IsStoryStart) return false;
        var result = _host.Session.AddStoryStartTrigger(NodeId, displayName, triggerType, triggerProperties);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    public bool SetStoryStartTriggerType(string portId, string triggerType)
    {
        if (!IsStoryStart) return false;
        var actorId = _actorItems.FirstOrDefault()?.Id;
        var result = _host.Session.SetStoryStartTriggerType(NodeId, portId, triggerType, actorId);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    public bool SetStoryStartTriggerProperties(string portId,
        IReadOnlyDictionary<string, JsonElement> triggerProperties)
    {
        if (!IsStoryStart) return false;
        var result = _host.Session.SetStoryStartTriggerProperties(NodeId, portId, triggerProperties);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    internal bool SetStoryStartTriggerProperty(string portId, string property, JsonElement value)
    {
        var slot = StoryStartTriggers.FirstOrDefault(candidate => candidate.Identity == portId);
        if (slot is null) return false;
        var properties = slot.TriggerProperties.ValueKind == JsonValueKind.Object
            ? slot.TriggerProperties.EnumerateObject().ToDictionary(item => item.Name,
                item => item.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        properties[property] = value.Clone();
        return SetStoryStartTriggerProperties(portId, properties);
    }

    public bool RenameStoryStartTrigger(string portId, string displayName)
    {
        if (!IsStoryStart) return false;
        var result = _host.Session.RenameStoryStartTrigger(NodeId, portId, displayName);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    public bool ReorderStoryStartTrigger(string portId, int order)
    {
        if (!IsStoryStart) return false;
        var result = _host.Session.ReorderStoryStartTrigger(NodeId, portId, order);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    public bool RemoveStoryStartTrigger(string portId)
    {
        if (!IsStoryStart) return false;
        var result = _host.Session.RemoveStoryStartTrigger(NodeId, portId);
        if (result) { _host.Refresh(); RefreshFromHost(); return true; }
        if (!_host.Session.LastValidationIssues.Any(issue => issue.Code == "graph.story.start.trigger.references.confirmation_required")) return false;
        var slot = StoryStartTriggers.FirstOrDefault(item => item.Identity == portId);
        if (slot is null || StoryStartTriggerRemovalConfirmationRequested is null
            || !StoryStartTriggerRemovalConfirmationRequested(new CanonicalStoryStartTriggerRemovalConfirmation(slot.DisplayName))) return false;
        result = _host.Session.RemoveStoryStartTrigger(NodeId, portId, true);
        _host.Refresh(); RefreshFromHost();
        return result;
    }

    public bool RenameChoiceOption(string optionId, string displayText)
        => IsChoice && Execute(() => _host.RenameSessionChoiceOption(NodeId, optionId, displayText));

    public bool ReorderChoiceOption(string optionId, int order)
        => IsChoice && Execute(() => _host.ReorderSessionChoiceOption(NodeId, optionId, order));

    public bool RemoveChoiceOption(string optionId)
    {
        if (!IsChoice || !Execute(() => _host.RemoveSessionChoiceOption(NodeId, optionId)))
        {
            if (!IsChoice || !HasChoiceReferenceConfirmationRequired()) return false;

            var option = ChoiceOptions.FirstOrDefault(candidate =>
                string.Equals(candidate.OptionId, optionId, StringComparison.Ordinal));
            if (option is null) return false;

            var confirmation = ChoiceOptionRemovalConfirmationRequested;
            if (confirmation is null || !confirmation(new CanonicalChoiceOptionRemovalConfirmation(option.DisplayText)))
                return false;

            return Execute(() => _host.RemoveSessionChoiceOption(NodeId, optionId, confirmReferencedRemoval: true));
        }

        return true;
    }

    public bool RemoveChoiceOption(CanonicalChoiceOptionViewModel option)
        => option is not null && RemoveChoiceOption(option.OptionId);

    public bool AddChoiceOption(string displayText = "新选项")
        => IsChoice && Execute(() => _host.AddSessionChoiceOption(NodeId, displayText));

    public bool AddTaskResultSlot(string displayName = "新结果")
        => IsTaskSettle && Execute(() => _host.AddDynamicPort(
            NodeId, displayName, GraphPortDirection.Input, GraphInterfaceKind.Logic));

    public bool AddResultSlot(string displayName = "新结果") => AddTaskResultSlot(displayName);

    public bool RenameTaskResultSlot(string portId, string displayName)
        => IsTaskSettle && Execute(() => _host.RenamePortDisplayName(NodeId, portId, displayName));

    public bool RenameResultSlot(string portId, string displayName)
        => RenameTaskResultSlot(portId, displayName);

    public bool ReorderTaskResultSlot(string portId, int order)
        => IsTaskSettle && Execute(() => _host.MoveDynamicPort(NodeId, portId, order));

    public bool ReorderResultSlot(string portId, int order)
        => ReorderTaskResultSlot(portId, order);

    public bool RemoveTaskResultSlot(string portId)
    {
        if (!IsTaskSettle || !Execute(() => _host.RemoveDynamicPort(NodeId, portId)))
        {
            if (!IsTaskSettle || !HasTaskResultSlotReferenceConfirmationRequired()) return false;

            var slot = TaskResultSlots.FirstOrDefault(candidate =>
                string.Equals(candidate.PortId, portId, StringComparison.Ordinal));
            if (slot is null) return false;

            var confirmation = TaskResultSlotRemovalConfirmationRequested;
            if (confirmation is null || !confirmation(new CanonicalTaskResultSlotRemovalConfirmation(
                    slot.DisplayName)))
                return false;

            return Execute(() => _host.RemoveDynamicPort(NodeId, portId, confirmReferencedRemoval: true));
        }

        return true;
    }

    public bool RemoveResultSlot(string portId) => RemoveTaskResultSlot(portId);

    public bool RemoveTaskResultSlot(CanonicalTaskResultSlotViewModel slot)
        => slot is not null && RemoveTaskResultSlot(slot.PortId);

    internal bool CanMoveChoiceOptionDown(int order)
        => IsChoice && order >= 0 && order < ChoiceOptions.Count - 1;

    internal bool CanMoveTaskResultSlotDown(int order)
        => IsTaskSettle && order >= 0 && order < TaskResultSlots.Count - 1;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.GraphChanged -= HostOnGraphChanged;
        _host.PropertyChanged -= HostOnPropertyChanged;
    }

    private void SetStringProperty(string property, string? value, ref string field, string propertyName)
    {
        if (_disposed || !CanEditProperty(property)) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)) return;
        if (_host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(next)))
        {
            field = next;
            OnPropertyChanged(propertyName);
            if (propertyName == nameof(LineText)) OnPropertyChanged(nameof(Text));
            if (propertyName == nameof(ChoicePrompt)) OnPropertyChanged(nameof(Prompt));
        }
        else
        {
            RefreshFromHost();
            OnPropertyChanged(propertyName);
        }
    }

    private void SetActionString(string property, string? value, ref string field, string propertyName)
    {
        if (_disposed || !IsStoryAction) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)) return;
        if (_host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(next))) field = next;
        else RefreshFromHost();
        OnPropertyChanged(propertyName);
    }

    private void SetActionInteger(string property, string? value, int minimum, ref string field, string propertyName)
    {
        if (_disposed || !IsStoryAction) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)) return;
        if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum
            && _host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(parsed))) field = next;
        else RefreshFromHost();
        OnPropertyChanged(propertyName);
    }

    private void SetSelectedSpeaker(CanonicalSessionSpeakerOption? option)
    {
        if (_disposed || !IsLine) return;

        // The unresolved item is a preservation projection, not an editable
        // Actor reference. Selecting it therefore keeps its existing ID.
        var next = option is null || option.IsPlaceholder
            ? string.Empty
            : option.Id;
        if (string.Equals(_speakerActorId, next, StringComparison.Ordinal)) return;

        if (_host.SetNodeProperty(NodeId, "speaker_actor_id", JsonSerializer.SerializeToElement(next)))
        {
            _speakerActorId = next;
            RebuildSpeakerOptions();
            NotifySpeakerPropertiesChanged();
        }
        else
        {
            RefreshFromHost();
        }
    }

    private bool Execute(Func<bool> action)
    {
        if (_disposed) return false;
        return action();
    }

    private bool HasChoiceReferenceConfirmationRequired()
        => _host.LastValidationIssues.Any(issue => string.Equals(
            issue.Code,
            "graph.session.choice.references.confirmation_required",
            StringComparison.Ordinal));

    private bool HasTaskResultSlotReferenceConfirmationRequired()
        => _host.LastValidationIssues.Any(issue => string.Equals(
            issue.Code,
            "graph.dynamic_port.references.confirmation_required",
            StringComparison.Ordinal));

    private void HostOnGraphChanged(object? sender, EventArgs args) => RefreshFromHost();

    private void HostOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(GraphEditorHostViewModel.LastValidationIssues))
            OnPropertyChanged(nameof(ValidationIssues));
    }

    public IReadOnlyList<ValidationIssue> ValidationIssues
        => IsStoryStart ? _host.Session.LastValidationIssues : _host.LastValidationIssues;

    private bool CanEditProperty(string property)
        => property == "speaker_actor_id" && IsLine
            || property == "text" && IsLine
            || property == SessionChoiceSchema.PromptProperty && IsChoice
            || property == "display_name" && (IsEnd || IsLogicOutput);

    private void RefreshFromHost()
    {
        var current = _host.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, NodeId, StringComparison.Ordinal));
        if (current is null) return;

        _repeatPolicy = current.Properties.TryGetValue(StoryStartSchema.RepeatPolicyProperty, out var repeat)
            && repeat.ValueKind == JsonValueKind.String ? repeat.GetString() ?? StoryStartSchema.Once : StoryStartSchema.Once;
        StoryStartTriggers.Clear();
        if (IsStoryStart)
        {
            foreach (var slot in StoryStartSchema.ReadTriggers(new GraphNode(current.NodeId, current.Type,
                current.DisplayName, current.Inputs.Concat(current.Outputs).Select(port => new GraphPort(port.PortId,
                    port.DisplayName, port.IsInput, port.GraphInterfaceKind, port.Order)), current.Properties)))
                StoryStartTriggers.Add(new CanonicalStoryStartTriggerViewModel(this, slot));
        }

        _speakerActorId = current.Properties.TryGetValue("speaker_actor_id", out var speaker)
            && speaker.ValueKind == JsonValueKind.String
            ? speaker.GetString() ?? string.Empty
            : string.Empty;
        _actionType = ReadString(current, CanonicalStoryActionSchema.TypeProperty);
        _actionItem = ReadString(current, CanonicalStoryActionSchema.ItemProperty);
        _actionMetadataText = current.Properties.TryGetValue(CanonicalStoryActionSchema.MetadataProperty, out var actionMetadata)
            && actionMetadata.ValueKind == JsonValueKind.Number ? actionMetadata.ToString() : string.Empty;
        _actionAmountText = current.Properties.TryGetValue(CanonicalStoryActionSchema.AmountProperty, out var actionAmount)
            && actionAmount.ValueKind == JsonValueKind.Number ? actionAmount.ToString() : string.Empty;
        _actionMessage = ReadString(current, CanonicalStoryActionSchema.MessageProperty);
        if (current.Properties.TryGetValue("text", out var text) && text.ValueKind == JsonValueKind.String)
            _lineText = text.GetString() ?? string.Empty;
        if (current.Properties.TryGetValue(SessionChoiceSchema.PromptProperty, out var prompt)
            && prompt.ValueKind == JsonValueKind.String)
            _choicePrompt = prompt.GetString() ?? string.Empty;
        if (current.Properties.TryGetValue("display_name", out var display) && display.ValueKind == JsonValueKind.String)
        {
            _endDisplayName = display.GetString() ?? string.Empty;
            _logicOutputDisplayName = _endDisplayName;
        }

        _objectiveType = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.TypeProperty, out var objectiveType)
            && objectiveType.ValueKind == JsonValueKind.String
            ? objectiveType.GetString() ?? string.Empty : string.Empty;
        _objectiveDescription = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.DescriptionProperty, out var objectiveDescription)
            && objectiveDescription.ValueKind == JsonValueKind.String
            ? objectiveDescription.GetString() ?? string.Empty : string.Empty;
        _objectiveRequiredText = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.RequiredProperty, out var objectiveRequired)
            && objectiveRequired.ValueKind == JsonValueKind.Number
            ? objectiveRequired.ToString() : string.Empty;
        _objectiveTarget = IsKillEntityObjective
            ? ReadString(current, CanonicalTaskObjectiveSchema.EntityProperty)
            : IsCollectItemObjective ? ReadString(current, CanonicalTaskObjectiveSchema.ItemProperty) : string.Empty;
        _objectiveActorId = ReadString(current, CanonicalTaskObjectiveSchema.ActorIdProperty);

        RebuildSpeakerOptions();
        RebuildObjectiveActorOptions();

        ChoiceOptions.Clear();
        if (IsChoice && current.Properties.TryGetValue(SessionChoiceSchema.OptionsProperty, out var options)
            && options.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var option in options.EnumerateArray())
            {
                if (option.ValueKind != JsonValueKind.Object
                    || !option.TryGetProperty("option_id", out var optionId)
                    || !option.TryGetProperty("display_text", out var displayText)
                    || optionId.ValueKind != JsonValueKind.String
                    || displayText.ValueKind != JsonValueKind.String)
                    continue;
                ChoiceOptions.Add(new CanonicalChoiceOptionViewModel(
                    this, optionId.GetString() ?? string.Empty,
                    displayText.GetString() ?? string.Empty, index));
                index++;
            }
        }

        TaskResultSlots.Clear();
        if (IsTaskSettle)
        {
            foreach (var port in current.Inputs
                .Where(port => port.GraphInterfaceKind == GraphInterfaceKind.Logic)
                .OrderBy(port => port.Order)
                .ThenBy(port => port.PortId, StringComparer.Ordinal))
            {
                TaskResultSlots.Add(new CanonicalTaskResultSlotViewModel(
                    this, port.PortId, port.DisplayName, port.Order));
            }
        }

        OnPropertyChanged(nameof(LineText));
        OnPropertyChanged(nameof(Text));
        NotifySpeakerPropertiesChanged();
        OnPropertyChanged(nameof(ChoicePrompt));
        OnPropertyChanged(nameof(Prompt));
        OnPropertyChanged(nameof(EndDisplayName));
        OnPropertyChanged(nameof(LogicOutputDisplayName));
        OnPropertyChanged(nameof(RepeatPolicy));
        OnPropertyChanged(nameof(SelectedRepeatPolicy));
        OnPropertyChanged(nameof(StoryStartTriggers));
        OnPropertyChanged(nameof(TriggerSlots));
        OnPropertyChanged(nameof(ObjectiveType));
        OnPropertyChanged(nameof(SelectedObjectiveType));
        OnPropertyChanged(nameof(ObjectiveDescription));
        OnPropertyChanged(nameof(ObjectiveRequiredText));
        OnPropertyChanged(nameof(ObjectiveRequired));
        OnPropertyChanged(nameof(ObjectiveTarget));
        OnPropertyChanged(nameof(IsKillEntityObjective));
        OnPropertyChanged(nameof(IsCollectItemObjective));
        OnPropertyChanged(nameof(IsInteractActorObjective));
        OnPropertyChanged(nameof(ObjectiveActorOptions));
        OnPropertyChanged(nameof(SelectedObjectiveActor));
        OnPropertyChanged(nameof(ObjectiveActorId));
        OnPropertyChanged(nameof(IsObjectiveActorResolved));
        OnPropertyChanged(nameof(IsObjectiveActorUnresolved));
        OnPropertyChanged(nameof(ObjectiveActorStatusText));
        OnPropertyChanged(nameof(StoryActionType));
        OnPropertyChanged(nameof(SelectedStoryActionType));
        OnPropertyChanged(nameof(StoryActionItem));
        OnPropertyChanged(nameof(StoryActionMetadataText));
        OnPropertyChanged(nameof(StoryActionAmountText));
        OnPropertyChanged(nameof(StoryActionMessage));
        OnPropertyChanged(nameof(IsGiveItemAction));
        OnPropertyChanged(nameof(IsGiveXpAction));
        OnPropertyChanged(nameof(IsSendMessageAction));
        OnPropertyChanged(nameof(ValidationIssues));
        AddChoiceOptionCommand.RaiseCanExecuteChanged();
        AddTaskResultSlotCommand.RaiseCanExecuteChanged();
    }

    private void RebuildSpeakerOptions()
    {
        var options = new List<CanonicalSessionSpeakerOption>
        {
            CanonicalSessionSpeakerOption.Placeholder,
        };
        options.AddRange(_actorItems
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new CanonicalSessionSpeakerOption(item.Id, item.DisplayName, true, item)));

        if (!string.IsNullOrWhiteSpace(_speakerActorId)
            && !options.Any(option => option.IsResolved
                && string.Equals(option.Id, _speakerActorId, StringComparison.Ordinal)))
        {
            options.Add(new CanonicalSessionSpeakerOption(
                _speakerActorId,
                $"未解析角色：{_speakerActorId}",
                false));
        }

        _speakerOptions = options;
        _selectedSpeaker = options.FirstOrDefault(option =>
            string.Equals(option.Id, _speakerActorId, StringComparison.Ordinal)) ?? options[0];
    }

    private void RebuildObjectiveActorOptions()
    {
        var options = new List<CanonicalSessionSpeakerOption> { CanonicalSessionSpeakerOption.Placeholder };
        options.AddRange(_actorItems
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new CanonicalSessionSpeakerOption(item.Id, item.DisplayName, true, item)));
        if (!string.IsNullOrWhiteSpace(_objectiveActorId)
            && !options.Any(option => option.IsResolved && option.Id == _objectiveActorId))
            options.Add(new CanonicalSessionSpeakerOption(_objectiveActorId,
                $"未解析角色：{_objectiveActorId}", false));
        _objectiveActorOptions = options;
        _selectedObjectiveActor = options.FirstOrDefault(option => option.Id == _objectiveActorId) ?? options[0];
    }

    private static string ReadString(GraphEditorNodeViewModel node, string property)
        => node.Properties.TryGetValue(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private void NotifySpeakerPropertiesChanged()
    {
        OnPropertyChanged(nameof(SpeakerOptions));
        OnPropertyChanged(nameof(ActorOptions));
        OnPropertyChanged(nameof(SpeakerActors));
        OnPropertyChanged(nameof(ActorItems));
        OnPropertyChanged(nameof(SelectedSpeaker));
        OnPropertyChanged(nameof(SelectedSpeakerActor));
        OnPropertyChanged(nameof(SelectedActor));
        OnPropertyChanged(nameof(SpeakerActorId));
        OnPropertyChanged(nameof(IsSpeakerResolved));
        OnPropertyChanged(nameof(IsSpeakerUnresolved));
        OnPropertyChanged(nameof(SpeakerStatusText));
    }
}

/// <summary>Author-facing Objective type option; Value is the persisted ID.</summary>
public sealed record CanonicalObjectiveTypeOption(string Value, string DisplayName)
{
    public string Id => Value;
    public string Type => Value;
}

/// <summary>
/// Non-editable ComboBox item for a Session Line speaker. Stable IDs are
/// retained for persistence, while the UI displays only author-facing labels.
/// </summary>
public sealed record CanonicalSessionSpeakerOption(
    string Id,
    string DisplayName,
    bool IsResolved,
    CanonicalStoryActorItem? ActorItem = null)
{
    public static CanonicalSessionSpeakerOption Placeholder { get; } =
        new(string.Empty, "请选择角色", false);

    public bool IsPlaceholder => string.IsNullOrWhiteSpace(Id);
    public bool IsUnresolved => !IsResolved && !IsPlaceholder;
    public string StableId => Id;
    public CanonicalStoryActorItem? Actor => ActorItem;
    public bool IsOwned => ActorItem?.IsOwned == true;
    public bool IsReferenced => ActorItem?.IsReferenced == true;
}

/// <summary>Author-facing destructive confirmation details for a Story Start trigger.</summary>
public sealed record CanonicalStoryStartTriggerRemovalConfirmation(string DisplayName);

/// <summary>
/// Visible Story Start trigger projection. Stable port identity is retained
/// internally for commands but is deliberately not exposed as an inspector
/// field.
/// </summary>
public sealed class CanonicalStoryStartTriggerViewModel : ObservableObject
{
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _displayName;
    private string _triggerType;
    private JsonElement _triggerProperties;
    private int _order;

    internal CanonicalStoryStartTriggerViewModel(CanonicalNodeInspectorViewModel owner, StoryStartTriggerSlot slot)
    {
        _owner = owner;
        Identity = slot.PortId;
        _displayName = slot.DisplayName;
        _triggerType = slot.TriggerType;
        _triggerProperties = slot.TriggerProperties.Clone();
        _order = slot.Order;
        RemoveCommand = new RelayCommand(() => _owner.RemoveStoryStartTrigger(Identity));
        MoveUpCommand = new RelayCommand(() => _owner.ReorderStoryStartTrigger(Identity, Order - 1), () => Order > 0);
        MoveDownCommand = new RelayCommand(() => _owner.ReorderStoryStartTrigger(Identity, Order + 1),
            () => Order < _owner.StoryStartTriggers.Count - 1);
    }

    internal string Identity { get; }
    public string PortId => Identity;
    public string StablePortId => Identity;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal)) return;
            if (_owner.RenameStoryStartTrigger(Identity, value))
            {
                _displayName = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<CanonicalStoryStartTriggerTypeOption> TriggerTypeOptions { get; } =
    [
        new(StoryStartSchema.EnterStory, "进入剧情"),
        new(StoryStartSchema.ActorInteraction, "角色交互"),
        new(StoryStartSchema.RegionEntry, "进入区域"),
    ];

    public string TriggerType => _triggerType;
    public CanonicalStoryStartTriggerTypeOption? SelectedTriggerType
    {
        get => TriggerTypeOptions.FirstOrDefault(option => option.Value == _triggerType);
        set
        {
            if (value is null || value.Value == _triggerType) return;
            if (_owner.SetStoryStartTriggerType(Identity, value.Value))
            {
                _triggerType = value.Value;
                _triggerProperties = JsonSerializer.SerializeToElement(
                    StoryStartSchema.DefaultTriggerProperties(value.Value));
                OnPropertyChanged(nameof(TriggerType));
                OnPropertyChanged(nameof(SelectedTriggerType));
                OnPropertyChanged(nameof(IsActorInteraction));
                OnPropertyChanged(nameof(IsRegionEntry));
                OnPropertyChanged(nameof(IsEnterStory));
                NotifyPropertyFields();
            }
        }
    }

    public JsonElement TriggerProperties => _triggerProperties;
    public bool IsActorInteraction => _triggerType == StoryStartSchema.ActorInteraction;
    public bool IsRegionEntry => _triggerType == StoryStartSchema.RegionEntry;
    public bool IsEnterStory => _triggerType == StoryStartSchema.EnterStory;
    public string ActorId
    {
        get => ReadString(StoryStartSchema.ActorIdProperty);
        set => SetString(StoryStartSchema.ActorIdProperty, value);
    }
    public string DimensionText
    {
        get => ReadNumber(StoryStartSchema.DimensionProperty);
        set => SetInteger(StoryStartSchema.DimensionProperty, value);
    }
    public string XText
    {
        get => ReadNumber(StoryStartSchema.XProperty);
        set => SetNumber(StoryStartSchema.XProperty, value);
    }
    public string YText
    {
        get => ReadNumber(StoryStartSchema.YProperty);
        set => SetNumber(StoryStartSchema.YProperty, value);
    }
    public string ZText
    {
        get => ReadNumber(StoryStartSchema.ZProperty);
        set => SetNumber(StoryStartSchema.ZProperty, value);
    }
    public string RadiusText
    {
        get => ReadNumber(StoryStartSchema.RadiusProperty);
        set => SetNumber(StoryStartSchema.RadiusProperty, value);
    }

    // Short aliases keep the projection convenient for host/test bindings.
    public string Dimension { get => DimensionText; set => DimensionText = value; }
    public string X { get => XText; set => XText = value; }
    public string Y { get => YText; set => YText = value; }
    public string Z { get => ZText; set => ZText = value; }
    public string Radius { get => RadiusText; set => RadiusText = value; }
    public int Order
    {
        get => _order;
        internal set
        {
            if (!SetProperty(ref _order, value)) return;
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    private string ReadString(string property)
        => _triggerProperties.ValueKind == JsonValueKind.Object
            && _triggerProperties.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private string ReadNumber(string property)
        => _triggerProperties.ValueKind == JsonValueKind.Object
            && _triggerProperties.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
                ? value.ToString() : string.Empty;

    private void SetString(string property, string? value)
    {
        if (!IsActorInteraction || string.IsNullOrWhiteSpace(value)) return;
        if (_owner.SetStoryStartTriggerProperty(Identity, property,
                JsonSerializer.SerializeToElement(value.Trim())))
        {
            _triggerProperties = SetLocalProperty(property, JsonSerializer.SerializeToElement(value.Trim()));
            NotifyPropertyFields();
        }
    }

    private void SetInteger(string property, string? value)
    {
        if (!IsRegionEntry || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return;
        if (_owner.SetStoryStartTriggerProperty(Identity, property, JsonSerializer.SerializeToElement(parsed)))
        {
            _triggerProperties = SetLocalProperty(property, JsonSerializer.SerializeToElement(parsed));
            NotifyPropertyFields();
        }
    }

    private void SetNumber(string property, string? value)
    {
        if (!IsRegionEntry || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.IsNaN(parsed) || double.IsInfinity(parsed) || (property == StoryStartSchema.RadiusProperty && parsed <= 0)) return;
        if (_owner.SetStoryStartTriggerProperty(Identity, property, JsonSerializer.SerializeToElement(parsed)))
        {
            _triggerProperties = SetLocalProperty(property, JsonSerializer.SerializeToElement(parsed));
            NotifyPropertyFields();
        }
    }

    private JsonElement SetLocalProperty(string property, JsonElement value)
    {
        var properties = _triggerProperties.ValueKind == JsonValueKind.Object
            ? _triggerProperties.EnumerateObject().ToDictionary(item => item.Name,
                item => item.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        properties[property] = value.Clone();
        return JsonSerializer.SerializeToElement(properties);
    }

    private void NotifyPropertyFields()
    {
        OnPropertyChanged(nameof(TriggerProperties));
        OnPropertyChanged(nameof(ActorId));
        OnPropertyChanged(nameof(DimensionText));
        OnPropertyChanged(nameof(XText));
        OnPropertyChanged(nameof(YText));
        OnPropertyChanged(nameof(ZText));
        OnPropertyChanged(nameof(RadiusText));
        OnPropertyChanged(nameof(Dimension));
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(Z));
        OnPropertyChanged(nameof(Radius));
    }
}

public sealed record CanonicalStoryStartTriggerTypeOption(string Value, string DisplayName);
public sealed record CanonicalStoryStartRepeatPolicyOption(string Value, string DisplayName);
public sealed record CanonicalStoryActionTypeOption(string Value, string DisplayName);

/// <summary>Author-facing details for the destructive Choice-option confirmation.</summary>
public sealed record CanonicalChoiceOptionRemovalConfirmation(string DisplayText);

/// <summary>Visible Choice option projection; OptionId is retained only for commands.</summary>
public sealed class CanonicalChoiceOptionViewModel : ObservableObject
{
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _displayText;
    private int _order;

    internal CanonicalChoiceOptionViewModel(CanonicalNodeInspectorViewModel owner, string optionId,
        string displayText, int order)
    {
        _owner = owner;
        OptionId = optionId;
        _displayText = displayText;
        _order = order;
        RemoveCommand = new RelayCommand(() => _owner.RemoveChoiceOption(this));
        MoveUpCommand = new RelayCommand(() => _owner.ReorderChoiceOption(OptionId, Order - 1), () => Order > 0);
        MoveDownCommand = new RelayCommand(
            () => _owner.ReorderChoiceOption(OptionId, Order + 1),
            () => _owner.CanMoveChoiceOptionDown(Order));
    }

    public string OptionId { get; }
    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (string.Equals(_displayText, value, StringComparison.Ordinal)) return;
            if (_owner.RenameChoiceOption(OptionId, value))
            {
                _displayText = value;
                OnPropertyChanged();
            }
        }
    }

    public int Order
    {
        get => _order;
        internal set
        {
            if (!SetProperty(ref _order, value)) return;
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
}

/// <summary>Visible Task settlement result slot; PortId is stable identity.</summary>
public sealed class CanonicalTaskResultSlotViewModel : ObservableObject
{
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _displayName;
    private int _order;

    internal CanonicalTaskResultSlotViewModel(CanonicalNodeInspectorViewModel owner,
        string portId, string displayName, int order)
    {
        _owner = owner;
        PortId = portId;
        _displayName = displayName;
        _order = order;
        RemoveCommand = new RelayCommand(() => _owner.RemoveTaskResultSlot(this));
        MoveUpCommand = new RelayCommand(() => _owner.ReorderTaskResultSlot(PortId, Order - 1),
            () => Order > 0);
        MoveDownCommand = new RelayCommand(() => _owner.ReorderTaskResultSlot(PortId, Order + 1),
            () => _owner.CanMoveTaskResultSlotDown(Order));
    }

    public string PortId { get; }
    public string StablePortId => PortId;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal)) return;
            if (_owner.RenameTaskResultSlot(PortId, value))
            {
                _displayName = value;
                OnPropertyChanged();
            }
        }
    }

    public int Order
    {
        get => _order;
        internal set
        {
            if (!SetProperty(ref _order, value)) return;
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
}

/// <summary>Author-facing details for destructive Task result-slot confirmation.</summary>
public sealed record CanonicalTaskResultSlotRemovalConfirmation(string DisplayName);
