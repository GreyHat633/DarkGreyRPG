using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>
/// Host-backed authoring projection for one selected graph node. It deliberately
/// exposes only author-facing Session fields; node and port identity stays with
/// the canonical graph and is never edited by this view model.
/// </summary>
public sealed partial class CanonicalNodeInspectorViewModel : ObservableObject, IDisposable
{
    private readonly GraphEditorHostViewModel _host;
    private readonly IReadOnlyList<CanonicalStoryActorItem> _actorItems;
    private readonly IReadOnlyList<CanonicalStoryItemItem> _itemItems;
    private string _lineText = string.Empty;
    private string _speakerActorId = string.Empty;
    private string _choicePrompt = string.Empty;
    private string _endDisplayName = string.Empty;
    private string _logicOutputDisplayName = string.Empty;
    private string _objectiveType = CanonicalTaskObjectiveSchema.KillEntity;
    private string _objectiveDescription = string.Empty;
    private string _objectiveRequiredText = string.Empty;
    private string _objectiveRequiredError = string.Empty;
    private bool _objectivePrerequisiteEnabled;
    private string _objectiveTarget = string.Empty;
    private string _objectiveActorId = string.Empty;
    private IReadOnlyList<CanonicalSessionSpeakerOption> _speakerOptions = [];
    private IReadOnlyList<CanonicalSessionSpeakerOption> _objectiveActorOptions = [];
    private CanonicalSessionSpeakerOption? _selectedSpeaker;
    private CanonicalSessionSpeakerOption? _selectedObjectiveActor;
    private IReadOnlyList<CanonicalResourceSelectionOption> _objectiveItemOptions = [];
    private CanonicalResourceSelectionOption? _selectedObjectiveItem;
    private IReadOnlyList<CanonicalResourceSelectionOption> _storyActionItemOptions = [];
    private CanonicalResourceSelectionOption? _selectedStoryActionItem;
    private bool _disposed;
    private readonly bool _subscribeToHostChanges;
    private string _repeatPolicy = StoryStartSchema.Once;
    private string _actionType = CanonicalStoryActionSchema.SendMessage;
    private string _actionItem = string.Empty;
    private string _actionAmountText = string.Empty;
    private string _actionAmountError = string.Empty;
    private string _actionMessage = string.Empty;
    private bool _suppressTargetedRefresh;
    // WPF can synchronously write a newly projected ComboBox item back to a
    // TwoWay source while ItemsSource is being rebuilt. During that callback
    // the item is a projection of canonical state, never a new edit.
    private bool _isProjectingCanonicalChange;
    private readonly Dictionary<string, string> _textDraftErrors = new(StringComparer.Ordinal);

    public CanonicalNodeInspectorViewModel(
        GraphEditorHostViewModel host,
        GraphEditorNodeViewModel node,
        IEnumerable<CanonicalStoryActorItem>? actorItems = null,
        IEnumerable<CanonicalStoryItemItem>? itemItems = null,
        bool subscribeToHostChanges = true)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _actorItems = (actorItems ?? []).Where(item => item is not null).ToArray();
        foreach (var actor in _actorItems) actor.PortraitsChanged += OnActorPortraitsChanged;
        _itemItems = (itemItems ?? []).Where(item => item is not null).ToArray();
        RewardItemOptions = _itemItems.Where(item => item.Item is IndividualItemResource)
            .Select(item => new CanonicalResourceSelectionOption(item.Id, item.DisplayName, true, item)).ToArray();
        _subscribeToHostChanges = subscribeToHostChanges;
        AddChoiceOptionCommand = new RelayCommand(() => AddChoiceOption(), () => IsChoice);
        AddTaskResultSlotCommand = new RelayCommand(() => AddTaskResultSlot(), () => IsTaskSettle);
        AddStoryStartTriggerCommand = new RelayCommand(() => AddStoryStartTrigger(), () => IsStoryStart);
        AddRewardEntryCommand = new RelayCommand(AddRewardEntry, () => IsTaskReward);
        AudioState.Changed += OnLineAudioStateChanged;
        RefreshFromHost();
        if (_subscribeToHostChanges) _host.NodesChanged += HostOnNodesChanged;
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
    public bool IsPublicBoundary => NodeType is "terminate" or "end" or "logic_input" or "logic_output";
    public string BoundaryDisplayName
    {
        get => Node.Properties.TryGetValue("display_name", out var value) ? value.GetString() ?? "" : "";
        set { if (IsPublicBoundary && !_isProjectingCanonicalChange) _host.SetNodeProperty(NodeId, "display_name", value); }
    }
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
    public bool IsSubmitItemObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.SubmitItem;
    public bool HasObjectiveActor => IsKillEntityObjective || IsInteractActorObjective || IsSubmitItemObjective;
    public bool HasObjectiveQuantity => IsKillEntityObjective || IsItemObjective;
    public string ObjectiveActorLabel => IsSubmitItemObjective ? "提交对象" : "目标角色";
    public bool IsItemObjective => IsObjective && _objectiveType is CanonicalTaskObjectiveSchema.CollectItem or CanonicalTaskObjectiveSchema.SubmitItem;
    public bool IsReachRegionObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.ReachRegion;
    public bool IsInteractActorObjective => IsObjective && _objectiveType == CanonicalTaskObjectiveSchema.InteractActor;
    public bool HasEditableFields => IsLine || IsChoice || IsEnd || IsLogicOutput || IsTaskSettle || IsObjective
        || IsStoryStart || IsStoryAction || IsTaskReward || IsMusic || IsScreen || IsTitle || IsPublicBoundary;
    public bool HasInlineFields => HasEditableFields;

    public CanonicalStoryActionTypeOption? SelectedStoryActionType
    {
        get => StoryActionTypeOptions.FirstOrDefault(option => option.Value == _actionType);
        set
        {
            if (_isProjectingCanonicalChange || value is null || !IsStoryAction || value.Value == _actionType) return;
            var itemId = value.Value == CanonicalStoryActionSchema.GiveItem
                ? _itemItems.FirstOrDefault(item => item.Type == IndividualItemResource.ResourceType)?.Id
                : null;
            if (!_host.ChangeStoryActionType(NodeId, value.Value, itemId)) RefreshFromHost();
        }
    }

    public string StoryActionType
    {
        get => _actionType;
        set => SelectedStoryActionType = StoryActionTypeOptions.FirstOrDefault(option => option.Value == value);
    }
    public string StoryActionItem
    {
        get => _actionItem;
        set => SetActionString(CanonicalStoryActionSchema.ItemProperty, value, ref _actionItem, nameof(StoryActionItem));
    }
    public IReadOnlyList<CanonicalResourceSelectionOption> StoryActionItemOptions => _storyActionItemOptions;
    public CanonicalResourceSelectionOption? SelectedStoryActionItem
    {
        get => _selectedStoryActionItem;
        set => SetSelectedStoryActionItemId(value?.Id);
    }
    public string SelectedStoryActionItemId
    {
        get => _actionItem;
        set => SetSelectedStoryActionItemId(value);
    }
    public string StoryActionItemStatusText => _selectedStoryActionItem is { IsUnresolved: true }
        ? $"物品未解析：{_actionItem}"
        : string.Empty;
    public bool HasStoryActionItemStatus => !string.IsNullOrEmpty(StoryActionItemStatusText);
    public string StoryActionAmountText
    {
        get => _actionAmountText;
        set => SetActionInteger(CanonicalStoryActionSchema.AmountProperty, value, int.MinValue, ref _actionAmountText,
            nameof(StoryActionAmountText));
    }
    public string StoryActionAmountError => _actionAmountError;
    public string StoryActionMessage
    {
        get => _actionMessage;
        set => SetActionString(CanonicalStoryActionSchema.MessageProperty, value, ref _actionMessage,
            nameof(StoryActionMessage));
    }
    public string StoryActionMessageError => TextDraftError(nameof(StoryActionMessage));

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
            if (_isProjectingCanonicalChange || !IsStoryStart
                || string.Equals(_repeatPolicy, value, StringComparison.Ordinal)) return;
            if (!_host.SetStoryStartRepeatPolicy(NodeId, value))
            {
                OnPropertyChanged(nameof(RepeatPolicy));
                OnPropertyChanged(nameof(IsRepeatable));
            }
        }
    }

    /// <summary>
    /// Author-facing checkbox projection of the schema's string repeat policy.
    /// The canonical value remains <c>once</c> or <c>repeatable</c> so the
    /// existing session, persistence, and runtime contracts remain unchanged.
    /// </summary>
    public bool IsRepeatable
    {
        get => string.Equals(_repeatPolicy, StoryStartSchema.Repeatable, StringComparison.Ordinal);
        set => RepeatPolicy = value ? StoryStartSchema.Repeatable : StoryStartSchema.Once;
    }

    private readonly Dictionary<string, string> _invalidRegionValues = new();
    public string RegionDimension { get => ReadRegion("dimension_id"); set => SetRegion("dimension_id", value, nameof(RegionDimension)); }
    public string RegionX { get => ReadRegion("center_x"); set => SetRegion("center_x", value, nameof(RegionX)); }
    public string RegionY { get => ReadRegion("center_y"); set => SetRegion("center_y", value, nameof(RegionY)); }
    public string RegionZ { get => ReadRegion("center_z"); set => SetRegion("center_z", value, nameof(RegionZ)); }
    public string RegionRadius { get => ReadRegion("radius"); set => SetRegion("radius", value, nameof(RegionRadius)); }
    private string ReadRegion(string key)
    {
        if (_invalidRegionValues.TryGetValue(key, out var pending)) return pending;
        var node = _host.Nodes.FirstOrDefault(item => item.NodeId == NodeId);
        return node?.Properties.TryGetValue(key, out var value) == true ? value.ToString() : "";
    }
    private void SetRegion(string key, string value, string propertyName)
    {
        if (!IsReachRegionObjective || _isProjectingCanonicalChange) return;
        JsonElement json;
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)
                || (key == "radius" && number < 0))
            {
                _invalidRegionValues[key] = value ?? "";
                _host.SetAuthoringIssue($"{NodeId}:region:{key}", new ValidationIssue("graph.objective.region.authoring", "请输入整数方块坐标；半径必须为非负整数。", key, ValidationSeverity.Error, NodeId));
                OnPropertyChanged(propertyName);
                return;
            }
            json = JsonSerializer.SerializeToElement(number);
        }
        _invalidRegionValues.Remove(key);
        _host.SetAuthoringIssue($"{NodeId}:region:{key}", null);
        _host.SetNodeProperty(NodeId, key, json);
        OnPropertyChanged(propertyName);
    }
    private void NotifyRegionFields()
    {
        OnPropertyChanged(nameof(RegionDimension));
        OnPropertyChanged(nameof(RegionX));
        OnPropertyChanged(nameof(RegionY));
        OnPropertyChanged(nameof(RegionZ));
        OnPropertyChanged(nameof(RegionRadius));
    }

    public IReadOnlyList<CanonicalObjectiveTypeOption> ObjectiveTypeOptions { get; } =
    [
        new(CanonicalTaskObjectiveSchema.KillEntity, "实体击杀"),
        new(CanonicalTaskObjectiveSchema.InteractActor, "角色交互"),
        new(CanonicalTaskObjectiveSchema.CollectItem, "物品收集"),
        new(CanonicalTaskObjectiveSchema.SubmitItem, "物品提交"),
        new(CanonicalTaskObjectiveSchema.ReachRegion, "区域到达"),
    ];

    public CanonicalObjectiveTypeOption? SelectedObjectiveType
    {
        get => ObjectiveTypeOptions.FirstOrDefault(option => option.Value == _objectiveType);
        set
        {
            if (_isProjectingCanonicalChange || value is null || !IsObjective || value.Value == _objectiveType) return;
            var actorId = value.Value == CanonicalTaskObjectiveSchema.InteractActor
                ? _actorItems.FirstOrDefault()?.Id
                : null;
            // ChangeObjectiveType replaces the complete typed payload in one
            // host/session transaction. The schema's explicit unselected
            // target is legal authoring state; appending a default target edit
            // here would create a second revision/Undo unit.
            if (!_host.ChangeObjectiveType(NodeId, value.Value, actorId)) RefreshFromHost();
        }
    }

    public string ObjectiveType => _objectiveType;
    public string ObjectiveDescription
    {
        get => _objectiveDescription;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_objectiveDescription, next, StringComparison.Ordinal)
                && string.IsNullOrEmpty(ObjectiveDescriptionError)) return;
            _objectiveDescription = next;
            var committed = _host.SetObjectiveDescription(NodeId, next);
            SetTextDraftError(nameof(ObjectiveDescription), committed ? string.Empty : InvalidTextDraftMessage);
            OnPropertyChanged();
        }
    }
    public string ObjectiveDescriptionError => TextDraftError(nameof(ObjectiveDescription));

    public string ObjectiveRequiredText
    {
        get => _objectiveRequiredText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_objectiveRequiredText, next, StringComparison.Ordinal)) return;
            _objectiveRequiredText = next;
            if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 1 && (_host.SetObjectiveRequired(NodeId, parsed)
                    || HasPersistedInteger(CanonicalTaskObjectiveSchema.RequiredProperty, parsed)))
                SetObjectiveRequiredError(string.Empty);
            else
                SetObjectiveRequiredError("请输入不小于 1 的整数。");
            OnPropertyChanged();
            OnPropertyChanged(nameof(ObjectiveRequired));
        }
    }

    public string ObjectiveRequiredError => _objectiveRequiredError;

    public int ObjectiveRequired => int.TryParse(_objectiveRequiredText, out var value) ? value : 0;
    public bool ObjectivePrerequisiteEnabled
    {
        get => _objectivePrerequisiteEnabled;
        set
        {
            if (_isProjectingCanonicalChange || !IsObjective
                || _objectivePrerequisiteEnabled == value) return;
            if (_host.SetObjectivePrerequisiteEnabled(NodeId, value))
            {
                _objectivePrerequisiteEnabled = value;
                OnPropertyChanged();
            }
            else RefreshFromHost();
        }
    }
    public string ObjectivePrerequisiteHelpText => "前置条件为 True 时激活";
    public string ObjectiveTarget
    {
        get => _objectiveTarget;
        set
        {
            var next = value ?? string.Empty;
            if (!IsKillEntityObjective && !IsItemObjective) return;
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
        set => SetSelectedObjectiveActorId(value?.Id);
    }
    public string SelectedObjectiveActorId
    {
        get => _objectiveActorId;
        set => SetSelectedObjectiveActorId(value);
    }
    public string ObjectiveActorId => _objectiveActorId;
    public bool IsObjectiveActorResolved => !string.IsNullOrWhiteSpace(_objectiveActorId)
        && _objectiveActorOptions.Any(option => option.IsResolved && option.Id == _objectiveActorId);
    public bool IsObjectiveActorUnresolved => !string.IsNullOrWhiteSpace(_objectiveActorId) && !IsObjectiveActorResolved;
    public string ObjectiveActorStatusText => IsObjectiveActorUnresolved
        ? $"角色未解析：{_objectiveActorId}" : string.Empty;
    public bool HasObjectiveActorStatus => !string.IsNullOrEmpty(ObjectiveActorStatusText);
    public IReadOnlyList<CanonicalResourceSelectionOption> ObjectiveItemOptions => _objectiveItemOptions;
    public CanonicalResourceSelectionOption? SelectedObjectiveItem
    {
        get => _selectedObjectiveItem;
        set => SetSelectedObjectiveItemId(value?.Id);
    }
    public string SelectedObjectiveItemId
    {
        get => _objectiveTarget;
        set => SetSelectedObjectiveItemId(value);
    }
    public string ObjectiveItemStatusText => _selectedObjectiveItem is { IsUnresolved: true }
        ? $"物品未解析：{_objectiveTarget}"
        : string.Empty;
    public bool HasObjectiveItemStatus => !string.IsNullOrEmpty(ObjectiveItemStatusText);

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
    public string SelectedSpeakerId
    {
        get => _speakerActorId;
        set => SetSelectedSpeakerId(value);
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
                SelectedSpeaker = null;
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
        : string.Empty;
    public bool HasSpeakerStatus => !string.IsNullOrEmpty(SpeakerStatusText);

    public string LineText
    {
        get => _lineText;
        set => SetStringProperty("text", value, ref _lineText, nameof(LineText));
    }
    public string LineTextError => TextDraftError(nameof(LineText));

    public string Text { get => LineText; set => LineText = value; }

    public string ChoicePrompt
    {
        get => _choicePrompt;
        set => SetStringProperty(SessionChoiceSchema.PromptProperty, value, ref _choicePrompt, nameof(ChoicePrompt));
    }
    public string ChoicePromptError => TextDraftError(nameof(ChoicePrompt));

    public string Prompt { get => ChoicePrompt; set => ChoicePrompt = value; }

    public string EndDisplayName
    {
        get => _endDisplayName;
        set => SetStringProperty("display_name", value, ref _endDisplayName, nameof(EndDisplayName));
    }
    public string EndDisplayNameError => TextDraftError(nameof(EndDisplayName));

    public string LogicOutputDisplayName
    {
        get => _logicOutputDisplayName;
        set => SetStringProperty("display_name", value, ref _logicOutputDisplayName, nameof(LogicOutputDisplayName));
    }
    public string LogicOutputDisplayNameError => TextDraftError(nameof(LogicOutputDisplayName));

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
    public int RefreshCount { get; private set; }

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

    public bool AddStoryStartTrigger(string? displayName = null,
        string triggerType = StoryStartSchema.RegionEntry,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
    {
        if (!IsStoryStart) return false;
        return _host.AddStoryStartTrigger(NodeId, displayName ?? AllocateStoryStartDisplayName(), triggerType, triggerProperties);
    }

    public bool SetStoryStartTriggerType(string portId, string triggerType)
    {
        if (!IsStoryStart) return false;
        var actorId = _actorItems.FirstOrDefault()?.Id;
        return _host.SetStoryStartTriggerType(NodeId, portId, triggerType, actorId);
    }

    internal bool SetStoryStartTriggerTypeLocally(string portId, string triggerType)
    {
        if (!IsStoryStart) return false;
        var actorId = _actorItems.FirstOrDefault()?.Id;
        return ExecuteLocalMutation(() => _host.SetStoryStartTriggerType(NodeId, portId, triggerType, actorId));
    }

    public bool SetStoryStartTriggerProperties(string portId,
        IReadOnlyDictionary<string, JsonElement> triggerProperties)
    {
        if (!IsStoryStart) return false;
        return _host.SetStoryStartTriggerProperties(NodeId, portId, triggerProperties);
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
        return ExecuteLocalMutation(() => _host.SetStoryStartTriggerProperties(NodeId, portId, properties));
    }

    public bool RenameStoryStartTrigger(string portId, string displayName)
    {
        if (!IsStoryStart) return false;
        return _host.RenameStoryStartTrigger(NodeId, portId, displayName);
    }

    internal bool RenameStoryStartTriggerLocally(string portId, string displayName)
        => IsStoryStart && ExecuteLocalMutation(() => _host.RenameStoryStartTrigger(NodeId, portId, displayName));

    public bool ReorderStoryStartTrigger(string portId, int order)
    {
        if (!IsStoryStart) return false;
        return _host.ReorderStoryStartTrigger(NodeId, portId, order);
    }

    public bool RemoveStoryStartTrigger(string portId)
    {
        if (!IsStoryStart) return false;
        var result = _host.RemoveStoryStartTrigger(NodeId, portId);
        if (result) return true;
        if (!_host.LastValidationIssues.Any(issue => issue.Code == "graph.story.start.trigger.references.confirmation_required")) return false;
        var slot = StoryStartTriggers.FirstOrDefault(item => item.Identity == portId);
        if (slot is null || StoryStartTriggerRemovalConfirmationRequested is null
            || !StoryStartTriggerRemovalConfirmationRequested(new CanonicalStoryStartTriggerRemovalConfirmation(slot.DisplayName))) return false;
        return _host.RemoveStoryStartTrigger(NodeId, portId, true);
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

    public bool AddTaskResultSlot(string? displayName = null)
        => IsTaskSettle && Execute(() => _host.AddDynamicPort(
            NodeId, string.IsNullOrWhiteSpace(displayName) ? NextAvailableResultName() : displayName,
            GraphPortDirection.Input, GraphInterfaceKind.Logic));

    public bool AddResultSlot(string? displayName = null) => AddTaskResultSlot(displayName);

    private string NextAvailableResultName()
    {
        var used = TaskResultSlots.Select(slot => slot.DisplayName).ToHashSet(StringComparer.Ordinal);
        for (var index = 1; ; index++)
        {
            var candidate = $"结果 {index}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

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
        AudioState.Changed -= OnLineAudioStateChanged;
        foreach (var actor in _actorItems) actor.PortraitsChanged -= OnActorPortraitsChanged;
        if (_subscribeToHostChanges) _host.NodesChanged -= HostOnNodesChanged;
        _host.PropertyChanged -= HostOnPropertyChanged;
    }

    private void SetStringProperty(string property, string? value, ref string field, string propertyName)
    {
        if (_disposed || !CanEditProperty(property)) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)
            && string.IsNullOrEmpty(TextDraftError(propertyName))) return;
        field = next;
        var committed = _host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(next));
        SetTextDraftError(propertyName, committed ? string.Empty : InvalidTextDraftMessage);
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(LineText)) OnPropertyChanged(nameof(Text));
        if (propertyName == nameof(ChoicePrompt)) OnPropertyChanged(nameof(Prompt));
    }

    private void SetActionString(string property, string? value, ref string field, string propertyName)
    {
        if (_disposed || !IsStoryAction) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)
            && string.IsNullOrEmpty(TextDraftError(propertyName))) return;
        field = next;
        var committed = _host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(next));
        SetTextDraftError(propertyName, committed ? string.Empty : InvalidTextDraftMessage);
        OnPropertyChanged(propertyName);
    }

    private const string InvalidTextDraftMessage = "该内容暂时无效；已保留草稿，请修改后再提交。";

    private string TextDraftError(string propertyName)
        => _textDraftErrors.TryGetValue(propertyName, out var error) ? error : string.Empty;

    private void SetTextDraftError(string propertyName, string message)
    {
        if (string.IsNullOrEmpty(message)) _textDraftErrors.Remove(propertyName);
        else _textDraftErrors[propertyName] = message;
        OnPropertyChanged($"{propertyName}Error");
        _host.SetAuthoringIssue($"{NodeId}:draft:{propertyName}",
            string.IsNullOrEmpty(message) ? null : new ValidationIssue(
                "graph.text.authoring_invalid", message, propertyName,
                ValidationSeverity.Error, NodeId));
    }

    private void SetActionInteger(string property, string? value, int minimum, ref string field, string propertyName)
    {
        if (_disposed || !IsStoryAction) return;
        var next = value ?? string.Empty;
        if (string.Equals(field, next, StringComparison.Ordinal)) return;
        field = next;
        if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum
            && (_host.SetNodeProperty(NodeId, property, JsonSerializer.SerializeToElement(parsed))
                || HasPersistedInteger(property, parsed)))
            SetActionAmountError(string.Empty);
        else SetActionAmountError(minimum == int.MinValue ? "请输入整数：正数增加、负数减少、零不变。" : $"请输入不小于 {minimum} 的整数。");
        OnPropertyChanged(propertyName);
    }

    private void SetObjectiveRequiredError(string message)
    {
        if (!SetProperty(ref _objectiveRequiredError, message, nameof(ObjectiveRequiredError))) return;
        _host.SetAuthoringIssue($"{NodeId}:objective_required",
            string.IsNullOrEmpty(message) ? null : new ValidationIssue(
                "graph.objective.required.authoring_invalid", message,
                CanonicalTaskObjectiveSchema.RequiredProperty, ValidationSeverity.Error, NodeId));
    }

    private bool HasPersistedInteger(string property, int expected)
        => _host.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, NodeId, StringComparison.Ordinal))
            ?.Properties.TryGetValue(property, out var value) == true
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var actual)
            && actual == expected;

    private void SetActionAmountError(string message)
    {
        if (!SetProperty(ref _actionAmountError, message, nameof(StoryActionAmountError))) return;
        _host.SetAuthoringIssue($"{NodeId}:action_amount",
            string.IsNullOrEmpty(message) ? null : new ValidationIssue(
                "graph.story.action.amount.authoring_invalid", message,
                CanonicalStoryActionSchema.AmountProperty, ValidationSeverity.Error, NodeId));
    }

    private void SetSelectedStoryActionItemId(string? id)
    {
        var next = id ?? string.Empty;
        if (!IsGiveItemAction || string.IsNullOrWhiteSpace(next)
            || string.Equals(next, _actionItem, StringComparison.Ordinal)) return;
        if (!_host.SetNodeProperty(NodeId, CanonicalStoryActionSchema.ItemProperty,
                JsonSerializer.SerializeToElement(next))) RefreshFromHost();
    }

    private void SetSelectedObjectiveActorId(string? id)
    {
        var next = id ?? string.Empty;
        if (!HasObjectiveActor
            || string.IsNullOrWhiteSpace(next)
            || string.Equals(next, _objectiveActorId, StringComparison.Ordinal)) return;
        if (!(IsSubmitItemObjective
            ? _host.SetNodeProperty(NodeId, CanonicalTaskObjectiveSchema.ActorIdProperty, JsonSerializer.SerializeToElement(next))
            : _host.ChangeObjectiveTarget(NodeId, next))) RefreshFromHost();
    }

    private void SetSelectedObjectiveItemId(string? id)
    {
        var next = id ?? string.Empty;
        if (!IsItemObjective || string.IsNullOrWhiteSpace(next)
            || string.Equals(next, _objectiveTarget, StringComparison.Ordinal)) return;
        if (!_host.ChangeObjectiveTarget(NodeId, next)) RefreshFromHost();
    }

    private void SetSelectedSpeaker(CanonicalSessionSpeakerOption? option)
    {
        // The unresolved item is a preservation projection, not an editable
        // Actor reference. Selecting it therefore keeps its existing ID.
        var next = option is null || option.IsPlaceholder
            ? string.Empty
            : option.Id;
        SetSelectedSpeakerId(next);
    }

    private void SetSelectedSpeakerId(string? id)
    {
        if (_disposed || !IsLine) return;
        var next = id ?? string.Empty;
        if (string.Equals(_speakerActorId, next, StringComparison.Ordinal)) return;

        if (!_host.ChangeSessionSpeaker(NodeId, next))
            RefreshFromHost();
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

    private void HostOnNodesChanged(object? sender, GraphNodesChangedEventArgs args)
    {
        if (!_suppressTargetedRefresh && args.NodeIds.Contains(NodeId, StringComparer.Ordinal)) RefreshFromHost();
    }

    internal void RefreshCanonicalProjection() => RefreshFromHost();

    internal bool TryGetStoryStartTrigger(string portId, out StoryStartTriggerSlot? slot)
    {
        var current = _host.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, NodeId, StringComparison.Ordinal));
        if (current is null)
        {
            slot = null;
            return false;
        }
        var node = new GraphNode(current.NodeId, current.Type, current.DisplayName,
            current.Inputs.Concat(current.Outputs).Select(port => new GraphPort(port.PortId,
                port.DisplayName, port.IsInput, port.GraphInterfaceKind, port.Order)), current.Properties);
        slot = StoryStartSchema.ReadTriggers(node).FirstOrDefault(candidate =>
            string.Equals(candidate.PortId, portId, StringComparison.Ordinal));
        return slot is not null;
    }

    private bool ExecuteLocalMutation(Func<bool> mutation)
    {
        _suppressTargetedRefresh = true;
        try { return mutation(); }
        finally { _suppressTargetedRefresh = false; }
    }

    private string AllocateStoryStartDisplayName()
    {
        var used = StoryStartTriggers.Select(trigger => trigger.DisplayName).ToHashSet(StringComparer.Ordinal);
        for (var index = 1; ; index++)
        {
            var candidate = $"启动条件 {index}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    private void HostOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(GraphEditorHostViewModel.LastValidationIssues))
            OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(HelpText));
        OnPropertyChanged(nameof(BoundaryDisplayName));
    }

    public IReadOnlyList<ValidationIssue> ValidationIssues
        => _host.LastValidationIssues;

    private bool CanEditProperty(string property)
        => property == "speaker_actor_id" && IsLine
            || property == "text" && IsLine
            || property == SessionChoiceSchema.PromptProperty && IsChoice
            || property == "display_name" && (IsEnd || IsLogicOutput);

    private void RefreshFromHost()
    {
        if (_isProjectingCanonicalChange) return;
        var current = _host.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.NodeId, NodeId, StringComparison.Ordinal));
        if (current is null) return;
        _isProjectingCanonicalChange = true;
        try
        {
            RefreshFromHostCore(current);
        }
        finally
        {
            _isProjectingCanonicalChange = false;
        }
    }

    private void RefreshFromHostCore(GraphEditorNodeViewModel current)
    {
        RefreshCount++;
        RefreshRewardEntries(current);

        _repeatPolicy = current.Properties.TryGetValue(StoryStartSchema.RepeatPolicyProperty, out var repeat)
            && repeat.ValueKind == JsonValueKind.String ? repeat.GetString() ?? StoryStartSchema.Once : StoryStartSchema.Once;
        var desiredTriggers = new List<CanonicalStoryStartTriggerViewModel>();
        if (IsStoryStart)
        {
            foreach (var slot in StoryStartSchema.ReadTriggers(new GraphNode(current.NodeId, current.Type,
                current.DisplayName, current.Inputs.Concat(current.Outputs).Select(port => new GraphPort(port.PortId,
                    port.DisplayName, port.IsInput, port.GraphInterfaceKind, port.Order)), current.Properties)))
            {
                var trigger = StoryStartTriggers.FirstOrDefault(item => item.Identity == slot.PortId);
                if (trigger is null) trigger = new CanonicalStoryStartTriggerViewModel(this, slot);
                else trigger.UpdateProjection(slot);
                desiredTriggers.Add(trigger);
            }
        }
        foreach (var removed in StoryStartTriggers.Where(item => !desiredTriggers.Contains(item)).ToArray())
            StoryStartTriggers.Remove(removed);
        for (var index = 0; index < desiredTriggers.Count; index++)
        {
            var existing = StoryStartTriggers.IndexOf(desiredTriggers[index]);
            if (existing < 0) StoryStartTriggers.Insert(index, desiredTriggers[index]);
            else if (existing != index) StoryStartTriggers.Move(existing, index);
        }
        foreach (var trigger in StoryStartTriggers) trigger.RefreshCommandStates();

        _speakerActorId = current.Properties.TryGetValue("speaker_actor_id", out var speaker)
            && speaker.ValueKind == JsonValueKind.String
            ? speaker.GetString() ?? string.Empty
            : string.Empty;
        _actionType = ReadString(current, CanonicalStoryActionSchema.TypeProperty);
        RefreshActionExtras();
        _actionItem = ReadString(current, CanonicalStoryActionSchema.ItemProperty);
        if (string.IsNullOrEmpty(_actionAmountError))
            _actionAmountText = current.Properties.TryGetValue(CanonicalStoryActionSchema.AmountProperty, out var actionAmount)
                && actionAmount.ValueKind == JsonValueKind.Number ? actionAmount.ToString() : string.Empty;
        if (string.IsNullOrEmpty(StoryActionMessageError))
            _actionMessage = ReadString(current, CanonicalStoryActionSchema.MessageProperty);
        if (string.IsNullOrEmpty(LineTextError)
            && current.Properties.TryGetValue("text", out var text) && text.ValueKind == JsonValueKind.String)
            _lineText = text.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(ChoicePromptError)
            && current.Properties.TryGetValue(SessionChoiceSchema.PromptProperty, out var prompt)
            && prompt.ValueKind == JsonValueKind.String)
            _choicePrompt = prompt.GetString() ?? string.Empty;
        if (current.Properties.TryGetValue("display_name", out var display) && display.ValueKind == JsonValueKind.String)
        {
            if (string.IsNullOrEmpty(EndDisplayNameError))
                _endDisplayName = display.GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(LogicOutputDisplayNameError))
                _logicOutputDisplayName = display.GetString() ?? string.Empty;
        }

        _objectiveType = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.TypeProperty, out var objectiveType)
            && objectiveType.ValueKind == JsonValueKind.String
            ? objectiveType.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrEmpty(ObjectiveDescriptionError))
            _objectiveDescription = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.DescriptionProperty, out var objectiveDescription)
                && objectiveDescription.ValueKind == JsonValueKind.String
                ? objectiveDescription.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrEmpty(_objectiveRequiredError))
            _objectiveRequiredText = current.Properties.TryGetValue(CanonicalTaskObjectiveSchema.RequiredProperty, out var objectiveRequired)
                && objectiveRequired.ValueKind == JsonValueKind.Number
                ? objectiveRequired.ToString() : string.Empty;
        _objectivePrerequisiteEnabled = current.Properties.TryGetValue(
                CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty, out var prerequisiteEnabled)
            && prerequisiteEnabled.ValueKind is JsonValueKind.True or JsonValueKind.False
            && prerequisiteEnabled.GetBoolean();
        _objectiveTarget = IsKillEntityObjective
            ? ReadString(current, CanonicalTaskObjectiveSchema.EntityProperty)
            : IsItemObjective ? ReadString(current, CanonicalTaskObjectiveSchema.ItemProperty) : string.Empty;
        _objectiveActorId = IsKillEntityObjective
            ? ReadString(current, CanonicalTaskObjectiveSchema.EntityProperty)
            : ReadString(current, CanonicalTaskObjectiveSchema.ActorIdProperty);

        RebuildSpeakerOptions();
        RebuildObjectiveActorOptions();
        RebuildItemOptions();

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

        OnPropertyChanged(nameof(PortraitVariantOptions));
        OnPropertyChanged(nameof(SelectedPortraitMediaRef));
        OnPropertyChanged(nameof(SelectedPortraitActor));
        OnPropertyChanged(nameof(SelectedPortraitVariant));
        OnPropertyChanged(nameof(HasLineSpeaker));
        OnPropertyChanged(nameof(LineVoiceRef));
        OnPropertyChanged(nameof(LineTextSpeed));
        OnPropertyChanged(nameof(IsLineTextSpeedCustom));
        OnPropertyChanged(nameof(LineVoiceVolume));
        OnPropertyChanged(nameof(LineVoiceVolumeValue));
        OnPropertyChanged(nameof(LineVoiceVolumeDraft));
        OnPropertyChanged(nameof(LineVoiceVolumeDisplayValue));
        OnPropertyChanged(nameof(LineVoiceVolumeLabel));
        OnPropertyChanged(nameof(IsLineAudioEnabled));
        OnPropertyChanged(nameof(AudioMediaRef));
        OnPropertyChanged(nameof(LineVoiceStatus));
        NotifyPresentation();
        NotifyTitle();
        OnPropertyChanged(nameof(LineText));
        OnPropertyChanged(nameof(LineTextError));
        OnPropertyChanged(nameof(Text));
        NotifySpeakerPropertiesChanged();
        OnPropertyChanged(nameof(ChoicePrompt));
        OnPropertyChanged(nameof(ChoicePromptError));
        OnPropertyChanged(nameof(Prompt));
        OnPropertyChanged(nameof(EndDisplayName));
        OnPropertyChanged(nameof(EndDisplayNameError));
        OnPropertyChanged(nameof(LogicOutputDisplayName));
        OnPropertyChanged(nameof(LogicOutputDisplayNameError));
        OnPropertyChanged(nameof(RepeatPolicy));
        OnPropertyChanged(nameof(IsRepeatable));
        OnPropertyChanged(nameof(SelectedRepeatPolicy));
        OnPropertyChanged(nameof(StoryStartTriggers));
        OnPropertyChanged(nameof(TriggerSlots));
        OnPropertyChanged(nameof(ObjectiveType));
        OnPropertyChanged(nameof(SelectedObjectiveType));
        OnPropertyChanged(nameof(ObjectiveDescription));
        OnPropertyChanged(nameof(ObjectiveDescriptionError));
        OnPropertyChanged(nameof(ObjectiveRequiredText));
        OnPropertyChanged(nameof(ObjectiveRequiredError));
        OnPropertyChanged(nameof(ObjectiveRequired));
        OnPropertyChanged(nameof(ObjectivePrerequisiteEnabled));
        OnPropertyChanged(nameof(ObjectivePrerequisiteHelpText));
        OnPropertyChanged(nameof(ObjectiveTarget));
        OnPropertyChanged(nameof(IsKillEntityObjective));
        OnPropertyChanged(nameof(IsItemObjective));
        OnPropertyChanged(nameof(IsCollectItemObjective));
        OnPropertyChanged(nameof(IsReachRegionObjective));
        OnPropertyChanged(nameof(IsSubmitItemObjective));
        OnPropertyChanged(nameof(HasObjectiveActor));
        OnPropertyChanged(nameof(HasObjectiveQuantity));
        OnPropertyChanged(nameof(ObjectiveActorLabel));
        NotifyRegionFields();
        OnPropertyChanged(nameof(IsInteractActorObjective));
        OnPropertyChanged(nameof(ObjectiveActorOptions));
        OnPropertyChanged(nameof(SelectedObjectiveActor));
        OnPropertyChanged(nameof(SelectedObjectiveActorId));
        OnPropertyChanged(nameof(ObjectiveActorId));
        OnPropertyChanged(nameof(IsObjectiveActorResolved));
        OnPropertyChanged(nameof(IsObjectiveActorUnresolved));
        OnPropertyChanged(nameof(ObjectiveActorStatusText));
        OnPropertyChanged(nameof(HasObjectiveActorStatus));
        OnPropertyChanged(nameof(ObjectiveItemOptions));
        OnPropertyChanged(nameof(SelectedObjectiveItem));
        OnPropertyChanged(nameof(SelectedObjectiveItemId));
        OnPropertyChanged(nameof(ObjectiveItemStatusText));
        OnPropertyChanged(nameof(HasObjectiveItemStatus));
        OnPropertyChanged(nameof(StoryActionType));
        OnPropertyChanged(nameof(SelectedStoryActionType));
        OnPropertyChanged(nameof(StoryActionItem));
        OnPropertyChanged(nameof(StoryActionItemOptions));
        OnPropertyChanged(nameof(SelectedStoryActionItem));
        OnPropertyChanged(nameof(SelectedStoryActionItemId));
        OnPropertyChanged(nameof(StoryActionItemStatusText));
        OnPropertyChanged(nameof(HasStoryActionItemStatus));
        OnPropertyChanged(nameof(StoryActionAmountText));
        OnPropertyChanged(nameof(StoryActionAmountError));
        OnPropertyChanged(nameof(StoryActionMessage));
        OnPropertyChanged(nameof(StoryActionMessageError));
        OnPropertyChanged(nameof(IsGiveItemAction));
        OnPropertyChanged(nameof(IsGiveXpAction));
        OnPropertyChanged(nameof(IsSendMessageAction));
        OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(HelpText));
        OnPropertyChanged(nameof(BoundaryDisplayName));
        AddChoiceOptionCommand.RaiseCanExecuteChanged();
        AddTaskResultSlotCommand.RaiseCanExecuteChanged();
    }

    private void RebuildSpeakerOptions()
    {
        var options = new List<CanonicalSessionSpeakerOption>();
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

        if (!_speakerOptions.SequenceEqual(options)) _speakerOptions = options;
        _selectedSpeaker = _speakerOptions.FirstOrDefault(option =>
            string.Equals(option.Id, _speakerActorId, StringComparison.Ordinal));
    }

    private void RebuildObjectiveActorOptions()
    {
        // Objective targets are required semantic references.  A selectable
        // "请选择角色" row is neither a resource nor a useful authoring action;
        // represent the absence of a target with a null selection instead.
        var options = new List<CanonicalSessionSpeakerOption>();
        options.AddRange(_actorItems
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new CanonicalSessionSpeakerOption(item.Id, item.DisplayName, true, item)));
        if (!string.IsNullOrWhiteSpace(_objectiveActorId)
            && !options.Any(option => option.IsResolved && option.Id == _objectiveActorId))
            options.Add(new CanonicalSessionSpeakerOption(_objectiveActorId,
                $"未解析角色：{_objectiveActorId}", false));
        if (!_objectiveActorOptions.SequenceEqual(options)) _objectiveActorOptions = options;
        _selectedObjectiveActor = _objectiveActorOptions.FirstOrDefault(option => option.Id == _objectiveActorId);
    }

    private void RebuildItemOptions()
    {
        var objectiveOptions = BuildItemOptions(_itemItems, _objectiveTarget, individualOnly: false);
        if (!_objectiveItemOptions.SequenceEqual(objectiveOptions)) _objectiveItemOptions = objectiveOptions;
        _selectedObjectiveItem = _objectiveItemOptions.FirstOrDefault(option => option.Id == _objectiveTarget);
        var actionOptions = BuildItemOptions(_itemItems, _actionItem, individualOnly: true);
        if (!_storyActionItemOptions.SequenceEqual(actionOptions)) _storyActionItemOptions = actionOptions;
        _selectedStoryActionItem = _storyActionItemOptions.FirstOrDefault(option => option.Id == _actionItem);
    }

    private static IReadOnlyList<CanonicalResourceSelectionOption> BuildItemOptions(
        IEnumerable<CanonicalStoryItemItem> items,
        string currentId,
        bool individualOnly)
    {
        var options = new List<CanonicalResourceSelectionOption>();
        options.AddRange(items
            .Where(item => !individualOnly || item.Type == IndividualItemResource.ResourceType)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new CanonicalResourceSelectionOption(item.Id, item.DisplayName, true, item)));
        if (!string.IsNullOrWhiteSpace(currentId) && !options.Any(option => option.IsResolved && option.Id == currentId))
            options.Add(new CanonicalResourceSelectionOption(currentId, $"未解析物品：{currentId}", false));
        return options;
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
        OnPropertyChanged(nameof(SelectedSpeakerId));
        OnPropertyChanged(nameof(SelectedSpeakerActor));
        OnPropertyChanged(nameof(SelectedActor));
        OnPropertyChanged(nameof(SpeakerActorId));
        OnPropertyChanged(nameof(IsSpeakerResolved));
        OnPropertyChanged(nameof(IsSpeakerUnresolved));
        OnPropertyChanged(nameof(SpeakerStatusText));
        OnPropertyChanged(nameof(HasSpeakerStatus));
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
    public bool IsPlaceholder => string.IsNullOrWhiteSpace(Id);
    public bool IsUnresolved => !IsResolved && !IsPlaceholder;
    public string StableId => Id;
    public CanonicalStoryActorItem? Actor => ActorItem;
    public bool IsOwned => ActorItem?.IsOwned == true;
    public bool IsReferenced => ActorItem?.IsReferenced == true;
}

public sealed record CanonicalResourceSelectionOption(
    string Id,
    string DisplayName,
    bool IsResolved,
    CanonicalStoryItemItem? Item = null)
{
    public bool IsPlaceholder => string.IsNullOrWhiteSpace(Id);
    public bool IsUnresolved => !IsResolved && !IsPlaceholder;
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
    private bool _projecting;
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _displayName;
    private string _triggerType;
    private JsonElement _triggerProperties;
    private string _dimensionText = string.Empty;
    private string _xText = string.Empty;
    private string _yText = string.Empty;
    private string _zText = string.Empty;
    private string _radiusText = string.Empty;
    private string _dimensionError = string.Empty;
    private string _xError = string.Empty;
    private string _yError = string.Empty;
    private string _zError = string.Empty;
    private string _radiusError = string.Empty;
    private int _order;

    internal CanonicalStoryStartTriggerViewModel(CanonicalNodeInspectorViewModel owner, StoryStartTriggerSlot slot)
    {
        _owner = owner;
        Identity = slot.PortId;
        _displayName = slot.DisplayName;
        _triggerType = slot.TriggerType;
        _triggerProperties = slot.TriggerProperties.Clone();
        LoadPropertyFields(clearErrors: false);
        _order = slot.Order;
        RemoveCommand = new RelayCommand(
            () => _owner.RemoveStoryStartTrigger(Identity),
            () => _owner.StoryStartTriggers.Count > 1);
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
            if (_projecting || string.Equals(_displayName, value, StringComparison.Ordinal)) return;
            if (_owner.RenameStoryStartTriggerLocally(Identity, value))
            {
                _displayName = value;
                OnPropertyChanged();
            }
        }
    }

    private static readonly IReadOnlyList<CanonicalStoryStartTriggerTypeOption> CurrentTriggerOptions =
        [new(StoryStartSchema.ActorInteraction, "角色交互"), new(StoryStartSchema.RegionEntry, "进入区域"),
         new(StoryStartSchema.Logic, "逻辑条件"), new(StoryStartSchema.FlowDriven, "流程驱动")];
    private static readonly IReadOnlyList<CanonicalStoryStartTriggerTypeOption> LegacyTriggerOptions =
        [new(StoryStartSchema.EnterStory, "进入故事（旧版兼容）"), .. CurrentTriggerOptions];
    public IReadOnlyList<CanonicalStoryStartTriggerTypeOption> TriggerTypeOptions
        => _triggerType == StoryStartSchema.EnterStory ? LegacyTriggerOptions : CurrentTriggerOptions;

    public string TriggerType => _triggerType;
    public CanonicalStoryStartTriggerTypeOption? SelectedTriggerType
    {
        get => TriggerTypeOptions.FirstOrDefault(option => option.Value == _triggerType);
        set
        {
            if (_projecting || value is null || value.Value == _triggerType) return;
            if (_owner.SetStoryStartTriggerTypeLocally(Identity, value.Value))
            {
                if (_owner.TryGetStoryStartTrigger(Identity, out var slot) && slot is not null)
                {
                    _triggerType = slot.TriggerType;
                    _triggerProperties = slot.TriggerProperties.Clone();
                }
                else
                {
                    _triggerType = value.Value;
                    _triggerProperties = JsonSerializer.SerializeToElement(
                        StoryStartSchema.DefaultTriggerProperties(value.Value));
                }
                OnPropertyChanged(nameof(TriggerType));
                OnPropertyChanged(nameof(SelectedTriggerType));
                OnPropertyChanged(nameof(IsActorInteraction));
                OnPropertyChanged(nameof(IsRegionEntry));
                OnPropertyChanged(nameof(IsEnterStory));
                OnPropertyChanged(nameof(IsLogic));
                LoadPropertyFields(clearErrors: true);
                NotifyPropertyFields();
            }
        }
    }

    public JsonElement TriggerProperties => _triggerProperties;
    public bool IsActorInteraction => _triggerType == StoryStartSchema.ActorInteraction;
    public bool IsRegionEntry => _triggerType == StoryStartSchema.RegionEntry;
    public bool IsEnterStory => _triggerType == StoryStartSchema.EnterStory;
    public bool IsLogic => _triggerType == StoryStartSchema.Logic;
    public string ActorId
    {
        get => ReadString(StoryStartSchema.ActorIdProperty);
        set
        {
            SetString(StoryStartSchema.ActorIdProperty, value);
            OnPropertyChanged(nameof(ActorOptions));
            OnPropertyChanged(nameof(SelectedActor));
        }
    }

    private IReadOnlyList<CanonicalSessionSpeakerOption> _cachedActorOptions = [];
    public IReadOnlyList<CanonicalSessionSpeakerOption> ActorOptions
    {
        get
        {
            var options = _owner.ActorOptions.ToList();
            if (!string.IsNullOrWhiteSpace(ActorId)
                && !options.Any(option => string.Equals(option.Id, ActorId, StringComparison.Ordinal)))
                options.Add(new CanonicalSessionSpeakerOption(ActorId, $"未解析角色：{ActorId}", false));
            if (!_cachedActorOptions.SequenceEqual(options)) _cachedActorOptions = options;
            return _cachedActorOptions;
        }
    }

    public CanonicalSessionSpeakerOption? SelectedActor
    {
        get => ActorOptions.FirstOrDefault(option => string.Equals(option.Id, ActorId, StringComparison.Ordinal));
        set
        {
            if (value is not null) ActorId = value.Id;
        }
    }
    public string DimensionText
    {
        get => _dimensionText;
        set => SetNumberField(StoryStartSchema.DimensionProperty, value, integer: true,
            ref _dimensionText, ref _dimensionError, nameof(DimensionText), nameof(DimensionError));
    }
    public string DimensionError => _dimensionError;
    public string XText
    {
        get => _xText;
        set => SetNumberField(StoryStartSchema.XProperty, value, integer: false,
            ref _xText, ref _xError, nameof(XText), nameof(XError));
    }
    public string XError => _xError;
    public string YText
    {
        get => _yText;
        set => SetNumberField(StoryStartSchema.YProperty, value, integer: false,
            ref _yText, ref _yError, nameof(YText), nameof(YError));
    }
    public string YError => _yError;
    public string ZText
    {
        get => _zText;
        set => SetNumberField(StoryStartSchema.ZProperty, value, integer: false,
            ref _zText, ref _zError, nameof(ZText), nameof(ZError));
    }
    public string ZError => _zError;
    public string RadiusText
    {
        get => _radiusText;
        set => SetNumberField(StoryStartSchema.RadiusProperty, value, integer: false,
            ref _radiusText, ref _radiusError, nameof(RadiusText), nameof(RadiusError));
    }
    public string RadiusError => _radiusError;

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

    internal void RefreshCommandStates()
    {
        RemoveCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
    }

    internal void UpdateProjection(StoryStartTriggerSlot slot)
    {
        _projecting = true;
        try
        {
            var typeChanged = _triggerType != slot.TriggerType;
            var propertiesChanged = !JsonElement.DeepEquals(_triggerProperties, slot.TriggerProperties);
            if (_displayName != slot.DisplayName)
            {
                _displayName = slot.DisplayName;
                OnPropertyChanged(nameof(DisplayName));
            }
            Order = slot.Order;
            _triggerType = slot.TriggerType;
            if (typeChanged || propertiesChanged)
            {
                _triggerProperties = slot.TriggerProperties.Clone();
                LoadPropertyFields(clearErrors: true);
                NotifyPropertyFields();
            }
            if (typeChanged)
                foreach (var name in new[] { nameof(TriggerType), nameof(TriggerTypeOptions), nameof(SelectedTriggerType),
                    nameof(IsActorInteraction), nameof(IsRegionEntry), nameof(IsEnterStory), nameof(IsLogic) })
                    OnPropertyChanged(name);
        }
        finally { _projecting = false; }
    }

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
        if (_projecting || !IsActorInteraction || string.IsNullOrWhiteSpace(value)) return;
        if (_owner.SetStoryStartTriggerProperty(Identity, property,
                JsonSerializer.SerializeToElement(value.Trim())))
        {
            _triggerProperties = SetLocalProperty(property, JsonSerializer.SerializeToElement(value.Trim()));
            NotifyPropertyFields();
        }
    }

    private void SetNumberField(string property, string? value, bool integer,
        ref string text, ref string error, string textProperty, string errorProperty)
    {
        if (_projecting || !IsRegionEntry) return;
        var next = value ?? string.Empty;
        if (string.Equals(text, next, StringComparison.Ordinal)) return;
        text = next;
        OnPropertyChanged(textProperty);

        JsonElement serialized = default;
        var valid = integer
            ? int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue)
                && (serialized = JsonSerializer.SerializeToElement(integerValue)).ValueKind == JsonValueKind.Number
            : double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue)
                && double.IsFinite(numberValue)
                && (property != StoryStartSchema.RadiusProperty || numberValue > 0)
                && (serialized = JsonSerializer.SerializeToElement(numberValue)).ValueKind == JsonValueKind.Number;

        var message = valid ? string.Empty
            : property == StoryStartSchema.RadiusProperty ? "半径必须是大于 0 的数字。"
            : integer ? "请输入有效的整数。" : "请输入有效的数字。";
        if (!string.Equals(error, message, StringComparison.Ordinal))
        {
            error = message;
            OnPropertyChanged(errorProperty);
        }
        _owner.Host.SetAuthoringIssue($"{_owner.NodeId}:trigger:{Identity}:{property}",
            string.IsNullOrEmpty(message) ? null : new ValidationIssue(
                "graph.story.start.trigger.authoring_invalid", message, property,
                ValidationSeverity.Error, _owner.NodeId));
        if (!valid) return;
        if (_owner.SetStoryStartTriggerProperty(Identity, property, serialized))
        {
            _triggerProperties = SetLocalProperty(property, serialized);
            NotifyPropertyFields();
        }
    }

    private void LoadPropertyFields(bool clearErrors)
    {
        _dimensionText = ReadNumber(StoryStartSchema.DimensionProperty);
        _xText = ReadNumber(StoryStartSchema.XProperty);
        _yText = ReadNumber(StoryStartSchema.YProperty);
        _zText = ReadNumber(StoryStartSchema.ZProperty);
        _radiusText = ReadNumber(StoryStartSchema.RadiusProperty);
        if (!clearErrors) return;
        foreach (var property in new[] { StoryStartSchema.DimensionProperty, StoryStartSchema.XProperty,
                     StoryStartSchema.YProperty, StoryStartSchema.ZProperty, StoryStartSchema.RadiusProperty })
            _owner.Host.SetAuthoringIssue($"{_owner.NodeId}:trigger:{Identity}:{property}", null);
        _dimensionError = _xError = _yError = _zError = _radiusError = string.Empty;
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
        OnPropertyChanged(nameof(ActorOptions));
        OnPropertyChanged(nameof(SelectedActor));
        OnPropertyChanged(nameof(DimensionText));
        OnPropertyChanged(nameof(DimensionError));
        OnPropertyChanged(nameof(XText));
        OnPropertyChanged(nameof(XError));
        OnPropertyChanged(nameof(YText));
        OnPropertyChanged(nameof(YError));
        OnPropertyChanged(nameof(ZText));
        OnPropertyChanged(nameof(ZError));
        OnPropertyChanged(nameof(RadiusText));
        OnPropertyChanged(nameof(RadiusError));
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
